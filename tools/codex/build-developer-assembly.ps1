param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Runtime", "Editor")]
    [string]$Assembly,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("Player", "Editor")]
    [string]$RuntimeFlavor = "Player"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$artifactRoot = Join-Path $repositoryRoot "Library\Bee\artifacts"
$outputDirectory = Join-Path $repositoryRoot "Temp\DeveloperBuild"

if (-not (Test-Path -LiteralPath $artifactRoot)) {
    throw "Unity compile metadata is missing. Open the project in Unity once, then build Kingdom.Developer.sln again."
}

$responseFileName = if ($Assembly -eq "Runtime") {
    "Kingdom.Runtime.rsp"
}
else {
    "Assembly-CSharp-Editor.rsp"
}

$responseCandidates = Get-ChildItem -LiteralPath $artifactRoot -Recurse -Filter $responseFileName |
    Sort-Object LastWriteTime -Descending
if ($Assembly -eq "Runtime") {
    $requiresUnityEditor = $RuntimeFlavor -eq "Editor"
    $sourceResponseFile = $responseCandidates | Where-Object {
        $hasUnityEditor = Select-String -LiteralPath $_.FullName -SimpleMatch "-define:UNITY_EDITOR" -Quiet
        $hasUnityEditor -eq $requiresUnityEditor
    } | Select-Object -First 1
}
else {
    $sourceResponseFile = $responseCandidates | Select-Object -First 1
}
if ($null -eq $sourceResponseFile) {
    throw "Unity compile metadata '$responseFileName' was not found. Let Unity finish compiling, then try again."
}

$projectVersionFile = Join-Path $repositoryRoot "ProjectSettings\ProjectVersion.txt"
$editorVersionLine = Get-Content -LiteralPath $projectVersionFile |
    Where-Object { $_ -like "m_EditorVersion:*" } |
    Select-Object -First 1
$editorVersion = ($editorVersionLine -split ":", 2)[1].Trim()

$unityEditorPath = Get-Process -Name Unity -ErrorAction SilentlyContinue |
    ForEach-Object { try { $_.Path } catch { $null } } |
    Where-Object { $_ -and $_ -like "*$editorVersion*" } |
    Select-Object -First 1

$editorCandidates = @()
if ($unityEditorPath) {
    $editorCandidates += Split-Path -Parent $unityEditorPath
}
$editorCandidates += "D:\Unity\Hub\Editor\$editorVersion\Editor"
# Join-Path throws on a null/empty -Path, so probe the env vars before using
# them: ProgramFiles and ProgramFiles(x86) can both be undefined.
if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
    $editorCandidates += (Join-Path $env:ProgramFiles "Unity\Hub\Editor\$editorVersion\Editor")
}
if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
    $editorCandidates += (Join-Path ${env:ProgramFiles(x86)} "Unity\Hub\Editor\$editorVersion\Editor")
}

$unityEditorDirectory = $editorCandidates |
    Where-Object { $_ -and $_.Trim() } |
    Where-Object { Test-Path -LiteralPath (Join-Path $_ "Data\DotNetSdkRoslyn\csc.dll") } |
    Select-Object -First 1

if (-not $unityEditorDirectory) {
    throw "Unity $editorVersion compiler was not found."
}

$unityDotnet = Join-Path $unityEditorDirectory "Data\NetCoreRuntime\dotnet.exe"
$compiler = Join-Path $unityEditorDirectory "Data\DotNetSdkRoslyn\csc.dll"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$assemblyName = if ($Assembly -eq "Runtime") {
    if ($RuntimeFlavor -eq "Editor") { "Kingdom.Runtime.Editor" } else { "Kingdom.Runtime" }
}
else {
    "Kingdom.Editor"
}
$outputAssembly = Join-Path $outputDirectory "$assemblyName.dll"
$referenceAssembly = Join-Path $outputDirectory "$assemblyName.ref.dll"
$filteredResponseFile = Join-Path $outputDirectory "$assemblyName.$($Configuration.ToLowerInvariant()).rsp"
$runtimeAssembly = Join-Path $outputDirectory "Kingdom.Runtime.Editor.dll"

