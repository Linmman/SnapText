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
        if (!HotkeyService.TryParse(GrabHotkeyBox.Text, out _, out _, out var error))
        {
            MessageBox.Show(this, "取字热键无效：" + error, "SnapText", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!HotkeyService.TryParse(RepeatHotkeyBox.Text, out _, out _, out error))
        {
            MessageBox.Show(this, "重复上次区域热键无效：" + error, "SnapText", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.GrabHotkey = GrabHotkeyBox.Text.Trim();
        _settings.RepeatHotkey = RepeatHotkeyBox.Text.Trim();
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

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
