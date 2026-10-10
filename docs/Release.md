# 发布流程

> 版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)：`主版本.次版本.修订号`。

## 1. 准备

1. 更新 `CHANGELOG.md`：把 `[未发布]` 的内容整理进新版本号条目（写清新增/修复/变更），并补上日期；
2. 更新版本号：编辑 `src/SnapText/SnapText.csproj` 的 `<Version>`；
3. 本地构建 + 回归：

```powershell
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"; $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"

dotnet build src/SnapText/SnapText.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools/smoke-pipeline.ps1   # 管线冒烟（含二维码往返）
# 有可用桌面时再跑一次交互验收：
powershell -NoProfile -ExecutionPolicy Bypass -File tools/smoke-e2e.ps1
```

## 2. 打包

```powershell
# 自包含发布（用户无需安装 .NET）
dotnet publish src/SnapText/SnapText.csproj -c Release -r win-x64 --self-contained true -o dist/SnapText

# 压缩为分发包
Compress-Archive -Path dist/SnapText/* -DestinationPath dist/SnapText-v0.1.0-win-x64.zip -Force

# 生成校验和（写进 Release 说明，便于用户核对）
certutil -hashfile dist/SnapText-v0.1.0-win-x64.zip SHA256
```

**注意：**

- `dist/` 与 `bin/`、`obj/` 都不进仓库（见 `.gitignore`），只作为 Release 附件上传；
- 发布包内含 `models/v5/` 的全部模型文件（检测/方向来自 NuGet，中文识别模型来自仓库）；
- 发布包的体积约 210MB（含 .NET 运行时 + ONNX Runtime + SkiaSharp + 模型），属于自包含版本的正常范围。

## 3. 打标签并推送

```powershell
git add -A
git commit -m "release: v0.1.0"
git tag -a v0.1.0 -m "SnapText v0.1.0"
git push origin main --tags
```

## 4. 在 GitHub 建 Release

1. 打开 `https://github.com/<你的用户名>/SnapText/releases/new`；
2. 选择刚推送的 tag（或新建同名 tag），标题填 `v0.1.0`；
3. 说明内容直接引用 `CHANGELOG.md` 对应条目，附上 SHA256；
4. 上传 `dist/SnapText-v0.1.0-win-x64.zip`；
5. 发布。

## 5. 发布后检查

- 在一个**干净环境**（没装 .NET 的机器/虚拟机）下载 zip，解压双击 `SnapText.exe`：
  托盘出现「字」图标 → `Ctrl+Alt+Q` 框选一段文字 → 剪贴板有内容；
- 检查 `%LOCALAPPDATA%\SnapText\logs` 无异常；确认"未识别到文字"等边界提示正常。

## 可选（以后需要再加）

- **代码签名**：开源项目可申请 [SignPath](https://signpath.io/) 免费签名，减少 SmartScreen 警告；
- **CI**：GitHub Actions（`windows-latest` + `dotnet 10`）跑构建与 `smoke-pipeline.ps1`；
- **自动更新**：检查 Releases 最新版本并提示（参考 Zshot/opensnap 的实现）。
