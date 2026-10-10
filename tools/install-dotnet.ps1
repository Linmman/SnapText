# 安装 .NET 10 SDK 到用户目录（不需要管理员权限）。
# 用法: powershell -NoProfile -ExecutionPolicy Bypass -File tools\install-dotnet.ps1
$ErrorActionPreference = 'Stop'

$dest = Join-Path $env:USERPROFILE '.dotnet'
$script = Join-Path $env:TEMP 'dotnet-install.ps1'

if (-not (Test-Path $script)) {
    Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1' -OutFile $script
}

& $script -Channel '10.0' -InstallDir $dest

$dotnet = Join-Path $dest 'dotnet.exe'
& $dotnet --list-sdks
Write-Host "SDK 安装完成: $dotnet"
Write-Host "如需在命令行直接使用 dotnet，可执行:"
Write-Host "  setx PATH `"$dest;%PATH%`""
