# Rasterize the user-provided SVG with a white taskbar backing plate.
# This dependency-free renderer supports the M/C/Z paths used by this asset;
# unsupported SVG features fail explicitly instead of silently changing it.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path -Parent $PSScriptRoot
[xml]$svg = Get-Content -LiteralPath (Join-Path $projectRoot 'assets/LumaDesk.svg') -Raw -Encoding UTF8
$ns = [Xml.XmlNamespaceManager]::new($svg.NameTable)
$ns.AddNamespace('svg', 'http://www.w3.org/2000/svg')
$group = $svg.SelectSingleNode('/svg:svg/svg:g', $ns)
if ($svg.DocumentElement.GetAttribute('viewBox') -ne '0 0 512 512' -or
    $group.GetAttribute('transform') -ne 'translate(0 72) scale(1 .8)') {
    throw 'Unsupported SVG canvas or transform; update the renderer before regenerating icons.'
}
$bitmap = [Drawing.Bitmap]::new(2048, 2048, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([Drawing.Color]::Transparent)
# Keep the SVG unchanged; only the Windows icon gets the requested white plate.
$plate = [Drawing.Drawing2D.GraphicsPath]::new()
try {
    # 32 px inset and 64 px corner radius on the 512 px icon canvas.
    $plate.AddArc(128, 128, 512, 512, 180, 90)
    $plate.AddArc(1408, 128, 512, 512, 270, 90)
    $plate.AddArc(1408, 1408, 512, 512, 0, 90)
    $plate.AddArc(128, 1408, 512, 512, 90, 90)
    $plate.CloseFigure()
    $graphics.FillPath([Drawing.Brushes]::White, $plate)
} finally { $plate.Dispose() }
# SVG coordinates: x = x, y = 72 + 0.8*y; supersample by four.
$matrix = [Drawing.Drawing2D.Matrix]::new(4, 0, 0, 3.2, 0, 288)
$graphics.Transform = $matrix
try {
    foreach ($element in $group.SelectNodes('svg:path', $ns)) {
        $tokens = [regex]::Matches($element.GetAttribute('d'), '[MCZ]|[-+]?(?:\d*\.\d+|\d+)(?:[eE][-+]?\d+)?')
        $unparsed = [regex]::Replace($element.GetAttribute('d'), '[MCZ]|[-+]?(?:\d*\.\d+|\d+)(?:[eE][-+]?\d+)?|[\s,]', '')
        if ($unparsed) { throw "Unsupported SVG path: $unparsed" }
        $path = [Drawing.Drawing2D.GraphicsPath]::new()
        $brush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($element.GetAttribute('fill')))
        try {
            $cursor = 0
            $currentX = [single]0
            $currentY = [single]0
            while ($cursor -lt $tokens.Count) {
                $command = $tokens[$cursor++].Value
                switch ($command) {
                    'M' {
                        $path.StartFigure()
                        $currentX = [single]::Parse($tokens[$cursor++].Value, [Globalization.CultureInfo]::InvariantCulture)
                        $currentY = [single]::Parse($tokens[$cursor++].Value, [Globalization.CultureInfo]::InvariantCulture)
                    }
                    'C' {
                        $points = @()
                        for ($index = 0; $index -lt 6; $index++) {
                            $points += [single]::Parse($tokens[$cursor++].Value, [Globalization.CultureInfo]::InvariantCulture)
                        }
                        $path.AddBezier($currentX, $currentY, $points[0], $points[1], $points[2], $points[3], $points[4], $points[5])
                        $currentX = $points[4]
                        $currentY = $points[5]
                    }
                    'Z' { $path.CloseFigure() }
                    default { throw "Unsupported SVG command: $command" }
                }
            }
            $graphics.FillPath($brush, $path)
        } finally { $brush.Dispose(); $path.Dispose() }
    }
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $images = @()
    foreach ($size in @($sizes) + @(512)) {
        $scaled = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $scaledGraphics = [Drawing.Graphics]::FromImage($scaled)
        $stream = [IO.MemoryStream]::new()
        try {
            $scaledGraphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $scaledGraphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $scaledGraphics.DrawImage($bitmap, 0, 0, $size, $size)
            $scaled.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            if ($size -eq 512) {
                [IO.File]::WriteAllBytes((Join-Path $projectRoot 'assets/LumaDesk.png'), $stream.ToArray())
            } else { $images += ,$stream.ToArray() }
        } finally { $stream.Dispose(); $scaledGraphics.Dispose(); $scaled.Dispose() }
    }
    $iconStream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($iconStream)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$images[$index].Length); $writer.Write([uint32]$offset)
            $offset += $images[$index].Length
        }
        foreach ($image in $images) { $writer.Write([byte[]]$image) }
        [IO.File]::WriteAllBytes((Join-Path $projectRoot 'assets/LumaDesk.ico'), $iconStream.ToArray())
    } finally { $writer.Dispose(); $iconStream.Dispose() }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'assets/LumaDesk.ico') -Destination (Join-Path $projectRoot 'app/Assets/AppIcon.ico') -Force
} finally { $matrix.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
