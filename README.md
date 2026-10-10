# SnapText · 屏幕取字

![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6?logo=windows)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![OCR](https://img.shields.io/badge/OCR-PP--OCRv5%20%2B%20Windows-2E7D32)
![license](https://img.shields.io/badge/license-MIT-yellow)

> 按一下热键 → 框选屏幕上的文字 → **自动进剪贴板**。全程本地离线（PP-OCRv5），专治抖音/B站视频里的字幕、句子和网址。

配合 Ditto、Windows 剪贴板历史（Win+V）等工具，形成"看到 → 取下 → 粘贴"的一步到位工作流。

<!-- TODO(作者): 截图位。把自己屏幕上的取字效果（覆盖层/轻提示）截 1-2 张放进 docs/images/，再在这里贴 `![效果](docs/images/demo.png)`。注意别带隐私内容。 -->

**文档**：[架构说明](docs/Architecture.md) · [常见问题与排查](docs/Troubleshooting.md) · [发布流程](docs/Release.md) · [调研与方案](docs/调研与方案.md)

---

## 一、功能一览

| 功能 | 说明 |
|---|---|
| 全局热键取字 | 默认 `Ctrl+Alt+Q`：冻结屏幕 → 框选 → 识别 → 自动复制 → 右下角轻提示 → 焦点回到原窗口（直接 Ctrl+V 即可粘贴） |
| 重复上次区域 | 默认 `Ctrl+Alt+R`：不框选，直接对上次区域重新取字（视频字幕位置固定的场景） |
| 复制可靠性 | 剪贴板写入自动重试，并**读回校验**后才提示成功（避免"没写进去却说已复制"）；识别严重不确定时提示里会多一句"建议核对" |
| 扫码取内容 | 框选二维码/条码即识别（与 OCR 并行，实测约 47ms，不增加延迟），内容直接进剪贴板 |
| 链接智能 | 识别到网址时提示里附「打开链接」；整段就是链接时自动去掉内部空格、补全 `https://`（视频里链接被断行/断词的场景） |
| 本地 OCR 双引擎 | 默认 **PP-OCRv5**（中英混排、中文更准、完全离线）；可选 Windows 系统 OCR（快，纯英文场景够用）；"自动"模式下 PP-OCR 失败会回落系统 OCR |
| 文本净化 | 自动去掉中文之间的假空格；多行按标点合并成一整行（可关） |
| 托盘常驻 | 左键单击 = 取字；右键菜单：取字 / 重复上次区域 / 设置 / 退出。静默启动约 1 秒，空闲时自动释放 OCR 内存 |
| 设置窗口 | 热键、引擎、静默复制 / 弹结果窗、合并行、开机自启、同时存图 |
| 脚本化入口 | `--grab`（通知已运行实例取字，可给 AutoHotkey/快捷方式用）、`--grab-region x,y,w,h`（固定区域取字）、`--ocr`、`--capture`、`--bench` |

多显示器、混合 DPI（PerMonitorV2）已支持：每块屏一个覆盖层，坐标按物理像素处理。

## 二、快速开始

1. 解压/运行 `dist\SnapText\SnapText.exe`（**自包含版，无需安装 .NET**）。
2. 托盘出现蓝色"字"图标，并弹出一次"已启动"提示。
3. 把它拖到托盘常驻后，任意界面按 `Ctrl+Alt+Q`，框选文字，松手即复制。
4. 想开机自启：托盘右键 → 设置 → 勾选"开机自动启动"。

> 注意：Windows 桌面整体缩放 150%/200% 的机器上，第一次使用建议先在浏览器里框一段中文验证效果；
> 若热键被其他软件占用，启动时会提示，可在设置里换一个组合。

## 二·五、设计原则（红线）

同类工具的优点会借鉴，但任何新增能力都必须同时满足下面三条，否则不做：

1. **默认零配置**——装上即用，不要求先开开关、先选引擎、先建规则；
2. **零新增步骤**——主流程永远是「热键 → 框选」两步；
3. **零新增打断式 UI**——最多复用现有 Toast，不弹窗、不新开窗口。

智能能力一律"默认静默、失败不打扰"；**宁可少一个功能，不可多一步操作**。
明确不做：标注 / 贴图 / 长截图 / 录屏 / 复杂规则编辑器（交给 PixPin、Snipaste、Umi-OCR 那些专业工具）。

## 三、命令行 / 自动化

```powershell
# 取字（正常交互流程，UI 由已运行的实例完成）
SnapText.exe --grab

# 固定区域取字：截屏指定区域 → OCR → 写剪贴板（可脚本化，适合"字幕位置固定"的场景）
SnapText.exe --grab-region 100,900,1200,200 --engine ppocr --out result.txt

# 直接 OCR 一张图片（不启动托盘）
SnapText.exe --ocr shot.png --engine ppocr --out result.txt

# 抓全屏保存 PNG
SnapText.exe --capture --out screen.png

# 解二维码 / 条码（输出其内容）
SnapText.exe --scan qr.png

# 生成二维码 PNG（把剪贴板里的文本变成手机可扫的码）
SnapText.exe --qr "https://www.bilibili.com/video/BV1xx411c7mD" --out qr.png

# 性能基准（同进程多次识别，测常驻后的热延迟）
SnapText.exe --bench shot.png --engine ppocr --n 10
```

参数：`--engine auto|ppocr|windows`、`--no-merge`（不合并多行）、`--no-copy`（不写剪贴板）。

**给 AutoHotkey / 快捷方式**：绑定 `SnapText.exe --grab` 即可把取字挂在任意按键或鼠标手势上。

## 四、实测数据（本机，Windows 11 25H2 / 2880×1800）

同一张中英混排+网址测试图：

| 引擎 | 识别结果 | 常驻热延迟（p50） | 首次加载 |
|---|---|---|---|
| **PP-OCRv5 本地** | `SnapText 屏幕取字测试B站视频里的文字一键复制到剪贴板https://www.bilibili.com/video/BV1xx411c7mD Hello world, this is OCR test 12345`（**网址 100% 正确**） | **≈406 ms** | ≈1.2 s |
| Windows 系统 OCR | `...B 站视频...https://www.biIibiIi.com/video/BV1xx411 c7mD ...`（`bilibili` 误识、链接被空格断开） | ≈109 ms | ≈0.3 s |

结论：中文两者都可用，**网址/英文/代码等拉丁字符 PP-OCR 明显更准**；系统 OCR 作为快速回退仍有价值。

## 五、已验证 / 待人工确认

自动化验证（`tools\smoke-pipeline.ps1`，内容无关，锁屏环境也能跑；`tools\smoke-e2e.ps1` 为交互式全链路）：

- ✅ 截屏（全屏/区域）→ OCR 全链路（从真实屏幕读出 803 字符）
- ✅ `--grab-region` 区域取字；剪贴板写入 + **读回校验一致**
- ✅ 二维码/条码：生成 → 解回（含中文内容）往返一致，单次约 47ms
- ✅ URL 断行修复：`…BV1xx411 c7mD` → `…BV1xx411c7mD`
- ✅ 托盘实例启动、热键注册、`--grab` IPC 触发 → 覆盖层打开（日志确认，覆盖 2880×1800 主屏）
- ✅ **交互端到端**（解锁桌面）：热键 → 覆盖层 → 鼠标拖拽（选区 120,150,1380,410）→ OCR 828ms / conf=0.968 → 剪贴板命中 3/3 关键词
- ⏸ 「打开链接」按钮的鼠标点击：代码路径最短（单击 → `Process.Start`），未做自动化点击验证，日常第一周留意即可

## 六、构建 / 发布 / 测试

```powershell
# 开发环境（本机已装到用户目录：C:\Users\<你>\.dotnet）
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"; $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"

dotnet build   src\SnapText\SnapText.csproj -c Release

# 自包含发布（无需目标机器安装 .NET）
dotnet publish src\SnapText\SnapText.csproj -c Release -r win-x64 --self-contained true -o dist\SnapText

# 自动化验收（截屏→OCR→区域取字→覆盖层）
powershell -NoProfile -ExecutionPolicy Bypass -File tools\smoke-pipeline.ps1

# 生成测试图 / 交互式端到端（需真实桌面，可注入键鼠）
powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-test-image.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools\smoke-e2e.ps1
```

## 七、目录结构

```
src/SnapText/
├─ App.xaml(.cs)            启动、单实例、CLI 分流
├─ Cli/CliRunner.cs         --ocr / --capture / --bench / --grab / --grab-region
├─ Core/                    控制器（热键→框选→OCR→剪贴板）、剪贴板服务、IPC 信号
├─ Capture/                 冻结截图、显示器枚举（DPI 感知）
├─ Overlay/                 多屏覆盖层：框选、遮罩、十字线、尺寸提示
├─ Ocr/                     引擎抽象 + PP-OCRv5（RapidOcrNet/ONNX）+ Windows OCR + 选择/回落
├─ Processing/              文本净化（中文空格、合行）
├─ Hotkeys/                 全局热键（RegisterHotKey）+ 解析
├─ Settings/                设置持久化（%LOCALAPPDATA%\SnapText\settings.json）+ 开机自启
├─ Tray/                    托盘图标与菜单
├─ Ui/                      轻提示 Toast、结果窗、设置窗
└─ models/v5/               中文识别模型（rec + dict）；检测/方向模型来自 NuGet 包
```

运行时数据：日志 `%LOCALAPPDATA%\SnapText\logs\`，配置 `%LOCALAPPDATA%\SnapText\settings.json`，可选截图 `图片\SnapText\`。

## 八、技术选型与许可

- .NET 10 + WPF（Windows 原生集成：托盘/热键/多屏 DPI/剪贴板/焦点控制）
- OCR：**RapidOcrNet 4.2**（MIT） + **PP-OCRv5 mobile**（PaddleOCR，Apache-2.0，模型经 SHA256 校验后随包分发）
- 托盘 H.NotifyIcon.Wpf（MIT）、二维码（计划）ZXing.Net（MIT）、SkiaSharp（MIT）、ONNX Runtime（MIT）
- 应用自身代码：建议 MIT

## 九、路线图（下一步）

- ~~**M4**：结果里识别到网址 → 轻提示附「打开」按钮；二维码/条码识别；URL 去空格/断行修复~~ ✅ 已完成
- **M5**：历史记录（SQLite + 搜索，可回填）；**视频连拍取字**（多帧 OCR 融合投票，对付滚动字幕/弹幕）
- M6：翻译（自带 API Key）、云 OCR 插件、自动更新

调研与方案见 [docs/调研与方案.md](docs/调研与方案.md)。
