Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
[IO.Directory]::CreateDirectory((Join-Path $root 'assets')) | Out-Null
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $bitmap = New-Object Drawing.Bitmap($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform($size / 256.0, $size / 256.0)
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $path.AddArc(8,8,64,64,180,90); $path.AddArc(184,8,64,64,270,90)
    $path.AddArc(184,184,64,64,0,90); $path.AddArc(8,184,64,64,90,90); $path.CloseFigure()
    $blue = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(0,122,255))
    $graphics.FillPath($blue,$path)
    $paper = New-Object Drawing.Drawing2D.GraphicsPath
    $paper.AddLines([Drawing.PointF[]]@((New-Object Drawing.PointF(66,42)),(New-Object Drawing.PointF(158,42)),(New-Object Drawing.PointF(194,78)),(New-Object Drawing.PointF(194,214)),(New-Object Drawing.PointF(66,214))))
    $paper.CloseFigure(); $graphics.FillPath([Drawing.Brushes]::White,$paper)
    $pen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(143,163,189),7)
    foreach ($y in @(108,136,164)) { $graphics.DrawLine($pen,89,$y,171,$y) }
    $stream = New-Object IO.MemoryStream
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $images += ,$stream.ToArray()
    $stream.Dispose(); $pen.Dispose(); $paper.Dispose(); $blue.Dispose(); $path.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$file = [IO.File]::Create((Join-Path $root 'assets\app.ico'))
$writer = New-Object IO.BinaryWriter($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i=0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write([byte[]]$image) }
$writer.Dispose(); $file.Dispose()
