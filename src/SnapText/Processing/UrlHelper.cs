using System.Text.RegularExpressions;

namespace SnapText.Processing;

/// <summary>
/// 从取字结果里找链接，并做最小限度的修复：
///   - 整段就是一条链接（或"链接被空格断成两截"）时：去掉内部空格、缺协议时补 https://
///   - 混合文本（链接 + 说明文字）：绝不改动原文，只提取第一条链接给「打开」按钮用
/// </summary>
internal static class UrlHelper
{
    private static readonly Regex UrlInText = new(
        @"(?<url>(?:https?://|www\.)[^\s\u3000]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>整段是纯链接时做清理（去空格 + 补协议）；否则原样返回。</summary>
    public static string Normalize(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return text;
        if (trimmed.Any(IsCjk)) return text;                       // 含中文：肯定不是"纯链接"，不动

        if (!LooksLikeUrlStart(trimmed)) return text;

        var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        // 三段以上 → 更像"链接 + 一句说明"，不动原文
        if (tokens.Length > 2) return text;

        // 两段时，第二段必须"像链接碎片"（含数字或点），否则可能是普通单词（如 "https://x.com click"）
        if (tokens.Length == 2 && !tokens[1].Any(c => char.IsDigit(c) || c == '.')) return text;

        return WithScheme(string.Concat(tokens));
    }

    /// <summary>
    /// 找第一条链接（给「打开链接」用）；找不到返回 null。
    /// 若链接被 OCR 的空格断成两截（如 "BV1xx411 c7mD"），会把像链接碎片的下一段接回来。
    /// </summary>
    public static string? FindFirst(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = UrlInText.Match(text);
        if (!match.Success) return null;

        var url = match.Groups["url"].Value.TrimEnd(
            '。', '，', '、', ',', '.', '!', '！', '?', '？', '；', ';', ':', '：',
            ')', '）', ']', '】', '}', '》', '」', '』', '"', '\'', '”', '’');

        // 断词修复：紧跟其后的"链接碎片"接回来（含数字或点、无中文、够短）
        var tail = text[(match.Index + match.Groups["url"].Length)..];
        var nextToken = tail.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (nextToken is not null
            && nextToken.Length <= 20
            && !nextToken.Any(IsCjk)
            && nextToken.Any(c => char.IsDigit(c) || c == '.'))
        {
            url += nextToken;
        }

        return url.Length < 5 ? null : WithScheme(url);
    }

    private static bool LooksLikeUrlStart(string text) =>
        text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("www.", StringComparison.OrdinalIgnoreCase);

    private static bool IsCjk(char c) =>
        c is >= '\u2e80' and <= '\u9fff' or >= '\u3000' and <= '\u303f' or >= '\uff00' and <= '\uffef';

    private static string WithScheme(string url) =>
        url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : "https://" + url;
}
