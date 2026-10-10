# 架构说明

> 一句话：**一个隐形的小工具**——按热键、框选、文字进剪贴板。不常驻可见 UI，不抢焦点，不打断你正在做的事。

## 一、模块划分

```
src/SnapText/
├─ App.xaml(.cs)            进程入口：单实例、CLI 分流、异常兜底
├─ Cli/CliRunner.cs         无界面命令：--ocr / --capture / --bench / --grab / --grab-region / --scan / --qr
├─ Core/
│   ├─ SnapTextController   总控：热键 → 截屏 → 框选 → OCR/扫码 → 剪贴板 → 提示 → 归还焦点
│   ├─ ClipboardService     写剪贴板：重试 + 读回校验
│   └─ IpcSignals           `--grab` 用的命名事件（Local\SnapText.Grab）
├─ Capture/
│   ├─ ScreenCapture        GDI 冻结帧：整屏/区域抓取与裁剪（物理像素）
│   └─ MonitorEnumerator    显示器枚举 + 每屏 DPI（PerMonitorV2 基础）
├─ Overlay/                 每屏一个冻结帧覆盖层：十字线、尺寸提示、选区高亮（多窗共享 SelectionState）
├─ Ocr/
│   ├─ IOcrEngine/OcrService 引擎抽象与选择（auto / ppocr / windows，失败自动回落）
│   ├─ PpOcrEngine         PP-OCRv5 本地（RapidOcrNet + ONNX Runtime）：会话懒加载、空闲 60s 释放
│   ├─ WindowsOcrEngine    Windows.Media.Ocr（零模型；中文需系统语言包）
│   └─ OcrPreprocess       小图放大（<64px 高 ×3，<150px 高 ×2）
├─ Processing/
│   ├─ PostProcessor       文本净化：中文假空格、多行按标点合并
│   ├─ UrlHelper           链接识别 + 修复（整段是链接时去内部空格、补 https://）
│   └─ CodeScanner         二维码/条码（ZXing，全格式 + TryHarder/TryInverted）
├─ Hotkeys/                全局热键（RegisterHotKey + 消息窗口）与字符串解析/冲突检测
├─ Settings/               settings.json、开机自启（HKCU Run）、MIT 说明
├─ Tray/                   托盘图标（运行时生成"字"图标）+ 菜单
└─ Ui/                     轻提示 Toast（可带「打开链接」）、结果窗、设置窗
```

运行时数据（不随程序目录）：

| 内容 | 位置 |
|---|---|
| 日志 | `%LOCALAPPDATA%\SnapText\logs\app-yyyyMMdd.log` |
| 配置 | `%LOCALAPPDATA%\SnapText\settings.json` |
| 可选截图 | `图片\SnapText\yyyyMMdd-HHmmss.png` |

## 二、一次取字的完整链路

```
[热键 Ctrl+Alt+Q / IPC --grab]
  → 记录前台窗口（用于结束后归还焦点）
  → GDI 抓取整个虚拟桌面（冻结帧，多屏拼接为一张位图）        ~30–60ms
  → 每屏一个覆盖层窗口（SetWindowPos 铺满物理像素；遮罩 + 选区未变暗）
  → 用户框选（鼠标坐标一律取 GetCursorPos 的物理像素，规避混合 DPI 换算）
  → 松开鼠标：立即关闭覆盖层，从冻结帧裁剪选区
  → 并行执行：
      ├─ 二维码/条码扫描（ZXing，实测 ~47ms）
      └─ OCR：PP-OCRv5 本地（常驻热态 p50 ~406ms）或系统 OCR（~109ms）
  → 结果优先级：识别到码 → 以码的内容为准；否则用 OCR 文本
  → 文本净化（中文空格 / 合行 / 链接修复）
  → 写剪贴板（重试 + 读回校验）
  → 右下角轻提示（含「打开链接」按钮，若有链接；不抢焦点）
  → 归还焦点给原窗口（用户直接 Ctrl+V）
```

延迟预算：**常驻后 p50 约 450ms，p95 < 1s**（PP-OCR 首次调用需加载模型约 1.2s，之后复用；空闲 60s 自动释放内存，下次再加载）。

## 三、关键决策与理由

