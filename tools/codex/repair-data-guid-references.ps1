param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
)

$ErrorActionPreference = "Stop"
$dataRoot = (Resolve-Path (Join-Path $ProjectPath "Assets/Resources/Datas")).Path
$repoRoot = (Resolve-Path (Join-Path $ProjectPath "..")).Path
$guidMap = @{}

Get-ChildItem -LiteralPath $dataRoot -Recurse -File -Filter '*.meta' | ForEach-Object {
    $relative = $_.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    $current = Select-String -LiteralPath $_.FullName -Pattern '^guid:\s*(\S+)' | Select-Object -First 1
    if (-not $current) { return }
    $oldText = git -C $repoRoot show "HEAD:$relative" 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $oldText) { return }
    $old = [regex]::Match(($oldText -join [Environment]::NewLine), '(?m)^guid:\s*(\S+)').Groups[1].Value
    $new = $current.Matches[0].Groups[1].Value
    if ($old -and $new -and $old -ne $new) {
        $guidMap[$old] = $new
    }
}

$updatedFiles = 0
$replacementCount = 0
$assets = Get-ChildItem -LiteralPath $dataRoot -Recurse -File -Filter '*.asset'
foreach ($asset in $assets) {
    $text = [IO.File]::ReadAllText($asset.FullName)
    $updated = $text
    foreach ($pair in $guidMap.GetEnumerator()) {
        $before = $updated
        $updated = $updated.Replace("guid: $($pair.Key)", "guid: $($pair.Value)")
        if ($updated -ne $before) {
            $replacementCount += ([regex]::Matches($before, [regex]::Escape("guid: $($pair.Key)"))).Count
        }
    }
    if ($updated -eq $text) { continue }
    [IO.File]::WriteAllText($asset.FullName, $updated, (New-Object Text.UTF8Encoding($false)))
    $updatedFiles++
}

Write-Output "GUID mappings=$($guidMap.Count); asset files updated=$updatedFiles; reference replacements=$replacementCount"
