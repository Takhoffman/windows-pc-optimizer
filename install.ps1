#Requires -Version 5.1
<#
    Installs Velocity for the current user: no admin needed for setup itself
    (the app requests elevation on its own each time it launches, since
    registry/powercfg/service tweaks require it).

    Usage:
        .\install.ps1              # publish + install
        .\install.ps1 -SkipBuild   # install an already-published build
#>
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$appName = "Velocity"
$installDir = Join-Path $env:LOCALAPPDATA "Programs\$appName"
$publishDir = Join-Path $root "publish"

if (-not $SkipBuild) {
    Write-Host "Building release..." -ForegroundColor Cyan
    & (Join-Path $root "tools\publish.ps1")
}

if (-not (Test-Path (Join-Path $publishDir "$appName.exe"))) {
    throw "$appName.exe not found in $publishDir - run tools\publish.ps1 first, or omit -SkipBuild."
}

Write-Host "Installing to $installDir..." -ForegroundColor Cyan
if (Test-Path $installDir) { Remove-Item $installDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item (Join-Path $publishDir "*") $installDir -Recurse -Force

Copy-Item (Join-Path $root "Assets\app.ico") $installDir -Force

$exePath = Join-Path $installDir "$appName.exe"
$iconPath = Join-Path $installDir "app.ico"

# --- Shortcuts (Start Menu + Desktop) ---
$shell = New-Object -ComObject WScript.Shell

$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$startShortcut = $shell.CreateShortcut((Join-Path $startMenuDir "$appName.lnk"))
$startShortcut.TargetPath = $exePath
$startShortcut.WorkingDirectory = $installDir
$startShortcut.IconLocation = $iconPath
$startShortcut.Description = "Velocity - Gaming PC Optimizer"
$startShortcut.Save()

$desktopShortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath("Desktop")) "$appName.lnk"))
$desktopShortcut.TargetPath = $exePath
$desktopShortcut.WorkingDirectory = $installDir
$desktopShortcut.IconLocation = $iconPath
$desktopShortcut.Description = "Velocity - Gaming PC Optimizer"
$desktopShortcut.Save()

# --- Add/Remove Programs entry (per-user, no admin required) ---
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appName"
New-Item -Path $uninstallKey -Force | Out-Null
$sizeKb = [math]::Round(((Get-ChildItem $installDir -Recurse | Measure-Object Length -Sum).Sum) / 1KB)
Set-ItemProperty $uninstallKey "DisplayName" "Velocity - Gaming PC Optimizer"
Set-ItemProperty $uninstallKey "DisplayIcon" $exePath
Set-ItemProperty $uninstallKey "DisplayVersion" "1.0.0"
Set-ItemProperty $uninstallKey "Publisher" "takhoffman"
Set-ItemProperty $uninstallKey "InstallLocation" $installDir
Set-ItemProperty $uninstallKey "UninstallString" "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$installDir\uninstall.ps1`""
Set-ItemProperty $uninstallKey "EstimatedSize" $sizeKb
Set-ItemProperty $uninstallKey "NoModify" 1
Set-ItemProperty $uninstallKey "NoRepair" 1

Copy-Item (Join-Path $root "uninstall.ps1") $installDir -Force

Write-Host "`nVelocity installed." -ForegroundColor Green
Write-Host "Launch it from the Start Menu or Desktop, or run:`n  $exePath" -ForegroundColor Green
Write-Host "To uninstall: Settings > Apps, or run $installDir\uninstall.ps1" -ForegroundColor DarkGray
