param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$OutputPath = (Join-Path $ProjectPath "Logs/codex-yaml-reference-audit.txt")
)
$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force (Split-Path $OutputPath) | Out-Null

$guidToPath = @{}
$metaRoots = @(
    (Join-Path $ProjectPath "Assets"),
    (Join-Path $ProjectPath "Library/PackageCache"),
    (Join-Path $ProjectPath "Packages")
) | Where-Object { Test-Path -LiteralPath $_ }
Get-ChildItem $metaRoots -Filter *.meta -Recurse | ForEach-Object {
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
        $assetLines = Get-Content -LiteralPath $_.FullName
        for ($lineIndex = 0; $lineIndex -lt $assetLines.Count; $lineIndex++) {
            if ($assetLines[$lineIndex] -notmatch 'm_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]+)') {
                continue
            }

            $guid = $Matches[1]
            if ($guid -eq "0000000000000000e000000000000000") {
                continue
            }
            if ($guidToPath.ContainsKey($guid)) {
                continue
            }

            # Unity UI/TMP components are serialized with package-owned script
            # GUIDs and intentionally have no local .meta file under Assets.
            # Keep auditing project scripts, but do not report these package
            # components as missing local scripts.
            $blockEnd = $lineIndex + 1
            while ($blockEnd -lt $assetLines.Count -and
                $assetLines[$blockEnd] -notmatch '^--- !u!') {
                $blockEnd++
            }
            $identifier = $assetLines[$lineIndex..($blockEnd - 1)] |
                Where-Object { $_ -match 'm_EditorClassIdentifier:\s*(.+)$' } |
                Select-Object -First 1
            if ($identifier -and
                ($identifier -match 'UnityEngine\.' -or $identifier -match 'TMPro::')) {
                continue
            }

            $relative = $_.FullName.Substring($ProjectPath.Length).TrimStart('\')
            $unresolved.Add("${relative}:$($lineIndex + 1): unresolved script guid $guid")
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
