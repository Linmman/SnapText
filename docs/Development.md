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
├─ Cli/CliRunner.cs         --grab / --grab-region / --ocr / --ocr-burst / --ocr-adaptive / --capture / --scan / --qr / --history / --loc / --bench
├─ Core/                    控制器（热键→框选→OCR→剪贴板）、剪贴板服务、IPC 信号、自适应多帧选优（BurstRunner）、取字历史（HistoryStore）
├─ Capture/                 冻结截图、显示器枚举（DPI 感知）
├─ Overlay/                 多屏覆盖层：框选、遮罩、十字线、尺寸提示
├─ Ocr/                     引擎抽象 + PP-OCRv5（RapidOcrNet/ONNX）+ Windows OCR + 选择/回落
├─ Processing/              文本净化、链接识别与修复、二维码/条码识别
├─ Hotkeys/                 全局热键（RegisterHotKey）+ 解析
├─ Localization/            界面文案表（zh / ja / en 三语字典）+ Loc.T + XAML 取值标记扩展
├─ Settings/                设置持久化（%LOCALAPPDATA%\SnapText\settings.json）+ 开机自启
├─ Tray/                    托盘图标与菜单（含「最近取字」子菜单）
├─ Ui/                      轻提示 Toast、结果窗、设置窗、历史搜索面板（按需打开）
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

**红线 3 的边界（2026-10-10 落实）**：禁的是"**自动出现**、打断主流程"的界面。"用户主动点击才打开"的按需面板不算——「设置」窗口一直如此，取字历史的「搜索历史…」面板同理（永不自动弹出，进不去主流程）。特别地，"把搜索框内嵌进托盘右键菜单"这类零窗口黑科技曾被否决：WPF 菜单弹层里的键盘焦点与中文/日文 IME 不可靠。

**新增用户可见文案**：必须同时进 `src/SnapText/Localization/Strings.cs` 的 zh / ja / en 三张表；`SnapText.exe --loc check`（与 `tools/smoke-loc.ps1`）会校验三种语言键集合一致。日志（`Log.*`）保持中文，不本地化。

## 四、实测数据（引擎选型依据）

同一张中英混排 + 网址测试图（Windows 11，2880×1800）：

| 引擎 | 识别结果 | 常驻热延迟（p50） | 首次加载 |
|---|---|---|---|
| **PP-OCRv5 本地** | `…B站视频里的文字…https://www.bilibili.com/video/BV1xx411c7mD Hello world, this is OCR test 12345`（**网址 100% 正确**） | **≈406 ms** | ≈1.2 s |
| Windows 系统 OCR | `…B 站视频…https://www.biIibiIi.com/video/BV1xx411 c7mD …`（`bilibili` 误识、链接被空格断开） | ≈109 ms | ≈0.3 s |

连拍（6 帧）实测：三张相同帧时只 OCR 1 帧（859ms → 656ms）；清晰/模糊/噪点三帧正确选中清晰帧（conf=0.989）。
自适应取字实测（`tools/smoke-adaptive.ps1`）：静止画面 frames=2 / ocr=1 / conf≈0.999（≈单帧速度）；画面每 80ms 变化时 frames=6 / ocr=5 / 约 1.8 秒，选中最优帧。

## 五、验证记录

自动化脚本（`tools/`）：

| 脚本 | 作用 | 环境要求 |
|---|---|---|
| `smoke-pipeline.ps1` | 截屏→OCR、区域取字+剪贴板读回、二维码往返、IPC 触发覆盖层 | 内容无关，锁屏也能跑；已有实例时自动跳过相关步骤 |
| `smoke-history.ps1` | 取字历史的写入/倒序/连续重复跳过/搜索/UTF-8 往返/条数上限裁剪/JSONL 格式/清空/用法退出码/默认存储路径（12 项断言） | 内容无关，锁屏也能跑；用临时文件隔离（`SNAPTEXT_HISTORY_FILE` / `SNAPTEXT_HISTORY_MAX`），可与运行中的实例并存 |
| `smoke-loc.ps1` | 界面语言：`--loc check` 三语键集合一致 + 三语言 CLI 输出互不相同 + zh/ja/en 各启动一次应用（日志零 `Missing UI string`、托盘创建成功） | 锁屏也能跑；不改设置（语言用 `SNAPTEXT_LANG` 强制）；已有实例在运行时自动跳过启动项 |
| `smoke-adaptive.ps1` | 自适应取字：静止画面走单帧快路径（frames=2/ocr=1）、画面在动走多帧选优（frames=6/ocr≥2 且结果正确） | 需解锁桌面（会显示一个小测试窗口，不注入键鼠、不写剪贴板）；可与运行中的实例并存 |
| `smoke-e2e.ps1` | 交互式全链路：热键 → 覆盖层 → 鼠标拖拽 → 剪贴板 | 需解锁桌面（锁屏会拦截键鼠注入）**且需先退出正在运行的 SnapText**（检测到已有实例会自动 SKIP，不接管用户实例） |
| `test-hotkey-disable.ps1` | 验证"热键留空 = 关闭"（自动备份/还原配置） | 需先退出正在运行的实例 |
| `make-*.ps1` | 生成测试素材（文字图 / 连拍帧 / URL 边界用例） | — |
| `diag-input.ps1` | 诊断环境是否允许注入键鼠（锁屏/远程环境常见限制） | — |

