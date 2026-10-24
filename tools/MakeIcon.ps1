param([string]$SourcePath = (Join-Path $PSScriptRoot '../assets/polypdf.png'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$output = Join-Path $PSScriptRoot '../assets/polypdf.ico'
$source = [System.Drawing.Bitmap]::new([IO.Path]::GetFullPath($SourcePath))
$sizes = @(16,20,24,32,40,48,64,96,128,256)
$frames = @()
try {
 foreach ($size in $sizes) {
  $bitmap = [System.Drawing.Bitmap]::new($size,$size,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  $memory = [IO.MemoryStream]::new()
  try {
   $graphics.Clear([System.Drawing.Color]::Transparent)
   $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
   $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
   $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
   $scale = [Math]::Min($size / [double]$source.Width,$size / [double]$source.Height)
   $width = [single]($source.Width * $scale); $height = [single]($source.Height * $scale)
   $graphics.DrawImage($source,[System.Drawing.RectangleF]::new(($size-$width)/2,($size-$height)/2,$width,$height))
   if ($size -eq 256) {
    $bitmap.Save($memory,[System.Drawing.Imaging.ImageFormat]::Png)
   } else {
    # Traditional 32-bit DIB frames also work in installer and shell icon readers.
    $writer = [IO.BinaryWriter]::new($memory,[Text.Encoding]::UTF8,$true)
    try {
     $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
     $writer.Write([uint32]40); $writer.Write([int]$size); $writer.Write([int]($size*2))
     $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]0)
     $writer.Write([uint32]($size*$size*4+$maskStride*$size))
     1..4 | ForEach-Object { $writer.Write([uint32]0) }
     for ($y=$size-1; $y -ge 0; $y--) {
      for ($x=0; $x -lt $size; $x++) {
       $pixel=$bitmap.GetPixel($x,$y)
       $writer.Write([byte]$pixel.B); $writer.Write([byte]$pixel.G)
       $writer.Write([byte]$pixel.R); $writer.Write([byte]$pixel.A)
      }
     }
     for ($y=$size-1; $y -ge 0; $y--) {
      $mask=[byte[]]::new($maskStride)
      for ($x=0; $x -lt $size; $x++) {
       if ($bitmap.GetPixel($x,$y).A -eq 0) {
        $index=[int][Math]::Floor($x/8.0)
        $mask[$index]=$mask[$index] -bor (0x80 -shr ($x%8))
       }
      }
      $writer.Write($mask)
     }
    } finally { $writer.Dispose() }
   }
   $frames += ,$memory.ToArray()
  } finally { $memory.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
 }
 $file=[IO.File]::Create($output)
 $writer=[IO.BinaryWriter]::new($file)
 try {
  $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
  $offset=6+16*$sizes.Count
  for ($i=0; $i -lt $sizes.Count; $i++) {
   $dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
   $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
   $writer.Write([byte]0); $writer.Write([byte]0)
   $writer.Write([uint16]1); $writer.Write([uint16]32)
   $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
   $offset += $frames[$i].Length
  }
  foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
 } finally { $writer.Dispose() }
} finally { $source.Dispose() }
Write-Output ('Created Windows icon sizes: '+($sizes -join ', '))
