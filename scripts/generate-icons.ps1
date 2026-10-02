$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
$bitmap = [Drawing.Bitmap]::new(256,256)
$g = [Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([Drawing.Color]::FromArgb(255,27,32,45))
$cyan = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,62,199,242))
$light = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,226,235,249))
$pen = [Drawing.Pen]::new($cyan,14)
$pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
$g.DrawRectangle($pen,48,50,160,112)
$g.FillRectangle($cyan,116,164,24,25)
$g.FillRectangle($light,82,191,92,12)
$g.FillPolygon($light,[Drawing.Point[]]@([Drawing.Point]::new(112,70),[Drawing.Point]::new(94,117),[Drawing.Point]::new(122,117),[Drawing.Point]::new(113,143),[Drawing.Point]::new(162,96),[Drawing.Point]::new(132,96),[Drawing.Point]::new(145,70)))
$bitmap.Save((Join-Path $projectRoot 'assets/LumaDesk.png'),[Drawing.Imaging.ImageFormat]::Png)
foreach($file in Get-ChildItem (Join-Path $projectRoot 'app/Assets') -Filter '*.png') { $bitmap.Save($file.FullName,[Drawing.Imaging.ImageFormat]::Png) }
$png = [IO.File]::ReadAllBytes((Join-Path $projectRoot 'assets/LumaDesk.png'))
$stream = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($stream)
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]1)
$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([byte]0)
$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$png.Length);$writer.Write([uint32]22);$writer.Write($png)
[IO.File]::WriteAllBytes((Join-Path $projectRoot 'assets/LumaDesk.ico'),$stream.ToArray())
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets/LumaDesk.ico') -Destination (Join-Path $projectRoot 'app/Assets/AppIcon.ico') -Force
$writer.Dispose();$stream.Dispose();$pen.Dispose();$cyan.Dispose();$light.Dispose();$g.Dispose();$bitmap.Dispose()
