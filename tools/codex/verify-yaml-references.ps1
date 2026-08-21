param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$OutputPath = (Join-Path $ProjectPath "Logs/codex-yaml-reference-audit.txt")
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force (Split-Path $OutputPath) | Out-Null

$guidToPath = @{}
Get-ChildItem (Join-Path $ProjectPath "Assets") -Filter *.meta -Recurse | ForEach-Object {
    $match = Select-String -Path $_.FullName -Pattern '^guid:\s*([0-9a-fA-F]+)' | Select-Object -First 1
    if ($match) {
        $guid = $match.Matches[0].Groups[1].Value
        $assetPath = $_.FullName.Substring(0, $_.FullName.Length - 5)
        $guidToPath[$guid] = $assetPath
    }
}

$unresolved = [System.Collections.Generic.List[string]]::new()
Get-ChildItem (Join-Path $ProjectPath "Assets") -Recurse -File |
    Where-Object { $_.Extension -in ".unity", ".prefab", ".asset" } |
    ForEach-Object {
        $matches = Select-String -Path $_.FullName -Pattern 'm_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]+)' -AllMatches
        foreach ($line in $matches) {
            foreach ($match in $line.Matches) {
                $guid = $match.Groups[1].Value
                if (-not $guidToPath.ContainsKey($guid)) {
                    $relative = $_.FullName.Substring($ProjectPath.Length).TrimStart('\')
                    $unresolved.Add("${relative}:$($line.LineNumber): unresolved script guid $guid")
                }
            }
        }
    }

if ($unresolved.Count -eq 0) {
    "No unresolved local m_Script GUIDs found." | Set-Content $OutputPath -Encoding UTF8
    Write-Host "No unresolved local m_Script GUIDs found. Output=$OutputPath"
    exit 0
}
$unresolved | Set-Content $OutputPath -Encoding UTF8
Write-Error "$($unresolved.Count) unresolved script GUID reference(s). Output=$OutputPath"
exit 1
