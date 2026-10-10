using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SnapText.Core;

/// <summary>一条取字历史（JSONL 里的一行）。</summary>
internal sealed class HistoryEntry
{
    [JsonPropertyName("ts")] public DateTime Time { get; set; }

    [JsonPropertyName("text")] public string Text { get; set; } = "";

    /// <summary>来源：ocr（单帧）/ burst（连拍）/ code（二维码）/ cli（命令行写入）。</summary>
    [JsonPropertyName("source")] public string Source { get; set; } = "";
}

/// <summary>
/// 取字历史：本地 JSONL 文件（默认 %LOCALAPPDATA%\SnapText\history.jsonl），最多 <see cref="MaxEntries"/> 条。
/// 不做数据库依赖（保持自包含体积为零增量）；文件小（200 条约几十 KB），每次读全量、追加、超限裁剪。
/// 读写用命名互斥量跨进程保护（应用与 CLI 可能同时访问）；任何失败只记日志，绝不影响取字主流程。
/// 测试钩子：环境变量 SNAPTEXT_HISTORY_FILE 指定文件、SNAPTEXT_HISTORY_MAX 指定条数上限。
/// </summary>
internal sealed class HistoryStore
{
    public const int DefaultMaxEntries = 200;

    private const string MutexName = @"Local\SnapText.History";

    // UnsafeRelaxedJsonEscaping：中文不转义成 \uXXXX，文件用记事本打开就能读懂（本地数据，非 HTML 场景）
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly int _maxEntries;

    public string FilePath { get; }

    public HistoryStore()
    {
        var overridePath = Environment.GetEnvironmentVariable("SNAPTEXT_HISTORY_FILE");
        FilePath = string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(Log.RootDir, "history.jsonl")
            : overridePath!;

        _maxEntries = int.TryParse(Environment.GetEnvironmentVariable("SNAPTEXT_HISTORY_MAX"), out var max) && max > 0
            ? max
            : DefaultMaxEntries;
    }

    public int MaxEntries => _maxEntries;

    /// <summary>追加一条；与最近一条完全相同则跳过（连拍/重复取字不会灌满历史）。返回是否写入。</summary>
    public bool Add(string text, string source)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        try
        {
            return WithLock(() =>
            {
                var entries = ReadAll();
                if (entries.Count > 0 && entries[^1].Text == text) return false;

                entries.Add(new HistoryEntry { Time = DateTime.Now, Text = text, Source = source });
                if (entries.Count > _maxEntries)
                    entries.RemoveRange(0, entries.Count - _maxEntries);
                WriteAll(entries);
                return true;
            });
        }
        catch (Exception ex)
        {
            Log.Error("History add failed", ex);
            return false;
        }
    }

    /// <summary>按时间倒序（最新在前）返回；文件不存在或某行损坏时尽力而为。</summary>
    public IReadOnlyList<HistoryEntry> Load()
    {
        try
        {
            return WithLock(() =>
            {
                var entries = ReadAll();
                entries.Reverse();
                return (IReadOnlyList<HistoryEntry>)entries;
            });
        }
        catch (Exception ex)
        {
            Log.Error("History load failed", ex);
            return Array.Empty<HistoryEntry>();
        }
    }

    /// <summary>
    /// 当前条数：只数非空行，不反序列化整个文件。
    /// 前提：WriteAll 每条历史恰好写一行且无空行（temp+move 保证不会留下半行），所以行数=条数。
    /// </summary>
    public int Count
    {
        get
        {
            try
            {
                return WithLock(() =>
                {
                    if (!File.Exists(FilePath)) return 0;
                    var count = 0;
                    foreach (var line in File.ReadLines(FilePath))
                    {
                        if (!string.IsNullOrWhiteSpace(line)) count++;
                    }
                    return count;
                });
            }
            catch (Exception ex)
            {
                Log.Error("History count failed", ex);
                return 0;
            }
        }
    }

    public void Clear()
    {
        try
        {
            WithLock(() =>
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                return true;
            });
        }
        catch (Exception ex)
        {
            Log.Error("History clear failed", ex);
        }
    }

    /// <summary>把多行文本压成一行（列表/提示里显示用）。</summary>
    public static string ToSingleLine(string text, int maxLength = 120)
    {
        var single = Regex.Replace(text, @"\s+", " ").Trim();
        return single.Length <= maxLength ? single : single[..maxLength] + "…";
    }

    private List<HistoryEntry> ReadAll()
    {
        var list = new List<HistoryEntry>();
        if (!File.Exists(FilePath)) return list;

        foreach (var line in File.ReadLines(FilePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var entry = JsonSerializer.Deserialize<HistoryEntry>(line, Options);
                if (entry is not null && !string.IsNullOrWhiteSpace(entry.Text))
                    list.Add(entry);
            }
            catch (JsonException)
            {
                // 坏行（例如断电写了一半）：跳过即可，历史不是关键数据
            }
        }

        return list;
    }

    private void WriteAll(List<HistoryEntry> entries)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        // 先写临时文件再替换：避免写到一半崩溃留下半个文件
        var temp = FilePath + ".tmp";
        using (var writer = new StreamWriter(temp, false, new UTF8Encoding(false)))
        {
            foreach (var entry in entries)
                writer.WriteLine(JsonSerializer.Serialize(entry, Options));
        }

        File.Move(temp, FilePath, overwrite: true);
    }

    private static T WithLock<T>(Func<T> action)
    {
        using var mutex = new Mutex(false, MutexName);
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(TimeSpan.FromSeconds(2));
            }
            catch (AbandonedMutexException)
            {
                acquired = true;   // 上一个持有者异常退出，所有权已归我们
            }

            if (!acquired) throw new TimeoutException("等待历史文件互斥量超时");
            return action();
        }
        finally
        {
            if (acquired)
            {
                try { mutex.ReleaseMutex(); } catch { }
            }
        }
    }
}
