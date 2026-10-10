using System.Runtime.InteropServices;
using System.Windows;

namespace SnapText.Core;

internal static class ClipboardService
{
    /// <summary>写文本到剪贴板；被其他程序占用时重试，并读回校验（防止"没写进去却提示成功"）。</summary>
    public static bool TrySetText(string text)
    {
        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, true);
            }
            catch (COMException ex)
            {
                Log.Warn($"Clipboard busy (attempt {attempt + 1}): {ex.Message}");
                Thread.Sleep(60);
                continue;
            }
            catch (Exception ex)
            {
                Log.Error("Clipboard write failed", ex);
                Thread.Sleep(60);
                continue;
            }

            if (VerifyClipboard(text))
                return true;

            Log.Warn($"Clipboard read-back mismatch (attempt {attempt + 1}).");
            Thread.Sleep(60);
        }

        return false;
    }

    /// <summary>
    /// 读回校验：只有"能读到内容且与写入不一致"时才判失败；
    /// 读不到（被占用/剪贴板不可用）时视为无法校验，不误报失败。
    /// </summary>
    private static bool VerifyClipboard(string text)
    {
        for (var i = 0; i < 3; i++)
        {
            try
            {
                return Clipboard.GetText() == text;
            }
            catch (COMException)
            {
                Thread.Sleep(40);
            }
            catch (Exception ex)
            {
                Log.Warn("Clipboard read-back failed: " + ex.Message);
                Thread.Sleep(40);
            }
        }

        return true;
    }
}
