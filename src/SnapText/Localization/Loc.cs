using System.Globalization;

namespace SnapText.Localization;

/// <summary>
/// 界面语言：auto（跟随系统，设置缺省）/ zh / ja / en。
/// 初始化时机：进程最早期（CLI 分流前、任何窗口创建前）；设置保存后由控制器重新初始化即可即时生效。
/// 环境变量 SNAPTEXT_LANG（zh|ja|en）优先级最高，便于脚本与回归测试强制语言。
/// </summary>
internal static class Loc
{
    public const string Auto = "auto";

    private static readonly object Gate = new();
    private static readonly HashSet<string> Warned = new();
    private static Dictionary<string, string> _table = Strings.Zh;

    /// <summary>当前生效的语言码：zh / ja / en。</summary>
    public static string Language { get; private set; } = "zh";

    public static void Initialize(string? setting)
    {
        Language = Resolve(setting);
        _table = Language switch
        {
            "ja" => Strings.Ja,
            "en" => Strings.En,
            _ => Strings.Zh
        };
    }

    /// <summary>把 "auto" / 具体语言码解析成 zh|ja|en；SNAPTEXT_LANG 环境变量优先于设置。</summary>
    public static string Resolve(string? setting)
    {
        var forced = Environment.GetEnvironmentVariable("SNAPTEXT_LANG");
        var key = string.IsNullOrWhiteSpace(forced) ? setting?.Trim().ToLowerInvariant() : forced.Trim().ToLowerInvariant();
        if (key is "zh" or "ja" or "en") return key!;

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "zh" => "zh",
            "ja" => "ja",
            _ => "en"
        };
    }

    /// <summary>取文案；缺键依次回退 en → 键名本身，并（每个键一次）记 WARN 日志。</summary>
    public static string T(string key)
    {
        if (_table.TryGetValue(key, out var text)) return text;
        if (Strings.En.TryGetValue(key, out var fallback))
        {
            WarnOnce(key);
            return fallback;
        }

        WarnOnce(key);
        return key;
    }

    public static string T(string key, params object?[] args)
    {
        var format = T(key);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, args);
        }
        catch (FormatException)
        {
            Log.Warn($"Loc format failed for key '{key}'");
            return format;
        }
    }

    /// <summary>返回三语言文案表的键差异（"--loc check" 用）：任何一张表里缺的键都会列出。</summary>
    public static List<string> FindMissingKeys()
    {
        var union = new HashSet<string>(Strings.Zh.Keys);
        union.UnionWith(Strings.Ja.Keys);
        union.UnionWith(Strings.En.Keys);

        var missing = new List<string>();
        foreach (var key in union.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (!Strings.Zh.ContainsKey(key)) missing.Add("zh:" + key);
            if (!Strings.Ja.ContainsKey(key)) missing.Add("ja:" + key);
            if (!Strings.En.ContainsKey(key)) missing.Add("en:" + key);
        }

        return missing;
    }

    /// <summary>键总数（三种语言应完全一致）。</summary>
    public static int KeyCount => Strings.En.Count;

    private static void WarnOnce(string key)
    {
        lock (Gate)
        {
            if (!Warned.Add(key)) return;
        }

        Log.Warn($"Missing UI string: {key} (language={Language})");
    }
}
