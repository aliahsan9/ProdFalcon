$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

& "$PSScriptRoot\Stop-ProdFalconApi.ps1"

Set-Location "$root\ProdFalcon.API"
Write-Host "Starting ProdFalcon API..."
dotnet run
