# 生成 URL 修复逻辑的边界用例图（配合 --ocr 使用）
# 每个用例对应一条"整段文本"，用于验证「什么时候该去空格、什么时候必须原样保留」
Add-Type -AssemblyName System.Drawing

$dir = Join-Path $PSScriptRoot '..\tests\artifacts'
New-Item -ItemType Directory -Force -Path $dir | Out-Null

function New-TextImage([string]$name, [string]$text, [int]$fontSize = 28) {
    $bmp = New-Object System.Drawing.Bitmap 1500, 120
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::White)
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $font = New-Object System.Drawing.Font 'Consolas', $fontSize
    $g.DrawString($text, $font, [System.Drawing.Brushes]::Black, 20, 35)
    $font.Dispose(); $g.Dispose()
    $bmp.Save((Join-Path $dir $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "saved $name  <= [$text]"
}

# 1) 链接被空格断成两截 → 应合并
New-TextImage 'url-split.png' 'https://www.bilibili.com/video/BV1xx411 c7mD'
# 2) 链接后面还有一句英文说明（3 段）→ 必须原样保留空格
New-TextImage 'url-with-words.png' 'https://www.bilibili.com/video/BV1xx411c7mD burst frame'
# 3) 链接 + 一个普通单词（2 段，但第二段不含数字/点）→ 必须原样保留
New-TextImage 'url-plus-word.png' 'https://example.com click'
# 4) 只有 www 前缀 → 应补上 https://
New-TextImage 'url-www.png' 'www.bilibili.com'
# 5) 中文 + 链接 → 不动原文
New-TextImage 'url-cjk.png' '看这个 https://b23.tv/abc'
