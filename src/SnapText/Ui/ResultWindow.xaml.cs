using System.Windows;
using SnapText.Core;

namespace SnapText.Ui;

/// <summary>非静默模式下的识别结果窗口：可编辑后再复制。</summary>
internal partial class ResultWindow : Window
{
    private readonly Action<string>? _onCopied;

    /// <param name="onCopied">真正复制成功时回调（最终文本，可能已被用户编辑）；用于记录取字历史。</param>
    public ResultWindow(string text, Action<string>? onCopied = null)
    {
        InitializeComponent();
        ResultBox.Text = text;
        _onCopied = onCopied;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        ResultBox.Focus();
        ResultBox.SelectAll();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        var text = ResultBox.Text;
        if (!ClipboardService.TrySetText(text))
        {
            MessageBox.Show(this, "写入剪贴板失败，请重试。", "SnapText", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _onCopied?.Invoke(text);
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
