using SnapText.Core;
using SnapText.Localization;

namespace SnapText.Tests;

/// <summary>
/// BurstRunner.Score（选帧打分，连拍/自适应取字的核心）与文案表一致性。
/// Score 已从 private 提为 internal，见 BurstRunner.cs。
/// </summary>
public class BurstRunnerScoreTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void Score_ReturnsMinusOneForBlankText(string? text)
    {
        Assert.Equal(-1, BurstRunner.Score(text!, 0.99));
    }

    [Fact]
    public void Score_UsesConfidenceAsPrimaryWeight()
    {
        // 同样 5 个字：置信度 0.9 → 90+5；0.5 → 50+5
        Assert.Equal(95, BurstRunner.Score("abcde", 0.9), precision: 6);
        Assert.Equal(55, BurstRunner.Score("abcde", 0.5), precision: 6);
    }

    [Fact]
    public void Score_ConfidenceDefaultsToPointNineWhenUnknown()
    {
        // 置信度为 null（系统 OCR 路径）按 0.9 估
        Assert.Equal(BurstRunner.Score("abcde", 0.9), BurstRunner.Score("abcde", null), precision: 6);
    }

    [Fact]
    public void Score_BoundsLengthBonusAtSixty()
    {
        var sixty = new string('字', 60);
        var hundred = new string('字', 100);
        Assert.Equal(BurstRunner.Score(sixty, 0.8), BurstRunner.Score(hundred, 0.8), precision: 6);
    }

    [Fact]
    public void Score_PrefersConfidentShortOverUnsureLong()
    {
        // 置信度是主权重：0.95 的 10 字（105）胜 0.5 的 40 字（90）
        Assert.True(BurstRunner.Score(new string('字', 10), 0.95) >
                    BurstRunner.Score(new string('字', 40), 0.5));
    }

    [Fact]
    public void Score_LengthBonusBreaksTiesWithinSameConfidenceBand()
    {
        // 同置信度下，稍长的文本略占优（但上限 60 字）
        Assert.True(BurstRunner.Score(new string('字', 20), 0.8) >
                    BurstRunner.Score(new string('字', 10), 0.8));
    }

    [Fact]
    public void LanguageTables_HaveIdenticalKeySets()
    {
        var missing = Loc.FindMissingKeys();
        Assert.True(missing.Count == 0, "文案表缺键: " + string.Join(", ", missing));
    }

    [Fact]
    public void LanguageTables_HaveNoEmptyValues()
    {
        foreach (var (language, table) in new[]
                 {
                     ("zh", Strings.Zh), ("ja", Strings.Ja), ("en", Strings.En)
                 })
        {
            foreach (var (key, value) in table)
                Assert.False(string.IsNullOrWhiteSpace(value), $"{language}:{key} 为空");
        }
    }

    [Fact]
    public void ToSingleLine_CollapsesWhitespace()
    {
        Assert.Equal("a b c", HistoryStore.ToSingleLine("a\r\n  b\t c"));
    }

    [Fact]
    public void ToSingleLine_TruncatesWithEllipsis()
    {
        var result = HistoryStore.ToSingleLine(new string('字', 200), maxLength: 120);
        Assert.Equal(121, result.Length);          // 120 字 + 省略号
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void ToSingleLine_KeepsShortTextIntact()
    {
        Assert.Equal("短文本", HistoryStore.ToSingleLine("短文本", maxLength: 120));
    }
}
