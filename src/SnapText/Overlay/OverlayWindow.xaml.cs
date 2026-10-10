using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapText.Capture;
using SnapText.Interop;

namespace SnapText.Overlay;

/// <summary>单台显示器上的冻结帧覆盖层；按鼠标物理坐标做框选。</summary>
internal partial class OverlayWindow : Window
{
    private readonly MonitorEntry _monitor;
    private readonly SelectionState _state;
    private readonly double _scale;

    private bool _isOwner;
    private bool _dragging;
    private Win32.POINT _dragStart;

    public event Action<Rectangle>? Committed;
    public event Action? Cancelled;

    public OverlayWindow(MonitorEntry monitor, Bitmap frozenCrop, SelectionState state)
    {
        InitializeComponent();

        _monitor = monitor;
        _state = state;
        _scale = monitor.Scale <= 0 ? 1.0 : monitor.Scale;

        var source = ToImageSource(frozenCrop);
        FrozenImage.Source = source;
        SelectionImage.Source = source;

        _state.Changed += RenderSelection;
        Closed += (_, _) => _state.Changed -= RenderSelection;
    }

    public MonitorEntry Monitor => _monitor;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        // 用物理像素精确铺满这台显示器：混合 DPI 下最可靠的方式
        Win32.SetWindowPos(
            handle,
            Win32.HWND_TOPMOST,
            _monitor.Bounds.Left,
            _monitor.Bounds.Top,
            _monitor.Bounds.Width,
            _monitor.Bounds.Height,
            Win32.SWP_SHOWWINDOW);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (_isOwner) return;

        _isOwner = true;
        _dragging = true;
        HintLabel.Visibility = Visibility.Collapsed;
        CaptureMouse();

        Win32.GetCursorPos(out _dragStart);
        _state.Update(new Rectangle(_dragStart.X, _dragStart.Y, 0, 0));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!Win32.GetCursorPos(out var cursor)) return;

        if (_isOwner)
        {
            UpdateGuides(cursor);
            if (_dragging)
                _state.Update(MakeRect(_dragStart, cursor));
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_dragging || !_isOwner) return;

        _dragging = false;
        ReleaseMouseCapture();

        var rect = _state.Rect;
        if (rect.Width < 8 || rect.Height < 8)
        {
            Log.Info($"Selection too small ({rect.Width}x{rect.Height}); cancelled.");
            Cancelled?.Invoke();
        }
        else
        {
            Log.Info($"Selection committed: {rect}");
            Committed?.Invoke(rect);
        }
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        Log.Info("Selection cancelled by right click.");
        Cancelled?.Invoke();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Log.Info("Selection cancelled by Esc.");
            Cancelled?.Invoke();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private static Rectangle MakeRect(Win32.POINT a, Win32.POINT b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        var w = Math.Abs(a.X - b.X);
        var h = Math.Abs(a.Y - b.Y);
        return new Rectangle(x, y, w, h);
    }

    private void UpdateGuides(Win32.POINT cursorPhysical)
    {
        var x = (cursorPhysical.X - _monitor.Bounds.Left) / _scale;
        var y = (cursorPhysical.Y - _monitor.Bounds.Top) / _scale;

        GuideH.Visibility = Visibility.Visible;
        GuideV.Visibility = Visibility.Visible;

        GuideH.X1 = 0;
        GuideH.X2 = RootGrid.ActualWidth;
        GuideH.Y1 = y;
        GuideH.Y2 = y;

        GuideV.Y1 = 0;
        GuideV.Y2 = RootGrid.ActualHeight;
        GuideV.X1 = x;
        GuideV.X2 = x;
    }

    private void RenderSelection()
    {
        var rect = _state.Rect;
        var localX = (rect.X - _monitor.Bounds.Left) / _scale;
        var localY = (rect.Y - _monitor.Bounds.Top) / _scale;
        var localW = rect.Width / _scale;
        var localH = rect.Height / _scale;

        if (!_state.HasSelection
            || localX > RootGrid.ActualWidth
            || localY > RootGrid.ActualHeight
            || localX + localW < 0
            || localY + localH < 0)
        {
            SelectionImage.Visibility = Visibility.Collapsed;
            SelectionBorder.Visibility = Visibility.Collapsed;
            SizeLabel.Visibility = Visibility.Collapsed;
            return;
        }

        SelectionImage.Visibility = Visibility.Visible;
        SelectionImage.Clip = new RectangleGeometry(new Rect(localX, localY, Math.Max(0, localW), Math.Max(0, localH)));

        SelectionBorder.Visibility = Visibility.Visible;
        SelectionBorder.Width = Math.Max(0, localW);
        SelectionBorder.Height = Math.Max(0, localH);
        SelectionBorder.Margin = new Thickness(localX, localY, 0, 0);

        SizeLabel.Visibility = Visibility.Visible;
        SizeText.Text = $"{rect.Width} × {rect.Height}";

        var labelX = Math.Clamp(localX, 0, Math.Max(0, RootGrid.ActualWidth - 110));
        var labelY = localY - 32;
        if (labelY < 4) labelY = localY + localH + 8;
        SizeLabel.Margin = new Thickness(labelX, labelY, 0, 0);
    }

    private static ImageSource ToImageSource(Bitmap bitmap)
    {
        using var memory = new MemoryStream();
        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
        memory.Seek(0, SeekOrigin.Begin);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = memory;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
