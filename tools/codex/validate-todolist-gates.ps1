param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
)

$ErrorActionPreference = "Stop"
$toolRoot = Join-Path $ProjectPath "tools/codex"

& (Join-Path $toolRoot "validate-guidance.ps1") -ProjectPath $ProjectPath
& (Join-Path $toolRoot "validate-ui-contract.ps1") -ProjectPath $ProjectPath
& (Join-Path $toolRoot "validate-android-settings.ps1") -ProjectPath $ProjectPath
& (Join-Path $toolRoot "verify-yaml-references.ps1") -ProjectPath $ProjectPath
& (Join-Path $toolRoot "content-closure-check.ps1")
& (Join-Path $toolRoot "building-resource-flow-check.ps1")

foreach ($scriptName in @("run-unity-tests.ps1", "build-android.ps1", "validate-ui-contract.ps1")) {
    $tokens = $null
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $toolRoot $scriptName),
        [ref]$tokens,
        [ref]$errors) | Out-Null
    if ($errors.Count -gt 0)
        { throw "PowerShell syntax errors in $scriptName" }
}

Write-Host "Todolist static gates passed. Unity runtime and device gates remain explicit separate steps."
