[CmdletBinding()]
param([int]$Port = 0)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
if ($Port -le 0) { $Port = Get-XiangqiConfiguredPort }
$localUrl = Get-XiangqiLocalUrl $Port
$statePath = Get-XiangqiStatePath 'local'
$existingState = Read-XiangqiProcessState 'local'
if ($owned = Get-XiangqiOwnedProcess $existingState) {
    if ($existingState.LocalUrl -eq $localUrl -and (Test-XiangqiHealthy $localUrl)) {
        Write-Host "Ứng dụng đã chạy: $localUrl (PID $($owned.Id))."
        exit 0
    }
    throw "Ứng dụng script quản lý đang chạy tại $($existingState.LocalUrl). Hãy dừng bằng stop-local.ps1 trước."
}

if (Test-NetConnection -ComputerName 127.0.0.1 -Port $Port -InformationLevel Quiet -WarningAction SilentlyContinue) {
    throw "Cổng $Port đang được tiến trình khác sử dụng. Script không dừng tiến trình đó. Chọn cổng khác, ví dụ: .\scripts\deploy\start-local.ps1 -Port $($Port + 1)"
}

$publishDirectory = Join-Path $script:XiangqiDeployRoot 'artifacts/publish/XiangqiOnline.API'
$executable = Join-Path $publishDirectory 'XiangqiOnline.API.exe'
if (-not (Test-Path -LiteralPath $executable)) { & (Join-Path $PSScriptRoot 'build-release.ps1') }
if (-not (Test-Path -LiteralPath $executable)) { throw 'Release chưa publish thành công.' }

$jwtKey = Get-XiangqiJwtSigningKey
$oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
$oldJwtKey = $env:Jwt__SigningKeyBase64
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:Jwt__SigningKeyBase64 = $jwtKey
try {
    $stateDirectory = $script:XiangqiStateDirectory
    New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
    $stdout = Join-Path $stateDirectory 'local.out.log'
    $stderr = Join-Path $stateDirectory 'local.err.log'
    Remove-Item -LiteralPath $stdout,$stderr -Force -ErrorAction SilentlyContinue
    $process = Start-Process -FilePath $executable -ArgumentList @('--urls', $localUrl) -WorkingDirectory $publishDirectory -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $state = [ordered]@{ ProcessId = $process.Id; ExecutablePath = $executable; StartedAtUtc = $process.StartTime.ToUniversalTime().ToString('O'); LocalUrl = $localUrl; Stdout = $stdout; Stderr = $stderr }
    $state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
} finally {
    $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
    $env:Jwt__SigningKeyBase64 = $oldJwtKey
    $jwtKey = $null
}

$ready = $false
for ($i = 0; $i -lt 45; $i++) {
    Start-Sleep -Seconds 1
    if (-not (Get-XiangqiOwnedProcess $state)) { break }
    if (Test-XiangqiHealthy $localUrl -TimeoutSeconds 2) { $ready = $true; break }
}
if (-not $ready) {
    $process = Get-XiangqiOwnedProcess $state
    if ($process) { $process.Kill(); $process.WaitForExit(10000) }
    Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue
    Write-Host '--- local app stderr (last 30 lines) ---'
    Get-Content -LiteralPath $state.Stderr -Tail 30 -ErrorAction SilentlyContinue
    throw 'Ứng dụng không đạt health check. Tunnel chưa được khởi chạy.'
}

try {
    $swagger = Invoke-WebRequest -Uri "$localUrl/swagger" -UseBasicParsing -TimeoutSec 4
    if ($swagger.StatusCode -eq 200) {
        $process = Get-XiangqiOwnedProcess $state
        if ($process) { $process.Kill(); $process.WaitForExit(10000) }
        Remove-Item -LiteralPath $statePath -Force
        throw 'Swagger đang công khai; app phải chạy Production. Đã dừng instance do script khởi chạy.'
    }
} catch {
    if ($_.Exception.Message -like '*Swagger đang công khai*') { throw }
}

Write-Host "PASS Ứng dụng Production chạy tại $localUrl (PID $($state.ProcessId))."
Write-Host "Log: $($state.Stderr)"
