using System.Drawing;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SnapText.Capture;
using SnapText.Interop;

namespace SnapText.Ui;

/// <summary>右下角非打扰轻提示（不抢焦点，可点击关闭）。</summary>
internal partial class ToastWindow : Window
{
    private readonly Rectangle? _anchor;
    private readonly Action? _action;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(2600) };
    private bool _closing;

    public ToastWindow(string title, string body, Rectangle? anchor, string? actionLabel = null, Action? action = null)
    {
        InitializeComponent();
        TitleText.Text = title;
        BodyText.Text = body;
        _anchor = anchor;
        _action = action;

        if (actionLabel is not null && action is not null)
        {
            ActionText.Text = actionLabel;
            AutomationProperties.SetName(ActionArea, actionLabel);
            ActionArea.Visibility = Visibility.Visible;
        }

        _timer.Tick += (_, _) => FadeOutAndClose();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var exStyle = Win32.GetWindowLongPtr(handle, Win32.GWL_EXSTYLE).ToInt64();
        Win32.SetWindowLongPtr(
            handle,
            Win32.GWL_EXSTYLE,
            new IntPtr(exStyle | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOOLWINDOW));
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        PositionAtBottomRight();
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
        _timer.Start();
    }

    private void PositionAtBottomRight()
    {
        var monitors = MonitorEnumerator.GetAll();
        if (monitors.Count == 0) return;

        MonitorEntry? target = null;
        if (_anchor is { } rect)
        {
            var center = new System.Drawing.Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            target = monitors.FirstOrDefault(m => m.Bounds.Contains(center));
        }
        target ??= MonitorEnumerator.GetAtCursor(monitors) ?? monitors[0];

        var handle = new WindowInteropHelper(this).Handle;
        var dpi = Win32.GetDpiForWindow(handle);
        var scale = dpi <= 0 ? 1.0 : dpi / 96.0;

        var width = (int)Math.Ceiling(ActualWidth * scale);
        var height = (int)Math.Ceiling(ActualHeight * scale);
        var work = target.WorkArea;

        Win32.SetWindowPos(
            handle,
            Win32.HWND_TOPMOST,
            work.Right - width - 18,
            work.Bottom - height - 18,
            width,
            height,
            Win32.SWP_NOACTIVATE | Win32.SWP_NOSIZE);
    }

    private void FadeOutAndClose()
    {
        if (_closing) return;
        _closing = true;
        _timer.Stop();

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => FadeOutAndClose();

    /// <summary>点「打开链接」：先关提示，再执行动作（Handled 防止冒泡触发"点击关闭"）。</summary>
    private void OnActionClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var action = _action;
        FadeOutAndClose();
        action?.Invoke();
    }
}
