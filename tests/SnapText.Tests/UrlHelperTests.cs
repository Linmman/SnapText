using SnapText.Processing;

namespace SnapText.Tests;

/// <summary>
/// UrlHelper 边界用例（对应 docs/Development.md 列出的 5 条链接修复口径）。
/// </summary>
public class UrlHelperTests
{
    [Theory]
    [InlineData("https://www.bilibili.com/video/BV1xx411c7mD", "https://www.bilibili.com/video/BV1xx411c7mD")]
    [InlineData("www.example.com/path", "https://www.example.com/path")]
    [InlineData("example.com/page", "example.com/page")]                       // 无协议前缀且不以 www. 开头：不是"纯链接"，不动
    public void Normalize_KeepsOrFixesScheme(string input, string expected)
    {
        Assert.Equal(expected, UrlHelper.Normalize(input));
    }

    [Fact]
    public void Normalize_MergesLinkSplitBySpace()
    {
        // "BV1xx411 c7mD"：链接被空格断成两截 → 合并、补协议
        Assert.Equal("https://www.bilibili.com/video/BV1xx411c7mD",
            UrlHelper.Normalize("www.bilibili.com/video/BV1xx411 c7mD"));
    }

    [Fact]
    public void Normalize_AddsSchemeWhenMissing()
    {
        // 只补协议，不改写域名本体
        Assert.Equal("https://www.example.com/x", UrlHelper.Normalize("www.example.com/x"));
    }

    [Fact]
    public void Normalize_KeepsOriginalWhenMixedWithDescription()
    {
        // 三段以上：更像"链接 + 一句说明"，原文不动
        const string mixed = "https://example.com/page 点击这里 查看详情";
        Assert.Equal(mixed, UrlHelper.Normalize(mixed));
    }

    [Fact]
    public void Normalize_KeepsOriginalWhenSecondTokenLooksLikeWord()
    {
        // 两段但第二段不像链接碎片（无数字/点）：不动
        const string mixed = "https://x.com click";
        Assert.Equal(mixed, UrlHelper.Normalize(mixed));
    }

    [Fact]
    public void Normalize_LeavesCjkTextUnchanged()
    {
        const string chinese = "这是一段中文说明，包含 www.example.com 链接";
        Assert.Equal(chinese, UrlHelper.Normalize(chinese));
    }

    [Fact]
    public void Normalize_TrimsOuterWhitespaceOnly()
    {
        Assert.Equal("https://example.com/a", UrlHelper.Normalize("  https://example.com/a  "));
    }

    [Fact]
    public void FindFirst_ReturnsFirstUrl()
    {
        Assert.Equal("https://example.com/a", UrlHelper.FindFirst("见 https://example.com/a 谢谢"));
    }

    [Fact]
    public void FindFirst_TrimsTrailingPunctuation()
    {
        Assert.Equal("https://example.com/a", UrlHelper.FindFirst("https://example.com/a。"));
        Assert.Equal("https://example.com/a", UrlHelper.FindFirst("(https://example.com/a)"));
    }

    [Fact]
    public void FindFirst_RepairsSplitToken()
    {
        Assert.Equal("https://www.bilibili.com/video/BV1xx411c7mD",
            UrlHelper.FindFirst("www.bilibili.com/video/BV1xx411 c7mD"));
    }

    [Fact]
    public void FindFirst_ReturnsNullWhenNoUrl()
    {
        Assert.Null(UrlHelper.FindFirst("没有任何链接的一句话"));
        Assert.Null(UrlHelper.FindFirst(null));
        Assert.Null(UrlHelper.FindFirst("   "));
    }

    [Fact]
    public void FindFirst_ReturnsNullWhenOnlyScheme()
    {
        // 只有协议前缀、没有实际内容：不是链接（长度守卫 + 正则都拦得住）
        Assert.Null(UrlHelper.FindFirst("http://"));
        Assert.Null(UrlHelper.FindFirst("www."));
    }

    [Fact]
    public void FindFirst_DoesNotAppendCjkTail()
    {
        // 断词修复只接"无中文的链接碎片"：紧跟中文时不接
        Assert.Equal("https://example.com/a", UrlHelper.FindFirst("https://example.com/a 中文"));
    }

    [Fact]
    public void FindFirst_ToleratesQuotesAndControlChars()
    {
        // 自定义打开程序时 url 走 ArgumentList（框架转义），这里确认提取结果本身不被引号截断
        var url = UrlHelper.FindFirst("\"https://example.com/a?q=1&r=2\"");
        Assert.Equal("https://example.com/a?q=1&r=2", url);
    }
}
