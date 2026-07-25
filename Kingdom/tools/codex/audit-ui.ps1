param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$OutputPath = (Join-Path $ProjectPath "Logs/codex-ui-audit.txt")
)
$ErrorActionPreference = "Stop"
$root = Join-Path $ProjectPath "Assets/Resources/Script"
New-Item -ItemType Directory -Force (Split-Path $OutputPath) | Out-Null

$checks = [ordered]@{
    "Viewer Update loops" = "class\s+\w*Viewer|void\s+Update\s*\("
    "UI refresh coroutines" = "RefreshLoop|UpdateUI|StartCoroutine"
    "Manual dynamic heights" = "sizeDelta|150f|200f|230f|requirementRows\s*\*\s*50"
    "Off-screen hiding" = "OutsideTheWindows|ViewerLocalPos|-10000"
    "Hidden parents" = "\bHide\b|SetParent"
    "Scene searches" = "FindObjectOfType"
    "GetComponent in UI refresh/layout" = "GetComponent<"
    "Destroy/Instantiate requirement rows" = "Destroy\(|Instantiate\("
}

$files = Get-ChildItem $root -Filter *.cs -Recurse |
    Where-Object { $_.FullName -match "\\(UI|Resource|Building|Research|Setting)\\" }

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("Kingdom UI audit $(Get-Date -Format o)")
$lines.Add("")
foreach ($entry in $checks.GetEnumerator()) {
    $lines.Add("=== $($entry.Key) ===")
    $matches = $files | Select-String -Pattern $entry.Value -AllMatches
    if (-not $matches) { $lines.Add("(none)") }
    else {
        foreach ($m in $matches) {
            $relative = $m.Path.Substring($ProjectPath.Length).TrimStart('\')
            $lines.Add("${relative}:$($m.LineNumber): $($m.Line.Trim())")
        }
    }
    $lines.Add("")
}
$lines | Set-Content $OutputPath -Encoding UTF8
Write-Host "UI audit written to $OutputPath"
