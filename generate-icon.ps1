param(
    [string]$OutputPath = (Join-Path $PSScriptRoot 'assets\StockPerpTicker.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$iconSizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = New-Object System.Collections.Generic.List[object]

foreach ($size in $iconSizes) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $padding = [single]($size * 0.06)
    $diameter = [single]($size - 2 * $padding)
    $background = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 8, 153, 129))
    $graphics.FillEllipse($background, $padding, $padding, $diameter, $diameter)

    $lineWidth = [single][Math]::Max(1, [Math]::Round($size * 0.06))
    $bodyWidth = [single][Math]::Max(2, [Math]::Round($size * 0.15))
    $whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, $lineWidth)
    $whitePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $whitePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)

    $candles = @(
        @{ X = 0.29; High = 0.28; Low = 0.72; Top = 0.40; Bottom = 0.61 },
        @{ X = 0.50; High = 0.20; Low = 0.61; Top = 0.29; Bottom = 0.48 },
        @{ X = 0.71; High = 0.34; Low = 0.78; Top = 0.48; Bottom = 0.66 }
    )

    foreach ($candle in $candles) {
        $x = [single]($size * $candle.X)
        $high = [single]($size * $candle.High)
        $low = [single]($size * $candle.Low)
        $top = [single]($size * $candle.Top)
        $bottom = [single]($size * $candle.Bottom)
        $graphics.DrawLine($whitePen, $x, $high, $x, $low)
        $graphics.FillRectangle($whiteBrush, [single]($x - $bodyWidth / 2), $top, $bodyWidth, [single]($bottom - $top))
    }

    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images.Add([pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() })

    $stream.Dispose()
    $whiteBrush.Dispose()
    $whitePen.Dispose()
    $background.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$fileStream = New-Object System.IO.FileStream($OutputPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$writer = New-Object System.IO.BinaryWriter($fileStream)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$images.Count)

$directoryEntryBytes = 16
$headerBytes = 6
$offset = $headerBytes + $directoryEntryBytes * $images.Count
foreach ($image in $images) {
    $dimension = if ($image.Size -ge 256) { [byte]0 } else { [byte]$image.Size }
    $writer.Write($dimension)
    $writer.Write($dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$image.Bytes.Length)
    $writer.Write([uint32]$offset)
    $offset += $image.Bytes.Length
}

foreach ($image in $images) {
    $writer.Write([byte[]]$image.Bytes)
}

$writer.Dispose()
$fileStream.Dispose()
Write-Host "图标已生成：$OutputPath"
