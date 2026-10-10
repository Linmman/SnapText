# 验证"热键留空 = 关闭该热键"：
#   1. 备份你的 settings.json
#   2. 临时写入 RepeatHotkey 为空的配置
#   3. 启动 SnapText，检查日志：应只有一条 Hotkey registered，且出现"treated as disabled"
#   4. 退出实例并还原你的配置
# 注意：运行前请先退出正在使用的 SnapText 实例（单实例机制会让测试启动失败）。
# 用法: powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-hotkey-disable.ps1
$ErrorActionPreference = 'Stop'

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Probe {
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
}
"@

if (Get-Process -Name SnapText -ErrorAction SilentlyContinue) {
    Write-Host '检测到 SnapText 正在运行：请先从托盘退出，再运行本测试。' -ForegroundColor Yellow
    exit 2
}

$root = Split-Path $PSScriptRoot -Parent
$exe = Get-ChildItem -Path (Join-Path $root 'src\SnapText\bin') -Recurse -Filter SnapText.exe -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $exe) { throw '找不到 SnapText.exe，请先构建' }
Write-Host "Exe: $($exe.FullName)"

$cfg = Join-Path $env:LOCALAPPDATA 'SnapText\settings.json'
$backup = Join-Path $env:TEMP 'snaptext-settings-backup.json'
$log = Join-Path $env:LOCALAPPDATA ('SnapText\logs\app-{0:yyyyMMdd}.log' -f (Get-Date))

$hadConfig = Test-Path $cfg
if ($hadConfig) { Copy-Item $cfg $backup -Force }

try {
    New-Item -ItemType Directory -Force -Path (Split-Path $cfg) | Out-Null
    @'
{ "GrabHotkey": "Ctrl+Alt+Q", "RepeatHotkey": "", "BurstHotkey": "", "LinkOpener": "", "Engine": "auto", "SilentCopy": true, "MergeLines": true, "AutoStart": false, "SaveCapture": false }
'@ | Set-Content -Encoding UTF8 -NoNewline $cfg

    $offset = 0
    if (Test-Path $log) { $offset = (Get-Item $log).Length }

    $proc = Start-Process -FilePath $exe.FullName -PassThru
    Start-Sleep -Seconds 6

    $newLines = ''
    if (Test-Path $log) {
        $fs = [System.IO.File]::Open($log, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        try {
            $fs.Seek($offset, [System.IO.SeekOrigin]::Begin) | Out-Null
            $reader = New-Object System.IO.StreamReader($fs, [System.Text.UTF8Encoding]::new($false))
            $newLines = $reader.ReadToEnd()
            $reader.Dispose()
        } finally { $fs.Dispose() }
    }

    Write-Host '---- 本次启动的新增日志 ----'
    Write-Host $newLines
    Write-Host '---------------------------'

    $registered = ([regex]::Matches($newLines, 'Hotkey registered')).Count
    $disabled = $newLines -match 'treated as disabled'

    Write-Host ("注册的热键数（应为 1）: {0}" -f $registered)
    Write-Host ("出现 disabled 日志（应为 True）: {0}" -f $disabled)
    if ($registered -eq 1 -and $disabled) {
        Write-Host 'HOTKEY_DISABLE_RESULT: PASS'
    } else {
        Write-Host 'HOTKEY_DISABLE_RESULT: FAIL'
    }
}
finally {
    Stop-Process -Name SnapText -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    if ($hadConfig) { Copy-Item $backup $cfg -Force; Write-Host '你的配置已还原' }
}
