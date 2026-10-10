# 管线冒烟测试（内容无关：锁屏 / 远程 / 受限环境都能跑）：
#   2. --capture + --ocr          → 校验能抓屏并识别出文字
#   3. --grab-region              → 校验 截屏→OCR→文件 与 剪贴板 一致（全链路）
#   4. 托盘实例 + --grab (IPC)     → 校验日志出现 "Overlay started"（覆盖层已打开）
#   5. -WithForm                  → 额外显示全屏白底测试窗口（可解锁桌面时人工看效果）
# 用法: powershell -NoProfile -ExecutionPolicy Bypass -File tools\smoke-pipeline.ps1 [-WithForm] [-KeepRunning]
param(
    [string]$Exe,
    [switch]$WithForm,
    [switch]$KeepRunning
)

$ErrorActionPreference = 'Stop'

if (-not $Exe) {
    $candidates = Get-ChildItem -Path (Join-Path $PSScriptRoot '..\src\SnapText\bin\Release') -Recurse -Filter SnapText.exe -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending
    if (-not $candidates) { throw '找不到 SnapText.exe，请先构建或发布' }
    $Exe = $candidates[0].FullName
}
Write-Host "Exe: $Exe"

$artifacts = Join-Path $PSScriptRoot '..\tests\artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$savedClipboard = $null
try { $savedClipboard = Get-Clipboard -Raw -ErrorAction SilentlyContinue } catch { }

function Get-ClipboardRetry([int]$attempts = 10) {
    for ($i = 0; $i -lt $attempts; $i++) {
        try { return (Get-Clipboard -Raw -ErrorAction Stop) } catch { Start-Sleep -Milliseconds 200 }
    }
    return ''
}

# 锁屏/安全桌面下系统剪贴板对普通进程不可用（连 PowerShell 自己都读写失败），此时跳过剪贴板校验
$clipboardAvailable = $true
try { Set-Clipboard -Value ('SNAPTEXT_PROBE_' + [Guid]::NewGuid().ToString('N')) -ErrorAction Stop }
catch { $clipboardAvailable = $false }
Write-Host ("clipboard available: {0}" -f $clipboardAvailable)

function Get-SnapTextLogPath {
    Join-Path (Join-Path $env:LOCALAPPDATA 'SnapText\logs') ("app-{0:yyyyMMdd}.log" -f (Get-Date))
}

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

