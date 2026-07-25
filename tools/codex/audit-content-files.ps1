param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)

$ErrorActionPreference = 'Stop'
$dataRoot = Join-Path $ProjectRoot 'Assets/Resources/Datas'
$scriptRoot = Join-Path $ProjectRoot 'Assets/Resources/Script'

Write-Output "ProjectRoot=$ProjectRoot"
Write-Output "Resources=$((Get-ChildItem (Join-Path $dataRoot 'Resource') -Recurse -Filter *.asset).Count)"
Write-Output "Buildings=$((Get-ChildItem (Join-Path $dataRoot 'Building') -Recurse -Filter *.asset).Count)"
Write-Output "Researches=$((Get-ChildItem (Join-Path $dataRoot 'Research') -Recurse -Filter *.asset).Count)"

Write-Output 'DEBUG_GOLD_REFERENCES'
Get-ChildItem $scriptRoot -Recurse -Filter *.cs |
    Select-String -Pattern 'AddResource\(DataBase<Resource>\.Find\("Gold"\)\)'

Write-Output 'CAPACITY_REFERENCES'
Get-ChildItem $scriptRoot -Recurse -Filter *.cs |
    Select-String -Pattern 'Resource.*Capacity|Capacity.*Resource|MaxAmount' |
    Where-Object { $_.Line -notmatch 'Food' }

Write-Output 'PLAYMODE_RESULT'
$playMode = Join-Path $ProjectRoot 'TestResults/PlayMode-results.xml'
if (Test-Path $playMode) {
    Select-String -Path $playMode -Pattern 'testcasecount="[0-9]+"' | Select-Object -First 1
} else {
    Write-Output 'missing'
}
