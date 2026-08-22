param(
    [string]$LogPath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path "Temp/KingdomPerf.log"),
    [int]$TimeoutMinutes = 15,
    [int]$PollSeconds = 10
)

$ErrorActionPreference = "Stop"
$validator = Join-Path $PSScriptRoot "validate-unity-perf-log.ps1"
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)

while ((Get-Date) -lt $deadline) {
    if (Test-Path -LiteralPath $LogPath -PathType Leaf) {
        try {
            & $validator -Path $LogPath
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Unity performance session passed."
                exit 0
            }
        }
        catch {
            Write-Host ("Waiting for a valid Unity session: " + $_.Exception.Message)
        }
    }
    Start-Sleep -Seconds ([Math]::Max(1, $PollSeconds))
}

throw "Timed out after $TimeoutMinutes minutes waiting for a valid Unity performance session."
