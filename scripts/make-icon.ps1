<#
.SYNOPSIS
    Regenerates assets/app.ico.

.DESCRIPTION
    Draws the Corner Actions mark - a light card with two blue corner wedges - at
    every size Windows asks for and packs it into one .ico file.

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

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @()

foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(0.6, $size * 0.035)
    $radius = $size * 0.22
    $diameter = $radius * 2
    $card = New-Object System.Drawing.RectangleF($pad, $pad, ($size - 2 * $pad), ($size - 2 * $pad))

    $outline = New-Object System.Drawing.Drawing2D.GraphicsPath
    $outline.AddArc($card.X, $card.Y, $diameter, $diameter, 180, 90)
    $outline.AddArc(($card.Right - $diameter), $card.Y, $diameter, $diameter, 270, 90)
    $outline.AddArc(($card.Right - $diameter), ($card.Bottom - $diameter), $diameter, $diameter, 0, 90)
    $outline.AddArc($card.X, ($card.Bottom - $diameter), $diameter, $diameter, 90, 90)
    $outline.CloseFigure()

    $cardBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 250, 250, 250))
    $graphics.FillPath($cardBrush, $outline)

    $borderWidth = [Math]::Max(1.0, $size * 0.05)
    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 58, 58, 58), $borderWidth)
    $borderPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawPath($borderPen, $outline)

    # Two corner wedges, the same mark the tray icon shows.
    $accentBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0, 120, 212))
    $inset = $size * 0.15
    $leg = $size * 0.33

    $graphics.FillPolygon($accentBrush, @(
        (New-Object System.Drawing.PointF($inset, $inset)),
        (New-Object System.Drawing.PointF(($inset + $leg), $inset)),
        (New-Object System.Drawing.PointF($inset, ($inset + $leg)))))

    $right = $size - $inset
    $graphics.FillPolygon($accentBrush, @(
        (New-Object System.Drawing.PointF($right, $inset)),
        (New-Object System.Drawing.PointF(($right - $leg), $inset)),
        (New-Object System.Drawing.PointF($right, ($inset + $leg)))))

    if ($size -ge 256) {
        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $frames += ,@{ Size = $size; Data = $stream.ToArray() }
        $stream.Dispose()
    } else {
        $frames += ,@{ Size = $size; Data = [byte[]](ConvertTo-DibFrame $bitmap $size) }
    }

    $accentBrush.Dispose()
    $borderPen.Dispose()
    $cardBrush.Dispose()
    $outline.Dispose()
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
