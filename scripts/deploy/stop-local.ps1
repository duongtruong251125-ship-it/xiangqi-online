$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
Stop-XiangqiOwnedProcess 'local'
