# 诊断：本环境里 SendKeys 注入的按键能否触发全局热键（RegisterHotKey）。
# 在同进程内注册 Ctrl+Alt+J，跑一个消息循环，然后用 SendKeys 注入，观察是否收到 WM_HOTKEY。
Add-Type -AssemblyName System.Windows.Forms

Add-Type -ReferencedAssemblies System.Windows.Forms @"
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class HotkeyProbeForm : Form
{
    public const int WM_HOTKEY = 0x0312;
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    public bool Fired = false;
    public bool Registered = false;

    public HotkeyProbeForm()
    {
        // Ctrl(0x0002) + Alt(0x0001) + J(0x4A)
        Registered = RegisterHotKey(this.Handle, 0x1234, 0x0002 | 0x0001, 0x4A);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            Fired = true;
            Console.WriteLine("WM_HOTKEY received");
        }
        base.WndProc(ref m);
    }
}
"@

Write-Host ("UserInteractive={0}" -f [Environment]::UserInteractive)

$form = New-Object HotkeyProbeForm
Write-Host ("Hotkey registered: {0}" -f $form.Registered)

$sendTimer = New-Object System.Windows.Forms.Timer
$sendTimer.Interval = 400
$sendTimer.Add_Tick({
    try { [System.Windows.Forms.SendKeys]::SendWait('^%j') } catch { Write-Host ("SendKeys error: {0}" -f $_.Exception.Message) }
    $sendTimer.Stop()
})

$stopTimer = New-Object System.Windows.Forms.Timer
$stopTimer.Interval = 2500
$stopTimer.Add_Tick({ [System.Windows.Forms.Application]::ExitThread() })

$sendTimer.Start()
$stopTimer.Start()
[System.Windows.Forms.Application]::Run()

if ($form.Fired) {
    Write-Host 'PROBE_RESULT: PASS (SendKeys 可以触发全局热键)'
} else {
    Write-Host 'PROBE_RESULT: FAIL (SendKeys 没有触发全局热键)'
}
