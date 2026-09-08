# Publie Heure+ en exe autonome puis compile l'installeur.
# Prerequis : SDK .NET 8+ et Inno Setup 6 (winget install JRSoftware.InnoSetup).

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src\HeurePlus\HeurePlus.csproj"

Write-Host "== Publication (win-x64, autonome, fichier unique) ==" -ForegroundColor Cyan
dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none

# Localiser ISCC.exe
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) { throw "ISCC.exe introuvable. Installez Inno Setup 6 : winget install JRSoftware.InnoSetup" }

Write-Host "== Compilation de l'installeur ($iscc) ==" -ForegroundColor Cyan
& $iscc (Join-Path $PSScriptRoot "HeurePlus.iss")

$out = Join-Path $PSScriptRoot "Output\HeurePlus-Setup.exe"
if (Test-Path $out) {
    $mb = [math]::Round((Get-Item $out).Length / 1MB, 1)
    Write-Host "OK -> $out ($mb Mo)" -ForegroundColor Green
} else {
    throw "L'installeur n'a pas ete genere."
}
