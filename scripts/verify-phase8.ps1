param([switch]$SkipFrontend)

$ErrorActionPreference = 'Continue'
$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
$results = [System.Collections.Generic.List[object]]::new()

function Invoke-Gate([string]$Name, [scriptblock]$Action) {
    Write-Host "`n=== $Name ==="
    try {
        & $Action
        if ($LASTEXITCODE -ne 0) { throw "exit code $LASTEXITCODE" }
        $results.Add([pscustomobject]@{ gate = $Name; status = 'PASS'; detail = 'Command completed successfully' })
        Write-Host "PASS: $Name" -ForegroundColor Green
    } catch {
        $results.Add([pscustomobject]@{ gate = $Name; status = 'FAIL'; detail = $_.Exception.Message })
        Write-Host "FAIL: $Name - $($_.Exception.Message)" -ForegroundColor Red
    }
}

Invoke-Gate 'dotnet SDK' { dotnet --version }
Invoke-Gate 'Restore solution' { dotnet restore XiangqiOnline.sln }
Invoke-Gate 'Release build' { dotnet build XiangqiOnline.sln -c Release --no-restore }
Invoke-Gate 'Automated .NET tests (includes SQL-backed integration tests)' { dotnet test XiangqiOnline.sln -c Release --no-restore }
Invoke-Gate 'Vue/MVC JavaScript syntax' { node --check src/XiangqiOnline.API/wwwroot/js/app.js }

if (-not $SkipFrontend) {
    Push-Location (Join-Path $repo 'frontend/xiangqi-web')
    try {
        Invoke-Gate 'React frontend lint' { npm run lint }
        Invoke-Gate 'React frontend tests' { npm test -- --reporter=dot }
        Invoke-Gate 'React frontend production build' { npm run build }
    } finally { Pop-Location }
} else {
    $results.Add([pscustomobject]@{ gate = 'React frontend lint/tests/build'; status = 'SKIPPED'; detail = 'SkipFrontend was requested' })
}

if (Get-Command dotnet-ef -ErrorAction SilentlyContinue) {
    Invoke-Gate 'EF model migration drift check' { dotnet ef migrations has-pending-model-changes --project src/XiangqiOnline.Infrastructure --startup-project src/XiangqiOnline.API }
} else {
    $results.Add([pscustomobject]@{ gate = 'EF model migration drift check'; status = 'SKIPPED'; detail = 'dotnet-ef tool is not installed' })
}

$results.Add([pscustomobject]@{ gate = 'Browser end-to-end and WebRTC media'; status = 'SKIPPED'; detail = 'No browser E2E runner or two-client media fixture is configured' })
$output = Join-Path $repo 'TestResults/phase8-verification.json'
New-Item -ItemType Directory -Path (Split-Path -Parent $output) -Force | Out-Null
$results | ConvertTo-Json -Depth 5 | Set-Content -Path $output -Encoding utf8
$results | Format-Table -AutoSize
if ($results.Where({ $_.status -eq 'FAIL' }).Count -gt 0) { exit 1 }
