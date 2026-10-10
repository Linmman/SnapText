# 取字历史回归（内容无关：锁屏也能跑；可与正在运行的 SnapText 实例并存）：
#   通过环境变量把历史指向临时文件（SNAPTEXT_HISTORY_FILE / SNAPTEXT_HISTORY_MAX），不触碰真实历史
#   校验：清空 / 写入与倒序（最新在前）/ 连续重复跳过 / 搜索命中与未命中 / --out 文件 UTF-8 /
#        条数上限裁剪 / JSONL 格式与字段 / 清空删文件 / 用法错误退出码 / 默认存储路径
# 用法: powershell -NoProfile -ExecutionPolicy Bypass -File tools\smoke-history.ps1 [-Exe <路径>]
param([string]$Exe)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

# bin\Release 里是框架依赖版 exe，需要 .NET 10 运行时；本机 SDK 装在用户目录
if (-not $env:DOTNET_ROOT) {
    $userDotnet = Join-Path $env:USERPROFILE '.dotnet'
    if (Test-Path $userDotnet) {
        $env:DOTNET_ROOT = $userDotnet
    } else {
        throw '找不到 .NET 10 运行时：请设置 DOTNET_ROOT，或先运行 tools\install-dotnet.ps1'
    }
}

if (-not $Exe) {
    $candidates = Get-ChildItem -Path (Join-Path $PSScriptRoot '..\src\SnapText\bin\Release') -Recurse -Filter SnapText.exe -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending
    if (-not $candidates) { throw '找不到 SnapText.exe，请先构建或发布' }
    $Exe = $candidates[0].FullName
}
Write-Host "Exe: $Exe"

$tmpDir = Join-Path $env:TEMP ("snaptext-history-test-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null
$historyFile = Join-Path $tmpDir 'history.jsonl'
$env:SNAPTEXT_HISTORY_FILE = $historyFile
$env:SNAPTEXT_HISTORY_MAX = '5'

$results = [ordered]@{}
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    $results[$name] = $ok
    Write-Host ("- {0}: {1}  {2}" -f $name, $(if ($ok) { 'PASS' } else { 'FAIL' }), $detail)
}

function Invoke-SnapText([string[]]$Arguments) {
    $text = (& $Exe @Arguments 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) {
        throw ("SnapText 退出码 {0}: {1} -> {2}" -f $LASTEXITCODE, ($Arguments -join ' '), $text.Trim())
    }
    return $text
}

# 用 --out 文件断言（不依赖控制台编码）；用 ,@() 返回，避免单元素数组被 PowerShell 解包成字符串
function Get-HistoryLines([string[]]$Arguments) {
    $outFile = Join-Path $tmpDir ("out-" + [Guid]::NewGuid().ToString('N').Substring(0, 8) + '.txt')
    Invoke-SnapText ($Arguments + @('--out', $outFile)) | Out-Null
    if (-not (Test-Path $outFile)) { return ,@() }
    return ,@(Get-Content -Encoding UTF8 $outFile | Where-Object { $_.Trim().Length -gt 0 })
}

