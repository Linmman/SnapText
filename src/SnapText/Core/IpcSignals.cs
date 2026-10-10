using System.Security.AccessControl;
using System.Security.Principal;
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

    /// <summary>创建取字信号事件。ACL 只放行当前用户：同会话的其他用户进程无法触发取字。</summary>
    public static EventWaitHandle CreateGrabSignal()
    {
        var security = new EventWaitHandleSecurity();
        security.AddAccessRule(new EventWaitHandleAccessRule(
            WindowsIdentity.GetCurrent().User!,
            EventWaitHandleRights.FullControl,
            AccessControlType.Allow));

        // EventWaitHandleAcl 在 System.Threading 命名空间（所在程序集才叫 System.Threading.AccessControl）
        return EventWaitHandleAcl.Create(false, EventResetMode.AutoReset, GrabEventName, out _, security);
    }
}
