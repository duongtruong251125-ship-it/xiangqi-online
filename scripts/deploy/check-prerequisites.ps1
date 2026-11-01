$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$failed = $false
$port = Get-XiangqiConfiguredPort
$localUrl = Get-XiangqiLocalUrl $port

if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $sdk = (& dotnet --version).Trim()
    Write-Host "PASS .NET SDK: $sdk"
    $runtimes = & dotnet --list-runtimes
    if ($runtimes -match 'Microsoft\.AspNetCore\.App 8\.') { Write-Host 'PASS ASP.NET Core runtime 8.x' }
    else { Write-Error 'Thiếu ASP.NET Core runtime 8.x.'; $failed = $true }
} else { Write-Error 'Không tìm thấy dotnet.'; $failed = $true }

Write-Host "PASS Startup: $script:XiangqiApiProject"
Write-Host "PASS HTTP loopback dự kiến: $localUrl"

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if ($sqlcmd) {
    $sql = & $sqlcmd.Source -S . -E -Q "SET NOCOUNT ON; SELECT CAST(SERVERPROPERTY('Edition') AS nvarchar(128)); SELECT state_desc FROM sys.databases WHERE name='XiangqiOnlineDB';" -h -1 -W 2>$null
    if ($LASTEXITCODE -eq 0 -and $sql -match 'ONLINE') {
        $edition = ($sql | Where-Object { $_ -match 'Edition|Express|Developer|Standard|Enterprise' } | Select-Object -First 1).Trim()
        Write-Host "PASS SQL default instance: $edition; XiangqiOnlineDB ONLINE. Không chạy migration ở bước kiểm tra."
        if ($edition -match 'Developer') { Write-Warning 'Máy hiện chạy SQL Server Developer Edition, không phải Express; đang giữ đúng instance Server=., không chuyển database.' }
    } else { Write-Error 'SQLCMD không kết nối được Server=.; chưa sẵn sàng cho app.'; $failed = $true }
} else { Write-Warning 'Không có sqlcmd; health endpoint sẽ xác minh database khi app chạy.' }

if (Get-XiangqiCloudflaredPath) { Write-Host 'PASS cloudflared đã cài.' }
else { Write-Warning 'cloudflared chưa cài. Lệnh cài từ nguồn Cloudflare: winget install --id Cloudflare.cloudflared -e --scope user' }

if (Test-XiangqiHealthy $localUrl) { Write-Host 'PASS Ứng dụng local và SQL health endpoint đang khỏe.' }
else { Write-Host 'INFO Ứng dụng chưa chạy hoặc chưa sẵn sàng; hãy chạy start-local.ps1 trước start-tunnel.ps1.' }

if ($failed) { exit 1 }
