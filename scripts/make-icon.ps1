<#
.SYNOPSIS
    Regenerates assets/app.ico.

.DESCRIPTION
    Draws the Corner Actions mark - a night-sky tile with amber brackets in the two
    top corners and a crescent moon below them - at every size Windows asks for
    and packs it into one .ico file.

    Frames up to 128 px are stored as uncompressed DIBs, because older readers
    (including System.Drawing on .NET Framework) render PNG-compressed frames as
    garbage. The 256 px frame is PNG, which every shell from Vista onwards reads
    and which keeps the file small. The file is committed, so this script only
    runs when the artwork changes.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\make-icon.ps1
#>
param(
    [string]$Output = (Join-Path (Split-Path -Parent $PSScriptRoot) "assets/app.ico")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function ConvertTo-DibFrame {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [int]$Size
    )

    $rect = New-Object System.Drawing.Rectangle(0, 0, $Size, $Size)
    $locked = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $stride = $locked.Stride
    $pixels = New-Object byte[] ($stride * $Size)
    [System.Runtime.InteropServices.Marshal]::Copy($locked.Scan0, $pixels, 0, $pixels.Length)
    $Bitmap.UnlockBits($locked)

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)

    # BITMAPINFOHEADER; Height is doubled because the DIB also carries the mask.
    $writer.Write([uint32]40)
    $writer.Write([int32]$Size)
    $writer.Write([int32]($Size * 2))
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]0)                 # BI_RGB
    $writer.Write([uint32]($Size * $Size * 4))
    $writer.Write([int32]0)
    $writer.Write([int32]0)
    $writer.Write([uint32]0)
    $writer.Write([uint32]0)

    # Bottom-up BGRA rows.
    for ($y = $Size - 1; $y -ge 0; $y--) {
        $writer.Write($pixels, $y * $stride, $Size * 4)
    }

    # 1bpp AND mask, zeroed: alpha decides transparency.
    $maskStride = [int]([Math]::Floor(($Size + 31) / 32) * 4)
    $writer.Write((New-Object byte[] ($maskStride * $Size)))

    $writer.Flush()
    $data = $stream.ToArray()
    $writer.Dispose()
    $stream.Dispose()

    # The comma keeps PowerShell from unrolling the byte array into the pipeline.
    return ,$data
}

# One corner bracket: a leg down the side, a rounded bend, a leg along the top.
# $Direction is 1 for the top-left bracket and -1 for its mirror on the right.
function New-BracketPath {
    param([single]$X, [single]$Y, [single]$Leg, [single]$Bend, [int]$Direction)

    $diameter = $Bend * 2
    $arcX = if ($Direction -gt 0) { $X } else { $X - $diameter }
    $arcStart = if ($Direction -gt 0) { 180 } else { 0 }

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddLine($X, ($Y + $Leg), $X, ($Y + $Bend))
    $path.AddArc($arcX, $Y, $diameter, $diameter, $arcStart, (90 * $Direction))
    $path.AddLine(($X + $Direction * $Bend), $Y, ($X + $Direction * $Leg), $Y)
    return $path
}

# A crescent: the moon disc minus a second disc pushed towards the upper right.
# Built from two arcs that meet where the circles intersect, so the edge stays
# anti-aliased (a GDI+ Region would fill with hard pixel edges).
function New-CrescentPath {
    param([single]$CenterX, [single]$CenterY, [single]$Radius)

    $theta = -45.0                       # cut direction, degrees, y axis down
    $offset = $Radius * 0.6
    $cutRadius = $Radius * 0.85

    $a = ($offset * $offset + $Radius * $Radius - $cutRadius * $cutRadius) / (2 * $offset)
    $h = [Math]::Sqrt($Radius * $Radius - $a * $a)
    $alpha = [Math]::Atan2($h, $a) * 180 / [Math]::PI
    $phi = [Math]::Atan2($h, $a - $offset) * 180 / [Math]::PI

    $cutX = $CenterX + $offset * [Math]::Cos($theta * [Math]::PI / 180)
    $cutY = $CenterY + $offset * [Math]::Sin($theta * [Math]::PI / 180)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(($CenterX - $Radius), ($CenterY - $Radius), (2 * $Radius), (2 * $Radius),
        [single]($theta + $alpha), [single](360 - 2 * $alpha))
    $path.AddArc(($cutX - $cutRadius), ($cutY - $cutRadius), (2 * $cutRadius), (2 * $cutRadius),
        [single]($theta - $phi), [single](-(360 - 2 * $phi)))
    $path.CloseFigure()
    return $path
}

