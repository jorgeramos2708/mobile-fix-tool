# Build / test / publicar / verificación de la plataforma MobileFix
#
# Uso:
#   .\build.ps1              -> compila la solución
#   .\build.ps1 -Test        -> compila, ejecuta las pruebas unitarias y la autocomprobación de invariantes
#   .\build.ps1 -Run         -> compila y arranca la aplicación de escritorio
#   .\build.ps1 -Publish     -> genera el ejecutable autocontenido en artifacts\desktop-standalone
#   .\build.ps1 -Release     -> compila en Release en lugar de Debug

param(
    [switch]$Test,
    [switch]$Run,
    [switch]$Publish,
    [switch]$Release
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# --- localizar el SDK (instalación local al usuario o instalación global) ---
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

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

Write-Host "dotnet: $dotnet" -ForegroundColor DarkGray
& $dotnet --version

$config = if ($Release) { 'Release' } else { 'Debug' }

Write-Host "`n==> Compilando ($config)" -ForegroundColor Cyan
& $dotnet build MobileFix.slnx -c $config --nologo
if ($LASTEXITCODE -ne 0) { throw "La compilación falló." }

if ($Test) {
    Write-Host "`n==> Pruebas unitarias (dominio, journal, cobertura)" -ForegroundColor Cyan
    & $dotnet test tests/MobileFix.Domain.Tests -c $config --nologo
    if ($LASTEXITCODE -ne 0) { throw "Las pruebas unitarias fallaron." }

    Write-Host "`n==> Autocomprobación de invariantes (CLI)" -ForegroundColor Cyan
    & $dotnet run --project src/MobileFix.Cli -c $config --no-build
    if ($LASTEXITCODE -ne 0) { throw "La autocomprobación falló." }
}

if ($Publish) {
    Write-Host "`n==> Publicando ejecutable autocontenido (win-x64)" -ForegroundColor Cyan
    & $dotnet publish src/MobileFix.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/desktop-standalone --nologo
    if ($LASTEXITCODE -ne 0) { throw "La publicación falló." }
    Write-Host "Ejecutable: artifacts\desktop-standalone\MobileFix.exe" -ForegroundColor Green
}

if ($Run) {
    Write-Host "`n==> Iniciando aplicación de escritorio" -ForegroundColor Cyan
    & $dotnet run --project src/MobileFix.Desktop -c $config --no-build
}

Write-Host "`nOK" -ForegroundColor Green
