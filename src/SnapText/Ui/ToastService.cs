using System.Drawing;

namespace SnapText.Ui;

internal static class ToastService
{
    public static void Show(string title, string body, Rectangle? anchor = null, string? actionLabel = null, Action? action = null)
    {
        try
        {
            new ToastWindow(title, body, anchor, actionLabel, action).Show();
        }
        catch (Exception ex)
        {
            Log.Error("Toast show failed", ex);
        }
    }

    public static void ShowInfo(string message) => Show("SnapText", message);
}
