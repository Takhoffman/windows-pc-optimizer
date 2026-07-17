# Generates Assets/app.ico: a rounded gradient tile with a lightning bolt,
# matching the in-app theme (cyan -> violet). Multi-resolution (16..256).
Add-Type -AssemblyName System.Drawing

$sizes = 256, 128, 64, 48, 32, 16
$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root "Assets"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function New-Tile([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $c1 = [System.Drawing.Color]::FromArgb(255, 0x0A, 0x0D, 0x14)
    $c2 = [System.Drawing.Color]::FromArgb(255, 0x1B, 0x2A, 0x4A)
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, 45)

    $radius = [Math]::Max(2, [int]($size * 0.22))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($bgBrush, $path)

    # Bolt as a polygon, scaled to the tile.
    $pts = @(
        [System.Drawing.PointF]::new(0.58 * $size, 0.10 * $size),
        [System.Drawing.PointF]::new(0.30 * $size, 0.56 * $size),
        [System.Drawing.PointF]::new(0.46 * $size, 0.56 * $size),
        [System.Drawing.PointF]::new(0.40 * $size, 0.90 * $size),
        [System.Drawing.PointF]::new(0.72 * $size, 0.42 * $size),
        [System.Drawing.PointF]::new(0.54 * $size, 0.42 * $size)
    )
    $boltRect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $bc1 = [System.Drawing.Color]::FromArgb(255, 0x22, 0xD3, 0xEE)
    $bc2 = [System.Drawing.Color]::FromArgb(255, 0x8B, 0x5C, 0xF6)
    $boltBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($boltRect, $bc1, $bc2, 45)
    $g.FillPolygon($boltBrush, $pts)

    $g.Dispose()
    return $bmp
}

$pngStreams = foreach ($s in $sizes) {
    $bmp = New-Tile $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    [PSCustomObject]@{ Size = $s; Bytes = $ms.ToArray() }
    $bmp.Dispose()
}

$icoPath = Join-Path $outDir "app.ico"
$fs = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([UInt16]0)      # reserved
$bw.Write([UInt16]1)      # type: icon
$bw.Write([UInt16]$pngStreams.Count)

$offset = 6 + (16 * $pngStreams.Count)
foreach ($p in $pngStreams) {
    $dim = if ($p.Size -ge 256) { 0 } else { $p.Size }
    $bw.Write([byte]$dim)      # width (0 = 256)
    $bw.Write([byte]$dim)      # height
    $bw.Write([byte]0)         # palette
    $bw.Write([byte]0)         # reserved
    $bw.Write([UInt16]1)       # color planes
    $bw.Write([UInt16]32)      # bits per pixel
    $bw.Write([UInt32]$p.Bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $p.Bytes.Length
}
foreach ($p in $pngStreams) { $bw.Write($p.Bytes) }

$bw.Flush(); $fs.Close()
Write-Host "Wrote $icoPath"
