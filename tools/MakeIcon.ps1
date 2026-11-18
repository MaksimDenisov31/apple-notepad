Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
[IO.Directory]::CreateDirectory((Join-Path $root 'assets')) | Out-Null
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = @()
foreach ($size in $sizes) {
    $bitmap = New-Object Drawing.Bitmap($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform($size / 256.0, $size / 256.0)
    $graphics.Clear([Drawing.Color]::Transparent)
    $paper = New-Object Drawing.Drawing2D.GraphicsPath
    $paper.AddLine(44,12,164,12); $paper.AddLine(164,12,226,74); $paper.AddLine(226,74,226,230)
    $paper.AddBezier(226,230,226,239,221,244,212,244); $paper.AddLine(212,244,44,244)
    $paper.AddBezier(44,244,34,244,30,239,30,230); $paper.AddLine(30,230,30,26)
    $paper.AddBezier(30,26,30,16,35,12,44,12); $paper.CloseFigure()
    $shadow = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(24,0,0,0))
    $graphics.TranslateTransform(3,4); $graphics.FillPath($shadow,$paper); $graphics.TranslateTransform(-3,-4)
    $fill = New-Object Drawing.Drawing2D.LinearGradientBrush((New-Object Drawing.Point(0,12)),(New-Object Drawing.Point(0,244)),[Drawing.Color]::White,[Drawing.Color]::FromArgb(248,248,248))
    $outline = New-Object Drawing.Pen([Drawing.Color]::FromArgb(178,178,178),[single][Math]::Max(2.5,192.0/$size))
    $graphics.FillPath($fill,$paper); $graphics.DrawPath($outline,$paper)
    $fold = New-Object Drawing.Drawing2D.GraphicsPath
    $fold.AddLine(164,12,164,60); $fold.AddBezier(164,60,164,70,170,74,178,74)
    $fold.AddLine(178,74,226,74); $fold.CloseFigure()
    $foldFill = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(232,232,232))
    $graphics.FillPath($foldFill,$fold); $graphics.DrawPath($outline,$fold)
    $pen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(150,150,150),[single][Math]::Max(7,230.0/$size))
    $pen.StartCap=[Drawing.Drawing2D.LineCap]::Round; $pen.EndCap=[Drawing.Drawing2D.LineCap]::Round
    foreach ($y in @(102,131,160,189)) { $graphics.DrawLine($pen,62,$y,194,$y) }
    $graphics.DrawLine($pen,62,218,162,218)
    $stream = New-Object IO.MemoryStream
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $images += ,$stream.ToArray()
    if($size -eq 256) { $bitmap.Save((Join-Path $root 'assets\app-icon.png'),[Drawing.Imaging.ImageFormat]::Png) }
    $stream.Dispose(); $pen.Dispose(); $foldFill.Dispose(); $fold.Dispose(); $outline.Dispose(); $fill.Dispose(); $shadow.Dispose(); $paper.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
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
