# Generates docs/images/social-preview.png (1280x640) for GitHub Settings -> Social preview.
Add-Type -AssemblyName System.Drawing

$root = Join-Path $PSScriptRoot ".."
$icoPath = Join-Path $root "src/SnapText/SnapText.ico"
$ico = [System.IO.File]::ReadAllBytes($icoPath)
$count = [BitConverter]::ToUInt16($ico, 4)
$png = $null
for ($i = 0; $i -lt $count; $i++) {
  $off = 6 + 16 * $i
  $dim = $ico[$off]
  if ($dim -eq 0) {   # 256px entry (PNG)
    $size = [BitConverter]::ToUInt32($ico, $off + 8)
    $start = [BitConverter]::ToUInt32($ico, $off + 12)
    $png = New-Object byte[] $size
    [Array]::Copy($ico, $start, $png, 0, $size)
    break
  }
}
if (-not $png) { throw "256px PNG entry not found in ico" }
$iconStream = New-Object System.IO.MemoryStream(,$png)

$W = 1280; $H = 640
$bmp = New-Object System.Drawing.Bitmap $W, $H
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

$bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 13, 27, 42))
$g.FillRectangle($bg, 0, 0, $W, $H)
$accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 120, 215))
$g.FillRectangle($accent, 0, 0, 14, $H)

$icon = [System.Drawing.Image]::FromStream($iconStream)
$g.DrawImage($icon, 96, 192, 256, 256)
$icon.Dispose()

$white = [System.Drawing.Brushes]::White
$blue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 130, 190, 240))
$grey = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 150, 165, 180))

$fTitle = New-Object System.Drawing.Font ('Segoe UI', 96, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$g.DrawString('SnapText', $fTitle, $white, 440, 150)

$fSub = New-Object System.Drawing.Font ('Segoe UI', 34, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$g.DrawString('Hotkey ' + [char]0x2192 + ' drag a box ' + [char]0x2192 + ' text in your clipboard', $fSub, $blue, 446, 292)

$fCjk = New-Object System.Drawing.Font ('Microsoft YaHei UI', 34, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$g.DrawString([string][char]0x6309 + [string][char]0x4E00 + [string][char]0x4E0B + [string][char]0x70ED + [string][char]0x952E + [char]0xFF0C + [string][char]0x6846 + [string][char]0x9009 + [string][char]0x5373 + [string][char]0x5F97 + [string][char]0x6587 + [string][char]0x5B57 + [char]0x3000 + [char]0xB7 + [char]0x3000 + 'OCR', $fCjk, $blue, 446, 352)

$fSmall = New-Object System.Drawing.Font ('Segoe UI', 26, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$g.DrawString('Windows ' + [char]0xB7 + ' Offline ' + [char]0xB7 + ' MIT ' + [char]0xB7 + ' EN / JA / ZH', $fSmall, $grey, 446, 430)

$g.Dispose()
$out = Join-Path $root "docs/images/social-preview.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host ("SOCIAL_PREVIEW written: " + $out + " bytes=" + (Get-Item $out).Length)