已通过的验证（2026-10-10）：

- ✅ 截屏（全屏/区域）→ OCR 全链路（从真实屏幕读出 803 字符）
- ✅ `--grab-region`；剪贴板写入 + **读回校验一致**
- ✅ 二维码/条码：生成 → 解回（含中文内容）往返一致，单次约 47ms
- ✅ URL 修复边界：5 条用例（断词合并 / 保留说明文字 / 保留普通单词 / 补 https / 中文原文不动）
- ✅ 连拍选优：清晰帧胜出；换帧顺序结果一致；相同帧只 OCR 一次
- ✅ 自适应取字（`tools/smoke-adaptive.ps1`，4/4 PASS）：静止画面 frames=2/ocr=1（conf≈0.999）；画面在动 frames=6/ocr=5、约 1.8 秒选中最优帧；`--no-copy` 不碰剪贴板
- ✅ 托盘启动、热键注册、`--grab` IPC → 覆盖层打开
- ✅ 交互端到端（解锁桌面）：热键 → 覆盖层 → 鼠标拖拽（选区 120,150,1380,410）→ OCR 828ms / conf=0.968 → 剪贴板命中 3/3
- ✅ 「打开链接」按钮：作者手动验证可用（暂无自动化点击测试）
- ✅ 取字历史（`tools/smoke-history.ps1`，12 项断言全 PASS）：写入 / 最新在前 / 连续重复跳过 / 搜索命中与未命中 / UTF-8 往返 / 条数上限裁剪 / JSONL 格式 / 清空删文件 / 用法退出码 / 默认存储路径
- ✅ 取字历史端到端落盘：真实热键 → 框选 → OCR → 剪贴板 后，历史文件新增 1 条（`source=ocr`）；随后同一文本再取一次，历史保持 1 条（去重生效）
- ✅ GDI+ 并发修复：修复前连续两次 e2e 中 PP-OCR 抛 `Object is currently in use elsewhere` 并静默回落系统 OCR（识别质量下降）；修复后连续两次 e2e 均命中 PP-OCRv5（conf=0.968，无回落）
- ⏸ 托盘「最近取字」子菜单的**点击复制**、以及「搜索历史…」面板的点击/输入：路径简单（点条目 → 写剪贴板 + 轻提示），暂无自动化点击测试，待手动过一遍（与「打开链接」按钮同级别）
- ⏸ 托盘/exe 图标改为嵌入 `SnapText.ico` 资源后的**实际显示效果**（explorer/任务栏/托盘）：构建期已验证 exe 图标可提取且渲染一致（`ExtractAssociatedIcon`），托盘 pack 资源加载待下次启动人工确认
- ✅ 界面语言（`tools/smoke-loc.ps1`，8/8 PASS）：三语言文案表键集合一致（117 键 × 3 语言）；CLI 输出随 `SNAPTEXT_LANG` 切换且三种语言互不相同；zh/ja/en 各启动一次应用，日志均无 `Missing UI string`、托盘创建成功
- ⏸ 设置窗口「界面语言」下拉选择与"保存后即时切换"：UI 交互无自动化测试，待手动过一遍（语言解析与 CLI 同源，已被上面的脚本覆盖）

## 六、README 三语同步规则

`README.md`（英文，**基准版**）、`README.zh-CN.md`、`README.ja.md`。

- 改动用户可见行为时，**先改英文版**，再同步中/日文；
- 来不及翻译时，在中文/日文版本顶部保留"以英文版为准"的说明即可，不要留半截不一致的内容；
- 作者向的细节（构建、验证、结构）一律放本文件，不要写回 README。
- 演示素材（放 `docs/images/`）：整屏截图 ≤300KB、宽 1200-1440px；**局部 UI 元素截图（如提示条）按元素实际尺寸出图、≤300KB**；**GIF ≤2MB**（宽 900-1000px、10-12fps、3-6 秒；用 ffmpeg 调色板两步法压缩：`fps=12,scale=900:-1,split[s0][s1];[s0]palettegen[p];[s1][p]paletteuse`）。超过 ~5MB 就别用 GIF——改用 GitHub 附件方式嵌 MP4（在网页版编辑 README，把 mp4 拖进编辑框，会生成可点播的视频链接）。
- 演示素材只用公开内容；**不要出现聊天记录、邮箱、个人信息**。