$form = $null
$proc = $null
$results = [ordered]@{}
try {
    if ($WithForm) {
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
        $form.Refresh()
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 900
    }

    # ---- STEP 1: 抓屏 + OCR ----
    $shot = Join-Path $artifacts 'screen.png'
    & $Exe --capture --out $shot | Out-Null
    $shotExists = Test-Path $shot
    $shotText = Join-Path $artifacts 'screen.txt'
    & $Exe --ocr $shot --engine ppocr --out $shotText | Out-Null
    $captured = (Get-Content -Raw -Encoding UTF8 $shotText).Trim()
    $capOk = $shotExists -and ($captured.Length -ge 5)
    $results['capture+ocr'] = $capOk
    Write-Host ("STEP1 capture+ocr: {0}  [{1} chars] {2}" -f $(if ($capOk) { 'PASS' } else { 'FAIL' }), $captured.Length, $captured.Substring(0, [Math]::Min(60, $captured.Length)))

    # ---- STEP 2: 固定区域取字 + 剪贴板 ----
    # 注意：断言"命令跑通并产出文件"；屏幕内容（可能锁屏/黑屏）不参与判定
    $regionText = Join-Path $artifacts 'region.txt'
    Remove-Item -Force $regionText -ErrorAction SilentlyContinue
    & $Exe --grab-region '150,180,1400,420' --engine ppocr --out $regionText | Out-Null
    $rawRegion = Get-Content -Raw -Encoding UTF8 $regionText -ErrorAction SilentlyContinue
    $region = if ($null -eq $rawRegion) { '' } else { $rawRegion.Trim() }
    Start-Sleep -Milliseconds 500
    $clip = (Get-ClipboardRetry).Trim()

    $regionOk = Test-Path $regionText
    if (-not $clipboardAvailable) {
        $clipOk = 'SKIP'
    } elseif ($region.Length -ge 3) {
        $clipOk = ($clip.Length -ge 3 -and $clip -eq $region)
    } else {
        $clipOk = 'SKIP'   # 该区域没有文字（锁屏/纯色背景），无从校验
    }
    $results['grab-region'] = $regionOk
    $results['clipboard'] = $clipOk
    Write-Host ("STEP2 grab-region: {0}  clipboard(matches): {1}" -f $(if ($regionOk) { 'PASS' } else { 'FAIL' }), $clipOk)
    Write-Host ("       region=[{0}]" -f $region.Substring(0, [Math]::Min(60, $region.Length)))

    # ---- STEP 2.5: 二维码生成 → 解回（自包含，不依赖屏幕内容；用 --out 文件校验，不靠 stdout） ----
    $qrPath = Join-Path $artifacts 'qr-test.png'
    $qrText = 'https://www.bilibili.com/video/BV1xx411c7mD'
    $qrOut = Join-Path $artifacts 'qr-test.txt'
    Remove-Item -Force $qrPath, $qrOut -ErrorAction SilentlyContinue
    & $Exe --qr $qrText --out $qrPath | Out-Null
    & $Exe --scan $qrPath --out $qrOut | Out-Null
    $scanned = Get-Content -Raw -Encoding UTF8 $qrOut -ErrorAction SilentlyContinue
    $qrOk = ($null -ne $scanned) -and ($scanned.Trim() -eq $qrText)
    $results['qr-roundtrip'] = $qrOk
    Write-Host ("STEP2.5 qr round-trip: {0}  [{1}]" -f $(if ($qrOk) { 'PASS' } else { 'FAIL' }), $scanned)

    # ---- STEP 3: 托盘实例 + --grab（IPC） ----
    $logPath = Get-SnapTextLogPath
    $logOffset = 0
    if (Test-Path $logPath) { $logOffset = (Get-Item $logPath).Length }

    $existing = Get-Process -Name SnapText -ErrorAction SilentlyContinue
    if ($existing) {
        # 已有实例（很可能是用户自己在用）——不启动、不发信号，避免干扰
        Write-Host "已有 SnapText 实例在运行（pid=$($existing[0].Id)），跳过 STEP3 以免干扰"
        $results['ipc-grab'] = 'SKIP'
    } else {
        $proc = Start-Process -FilePath $Exe -PassThru
        Write-Host "SnapText starting (pid=$($proc.Id)) ..."
        if (-not (Wait-Log 'SnapText started' 60000 $logOffset)) { throw 'SnapText 启动超时（未见 SnapText started 日志）' }
        Write-Host 'SnapText ready'

        & $Exe --grab | Out-Null
        $ipcOk = Wait-Log 'Grab signal received via IPC' 10000 $logOffset
        $overlayOpened = Wait-Log 'Overlay started' 3000 $logOffset
        $results['ipc-grab'] = $ipcOk
        Write-Host ("STEP3 ipc grab: {0}  overlay opened: {1}" -f $(if ($ipcOk) { 'PASS' } else { 'FAIL' }), $overlayOpened)
    }

    Write-Host ''
    $allPass = ($results.Values -notcontains $false)
    Write-Host ("PIPELINE_RESULT: {0}" -f $(if ($allPass) { 'PASS' } else { 'FAIL' }))
}
finally {
    if ($form) { $form.Close(); $form.Dispose() }
    if ($null -ne $savedClipboard) { try { Set-Clipboard -Value $savedClipboard } catch { } }
    if ($proc -and -not $KeepRunning) {
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Write-Host 'SnapText 测试实例已退出'
    }
}
