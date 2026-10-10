# 端到端冒烟测试：
#   1. 显示一个全屏白底窗口（上面画着已知文字）
#   2. 等待 SnapText 启动完成（轮询日志，避免竞态）后发送全局热键 Ctrl+Alt+Q
#   3. 等覆盖层出现 → 模拟鼠标拖拽框选文字区域
#   4. 等 OCR 完成 → 读取剪贴板校验识别结果
#   5. 还原剪贴板/光标位置，清理窗口
# 用法: powershell -NoProfile -ExecutionPolicy Bypass -File tools\smoke-e2e.ps1
param(
    [string]$Exe,
    [switch]$KeepRunning
)

$ErrorActionPreference = 'Stop'

if (-not $Exe) {
    $candidates = Get-ChildItem -Path (Join-Path $PSScriptRoot '..\src\SnapText\bin') -Recurse -Filter SnapText.exe -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending
    if (-not $candidates) { throw "找不到 SnapText.exe，请先构建或发布" }
    $Exe = $candidates[0].FullName
}
Write-Host "Exe: $Exe"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class SnapTextSmoke {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, IntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
    public const uint LEFTDOWN = 0x0002;
    public const uint LEFTUP = 0x0004;
}
"@

[SnapTextSmoke]::SetProcessDPIAware() | Out-Null

function Get-SnapTextLogPath {
    Join-Path (Join-Path $env:LOCALAPPDATA 'SnapText\logs') ("app-{0:yyyyMMdd}.log" -f (Get-Date))
}

function Get-LogLength([string]$path) {
    if (Test-Path $path) { return (Get-Item $path).Length }
    return 0
}

# 轮询日志（从指定偏移开始），等待出现某个标记
function Wait-Log([string]$pattern, [int]$timeoutMs, [long]$fromOffset) {
    $path = Get-SnapTextLogPath
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $path) {
            try {
                $fs = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
                try {
                    if ($fromOffset -lt $fs.Length) {
                        $fs.Seek($fromOffset, [System.IO.SeekOrigin]::Begin) | Out-Null
                        $reader = New-Object System.IO.StreamReader($fs, [System.Text.UTF8Encoding]::new($false))
                        $text = $reader.ReadToEnd()
                        $reader.Dispose()
                        if ($text -match $pattern) { return $true }
                    }
                } finally { $fs.Dispose() }
            } catch { }
        }
        Start-Sleep -Milliseconds 250
    }
    return $false
}

function Read-ClipboardWithRetry([int]$attempts = 8) {
    for ($i = 0; $i -lt $attempts; $i++) {
        try { return (Get-Clipboard -Raw -ErrorAction Stop) } catch { Start-Sleep -Milliseconds 200 }
    }
    return ''
}

# 已有实例在运行（很可能是用户自己在用）：接管它会污染用户的剪贴板/历史/屏幕，直接跳过
# （单实例互斥体下也无法再起一个测试实例，所以这里必须提前退出）
$existingInstance = Get-Process -Name SnapText -ErrorAction SilentlyContinue
if ($existingInstance) {
    Write-Host ("SKIP: 已有 SnapText 实例在运行（pid={0}）——本测试需要独占实例，请先退出托盘里的 SnapText 再跑" -f $existingInstance[0].Id)
    Write-Host 'E2E_RESULT: SKIP'
    exit 0
}

$logPath = Get-SnapTextLogPath
$logOffset = Get-LogLength $logPath

# 保存现场
$savedClipboard = $null
try { $savedClipboard = Get-Clipboard -Raw -ErrorAction SilentlyContinue } catch { }
$cursor = New-Object SnapTextSmoke+POINT
[SnapTextSmoke]::GetCursorPos([ref]$cursor) | Out-Null