| 决策 | 理由 |
|---|---|
| **先冻结、再框选** | 视频在动，先在覆盖层上"停下来"才框得准；也避免选框期间的画面变化导致结果不符 |
| **鼠标坐标用物理像素** | 混合 DPI 多屏下最不容易错的方案：覆盖层用 `SetWindowPos` 铺物理像素，选区用 `GetCursorPos` 读物理像素，渲染时再换算成各窗口的 DIP |
| **双引擎 + 自动回落** | PP-OCR 中文/网址明显更准（实测 `bilibili` vs `biIibiIi`），但它需要模型；系统 OCR 零依赖、极快，适合纯英文/应急。`auto` 模式下 PP-OCR 失败自动回落 |
| **模型懒加载 + 空闲卸载** | 常驻内存 ≤60MB；空闲 60s 释放 ONNX 会话（借鉴 Zshot 的做法） |
| **二维码与 OCR 并行** | 扫码只花 ~47ms，并行后既不拖慢取字，也不丢"扫码"这个高频意图 |
| **写剪贴板后读回校验** | 避免"提示已复制、其实没写进去"（同类项目的真实踩坑）；读不到时不误报失败 |
| **归还焦点** | "一步到位"的关键：复制完焦点回到原窗口，下一个动作就能 Ctrl+V。若焦点已被用户切走则不强抢 |
| **提示不抢焦点** | Toast 用 `WS_EX_NOACTIVATE`+`ToolWindow`，可点击（含按钮）但不激活，不打断输入 |
| **文本净化** | 中文之间空格是 OCR 拆词的产物，直接删；字幕多行按标点合并成整句 |
| **链接修复只作用于"整段是链接"** | 最安全的边界：混合文本绝不改动原文，只在提示里提供「打开链接」；两段式（链接被空格断成两截）才合并，且第二段必须"像链接碎片"（含数字或点） |

### 连拍取字（M5）

按下连拍热键 → 对同一区域连抓 6 帧（间隔 90ms，**不冻结屏幕**，所以画面继续在动）→ 相邻帧做 16×16 灰度差异去重（视频暂停时只 OCR 1 帧，耗时和单帧一样）→ 逐帧 OCR → 打分 `置信度×100 + min(字数,60)` → 取最高分那帧的文本。

- **为什么不做"多数投票"**：滚动/切换中的字幕，不同帧内容本来就不同，投票会选出"上一句"；用户要的始终是"此刻屏幕上是什么"。
- **为什么是单独热键**：连拍要跑多次 OCR（约 1.1s），不能拖慢日常单帧取字（约 0.45s）。画面静止时应该用普通取字。
- **已知不做**：把跨帧滚动的文字拼成一整句（需要按文本行位置对齐 + 重叠检测，收益低、复杂度高）。

## 四、并发与线程模型

- UI 线程（STA）：托盘、覆盖层、Toast、结果窗、剪贴板写入（WPF `Clipboard` 要求 STA）。
- OCR：`Task.Run` 到线程池（CPU 密集且是同步流程）。
- PP-OCR 会话：`lock` 串行化推理；空闲卸载与推理共用同一把锁，避免用已释放会话。
- IPC 信号：后台线程 `WaitOne(1s)` 循环，收到信号后 `Dispatcher.Invoke` 切回 UI 线程执行取字。

## 五、构建与发布

```powershell
# 依赖：.NET 10 SDK（本仓库在用户目录安装过：%USERPROFILE%\.dotnet）
dotnet build src/SnapText/SnapText.csproj -c Release

# 自包含发布（目标机器无需安装 .NET）
dotnet publish src/SnapText/SnapText.csproj -c Release -r win-x64 --self-contained true -o dist/SnapText
```

- 检测/方向模型来自 NuGet 包 `RapidOcrNet`（构建时自动复制到输出 `models/v5/`）；
- 中文识别模型 `ch_PP-OCRv5_rec_mobile.onnx` + `ppocrv5_dict.txt` **随仓库提交**（Apache-2.0，SHA256 校验通过），避免依赖国内访问不稳的下载源。

## 六、已知限制

- 屏幕抓取用 GDI：HDR 显示器和独占全屏游戏可能偏色/抓不到（后续可换 Windows.Graphics.Capture，参考 Zshot 路线）；
- 跨屏拖拽时，选区高亮只在起始屏完整渲染（数据裁剪正确，仅视觉反馈受限）；
- 锁屏/安全桌面下，全局热键与剪贴板对整个会话不可用（Windows 行为，非本工具限制）；
- OCR 对艺术字、极端低对比度、被弹幕遮挡的文字仍可能出错——这是当前 OCR 模型的边界，不是参数问题。
