using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SnapText.Core;
using SnapText.Interop;
using SnapText.Localization;

namespace SnapText.Ui;

/// <summary>
/// 取字历史面板：搜索 + 列表 + 全文预览；Enter / 双击 = 复制并关闭。
/// 只由用户在托盘菜单里显式打开（「最近取字 → 搜索历史…」），永不自动弹出，不进入取字主流程。
/// </summary>
internal partial class HistoryWindow : Window
{
    private readonly HistoryStore _store;
    private readonly IntPtr _returnTo;
    private readonly DispatcherTimer _clearTimer;

    private List<HistoryEntry> _all = new();
    private bool _clearArmed;

    public HistoryWindow(HistoryStore store, IntPtr returnTo)
    {
        InitializeComponent();
        _store = store;
        _returnTo = returnTo;

        _clearTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _clearTimer.Tick += (_, _) => DisarmClear();

        Reload();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        SearchBox.Focus();
    }

    private void Reload()
    {
        _all = _store.Load().ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text.Trim();
        var rows = _all
            .Where(e => query.Length == 0 || e.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(e => new Row(e))
            .ToList();

        EntryList.ItemsSource = rows;
        EmptyHint.Text = _all.Count == 0 ? Loc.T("Hist_Empty") : Loc.T("Hist_NoMatch");
        EmptyHint.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = Loc.T("Hist_Status", _all.Count, _store.MaxEntries);

        EntryList.SelectedIndex = rows.Count > 0 ? 0 : -1;
        if (rows.Count == 0)
        {
            PreviewBox.Text = "";
            CopyButton.IsEnabled = false;
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = EntryList.SelectedItem as Row;
        PreviewBox.Text = row?.Full ?? "";
        CopyButton.IsEnabled = row is not null;
    }

    private void OnListMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is Row)
            CopySelected();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e) => CopySelected();

    private void CopySelected()
    {
        if (EntryList.SelectedItem is not Row row) return;

        if (!ClipboardService.TrySetText(row.Full))
        {
            ToastService.Show(Loc.T("Toast_CopyFailed_Title"), Loc.T("Toast_History_CopyFailed_Body"));
            return;
        }

        Log.Info($"History: copied entry ({row.Full.Length} chars).");
        ToastService.Show(Loc.T("Toast_History_Copied_Title"), HistoryStore.ToSingleLine(row.Full));
        Close();
        RestoreForeground();   // 关闭后再归还焦点，用户直接 Ctrl+V
    }

    /// <summary>清空做了两步确认（第一次点变为「再点一次确认」，3 秒后自动还原），避免误删。</summary>
    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (!_clearArmed)
        {
            _clearArmed = true;
            ClearButton.Content = Loc.T("Hist_ClearConfirm");
            _clearTimer.Stop();
            _clearTimer.Start();
            return;
        }

        DisarmClear();
        _store.Clear();
        Reload();
        ToastService.Show(Loc.T("Toast_History_Cleared_Title"), Loc.T("Toast_History_Cleared_Body"));
    }

    private void DisarmClear()
    {
        _clearTimer.Stop();
        _clearArmed = false;
        ClearButton.Content = Loc.T("Tray_HistoryClear");
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void RestoreForeground()
    {
        try
        {
            if (_returnTo == IntPtr.Zero || !Win32.IsWindow(_returnTo)) return;
            Win32.SetForegroundWindow(_returnTo);
            Log.Info("History: focus restored.");
        }
        catch (Exception ex)
        {
            Log.Warn("History restore focus failed: " + ex.Message);
        }
    }

    /// <summary>列表行（时间与单行预览预先格式化好，XAML 里只做绑定）。</summary>
    private sealed class Row
    {
        public Row(HistoryEntry entry)
        {
            Full = entry.Text;
            Time = entry.Time.ToString("MM-dd HH:mm:ss");
            Preview = HistoryStore.ToSingleLine(entry.Text);
        }

        public string Time { get; }

        public string Preview { get; }

        public string Full { get; }
    }
}
