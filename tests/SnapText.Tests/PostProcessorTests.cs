using SnapText.Processing;

namespace SnapText.Tests;

/// <summary>PostProcessor：中文假空格、首尾空白、断行合并。</summary>
public class PostProcessorTests
{
    [Fact]
    public void Clean_RemovesSpacesBetweenCjk()
    {
        Assert.Equal("你好世界", PostProcessor.Clean("你 好 世 界", mergeLines: false));
    }

    [Fact]
    public void Clean_KeepsSpacesBetweenAsciiWords()
    {
        Assert.Equal("hello world", PostProcessor.Clean("hello world", mergeLines: false));
    }

    [Fact]
    public void Clean_KeepsSpaceBetweenCjkAndAscii()
    {
        // 中英之间保留空格（?<=中文) 只匹配中文两侧）
        Assert.Equal("中文 English", PostProcessor.Clean("中文 English", mergeLines: false));
    }

    [Fact]
    public void Clean_TrimsEachLineAndDropsEmptyLines()
    {
        Assert.Equal("第一行\n第二行", PostProcessor.Clean("  第一行  \n\n  第二行  ", mergeLines: false));
    }

    [Fact]
    public void Clean_MergesLinesWithSpaceBetweenAsciiWords()
    {
        Assert.Equal("hello world", PostProcessor.Clean("hello\nworld", mergeLines: true));
    }

    [Fact]
    public void Clean_MergesCjkLinesWithoutSpace()
    {
        Assert.Equal("你好世界", PostProcessor.Clean("你好\n世界", mergeLines: true));
    }

    [Fact]
    public void Clean_MergesMixedLinesWithoutSpaceAtCjkBoundary()
    {
        Assert.Equal("你好世界", PostProcessor.Clean("你好\n世界", mergeLines: true));
        Assert.Equal("abc你好", PostProcessor.Clean("abc\n你好", mergeLines: true));
    }

    [Fact]
    public void Clean_NormalizesCrLf()
    {
        Assert.Equal("a\nb", PostProcessor.Clean("a\r\nb\r\n", mergeLines: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    public void Clean_ReturnsEmptyForBlankInput(string? raw)
    {
        Assert.Equal(string.Empty, PostProcessor.Clean(raw, mergeLines: true));
    }
}
