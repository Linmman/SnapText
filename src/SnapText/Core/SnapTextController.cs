using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using SnapText.Capture;
using SnapText.Hotkeys;
using SnapText.Interop;
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

    private TrayHost? _tray;
    private OverlayManager? _overlay;
    private ScreenCapture? _pendingCapture;
    private IntPtr _lastForeground = IntPtr.Zero;
    private Rectangle _lastRect;
    private bool _hasLastRect;
    private EventWaitHandle? _grabSignal;
    private Thread? _signalThread;
    private volatile bool _disposed;

    public SnapTextController()
    {
        _settings = SettingsStore.Load();
        _ocr = new OcrService(_settings);
    }

    public void Start()
    {
        _tray = new TrayHost(Grab, RepeatLast, ShowSettings, Exit);
        ApplySettings();
        StartSignalListener();

        Log.Info($"SnapText started. Engine={_ocr.Name}, available={_ocr.IsAvailable}");
        if (!_ocr.IsAvailable)
        {
            ToastService.Show("OCR 引擎不可用",
                "未找到 PP-OCR 模型（models/v5），系统 OCR 也没有可用语言包。请检查程序目录完整性。");
        }
        else
        {
            ToastService.Show("SnapText 已启动",
                $"按 {_settings.GrabHotkey} 框选取字 · {_settings.RepeatHotkey} 重复上次区域 · 托盘图标右键可设置/退出");
        }
    }

    private void ApplySettings()
    {
        _hotkeys.UnregisterAll();
        _ocr.UpdateSettings(_settings);

        if (!_hotkeys.Register(_settings.GrabHotkey, Grab, out var error))
            ToastService.Show("热键注册失败", error);
        if (!_hotkeys.Register(_settings.RepeatHotkey, RepeatLast, out error))
            ToastService.Show("热键注册失败", error);

        AutoStart.Apply(_settings.AutoStart);
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
            ToastService.Show("取字失败", ex.Message);
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
            overlay.Dispose();
            return;
        }

        Bitmap? crop = null;
        try
        {
            crop = capture.Crop(rect.Value);
        }
        catch (Exception ex)
        {
            Log.Error("Crop failed", ex);
        }

        overlay.Dispose(); // 释放冻结帧

        if (crop is null) return;

        _lastRect = rect.Value;
        _hasLastRect = true;

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
                ToastService.Show("还没有可重复的区域", $"先按 {_settings.GrabHotkey} 框选一次。");
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

            // 二维码扫描与 OCR 并行（不额外增加延迟）；框选到码时以码的内容为准
            var codeTask = Task.Run(() => CodeScanner.TryDecode(image));
            OcrOutcome? outcome = null;
            Exception? ocrError = null;
            try
            {
                outcome = await _ocr.RecognizeAsync(image);
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
                throw ocrError ?? new InvalidOperationException("OCR 失败。");

            var text = UrlHelper.Normalize(PostProcessor.Clean(outcome.Text, _settings.MergeLines));
            var lowConfidence = outcome.Confidence is { } confidence && confidence < LowConfidenceThreshold;

            Log.Info($"OCR done in {elapsed} ms, {text.Length} chars, conf={outcome.Confidence?.ToString("F3") ?? "n/a"}.");

            if (_settings.SaveCapture)
                SaveCapture(image, text);

            if (text.Length == 0)
            {
                ToastService.Show("未识别到文字", "把区域框得更紧凑一些，或换一帧画面再试。", rect);
                return;
            }

            ShowResult(text, rect, isCode: false, lowConfidence: lowConfidence);
        }
        catch (Exception ex)
        {
            Log.Error("OCR pipeline failed", ex);
            ToastService.Show("识别失败", ex.Message, rect);
        }
        finally
        {
            image.Dispose();
        }
    }

    /// <summary>统一出口：静默复制 + 轻提示（识别到链接时附「打开链接」）；非静默模式弹可编辑结果窗。</summary>
    private void ShowResult(string text, Rectangle rect, bool isCode, bool lowConfidence = false)
    {
        if (!_settings.SilentCopy)
        {
            var window = new ResultWindow(text);
            window.Show();
            window.Activate();
            return;
        }

        var url = UrlHelper.FindFirst(text);
        var copied = ClipboardService.TrySetText(text);

        var title = copied
            ? isCode
                ? "已复制二维码内容"
                : lowConfidence ? "已复制（识别可能不准，建议核对）" : "已复制到剪贴板"
            : "复制失败（剪贴板被占用）";

        ToastService.Show(
            title,
            Preview(text),
            rect,
            actionLabel: url is null ? null : "打开链接",
            action: url is null ? null : () => OpenUrl(url));
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            Log.Info("Opened url: " + url);
        }
        catch (Exception ex)
        {
            Log.Error("Open url failed: " + url, ex);
            ToastService.Show("打不开链接", url);
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
