using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using SnapText.Capture;
using SnapText.Core;
using SnapText.Interop;
using ZXing;
using ZXing.QrCode;
using SnapText.Localization;
using SnapText.Ocr;
using SnapText.Processing;
using SnapText.Settings;

namespace SnapText.Cli;

/// <summary>
/// 无界面命令行模式（文案随界面语言/系统语言，SNAPTEXT_LANG 可强制）：
///   SnapText.exe --ocr &lt;图片&gt; [--out 文本.txt] [--no-merge]
///   SnapText.exe --capture [--out 截图.png]
///   SnapText.exe --ocr-adaptive &lt;x,y,w,h&gt;（对屏幕区域做自适应多帧取字，供脚本/测试）
///   SnapText.exe --history list|search|add|clear（取字历史，供脚本/测试）
///   SnapText.exe --loc check（校验三语言文案表键集合一致）
///   SnapText.exe --version
/// </summary>
internal static class CliRunner
{
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0) return false;

        switch (args[0].ToLowerInvariant())
        {
            case "--ocr":
                exitCode = RunOcr(args);
                return true;
            case "--capture":
                exitCode = RunCapture(args);
                return true;
            case "--bench":
                exitCode = RunBench(args);
                return true;
            case "--grab":
                exitCode = RunSignalGrab();
                return true;
            case "--grab-region":
                exitCode = RunGrabRegion(args);
                return true;
            case "--scan":
                exitCode = RunScan(args);
                return true;
            case "--ocr-burst":
                exitCode = RunOcrBurst(args);
                return true;
            case "--ocr-adaptive":
                exitCode = RunOcrAdaptive(args);
                return true;
            case "--qr":
                exitCode = RunQr(args);
                return true;
            case "--history":
                exitCode = RunHistory(args);
                return true;
            case "--loc":
                exitCode = RunLocCheck(args);
                return true;
            case "--help":
            case "-h":
                Print(Loc.T("Cli_Help"));
                return true;
            case "--version":
                Print("SnapText " + (typeof(CliRunner).Assembly.GetName().Version?.ToString() ?? "0.0.0"));
                return true;
            default:
                return false;
        }
    }

    private static int RunOcr(string[] args)
    {
        var path = GetPositional(args, "--out", "--engine").FirstOrDefault();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Print(Loc.T("Cli_Usage_Ocr"));
            return 2;
        }

        var merge = !args.Contains("--no-merge", StringComparer.OrdinalIgnoreCase);
        var outPath = GetOption(args, "--out");
        var engineName = GetOption(args, "--engine") ?? "auto";

        try
        {
            var settings = new AppSettings { Engine = engineName };
            using var bitmap = new Bitmap(path);
            using var ocr = new OcrService(settings);

            var started = Environment.TickCount64;
            // 放到线程池执行（OCR 是 CPU 密集的同步流程），避免阻塞
            var outcome = Task.Run(() => ocr.RecognizeAsync(bitmap)).GetAwaiter().GetResult();
            var elapsed = Environment.TickCount64 - started;
            var text = UrlHelper.Normalize(PostProcessor.Clean(outcome.Text, merge));
            Print($"engine={ocr.Name} elapsed={elapsed}ms chars={text.Length} conf={FormatConfidence(outcome.Confidence)}");

            if (!string.IsNullOrEmpty(outPath))
            {
                File.WriteAllText(outPath!, text, new UTF8Encoding(false));
                Print($"OK {text.Length} chars -> {outPath}");
            }
            else
            {
                Print(text);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("CLI --ocr failed", ex);
            Print("FAILED: " + ex.Message);
            return 1;
        }
    }

    private static int RunCapture(string[] args)
    {
        var outPath = GetOption(args, "--out")
                      ?? Path.Combine(Path.GetTempPath(), $"SnapText-capture-{DateTime.Now:HHmmss}.png");

        try
        {
            using var capture = ScreenCapture.CaptureAll();
            capture.Bitmap.Save(outPath, ImageFormat.Png);
            Print($"OK {capture.Bitmap.Width}x{capture.Bitmap.Height} -> {outPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("CLI --capture failed", ex);
            Print("FAILED: " + ex.Message);
            return 1;
        }
    }

    /// <summary>同一进程内多次识别，测量常驻后的热延迟（模型只加载一次）。</summary>
    private static int RunBench(string[] args)
    {
        var path = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Print(Loc.T("Cli_Usage_Bench"));
            return 2;
        }

        var engineName = GetOption(args, "--engine") ?? "auto";
        var iterations = int.TryParse(GetOption(args, "--n"), out var n) && n > 0 ? n : 10;

        try
        {
            var settings = new AppSettings { Engine = engineName };
            using var bitmap = new Bitmap(path);
            using var ocr = new OcrService(settings);

            // 首次包含模型加载
            var warmStarted = Environment.TickCount64;
            _ = Task.Run(() => ocr.RecognizeAsync(bitmap)).GetAwaiter().GetResult();
            var warmup = Environment.TickCount64 - warmStarted;

            var times = new List<long>();
            for (var i = 0; i < iterations; i++)
            {
                var started = Environment.TickCount64;
                _ = Task.Run(() => ocr.RecognizeAsync(bitmap)).GetAwaiter().GetResult();
                times.Add(Environment.TickCount64 - started);
            }

            times.Sort();
            Print($"engine={ocr.Name} warmup={warmup}ms min={times[0]}ms p50={times[times.Count / 2]}ms max={times[^1]}ms n={iterations}");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("CLI --bench failed", ex);
            Print("FAILED: " + ex.Message);
            return 1;
        }
    }

    /// <summary>通知正在运行的实例开始取字。</summary>
    private static int RunSignalGrab()
    {
        if (IpcSignals.TrySignalGrab())
        {
            Print(Loc.T("Cli_GrabSignaled"));
            return 0;
        }

        Print(Loc.T("Cli_NoInstance"));
        return 1;
    }

    /// <summary>对屏幕固定区域取字：截屏 → OCR → 剪贴板（可脚本化的固定区域取字）。</summary>
    private static int RunGrabRegion(string[] args)
    {
        var spec = GetPositional(args, "--out", "--engine").FirstOrDefault();
        if (string.IsNullOrEmpty(spec))
        {
            Print(Loc.T("Cli_Usage_GrabRegion"));
            return 2;
        }

        var parts = spec.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !parts.All(p => int.TryParse(p, out _)))
        {
            Print(Loc.T("Cli_BadRegion"));
            return 2;
        }

        var rect = new Rectangle(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
        var merge = !args.Contains("--no-merge", StringComparer.OrdinalIgnoreCase);
        var copy = !args.Contains("--no-copy", StringComparer.OrdinalIgnoreCase);
        var outPath = GetOption(args, "--out");
        var engineName = GetOption(args, "--engine") ?? "auto";

        try
        {
            using var bitmap = ScreenCapture.CaptureRegion(rect);
            var settings = new AppSettings { Engine = engineName };
            using var ocr = new OcrService(settings);

            var started = Environment.TickCount64;
            var outcome = Task.Run(() => ocr.RecognizeAsync(bitmap)).GetAwaiter().GetResult();
            var elapsed = Environment.TickCount64 - started;
            var text = UrlHelper.Normalize(PostProcessor.Clean(outcome.Text, merge));

            var copied = copy && ClipboardService.TrySetText(text);

            if (!string.IsNullOrEmpty(outPath))
                File.WriteAllText(outPath!, text, new UTF8Encoding(false));

            Print($"engine={ocr.Name} elapsed={elapsed}ms chars={text.Length} copied={copied} conf={FormatConfidence(outcome.Confidence)}");
            if (string.IsNullOrEmpty(outPath))
                Print(text);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("CLI --grab-region failed", ex);
            Print("FAILED: " + ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// 对多张图跑"连拍选优"逻辑：逐帧 OCR → 按置信度/完整度打分 → 输出最优的一帧文本。
    /// 与托盘里的连拍取字共用同一套逻辑，便于自动化验证。
    /// </summary>
    private static int RunOcrBurst(string[] args)
    {
        var paths = GetPositional(args, "--out", "--engine").ToArray();
        if (paths.Length == 0)
        {
            Print(Loc.T("Cli_Usage_OcrBurst"));
            return 2;
        }

        var merge = !args.Contains("--no-merge", StringComparer.OrdinalIgnoreCase);
        var outPath = GetOption(args, "--out");
        var engineName = GetOption(args, "--engine") ?? "auto";

        var bitmaps = new List<Bitmap>();
        try
        {
            foreach (var path in paths)
            {
                if (!File.Exists(path)) { Print(Loc.T("Cli_FileNotFound", path)); return 2; }
                bitmaps.Add(new Bitmap(path));
            }

            var settings = new AppSettings { Engine = engineName };
            using var ocr = new OcrService(settings);

            var started = Environment.TickCount64;
            var outcome = Task.Run(() => BurstRunner.SelectBestAsync(bitmaps, ocr)).GetAwaiter().GetResult();
            var elapsed = Environment.TickCount64 - started;
            var text = UrlHelper.Normalize(PostProcessor.Clean(outcome.Text, merge));

            if (!string.IsNullOrEmpty(outPath))
                File.WriteAllText(outPath!, text, new UTF8Encoding(false));

            Print($"frames={outcome.InputFrames} ocr={outcome.OcrFrames} agree={outcome.AgreementCount} " +
                  $"elapsed={elapsed}ms chars={text.Length} conf={FormatConfidence(outcome.Confidence)}");
            if (string.IsNullOrEmpty(outPath))
                Print(text);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("CLI --ocr-burst failed", ex);
            Print("FAILED: " + ex.Message);
            return 1;
        }
        finally
        {
            foreach (var bitmap in bitmaps)
                bitmap.Dispose();
        }
    }

    /// <summary>对屏幕固定区域做「自适应多帧取字」：画面静止只识别一帧，画面在动则多帧选优（供脚本与自动化验证）。</summary>
    private static int RunOcrAdaptive(string[] args)
    {
        var spec = GetPositional(args, "--out", "--engine").FirstOrDefault();
        if (string.IsNullOrEmpty(spec))
        {
            Print(Loc.T("Cli_Usage_OcrAdaptive"));
            return 2;
        }

        var parts = spec.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !parts.All(p => int.TryParse(p, out _)))
        {
            Print(Loc.T("Cli_BadRegion"));
            return 2;
        }

        var rect = new Rectangle(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
        var merge = !args.Contains("--no-merge", StringComparer.OrdinalIgnoreCase);
        var copy = !args.Contains("--no-copy", StringComparer.OrdinalIgnoreCase);
        var outPath = GetOption(args, "--out");
        var engineName = GetOption(args, "--engine") ?? "auto";

        try
        {
            var settings = new AppSettings { Engine = engineName };
            using var ocr = new OcrService(settings);

            var started = Environment.TickCount64;
            var outcome = Task.Run(() => BurstRunner.RunAdaptiveAsync(rect, ocr)).GetAwaiter().GetResult();
            var elapsed = Environment.TickCount64 - started;
            var text = UrlHelper.Normalize(PostProcessor.Clean(outcome.Text, merge));

            var copied = copy && ClipboardService.TrySetText(text);
            if (!string.IsNullOrEmpty(outPath))
                File.WriteAllText(outPath!, text, new UTF8Encoding(false));

            Print($"frames={outcome.InputFrames} ocr={outcome.OcrFrames} agree={outcome.AgreementCount} " +
                  $"elapsed={elapsed}ms chars={text.Length} copied={copied} conf={FormatConfidence(outcome.Confidence)}");
            if (string.IsNullOrEmpty(outPath))
                Print(text);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("CLI --ocr-adaptive failed", ex);
            Print("FAILED: " + ex.Message);
            return 1;
        }
    }

    /// <summary>识别图片里的二维码/条码，输出其内容（可用于脚本）。</summary>
    private static int RunScan(string[] args)
    {
        var path = GetPositional(args, "--out").FirstOrDefault();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Print(Loc.T("Cli_Usage_Scan"));
            return 2;
        }

        try
        {
            using var bitmap = new Bitmap(path);
            var started = Environment.TickCount64;
            var content = CodeScanner.TryDecode(bitmap);
            var elapsed = Environment.TickCount64 - started;

            if (string.IsNullOrEmpty(content))
            {
                Print($"no code found ({elapsed}ms)");
                return 3;
            }

            var outPath = GetOption(args, "--out");
            if (!string.IsNullOrEmpty(outPath))
                File.WriteAllText(outPath!, content, new UTF8Encoding(false));

            Print($"code={content} ({elapsed}ms)");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("CLI --scan failed", ex);
            Print("FAILED: " + ex.Message);
            return 1;
        }
    }

    /// <summary>把文本生成二维码 PNG（顺手提供的小工具，也用于自测）。</summary>
    private static int RunQr(string[] args)
    {
        var text = GetPositional(args, "--out", "--size").FirstOrDefault();
        if (string.IsNullOrEmpty(text))
        {
            Print(Loc.T("Cli_Usage_Qr"));
            return 2;
        }

        var outPath = GetOption(args, "--out")
                      ?? Path.Combine(Path.GetTempPath(), $"SnapText-qr-{DateTime.Now:HHmmss}.png");
        var size = int.TryParse(GetOption(args, "--size"), out var parsed) && parsed is >= 64 and <= 2048 ? parsed : 300;

        try
        {
            var writer = new ZXing.Windows.Compatibility.BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = new QrCodeEncodingOptions
                {
                    Width = size,
                    Height = size,
                    Margin = 2,
                    CharacterSet = "UTF-8"
                }
            };

            using var bitmap = writer.Write(text);
            bitmap.Save(outPath, ImageFormat.Png);
            Print($"OK -> {outPath} ({size}x{size})");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("CLI --qr failed", ex);
            Print("FAILED: " + ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// 取字历史（供脚本与自动化测试）：list / search / add / clear。
    /// 与应用共用同一存储（默认 %LOCALAPPDATA%\SnapText\history.jsonl）；
    /// 测试可用环境变量 SNAPTEXT_HISTORY_FILE 指向临时文件、SNAPTEXT_HISTORY_MAX 缩小条数上限，不动真实历史。
    /// </summary>
    private static int RunHistory(string[] args)
    {
        var verb = args.Length > 1 ? args[1].ToLowerInvariant() : "";
        var store = new HistoryStore();
        var outPath = GetOption(args, "--out");
        var limit = int.TryParse(GetOption(args, "--n"), out var n) && n > 0 ? n : int.MaxValue;

        switch (verb)
        {
            case "list":
            {
                PrintHistory(store, store.Load().Take(limit).ToList(), outPath, "HISTORY_COUNT");
                return 0;
            }
            case "search":
            {
                var query = GetPositional(args, 2, "--out", "--n").FirstOrDefault();
                if (string.IsNullOrEmpty(query))
                {
                    Print(Loc.T("Cli_Usage_HistorySearch"));
                    return 2;
                }

                var hits = store.Load()
                    .Where(e => e.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(limit)
                    .ToList();
                PrintHistory(store, hits, outPath, "HISTORY_MATCH");
                return 0;
            }
            case "add":
            {
                var text = GetPositional(args, 2, "--out").FirstOrDefault();
                if (string.IsNullOrEmpty(text))
                {
                    Print(Loc.T("Cli_Usage_HistoryAdd"));
                    return 2;
                }

                var added = store.Add(text, "cli");
                Print(added ? Loc.T("Cli_HistoryAdded") : Loc.T("Cli_HistoryDuplicate"));
                return 0;
            }
            case "clear":
                store.Clear();
                Print(Loc.T("Cli_HistoryCleared"));
                return 0;
            default:
                Print(Loc.T("Cli_Usage_HistoryVerbs"));
                return 2;
        }
    }

    /// <summary>校验三语言文案表（zh/ja/en）键集合一致；缺键则列出并返回 1（供回归脚本/CI）。</summary>
    private static int RunLocCheck(string[] args)
    {
        var verb = args.Length > 1 ? args[1].ToLowerInvariant() : "";
        if (verb != "check")
        {
            Print(Loc.T("Cli_Usage_LocVerbs"));
            return 2;
        }

        var missing = Loc.FindMissingKeys();
        Print($"LANG: {Loc.Language}");
        Print($"LOC_TOTAL: {Loc.KeyCount}");
        Print($"LOC_MISSING: {missing.Count}");
        foreach (var item in missing)
            Print("missing " + item);

        if (missing.Count > 0)
            Print(Loc.T("Cli_LocMissing", string.Join(", ", missing)));

        Print(missing.Count == 0 ? "LOC_RESULT: PASS" : "LOC_RESULT: FAIL");
        return missing.Count == 0 ? 0 : 1;
    }

    /// <summary>打印历史条目（时间 | 来源 | 单行文本）；--out 时同时写入 UTF-8 文本；末行为机器可读统计。</summary>
    private static void PrintHistory(HistoryStore store, List<HistoryEntry> entries, string? outPath, string countLabel)
    {
        var lines = entries
            .Select(e => $"{e.Time:yyyy-MM-dd HH:mm:ss}\t[{e.Source}]\t{HistoryStore.ToSingleLine(e.Text, 500)}")
            .ToList();

        if (!string.IsNullOrEmpty(outPath))
            File.WriteAllText(outPath!, string.Join(Environment.NewLine, lines), new UTF8Encoding(false));

        Print(Loc.T("Cli_HistoryFile", store.FilePath));
        foreach (var line in lines)
            Print(line);
        Print($"{countLabel}: {entries.Count}");
        Print($"HISTORY_CAP: {store.MaxEntries}");
    }

    /// <summary>
    /// 取位置参数（跳过 --flag 以及带取值的 --flag 的取值）。
    /// 之前用「第一个不以 -- 开头」的做法，会把 --out 的文件名误当成输入。
    /// </summary>
    private static List<string> GetPositional(string[] args, params string[] valueFlags) =>
        GetPositional(args, 1, valueFlags);

    /// <summary>从 startIndex 起取位置参数（子命令用，例如 --history search &lt;关键词&gt;）。</summary>
    private static List<string> GetPositional(string[] args, int startIndex, params string[] valueFlags)
    {
        var flags = new HashSet<string>(valueFlags, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        for (var i = startIndex; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                if (flags.Contains(args[i]))
                    i++;   // 跳过该选项的取值
                continue;
            }
            result.Add(args[i]);
        }

        return result;
    }

    private static string FormatConfidence(double? confidence) =>
        confidence is null ? "n/a" : confidence.Value.ToString("F3");

    private static string? GetOption(string[] args, string name)
    {
        var index = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void Print(string message)
    {
        try
        {
            Win32.AttachConsole(-1); // ATTACH_PARENT_PROCESS，有控制台时输出到父控制台（尽力而为）
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            Console.WriteLine(message);
        }
        catch
        {
            // 没有可用的控制台时忽略；结果仍然写入 --out 文件
        }
    }
}
