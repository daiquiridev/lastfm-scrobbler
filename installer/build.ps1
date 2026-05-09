#Requires -Version 7
<#
.SYNOPSIS
    Build, package, and prepare a release of Last.fm Scrobbler.

.PARAMETER Version
    Override the version string (default: read from LastFmScrobbler.csproj).

.PARAMETER BucketName
    Cloudflare R2 bucket name (default: lastfm-releases).

.PARAMETER R2PublicUrl
    Public base URL of the R2 bucket, no trailing slash.
    Example: https://pub-XXXXXXXX.r2.dev
             https://updates.yourdomain.com

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Version 1.2.0 -BucketName lastfm-releases -R2PublicUrl https://pub-abc.r2.dev
#>
param(
    [string]$Version    = "",
    [string]$BucketName = "lastfm-releases",
    [string]$R2PublicUrl = "https://pub-8a5464b225534730b481b262ffe4748b.r2.dev"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── 1. Resolve version ───────────────────────────────────────────────────────

if (-not $Version) {
    $csproj  = [xml](Get-Content "$PSScriptRoot\..\LastFmScrobbler.csproj")
    $Version = ($csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
    if (-not $Version) { throw "Could not read version from LastFmScrobbler.csproj" }
}
Write-Host "Version: $Version" -ForegroundColor Cyan

# ── 2. Publish single-file executable ────────────────────────────────────────

Write-Host "Publishing…" -ForegroundColor Cyan
dotnet publish "$PSScriptRoot\..\LastFmScrobbler.csproj" `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:Version=$Version `
    -c Release `
    -o "$PSScriptRoot\..\portable\" `
    --nologo

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# ── 3. Compile installer ─────────────────────────────────────────────────────

$iscc = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
    "C:\Program Files\Inno Setup 7\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup not found — download from https://jrsoftware.org/isinfo.php" }

Write-Host "Compiling installer…" -ForegroundColor Cyan
& $iscc /DAppVersion=$Version "$PSScriptRoot\setup.iss"
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

# ── 4. SHA-256 ───────────────────────────────────────────────────────────────

$installer   = "$PSScriptRoot\LastFmScrobbler-Setup-$Version.exe"
if (-not (Test-Path $installer)) { throw "Installer not found: $installer" }

$sha256 = (Get-FileHash $installer -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "SHA-256: $sha256" -ForegroundColor Green

# ── 5. Write latest.json ─────────────────────────────────────────────────────

$installerFilename = "LastFmScrobbler-Setup-$Version.exe"
$manifest = [ordered]@{
    version = $Version
    url     = "$R2PublicUrl/lastfm-scrobbler/$installerFilename"
    sha256  = $sha256
} | ConvertTo-Json
$manifest | Set-Content "$PSScriptRoot\latest.json" -Encoding utf8NoBOM
Write-Host "latest.json written." -ForegroundColor Green

# ── 6. Upload to R2 ─────────────────────────────────────────────────────────

$workerDir = "$PSScriptRoot\..\worker"
if (-not (Test-Path "$workerDir\node_modules\.bin\wrangler.cmd")) { throw "wrangler not found. Run: npm install (in worker/)" }

Write-Host "Uploading installer to R2…" -ForegroundColor Cyan
cmd /c "npx --prefix `"$workerDir`" wrangler r2 object put $BucketName/lastfm-scrobbler/$installerFilename --file `"$PSScriptRoot\$installerFilename`" --content-type application/octet-stream --remote 2>&1"
if ($LASTEXITCODE -ne 0) { throw "R2 upload (installer) failed" }

Write-Host "Uploading latest.json to R2…" -ForegroundColor Cyan
cmd /c "npx --prefix `"$workerDir`" wrangler r2 object put $BucketName/lastfm-scrobbler/latest.json --file `"$PSScriptRoot\latest.json`" --content-type application/json --cache-control no-cache,no-store --remote 2>&1"
if ($LASTEXITCODE -ne 0) { throw "R2 upload (manifest) failed" }

Write-Host ""
Write-Host "✅ v$Version released and uploaded." -ForegroundColor Green
Write-Host "   Manifest : $R2PublicUrl/lastfm-scrobbler/latest.json" -ForegroundColor Gray
Write-Host "   Installer: $R2PublicUrl/lastfm-scrobbler/$installerFilename" -ForegroundColor Gray
