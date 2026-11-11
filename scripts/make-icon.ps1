# Generates the Taveloom brand mark (original artwork: a rounded square holding a 2x2 grid of control tiles).
# Outputs assets/branding/taveloom.png and apps/windows/Taveloom.App/Assets/taveloom.ico (+ png for window icon).
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure(); return $p
}

function New-Mark([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 256.0
    $bg = New-RoundedPath (8 * $s) (8 * $s) (240 * $s) (240 * $s) (56 * $s)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 99, 112, 242))), $bg)
    $tiles = @(@(56, 56, 255), @(140, 56, 255), @(56, 140, 255), @(140, 140, 110))
    foreach ($t in $tiles) {
        $path = New-RoundedPath ($t[0] * $s) ($t[1] * $s) (60 * $s) (60 * $s) (16 * $s)
        $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb($t[2], 255, 255, 255))), $path)
    }
    $g.Dispose(); return $bmp
}

$sizes = 16, 32, 48, 256
$pngs = foreach ($sz in $sizes) {
    $ms = New-Object System.IO.MemoryStream
    (New-Mark $sz).Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    ,$ms.ToArray()
}
New-Item -ItemType Directory -Force "$root\assets\branding", "$root\apps\windows\Taveloom.App\Assets" | Out-Null
(New-Mark 512).Save("$root\assets\branding\taveloom.png", [System.Drawing.Imaging.ImageFormat]::Png)
(New-Mark 64).Save("$root\apps\windows\Taveloom.App\Assets\taveloom.png", [System.Drawing.Imaging.ImageFormat]::Png)

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]; $b = if ($sz -ge 256) { 0 } else { $sz }
    $w.Write([byte]$b); $w.Write([byte]$b); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
[System.IO.File]::WriteAllBytes("$root\apps\windows\Taveloom.App\Assets\taveloom.ico", $out.ToArray())
Write-Host "Icon written."
