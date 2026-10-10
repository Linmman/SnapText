using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using SnapText.Capture;
using SnapText.Core;
using SnapText.Interop;
using ZXing;
using ZXing.QrCode;
using SnapText.Ocr;
using SnapText.Processing;
using SnapText.Settings;

namespace SnapText.Cli;

/// <summary>
/// 无界面命令行模式：
///   SnapText.exe --ocr &lt;图片&gt; [--out 文本.txt] [--no-merge]
///   SnapText.exe --capture [--out 截图.png]
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
            case "--qr":
                exitCode = RunQr(args);
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
            Print("用法: SnapText.exe --ocr <图片路径> [--engine auto|ppocr|windows] [--out <输出文本路径>] [--no-merge]");
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
            Print("用法: SnapText.exe --bench <图片路径> [--engine auto|ppocr|windows] [--n 10]");
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
            Print("OK 已通知正在运行的 SnapText 开始取字");
            return 0;
        }

        Print("FAILED: 没有正在运行的 SnapText 实例（先启动 SnapText.exe）");
        return 1;
    }

    /// <summary>对屏幕固定区域取字：截屏 → OCR → 剪贴板（可脚本化的固定区域取字）。</summary>
    private static int RunGrabRegion(string[] args)
    {
        var spec = GetPositional(args, "--out", "--engine").FirstOrDefault();
        if (string.IsNullOrEmpty(spec))
        {
            Print("用法: SnapText.exe --grab-region <x,y,w,h> [--engine auto|ppocr|windows] [--out <文本.txt>] [--no-copy] [--no-merge]");
            return 2;
        }

        var parts = spec.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !parts.All(p => int.TryParse(p, out _)))
        {
            Print("FAILED: 区域格式应为 x,y,w,h（物理像素）");
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
            Print("用法: SnapText.exe --ocr-burst <图1> [图2 ...] [--engine auto|ppocr|windows] [--out <文本.txt>]");
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
                if (!File.Exists(path)) { Print($"FAILED: 找不到 {path}"); return 2; }
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

    /// <summary>识别图片里的二维码/条码，输出其内容（可用于脚本）。</summary>
    private static int RunScan(string[] args)
    {
        var path = GetPositional(args, "--out").FirstOrDefault();
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            Print("用法: SnapText.exe --scan <图片路径> [--out <内容.txt>]");
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
            Print("用法: SnapText.exe --qr <文本> [--out <二维码.png>] [--size 300]");
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
    /// 取位置参数（跳过 --flag 以及带取值的 --flag 的取值）。
    /// 之前用「第一个不以 -- 开头」的做法，会把 --out 的文件名误当成输入。
    /// </summary>
    private static List<string> GetPositional(string[] args, params string[] valueFlags)
    {
        var flags = new HashSet<string>(valueFlags, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        for (var i = 1; i < args.Length; i++)
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
