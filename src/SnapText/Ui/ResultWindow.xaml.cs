using System.Windows;
using SnapText.Core;

namespace SnapText.Ui;

/// <summary>非静默模式下的识别结果窗口：可编辑后再复制。</summary>
internal partial class ResultWindow : Window
{
    public ResultWindow(string text)
    {
        InitializeComponent();
        ResultBox.Text = text;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        ResultBox.Focus();
        ResultBox.SelectAll();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (!ClipboardService.TrySetText(ResultBox.Text))
        {
            MessageBox.Show(this, "写入剪贴板失败，请重试。", "SnapText", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
