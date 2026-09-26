# Build / test / verificación de la plataforma MobileFix
# Uso:  .\build.ps1            -> compila la solución
#       .\build.ps1 -Test      -> compila y ejecuta la autocomprobación (CLI)
#       .\build.ps1 -Run       -> compila y arranca la aplicación de escritorio

param(
    [switch]$Test,
    [switch]$Run,
    [switch]$Release
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# --- localizar el SDK (local al usuario o global) -------------------------
$localSdk = Join-Path $env:LOCALAPPDATA 'dotnet-sdk\dotnet.exe'
if (Test-Path $localSdk) {
    $dotnet = $localSdk
    $env:DOTNET_ROOT = Split-Path $localSdk -Parent
} else {
    $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    if (-not $dotnet) {
        throw "No se encontró el SDK de .NET. Instala .NET 10 LTS (ver README.md)."
    }
}

Write-Host "dotnet: $dotnet" -ForegroundColor DarkGray
& $dotnet --version

$config = if ($Release) { 'Release' } else { 'Debug' }

Write-Host "`n==> Compilando ($config)" -ForegroundColor Cyan
& $dotnet build MobileFix.slnx -c $config --nologo
if ($LASTEXITCODE -ne 0) { throw "La compilación falló." }

if ($Test) {
    Write-Host "`n==> Autocomprobación (invariantes del dominio + journal)" -ForegroundColor Cyan
    & $dotnet run --project src/MobileFix.Cli -c $config --no-build
    if ($LASTEXITCODE -ne 0) { throw "La autocomprobación falló." }
}

if ($Run) {
    Write-Host "`n==> Iniciando aplicación de escritorio" -ForegroundColor Cyan
    & $dotnet run --project src/MobileFix.Desktop -c $config --no-build
}

Write-Host "`nOK" -ForegroundColor Green
