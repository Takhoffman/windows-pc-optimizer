# Publishes the self-contained exe, then builds Setup\dist\VelocitySetup.msi with WiX.
param(
    [switch]$SkipBuild
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish"
$setupDir = Join-Path $root "Setup"
$distDir = Join-Path $setupDir "dist"

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "publish.ps1")
}

New-Item -ItemType Directory -Force -Path $distDir | Out-Null

wix build (Join-Path $setupDir "Product.wxs") `
    -ext WixToolset.UI.wixext `
    -ext WixToolset.Util.wixext `
    -d "PublishDir=$publishDir" `
    -bindpath $setupDir `
    -culture en-us `
    -arch x64 `
    -o (Join-Path $distDir "VelocitySetup.msi")

if ($LASTEXITCODE -ne 0) { throw "wix build failed with exit code $LASTEXITCODE" }
Write-Host "`nBuilt $(Join-Path $distDir 'VelocitySetup.msi')" -ForegroundColor Cyan
