# Generates the WiX UI banner and dialog-background bitmaps, styled to match
# Velocity's in-app theme (dark navy -> violet gradient, cyan/violet bolt mark).
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root "Setup"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function New-Gradient([int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $c1 = [System.Drawing.Color]::FromArgb(255, 0x0A, 0x0D, 0x14)
    $c2 = [System.Drawing.Color]::FromArgb(255, 0x1B, 0x2A, 0x4A)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, 20)
    $g.FillRectangle($brush, $rect)
    return @{ Bmp = $bmp; G = $g }
}

function Add-Bolt($g, [single]$cx, [single]$cy, [single]$s) {
    $pts = @(
        [System.Drawing.PointF]::new($cx + 0.10 * $s, $cy - 0.42 * $s),
        [System.Drawing.PointF]::new($cx - 0.22 * $s, $cy + 0.06 * $s),
        [System.Drawing.PointF]::new($cx - 0.04 * $s, $cy + 0.06 * $s),
        [System.Drawing.PointF]::new($cx - 0.12 * $s, $cy + 0.42 * $s),
        [System.Drawing.PointF]::new($cx + 0.24 * $s, $cy - 0.10 * $s),
        [System.Drawing.PointF]::new($cx + 0.04 * $s, $cy - 0.10 * $s)
    )
    $rect = New-Object System.Drawing.Rectangle ([int]($cx - $s)), ([int]($cy - $s)), ([int]($s * 2)), ([int]($s * 2))
    $bc1 = [System.Drawing.Color]::FromArgb(255, 0x22, 0xD3, 0xEE)
    $bc2 = [System.Drawing.Color]::FromArgb(255, 0x8B, 0x5C, 0xF6)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $bc1, $bc2, 45)
    $g.FillPolygon($brush, $pts)
}

# --- Banner: 493x58, shown atop every wizard page ---
$b = New-Gradient 493 58
Add-Bolt $b.G 30 29 20
$font = New-Object System.Drawing.Font("Segoe UI Semibold", 13, [System.Drawing.FontStyle]::Bold)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0xEC, 0xF1, 0xFA))
$b.G.DrawString("Velocity", $font, $textBrush, 54, 16)
$sub = New-Object System.Drawing.Font("Segoe UI", 8)
$subBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0x93, 0xA0, 0xB8))
$b.G.DrawString("Gaming PC Optimizer", $sub, $subBrush, 54, 34)
$b.G.Dispose()
$b.Bmp.Save((Join-Path $outDir "Banner.bmp"), [System.Drawing.Imaging.ImageFormat]::Bmp)
$b.Bmp.Dispose()

# --- Dialog background: 493x312, shown on Welcome/Exit pages ---
$d = New-Gradient 493 312
Add-Bolt $d.G 110 100 46
$font2 = New-Object System.Drawing.Font("Segoe UI Semibold", 20, [System.Drawing.FontStyle]::Bold)
$d.G.DrawString("Velocity", $font2, $textBrush, 46, 160)
$sub2 = New-Object System.Drawing.Font("Segoe UI", 10)
$d.G.DrawString("Gaming PC Optimizer", $sub2, $subBrush, 48, 196)
$d.G.Dispose()
$d.Bmp.Save((Join-Path $outDir "Dialog.bmp"), [System.Drawing.Imaging.ImageFormat]::Bmp)
$d.Bmp.Dispose()

Write-Host "Wrote $outDir\Banner.bmp and $outDir\Dialog.bmp"
