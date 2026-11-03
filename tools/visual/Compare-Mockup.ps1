#requires -Version 7
<# Writes mockup | capture | absolute difference side by side, each scaled to 1440 px wide. Windows only. #>
param(
    [Parameter(Mandatory)][string]$Mockup,
    [Parameter(Mandatory)][string]$Capture,
    [Parameter(Mandatory)][string]$Out)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$W = 1440
function Resize($path) {
    $src = [System.Drawing.Image]::FromFile((Resolve-Path $path).Path)
    $h = [int][math]::Round($src.Height * $W / $src.Width)
    $bmp = New-Object System.Drawing.Bitmap $W, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.DrawImage($src, 0, 0, $W, $h)
    $g.Dispose(); $src.Dispose()
    $bmp
}
$a = Resize $Mockup; $b = Resize $Capture
$h = [math]::Min($a.Height, $b.Height)
$rect = [System.Drawing.Rectangle]::new(0, 0, $W, $h)
$da = $a.LockBits($rect, 'ReadOnly', 'Format32bppArgb'); $db = $b.LockBits($rect, 'ReadOnly', 'Format32bppArgb')
$bytes = $da.Stride * $h
$ba = New-Object byte[] $bytes; $bb = New-Object byte[] $bytes
[Runtime.InteropServices.Marshal]::Copy($da.Scan0, $ba, 0, $bytes)
[Runtime.InteropServices.Marshal]::Copy($db.Scan0, $bb, 0, $bytes)
$a.UnlockBits($da); $b.UnlockBits($db)
for ($i = 0; $i -lt $bytes; $i++) {
    $ba[$i] = if ($i % 4 -eq 3) { 255 } else { [byte][math]::Abs([int]$ba[$i] - [int]$bb[$i]) }
}
$diff = New-Object System.Drawing.Bitmap $W, $h
$dd = $diff.LockBits($rect, 'WriteOnly', 'Format32bppArgb')
[Runtime.InteropServices.Marshal]::Copy($ba, 0, $dd.Scan0, $bytes)
$diff.UnlockBits($dd)
$sheet = New-Object System.Drawing.Bitmap ($W * 3), $h
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.DrawImage($a, 0, 0); $g.DrawImage($b, $W, 0); $g.DrawImage($diff, $W * 2, 0)
$g.Dispose()
$full = [IO.Path]::GetFullPath($Out)
$null = New-Item -ItemType Directory -Force (Split-Path -Parent $full)
$sheet.Save($full, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Saved $Out ($($sheet.Width)x$($sheet.Height) px: mockup | capture | difference)"
