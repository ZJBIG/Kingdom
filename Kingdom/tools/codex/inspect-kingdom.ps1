param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$OutputPath = (Join-Path $ProjectPath "Logs/codex-repository-audit.txt")
)
$ErrorActionPreference = "Stop"
$scriptRoot = Join-Path $ProjectPath "Assets/Resources/Script"
if (-not (Test-Path $scriptRoot)) { throw "Game script directory not found: $scriptRoot" }
New-Item -ItemType Directory -Force (Split-Path $OutputPath) | Out-Null

$patterns = [ordered]@{
    "BigNumber references" = "\bBigNumber\b"
    "Legacy Pair<Resource,string>" = "Pair\s*<\s*Resource\s*,\s*string\s*>"
    "Canonical Pair<Resource,ExpantaNum>" = "Pair\s*<\s*Resource\s*,\s*ExpantaNum\s*>"
    "Gameplay/UI coroutines" = "StartCoroutine|IEnumerator|WaitForSeconds"
    "Update methods" = "\bvoid\s+Update\s*\("
    "Transform.GetChild" = "Transform\.GetChild|\.GetChild\s*\("
    "Off-screen hiding" = "OutsideTheWindows|ViewerLocalPos|-10000"
    "Scene searches" = "FindObjectOfType"
    "Resources.Load" = "Resources\.Load"
    "Reflection" = "System\.Reflection|GetCustomAttributes|GetField\s*\("
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("Kingdom repository audit $(Get-Date -Format o)")
$lines.Add("Project: $ProjectPath")
$lines.Add("")
$files = Get-ChildItem $scriptRoot -Filter *.cs -Recurse

foreach ($entry in $patterns.GetEnumerator()) {
    $lines.Add("=== $($entry.Key) / $($entry.Value) ===")
    $matches = $files | Select-String -Pattern $entry.Value -AllMatches
    if (-not $matches) {
        $lines.Add("(none)")
    } else {
        foreach ($match in $matches) {
            $relative = $match.Path.Substring($ProjectPath.Length).TrimStart('\')
            $lines.Add("${relative}:$($match.LineNumber): $($match.Line.Trim())")
        }
    }
    $lines.Add("")
}

$lines | Set-Content $OutputPath -Encoding UTF8
Write-Host "Audit written to $OutputPath"
