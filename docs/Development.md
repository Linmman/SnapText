# 开发与验证

> 面向**维护者与贡献者**的文档。使用者请看 [README](../README.zh-CN.md)；贡献流程见 [CONTRIBUTING.md](../CONTRIBUTING.md)。
> 架构细节见 [Architecture.md](Architecture.md)，发版流程见 [Release.md](Release.md)。

## 一、环境与构建

需要 .NET 10 SDK（本项目开发机上装在用户目录：`%USERPROFILE%\.dotnet`，不污染系统目录）。

```powershell
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"; $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"

dotnet build   src\SnapText\SnapText.csproj -c Release
dotnet publish src\SnapText\SnapText.csproj -c Release -r win-x64 --self-contained true -o dist\SnapText
```

- 检测/方向模型来自 NuGet 包 `RapidOcrNet`，构建时自动复制到输出 `models/v5/`；
- 中文识别模型 `ch_PP-OCRv5_rec_mobile.onnx` + `ppocrv5_dict.txt` 随仓库提交（Apache-2.0，SHA-256 已校验），避免依赖国内访问不稳的下载源。

## 二、目录结构

```
src/SnapText/
├─ App.xaml(.cs)            启动、单实例、CLI 分流
├─ Cli/CliRunner.cs         --grab / --grab-region / --ocr / --ocr-burst / --capture / --scan / --qr / --bench
├─ Core/                    控制器（热键→框选→OCR→剪贴板）、剪贴板服务、IPC 信号、连拍选优
├─ Capture/                 冻结截图、显示器枚举（DPI 感知）
├─ Overlay/                 多屏覆盖层：框选、遮罩、十字线、尺寸提示
├─ Ocr/                     引擎抽象 + PP-OCRv5（RapidOcrNet/ONNX）+ Windows OCR + 选择/回落
├─ Processing/              文本净化、链接识别与修复、二维码/条码识别
├─ Hotkeys/                 全局热键（RegisterHotKey）+ 解析
├─ Settings/                设置持久化（%LOCALAPPDATA%\SnapText\settings.json）+ 开机自启
├─ Tray/                    托盘图标与菜单
├─ Ui/                      轻提示 Toast、结果窗、设置窗
└─ models/v5/               中文识别模型（rec + dict）；检测/方向模型来自 NuGet 包
```

运行时数据：日志 `%LOCALAPPDATA%\SnapText\logs\`，配置 `%LOCALAPPDATA%\SnapText\settings.json`，可选截图 `图片\SnapText\`。

## 三、设计红线（决定"什么不做"）

新增能力必须同时满足三条，否则不合并：

1. **默认零配置**——装上即用，不要求先开开关、先选引擎、先建规则；
2. **零新增步骤**——主流程永远是「热键 → 框选」两步；
3. **零新增打断式 UI**——最多复用现有 Toast，不弹窗、不新开窗口。

智能能力一律"默认静默、失败不打扰"；宁可少一个功能，不可多一步操作。
明确不做：标注 / 贴图 / 长截图 / 录屏 / 复杂规则编辑器（交给 PixPin、Snipaste、Umi-OCR）。

## 四、实测数据（引擎选型依据）

同一张中英混排 + 网址测试图（Windows 11，2880×1800）：

| 引擎 | 识别结果 | 常驻热延迟（p50） | 首次加载 |
|---|---|---|---|
| **PP-OCRv5 本地** | `…B站视频里的文字…https://www.bilibili.com/video/BV1xx411c7mD Hello world, this is OCR test 12345`（**网址 100% 正确**） | **≈406 ms** | ≈1.2 s |
| Windows 系统 OCR | `…B 站视频…https://www.biIibiIi.com/video/BV1xx411 c7mD …`（`bilibili` 误识、链接被空格断开） | ≈109 ms | ≈0.3 s |

连拍（6 帧）实测：三张相同帧时只 OCR 1 帧（859ms → 656ms）；清晰/模糊/噪点三帧正确选中清晰帧（conf=0.989）。

## 五、验证记录

自动化脚本（`tools/`）：

| 脚本 | 作用 | 环境要求 |
|---|---|---|
| `smoke-pipeline.ps1` | 截屏→OCR、区域取字+剪贴板读回、二维码往返、IPC 触发覆盖层 | 内容无关，锁屏也能跑；已有实例时自动跳过相关步骤 |
| `smoke-e2e.ps1` | 交互式全链路：热键 → 覆盖层 → 鼠标拖拽 → 剪贴板 | 需解锁桌面（锁屏会拦截键鼠注入） |
| `test-hotkey-disable.ps1` | 验证"热键留空 = 关闭"（自动备份/还原配置） | 需先退出正在运行的实例 |
| `make-*.ps1` | 生成测试素材（文字图 / 连拍帧 / URL 边界用例） | — |
| `diag-input.ps1` | 诊断环境是否允许注入键鼠（锁屏/远程环境常见限制） | — |

已通过的验证（2026-10-10）：

- ✅ 截屏（全屏/区域）→ OCR 全链路（从真实屏幕读出 803 字符）
- ✅ `--grab-region`；剪贴板写入 + **读回校验一致**
- ✅ 二维码/条码：生成 → 解回（含中文内容）往返一致，单次约 47ms
- ✅ URL 修复边界：5 条用例（断词合并 / 保留说明文字 / 保留普通单词 / 补 https / 中文原文不动）
- ✅ 连拍选优：清晰帧胜出；换帧顺序结果一致；相同帧只 OCR 一次
- ✅ 托盘启动、热键注册、`--grab` IPC → 覆盖层打开
- ✅ 交互端到端（解锁桌面）：热键 → 覆盖层 → 鼠标拖拽（选区 120,150,1380,410）→ OCR 828ms / conf=0.968 → 剪贴板命中 3/3
- ✅ 「打开链接」按钮：作者手动验证可用（暂无自动化点击测试）

## 六、README 三语同步规则

`README.md`（英文，**基准版**）、`README.zh-CN.md`、`README.ja.md`。

- 改动用户可见行为时，**先改英文版**，再同步中/日文；
- 来不及翻译时，在中文/日文版本顶部保留"以英文版为准"的说明即可，不要留半截不一致的内容；
- 作者向的细节（构建、验证、结构）一律放本文件，不要写回 README。
