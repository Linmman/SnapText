using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Controls;
using H.NotifyIcon;
using SnapText.Core;
using SnapText.Interop;
using SnapText.Localization;

namespace SnapText.Tray;

/// <summary>托盘图标 + 右键菜单（含「最近取字」子菜单）+ 左键单击取字。菜单文案随界面语言切换。</summary>
internal sealed class TrayHost : IDisposable
{
    /// <summary>「最近取字」子菜单最多列出多少条（最新在前）。</summary>
    private const int MaxHistoryItems = 15;

    private static Icon? _iconKeepAlive;

    private readonly TaskbarIcon _taskbarIcon;
    private readonly Func<IReadOnlyList<HistoryEntry>> _loadHistory;
    private readonly Action<HistoryEntry> _onHistoryItem;
    private readonly Action _onHistorySearch;
    private readonly Action _onHistoryClear;

    private readonly MenuItem _grabItem = new();
    private readonly MenuItem _repeatItem = new();
    private readonly MenuItem _historyItem = new();
    private readonly MenuItem _settingsItem = new();
    private readonly MenuItem _exitItem = new();

    public TrayHost(
        Action onGrab,
        Action onRepeatLast,
        Action onSettings,
        Action onExit,
        Func<IReadOnlyList<HistoryEntry>> loadHistory,
        Action<HistoryEntry> onHistoryItem,
        Action onHistorySearch,
        Action onHistoryClear)
    {
        _iconKeepAlive ??= CreateTrayIcon();
        _loadHistory = loadHistory;
        _onHistoryItem = onHistoryItem;
        _onHistorySearch = onHistorySearch;
        _onHistoryClear = onHistoryClear;

        _taskbarIcon = new TaskbarIcon
        {
            Icon = _iconKeepAlive,
            ToolTipText = Loc.T("Tray_Tooltip")
        };

        _grabItem.Click += (_, _) => SafeInvoke(onGrab, "grab");
        _repeatItem.Click += (_, _) => SafeInvoke(onRepeatLast, "repeat");
        _settingsItem.Click += (_, _) => SafeInvoke(onSettings, "settings");
        _exitItem.Click += (_, _) => SafeInvoke(onExit, "exit");

        var menu = new ContextMenu();
        menu.Items.Add(_grabItem);
        menu.Items.Add(_repeatItem);
        menu.Items.Add(_historyItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_settingsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_exitItem);
        _taskbarIcon.ContextMenu = menu;

        // 每次打开菜单时刷新「最近取字」（读取失败也不影响菜单打开）
        menu.Opened += (_, _) => PopulateHistoryMenu(_historyItem);

        // 托盘左键单击 = 取字（和 Zshot 一致）
        _taskbarIcon.TrayLeftMouseUp += (_, _) => SafeInvoke(onGrab, "tray grab");

        ApplyLanguage();

        _taskbarIcon.ForceCreate();
        Log.Info("Tray icon created.");
    }

    /// <summary>按当前界面语言刷新常驻菜单项的文案（语言切换后由控制器调用）。</summary>
    public void ApplyLanguage()
    {
        _taskbarIcon.ToolTipText = Loc.T("Tray_Tooltip");
        _grabItem.Header = Loc.T("Tray_Grab");
        _repeatItem.Header = Loc.T("Tray_RepeatLast");
        _historyItem.Header = Loc.T("Tray_History");
        _settingsItem.Header = Loc.T("Tray_Settings");
        _exitItem.Header = Loc.T("Tray_Exit");
    }

    /// <summary>填充「最近取字」子菜单：搜索入口 + 最新若干条（点一条即复制）+ 尾部「清空历史」。</summary>
    private void PopulateHistoryMenu(MenuItem historyMenu)
    {
        historyMenu.Items.Clear();

        var search = new MenuItem { Header = Loc.T("Tray_HistorySearch") };
        search.Click += (_, _) => SafeInvoke(_onHistorySearch, "history search");
        historyMenu.Items.Add(search);
        historyMenu.Items.Add(new Separator());

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
            historyMenu.Items.Add(new MenuItem { Header = Loc.T("Tray_HistoryEmpty"), IsEnabled = false });
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
        var clear = new MenuItem { Header = Loc.T("Tray_HistoryClear") };
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

            // 图标上的“字”是品牌标记，不随界面语言变化
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