$proc = $null
$form = $null
try {
    # 1. 全屏测试窗口（文字画在固定位置）
    $primary = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    Write-Host "Primary screen: $($primary.Width)x$($primary.Height) @ ($($primary.Left),$($primary.Top))"

    $form = New-Object System.Windows.Forms.Form
    $form.FormBorderStyle = 'None'
    $form.WindowState = 'Maximized'
    $form.TopMost = $true
    $form.BackColor = [System.Drawing.Color]::White
    $script:testFont = New-Object System.Drawing.Font('Microsoft YaHei UI', 40)
    $script:testBrush = [System.Drawing.Brushes]::Black
    $form.Add_Paint({
        param($sender, $e)
        $e.Graphics.DrawString('SnapText 屏幕取字端到端测试', $script:testFont, $script:testBrush, 200, 220)
        $e.Graphics.DrawString('B站视频里的文字 一键复制到剪贴板', $script:testFont, $script:testBrush, 200, 340)
        $e.Graphics.DrawString('https://www.bilibili.com/video/BV1xx411c7mD', $script:testFont, $script:testBrush, 200, 460)
    })
    $form.Show()
    # 持续泵消息 ~1.2 秒，确保窗口"完整绘制"后再截屏（否则冻结帧可能抓到只画了一半的画面）
    $pumpUntil = (Get-Date).AddMilliseconds(1200)
    while ((Get-Date) -lt $pumpUntil) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 30
    }

    # 2. 启动 SnapText（前面已确认没有实例在运行），并等"启动完成"日志
    $proc = Start-Process -FilePath $Exe -PassThru
    Write-Host "SnapText starting (pid=$($proc.Id)) ..."
    if (-not (Wait-Log 'Hotkey registered' 40000 $logOffset)) {
        throw 'SnapText 启动超时：未看到 Hotkey registered 日志'
    }
    Write-Host 'SnapText ready (hotkey registered)'

    # 3. 全局热键 Ctrl+Alt+Q → 等覆盖层
    [System.Windows.Forms.SendKeys]::SendWait('^%q')
    if (-not (Wait-Log 'Overlay started' 8000 $logOffset)) {
        Write-Host 'WARN: 未在日志中看到 Overlay started'
    } else {
        Write-Host 'Overlay opened'
    }
    Start-Sleep -Milliseconds 400

    # 4. 模拟拖拽框选（物理像素）
    $startX = $primary.Left + 120
    $startY = $primary.Top + 150
    $endX = [Math]::Min($primary.Right - 60, $primary.Left + 1500)
    $endY = [Math]::Min($primary.Bottom - 60, $primary.Top + 560)

    [SnapTextSmoke]::SetCursorPos($startX, $startY) | Out-Null
    Start-Sleep -Milliseconds 200
    [SnapTextSmoke]::mouse_event([SnapTextSmoke]::LEFTDOWN, 0, 0, 0, [IntPtr]::Zero)
    foreach ($step in 0..4) {
        $x = [int]($startX + ($endX - $startX) * $step / 4)
        $y = [int]($startY + ($endY - $startY) * $step / 4)
        [SnapTextSmoke]::SetCursorPos($x, $y) | Out-Null
        Start-Sleep -Milliseconds 70
    }
    [SnapTextSmoke]::mouse_event([SnapTextSmoke]::LEFTUP, 0, 0, 0, [IntPtr]::Zero)
    Write-Host "Drag: ($startX,$startY) -> ($endX,$endY)"

    # 5. 等 OCR 完成 → 读剪贴板
    if (-not (Wait-Log 'OCR done in' 20000 $logOffset)) {
        Write-Host 'WARN: 未在日志中看到 OCR done'
    }
    Start-Sleep -Milliseconds 500

    $clipboard = Read-ClipboardWithRetry
    Write-Host '---- 剪贴板内容 ----'
    Write-Host $clipboard
    Write-Host '--------------------'

    $expected = @('SnapText', 'B站', 'bilibili')
    $hit = 0
    foreach ($word in $expected) {
        if ($clipboard -like "*$word*") { $hit++; Write-Host "命中: $word" }
    }

    if ($hit -ge 2) {
        Write-Host 'E2E_RESULT: PASS'
    } else {
        Write-Host "E2E_RESULT: FAIL (命中 $hit/3)"
    }
}
finally {
    if ($form) { $form.Close(); $form.Dispose() }
    [SnapTextSmoke]::SetCursorPos($cursor.X, $cursor.Y) | Out-Null
    if ($null -ne $savedClipboard) {
        try { Set-Clipboard -Value $savedClipboard } catch { }
    }
    if ($proc -and -not $KeepRunning) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Write-Host 'SnapText 测试实例已退出'
    }
}
