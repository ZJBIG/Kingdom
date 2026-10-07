# Compile gate for the Unity test assemblies, without launching Unity.
#
# Why this exists: build-developer-assembly.ps1 deliberately strips Assets/Tests/*,
# nunit and TestRunner references so the shipped developer assemblies stay lean.
# run-unity-tests.ps1 runs the tests but needs Unity's batchmode runner, and on
# this machine batchmode stalls during the initial asset-database refresh. That
# left the test assemblies with no reachable compile gate at all -- a broken test
# file could sit undetected indefinitely.
#
# This script reuses the Bee response file Unity already wrote, keeps the test
# sources and nunit/TestRunner references, and swaps the stale Runtime reference
# assembly for the one just built from current sources. It compiles, it does not
# run: it answers "does the test code still build against today's Runtime?".
#
# Invoke through Kingdom.DeveloperTests.csproj so MSBuild spawns the compiler.
# Writes only under Temp/DeveloperTests; no tracked source is modified.
param(
    [ValidateSet("All", "Editor", "PlayMode")]
    [string]$Target = "All",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,

    [string]$UnityPath
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path $ProjectPath).Path
$artifactRoot = Join-Path $repositoryRoot "Library\Bee\artifacts"
$outputDirectory = Join-Path $repositoryRoot "Temp\DeveloperTests"
$currentRuntime = Join-Path $repositoryRoot "Temp\DeveloperBuild\Kingdom.Runtime.Editor.dll"

if (-not (Test-Path -LiteralPath $artifactRoot)) {
    throw "Unity compile metadata is missing. Open the project in Unity once, then try again."
}
if (-not (Test-Path -LiteralPath $currentRuntime)) {
    throw "Build Kingdom.Editor.Developer.csproj first; missing $currentRuntime"
}
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$specs = [ordered]@{
    Editor = @{
        ResponseFile = "Assembly-CSharp-Editor.rsp"
        AssemblyName = "Kingdom.EditorTests"
        ExtraSourceRoot = "Assets\Tests\Editor"
    }
    PlayMode = @{
        ResponseFile = "Kingdom.PlayModeTests.rsp"
        AssemblyName = "Kingdom.PlayModeTests"
        ExtraSourceRoot = "Assets\Tests\PlayMode"
    }
}
$selected = if ($Target -eq "All") { @($specs.Keys) } else { @($Target) }

$unityExecutable = if ($UnityPath) {
    $UnityPath
}
else {
    & (Join-Path $PSScriptRoot "find-unity.ps1") -ProjectPath $repositoryRoot
}
$unityEditorDirectory = Split-Path -Parent $unityExecutable
$unityDotnet = Join-Path $unityEditorDirectory "Data\NetCoreRuntime\dotnet.exe"
$compilerPath = Join-Path $unityEditorDirectory "Data\DotNetSdkRoslyn\csc.dll"
if (-not (Test-Path -LiteralPath $compilerPath)) {
    throw "Unity Roslyn compiler not found next to $unityExecutable"
}

$summary = @()
$worstExitCode = 0

