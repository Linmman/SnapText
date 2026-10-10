using System.Windows.Interop;
using SnapText.Interop;

namespace SnapText.Hotkeys;

/// <summary>全局热键（RegisterHotKey + 消息窗口）。</summary>
internal sealed class HotkeyService : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 0xA000;
    private bool _disposed;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("SnapText.HotkeyMessageWindow")
        {
            Width = 0,
            Height = 0,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = 0,
            ParentWindow = Win32.HWND_MESSAGE
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>空/空白 = 不使用该热键（合法配置；比强制填一个更符合"用不上就关掉"）。</summary>
    public static bool IsDisabled(string? hotkey) => string.IsNullOrWhiteSpace(hotkey);

    public bool Register(string hotkey, Action action, out string error)
    {
        error = string.Empty;

        if (IsDisabled(hotkey))
        {
            Log.Info("Hotkey is empty; treated as disabled.");
            return true;
        }

        if (!TryParse(hotkey, out var modifiers, out var virtualKey, out error))
            return false;

        var id = _nextId++;
        if (!Win32.RegisterHotKey(_source.Handle, id, modifiers | Win32.MOD_NOREPEAT, virtualKey))
        {
            error = $"热键 {hotkey} 注册失败（可能已被其他程序占用）";
            return false;
        }

        _actions[id] = action;
        Log.Info($"Hotkey registered: {hotkey} (id={id})");
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys)
        {
            Win32.UnregisterHotKey(_source.Handle, id);
        }
        _actions.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_actions.TryGetValue(id, out var action))
            {
                handled = true;
                try { action(); }
                catch (Exception ex) { Log.Error("Hotkey action failed", ex); }
            }
        }
        return IntPtr.Zero;
    }

    private static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BACKSPACE"] = 0x08,
        ["TAB"] = 0x09,
        ["ENTER"] = 0x0D,
        ["RETURN"] = 0x0D,
        ["ESC"] = 0x1B,
        ["ESCAPE"] = 0x1B,
        ["SPACE"] = 0x20,
        ["PAGEUP"] = 0x21,
        ["PAGEDOWN"] = 0x22,
        ["END"] = 0x23,
        ["HOME"] = 0x24,
        ["LEFT"] = 0x25,
        ["UP"] = 0x26,
        ["RIGHT"] = 0x27,
        ["DOWN"] = 0x28,
        ["INSERT"] = 0x2D,
        ["DELETE"] = 0x2E,
        ["`"] = 0xC0,
        ["-"] = 0xBD,
        ["="] = 0xBB,
        ["["] = 0xDB,
        ["]"] = 0xDD,
        ["\\"] = 0xDC,
        [";"] = 0xBA,
        ["'"] = 0xDE,
        [","] = 0xBC,
        ["."] = 0xBE,
        ["/"] = 0xBF
    };

    public static bool TryParse(string hotkey, out uint modifiers, out uint virtualKey, out string error)
    {
        modifiers = 0;
        virtualKey = 0;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(hotkey))
        {
            error = "热键不能为空";
            return false;
        }

        var key = string.Empty;
        foreach (var part in hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= Win32.MOD_CONTROL;
                    break;
                case "ALT":
                    modifiers |= Win32.MOD_ALT;
                    break;
                case "SHIFT":
                    modifiers |= Win32.MOD_SHIFT;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= Win32.MOD_WIN;
                    break;
                default:
                    if (key.Length > 0)
                    {
                        error = $"热键 '{hotkey}' 里有多个主按键";
                        return false;
                    }
                    key = part;
                    break;
            }
        }

        if (key.Length == 0)
        {
            error = $"热键 '{hotkey}' 缺少主按键";
            return false;
        }

        virtualKey = ResolveKey(key);
        if (virtualKey == 0)
        {
            error = $"无法识别的按键 '{key}'";
            return false;
        }

        if (modifiers == 0)
        {
            error = "至少需要一个修饰键（Ctrl / Alt / Shift / Win）";
            return false;
        }

        return true;
    }

    private static uint ResolveKey(string key)
    {
        if (NamedKeys.TryGetValue(key, out var named))
            return named;

        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
                return c;
        }

        if (key.Length is >= 2 and <= 3
            && (key[0] is 'F' or 'f')
            && int.TryParse(key.AsSpan(1), out var number)
            && number is >= 1 and <= 24)
        {
            return (uint)(0x70 + number - 1);
        }

        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var id in _actions.Keys)
        {
            Win32.UnregisterHotKey(_source.Handle, id);
        }
        _actions.Clear();
        _source.Dispose();
    }
}
