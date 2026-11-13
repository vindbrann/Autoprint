# Icône de l'installeur : symbole Autoprint + pastille « installation » (flèche vers un plateau), multi-tailles PNG dans un .ico
param([string]$Source, [string]$Out, [string]$PreviewPng)
Add-Type -AssemblyName System.Drawing
$src = [Drawing.Image]::FromFile($Source)

# Image maîtresse 512 px
$master = New-Object Drawing.Bitmap 512, 512
$g = [Drawing.Graphics]::FromImage($master)
$g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'
$g.Clear([Drawing.Color]::Transparent)
$g.DrawImage($src, 0, 0, 440, 440)   # symbole décalé en haut à gauche pour laisser la place à la pastille

# Pastille : cercle vert (charte : succès) cerclé de blanc, en bas à droite
$cx = 372; $cy = 372; $r = 132
$g.FillEllipse([Drawing.Brushes]::White, $cx - $r - 14, $cy - $r - 14, 2 * ($r + 14), 2 * ($r + 14))
$green = New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml('#1e8a4c'))
$g.FillEllipse($green, $cx - $r, $cy - $r, 2 * $r, 2 * $r)

# Flèche vers le bas + plateau
$pen = New-Object Drawing.Pen ([Drawing.Color]::White), 30
$pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
$g.DrawLine($pen, $cx, $cy - 78, $cx, $cy + 22)
$g.DrawLines($pen, [Drawing.Point[]]@((New-Object Drawing.Point ($cx - 44), ($cy - 18)), (New-Object Drawing.Point $cx, ($cy + 26)), (New-Object Drawing.Point ($cx + 44), ($cy - 18))))
$g.DrawLines($pen, [Drawing.Point[]]@((New-Object Drawing.Point ($cx - 66), ($cy + 34)), (New-Object Drawing.Point ($cx - 66), ($cy + 70)), (New-Object Drawing.Point ($cx + 66), ($cy + 70)), (New-Object Drawing.Point ($cx + 66), ($cy + 34))))
$g.Dispose()
if ($PreviewPng) { $master.Save($PreviewPng, [Drawing.Imaging.ImageFormat]::Png) }

# .ico : une entrée PNG par taille
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
    $bmp = New-Object Drawing.Bitmap $s, $s
    $gg = [Drawing.Graphics]::FromImage($bmp); $gg.InterpolationMode = 'HighQualityBicubic'; $gg.SmoothingMode = 'AntiAlias'; $gg.PixelOffsetMode = 'HighQuality'
    $gg.DrawImage($master, 0, 0, $s, $s); $gg.Dispose()
    $ms = New-Object IO.MemoryStream; $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    , $ms.ToArray()
}
$fs = [IO.File]::Create($Out); $w = New-Object IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close(); $master.Dispose(); $src.Dispose()
"icône : $Out ($((Get-Item $Out).Length) octets)"
