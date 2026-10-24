$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = Join-Path $PSScriptRoot '..\assets'
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null
$bitmap = [System.Drawing.Bitmap]::new(256, 256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$shape = [System.Drawing.Drawing2D.GraphicsPath]::new()
$shape.AddArc(8, 8, 60, 60, 180, 90)
$shape.AddArc(188, 8, 60, 60, 270, 90)
$shape.AddArc(188, 188, 60, 60, 0, 90)
$shape.AddArc(8, 188, 60, 60, 90, 90)
$shape.CloseFigure()
$brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(65, 105, 225))
$graphics.FillPath($brush, $shape)
$font = [System.Drawing.Font]::new('Segoe UI', 146, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$format = [System.Drawing.StringFormat]::new()
$format.Alignment = [System.Drawing.StringAlignment]::Center
$format.LineAlignment = [System.Drawing.StringAlignment]::Center
$graphics.DrawString('P', $font, [System.Drawing.Brushes]::White, [System.Drawing.RectangleF]::new(0, -3, 256, 256), $format)
$memory = [System.IO.MemoryStream]::new()
$bitmap.Save($memory, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $memory.ToArray()
[System.IO.File]::WriteAllBytes((Join-Path $assetDirectory 'polypdf.png'), $png)
$file = [System.IO.File]::Create((Join-Path $assetDirectory 'polypdf.ico'))
$writer = [System.IO.BinaryWriter]::new($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$png.Length); $writer.Write([uint32]22)
$writer.Write($png)
$writer.Dispose(); $memory.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $shape.Dispose(); $brush.Dispose(); $font.Dispose(); $format.Dispose()
