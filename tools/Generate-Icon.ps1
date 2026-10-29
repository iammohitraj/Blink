# Generates the Blink application icon (multi-resolution .ico) and a PNG brand logo.
# The artwork is drawn with System.Drawing, so every size stays crisp and no external
# design tools are required.
[CmdletBinding()]
param(
    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\Blink\Assets'
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$gradientStart = [System.Drawing.ColorTranslator]::FromHtml('#7C5CFF')
$gradientEnd = [System.Drawing.ColorTranslator]::FromHtml('#00C8FF')
$pupilColor = [System.Drawing.ColorTranslator]::FromHtml('#0B1020')
$edgeColor = [System.Drawing.Color]::FromArgb(56, 255, 255, 255)
$glossStart = [System.Drawing.Color]::FromArgb(64, 255, 255, 255)
$glossEnd = [System.Drawing.Color]::FromArgb(0, 255, 255, 255)
$specularColor = [System.Drawing.Color]::FromArgb(235, 255, 255, 255)

function New-RoundedPath {
    param(
        [float] $X,
        [float] $Y,
        [float] $Width,
        [float] $Height,
        [float] $Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2

    if ($diameter -le 0) {
        $rectangle = [System.Drawing.RectangleF]::new($X, $Y, $Width, $Height)
        $path.AddRectangle($rectangle)
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

# A lens / almond shaped "eye" built from two mirrored bezier curves.
function New-EyePath {
    param(
        [float] $CenterX,
        [float] $CenterY,
        [float] $HalfWidth,
        [float] $HalfHeight
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $left = $CenterX - $HalfWidth
    $right = $CenterX + $HalfWidth
    $controlX = $HalfWidth * 0.42
    $controlY = $HalfHeight * 1.22

    $path.StartFigure()
    $path.AddBezier(
        $left, $CenterY,
        ($CenterX - $controlX), ($CenterY - $controlY),
        ($CenterX + $controlX), ($CenterY - $controlY),
        $right, $CenterY)
    $path.AddBezier(
        $right, $CenterY,
        ($CenterX + $controlX), ($CenterY + $controlY),
        ($CenterX - $controlX), ($CenterY + $controlY),
        $left, $CenterY)
    $path.CloseFigure()
    return $path
}

function New-BlinkBitmap {
    param(
        [int] $Size,
        [switch] $Detailed
    )

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $padding = [Math]::Max(0.5, $Size * 0.035)
    $innerSize = $Size - ($padding * 2)
    $radius = $Size * 0.235

    $bounds = [System.Drawing.RectangleF]::new($padding, $padding, $innerSize, $innerSize)
    $tile = New-RoundedPath -X $padding -Y $padding -Width $innerSize -Height $innerSize -Radius $radius

    # Brand gradient tile.
    $gradient = [System.Drawing.Drawing2D.LinearGradientBrush]::new($bounds, $gradientStart, $gradientEnd, 55.0)
    $graphics.FillPath($gradient, $tile)

    # Soft top highlight for depth.
    $glossHeight = $innerSize * 0.58
    $glossBounds = [System.Drawing.RectangleF]::new($padding, $padding, $innerSize, $glossHeight)
    $gloss = [System.Drawing.Drawing2D.LinearGradientBrush]::new($glossBounds, $glossStart, $glossEnd, 90.0)
    $graphics.SetClip($tile)
    $graphics.FillRectangle($gloss, $glossBounds)
    $graphics.ResetClip()

    $centerX = $Size / 2.0
    $centerY = $Size / 2.0
    $eyeWidth = $Size * 0.295
    $eyeHeight = $Size * 0.175
    $eye = New-EyePath -CenterX $centerX -CenterY $centerY -HalfWidth $eyeWidth -HalfHeight $eyeHeight

    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $graphics.FillPath($white, $eye)

    $pupilRadius = $Size * 0.122
    $pupil = [System.Drawing.SolidBrush]::new($pupilColor)
    $graphics.FillEllipse($pupil, ($centerX - $pupilRadius), ($centerY - $pupilRadius), ($pupilRadius * 2), ($pupilRadius * 2))

    if ($Detailed) {
        $specularRadius = [Math]::Max(1.0, $Size * 0.038)
        $specularX = $centerX + ($pupilRadius * 0.10) - $specularRadius
        $specularY = $centerY - ($pupilRadius * 0.45) - $specularRadius
        $specular = [System.Drawing.SolidBrush]::new($specularColor)
        $graphics.FillEllipse($specular, $specularX, $specularY, ($specularRadius * 2), ($specularRadius * 2))
        $specular.Dispose()
    }

    # Hairline edge keeps the tile readable on light and dark wallpapers alike.
    $strokeWidth = [float][Math]::Max(0.9, $Size * 0.018)
    $stroke = [System.Drawing.Pen]::new($edgeColor, $strokeWidth)
    $graphics.DrawPath($stroke, $tile)

    $stroke.Dispose()
    $pupil.Dispose()
    $white.Dispose()
    $eye.Dispose()
    $gloss.Dispose()
    $gradient.Dispose()
    $tile.Dispose()
    $graphics.Dispose()

    return $bitmap
}

# Packs a bitmap as a 32bpp DIB (BITMAPINFOHEADER + BGRA rows + AND mask).
function Get-IconDib {
    param([System.Drawing.Bitmap] $Bitmap)

    $width = $Bitmap.Width
    $height = $Bitmap.Height

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
        for ($x = 0; $x -lt $width; $x++) {
            $pixel = $Bitmap.GetPixel($x, $y)
            $writer.Write([byte]$pixel.B)
            $writer.Write([byte]$pixel.G)
            $writer.Write([byte]$pixel.R)
            $writer.Write([byte]$pixel.A)
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

$iconSizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = @()

foreach ($size in $iconSizes) {
    $bitmap = New-BlinkBitmap -Size $size -Detailed:($size -ge 48)
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
$logoBitmap = New-BlinkBitmap -Size 512 -Detailed
$logoBitmap.Save($logoPath, [System.Drawing.Imaging.ImageFormat]::Png)
$logoBitmap.Dispose()

Write-Host "Icon written: $iconPath"
Write-Host "Logo written: $logoPath"
Write-Host "Icon sizes:   $($iconSizes -join ', ')"
