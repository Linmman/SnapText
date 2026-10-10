# 生成连拍选优测试用的三种帧：清晰 / 模糊 / 噪点（内容相同，质量不同）
# 用法: powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-burst-test-images.ps1
Add-Type -AssemblyName System.Drawing

$dir = Join-Path $PSScriptRoot '..\tests\artifacts'
New-Item -ItemType Directory -Force -Path $dir | Out-Null

$text = 'https://www.bilibili.com/video/BV1xx411c7mD burst frame'

function New-CleanFrame {
    $bmp = New-Object System.Drawing.Bitmap 1200, 130
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::White)
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $font = New-Object System.Drawing.Font 'Consolas', 26
    $g.DrawString($text, $font, [System.Drawing.Brushes]::Black, 20, 40)
    $font.Dispose(); $g.Dispose()
    return $bmp
}

# 1) 清晰帧
$clean = New-CleanFrame
$clean.Save((Join-Path $dir 'burst-clean.png'), [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host 'saved burst-clean.png'

# 2) 模糊帧：缩小到 25% 再放大回来
$small = New-Object System.Drawing.Bitmap 300, 33
$gs = [System.Drawing.Graphics]::FromImage($small)
$gs.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gs.DrawImage($clean, 0, 0, 300, 33)
$gs.Dispose()

$blurry = New-Object System.Drawing.Bitmap 1200, 130
$gb = [System.Drawing.Graphics]::FromImage($blurry)
$gb.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gb.DrawImage($small, 0, 0, 1200, 130)
$gb.Dispose(); $small.Dispose()
$blurry.Save((Join-Path $dir 'burst-blurry.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$blurry.Dispose()
Write-Host 'saved burst-blurry.png'

# 3) 噪点帧：在清晰帧上随机撒噪点
$noisy = New-Object System.Drawing.Bitmap $clean
$rand = New-Object System.Random 20261010
for ($i = 0; $i -lt 24000; $i++) {
    $x = $rand.Next(0, 1200)
    $y = $rand.Next(0, 130)
    $v = $rand.Next(0, 130)
    $noisy.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $v, $v, $v))
}
$noisy.Save((Join-Path $dir 'burst-noisy.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$noisy.Dispose()
$clean.Dispose()
Write-Host 'saved burst-noisy.png'
