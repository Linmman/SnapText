using System.Drawing;

namespace SnapText.Ui;

internal static class ToastService
{
    private static ToastWindow? _current;

    public static void Show(string title, string body, Rectangle? anchor = null, string? actionLabel = null, Action? action = null)
    {
        try
        {
            var previous = _current;
            var toast = new ToastWindow(title, body, anchor, actionLabel, action);
            _current = toast;
            toast.Closed += (_, _) =>
            {
                if (ReferenceEquals(_current, toast))
                    _current = null;
            };

            toast.Show();
            previous?.Close();   // 关掉上一条，避免两条提示叠在同一个位置
        }
        catch (Exception ex)
        {
            Log.Error("Toast show failed", ex);
        }
    }

    public static void ShowInfo(string message) => Show("SnapText", message);
}
