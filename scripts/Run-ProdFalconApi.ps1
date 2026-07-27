$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

& "$PSScriptRoot\Stop-ProdFalconApi.ps1"

# Override any machine/user ConnectionStrings__DefaultConnection (e.g. other projects)
$env:ConnectionStrings__DefaultConnection = "Server=.\SQLEXPRESS;Database=ProdFalconDb;Trusted_Connection=True;TrustServerCertificate=True"

Set-Location "$root\ProdFalcon.API"
Write-Host "Starting ProdFalcon API..."
dotnet run --launch-profile http
