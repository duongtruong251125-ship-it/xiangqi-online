Set-StrictMode -Version Latest

$script:XiangqiDeployRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$script:XiangqiApiProject = Join-Path $script:XiangqiDeployRoot 'src/XiangqiOnline.API/XiangqiOnline.API.csproj'
$script:XiangqiStateDirectory = Join-Path $env:LOCALAPPDATA 'XiangqiOnline/deploy'

function Get-XiangqiConfiguredPort {
    $settingsPath = Join-Path $script:XiangqiDeployRoot 'src/XiangqiOnline.API/Properties/launchSettings.json'
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $profile = $settings.profiles.'XiangqiOnline.API'
    if (-not $profile) {
        $profile = $settings.profiles.PSObject.Properties.Value | Where-Object { $_.commandName -eq 'Project' } | Select-Object -First 1
    }
    $httpUrl = $profile.applicationUrl -split ';' | Where-Object { $_ -match '^http://' } | Select-Object -First 1
    if (-not $httpUrl) { throw 'Không tìm thấy URL HTTP trong launchSettings.json.' }
    return ([Uri]$httpUrl).Port
}

function Get-XiangqiLocalUrl([int]$Port) { return "http://127.0.0.1:$Port" }

function Get-XiangqiCloudflaredPath {
    $command = Get-Command cloudflared -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $wingetAlias = Join-Path $env:LOCALAPPDATA 'Microsoft/WinGet/Links/cloudflared.exe'
    if (Test-Path -LiteralPath $wingetAlias) { return $wingetAlias }
    return $null
}

function Get-XiangqiStatePath([string]$Name) {
    if (-not (Test-Path -LiteralPath $script:XiangqiStateDirectory)) {
        New-Item -ItemType Directory -Path $script:XiangqiStateDirectory -Force | Out-Null
    }
    return Join-Path $script:XiangqiStateDirectory "$Name.json"
}

function Get-XiangqiJwtSigningKey {
    if (-not [string]::IsNullOrWhiteSpace($env:Jwt__SigningKeyBase64)) { return $env:Jwt__SigningKeyBase64 }
    $lines = & dotnet user-secrets list --project $script:XiangqiApiProject 2>$null
    if ($LASTEXITCODE -eq 0) {
        foreach ($line in $lines) {
            if ($line -match '^\s*Jwt:SigningKeyBase64\s*=\s*(\S.*)$') { return $Matches[1].Trim() }
        }
    }
    throw 'Chưa cấu hình Jwt:SigningKeyBase64. Hãy lưu khóa bằng dotnet user-secrets; script không ghi khóa vào file hoặc log.'
}

function Read-XiangqiProcessState([string]$Name) {
    $statePath = Get-XiangqiStatePath $Name
    if (-not (Test-Path -LiteralPath $statePath)) { return $null }
    try { return Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json }
    catch { throw "Tệp trạng thái $statePath bị lỗi. Không dừng tiến trình nào; hãy kiểm tra thủ công." }
}

function Get-XiangqiOwnedProcess($State) {
    if (-not $State) { return $null }
    $process = Get-Process -Id ([int]$State.ProcessId) -ErrorAction SilentlyContinue
    if (-not $process) { return $null }
    try {
        if (-not [string]::Equals([IO.Path]::GetFullPath($process.Path), [IO.Path]::GetFullPath([string]$State.ExecutablePath), [StringComparison]::OrdinalIgnoreCase)) { return $null }
        $recorded = if ($State.StartedAtUtc -is [DateTime]) {
            $State.StartedAtUtc.ToUniversalTime()
        } else {
            [DateTimeOffset]::Parse([string]$State.StartedAtUtc).UtcDateTime
        }
        if ([Math]::Abs(($process.StartTime.ToUniversalTime() - $recorded).TotalSeconds) -gt 5) { return $null }
        return $process
    } catch { return $null }
}

function Stop-XiangqiOwnedProcess([string]$Name) {
    $statePath = Get-XiangqiStatePath $Name
    $state = Read-XiangqiProcessState $Name
    if (-not $state) { Write-Host "Không có tiến trình $Name do script quản lý."; return }
    $process = Get-XiangqiOwnedProcess $state
    if (-not $process) {
        Remove-Item -LiteralPath $statePath -Force
        Write-Warning 'PID đã hết hạn hoặc không khớp chương trình đã khởi chạy; không dừng tiến trình khác.'
        return
    }
    $process.Kill()
    $process.WaitForExit(10000)
    Remove-Item -LiteralPath $statePath -Force
    Write-Host "Đã dừng tiến trình $Name (PID $($process.Id))."
}

function Test-XiangqiHealthy([string]$BaseUrl, [int]$TimeoutSeconds = 5) {
    try {
        $health = Invoke-RestMethod -Uri "$BaseUrl/api/v1/health" -TimeoutSec $TimeoutSeconds
        return $health.status -eq 'Healthy'
    } catch { return $false }
}
