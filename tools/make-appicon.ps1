# Génère src/ShiftBridge.App/Assets/appicon.ico à partir des SVG de l'icône Android
# (src/ShiftBridge.Android/Resources/AppIcon/appicon.svg + appiconfg.svg).
# Usage : pwsh tools/make-appicon.ps1
Add-Type -AssemblyName PresentationCore, WindowsBase

$root   = Split-Path -Parent $PSScriptRoot
$iconDir = Join-Path $root 'src\ShiftBridge.Android\Resources\AppIcon'
$outDir = Join-Path $root 'src\ShiftBridge.App\Assets'
New-Item -ItemType Directory -Force $outDir | Out-Null

# Lecture des couleurs et de la géométrie depuis les SVG (source de vérité unique).
$bgSvg = Get-Content (Join-Path $iconDir 'appicon.svg') -Raw
$fgSvg = Get-Content (Join-Path $iconDir 'appiconfg.svg') -Raw
$bgColor = [regex]::Match($bgSvg, 'fill="(#[0-9A-Fa-f]{6})"').Groups[1].Value
$fgColor = [regex]::Match($fgSvg, 'fill="(#[0-9A-Fa-f]{6})"').Groups[1].Value
$pathData = [regex]::Match($fgSvg, '<path d="([^"]+)"').Groups[1].Value
$canvas = 456.0
if (-not $bgColor -or -not $fgColor -or -not $pathData) { throw 'Impossible de lire les SVG Android.' }

$bgBrush = [System.Windows.Media.BrushConverter]::new().ConvertFromString($bgColor)
$fgBrush = [System.Windows.Media.BrushConverter]::new().ConvertFromString($fgColor)
$geometry = [System.Windows.Media.Geometry]::Parse($pathData)

function New-PngBytes([int]$size) {
    $dv = [System.Windows.Media.DrawingVisual]::new()
    $dc = $dv.RenderOpen()
    $scale = $size / $canvas
    $dc.PushTransform([System.Windows.Media.ScaleTransform]::new($scale, $scale))
    # Fond arrondi (comme le masque de l'icône adaptative Android)
    $dc.DrawRoundedRectangle($bgBrush, $null, [System.Windows.Rect]::new(0, 0, $canvas, $canvas), 90, 90)
    $dc.DrawGeometry($fgBrush, $null, $geometry)
    $dc.Pop()
    $dc.Close()
    $rtb = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($dv)
    $enc = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $ms = [System.IO.MemoryStream]::new()
    $enc.Save($ms)
    return ,$ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) { ,(New-PngBytes $s) }

# Conteneur ICO : ICONDIR (6 o) + ICONDIRENTRY (16 o) par image + données PNG.
$out = [System.IO.MemoryStream]::new()
$w = [System.IO.BinaryWriter]::new($out)
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Flush()

$icoPath = Join-Path $outDir 'appicon.ico'
[System.IO.File]::WriteAllBytes($icoPath, $out.ToArray())
Write-Host "Écrit $icoPath ($($sizes -join ', ') px)"
