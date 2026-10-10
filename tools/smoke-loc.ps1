# 界面语言（本地化）回归（内容无关：锁屏也能跑；不改配置，只读设置）：
#   1. --loc check              → 三语言文案表键集合一致（LOC_MISSING: 0 / LOC_RESULT: PASS）
#   2. SNAPTEXT_LANG=zh|ja|en   → CLI 输出分别使用对应语言（用法行前缀 用法/使い方/Usage）
#   3. 三种语言强制启动一次应用（日志校验：无 "Missing UI string"、语言已生效、托盘创建成功）
# 用法: powershell -NoProfile -ExecutionPolicy Bypass -File tools\smoke-loc.ps1 [-Exe <路径>]
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

$results = [ordered]@{}
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    $results[$name] = $ok
    Write-Host ("- {0}: {1}  {2}" -f $name, $(if ($ok) { 'PASS' } else { 'FAIL' }), $detail)
}

function Get-SnapTextLogPath {
    Join-Path (Join-Path $env:LOCALAPPDATA 'SnapText\logs') ("app-{0:yyyyMMdd}.log" -f (Get-Date))
}

# 启动一个用了指定语言的实例，等日志出现标记，返回该实例读到的日志文本
function Start-WithLanguage([string]$lang, [int]$timeoutSeconds = 40) {
    $logPath = Get-SnapTextLogPath
    $offset = 0
    if (Test-Path $logPath) { $offset = (Get-Item $logPath).Length }

    $env:SNAPTEXT_LANG = $lang
    $env:SNAPTEXT_HISTORY_FILE = Join-Path $env:TEMP ("snaptext-loc-{0}.jsonl" -f $lang)
    $proc = Start-Process -FilePath $Exe -PassThru
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    $text = ''
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 400
        try {
            $fs = [System.IO.File]::Open($logPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
            try {
                if ($fs.Length -gt $offset) {
                    $fs.Seek($offset, [System.IO.SeekOrigin]::Begin) | Out-Null
                    $sr = New-Object System.IO.StreamReader($fs, [System.Text.UTF8Encoding]::new($false))
                    $text = $sr.ReadToEnd()
                    $sr.Dispose()
                }
            } finally { $fs.Dispose() }
        } catch { }
        if ($text -match 'Tray icon created') { break }
    }

    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 700
    return $text
}

try {
    # 1) 三语言键集合一致
    $env:SNAPTEXT_LANG = $null
    $locOut = (& $Exe --loc check | Out-String)
    $checkOk = ($locOut -match 'LOC_MISSING: 0') -and ($locOut -match 'LOC_RESULT: PASS')
    Check 'loc-check' $checkOk (($locOut -split "`n" | Where-Object { $_ -match 'LOC_TOTAL|LOC_MISSING|LOC_RESULT' }) -join ' | ')

    # 2) CLI 输出随 SNAPTEXT_LANG 切换
    $prefix = @{ zh = '用法:'; ja = '使い方:'; en = 'Usage:' }
    $outputs = @{}
    foreach ($lang in 'zh', 'ja', 'en') {
        $env:SNAPTEXT_LANG = $lang
        $outputs[$lang] = ((& $Exe --qr 2>&1 | Out-String)).Trim()
        Check ("cli-output-" + $lang) ($outputs[$lang].StartsWith($prefix[$lang])) $outputs[$lang].Substring(0, [Math]::Min(50, $outputs[$lang].Length))
    }
    $distinct = (@($outputs['zh'], $outputs['ja'], $outputs['en']) | Sort-Object -Unique).Count
    Check 'cli-output-distinct' ($distinct -eq 3) ("unique=" + $distinct)

    # 3) 三种语言启动应用：无缺失文案、语言日志正确、托盘创建成功
    foreach ($lang in 'zh', 'ja', 'en') {
        $text = Start-WithLanguage $lang
        $missing = ([regex]::Matches($text, 'Missing UI string')).Count
        $langOk = $text -match ("UI language: " + $lang + " ")
        $trayOk = $text -match 'Tray icon created'
        Check ("startup-" + $lang) ($missing -eq 0 -and $langOk -and $trayOk) ("missing=$missing lang=$langOk tray=$trayOk")
    }

    Write-Host ''
    $allPass = ($results.Values -notcontains $false)
    Write-Host ("LOC_RESULT_ALL: {0}" -f $(if ($allPass) { 'PASS' } else { 'FAIL' }))
}
finally {
    Remove-Item Env:SNAPTEXT_LANG -ErrorAction SilentlyContinue
    Get-ChildItem $env:TEMP -Filter 'snaptext-loc-*.jsonl' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}
