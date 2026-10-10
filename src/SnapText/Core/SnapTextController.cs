using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using SnapText.Capture;
using SnapText.Hotkeys;
using SnapText.Interop;
using SnapText.Localization;
using SnapText.Ocr;
using SnapText.Overlay;
using SnapText.Processing;
using SnapText.Settings;
using SnapText.Tray;
using SnapText.Ui;

namespace SnapText.Core;

/// <summary>应用主控制器：托盘 + 热键 + 框选 + OCR + 剪贴板 + 提示。</summary>
internal sealed class SnapTextController : IDisposable
{
    /// <summary>
    /// 低于该逐字平均置信度时，在提示里提醒"可能不准"（只改提示文案，不弹窗、不打扰）。
    /// 实测校准（2026-10-10）：干净样张 conf=0.980，轻微降质仍 ≥0.948，只有识别严重不确定时才会明显下降；
    /// 取 0.85 作为兜底网，正常使用几乎不会触发。
    /// </summary>
    private const double LowConfidenceThreshold = 0.85;

    private readonly AppSettings _settings;
    private readonly HotkeyService _hotkeys = new();
    private readonly OcrService _ocr;
    private readonly HistoryStore _history = new();

    private TrayHost? _tray;
    private OverlayManager? _overlay;
    private HistoryWindow? _historyWindow;
    private ScreenCapture? _pendingCapture;
    private IntPtr _lastForeground = IntPtr.Zero;
    private Rectangle _lastRect;
    private bool _hasLastRect;
    private EventWaitHandle? _grabSignal;
    private Thread? _signalThread;
    private volatile bool _disposed;
    private string? _appliedLanguage;

    public SnapTextController()
    {
        _settings = SettingsStore.Load();
        _ocr = new OcrService(_settings);
    }

    public void Start()
    {
        Loc.Initialize(_settings.Language);
        Log.Info($"UI language: {Loc.Language} (setting={_settings.Language})");
        _tray = new TrayHost(Grab, RepeatLast, ShowSettings, Exit,
            loadHistory: () => _history.Load(),
            onHistoryItem: CopyHistoryEntry,
            onHistorySearch: ShowHistory,
            onHistoryClear: ClearHistory);
        ApplySettings();
        StartSignalListener();

        Log.Info($"SnapText started. Engine={_ocr.Name}, available={_ocr.IsAvailable}");
        if (!_ocr.IsAvailable)
        {
            ToastService.Show(Loc.T("Toast_EngineUnavailable_Title"), Loc.T("Toast_EngineUnavailable_Body"));
        }
        else
        {
            var body = HotkeyService.IsDisabled(_settings.GrabHotkey)
                ? Loc.T("Toast_Started_NoHotkey")
                : Loc.T("Toast_Started_Body", _settings.GrabHotkey);
            ToastService.Show(Loc.T("Toast_Started_Title"), body);
        }
    }

    private void ApplySettings()
    {
        var languageChanged = _appliedLanguage is not null &&
                              !string.Equals(_appliedLanguage, _settings.Language, StringComparison.OrdinalIgnoreCase);

        Loc.Initialize(_settings.Language);   // 语言可能在设置里改过：先应用，后续提示/菜单才会用新语言
        _appliedLanguage = _settings.Language;

        _hotkeys.UnregisterAll();
        _ocr.UpdateSettings(_settings);
        _tray?.ApplyLanguage();

        if (!_hotkeys.Register(_settings.GrabHotkey, Grab, out var error))
            ToastService.Show(Loc.T("Toast_HotkeyRegisterFailed_Title"), error);
        if (!_hotkeys.Register(_settings.RepeatHotkey, RepeatLast, out error))
            ToastService.Show(Loc.T("Toast_HotkeyRegisterFailed_Title"), error);

        AutoStart.Apply(_settings.AutoStart);

        if (languageChanged)
        {
            var nameKey = Loc.Language switch
            {
                "ja" => "Language_Ja",
                "en" => "Language_En",
                _ => "Language_Zh"
            };
            ToastService.Show(Loc.T("Toast_LanguageChanged_Title"), Loc.T(nameKey));
        }
    }

