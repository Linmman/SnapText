using System.Globalization;
using System.IO;
using System.Text;

namespace SnapText;

/// <summary>极简文件日志：%LOCALAPPDATA%\SnapText\logs\app-yyyyMMdd.log（启动时清理保留期外的旧日志）</summary>
internal static class Log
{
    private static readonly object Gate = new();
    private static string? _file;
    private static bool _stdoutEnabled;
    private static bool _cleanupDone;

    /// <summary>日志保留天数：更早的 app-*.log 在首次写日志时清理。</summary>
    public const int KeepDays = 30;

    public static string RootDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnapText");

    public static string LogDir { get; } = Path.Combine(RootDir, "logs");

    public static void EnableStdout() => _stdoutEnabled = true;

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message} :: {ex}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}";
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDir);
                CleanupOldLogsLocked();
                _file ??= Path.Combine(LogDir, $"app-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(_file, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // 日志失败永远不能让主流程挂掉
        }

        if (_stdoutEnabled)
        {
            try { Console.Error.WriteLine(line); } catch { }
        }
    }

    /// <summary>每进程一次：删除保留期外的旧日志。文件名即日期，解析不了的按文件修改时间兜底。失败静默（调用方已在锁内且吞异常）。</summary>
    private static void CleanupOldLogsLocked()
    {
        if (_cleanupDone) return;
        _cleanupDone = true;

        try
        {
            var cutoff = DateTime.Now.AddDays(-KeepDays);
            foreach (var file in Directory.EnumerateFiles(LogDir, "app-*.log"))
            {
                var dateText = Path.GetFileNameWithoutExtension(file)[4..];
                var expired = DateTime.TryParseExact(dateText, "yyyyMMdd", CultureInfo.InvariantCulture,
                                    DateTimeStyles.None, out var date)
                    ? date < cutoff
                    : File.GetLastWriteTime(file) < cutoff;
                if (expired) File.Delete(file);
            }
        }
        catch
        {
            // 清理失败只跳过本轮，不影响主流程
        }
    }
}
