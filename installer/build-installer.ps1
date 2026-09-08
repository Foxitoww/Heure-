# Publie Heure+ en exe autonome puis compile l'installeur.
# Prerequis : SDK .NET 8+ et Inno Setup 6 (winget install JRSoftware.InnoSetup).
#
# La version vient de <Version> dans src/HeurePlus/HeurePlus.csproj (source unique).

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src\HeurePlus\HeurePlus.csproj"

# --- Version (source unique = le .csproj) ---
[xml]$csproj = Get-Content $proj
$version = @($csproj.Project.PropertyGroup.Version | Where-Object { $_ })[0]
if (-not $version) { throw "Impossible de lire <Version> dans $proj" }
$version = ([string]$version).Trim()
Write-Host "== Version $version ==" -ForegroundColor Cyan

# Fichier d'include genere, consomme par HeurePlus.iss
$versionIss = Join-Path $PSScriptRoot "version.generated.iss"
Set-Content -Path $versionIss -Value "#define AppVersion `"$version`"" -Encoding UTF8

# --- Publication ---
Write-Host "== Publication (win-x64, autonome, fichier unique) ==" -ForegroundColor Cyan
dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none

# --- Localiser ISCC.exe ---
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) { throw "ISCC.exe introuvable. Installez Inno Setup 6 : winget install JRSoftware.InnoSetup" }

# --- Compilation de l'installeur ---
Write-Host "== Compilation de l'installeur ($iscc) ==" -ForegroundColor Cyan
& $iscc (Join-Path $PSScriptRoot "HeurePlus.iss")

$out = Join-Path $PSScriptRoot "Output\HeurePlus-Setup.exe"
if (-not (Test-Path $out)) { throw "L'installeur n'a pas ete genere." }

$mb = [math]::Round((Get-Item $out).Length / 1MB, 1)
Write-Host ""
Write-Host "OK -> $out ($mb Mo) - version $version" -ForegroundColor Green
Write-Host "Prochaine etape (publication) :" -ForegroundColor Yellow
Write-Host "  git tag v$version && git push origin v$version" -ForegroundColor Yellow
Write-Host "  puis creer la Release GitHub v$version en y joignant HeurePlus-Setup.exe" -ForegroundColor Yellow
