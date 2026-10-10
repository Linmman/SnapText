using SnapText.Hotkeys;
using SnapText.Interop;

namespace SnapText.Tests;

/// <summary>HotkeyService.TryParse：热键字符串解析与全部错误分支。</summary>
public class HotkeyServiceTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Q", Win32.MOD_CONTROL | Win32.MOD_ALT, 'Q')]
    [InlineData("ctrl+alt+q", Win32.MOD_CONTROL | Win32.MOD_ALT, 'Q')]          // 大小写不敏感
    [InlineData("Control+Shift+A", Win32.MOD_CONTROL | Win32.MOD_SHIFT, 'A')]
    [InlineData("Windows+F12", Win32.MOD_WIN, 0x7B)]
    [InlineData("Ctrl+Alt+R", Win32.MOD_CONTROL | Win32.MOD_ALT, 'R')]
    [InlineData("Ctrl+Alt+Space", Win32.MOD_CONTROL | Win32.MOD_ALT, 0x20)]
    [InlineData("Ctrl+Alt+F1", Win32.MOD_CONTROL | Win32.MOD_ALT, 0x70)]
    [InlineData("Ctrl+Alt+F24", Win32.MOD_CONTROL | Win32.MOD_ALT, 0x87)]
    [InlineData("Ctrl+Alt+5", Win32.MOD_CONTROL | Win32.MOD_ALT, '5')]
    [InlineData("Ctrl+Alt+Delete", Win32.MOD_CONTROL | Win32.MOD_ALT, 0x2E)]
    [InlineData("Ctrl+Alt+/", Win32.MOD_CONTROL | Win32.MOD_ALT, 0xBF)]
    [InlineData("Ctrl+Alt+`", Win32.MOD_CONTROL | Win32.MOD_ALT, 0xC0)]
    public void TryParse_AcceptsValidHotkeys(string hotkey, uint expectedModifiers, uint expectedKey)
    {
        Assert.True(HotkeyService.TryParse(hotkey, out var modifiers, out var virtualKey, out var error), error);
        Assert.Equal(expectedModifiers, modifiers);
        Assert.Equal(expectedKey, virtualKey);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("Ctrl+Alt")]        // 只有修饰键
    [InlineData("Ctrl+")]           // 尾部空段被跳过，仍无主键
    public void TryParse_RejectsNoMainKey(string hotkey)
    {
        Assert.False(HotkeyService.TryParse(hotkey, out _, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParse_RejectsEmpty()
    {
        Assert.False(HotkeyService.TryParse("", out _, out _, out var error));
        Assert.NotEmpty(error);
        Assert.False(HotkeyService.TryParse("   ", out _, out _, out error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParse_RejectsUnknownKey()
    {
        Assert.False(HotkeyService.TryParse("Ctrl+Alt+Foo", out _, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParse_RejectsOutOfRangeFunctionKey()
    {
        Assert.False(HotkeyService.TryParse("Ctrl+F0", out _, out _, out _));
        Assert.False(HotkeyService.TryParse("Ctrl+F25", out _, out _, out _));
    }

    [Fact]
    public void TryParse_RejectsMultipleMainKeys()
    {
        Assert.False(HotkeyService.TryParse("Ctrl+A+B", out _, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void TryParse_RejectsWithoutModifier()
    {
        // 无修饰键的全局热键会抢占正常输入，必须拒绝
        Assert.False(HotkeyService.TryParse("Q", out _, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void IsDisabled_TreatsBlankAsDisabled()
    {
        Assert.True(HotkeyService.IsDisabled(null));
        Assert.True(HotkeyService.IsDisabled(""));
        Assert.True(HotkeyService.IsDisabled("  "));
        Assert.False(HotkeyService.IsDisabled("Ctrl+Alt+Q"));
    }
}
