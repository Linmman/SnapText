# 生成一张带中文/英文/网址的测试图片，用于 OCR 回归测试。
# 用法: powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-test-image.ps1
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot '..\tests\artifacts\sample.png'
New-Item -ItemType Directory -Force -Path (Split-Path $out) | Out-Null

$bitmap = New-Object System.Drawing.Bitmap 1400, 480
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.Clear([System.Drawing.Color]::White)
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

$font = New-Object System.Drawing.Font 'Microsoft YaHei UI', 34
$brush = [System.Drawing.Brushes]::Black

$graphics.DrawString('SnapText 屏幕取字测试', $font, $brush, 40, 40)
$graphics.DrawString('B站视频里的文字 一键复制到剪贴板', $font, $brush, 40, 130)
$graphics.DrawString('https://www.bilibili.com/video/BV1xx411c7mD', $font, $brush, 40, 220)
$graphics.DrawString('Hello world, this is OCR test 12345', $font, $brush, 40, 310)

$graphics.Dispose()
$bitmap.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()

Write-Host "Saved: $out"
