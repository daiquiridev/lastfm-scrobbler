# Generates app.ico (16/32/48/256 px) from icons-preview/logo.svg
# Requires Node.js. Run from any directory: .\tools\generate-icon.ps1

param(
    [string]$SvgPath = (Join-Path $PSScriptRoot "..\icons-preview\logo.svg"),
    [string]$OutPath = (Join-Path $PSScriptRoot "..\app.ico")
)

$SvgPath = [System.IO.Path]::GetFullPath($SvgPath)
$OutPath = [System.IO.Path]::GetFullPath($OutPath)
$script  = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "generate-icon.js"))

# sharp cannot be installed on UNC paths — use a local temp dir
$tmpDir = "C:\tmp\lastfm-icon"
if (-not (Test-Path "$tmpDir\node_modules\sharp")) {
    Write-Host "Installing sharp..."
    New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null
    $initJson = '{"name":"icon-gen","version":"1.0.0","private":true}'
    [System.IO.File]::WriteAllText("$tmpDir\package.json", $initJson)
    Push-Location $tmpDir
    npm install sharp --save 2>&1 | Out-Null
    Pop-Location
}

node --require "$tmpDir\node_modules\sharp" $script "$SvgPath" "$OutPath" 2>&1
if ($LASTEXITCODE -ne 0) {
    # Fallback: run script from tmpDir where sharp is resolvable
    Copy-Item $script "$tmpDir\generate-icon.js" -Force
    Push-Location $tmpDir
    node generate-icon.js "$SvgPath" "$OutPath"
    Pop-Location
}
