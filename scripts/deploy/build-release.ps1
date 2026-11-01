$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')

$state = Read-XiangqiProcessState 'local'
if (Get-XiangqiOwnedProcess $state) { throw 'Ứng dụng local đang chạy từ thư mục publish. Hãy chạy stop-local.ps1 trước khi build lại.' }

$publishDirectory = Join-Path $script:XiangqiDeployRoot 'artifacts/publish/XiangqiOnline.API'
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
Push-Location $script:XiangqiDeployRoot
try {
    dotnet restore XiangqiOnline.sln
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore thất bại ($LASTEXITCODE)." }
    dotnet build XiangqiOnline.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet build thất bại ($LASTEXITCODE)." }
    dotnet publish $script:XiangqiApiProject -c Release --no-build -o $publishDirectory
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish thất bại ($LASTEXITCODE)." }
} finally { Pop-Location }

$executable = Join-Path $publishDirectory 'XiangqiOnline.API.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw "Không tìm thấy apphost sau publish: $executable" }
Write-Host "PASS Release publish: $publishDirectory"
