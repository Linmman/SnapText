namespace SnapText.Localization;

/// <summary>
/// 界面文案表：键 → 各语言文本（zh / ja / en）。
/// 三种语言的键集合必须完全一致（用 `SnapText.exe --loc check` 校验）；
/// 缺键时 Loc.T 依次回退 en → 键名本身，并记一条 WARN 日志。
/// 日志（Log.*）不参与本地化：日志保持中文，便于作者排查。
/// </summary>
internal static class Strings
{
    public static readonly Dictionary<string, string> Zh = new()
    {
        // —— 通用 ——
        ["Common_Close"] = "关闭",
        ["Common_Save"] = "保存",
        ["Common_Cancel"] = "取消",
        ["Common_Browse"] = "浏览…",

        // —— 托盘 ——
        ["Tray_Grab"] = "取字",
        ["Tray_RepeatLast"] = "重复上次区域",
        ["Tray_History"] = "最近取字",
        ["Tray_HistorySearch"] = "搜索历史…",
        ["Tray_HistoryClear"] = "清空历史",
        ["Tray_HistoryEmpty"] = "（还没有记录）",
        ["Tray_Settings"] = "设置…",
        ["Tray_Exit"] = "退出",
        ["Tray_Tooltip"] = "SnapText · 屏幕取字",

        // —— 提示（Toast）与状态 ——
        ["Toast_EngineUnavailable_Title"] = "OCR 引擎不可用",
        ["Toast_EngineUnavailable_Body"] = "未找到 PP-OCR 模型（models/v5），系统 OCR 也没有可用语言包。请检查程序目录完整性。",
        ["Toast_Started_Title"] = "SnapText 已启动",
        ["Toast_Started_NoHotkey"] = "热键已关闭 · 托盘图标左键取字 / 右键菜单",
        ["Toast_Started_Body"] = "按 {0} 框选取字 · 其余功能在托盘图标右键菜单",
        ["Toast_HotkeyRegisterFailed_Title"] = "热键注册失败",
        ["Toast_GrabFailed_Title"] = "取字失败",
        ["Toast_Burst_Title"] = "多帧选优…",
        ["Toast_Burst_Body"] = "画面在动：连续采样几帧、选最干净的一帧，完成后自动进剪贴板",
        ["Toast_NoText_Title"] = "未识别到文字",
        ["Toast_NoText_Body"] = "把区域框得更紧凑一些，或换一帧画面再试。",
        ["Toast_NoRegion_Title"] = "还没有可重复的区域",
        ["Toast_NoRegion_Body"] = "先按 {0} 框选一次。",
        ["Toast_OcrFailed_Title"] = "识别失败",
        ["Toast_Copied_Code"] = "已复制二维码内容",
        ["Toast_Copied_LowConfidence"] = "已复制（识别可能不准，建议核对）",
        ["Toast_Copied"] = "已复制到剪贴板",
        ["Toast_CopyFailed_Title"] = "复制失败（剪贴板被占用）",
        ["Toast_OpenLink"] = "打开链接",
        ["Toast_OpenLinkFailed_Title"] = "打不开链接",
        ["Toast_OpenLinkFailed_Body"] = "{0}（检查设置里的「打开链接的程序」）",
        ["Err_OcrFailed"] = "OCR 失败。",

        // —— 取字历史 ——
        ["Toast_History_Copied_Title"] = "已复制历史文本",
        ["Toast_History_CopyFailed_Body"] = "稍后重试即可，记录还在历史里。",
        ["Toast_History_Cleared_Title"] = "已清空取字历史",
        ["Toast_History_Cleared_Body"] = "之后的取字还会继续记录。",
        ["Toast_LanguageChanged_Title"] = "界面语言已切换",
        ["Hist_Title"] = "SnapText 取字历史",
        ["Hist_SearchHint"] = "输入关键词过滤…（留空显示全部）",
        ["Hist_ClearConfirm"] = "再点一次确认",
        ["Hist_Empty"] = "还没有取字记录。取一次字就会出现在这里。",
        ["Hist_NoMatch"] = "没有匹配的记录。",
        ["Hist_Status"] = "共 {0} 条 · 最多保留 {1} 条 · Enter 复制并关闭 · Esc 关闭",
        ["Hist_A11y_Search"] = "搜索取字历史",
        ["Hist_A11y_List"] = "取字历史列表",
        ["Hist_A11y_Preview"] = "选中记录全文",

        // —— 热键解析/注册 ——
        ["Hotkey_RegisterFailed"] = "热键 {0} 注册失败（可能已被其他程序占用）",
        ["Hotkey_Empty"] = "热键不能为空",
        ["Hotkey_MultipleMains"] = "热键 '{0}' 里有多个主按键",
        ["Hotkey_NoMain"] = "热键 '{0}' 缺少主按键",
        ["Hotkey_UnknownKey"] = "无法识别的按键 '{0}'",
        ["Hotkey_NeedModifier"] = "至少需要一个修饰键（Ctrl / Alt / Shift / Win）",

        // —— OCR ——
        ["Ocr_Auto"] = "（自动）",
        ["Ocr_AutoFallback"] = "（自动回落）",
        ["Ocr_PpLocal"] = "PP-OCRv5 本地",
        ["Ocr_Windows"] = "Windows 系统 OCR",
        ["Err_NoEngine"] = "没有可用的 OCR 引擎。",
        ["Err_Preprocess"] = "OCR 预处理失败（位图转换）。",
        ["Err_ModelMissing"] = "PP-OCR 模型缺失：{0}",
        ["WindowsOcr_Unavailable"] = "系统 OCR 不可用：请在「设置 → 时间和语言 → 语言和区域 → 中文(简体) → 语言选项」中安装“光学字符识别”。",

        // —— 框选覆盖层 ——
        ["Overlay_Hint"] = "框选要识别的文字 · 松开鼠标即复制到剪贴板 · Esc 取消",

        // —— 结果窗口 ——
        ["Result_Title"] = "SnapText 识别结果",
        ["Result_Hint"] = "识别结果（可直接编辑，改完点“复制并关闭”）",
        ["Result_CopyClose"] = "复制并关闭",
        ["Err_ClipboardWrite"] = "写入剪贴板失败，请重试。",

        // —— 设置窗口 ——
        ["Settings_Title"] = "SnapText 设置",
        ["Settings_Language"] = "界面语言",
        ["Settings_LanguageHint"] = "选择后点「保存」生效：托盘菜单、提示和窗口随即切换。",
        ["Language_Auto"] = "跟随系统",
        ["Language_Zh"] = "中文",
        ["Language_Ja"] = "日本語",
        ["Language_En"] = "English",
        ["Settings_HotkeysHint"] = "快捷键（示例：Ctrl+Alt+Q，支持 A-Z / 0-9 / F1-F12；留空 = 关闭该快捷键）",
        ["Settings_AdaptiveHint"] = "取字会自动适应画面：静止时按单帧速度识别；画面在动（播放中的视频）时自动多采样几帧、选最干净的一帧，约慢半秒。",
        ["Settings_Engine"] = "识别引擎",
        ["Engine_Auto"] = "自动（优先 PP-OCR 本地，失败回落系统 OCR）",
        ["Engine_Pp"] = "PP-OCRv5 本地（推荐：中文、视频字幕更准）",
        ["Engine_Windows"] = "Windows 系统 OCR（不装模型，纯英文场景快）",
        ["Settings_LinkOpener"] = "打开链接的程序",
        ["Settings_LinkOpenerHint"] = "留空 = 系统默认浏览器；也可以指定某个 exe（例如另一个浏览器），链接会作为参数传给它。",
        ["Settings_SilentCopy"] = "识别后静默复制到剪贴板（不弹出结果窗口）",
        ["Settings_MergeLines"] = "多行合并为一整行（适合视频字幕、整句提取）",
        ["Settings_SaveCapture"] = "同时把截图保存为 PNG（图片\\SnapText 目录）",
        ["Settings_SaveHistory"] = "保存取字历史（仅存本机，最多 200 条；托盘菜单「最近取字」可查看和清空）",
        ["Settings_AutoStart"] = "开机自动启动",
        ["Settings_InvalidGrabHotkey"] = "取字热键无效：{0}",
        ["Settings_InvalidRepeatHotkey"] = "重复上次区域热键无效：{0}",
        ["Settings_PickLinkExe"] = "选择用来打开链接的程序",
        ["Settings_ExeFilter"] = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
        ["A11y_Hotkey_Grab"] = "取字热键",
        ["A11y_Hotkey_Repeat"] = "重复上次区域热键",

        // —— 提示窗（无障碍） ——
        ["Toast_A11y_Close"] = "关闭提示",

        // —— 启动 ——
        ["App_StartupFailed"] = "SnapText 启动失败",

        // —— 命令行（CLI） ——
        ["Cli_Usage_Ocr"] = "用法: SnapText.exe --ocr <图片路径> [--engine auto|ppocr|windows] [--out <输出文本路径>] [--no-merge]",
        ["Cli_Usage_Bench"] = "用法: SnapText.exe --bench <图片路径> [--engine auto|ppocr|windows] [--n 10]",
        ["Cli_Usage_GrabRegion"] = "用法: SnapText.exe --grab-region <x,y,w,h> [--engine auto|ppocr|windows] [--out <文本.txt>] [--no-copy] [--no-merge]",
        ["Cli_Usage_OcrBurst"] = "用法: SnapText.exe --ocr-burst <图1> [图2 ...] [--engine auto|ppocr|windows] [--out <文本.txt>]",
        ["Cli_Usage_OcrAdaptive"] = "用法: SnapText.exe --ocr-adaptive <x,y,w,h> [--engine auto|ppocr|windows] [--out <文本.txt>] [--no-copy] [--no-merge]",
        ["Cli_Usage_Scan"] = "用法: SnapText.exe --scan <图片路径> [--out <内容.txt>]",
        ["Cli_Usage_Qr"] = "用法: SnapText.exe --qr <文本> [--out <二维码.png>] [--size 300]",
        ["Cli_Usage_HistorySearch"] = "用法: SnapText.exe --history search <关键词> [--n 20] [--out <文本.txt>]",
        ["Cli_Usage_HistoryAdd"] = "用法: SnapText.exe --history add <文本>",
        ["Cli_Usage_HistoryVerbs"] = "用法: SnapText.exe --history list|search|add|clear [--n N] [--out <文本.txt>]",
        ["Cli_Usage_LocVerbs"] = "用法: SnapText.exe --loc check（校验三语言文案表键集合一致）",
        ["Cli_GrabSignaled"] = "OK 已通知正在运行的 SnapText 开始取字",
        ["Cli_NoInstance"] = "FAILED: 没有正在运行的 SnapText 实例（先启动 SnapText.exe）",
        ["Cli_BadRegion"] = "FAILED: 区域格式应为 x,y,w,h（物理像素）",
        ["Cli_FileNotFound"] = "FAILED: 找不到 {0}",
        ["Cli_HistoryAdded"] = "OK 已写入取字历史",
        ["Cli_HistoryDuplicate"] = "SKIP 与最近一条相同，未重复写入",
        ["Cli_HistoryCleared"] = "OK 已清空取字历史",
        ["Cli_HistoryFile"] = "历史文件: {0}",
        ["Cli_LocMissing"] = "FAILED: 文案表缺键：{0}",
    };

