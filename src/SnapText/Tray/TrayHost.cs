using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Controls;
using H.NotifyIcon;
using SnapText.Core;
using SnapText.Interop;

namespace SnapText.Tray;

/// <summary>托盘图标 + 右键菜单（含「最近取字」子菜单）+ 左键单击取字。</summary>
internal sealed class TrayHost : IDisposable
{
    /// <summary>「最近取字」子菜单最多列出多少条（最新在前）。</summary>
    private const int MaxHistoryItems = 15;

    private static Icon? _iconKeepAlive;
    private readonly TaskbarIcon _taskbarIcon;
    private readonly Func<IReadOnlyList<HistoryEntry>> _loadHistory;
    private readonly Action<HistoryEntry> _onHistoryItem;
    private readonly Action _onHistoryClear;

    public TrayHost(
        Action onGrab,
        Action onRepeatLast,
        Action onBurst,
        Action onSettings,
        Action onExit,
        Func<IReadOnlyList<HistoryEntry>> loadHistory,
        Action<HistoryEntry> onHistoryItem,
        Action onHistoryClear)
    {
        _iconKeepAlive ??= CreateTrayIcon();
        _loadHistory = loadHistory;
        _onHistoryItem = onHistoryItem;
        _onHistoryClear = onHistoryClear;

        _taskbarIcon = new TaskbarIcon
        {
            Icon = _iconKeepAlive,
            ToolTipText = "SnapText · 屏幕取字"
        };

        var historyMenu = new MenuItem { Header = "最近取字" };
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("取字", onGrab));
        menu.Items.Add(CreateMenuItem("重复上次区域", onRepeatLast));
        menu.Items.Add(CreateMenuItem("连拍取字", onBurst));
        menu.Items.Add(historyMenu);
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("设置…", onSettings));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("退出", onExit));
        _taskbarIcon.ContextMenu = menu;

        // 每次打开菜单时刷新「最近取字」（读取失败也不影响菜单打开）
        menu.Opened += (_, _) => PopulateHistoryMenu(historyMenu);

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

    /// <summary>填充「最近取字」子菜单：最新在前、点一条即复制；尾部固定「清空历史」。</summary>
    private void PopulateHistoryMenu(MenuItem historyMenu)
    {
        historyMenu.Items.Clear();

        IReadOnlyList<HistoryEntry> entries;
        try
        {
            entries = _loadHistory();
        }
        catch (Exception ex)
        {
            Log.Error("History menu load failed", ex);
            entries = Array.Empty<HistoryEntry>();
        }

        if (entries.Count == 0)
        {
            historyMenu.Items.Add(new MenuItem { Header = "（还没有记录）", IsEnabled = false });
        }
        else
        {
            foreach (var entry in entries.Take(MaxHistoryItems))
            {
                var captured = entry;
                var item = new MenuItem
                {
                    Header = $"{captured.Time:MM-dd HH:mm}  {HistoryStore.ToSingleLine(captured.Text, 36)}"
                };
                item.Click += (_, _) => SafeInvoke(() => _onHistoryItem(captured), "history item");
                historyMenu.Items.Add(item);
            }
        }

        historyMenu.Items.Add(new Separator());
        var clear = new MenuItem { Header = "清空历史" };
        clear.Click += (_, _) => SafeInvoke(_onHistoryClear, "history clear");
        historyMenu.Items.Add(clear);
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
