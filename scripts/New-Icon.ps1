$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDir = Join-Path $PSScriptRoot '..\src\Autoklicker\Assets'
New-Item -ItemType Directory -Path $assetDir -Force | Out-Null
$images = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.ScaleTransform($size / 64.0, $size / 64.0)
    $background = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 11, 11, 13))
    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 242, 242, 244))
    $outline = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 70, 70, 78), 1.4)
    $round = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $round.AddArc(1, 1, 18, 18, 180, 90)
    $round.AddArc(45, 1, 18, 18, 270, 90)
    $round.AddArc(45, 45, 18, 18, 0, 90)
    $round.AddArc(1, 45, 18, 18, 90, 90)
    $round.CloseFigure()
    $graphics.FillPath($background, $round)
    $graphics.DrawPath($outline, $round)
    $points = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(21, 16), [System.Drawing.PointF]::new(21, 45),
        [System.Drawing.PointF]::new(28, 38), [System.Drawing.PointF]::new(34, 50),
        [System.Drawing.PointF]::new(40, 47), [System.Drawing.PointF]::new(34, 35),
        [System.Drawing.PointF]::new(45, 35))
    $graphics.FillPolygon($white, $points)
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,@{ Size = $size; Bytes = $stream.ToArray() }
    $stream.Dispose(); $round.Dispose(); $outline.Dispose(); $white.Dispose(); $background.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$file = [System.IO.File]::Create((Join-Path $assetDir 'Autoklicker.ico'))
$writer = [System.IO.BinaryWriter]::new($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($entry in $images) {
    $dimension = if ($entry.Size -eq 256) { 0 } else { $entry.Size }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$entry.Bytes.Length); $writer.Write([uint32]$offset)
    $offset += $entry.Bytes.Length
}
foreach ($entry in $images) { $writer.Write([byte[]]$entry.Bytes) }
$writer.Dispose()
