# Publishes the self-contained exe, then builds Setup\dist\VelocitySetup.msi with WiX.
#
# Self-restoring: installs the WiX v5 CLI and pins the UI/Util extensions to a
# matching version if they are missing. This is what makes a fresh git clone
# build cleanly -- the .wix\ extension cache is intentionally NOT committed, so
# without this restore step "wix build -ext ..." would resolve a mismatched
# extension (or none) and fail, often with a confusing "culture 'en-us'"
# localization error from the UI extension.
param(
    [switch]$SkipBuild
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish"
$setupDir = Join-Path $root "Setup"
$distDir = Join-Path $setupDir "dist"

# WiX v5 is pinned deliberately: v7+ requires accepting the paid Open Source
# Maintenance Fee (OSMF) EULA before the CLI will run. Keep the tool and both
# extensions on the same version so localization resolves correctly.
$WixVersion = "5.0.2"
$Extensions = @("WixToolset.UI.wixext", "WixToolset.Util.wixext")

function Test-Command($name) {
    $null -ne (Get-Command $name -ErrorAction SilentlyContinue)
}

# --- Ensure the WiX CLI is installed at the pinned version ---
$haveWix = Test-Command "wix"
$wixVersionOk = $false
if ($haveWix) {
    $wixVersionOk = ((wix --version) 2>&1) -match [regex]::Escape($WixVersion)
}
if (-not $wixVersionOk) {
    Write-Host "Installing WiX $WixVersion CLI..." -ForegroundColor Cyan
    if ($haveWix) { dotnet tool uninstall --global wix 2>&1 | Out-Null }
    dotnet tool install --global wix --version $WixVersion
    if ($LASTEXITCODE -ne 0) { throw "Failed to install the WiX $WixVersion CLI." }
    if (-not (Test-Command "wix")) {
        throw "WiX installed but 'wix' is not on PATH. Open a new shell (so %USERPROFILE%\.dotnet\tools is picked up) and re-run."
    }
}

# --- Restore the required extensions into the local .wix\ cache ---
# Always add (it is idempotent): `wix extension list` reports globally-added
# extensions too, so trusting it would skip the local restore that `wix build`
# actually reads from -- the classic fresh-clone failure.
foreach ($ext in $Extensions) {
    Write-Host "Restoring $ext/$WixVersion..." -ForegroundColor Cyan
    wix extension add "$ext/$WixVersion" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to add WiX extension $ext/$WixVersion." }
}

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "publish.ps1")
}

New-Item -ItemType Directory -Force -Path $distDir | Out-Null

# Reference extensions by explicit "name/version" so the build always binds the
# pinned copy even if another version is present in the cache.
wix build (Join-Path $setupDir "Product.wxs") `
    -ext "WixToolset.UI.wixext/$WixVersion" `
    -ext "WixToolset.Util.wixext/$WixVersion" `
    -d "PublishDir=$publishDir" `
    -bindpath $setupDir `
    -culture en-us `
    -arch x64 `
    -o (Join-Path $distDir "VelocitySetup.msi")

if ($LASTEXITCODE -ne 0) { throw "wix build failed with exit code $LASTEXITCODE" }
Write-Host "`nBuilt $(Join-Path $distDir 'VelocitySetup.msi')" -ForegroundColor Cyan
