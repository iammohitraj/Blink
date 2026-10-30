# Builds Blink's brand assets from the master artwork:
#   - blink-logo.png  (512 px rounded tile for docs and the repo README)
#   - blink.ico       (multi-resolution icon for the app, tray and installer)
# Small icon sizes use a tighter crop so the monitor stays readable in the tray.
[CmdletBinding()]
param(
    [string] $SourceImage,
    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$assetsDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\Blink\Assets'

if (-not $OutputDirectory) {
    $OutputDirectory = $assetsDirectory
}

if (-not $SourceImage) {
    $SourceImage = Join-Path $assetsDirectory 'blink-logo-source.png'
}

if (-not (Test-Path -LiteralPath $SourceImage)) {
    throw "Master artwork not found: $SourceImage"
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$cornerRatio = 0.235
$edgeColor = [System.Drawing.Color]::FromArgb(64, 255, 255, 255)
$iconSizes = 16, 20, 24, 32, 40, 48, 64, 128, 256

function New-RoundedPath {
    param(
        [float] $X,
        [float] $Y,
        [float] $Width,
        [float] $Height,
        [float] $Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = [Math]::Min($Radius * 2, [Math]::Min($Width, $Height))

    if ($diameter -le 0) {
        $path.AddRectangle([System.Drawing.RectangleF]::new($X, $Y, $Width, $Height))
        return $path
    }

    $right = $X + $Width - $diameter
    $bottom = $Y + $Height - $diameter

    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($right, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($right, $bottom, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $bottom, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

# Finds the painted area so the icon crop follows the artwork instead of hardcoded
# coordinates; the background of the master image is almost black.
function Get-ContentBounds {
    param(
        [System.Drawing.Bitmap] $Bitmap,
        [int] $Threshold = 30
    )

    $minX = $Bitmap.Width
    $minY = $Bitmap.Height
    $maxX = -1
    $maxY = -1

    for ($y = 0; $y -lt $Bitmap.Height; $y += 2) {
        for ($x = 0; $x -lt $Bitmap.Width; $x += 2) {
            $pixel = $Bitmap.GetPixel($x, $y)
            if ((($pixel.R + $pixel.G + $pixel.B) / 3) -gt $Threshold) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    if ($maxX -lt 0) {
        return [System.Drawing.Rectangle]::new(0, 0, $Bitmap.Width, $Bitmap.Height)
    }

    return [System.Drawing.Rectangle]::new($minX, $minY, ($maxX - $minX) + 1, ($maxY - $minY) + 1)
}

function New-SquareCrop {
    param(
        [System.Drawing.Rectangle] $Bounds,
        [int] $SourceWidth,
        [int] $SourceHeight,
        [int] $Side
    )

    $Side = [int][Math]::Min($Side, [Math]::Min($SourceWidth, $SourceHeight))
    $centerX = $Bounds.X + ($Bounds.Width / 2.0)
    $centerY = $Bounds.Y + ($Bounds.Height / 2.0)

    $x = [int][Math]::Round($centerX - ($Side / 2.0))
    $y = [int][Math]::Round($centerY - ($Side / 2.0))
    $x = [Math]::Max(0, [Math]::Min($x, $SourceWidth - $Side))
    $y = [Math]::Max(0, [Math]::Min($y, $SourceHeight - $Side))

    return [System.Drawing.Rectangle]::new($x, $y, $Side, $Side)
}

function New-TileBitmap {
    param(
        [System.Drawing.Image] $Source,
        [System.Drawing.Rectangle] $Crop,
        [int] $Size
    )

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $inset = [float][Math]::Max(0.5, $Size * 0.012)
    $radius = [float]($Size * $cornerRatio)
    $tile = New-RoundedPath -X $inset -Y $inset -Width ($Size - (2 * $inset)) -Height ($Size - (2 * $inset)) -Radius $radius

    $graphics.SetClip($tile)
    $destination = [System.Drawing.RectangleF]::new(0, 0, $Size, $Size)
    $graphics.DrawImage($Source, $destination, $Crop, [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.ResetClip()

    # A hairline edge keeps the dark tile readable on dark wallpapers and the taskbar.
    $strokeWidth = [float][Math]::Max(0.8, $Size * 0.01)
    $stroke = [System.Drawing.Pen]::new($edgeColor, $strokeWidth)
    $graphics.DrawPath($stroke, $tile)

    $stroke.Dispose()
    $tile.Dispose()
    $graphics.Dispose()

    return $bitmap
}

# Packs a bitmap as a 32bpp DIB (BITMAPINFOHEADER + BGRA rows + AND mask).
function Get-IconDib {
    param([System.Drawing.Bitmap] $Bitmap)

    $width = $Bitmap.Width
    $height = $Bitmap.Height

    $rectangle = [System.Drawing.Rectangle]::new(0, 0, $width, $height)
    $locked = $Bitmap.LockBits(
        $rectangle,
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $stride = $locked.Stride
    $pixels = [byte[]]::new($stride * $height)
    [System.Runtime.InteropServices.Marshal]::Copy($locked.Scan0, $pixels, 0, $pixels.Length)
    $Bitmap.UnlockBits($locked)

    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)

    $writer.Write([uint32]40)
    $writer.Write([int32]$width)
    $writer.Write([int32]($height * 2))
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]0)
    $writer.Write([uint32]($width * $height * 4))
    $writer.Write([int32]0)
    $writer.Write([int32]0)
    $writer.Write([uint32]0)
    $writer.Write([uint32]0)

    for ($y = $height - 1; $y -ge 0; $y--) {
        $rowStart = $y * $stride
        for ($x = 0; $x -lt $width; $x++) {
            $index = $rowStart + ($x * 4)
            $writer.Write($pixels[$index])
            $writer.Write($pixels[$index + 1])
            $writer.Write($pixels[$index + 2])
            $writer.Write($pixels[$index + 3])
        }
    }

    $maskRowBytes = [int]([Math]::Floor(($width + 31) / 32) * 4)
    $maskRow = [byte[]]::new($maskRowBytes)
    for ($y = 0; $y -lt $height; $y++) {
        $writer.Write([byte[]]$maskRow)
    }

    $writer.Flush()
    $bytes = $stream.ToArray()
    $writer.Dispose()
    $stream.Dispose()
    return , $bytes
}

$sourceStream = [System.IO.File]::OpenRead($SourceImage)
$source = [System.Drawing.Bitmap]::new($sourceStream)

try {
    $content = Get-ContentBounds -Bitmap $source
    $tightCrop = New-SquareCrop -Bounds $content -SourceWidth $source.Width -SourceHeight $source.Height -Side ([int][Math]::Round($content.Height * 1.25))
    $fullCrop = New-SquareCrop -Bounds $content -SourceWidth $source.Width -SourceHeight $source.Height -Side ([int][Math]::Round($content.Width * 1.18))
    $logoCrop = New-SquareCrop -Bounds ([System.Drawing.Rectangle]::new(0, 0, $source.Width, $source.Height)) -SourceWidth $source.Width -SourceHeight $source.Height -Side ([Math]::Min($source.Width, $source.Height))

    $frames = @()
    foreach ($size in $iconSizes) {
        $crop = if ($size -lt 48) { $tightCrop } else { $fullCrop }
        $bitmap = New-TileBitmap -Source $source -Crop $crop -Size $size
        $frames += [pscustomobject]@{
            Size = $size
            Data = (Get-IconDib -Bitmap $bitmap)
        }
        $bitmap.Dispose()
    }

    $iconPath = Join-Path $OutputDirectory 'blink.ico'
    $fileStream = [System.IO.File]::Create($iconPath)
    $iconWriter = [System.IO.BinaryWriter]::new($fileStream)
    $iconWriter.Write([uint16]0)
    $iconWriter.Write([uint16]1)
    $iconWriter.Write([uint16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
        $iconWriter.Write([byte]$dimension)
        $iconWriter.Write([byte]$dimension)
        $iconWriter.Write([byte]0)
        $iconWriter.Write([byte]0)
        $iconWriter.Write([uint16]1)
        $iconWriter.Write([uint16]32)
        $iconWriter.Write([uint32]$frame.Data.Length)
        $iconWriter.Write([uint32]$offset)
        $offset += $frame.Data.Length
    }

    foreach ($frame in $frames) {
        $iconWriter.Write([byte[]]$frame.Data)
    }

    $iconWriter.Flush()
    $iconWriter.Dispose()
    $fileStream.Dispose()

    $logoPath = Join-Path $OutputDirectory 'blink-logo.png'
    $logoBitmap = New-TileBitmap -Source $source -Crop $logoCrop -Size 512
    $logoBitmap.Save($logoPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $logoBitmap.Dispose()

    Write-Host "Source:       $SourceImage"
    Write-Host "Icon written: $iconPath"
    Write-Host "Logo written: $logoPath"
    Write-Host "Icon sizes:   $($iconSizes -join ', ')"
}
finally {
    $source.Dispose()
    $sourceStream.Dispose()
}