foreach ($name in $selected) {
    $spec = $specs[$name]
    # The dag directory name is a hash Unity regenerates, so locate the response
    # file by name instead of hardcoding it.
    $sourceResponseFile = Get-ChildItem -LiteralPath $artifactRoot -Recurse -Filter $spec.ResponseFile |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $sourceResponseFile) {
        throw "Unity response file not found under $artifactRoot`: $($spec.ResponseFile)"
    }

    $outputAssembly = Join-Path $outputDirectory "$($spec.AssemblyName).dll"
    $referenceAssembly = Join-Path $outputDirectory "$($spec.AssemblyName).ref.dll"
    $filteredResponseFile = Join-Path $outputDirectory "$($spec.AssemblyName).$($Configuration.ToLowerInvariant()).rsp"
    $compilerLog = Join-Path $outputDirectory "$($spec.AssemblyName).compiler-output.txt"

    $filteredLines = foreach ($line in Get-Content -LiteralPath $sourceResponseFile.FullName) {
        if ($line -match "^-out:" -or $line -match "^-refout:") { continue }
        if ($line -match 'Kingdom\.Runtime\.ref\.dll') {
            "-r:`"$($currentRuntime.Replace('\', '/'))`""
            continue
        }
        $line
    }

    # Pick up test sources added after the response file was generated.
    $knownSources = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($line in $filteredLines) {
        if ($line -match '^"(?<path>Assets/.+\.cs)"$') {
            [void]$knownSources.Add($Matches.path)
        }
    }
    $addedSources = @()
    $extraRoot = Join-Path $repositoryRoot $spec.ExtraSourceRoot
    if (Test-Path -LiteralPath $extraRoot) {
        foreach ($sourceFile in Get-ChildItem -LiteralPath $extraRoot -Recurse -Filter "*.cs") {
            $relativeSource = $sourceFile.FullName.Substring($repositoryRoot.Length + 1).Replace('\', '/')
            if ($knownSources.Add($relativeSource)) {
                $filteredLines += "`"$relativeSource`""
                $addedSources += $relativeSource
            }
        }
    }

    $compilerArguments = @(
        "-out:`"$($outputAssembly.Replace('\', '/'))`"",
        "-refout:`"$($referenceAssembly.Replace('\', '/'))`""
    )
    $compilerArguments += $filteredLines
    Set-Content -LiteralPath $filteredResponseFile -Value $compilerArguments -Encoding UTF8

    Push-Location $repositoryRoot
    try {
        $compilerOutput = & $unityDotnet $compilerPath "@$filteredResponseFile" 2>&1
        $compilerExitCode = $LASTEXITCODE
    } finally {
        Pop-Location
    }

    # A swallowed native process leaves $LASTEXITCODE as $null. Reporting only
    # the exit code then let the gate claim success while nothing compiled, so
    # "did the compiler actually run" and "did it produce an assembly" are both
    # recorded and both count as failure.
    $compilerRan = $null -ne $compilerExitCode
    $outputProduced = Test-Path -LiteralPath $outputAssembly

    $sourceCount = @($filteredLines | Where-Object { $_ -match '^"Assets/' }).Count
    $perTarget = @()
    $perTarget += "target=$name"
    $perTarget += "  response_file=$($sourceResponseFile.FullName)"
    $perTarget += "  source_files=$sourceCount"
    $perTarget += "  sources_added_beyond_bee_snapshot=$($addedSources.Count)"
    foreach ($s in $addedSources) { $perTarget += "    + $s" }
    $perTarget += "  compiler_ran=$compilerRan"
    $perTarget += "  compiler_exit=$compilerExitCode"
    $perTarget += "  output_produced=$outputProduced"
    $perTarget += "  output=$outputAssembly"
    if ($compilerOutput) {
        $perTarget += "  --- compiler output ---"
        $perTarget += ($compilerOutput | ForEach-Object { "  $_" })
    }
    if (-not $compilerRan) {
        $perTarget += "  FAILED: the compiler did not run (exit code unreadable); no assembly was produced."
    }
    elseif (-not $outputProduced) {
        $perTarget += "  FAILED: the compiler ran but produced no assembly at $outputAssembly."
    }
    $perTarget | Out-File -FilePath $compilerLog -Encoding utf8
    $summary += $perTarget

    if (-not $compilerRan -or -not $outputProduced) {
        $worstExitCode = 1
    }
    elseif ($compilerExitCode -ne 0 -and $worstExitCode -eq 0) {
        $worstExitCode = $compilerExitCode
    }
}

$summary += "worst_compiler_exit=$worstExitCode"
$summaryPath = Join-Path $outputDirectory "summary.txt"
$summary | Out-File -FilePath $summaryPath -Encoding utf8
$summary | ForEach-Object { Write-Host $_ }

if ($worstExitCode -eq 0) {
    Write-Host "Test assemblies compiled ($Configuration): $($selected -join ', '). Summary=$summaryPath"
}
else {
    Write-Host "Test assemblies FAILED to compile ($Configuration): $($selected -join ', '). Summary=$summaryPath"
}
exit $worstExitCode
