using System.Threading;

namespace SnapText.Core;

/// <summary>进程间信号：让已运行的实例执行一次取字（供脚本 / 自动化 / 其他工具触发）。</summary>
internal static class IpcSignals
{
    public const string GrabEventName = @"Local\SnapText.Grab";

    /// <summary>通知正在运行的实例开始取字。返回是否找到实例。</summary>
    public static bool TrySignalGrab()
    {
        try
        {
            using var handle = EventWaitHandle.OpenExisting(GrabEventName);
            handle.Set();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static EventWaitHandle CreateGrabSignal() =>
        new(false, EventResetMode.AutoReset, GrabEventName);
}
