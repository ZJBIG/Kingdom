param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
)

$ErrorActionPreference = "Stop"
$toolRoot = Join-Path $ProjectPath "tools/codex"

function Invoke-Gate([string]$ScriptPath, [hashtable]$Arguments = @{}) {
    $global:LASTEXITCODE = 0
    & $ScriptPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Gate failed with exit code $LASTEXITCODE`: $ScriptPath"
    }
}

Invoke-Gate (Join-Path $toolRoot "validate-guidance.ps1") @{ ProjectPath = $ProjectPath }
Invoke-Gate (Join-Path $toolRoot "validate-ui-contract.ps1") @{ ProjectPath = $ProjectPath }
Invoke-Gate (Join-Path $toolRoot "validate-android-settings.ps1") @{ ProjectPath = $ProjectPath }
Invoke-Gate (Join-Path $toolRoot "verify-yaml-references.ps1") @{ ProjectPath = $ProjectPath }
Invoke-Gate (Join-Path $toolRoot "content-closure-check.ps1") @{ ProjectRoot = $ProjectPath }
Invoke-Gate (Join-Path $toolRoot "building-resource-flow-check.ps1") @{ ProjectPath = $ProjectPath }

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
