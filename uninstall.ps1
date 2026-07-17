#Requires -Version 5.1
<#
    Removes Velocity: install directory, shortcuts, and the Add/Remove
    Programs entry. Does not touch any tweak Velocity applied - use the
    app's own "Revert everything" first if you want your original Windows
    settings back before uninstalling.
#>
$ErrorActionPreference = "SilentlyContinue"
$appName = "Velocity"
$installDir = Join-Path $env:LOCALAPPDATA "Programs\$appName"

Write-Host "Uninstalling Velocity..." -ForegroundColor Cyan

Get-Process -Name $appName -ErrorAction SilentlyContinue | Stop-Process -Force

Remove-Item (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$appName.lnk") -Force
Remove-Item (Join-Path ([Environment]::GetFolderPath("Desktop")) "$appName.lnk") -Force
Remove-Item "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appName" -Recurse -Force

Write-Host "Velocity has been removed. Your backup file (if any) is kept at:" -ForegroundColor Green
Write-Host "  $env:ProgramData\Velocity\backup.json" -ForegroundColor Green
Write-Host "Delete it manually if you don't need to revert any remaining tweaks." -ForegroundColor DarkGray

# Remove the install directory last - this script may be running from inside it.
$cleanupCmd = "Start-Sleep -Milliseconds 500; Remove-Item -Recurse -Force '$installDir'"
Start-Process powershell -ArgumentList "-NoProfile", "-WindowStyle", "Hidden", "-Command", $cleanupCmd
