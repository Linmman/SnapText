# SnapText · 屏幕取字

![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6?logo=windows)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![OCR](https://img.shields.io/badge/OCR-PP--OCRv5%20%2B%20Windows-2E7D32)
![license](https://img.shields.io/badge/license-MIT-yellow)

[English](README.md) | **简体中文** | [日本語](README.ja.md)

> 按一下热键，框住屏幕上任意文字 —— 松手时它**已经在剪贴板里**。全程本地离线运行。

## 为什么需要它

屏幕上很多文字是选不中的：视频字幕、弹幕、聊天通知、游戏界面、PDF、报错弹窗。传统做法是一串步骤：截图 → 切到别的工具 → 粘贴 → 点识别 → 再复制。

SnapText 把这串步骤压成一步：**热键 → 框选 → 粘贴**。配合 [Ditto](https://ditto-cp.sourceforge.io/) 或 Windows 剪贴板历史（`Win+V`），"看到一句想留下的句子/网址"就变成一个条件反射。

- **默认离线**：识别在本机完成（PP-OCRv5），不上传、不要 API Key、不用登录。
- **为中文视频场景调过**：抖音/B站的字幕、一闪而过的网址正是最难的一类——用中文优化的引擎识别，链接会修好而不是弄坏。
- **不打扰**：常驻托盘，取字时不弹主窗口，用完把焦点还给你原来的窗口，下一步直接 `Ctrl+V`。

![SnapText 实际使用：热键 → 框选 → 松手即复制](docs/images/demo.gif)

## 下载与安装

| | |
|---|---|
| 系统要求 | Windows 10 / 11，64 位 |
| 下载 | [Releases](../../releases) → 解压 → 运行 `SnapText.exe` |
| 安装方式 | 免安装：解压即用，只有 `%LOCALAPPDATA%\SnapText` 和 `图片\SnapText` 会写入文件（开启"开机自启"会额外写一条 HKCU Run 注册表项） |
| .NET | 不需要 —— 发布包自带运行时 |
| 体积 | 解压后约 210MB（含 .NET 运行时和离线 OCR 模型） |

首次运行 Windows 可能弹出 SmartScreen 提示（应用没有代码签名——个人开源项目的常态）。你可以用 Release 说明里的 SHA-256 校验下载文件，或者自行编译。

## 怎么用

1. 运行 `SnapText.exe`，托盘出现蓝色 **字** 图标。
2. 按 `Ctrl+Alt+Q`，框住文字，松手 —— 完事，`Ctrl+V` 粘贴。

   ![已复制到剪贴板；识别到网址时会附「打开链接」按钮](docs/images/toast.png)
3. 托盘图标：左键再取一次，右键出菜单（重复上次区域、**最近取字**、设置…）。

| 热键 | 作用 |
|---|---|
| `Ctrl+Alt+Q` | 取字：冻结屏幕 → 框选 → 文字进剪贴板；画面在动（播放中的视频）时自动多采样几帧、选最干净的一帧 |
| `Ctrl+Alt+R` | 对**上次区域**再取一次（不用重新框；字幕位置固定时最顺手） |
| 托盘左键 | 等同于 `Ctrl+Alt+Q` |
| `Esc` / 右键 | 取消框选 |

两个热键都可以在**设置**里改，或者清空以彻底关闭（不想要的快捷键不必占着）。设置里还能调：界面语言（中文 / 日本語 / English，默认跟随系统）、OCR 引擎、静默复制还是弹可编辑结果窗、多行是否合并成一句、用哪个程序打开链接、开机自启、是否保留最近 200 条取字历史、是否同时保存截图。

## 它能做什么

- **一个热键取字**：先冻结画面再框选，不会和正在播放的视频抢时间；完成后焦点自动回到你的窗口。静止画面按单帧速度识别；画面在动时自动多取几帧、挑最干净的一帧，避免正好抓到切换/模糊的那一瞬间。
- **复制不会骗你**：写入失败自动重试，并读回校验；失败会明确告诉你，而不是显示"已复制"却粘不出东西。
- **最近取字，一点回来**：每次取字（含连拍、二维码）都会在本机留一份（最多 200 条，连续重复自动跳过）。托盘右键 →「最近取字」列出最近 15 条，点一条就重新复制回剪贴板；需要找更早的用「搜索历史…」打开小搜索面板（只在你点击时出现——绝不自动弹）。不想要时在同菜单里关掉或清空。
- **界面说你的语言**：托盘菜单、提示、设置、结果窗、框选提示都有 中文 / 日本語 / English 三种界面，默认跟随系统语言，在设置里切换后立即生效。
- **二维码/条码**：框住码就把内容解出来复制（与 OCR 并行执行，不额外耗时）。
- **链接处理**：结果里有网址时，提示上会出现 **打开链接**（默认浏览器，也可在设置里指定别的程序）。如果整段就是链接，会自动去掉 OCR 塞进来的空格、补全 `https://`；但如果链接后面还跟着说明文字，**原文一个字都不改**。
- **中文排版净化**：自动去掉 OCR 在中文之间插入的假空格；多行可选合并成一句。
- **双 OCR 引擎**：默认 PP-OCRv5 本地引擎（中英混排、网址、代码更准），Windows 系统 OCR 作为快速回退；可自动也可固定。两者的实测对比见 [docs/Development.md](docs/Development.md)。
- **多显示器、混合缩放**：每块屏独立覆盖层，坐标按物理像素处理。

## 命令行（可选）

给脚本、快捷方式、AutoHotkey 用（没有 `--help`，这就是全部命令）：

```powershell
SnapText.exe --grab                                        # 让已运行的实例取一次字
SnapText.exe --grab-region 100,900,1200,200 --out r.txt    # 固定区域取字（如字幕条）
SnapText.exe --ocr-adaptive 100,900,1200,200 --out r.txt   # 同上，但对画面在动的场景多帧选优
SnapText.exe --ocr shot.png --engine ppocr --out r.txt     # 识别一张图片（不启动托盘）
SnapText.exe --scan qr.png                                 # 解二维码 / 条码
SnapText.exe --qr "https://example.com" --out qr.png       # 生成二维码
SnapText.exe --capture --out screen.png                    # 抓整个桌面
SnapText.exe --history list --n 20                         # 最近取字（最新在前）
SnapText.exe --history search <关键词>                      # 搜索取字历史
SnapText.exe --history clear                               # 清空取字历史
```

参数：`--engine auto|ppocr|windows`、`--no-merge`（保留换行）、`--no-copy`（不写剪贴板）。

## 出问题了怎么办

见 **[docs/Troubleshooting.md](docs/Troubleshooting.md)**（中文），覆盖热键冲突、识别不准、剪贴板失败、SmartScreen 提示、托盘图标不见了、如何彻底卸载等。最常见的几条：

- **按热键没反应**：多半被别的软件占了（启动时会提示），在设置里换一个或清空；锁屏时任何热键都不工作（Windows 的限制）。
- **识别不准**：把引擎设为 PP-OCRv5，框紧一点，能暂停视频就暂停。
- **提示挡住了想看的画面**：点它本身或右上角 ✕ 即可收起，它自己也会淡出。
- **日志**：`%LOCALAPPDATA%\SnapText\logs\`。

## 已知限制

- 截图走 GDI：HDR 屏幕和独占全屏游戏可能偏色或抓不到。
- 跨屏拖拽时，选区高亮只在起始的那块屏上完整显示（取到的数据是对的）。
- 锁屏状态下不可用（Windows 行为，与本工具无关）。
- 艺术字、极低对比度、被弹幕遮住的文字，OCR 仍可能出错。

## 参与开发

欢迎提 issue 和 PR —— 构建方法、测试脚本和项目的三条设计红线见 **[CONTRIBUTING.md](CONTRIBUTING.md)** 与 **[docs/Development.md](docs/Development.md)**（简短版：不多一步操作、不弹新窗口、零配置可用）。
开发者文档目前是中文；用英文提 issue / PR 完全没问题。

## 许可

MIT，见 [LICENSE](LICENSE)。
随包使用的第三方组件与模型见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
