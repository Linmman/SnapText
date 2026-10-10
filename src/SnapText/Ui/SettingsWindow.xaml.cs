using System.Windows;
using SnapText.Hotkeys;
using SnapText.Settings;

namespace SnapText.Ui;

internal partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action _onSaved;

    public SettingsWindow(AppSettings settings, Action onSaved)
    {
        InitializeComponent();

        _settings = settings;
        _onSaved = onSaved;

        GrabHotkeyBox.Text = settings.GrabHotkey;
        RepeatHotkeyBox.Text = settings.RepeatHotkey;
        LinkOpenerBox.Text = settings.LinkOpener;
        EngineCombo.SelectedIndex = settings.Engine?.ToLowerInvariant() switch
        {
            "ppocr" => 1,
            "windows" => 2,
            _ => 0
        };
        SilentCopyCheck.IsChecked = settings.SilentCopy;
        MergeLinesCheck.IsChecked = settings.MergeLines;
        SaveCaptureCheck.IsChecked = settings.SaveCapture;
        AutoStartCheck.IsChecked = settings.AutoStart;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var grabHotkey = GrabHotkeyBox.Text.Trim();
        var repeatHotkey = RepeatHotkeyBox.Text.Trim();

        // 留空 = 关闭该热键（合法），只有填了内容才校验格式
        if (!HotkeyService.IsDisabled(grabHotkey) && !HotkeyService.TryParse(grabHotkey, out _, out _, out var grabError))
        {
            MessageBox.Show(this, "取字热键无效：" + grabError, "SnapText", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!HotkeyService.IsDisabled(repeatHotkey) && !HotkeyService.TryParse(repeatHotkey, out _, out _, out var repeatError))
        {
            MessageBox.Show(this, "重复上次区域热键无效：" + repeatError, "SnapText", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.GrabHotkey = grabHotkey;
        _settings.RepeatHotkey = repeatHotkey;
        _settings.LinkOpener = LinkOpenerBox.Text.Trim();
        _settings.Engine = EngineCombo.SelectedIndex switch
        {
            1 => "ppocr",
            2 => "windows",
            _ => "auto"
        };
        _settings.SilentCopy = SilentCopyCheck.IsChecked == true;
        _settings.MergeLines = MergeLinesCheck.IsChecked == true;
        _settings.SaveCapture = SaveCaptureCheck.IsChecked == true;
        _settings.AutoStart = AutoStartCheck.IsChecked == true;

        SettingsStore.Save(_settings);
        _onSaved();
        Close();
    }

    private void OnBrowseLinkOpenerClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择用来打开链接的程序",
            Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*",
            CheckFileExists = true
        };

        if (System.IO.File.Exists(LinkOpenerBox.Text))
            dialog.FileName = LinkOpenerBox.Text;

        if (dialog.ShowDialog(this) == true)
            LinkOpenerBox.Text = dialog.FileName;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
