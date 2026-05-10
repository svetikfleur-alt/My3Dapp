param(
    [string]$OutputPath = "Assets\AppIcon.ico"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$size = 256
$bitmap = New-Object System.Drawing.Bitmap($size, $size)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

$cardRect = New-Object System.Drawing.RectangleF(8, 8, 240, 240)
$backgroundBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(33, 56, 122))
$borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(92, 122, 201), 6)
$accentBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(71, 214, 192))
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)

$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$radius = 34
$diameter = $radius * 2
$path.AddArc($cardRect.X, $cardRect.Y, $diameter, $diameter, 180, 90)
$path.AddArc($cardRect.Right - $diameter, $cardRect.Y, $diameter, $diameter, 270, 90)
$path.AddArc($cardRect.Right - $diameter, $cardRect.Bottom - $diameter, $diameter, $diameter, 0, 90)
$path.AddArc($cardRect.X, $cardRect.Bottom - $diameter, $diameter, $diameter, 90, 90)
$path.CloseFigure()

$graphics.FillPath($backgroundBrush, $path)
$graphics.DrawPath($borderPen, $path)

$fontM = New-Object System.Drawing.Font("Segoe UI Black", 118, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$font3 = New-Object System.Drawing.Font("Segoe UI Black", 84, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

$graphics.DrawString("M", $fontM, $textBrush, 38, 52)
$graphics.DrawString("3", $font3, $accentBrush, 136, 73)
$graphics.FillRectangle($accentBrush, 132, 182, 74, 12)

$pngStream = New-Object System.IO.MemoryStream
$bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $pngStream.ToArray()

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory) -and -not (Test-Path $outputDirectory)) {
    New-Item -Path $outputDirectory -ItemType Directory | Out-Null
}

$fileStream = [System.IO.File]::Open($OutputPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$writer = New-Object System.IO.BinaryWriter($fileStream)

# ICO header
$writer.Write([UInt16]0)      # Reserved
$writer.Write([UInt16]1)      # Image type: icon
$writer.Write([UInt16]1)      # Image count

# Directory entry for one 256x256 PNG (0 = 256 in ICO format)
$writer.Write([Byte]0)        # Width
$writer.Write([Byte]0)        # Height
$writer.Write([Byte]0)        # Color count
$writer.Write([Byte]0)        # Reserved
$writer.Write([UInt16]1)      # Planes
$writer.Write([UInt16]32)     # Bit count
$writer.Write([UInt32]$pngBytes.Length)  # Image size
$writer.Write([UInt32]22)     # Data offset (6 + 16)

$writer.Write($pngBytes)
$writer.Flush()
$writer.Dispose()
$fileStream.Dispose()

$fontM.Dispose()
$font3.Dispose()
$textBrush.Dispose()
$accentBrush.Dispose()
$borderPen.Dispose()
$backgroundBrush.Dispose()
$path.Dispose()
$graphics.Dispose()
$bitmap.Dispose()
$pngStream.Dispose()

Write-Host "Generated icon at $OutputPath"
