# Builds the self-contained, single-file release exe used by install.ps1.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish"

Push-Location $root
try {
    dotnet publish Velocity.csproj -c Release -r win-x64 --self-contained true -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
    Write-Host "`nPublished to $publishDir" -ForegroundColor Cyan
}
finally {
    Pop-Location
}
