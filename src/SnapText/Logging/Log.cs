using System.IO;
using System.Text;

namespace SnapText;

/// <summary>极简文件日志：%LOCALAPPDATA%\SnapText\logs\app-yyyyMMdd.log</summary>
internal static class Log
{
    private static readonly object Gate = new();
    private static string? _file;
    private static bool _stdoutEnabled;

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
}
