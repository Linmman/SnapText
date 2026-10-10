using System.Text;
using System.Text.RegularExpressions;

namespace SnapText.Processing;

/// <summary>OCR 结果的文本净化：去掉中文之间的假空格、可选按标点合并断行。</summary>
internal static class PostProcessor
{
    private static readonly Regex CjkSpaces = new(
        @"(?<=[\u2E80-\u9FFF\u3000-\u303F\uFF00-\uFFEF])\s+(?=[\u2E80-\u9FFF\u3000-\u303F\uFF00-\uFFEF])",
        RegexOptions.Compiled);

    public static string Clean(string? raw, bool mergeLines)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var lines = raw
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => CjkSpaces.Replace(line.Trim(), string.Empty))
            .Where(line => line.Length > 0)
            .ToList();

        if (lines.Count == 0)
            return string.Empty;

        return mergeLines
            ? MergeLines(lines)
            : string.Join("\n", lines).Trim();
    }

    private static string MergeLines(List<string> lines)
    {
        var builder = new StringBuilder(lines[0]);
        for (var i = 1; i < lines.Count; i++)
        {
            var previous = builder[^1];
            var next = lines[i][0];
            if (IsAsciiWordChar(previous) && IsAsciiWordChar(next))
                builder.Append(' ');
            builder.Append(lines[i]);
        }
        return builder.ToString().Trim();
    }

    private static bool IsAsciiWordChar(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9';
}
