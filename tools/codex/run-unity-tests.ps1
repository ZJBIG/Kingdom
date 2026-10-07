param(
    [ValidateSet("EditMode", "PlayMode")]
    [string]$Platform = "EditMode",
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$UnityPath,
    [string]$ResultsPath,
    [string]$LogPath,
    [string]$LatestErrorsPath,
    [string]$TestFilter,
    [int]$TimeoutSeconds = 600
)
$ErrorActionPreference = "Stop"
if (-not $UnityPath) {
    $UnityPath = & (Join-Path $PSScriptRoot "find-unity.ps1") -ProjectPath $ProjectPath
}
$editorInstancePath = Join-Path $ProjectPath "Library/EditorInstance.json"
if (Test-Path -LiteralPath $editorInstancePath) {
    $lockConflict = $null
    try {
        $editorInstance = Get-Content -LiteralPath $editorInstancePath -Raw | ConvertFrom-Json
        $existingUnity = Get-Process -Id ([int]$editorInstance.process_id) -ErrorAction SilentlyContinue
        if ($existingUnity -and $existingUnity.ProcessName -eq "Unity") {
            $lockConflict = "Unity project is already open by PID $($editorInstance.process_id). Continue other work instead of waiting for the project lock."
        }
    }
    catch {
        Write-Warning "Could not inspect Unity project lock metadata: $($_.Exception.Message)"
    }
    if ($lockConflict) {
        throw $lockConflict
    }
}
if (-not $ResultsPath) {
    $ResultsPath = Join-Path $ProjectPath "TestResults/$Platform-results.xml"
}
if (-not $LogPath) {
    $LogPath = Join-Path $ProjectPath "Logs/codex-$($Platform.ToLower())-tests.log"
}
if (-not $LatestErrorsPath) {
    $LatestErrorsPath = Join-Path $ProjectPath "TestResults/Latest-Test-Errors.txt"
}
New-Item -ItemType Directory -Force (Split-Path $ResultsPath) | Out-Null
New-Item -ItemType Directory -Force (Split-Path $LogPath) | Out-Null
New-Item -ItemType Directory -Force (Split-Path $LatestErrorsPath) | Out-Null
Remove-Item -LiteralPath $ResultsPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $LogPath -Force -ErrorAction SilentlyContinue

function Write-LatestTestReport([string]$Result, [string]$Details) {
    @(
        "Kingdom Unity Test Runner - Latest Error Report"
        "Generated: $(Get-Date -Format o)"
        "Platform: $Platform"
        "Result: $Result"
        $Details
    ) | Set-Content -LiteralPath $LatestErrorsPath -Encoding UTF8
}

Write-LatestTestReport "NotStarted" "Test run started; no result XML is available yet."

$arguments = @(
    "-batchmode",
    "-nographics",
    "-projectPath", $ProjectPath,
    "-runTests",
    "-testPlatform", $Platform,
    "-testResults", $ResultsPath,
    "-logFile", $LogPath
)
if ($TestFilter) {
    $arguments += @("-testFilter", $TestFilter)
}
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments `
    -PassThru -WindowStyle Hidden
$completed = $process.WaitForExit($TimeoutSeconds * 1000)
if (-not $completed) {
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    Write-LatestTestReport "Failed(Timeout)" "Unity $Platform tests timed out after $TimeoutSeconds seconds. Log=$LogPath"
    Write-Error "Unity $Platform tests timed out after $TimeoutSeconds seconds. Log=$LogPath"
    exit 1
}
$unityExitCode = $process.ExitCode
if (-not (Test-Path $ResultsPath)) {
    Write-LatestTestReport "Failed(NoXml)" "ExitCode=$unityExitCode; Unity produced no valid result XML. Log=$LogPath"
    Write-Error "Unity $Platform tests failed or produced no XML. ExitCode=$unityExitCode. Log=$LogPath"
    exit 1
}

try {
    [xml]$xml = Get-Content -Encoding UTF8 $ResultsPath -Raw
}
catch {
    Write-LatestTestReport "Failed(InvalidXml)" "Unity produced invalid result XML: $ResultsPath`n$($_.Exception.Message)"
    Write-Error "Unity $Platform tests produced invalid XML. Results=$ResultsPath"
    exit 1
}
$run = $xml.'test-run'
if (-not $run) {
    Write-LatestTestReport "Failed(InvalidXml)" "Unity result XML is missing the test-run element: $ResultsPath"
    Write-Error "Unity $Platform tests produced XML without a test-run element. Results=$ResultsPath"
    exit 1
}
foreach ($attribute in @('total', 'passed', 'failed', 'skipped')) {
    if ($null -eq $run.GetAttribute($attribute) -or [string]::IsNullOrWhiteSpace($run.GetAttribute($attribute))) {
        Write-LatestTestReport "Failed(InvalidXml)" "Unity result XML is missing test-run/${attribute}: $ResultsPath"
        Write-Error "Unity $Platform tests produced incomplete result XML (missing $attribute). Results=$ResultsPath"
        exit 1
    }
}
try {
    $failed = [int]$run.failed
    $total = [int]$run.total
    $passed = [int]$run.passed
    $skipped = [int]$run.skipped
    if ($failed -lt 0 -or $total -lt 0 -or $passed -lt 0 -or $skipped -lt 0) {
        throw "test-run counts cannot be negative"
    }
}
catch {
    Write-LatestTestReport "Failed(InvalidXml)" "Unity result XML has invalid test-run counts: $ResultsPath`n$($_.Exception.Message)"
    Write-Error "Unity $Platform tests produced invalid test-run counts. Results=$ResultsPath"
    exit 1
}
$reportLines = @(
    "Total: $total Passed:$passed Failed:$failed Skipped:$skipped"
)
$failureCases = @($xml.SelectNodes('//test-case[@result="Failed"]'))
for ($i = 0; $i -lt $failureCases.Count; $i++) {
    $failureCase = $failureCases[$i]
    $reportLines += ""
    $reportLines += "=== FAILURE $($i + 1) ==="
    $reportLines += "Test: $($failureCase.fullname)"
    if ($failureCase.failure -and $failureCase.failure.message) {
        $reportLines += "Message: $($failureCase.failure.message)"
    }
    if ($failureCase.failure -and $failureCase.failure.'stack-trace') {
        $reportLines += "StackTrace: $($failureCase.failure.'stack-trace')"
    }
}
$result = if ($unityExitCode -ne 0 -or $failed -gt 0 -or $total -eq 0) { "Failed" } else { "Passed" }
if ($unityExitCode -ne 0) {
    $reportLines += "UnityExitCode: $unityExitCode"
}
Write-LatestTestReport $result ($reportLines -join [Environment]::NewLine)
if ($unityExitCode -ne 0) {
    Write-Error "Unity $Platform runner exited with code $unityExitCode. Results=$ResultsPath Log=$LogPath"
    exit 1
}
if ($total -eq 0) {
    Write-Error "Unity $Platform runner completed with zero test cases. This is not acceptance evidence. Results=$ResultsPath"
    exit 1
}
if ($failed -gt 0) {
    Write-Error "$failed of $total $Platform tests failed. Results=$ResultsPath"
    exit 1
}
Write-Host "$Platform tests passed. Total=$total Results=$ResultsPath Log=$LogPath"