    public static readonly Dictionary<string, string> Ja = new()
    {
        // —— 共通 ——
        ["Common_Close"] = "閉じる",
        ["Common_Save"] = "保存",
        ["Common_Cancel"] = "キャンセル",
        ["Common_Browse"] = "参照…",

        // —— トレイ ——
        ["Tray_Grab"] = "取得",
        ["Tray_RepeatLast"] = "前回の範囲を再取得",
        ["Tray_History"] = "最近の取得",
        ["Tray_HistorySearch"] = "履歴を検索…",
        ["Tray_HistoryClear"] = "履歴を消去",
        ["Tray_HistoryEmpty"] = "（まだ記録がありません）",
        ["Tray_Settings"] = "設定…",
        ["Tray_Exit"] = "終了",
        ["Tray_Tooltip"] = "SnapText · 画面の文字を取得",

        // —— 通知（トースト） ——
        ["Toast_EngineUnavailable_Title"] = "OCR エンジンを利用できません",
        ["Toast_EngineUnavailable_Body"] = "PP-OCR モデル（models/v5）が見つからず、Windows OCR の言語パックも利用できません。プログラムフォルダーの内容を確認してください。",
        ["Toast_Started_Title"] = "SnapText を起動しました",
        ["Toast_Started_NoHotkey"] = "ホットキーは無効です · トレイ左クリックで取得、右クリックでメニュー",
        ["Toast_Started_Body"] = "{0} を押して範囲を囲んで取得 · その他の機能はトレイ右クリックのメニューから",
        ["Toast_HotkeyRegisterFailed_Title"] = "ホットキーを登録できません",
        ["Toast_GrabFailed_Title"] = "取得に失敗しました",
        ["Toast_Burst_Title"] = "複数フレームから選別中…",
        ["Toast_Burst_Body"] = "画面が動いています: 数フレームを採取し、最もきれいな 1 枚を採用します。完了すると自動でクリップボードに入ります",
        ["Toast_NoText_Title"] = "文字を認識できませんでした",
        ["Toast_NoText_Body"] = "範囲をより狭く囲むか、別のフレームで試してください。",
        ["Toast_NoRegion_Title"] = "まだ再取得できる範囲がありません",
        ["Toast_NoRegion_Body"] = "まず {0} で一度範囲を囲んでください。",
        ["Toast_OcrFailed_Title"] = "認識に失敗しました",
        ["Toast_Copied_Code"] = "コードの内容をコピーしました",
        ["Toast_Copied_LowConfidence"] = "コピーしました（認識が不正確な可能性があります。ご確認ください）",
        ["Toast_Copied"] = "クリップボードにコピーしました",
        ["Toast_CopyFailed_Title"] = "コピーに失敗しました（クリップボードが使用中）",
        ["Toast_OpenLink"] = "リンクを開く",
        ["Toast_OpenLinkFailed_Title"] = "リンクを開けません",
        ["Toast_OpenLinkFailed_Body"] = "{0}（設定の「リンクを開くプログラム」を確認してください）",
        ["Err_OcrFailed"] = "OCR に失敗しました。",

        // —— 取得履歴 ——
        ["Toast_History_Copied_Title"] = "履歴からコピーしました",
        ["Toast_History_CopyFailed_Body"] = "しばらくして再試行してください。記録は履歴に残っています。",
        ["Toast_History_Cleared_Title"] = "取得履歴を消去しました",
        ["Toast_History_Cleared_Body"] = "以後の取得は引き続き記録されます。",
        ["Toast_LanguageChanged_Title"] = "表示言語を変更しました",
        ["Hist_Title"] = "SnapText 取得履歴",
        ["Hist_SearchHint"] = "キーワードで絞り込み…（空欄ですべて表示）",
        ["Hist_ClearConfirm"] = "もう一度クリックで確定",
        ["Hist_Empty"] = "まだ取得履歴がありません。取得するとここに表示されます。",
        ["Hist_NoMatch"] = "一致する記録がありません。",
        ["Hist_Status"] = "{0} 件 · 最大 {1} 件保持 · Enter でコピーして閉じる · Esc で閉じる",
        ["Hist_A11y_Search"] = "取得履歴を検索",
        ["Hist_A11y_List"] = "取得履歴の一覧",
        ["Hist_A11y_Preview"] = "選択した記録の全文",

        // —— ホットキー ——
        ["Hotkey_RegisterFailed"] = "ホットキー {0} を登録できません（他のソフトが使用中の可能性があります）",
        ["Hotkey_Empty"] = "ホットキーを空にはできません",
        ["Hotkey_MultipleMains"] = "ホットキー '{0}' に主要キーが複数あります",
        ["Hotkey_NoMain"] = "ホットキー '{0}' に主要キーがありません",
        ["Hotkey_UnknownKey"] = "認識できないキー '{0}'",
        ["Hotkey_NeedModifier"] = "修飾キーが少なくとも 1 つ必要です（Ctrl / Alt / Shift / Win）",

        // —— OCR ——
        ["Ocr_Auto"] = "（自動）",
        ["Ocr_AutoFallback"] = "（自動フォールバック）",
        ["Ocr_PpLocal"] = "PP-OCRv5 ローカル",
        ["Ocr_Windows"] = "Windows OCR",
        ["Err_NoEngine"] = "利用できる OCR エンジンがありません。",
        ["Err_Preprocess"] = "OCR の前処理に失敗しました（ビットマップ変換）。",
        ["Err_ModelMissing"] = "PP-OCR モデルが見つかりません: {0}",
        ["WindowsOcr_Unavailable"] = "Windows OCR を利用できません：「設定 → 時刻と言語 → 言語と地域」で「光学文字認識」をインストールしてください。",

        // —— 範囲選択オーバーレイ ——
        ["Overlay_Hint"] = "文字を囲んでください · マウスを離すとコピー · Esc でキャンセル",

        // —— 結果ウィンドウ ——
        ["Result_Title"] = "SnapText 認識結果",
        ["Result_Hint"] = "認識結果（そのまま編集できます。編集後に「コピーして閉じる」をクリック）",
        ["Result_CopyClose"] = "コピーして閉じる",
        ["Err_ClipboardWrite"] = "クリップボードへの書き込みに失敗しました。再試行してください。",

        // —— 設定ウィンドウ ——
        ["Settings_Title"] = "SnapText 設定",
        ["Settings_Language"] = "表示言語",
        ["Settings_LanguageHint"] = "「保存」で反映されます。トレイメニュー・通知・ウィンドウが切り替わります。",
        ["Language_Auto"] = "システムに合わせる",
        ["Language_Zh"] = "中文",
        ["Language_Ja"] = "日本語",
        ["Language_En"] = "English",
        ["Settings_HotkeysHint"] = "ホットキー（例: Ctrl+Alt+Q。A-Z / 0-9 / F1-F12 に対応。空欄にすると無効化）",
        ["Settings_AdaptiveHint"] = "取得は画面に自動適応します。静止画は 1 フレームで読み取り、動いている画面（再生中の動画）では数フレームから最もきれいな 1 枚を採用します（約 0.5 秒遅くなります）。",
        ["Settings_Engine"] = "OCR エンジン",
        ["Engine_Auto"] = "自動（PP-OCR ローカルを優先し、失敗時は Windows OCR にフォールバック）",
        ["Engine_Pp"] = "PP-OCRv5 ローカル（推奨: 中国語や動画字幕に強い）",
        ["Engine_Windows"] = "Windows OCR（モデル不要。英語のみの場面で高速）",
        ["Settings_LinkOpener"] = "リンクを開くプログラム",
        ["Settings_LinkOpenerHint"] = "空欄なら既定のブラウザー。任意の exe（別のブラウザーなど）を指定でき、リンクが引数として渡されます。",
        ["Settings_SilentCopy"] = "認識後にクリップボードへ静かにコピー（結果ウィンドウを出さない）",
        ["Settings_MergeLines"] = "複数行を 1 行にまとめる（動画字幕や一文の抽出に）",
        ["Settings_SaveCapture"] = "スクリーンショットも PNG で保存（ピクチャ\\SnapText）",
        ["Settings_SaveHistory"] = "取得履歴を保存（ローカルのみ・最大 200 件。トレイメニューの「最近の取得」で確認・消去できます）",
        ["Settings_AutoStart"] = "スタートアップに登録",
        ["Settings_InvalidGrabHotkey"] = "取得ホットキーが無効です: {0}",
        ["Settings_InvalidRepeatHotkey"] = "前回の範囲のホットキーが無効です: {0}",
        ["Settings_PickLinkExe"] = "リンクを開くプログラムを選択",
        ["Settings_ExeFilter"] = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
        ["A11y_Hotkey_Grab"] = "取得ホットキー",
        ["A11y_Hotkey_Repeat"] = "前回の範囲を再取得するホットキー",

        // —— トースト（アクセシビリティ） ——
        ["Toast_A11y_Close"] = "通知を閉じる",

        // —— 起動 ——
        ["App_StartupFailed"] = "SnapText の起動に失敗しました",

        // —— コマンドライン ——
        ["Cli_Usage_Ocr"] = "使い方: SnapText.exe --ocr <画像パス> [--engine auto|ppocr|windows] [--out <テキストファイル>] [--no-merge]",
        ["Cli_Usage_Bench"] = "使い方: SnapText.exe --bench <画像パス> [--engine auto|ppocr|windows] [--n 10]",
        ["Cli_Usage_GrabRegion"] = "使い方: SnapText.exe --grab-region <x,y,w,h> [--engine auto|ppocr|windows] [--out <テキスト.txt>] [--no-copy] [--no-merge]",
        ["Cli_Usage_OcrBurst"] = "使い方: SnapText.exe --ocr-burst <画像1> [画像2 ...] [--engine auto|ppocr|windows] [--out <テキスト.txt>]",
        ["Cli_Usage_OcrAdaptive"] = "使い方: SnapText.exe --ocr-adaptive <x,y,w,h> [--engine auto|ppocr|windows] [--out <テキスト.txt>] [--no-copy] [--no-merge]",
        ["Cli_Usage_Scan"] = "使い方: SnapText.exe --scan <画像パス> [--out <内容.txt>]",
        ["Cli_Usage_Qr"] = "使い方: SnapText.exe --qr <テキスト> [--out <QR.png>] [--size 300]",
        ["Cli_Usage_HistorySearch"] = "使い方: SnapText.exe --history search <キーワード> [--n 20] [--out <テキスト.txt>]",
        ["Cli_Usage_HistoryAdd"] = "使い方: SnapText.exe --history add <テキスト>",
        ["Cli_Usage_HistoryVerbs"] = "使い方: SnapText.exe --history list|search|add|clear [--n N] [--out <テキスト.txt>]",
        ["Cli_Usage_LocVerbs"] = "使い方: SnapText.exe --loc check（3 言語の文案表のキー一致を検証）",
        ["Cli_GrabSignaled"] = "OK: 起動中の SnapText に取得を依頼しました",
        ["Cli_NoInstance"] = "FAILED: 起動中の SnapText がありません（先に SnapText.exe を起動してください）",
        ["Cli_BadRegion"] = "FAILED: 範囲は x,y,w,h（物理ピクセル）で指定してください",
        ["Cli_FileNotFound"] = "FAILED: ファイルが見つかりません: {0}",
        ["Cli_HistoryAdded"] = "OK: 履歴に追加しました",
        ["Cli_HistoryDuplicate"] = "SKIP: 最新の記録と同一のため追加しませんでした",
        ["Cli_HistoryCleared"] = "OK: 履歴を消去しました",
        ["Cli_HistoryFile"] = "履歴ファイル: {0}",
        ["Cli_LocMissing"] = "FAILED: 文案表に欠けているキー: {0}",
    };

