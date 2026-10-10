# 自适应取字回归（需解锁桌面：测试窗口必须真实显示在屏幕上；不注入键鼠、不写剪贴板）：
#   1. 静止画面 → 只识别一帧（frames=2 / ocr=1，即"单帧速度"快路径），且文字识别正确
#   2. 画面在动   → 多帧采样选优（frames=6 / ocr>=2），结果必须是画面上的完整句子之一
# 原理：脚本在屏幕固定位置显示一个小窗口，用 CLI `--ocr-adaptive` 对同一区域取样；
#       运动阶段由一个独立进程负责动画（有自己的消息泵），保证 CLI 阻塞期间画面仍在变。
# 用法: powershell -NoProfile -ExecutionPolicy Bypass -File tools\smoke-adaptive.ps1 [-Exe <路径>]
param([string]$Exe)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

if (-not $env:DOTNET_ROOT) {
    $userDotnet = Join-Path $env:USERPROFILE '.dotnet'
    if (Test-Path $userDotnet) { $env:DOTNET_ROOT = $userDotnet }
}

if (-not $Exe) {
    $candidates = Get-ChildItem -Path (Join-Path $PSScriptRoot '..\src\SnapText\bin\Release') -Recurse -Filter SnapText.exe -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending
    if (-not $candidates) { throw '找不到 SnapText.exe，请先构建或发布' }
    $Exe = $candidates[0].FullName
}
Write-Host "Exe: $Exe"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System.Runtime.InteropServices;
public static class SnapTextAdaptiveTest {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[SnapTextAdaptiveTest]::SetProcessDPIAware() | Out-Null

# 测试窗口位置（物理像素；与下方动画脚本里的坐标保持一致）
$formX = 60
$formY = 1400
$formW = 1800
$formH = 260
$rect = "$formX,$formY,$formW,$formH"

$outDir = Join-Path $env:TEMP ("snaptext-adaptive-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$results = [ordered]@{}
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    $results[$name] = $ok
    Write-Host ("- {0}: {1}  {2}" -f $name, $(if ($ok) { 'PASS' } else { 'FAIL' }), $detail)
}

function Get-StatLine([string]$text) {
    $line = ($text -split "`r?`n" | Where-Object { $_ -match '^frames=' } | Select-Object -First 1)
    if ($line) { return $line.Trim() }
    return '(no stats line)'
}

function Read-Out([string]$path) {
    if (-not (Test-Path $path)) { return '' }
    return (Get-Content -Raw -Encoding UTF8 $path).Trim()
}

$form = $null
$animProc = $null
try {
    # ---- 阶段 1：静止画面 → 单帧快路径 ----
    $script:staticText = 'adaptive static check 111'
    $script:staticFont = New-Object System.Drawing.Font('Microsoft YaHei UI', 28)
    $form = New-Object System.Windows.Forms.Form
    $form.FormBorderStyle = 'None'
    $form.StartPosition = 'Manual'
    $form.Location = New-Object System.Drawing.Point($formX, $formY)
    $form.Size = New-Object System.Drawing.Size($formW, $formH)
    $form.TopMost = $true
    $form.BackColor = [System.Drawing.Color]::White
    $form.Add_Paint({
        param($sender, $e)
        $e.Graphics.DrawString($script:staticText, $script:staticFont, [System.Drawing.Brushes]::Black, 30, 60)
    })
    $form.Show()
    $until = (Get-Date).AddMilliseconds(1200)
    while ((Get-Date) -lt $until) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 30
    }

    $staticOut = Join-Path $outDir 'static.txt'
    $staticConsole = (& $Exe --ocr-adaptive $rect --no-copy --out $staticOut 2>&1 | Out-String)
    $staticText = Read-Out $staticOut
    $staticOk = ($staticConsole -match 'frames=2\b') -and ($staticConsole -match 'ocr=1\b')
    Check 'static-single-frame' $staticOk (Get-StatLine $staticConsole)
    Check 'static-text' ($staticText -like '*adaptive static*') ("out=[" + $staticText.Substring(0, [Math]::Min(60, $staticText.Length)) + "]")

    $form.Close()
    $form.Dispose()
    $form = $null

    # ---- 阶段 2：画面在动 → 多帧选优 ----
    $animScript = Join-Path $outDir 'animate.ps1'
    @'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -Namespace SA -Name Dpi -MemberDefinition '[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();'
[SA.Dpi]::SetProcessDPIAware() | Out-Null
$form = New-Object System.Windows.Forms.Form
$form.FormBorderStyle = 'None'
$form.StartPosition = 'Manual'
$form.Location = New-Object System.Drawing.Point(60, 1400)
$form.Size = New-Object System.Drawing.Size(1800, 260)
$form.TopMost = $true
$form.BackColor = [System.Drawing.Color]::White
$script:on = $false
$script:font = New-Object System.Drawing.Font('Microsoft YaHei UI', 28)
$form.Add_Paint({
    param($sender, $e)
    $e.Graphics.DrawString('adaptive motion AAA', $script:font, [System.Drawing.Brushes]::Black, 30, 60)
    $brush = if ($script:on) { [System.Drawing.Brushes]::Black } else { [System.Drawing.Brushes]::White }
    $e.Graphics.FillRectangle($brush, 0, 180, 1800, 80)
})
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 80
$timer.Add_Tick({
    $script:on = -not $script:on
    $form.Invalidate()
    $form.Update()
})
$timer.Start()
[System.Windows.Forms.Application]::Run($form)
'@ | Set-Content -Path $animScript -Encoding UTF8

    $animProc = Start-Process -FilePath 'powershell' -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $animScript -PassThru
    Start-Sleep -Milliseconds 1300

    $motionOut = Join-Path $outDir 'motion.txt'
    $motionConsole = (& $Exe --ocr-adaptive $rect --no-copy --out $motionOut 2>&1 | Out-String)
    $motionText = Read-Out $motionOut

    $ocrCount = 0
    $m = [regex]::Match($motionConsole, 'ocr=(\d+)')
    if ($m.Success) { $ocrCount = [int]$m.Groups[1].Value }
    $motionOk = ($motionConsole -match 'frames=6\b') -and ($ocrCount -ge 2)
    Check 'motion-multi-frame' $motionOk (Get-StatLine $motionConsole)
    Check 'motion-text' ($motionText -like '*adaptive motion*') ("out=[" + $motionText.Substring(0, [Math]::Min(60, $motionText.Length)) + "]")

    Write-Host ''
    $allPass = ($results.Values -notcontains $false)
    Write-Host ("ADAPTIVE_RESULT: {0}" -f $(if ($allPass) { 'PASS' } else { 'FAIL' }))
}
finally {
    if ($animProc) { Stop-Process -Id $animProc.Id -Force -ErrorAction SilentlyContinue }
    if ($form) { $form.Close(); $form.Dispose() }
    Start-Sleep -Milliseconds 500
    Remove-Item -Recurse -Force $outDir -ErrorAction SilentlyContinue
}
