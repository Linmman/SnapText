using System.Drawing;
using System.Runtime.InteropServices;
using SnapText.Interop;

namespace SnapText.Capture;

internal sealed class MonitorEntry
{
    public required IntPtr Handle { get; init; }
    public required Rectangle Bounds { get; init; }        // 物理像素，虚拟桌面坐标
    public required Rectangle WorkArea { get; init; }      // 物理像素
    public required double Scale { get; init; }            // 96dpi = 1.0
}

internal static class MonitorEnumerator
{
    public static List<MonitorEntry> GetAll()
    {
        var list = new List<MonitorEntry>();

        Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref Win32.RECT rect, IntPtr data) =>
        {
            var info = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };
            if (!Win32.GetMonitorInfo(hMonitor, ref info))
                return true;

            uint dpiX = 96, dpiY = 96;
            try { Win32.GetDpiForMonitor(hMonitor, 0 /* MDT_EFFECTIVE_DPI */, out dpiX, out dpiY); }
            catch { /* 老系统拿不到 DPI 就按 100% 处理 */ }

            list.Add(new MonitorEntry
            {
                Handle = hMonitor,
                Bounds = Rectangle.FromLTRB(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Right, info.rcMonitor.Bottom),
                WorkArea = Rectangle.FromLTRB(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom),
                Scale = dpiX / 96.0
            });
            return true;
        }, IntPtr.Zero);

        return list;
    }

    /// <summary>光标所在的显示器（找不到时退化为第一个）。</summary>
    public static MonitorEntry? GetAtCursor(List<MonitorEntry> monitors)
    {
        if (monitors.Count == 0) return null;
        if (!Win32.GetCursorPos(out var pt)) return monitors[0];

        var handle = Win32.MonitorFromPoint(pt, Win32.MONITOR_DEFAULTTONEAREST);
        foreach (var m in monitors)
        {
            if (m.Handle == handle) return m;
        }

        foreach (var m in monitors)
        {
            if (m.Bounds.Contains(pt.X, pt.Y)) return m;
        }

        return monitors[0];
    }
}