    /// <summary>监听 IPC 信号：SnapText.exe --grab 可让已运行的实例开始取字（供脚本/AutoHotkey 触发）。</summary>
    private void StartSignalListener()
    {
        try
        {
            _grabSignal = IpcSignals.CreateGrabSignal();
            _signalThread = new Thread(SignalLoop) { IsBackground = true, Name = "SnapText.IpcSignals" };
            _signalThread.Start();
            Log.Info("IPC grab signal listener started.");
        }
        catch (Exception ex)
        {
            Log.Error("IPC signal init failed", ex);
        }
    }

    private void SignalLoop()
    {
        while (!_disposed)
        {
            try
            {
                if (_grabSignal is not null && _grabSignal.WaitOne(TimeSpan.FromSeconds(1)))
                {
                    Log.Info("Grab signal received via IPC.");
                    Application.Current?.Dispatcher.Invoke(Grab);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Signal loop error", ex);
                Thread.Sleep(500);
            }
        }
    }

    /// <summary>热键：开始框选取字。</summary>
    private void Grab()
    {
        try
        {
            if (_overlay is not null)
            {
                Log.Info("Grab pressed while overlay open; cancelling.");
                _overlay.Cancel();
                return;
            }

            _lastForeground = Win32.GetForegroundWindow();
            _pendingCapture = ScreenCapture.CaptureAll();

            var overlay = new OverlayManager(_pendingCapture);
            overlay.Completed += OnSelectionCompleted;
            _overlay = overlay;
            overlay.Start();
        }
        catch (Exception ex)
        {
            Log.Error("Grab failed", ex);
            CleanupPending();
            ToastService.Show(Loc.T("Toast_GrabFailed_Title"), ex.Message);
        }
    }

    private void OnSelectionCompleted(Rectangle? rect)
    {
        var overlay = _overlay;
        _overlay = null;

        var capture = _pendingCapture;
        _pendingCapture = null;

        if (overlay is null) return;

        if (rect is null || capture is null)
        {
            overlay.Dispose();   // 用户取消了框选
            return;
        }

        _lastRect = rect.Value;
        _hasLastRect = true;

        Bitmap? crop = null;
        try
        {
            crop = capture.Crop(rect.Value);   // 冻结帧用于扫码与可选截图保存；文字识别走自适应多帧采样
        }
        catch (Exception ex)
        {
            Log.Error("Crop failed", ex);
        }

        overlay.Dispose(); // 释放冻结帧

        if (crop is null) return;

        _ = ProcessAsync(crop, rect.Value, restoreFocus: true);
    }

    /// <summary>热键：不框选，直接对上次区域重新取字（视频字幕场景）。</summary>
    private void RepeatLast()
    {
        try
        {
            if (_overlay is not null) return;

            if (!_hasLastRect)
            {
                ToastService.Show(Loc.T("Toast_NoRegion_Title"), Loc.T("Toast_NoRegion_Body", _settings.GrabHotkey));
                return;
            }

            var crop = ScreenCapture.CaptureRegion(_lastRect);
            _ = ProcessAsync(crop, _lastRect, restoreFocus: false);
        }
        catch (Exception ex)
        {
            Log.Error("RepeatLast failed", ex);
        }
    }

    private async Task ProcessAsync(Bitmap image, Rectangle rect, bool restoreFocus)
    {
        try
        {
            var started = Environment.TickCount64;

            // 二维码扫描与文字识别并行（不额外增加延迟）；框选到码时以码的内容为准。
            // 扫码用克隆位图：GDI+ 位图不是线程安全的，两条路径同时读同一张位图会抛
            // "Object is currently in use elsewhere"——曾导致 PP-OCR 偶发失败后静默回落到系统 OCR、或二维码偶发识别不到。
            using var codeImage = (Bitmap)image.Clone();
            var codeTask = Task.Run(() => CodeScanner.TryDecode(codeImage));

            // 文字识别走自适应采样：画面静止只识别一帧（≈单帧速度），画面在动（播放中的视频）才多帧选优
            BurstOutcome? outcome = null;
            Exception? ocrError = null;
            try
            {
                outcome = await BurstRunner.RunAdaptiveAsync(rect, _ocr,
                    onMultiFrame: () => ToastService.Show(Loc.T("Toast_Burst_Title"), Loc.T("Toast_Burst_Body"), rect));
            }
            catch (Exception ex)
            {
                ocrError = ex;
                Log.Error("OCR engine failed", ex);
            }

            var codeContent = await codeTask;
            var elapsed = Environment.TickCount64 - started;

            if (restoreFocus)
                RestoreFocus();

            if (!string.IsNullOrWhiteSpace(codeContent))
            {
                Log.Info($"Barcode decoded in {elapsed} ms, {codeContent.Length} chars.");
                if (_settings.SaveCapture)
                    SaveCapture(image, codeContent);
                ShowResult(codeContent, rect, isCode: true);
                return;
            }

            if (outcome is null)
                throw ocrError ?? new InvalidOperationException(Loc.T("Err_OcrFailed"));

            var text = UrlHelper.Normalize(PostProcessor.Clean(outcome.Text, _settings.MergeLines));
            var lowConfidence = outcome.Confidence is { } confidence && confidence < LowConfidenceThreshold;

            Log.Info(
                $"OCR done in {elapsed} ms, {text.Length} chars, conf={outcome.Confidence?.ToString("F3") ?? "n/a"}, " +
                $"frames={outcome.InputFrames}/ocr={outcome.OcrFrames}.");

            if (_settings.SaveCapture)
                SaveCapture(image, text);

            if (text.Length == 0)
            {
                ToastService.Show(Loc.T("Toast_NoText_Title"), Loc.T("Toast_NoText_Body"), rect);
                return;
            }

            ShowResult(text, rect, isCode: false, lowConfidence: lowConfidence);
        }
        catch (Exception ex)
        {
            Log.Error("OCR pipeline failed", ex);
            ToastService.Show(Loc.T("Toast_OcrFailed_Title"), ex.Message, rect);
        }
        finally
        {
            image.Dispose();
        }
    }

    /// <summary>统一出口：静默复制 + 轻提示（识别到链接时附「打开链接」）；非静默模式弹可编辑结果窗。</summary>
    private void ShowResult(string text, Rectangle rect, bool isCode, bool lowConfidence = false, string? source = null)
    {
        var sourceName = source ?? (isCode ? "code" : "ocr");

        if (!_settings.SilentCopy)
        {
            // 非静默模式：用户可能先编辑再复制，所以等真正复制时再记入历史
            var window = new ResultWindow(text, onCopied: copied => RecordHistory(copied, sourceName));
            window.Show();
            window.Activate();
            return;
        }

        RecordHistory(text, sourceName);

        var url = UrlHelper.FindFirst(text);
        var copied = ClipboardService.TrySetText(text);

        var title = copied
            ? isCode
                ? Loc.T("Toast_Copied_Code")
                : lowConfidence ? Loc.T("Toast_Copied_LowConfidence") : Loc.T("Toast_Copied")
            : Loc.T("Toast_CopyFailed_Title");

        ToastService.Show(
            title,
            Preview(text),
            rect,
            actionLabel: url is null ? null : Loc.T("Toast_OpenLink"),
            action: url is null ? null : () => OpenUrl(url));
    }

    /// <summary>打开链接：默认交给系统默认浏览器（http/https 关联程序）；设置里指定了程序则用指定程序。</summary>
    private void OpenUrl(string url)
    {
        try
        {
            var opener = _settings.LinkOpener;
            if (!string.IsNullOrWhiteSpace(opener))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = opener,
                    Arguments = $"\"{url}\"",
                    UseShellExecute = true
                });
                Log.Info($"Opened url with custom opener ({opener}): {url}");
            }
            else
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                Log.Info("Opened url with default browser: " + url);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Open url failed: " + url, ex);
            ToastService.Show(Loc.T("Toast_OpenLinkFailed_Title"), Loc.T("Toast_OpenLinkFailed_Body", url));
        }
    }

    /// <summary>把焦点还给取字前的窗口，用户直接 Ctrl+V 即可粘贴。</summary>
    private void RestoreFocus()
    {
        try
        {
            if (_lastForeground == IntPtr.Zero || !Win32.IsWindow(_lastForeground)) return;

            var foreground = Win32.GetForegroundWindow();
            if (foreground != IntPtr.Zero)
            {
                Win32.GetWindowThreadProcessId(foreground, out var pid);
                // 焦点已经不在我们进程上（用户切走了）就不要抢回来
                if (pid != (uint)Environment.ProcessId) return;
            }

            Win32.SetForegroundWindow(_lastForeground);
            Log.Info("Focus restored.");
        }
        catch (Exception ex)
        {
            Log.Warn("RestoreFocus failed: " + ex.Message);
        }
    }

    private void SaveCapture(Bitmap image, string text)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SnapText");
            Directory.CreateDirectory(dir);

            var file = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd-HHmmss}.png");
            image.Save(file, ImageFormat.Png);
            Log.Info("Capture saved: " + file);
        }
        catch (Exception ex)
        {
            Log.Error("SaveCapture failed", ex);
        }
    }

    private void ShowSettings()
    {
        try
        {
            var window = new SettingsWindow(_settings, ApplySettings);
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            Log.Error("ShowSettings failed", ex);
        }
    }

    /// <summary>托盘「最近取字 → 搜索历史…」：按需打开历史面板（只在用户点击时出现，永不自动弹出）。</summary>
    private void ShowHistory()
    {
        try
        {
            if (_historyWindow is { } existing)
            {
                existing.Activate();
                return;
            }

            var returnTo = Win32.GetForegroundWindow();   // 复制完成后把焦点还给打开面板之前的窗口
            var window = new HistoryWindow(_history, returnTo);
            _historyWindow = window;
            window.Closed += (_, _) => _historyWindow = null;
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            Log.Error("ShowHistory failed", ex);
        }
    }

    /// <summary>托盘「最近取字」：把某条历史复制回剪贴板（复制旧记录不改变历史顺序）。</summary>
    private void CopyHistoryEntry(HistoryEntry entry)
    {
        if (!ClipboardService.TrySetText(entry.Text))
        {
            ToastService.Show(Loc.T("Toast_CopyFailed_Title"), Loc.T("Toast_History_CopyFailed_Body"));
            return;
        }

        Log.Info($"History: copied entry ({entry.Text.Length} chars).");
        ToastService.Show(Loc.T("Toast_History_Copied_Title"), HistoryStore.ToSingleLine(entry.Text));
    }

    /// <summary>托盘「最近取字 → 清空历史」。</summary>
    private void ClearHistory()
    {
        _history.Clear();
        ToastService.Show(Loc.T("Toast_History_Cleared_Title"), Loc.T("Toast_History_Cleared_Body"));
    }

    /// <summary>把最终交付的文本记入取字历史（设置里可关；失败只记日志，不打扰主流程）。</summary>
    private void RecordHistory(string text, string source)
    {
        try
        {
            if (!_settings.SaveHistory) return;
            if (_history.Add(text, source))
                Log.Info($"History recorded: {text.Length} chars (source={source}).");
        }
        catch (Exception ex)
        {
            Log.Error("History record failed", ex);
        }
    }

    private void Exit()
    {
        Log.Info("Exit requested from tray.");
        Dispose();
        Application.Current.Shutdown();
    }

    private void CleanupPending()
    {
        _pendingCapture?.Dispose();
        _pendingCapture = null;
        _overlay?.Dispose();
        _overlay = null;
    }

    private static string Preview(string text)
    {
        var single = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return single.Length <= 80 ? single : single[..80] + "…";
    }

    public void Dispose()
    {
        _disposed = true;
        try { _grabSignal?.Dispose(); } catch { }
        try { _hotkeys.Dispose(); } catch { }
        try { _ocr.Dispose(); } catch { }
        _tray?.Dispose();
        CleanupPending();
    }
}
