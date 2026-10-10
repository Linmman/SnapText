namespace SnapText.Settings;

internal sealed class AppSettings
{
    /// <summary>取字热键。</summary>
    public string GrabHotkey { get; set; } = "Ctrl+Alt+Q";

    /// <summary>重复上次区域热键。</summary>
    public string RepeatHotkey { get; set; } = "Ctrl+Alt+R";

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
}
