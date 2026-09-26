# Generates BatteryTray\app.ico - the exe / window icon - from code, so it can be re-made or
# tweaked without an image editor. Draws each size separately (not downscaled) so small sizes
# stay crisp. Sizes below 64 are stored as classic 32-bit bitmaps (readable by every tool);
# 64 and up are stored as PNG to keep the file small.
# Run: powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-icon.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$outPath = Join-Path $root 'BatteryTray\app.ico'
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $d = [Math]::Min($r * 2, [Math]::Min($w, $h))
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

# Draws the icon on a 256-unit grid, scaled to $size pixels.
function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $k = $size / 256.0
    $g.ScaleTransform($k, $k)

    $dark = [System.Drawing.Color]::FromArgb(43, 43, 43)
    $green = [System.Drawing.Color]::FromArgb(21, 163, 74)

    # Outline gets relatively thicker at small sizes so it never drops below ~1.5px.
    $stroke = [Math]::Max(16.0, 1.5 / $k)

    # Terminal nub on the right.
    $nub = New-RoundedRect 214 98 30 60 10
    $g.FillPath((New-Object System.Drawing.SolidBrush $dark), $nub)

    # Body: white inside, dark outline.
    $body = New-RoundedRect 12 56 204 144 28
    $g.FillPath([System.Drawing.Brushes]::White, $body)

    # Charge level (about 75%).
    $inner = 12 + $stroke / 2 + 10
    $fill = New-RoundedRect $inner (56 + $stroke / 2 + 10) (150 - $inner + 12) (144 - $stroke - 20) 12
    $g.FillPath((New-Object System.Drawing.SolidBrush $green), $fill)

    $pen = New-Object System.Drawing.Pen $dark, $stroke
    $pen.LineJoin = 'Round'
    $g.DrawPath($pen, $body)

    # Lightning bolt: white with a dark edge, skipped where it would only be noise.
    if ($size -ge 24) {
        $bolt = New-Object System.Drawing.Drawing2D.GraphicsPath
        $bolt.AddPolygon([System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new(132, 66),
            [System.Drawing.PointF]::new(80, 138),
            [System.Drawing.PointF]::new(112, 138),
            [System.Drawing.PointF]::new(96, 192),
            [System.Drawing.PointF]::new(152, 112),
            [System.Drawing.PointF]::new(120, 112),
            [System.Drawing.PointF]::new(140, 66)))
        $edge = New-Object System.Drawing.Pen $dark, ([Math]::Max(8.0, 1.0 / $k))
        $edge.LineJoin = 'Round'
        $g.DrawPath($edge, $bolt)
        $g.FillPath([System.Drawing.Brushes]::White, $bolt)
    }

    $g.Dispose()
    return $bmp
}

# Classic icon image: BITMAPINFOHEADER, bottom-up BGRA pixels, then an all-zero AND mask
# (transparency comes from the alpha channel).
function ConvertTo-IconDib([System.Drawing.Bitmap]$bmp) {
    $s = $bmp.Width
    $rect = New-Object System.Drawing.Rectangle 0, 0, $s, $s
    $data = $bmp.LockBits($rect, 'ReadOnly', [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $pixels = New-Object byte[] ($data.Stride * $s)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    $bmp.UnlockBits($data)

    $maskStride = [int]([Math]::Ceiling($s / 32.0) * 4)
    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter $ms
    $w.Write([UInt32]40); $w.Write([Int32]$s); $w.Write([Int32]($s * 2))
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]0)
    $w.Write([UInt32]($s * $s * 4 + $maskStride * $s))
    $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)
    for ($y = $s - 1; $y -ge 0; $y--) { $w.Write($pixels, $y * $data.Stride, $s * 4) }
    $w.Write((New-Object byte[] ($maskStride * $s)))
    $w.Flush()
    return , $ms.ToArray()
}

# ICO file: header, one directory entry per image, then the image data blocks.
$pngs = foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    if ($s -ge 64) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bytes = $ms.ToArray()
    } else {
        $bytes = ConvertTo-IconDib $bmp
    }
    $bmp.Dispose()
    , $bytes
}

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim)
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$pngs[$i].Length); $w.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Flush()
[System.IO.File]::WriteAllBytes($outPath, $out.ToArray())

Write-Host "Wrote $outPath ($($sizes -join ', ') px)" -ForegroundColor Green
