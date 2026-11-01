[CmdletBinding()]
param([int]$Port = 0)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
if ($Port -le 0) { $Port = Get-XiangqiConfiguredPort }
$localUrl = Get-XiangqiLocalUrl $Port
$statePath = Get-XiangqiStatePath 'tunnel'
$existingState = Read-XiangqiProcessState 'tunnel'
if ($owned = Get-XiangqiOwnedProcess $existingState) {
    Write-Host "Tunnel đang chạy: $($existingState.PublicUrl) (PID $($owned.Id))."
    exit 0
}
if (-not (Test-XiangqiHealthy $localUrl)) { throw "Ứng dụng local chưa healthy tại $localUrl. Chạy start-local.ps1 trước." }
try {
    $swagger = Invoke-WebRequest -Uri "$localUrl/swagger" -UseBasicParsing -TimeoutSec 4
    if ($swagger.StatusCode -eq 200) { throw 'Swagger đang truy cập được. Tunnel không được mở app Development ra Internet.' }
} catch {
    if ($_.Exception.Message -like '*Swagger đang truy cập được*') { throw }
}

$cloudflaredPath = Get-XiangqiCloudflaredPath
if (-not $cloudflaredPath) { throw 'Thiếu cloudflared. Cài bằng: winget install --id Cloudflare.cloudflared -e --scope user. Nếu cần Administrator, dừng tại đây và yêu cầu người dùng.' }
$stateDirectory = $script:XiangqiStateDirectory
$stdout = Join-Path $stateDirectory 'tunnel.out.log'
$stderr = Join-Path $stateDirectory 'tunnel.err.log'
Remove-Item -LiteralPath $stdout,$stderr -Force -ErrorAction SilentlyContinue
$process = Start-Process -FilePath $cloudflaredPath -ArgumentList @('tunnel','--url',$localUrl) -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
$state = [ordered]@{ ProcessId = $process.Id; ExecutablePath = $cloudflaredPath; StartedAtUtc = $process.StartTime.ToUniversalTime().ToString('O'); LocalUrl = $localUrl; PublicUrl = $null; Stdout = $stdout; Stderr = $stderr }
$state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8

$publicUrl = $null
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 1
    $process.Refresh()
    if ($process.HasExited) { break }
    $text = ((Get-Content -LiteralPath $stdout -Raw -ErrorAction SilentlyContinue) -join "`n") + "`n" + ((Get-Content -LiteralPath $stderr -Raw -ErrorAction SilentlyContinue) -join "`n")
    $match = [regex]::Match($text, 'https://[a-z0-9-]+\.trycloudflare\.com', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
    if ($match.Success) { $publicUrl = $match.Value; break }
}

if (-not $publicUrl) {
    $owned = Get-XiangqiOwnedProcess $state
    if ($owned) { $owned.Kill(); $owned.WaitForExit(10000) }
    Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue
    Write-Host '--- cloudflared stderr (last 30 lines) ---'
    Get-Content -LiteralPath $stderr -Tail 30 -ErrorAction SilentlyContinue
    throw 'Không nhận được URL Quick Tunnel; chưa thể coi website public.'
}

$state.PublicUrl = $publicUrl
$state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
Write-Host "Tunnel đang chạy (PID $($process.Id))."
Write-Host "Public HTTPS URL: $publicUrl"
Write-Host 'Giữ máy và SQL Server luôn hoạt động. Dừng tunnel bằng scripts/deploy/stop-tunnel.ps1.'
