using System.Text.RegularExpressions;

namespace SnapText.Processing;

/// <summary>
/// 从取字结果里找链接，并做最小限度的修复：
///   - 整段就是一条链接时：去掉内部空格（视频里常见断行/断词）、缺协议时补 https://
///   - 混合文本：只提取第一条可用链接用于「打开」按钮，不改动原文
/// </summary>
internal static class UrlHelper
{
    private static readonly Regex UrlInText = new(
        @"(?<url>(?:https?://|www\.)[^\s\u3000]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PureUrl = new(
        @"^(?:https?://|www\.)[^\u4e00-\u9fff\u3000\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>整段是纯链接时做清理（去空格 + 补协议）；否则原样返回。</summary>
    public static string Normalize(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return text;

        var compact = trimmed.Replace(" ", string.Empty).Replace("\u3000", string.Empty);
        if (!PureUrl.IsMatch(compact)) return text;

        return WithScheme(compact);
    }

    /// <summary>找第一条链接（给「打开链接」用）；找不到返回 null。</summary>
    public static string? FindFirst(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = UrlInText.Match(text);
        if (!match.Success) return null;

        var url = match.Groups["url"].Value.TrimEnd(
            '。', '，', '、', ',', '.', '!', '！', '?', '？', '；', ';', ':', '：',
            ')', '）', ']', '】', '}', '》', '」', '』', '"', '\'', '”', '’');

        return url.Length < 5 ? null : WithScheme(url);
    }

    private static string WithScheme(string url) =>
        url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : "https://" + url;
}
