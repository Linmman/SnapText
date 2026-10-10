# Generates src/SnapText/SnapText.ico.
# Layout: 16/32/48 as raw 32bpp DIB entries (maximum GDI compatibility),
#         256 as a PNG entry (standard since Vista, used by Explorer).
# Same design as the old runtime-drawn tray icon: blue circle + white brand glyph.
param([string]$OutPath = (Join-Path $PSScriptRoot "..\src\SnapText\SnapText.ico"))

Add-Type -AssemblyName System.Drawing

$glyph = [char]0x5B57   # U+5B57, the brand glyph

function ConvertTo-IcoDib([System.Drawing.Bitmap]$bmp) {
  $w = $bmp.Width; $h = $bmp.Height
  $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
  $pix = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  try {
    $xor = New-Object byte[] ($w * $h * 4)
    [System.Runtime.InteropServices.Marshal]::Copy($pix.Scan0, $xor, 0, $xor.Length)
  }
  finally { $bmp.UnlockBits($pix) }

  $ms = New-Object System.IO.MemoryStream
  $bw = New-Object System.IO.BinaryWriter $ms
  # BITMAPINFOHEADER; biHeight doubled because an ICO DIB carries XOR+AND masks
  $bw.Write([UInt32]40)
  $bw.Write([Int32]$w)
  $bw.Write([Int32]($h * 2))
  $bw.Write([UInt16]1)
  $bw.Write([UInt16]32)
  $bw.Write([UInt32]0)                       # BI_RGB
  $bw.Write([UInt32]0)                       # biSizeImage
  $bw.Write([Int32]0); $bw.Write([Int32]0)   # pels per meter
  $bw.Write([UInt32]0); $bw.Write([UInt32]0) # clr used / important
  $stride = $w * 4
  for ($y = $h - 1; $y -ge 0; $y--) { $bw.Write($xor, $y * $stride, $stride) }  # bottom-up BGRA
  $maskStride = [Math]::Ceiling($w / 32.0) * 4
  $zeroRow = New-Object byte[] $maskStride
  for ($y = 0; $y -lt $h; $y++) { $bw.Write($zeroRow) }                          # AND mask: alpha channel rules
  $bw.Flush()
  $bytes = $ms.ToArray()
  $bw.Dispose(); $ms.Dispose()
  return ,$bytes
}

function ConvertTo-Png([System.Drawing.Bitmap]$bmp) {
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $bytes = $ms.ToArray()
  $ms.Dispose()
  return ,$bytes
}

function New-IconBitmap([int]$s) {
  $bmp = New-Object System.Drawing.Bitmap $s, $s
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  try {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 120, 215))
    $g.FillEllipse($bg, 0, 0, $s - 1, $s - 1)

    $fontSize = [Math]::Floor($s * 0.6)
    $font = New-Object System.Drawing.Font ('Microsoft YaHei UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = New-Object System.Drawing.RectangleF (0, 0, $s, $s)
    $g.DrawString($glyph, $font, [System.Drawing.Brushes]::White, $rect, $sf)
  }
  finally { $g.Dispose() }
  return $bmp
}

$entries = @()
foreach ($s in 16, 32, 48) {
  $bmp = New-IconBitmap $s
  $entries += ,@($s, (ConvertTo-IcoDib $bmp))
  $bmp.Dispose()
}
$bmp = New-IconBitmap 256
$entries += ,@(256, (ConvertTo-Png $bmp))
$bmp.Dispose()

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $out
$bw.Write([UInt16]0)                 # reserved
$bw.Write([UInt16]1)                 # type: icon
$bw.Write([UInt16]$entries.Count)
$offset = 6 + 16 * $entries.Count
foreach ($entry in $entries) {
  $s = $entry[0]
  $dim = 0; if ($s -lt 256) { $dim = $s }
  $bw.Write([Byte]$dim); $bw.Write([Byte]$dim)
  $bw.Write([Byte]0); $bw.Write([Byte]0)
  $bw.Write([UInt16]1); $bw.Write([UInt16]32)
  $bw.Write([UInt32]$entry[1].Length)
  $bw.Write([UInt32]$offset)
  $offset += $entry[1].Length
}
foreach ($entry in $entries) { $bw.Write([Byte[]]$entry[1]) }
$bw.Flush()

[System.IO.File]::WriteAllBytes($OutPath, $out.ToArray())
$bw.Dispose(); $out.Dispose()
Write-Host ("ICON_WRITTEN {0} bytes={1}" -f $OutPath, $out.Length)
