using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Controls;
using H.NotifyIcon;
using SnapText.Interop;

namespace SnapText.Tray;

/// <summary>托盘图标 + 右键菜单 + 左键单击取字。</summary>
internal sealed class TrayHost : IDisposable
{
    private static Icon? _iconKeepAlive;
    private readonly TaskbarIcon _taskbarIcon;

    public TrayHost(Action onGrab, Action onRepeatLast, Action onBurst, Action onSettings, Action onExit)
    {
        _iconKeepAlive ??= CreateTrayIcon();

        _taskbarIcon = new TaskbarIcon
        {
            Icon = _iconKeepAlive,
            ToolTipText = "SnapText · 屏幕取字"
        };

        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("取字", onGrab));
        menu.Items.Add(CreateMenuItem("重复上次区域", onRepeatLast));
        menu.Items.Add(CreateMenuItem("连拍取字", onBurst));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("设置…", onSettings));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("退出", onExit));
        _taskbarIcon.ContextMenu = menu;

        // 托盘左键单击 = 取字（和 Zshot 一致）
        _taskbarIcon.TrayLeftMouseUp += (_, _) => SafeInvoke(onGrab, "tray grab");

        _taskbarIcon.ForceCreate();
        Log.Info("Tray icon created.");
    }

    private static MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => SafeInvoke(action, header);
        return item;
    }

    private static void SafeInvoke(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error($"Tray action failed: {what}", ex);
        }
    }

    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            using var background = new SolidBrush(Color.FromArgb(255, 0, 120, 215));
            g.FillEllipse(background, 0, 0, 31, 31);

            using var font = new Font("Microsoft YaHei UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
            const string glyph = "字";
            var size = g.MeasureString(glyph, font);
            using var foreground = new SolidBrush(Color.White);
            g.DrawString(glyph, font, foreground, (32 - size.Width) / 2f, (32 - size.Height) / 2f);
        }

        // 句柄随进程存活（只创建一次，不主动销毁）
        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        try
        {
            _taskbarIcon.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error("Tray dispose failed", ex);
        }
    }
}