$skyTop = [System.Drawing.Color]::FromArgb(255, 60, 52, 150)
$skyBottom = [System.Drawing.Color]::FromArgb(255, 20, 18, 60)
$amber = [System.Drawing.Color]::FromArgb(255, 255, 180, 64)
$moonLight = [System.Drawing.Color]::FromArgb(255, 255, 244, 214)

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @()

foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    # Small frames get thicker strokes and a larger moon so they survive the
    # 16-24 px tray sizes; large frames get finer proportions.
    $small = $size -le 24

    # Night-sky tile.
    $pad = [Math]::Max(0.5, $size * 0.03)
    $radius = $size * 0.225
    $diameter = $radius * 2
    $tile = New-Object System.Drawing.RectangleF($pad, $pad, ($size - 2 * $pad), ($size - 2 * $pad))

    $tilePath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $tilePath.AddArc($tile.X, $tile.Y, $diameter, $diameter, 180, 90)
    $tilePath.AddArc(($tile.Right - $diameter), $tile.Y, $diameter, $diameter, 270, 90)
    $tilePath.AddArc(($tile.Right - $diameter), ($tile.Bottom - $diameter), $diameter, $diameter, 0, 90)
    $tilePath.AddArc($tile.X, ($tile.Bottom - $diameter), $diameter, $diameter, 90, 90)
    $tilePath.CloseFigure()
    $skyBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($tile, $skyTop, $skyBottom, [single]80)
    $graphics.FillPath($skyBrush, $tilePath)

    # A faint rim keeps the tile apart from a dark taskbar.
    if ($size -ge 32) {
        $rimPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(40, 255, 255, 255), ($size * 0.015))
        $graphics.DrawPath($rimPen, $tilePath)
        $rimPen.Dispose()
    }

    # The two hot corners. Small strokes are whole pixels on whole (even) or half
    # (odd) coordinates, so the brackets stay crisp instead of smearing.
    if ($small) {
        $stroke = [Math]::Round($size * 0.125)
        $inset = [Math]::Round($size * 0.16) + ($stroke % 2) / 2
        $leg = $size * 0.16
    } else {
        $stroke = $size * 0.075
        $inset = $size * 0.19
        $leg = $size * 0.2
    }
    $bend = $size * 0.06
    $amberPen = New-Object System.Drawing.Pen($amber, $stroke)
    $amberPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $amberPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $amberPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $leftBracket = New-BracketPath $inset $inset $leg $bend 1
    $rightBracket = New-BracketPath ($size - $inset) $inset $leg $bend -1
    $graphics.DrawPath($amberPen, $leftBracket)
    $graphics.DrawPath($amberPen, $rightBracket)

    # The display going to sleep. The disc sits right of centre because the lit
    # part of a crescent is its lower-left half; this centres what the eye sees.
    $moonRadius = if ($small) { $size * 0.25 } else { $size * 0.21 }
    $moonPath = New-CrescentPath ($size * 0.53) ($size * 0.63) $moonRadius
    $moonBrush = New-Object System.Drawing.SolidBrush($moonLight)
    $graphics.FillPath($moonBrush, $moonPath)

    if ($size -ge 256) {
        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $frames += ,@{ Size = $size; Data = $stream.ToArray() }
        $stream.Dispose()
    } else {
        $frames += ,@{ Size = $size; Data = [byte[]](ConvertTo-DibFrame $bitmap $size) }
    }

    $moonBrush.Dispose()
    $moonPath.Dispose()
    $rightBracket.Dispose()
    $leftBracket.Dispose()
    $amberPen.Dispose()
    $skyBrush.Dispose()
    $tilePath.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

$directory = Split-Path -Parent $Output
if ($directory -and -not (Test-Path -LiteralPath $directory)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}

$file = [System.IO.File]::Create($Output)
$writer = New-Object System.IO.BinaryWriter($file)

$writer.Write([uint16]0)                 # reserved
$writer.Write([uint16]1)                 # type: icon
$writer.Write([uint16]$frames.Count)     # image count

$offset = 6 + (16 * $frames.Count)
foreach ($frame in $frames) {
    $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
    $writer.Write([byte]$dimension)      # width
    $writer.Write([byte]$dimension)      # height
    $writer.Write([byte]0)               # palette entries
    $writer.Write([byte]0)               # reserved
    $writer.Write([uint16]1)             # colour planes
    $writer.Write([uint16]32)            # bits per pixel
    $writer.Write([uint32]$frame.Data.Length)
    $writer.Write([uint32]$offset)
    $offset += $frame.Data.Length
}

foreach ($frame in $frames) {
    $writer.Write($frame.Data)
}

$writer.Flush()
$writer.Dispose()
$file.Dispose()

Write-Host ("Wrote {0} ({1} frames, {2:N1} KB)" -f $Output, $frames.Count, ((Get-Item -LiteralPath $Output).Length / 1KB))
