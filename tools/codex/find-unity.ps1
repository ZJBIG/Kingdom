param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$UnityPath
)
$ErrorActionPreference = "Stop"

if ($UnityPath) {
    if (-not (Test-Path $UnityPath)) { throw "Unity executable not found: $UnityPath" }
    (Resolve-Path $UnityPath).Path
    exit 0
}

$versionFile = Join-Path $ProjectPath "ProjectSettings/ProjectVersion.txt"
if (-not (Test-Path $versionFile)) { throw "ProjectVersion.txt not found: $versionFile" }
$line = Get-Content $versionFile | Select-Object -First 1
$version = ($line -split ':', 2)[1].Trim()

$candidates = @(
    "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe",
    "D:\Unity\Hub\Editor\$version\Editor\Unity.exe",
    "D:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe",
    "E:\Unity\Hub\Editor\$version\Editor\Unity.exe"
)

foreach ($candidate in $candidates) {
    if (Test-Path $candidate) {
        (Resolve-Path $candidate).Path
        exit 0
    }
}

$roots = @(
    "C:\Program Files\Unity\Hub\Editor",
    "D:\Unity\Hub\Editor",
    "D:\Program Files\Unity\Hub\Editor",
    "E:\Unity\Hub\Editor"
)
foreach ($root in $roots) {
    if (-not (Test-Path $root)) { continue }
    $found = Get-ChildItem $root -Filter Unity.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match [regex]::Escape($version) } |
        Select-Object -First 1
    if ($found) {
        $found.FullName
        exit 0
    }
}

throw "Unity $version was not found. Pass -UnityPath explicitly."