try {
    # 1) 清空后为空
    Invoke-SnapText @('--history', 'clear') | Out-Null
    $lines = Get-HistoryLines @('--history', 'list')
    Check 'clear->empty' ($lines.Count -eq 0) ("count=" + $lines.Count)

    # 2) 写入 + 倒序（最新在前）；含中文，验证 UTF-8 往返
    Invoke-SnapText @('--history', 'add', 'alpha 111') | Out-Null
    Invoke-SnapText @('--history', 'add', '第二轮 中文 Bravo 222') | Out-Null
    $lines = Get-HistoryLines @('--history', 'list')
    $orderOk = $lines.Count -eq 2 -and $lines[0] -like '*Bravo 222*' -and $lines[1] -like '*alpha 111*'
    Check 'add+newest-first' $orderOk ("lines=" + $lines.Count)
    Check 'cjk-exact' ($lines.Count -ge 1 -and $lines[0].EndsWith('第二轮 中文 Bravo 222')) $lines[0]

    # 3) 连续重复跳过（连拍/重复取字不会灌满历史）
    $dup = (Invoke-SnapText @('--history', 'add', '第二轮 中文 Bravo 222')).Trim()
    $lines = Get-HistoryLines @('--history', 'list')
    Check 'duplicate-skip' (($dup -like 'SKIP*') -and $lines.Count -eq 2) ("add=$dup count=" + $lines.Count)

    # 4) 搜索命中 / 未命中
    $hit = Get-HistoryLines @('--history', 'search', 'alpha')
    $miss = Get-HistoryLines @('--history', 'search', 'NOT-FOUND-XYZ')
    Check 'search-hit' ($hit.Count -eq 1 -and $hit[0] -like '*alpha 111*') ("hits=" + $hit.Count + " first=[" + ($hit -join '|') + "]")
    Check 'search-miss' ($miss.Count -eq 0) ("hits=" + $miss.Count)

    # 5) 条数上限裁剪（SNAPTEXT_HISTORY_MAX=5）：只保留最新的 5 条
    foreach ($i in 1..7) { Invoke-SnapText @('--history', 'add', "bulk-$i") | Out-Null }
    $lines = Get-HistoryLines @('--history', 'list')
    $kept = ($lines -join "`n")
    $capOk = $lines.Count -eq 5 -and ($kept -like '*bulk-7*') -and ($kept -like '*bulk-3*') -and
             ($kept -notlike '*bulk-2*') -and ($kept -notlike '*bulk-1*') -and ($kept -notlike '*Bravo 222*')
    Check 'cap-trim-5' $capOk ("count=" + $lines.Count)

    # 6) JSONL 格式：每行一个 JSON 对象，含 ts / text / source
    $jsonOk = $true
    $jsonDetail = ''
    try {
        $jsonLines = [System.IO.File]::ReadAllLines($historyFile, [System.Text.UTF8Encoding]::new($false))
    } catch {
        $jsonOk = $false
        $jsonDetail = $_.Exception.Message
        $jsonLines = @()
    }
    if ($jsonOk) {
        if ($jsonLines.Count -ne 5) {
            $jsonOk = $false
            $jsonDetail = "lines=$($jsonLines.Count)"
        } else {
            foreach ($line in $jsonLines) {
                try {
                    $obj = $line | ConvertFrom-Json
                    if (-not $obj.ts -or -not $obj.text -or -not $obj.source) {
                        $jsonOk = $false
                        $jsonDetail = "missing field: $line"
                        break
                    }
                } catch {
                    $jsonOk = $false
                    $jsonDetail = $_.Exception.Message
                    break
                }
            }
        }
    }
    Check 'jsonl-format' $jsonOk $jsonDetail

    # 7) 清空：文件删除、再取为空
    Invoke-SnapText @('--history', 'clear') | Out-Null
    Check 'clear-deletes-file' (-not (Test-Path $historyFile))
    $lines = Get-HistoryLines @('--history', 'list')
    Check 'empty-after-clear' ($lines.Count -eq 0) ("count=" + $lines.Count)

    # 8) 用法错误退出码 = 2
    & $Exe --history | Out-Null
    Check 'usage-exit-2' ($LASTEXITCODE -eq 2) ("exit=" + $LASTEXITCODE)

    # 9) 默认存储路径（不设环境变量时）= %LOCALAPPDATA%\SnapText\history.jsonl
    #    只校验打印的路径行，不回显用户真实历史内容
    $env:SNAPTEXT_HISTORY_FILE = $null
    $defaultOut = Invoke-SnapText @('--history', 'list')
    $expected = Join-Path $env:LOCALAPPDATA 'SnapText\history.jsonl'
    Check 'default-path' ($defaultOut -match [Regex]::Escape($expected))
    $env:SNAPTEXT_HISTORY_FILE = $historyFile

    Write-Host ''
    $allPass = ($results.Values -notcontains $false)
    Write-Host ("HISTORY_RESULT: {0}" -f $(if ($allPass) { 'PASS' } else { 'FAIL' }))
}
finally {
    Remove-Item -Recurse -Force $tmpDir -ErrorAction SilentlyContinue
}
