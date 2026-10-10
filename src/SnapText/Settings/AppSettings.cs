namespace SnapText.Settings;

internal sealed class AppSettings
{
    /// <summary>取字热键（留空 = 关闭该热键）。框选后自动对选区做多帧采样：画面静止只识别一帧，在动则多帧选优。</summary>
    public string GrabHotkey { get; set; } = "Ctrl+Alt+Q";

    /// <summary>重复上次区域热键（留空 = 关闭该热键）。</summary>
    public string RepeatHotkey { get; set; } = "Ctrl+Alt+R";

    /// <summary>打开链接用的程序（留空 = 系统默认浏览器）。填 exe 路径，链接会作为参数传入。</summary>
    public string LinkOpener { get; set; } = "";

    /// <summary>OCR 引擎：auto | windows | ppocr（ppocr 待接入）。</summary>
    public string Engine { get; set; } = "auto";

    /// <summary>识别后静默复制到剪贴板（false 时弹出可编辑结果窗口）。</summary>
    public bool SilentCopy { get; set; } = true;

    /// <summary>多行合并为一整行（适合视频字幕、整句提取）。</summary>
    public bool MergeLines { get; set; } = true;

    /// <summary>开机自动启动。</summary>
    public bool AutoStart { get; set; }

    /// <summary>同时保存截图 PNG 到「图片\SnapText」。</summary>
    public bool SaveCapture { get; set; }

    /// <summary>记录取字历史（仅存本机 %LOCALAPPDATA%\SnapText\history.jsonl，最多 200 条）。</summary>
    public bool SaveHistory { get; set; } = true;

    /// <summary>界面语言：auto（跟随系统，缺省）| zh | ja | en。</summary>
    public string Language { get; set; } = "auto";
}