if ($Assembly -eq "Editor") {
    & $PSCommandPath Runtime -Configuration $Configuration -RuntimeFlavor Editor
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
$filteredLines = foreach ($line in Get-Content -LiteralPath $sourceResponseFile.FullName) {
    if ($line -match "^-out:" -or $line -match "^-refout:") { continue }
    if ($line -eq "-define:UNITY_INCLUDE_TESTS") { continue }
    if ($line -match '^"(?<path>Assets/.+\.cs)"$' -and
        -not (Test-Path -LiteralPath (Join-Path $repositoryRoot $Matches.path))) {
        continue
    }

    if ($Assembly -eq "Editor") {
        if ($line -match '^"Assets/Tests/') { continue }
        if ($line -match 'Assets/Editor/Codex/LatestTestErrorReport\.cs') { continue }
        if ($line -match 'Assets/Editor/KingdomPerfTestTools\.cs') { continue }
        if ($line -match 'Assembly-CSharp\.ref\.dll') { continue }
        if ($line -match 'Kingdom\.Runtime\.ref\.dll') { continue }
        if ($line -match '(?i)nunit|TestRunner') { continue }
    }

    $line
}

# Unity's Bee response file can lag behind a newly added production script
# while the Editor is busy. Keep references/defines from Bee, but discover the
# current production source set directly so the developer solution is complete.
$knownSources = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
foreach ($line in $filteredLines) {
    if ($line -match '^"(?<path>Assets/.+\.cs)"$') {
        [void]$knownSources.Add($Matches.path)
    }
}
$sourceRoot = if ($Assembly -eq "Runtime") {
    Join-Path $repositoryRoot "Assets\Resources\Script"
} else {
    Join-Path $repositoryRoot "Assets\Editor"
}
foreach ($sourceFile in Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter "*.cs") {
    $relativeSource = $sourceFile.FullName.Substring($repositoryRoot.Length + 1).Replace('\', '/')
    if ($Assembly -eq "Editor" -and
        ($relativeSource -eq "Assets/Editor/Codex/LatestTestErrorReport.cs" -or
         $relativeSource -eq "Assets/Editor/KingdomPerfTestTools.cs")) {
        continue
    }
    if ($knownSources.Add($relativeSource)) {
        $filteredLines += "`"$relativeSource`""
    }
}

$compilerArguments = @(
    "-out:`"$($outputAssembly.Replace('\', '/'))`"",
    "-refout:`"$($referenceAssembly.Replace('\', '/'))`""
)
if ($Assembly -eq "Editor") {
    $compilerArguments += "-r:`"$($runtimeAssembly.Replace('\', '/'))`""
}
$compilerArguments += $filteredLines

Set-Content -LiteralPath $filteredResponseFile -Value $compilerArguments -Encoding UTF8

Push-Location $repositoryRoot
try {
    & $unityDotnet $compiler "@$filteredResponseFile"
    $compilerExitCode = $LASTEXITCODE
} finally {
    Pop-Location
}

# A swallowed native process leaves $LASTEXITCODE as $null, and `exit $null`
# returns 0 -- so without this check the script reported success while never
# compiling anything. Treat "no exit code" and "no output assembly" as failures.
if ($null -eq $compilerExitCode) {
    throw "The compiler did not actually run: its exit code could not be read. Do not treat this as a successful compile."
}
if ($compilerExitCode -ne 0) {
    exit $compilerExitCode
}
if (-not (Test-Path -LiteralPath $outputAssembly)) {
    throw "The compiler returned 0 but produced no assembly at $outputAssembly."
}

Write-Host "Built $assemblyName ($Configuration) without test sources: $outputAssembly"
