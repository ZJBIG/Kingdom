param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$OutputPath = (Join-Path $ProjectPath "Logs/codex-baseline.txt")
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force (Split-Path $OutputPath) | Out-Null

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("Kingdom baseline $(Get-Date -Format o)")
$lines.Add("Project: $ProjectPath")
$lines.Add("")

if (Test-Path (Join-Path $ProjectPath ".git")) {
    $lines.Add("=== git status --short ===")
    $lines.AddRange([string[]](& git -C $ProjectPath status --short))
    $lines.Add("")
    $lines.Add("=== git log --oneline -10 ===")
    $lines.AddRange([string[]](& git -C $ProjectPath log --oneline -10))
    $lines.Add("")
} else {
    $lines.Add("No .git directory found.")
    $lines.Add("")
}

$scriptRoot = Join-Path $ProjectPath "Assets/Resources/Script"
$lines.Add("C# files: $((Get-ChildItem $scriptRoot -Filter *.cs -Recurse).Count)")
$lines.Add("Definition assets: $((Get-ChildItem (Join-Path $ProjectPath 'Assets/Resources/Datas') -Filter *.asset -Recurse).Count)")
$lines.Add("Prefabs: $((Get-ChildItem (Join-Path $ProjectPath 'Assets') -Filter *.prefab -Recurse).Count)")
$lines.Add("Scenes: $((Get-ChildItem (Join-Path $ProjectPath 'Assets') -Filter *.unity -Recurse).Count)")
$lines | Set-Content $OutputPath -Encoding UTF8
Write-Host "Baseline written to $OutputPath"
