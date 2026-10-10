using System.Drawing;
using SnapText.Capture;

namespace SnapText.Overlay;

/// <summary>每台显示器一个覆盖层窗口，共享同一份框选状态。</summary>
internal sealed class OverlayManager : IDisposable
{
    private readonly ScreenCapture _capture;
    private readonly SelectionState _state = new();
    private readonly List<OverlayWindow> _windows = new();
    private bool _finished;

    public OverlayManager(ScreenCapture capture) => _capture = capture;

    /// <summary>框选完成（rect 非空）或取消（rect 为空）。</summary>
    public event Action<Rectangle?>? Completed;

    public ScreenCapture Capture => _capture;

    public void Start()
    {
        var monitors = MonitorEnumerator.GetAll();
        if (monitors.Count == 0)
        {
            Finish(null);
            return;
        }

        var cursorMonitor = MonitorEnumerator.GetAtCursor(monitors);

        foreach (var monitor in monitors)
        {
            var frozenCrop = _capture.Crop(monitor.Bounds);
            var window = new OverlayWindow(monitor, frozenCrop, _state);
            window.Committed += rect => Finish(rect);
            window.Cancelled += () => Finish(null);
            window.Closed += (_, _) => { if (!_finished) Finish(null); };
            _windows.Add(window);
        }

        foreach (var window in _windows)
        {
            window.ShowActivated = cursorMonitor is not null && window.Monitor.Handle == cursorMonitor.Handle;
            window.Show();
        }

        var active = _windows.FirstOrDefault(w => cursorMonitor is not null && w.Monitor.Handle == cursorMonitor.Handle)
                     ?? _windows[0];
        active.Activate();
        active.Focus();

        Log.Info($"Overlay started: {_windows.Count} monitor(s), active on {active.Monitor.Bounds}.");
    }

    public void Cancel() => Finish(null);

    private void Finish(Rectangle? rect)
    {
        if (_finished) return;
        _finished = true;

        foreach (var window in _windows.ToList())
        {
            try { window.Close(); } catch { }
        }
        _windows.Clear();

        Completed?.Invoke(rect);
    }

    public void Dispose()
    {
        _finished = true;

        foreach (var window in _windows.ToList())
        {
            try { window.Close(); } catch { }
        }
        _windows.Clear();
        _capture.Dispose();
    }
}