    public static readonly Dictionary<string, string> En = new()
    {
        // —— Common ——
        ["Common_Close"] = "Close",
        ["Common_Save"] = "Save",
        ["Common_Cancel"] = "Cancel",
        ["Common_Browse"] = "Browse…",

        // —— Tray ——
        ["Tray_Grab"] = "Grab",
        ["Tray_RepeatLast"] = "Repeat last region",
        ["Tray_History"] = "Recent grabs",
        ["Tray_HistorySearch"] = "Search history…",
        ["Tray_HistoryClear"] = "Clear history",
        ["Tray_HistoryEmpty"] = "(no entries yet)",
        ["Tray_Settings"] = "Settings…",
        ["Tray_Exit"] = "Exit",
        ["Tray_Tooltip"] = "SnapText · grab text on screen",

        // —— Toasts ——
        ["Toast_EngineUnavailable_Title"] = "OCR engine unavailable",
        ["Toast_EngineUnavailable_Body"] = "The PP-OCR models (models/v5) were not found, and no Windows OCR language pack is available. Check that the program folder is complete.",
        ["Toast_Started_Title"] = "SnapText is running",
        ["Toast_Started_NoHotkey"] = "Hotkeys are off · left-click the tray icon to grab, right-click for the menu",
        ["Toast_Started_Body"] = "Press {0} to grab · other features are in the tray right-click menu",
        ["Toast_HotkeyRegisterFailed_Title"] = "Could not register hotkey",
        ["Toast_GrabFailed_Title"] = "Grab failed",
        ["Toast_Burst_Title"] = "Picking the cleanest frame…",
        ["Toast_Burst_Body"] = "The picture is moving — sampling several frames and keeping the cleanest one; the result goes to the clipboard when done",
        ["Toast_NoText_Title"] = "No text recognised",
        ["Toast_NoText_Body"] = "Select a tighter area, or try another frame.",
        ["Toast_NoRegion_Title"] = "No region to repeat yet",
        ["Toast_NoRegion_Body"] = "Grab once with {0} first.",
        ["Toast_OcrFailed_Title"] = "Recognition failed",
        ["Toast_Copied_Code"] = "Code content copied",
        ["Toast_Copied_LowConfidence"] = "Copied (recognition may be off — please double-check)",
        ["Toast_Copied"] = "Copied to clipboard",
        ["Toast_CopyFailed_Title"] = "Copy failed (clipboard is busy)",
        ["Toast_OpenLink"] = "Open link",
        ["Toast_OpenLinkFailed_Title"] = "Could not open link",
        ["Toast_OpenLinkFailed_Body"] = "{0} (check \"Program for opening links\" in Settings)",
        ["Err_OcrFailed"] = "OCR failed.",

        // —— Grab history ——
        ["Toast_History_Copied_Title"] = "Copied from history",
        ["Toast_History_CopyFailed_Body"] = "Try again in a moment — the entry is still in the history.",
        ["Toast_History_Cleared_Title"] = "Grab history cleared",
        ["Toast_History_Cleared_Body"] = "New grabs will keep being recorded.",
        ["Toast_LanguageChanged_Title"] = "Interface language switched",
        ["Hist_Title"] = "SnapText grab history",
        ["Hist_SearchHint"] = "Type to filter… (empty shows all)",
        ["Hist_ClearConfirm"] = "Click again to confirm",
        ["Hist_Empty"] = "No grabs yet. They will show up here after your first grab.",
        ["Hist_NoMatch"] = "Nothing matches.",
        ["Hist_Status"] = "{0} entries · up to {1} kept · Enter copies and closes · Esc closes",
        ["Hist_A11y_Search"] = "Search grab history",
        ["Hist_A11y_List"] = "Grab history list",
        ["Hist_A11y_Preview"] = "Full text of the selected entry",

        // —— Hotkeys ——
        ["Hotkey_RegisterFailed"] = "Hotkey {0} could not be registered (another program may be using it)",
        ["Hotkey_Empty"] = "Hotkey cannot be empty",
        ["Hotkey_MultipleMains"] = "Hotkey '{0}' has more than one main key",
        ["Hotkey_NoMain"] = "Hotkey '{0}' has no main key",
        ["Hotkey_UnknownKey"] = "Unrecognised key '{0}'",
        ["Hotkey_NeedModifier"] = "At least one modifier is required (Ctrl / Alt / Shift / Win)",

        // —— OCR ——
        ["Ocr_Auto"] = " (auto)",
        ["Ocr_AutoFallback"] = " (auto, with fallback)",
        ["Ocr_PpLocal"] = "PP-OCRv5 local",
        ["Ocr_Windows"] = "Windows OCR",
        ["Err_NoEngine"] = "No OCR engine is available.",
        ["Err_Preprocess"] = "OCR preprocessing failed (bitmap conversion).",
        ["Err_ModelMissing"] = "PP-OCR model files are missing: {0}",
        ["WindowsOcr_Unavailable"] = "Windows OCR is unavailable: install the \"Optical character recognition\" language feature in Windows Settings → Time & language → Language & region.",

        // —— Selection overlay ——
        ["Overlay_Hint"] = "Drag over the text to grab · release to copy · Esc to cancel",

        // —— Result window ——
        ["Result_Title"] = "SnapText result",
        ["Result_Hint"] = "Recognised text — edit if needed, then click \"Copy and close\"",
        ["Result_CopyClose"] = "Copy and close",
        ["Err_ClipboardWrite"] = "Could not write to the clipboard. Please try again.",

        // —— Settings window ——
        ["Settings_Title"] = "SnapText Settings",
        ["Settings_Language"] = "Interface language",
        ["Settings_LanguageHint"] = "Takes effect after Save — the tray menu, notifications and windows switch over.",
        ["Language_Auto"] = "Follow system",
        ["Language_Zh"] = "中文",
        ["Language_Ja"] = "日本語",
        ["Language_En"] = "English",
        ["Settings_HotkeysHint"] = "Hotkeys (example: Ctrl+Alt+Q; A-Z / 0-9 / F1-F12; leave empty to disable)",
        ["Settings_AdaptiveHint"] = "Grab adapts to the screen: a still picture is read in single-frame time, while a moving picture (a playing video) is sampled over several frames and the cleanest one is kept — about half a second slower.",
        ["Settings_Engine"] = "OCR engine",
        ["Engine_Auto"] = "Auto (prefer local PP-OCR, fall back to Windows OCR)",
        ["Engine_Pp"] = "PP-OCRv5 local (recommended: best for Chinese and video subtitles)",
        ["Engine_Windows"] = "Windows OCR (no models; fast for English-only text)",
        ["Settings_LinkOpener"] = "Program for opening links",
        ["Settings_LinkOpenerHint"] = "Leave empty for the default browser; or pick an .exe (e.g. another browser) — the link is passed to it as an argument.",
        ["Settings_SilentCopy"] = "Copy silently after recognition (no result window)",
        ["Settings_MergeLines"] = "Join lines into one (good for subtitles and whole sentences)",
        ["Settings_SaveCapture"] = "Also save the screenshot as PNG (Pictures\\SnapText)",
        ["Settings_SaveHistory"] = "Keep a local grab history (last 200 entries; see \"Recent grabs\" in the tray menu)",
        ["Settings_AutoStart"] = "Run at login",
        ["Settings_InvalidGrabHotkey"] = "Invalid Grab hotkey: {0}",
        ["Settings_InvalidRepeatHotkey"] = "Invalid Repeat hotkey: {0}",
        ["Settings_PickLinkExe"] = "Choose the program to open links with",
        ["Settings_ExeFilter"] = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
        ["A11y_Hotkey_Grab"] = "Grab hotkey",
        ["A11y_Hotkey_Repeat"] = "Repeat last region hotkey",

        // —— Toast (accessibility) ——
        ["Toast_A11y_Close"] = "Close notification",

        // —— Startup ——
        ["App_StartupFailed"] = "SnapText failed to start",

        // —— Command line ——
        ["Cli_Usage_Ocr"] = "Usage: SnapText.exe --ocr <image path> [--engine auto|ppocr|windows] [--out <text file>] [--no-merge]",
        ["Cli_Usage_Bench"] = "Usage: SnapText.exe --bench <image path> [--engine auto|ppocr|windows] [--n 10]",
        ["Cli_Usage_GrabRegion"] = "Usage: SnapText.exe --grab-region <x,y,w,h> [--engine auto|ppocr|windows] [--out <text.txt>] [--no-copy] [--no-merge]",
        ["Cli_Usage_OcrBurst"] = "Usage: SnapText.exe --ocr-burst <image1> [image2 ...] [--engine auto|ppocr|windows] [--out <text.txt>]",
        ["Cli_Usage_OcrAdaptive"] = "Usage: SnapText.exe --ocr-adaptive <x,y,w,h> [--engine auto|ppocr|windows] [--out <text.txt>] [--no-copy] [--no-merge]",
        ["Cli_Usage_Scan"] = "Usage: SnapText.exe --scan <image path> [--out <content.txt>]",
        ["Cli_Usage_Qr"] = "Usage: SnapText.exe --qr <text> [--out <qr.png>] [--size 300]",
        ["Cli_Usage_HistorySearch"] = "Usage: SnapText.exe --history search <keyword> [--n 20] [--out <text.txt>]",
        ["Cli_Usage_HistoryAdd"] = "Usage: SnapText.exe --history add <text>",
        ["Cli_Usage_HistoryVerbs"] = "Usage: SnapText.exe --history list|search|add|clear [--n N] [--out <text.txt>]",
        ["Cli_Usage_LocVerbs"] = "Usage: SnapText.exe --loc check (verify the three language tables have identical keys)",
        ["Cli_GrabSignaled"] = "OK: asked the running SnapText to grab",
        ["Cli_NoInstance"] = "FAILED: no running SnapText instance (start SnapText.exe first)",
        ["Cli_BadRegion"] = "FAILED: region must be x,y,w,h (physical pixels)",
        ["Cli_FileNotFound"] = "FAILED: file not found: {0}",
        ["Cli_HistoryAdded"] = "OK: added to history",
        ["Cli_HistoryDuplicate"] = "SKIP: same as the newest entry, not re-added",
        ["Cli_HistoryCleared"] = "OK: history cleared",
        ["Cli_HistoryFile"] = "history file: {0}",
        ["Cli_LocMissing"] = "FAILED: missing keys in the language tables: {0}",
    };
}
