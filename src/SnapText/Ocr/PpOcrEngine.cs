using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using RapidOcrNet;
using SkiaSharp;

namespace SnapText.Ocr;

/// <summary>
/// PP-OCRv5 本地 ONNX 引擎（RapidOcrNet）：
/// 检测/方向模型随 NuGet 包分发，中文识别模型（rec + dict）在 models/v5/ 下随程序分发，全程离线。
/// 会话懒加载，空闲 60 秒自动释放内存。
/// </summary>
internal sealed class PpOcrEngine : IOcrEngine, IDisposable
{
    private readonly object _gate = new();
    private RapidOcr? _session;
    private Timer? _idleTimer;
    private bool _unavailable;

    public string Name => "PP-OCRv5 本地";

    public static string ModelsDir => Path.Combine(AppContext.BaseDirectory, "models", "v5");

    private static string RecPath => Path.Combine(ModelsDir, "ch_PP-OCRv5_rec_mobile.onnx");
    private static string DictPath => Path.Combine(ModelsDir, "ppocrv5_dict.txt");

    public bool IsAvailable
    {
        get
        {
            if (_unavailable) return false;
            try
            {
                return File.Exists(FindByToken("det"))
                       && File.Exists(FindByToken("cls"))
                       && File.Exists(RecPath)
                       && File.Exists(DictPath);
            }
            catch
            {
                return false;
            }
        }
    }

    public Task<OcrOutcome> RecognizeAsync(Bitmap image, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var prepared = OcrPreprocess.Prepare(image);
            using var skBitmap = ToSkBitmap(prepared)
                                 ?? throw new InvalidOperationException("OCR 预处理失败（位图转换）。");

            lock (_gate)
            {
                var session = EnsureSessionLocked();
                RestartIdleTimerLocked();

                var result = session.Detect(skBitmap, RapidOcrOptions.Default);

                var blocks = result.TextBlocks
                    .Where(block => !string.IsNullOrWhiteSpace(block.Text))
                    .ToList();

                var text = string.Join("\n", blocks.Select(block => block.Text));

                // 逐字置信度均值：用于"识别可能不准"的轻提示；拿不到分数则为 null
                var scores = new List<double>();
                foreach (var block in blocks)
                {
                    var charScores = block.CharScores;
                    if (charScores is null) continue;
                    foreach (var score in charScores)
                        scores.Add(score);
                }

                var confidence = scores.Count > 0 ? scores.Average() : (double?)null;
                return new OcrOutcome(text, confidence);
            }
        }, cancellationToken);
    }

    private RapidOcr EnsureSessionLocked()
    {
        if (_session is not null) return _session;

        if (!IsAvailable)
        {
            _unavailable = true;
            throw new InvalidOperationException($"PP-OCR 模型缺失：{ModelsDir}");
        }

        try
        {
            var session = new RapidOcr();
            session.InitModels(FindByToken("det"), FindByToken("cls"), RecPath, DictPath);
            _session = session;
            Log.Info($"PP-OCR session initialized (models: {ModelsDir}).");
            return session;
        }
        catch (Exception ex)
        {
            _unavailable = true;
            Log.Error("PP-OCR init failed", ex);
            throw;
        }
    }

    private void RestartIdleTimerLocked()
    {
        _idleTimer ??= new Timer(_ => UnloadIfIdle(), null, Timeout.Infinite, Timeout.Infinite);
        _idleTimer.Change(TimeSpan.FromSeconds(60), Timeout.InfiniteTimeSpan);
    }

    private void UnloadIfIdle()
    {
        lock (_gate)
        {
            if (_session is null) return;
            try { _session.Dispose(); } catch { }
            _session = null;
            Log.Info("PP-OCR session released (idle 60s).");
        }
    }

    private static string FindByToken(string token)
    {
        if (!Directory.Exists(ModelsDir))
            return Path.Combine(ModelsDir, "missing.onnx");

        var file = Directory.EnumerateFiles(ModelsDir, "*.onnx")
            .FirstOrDefault(f => Path.GetFileName(f).Contains(token, StringComparison.OrdinalIgnoreCase));
        return file ?? Path.Combine(ModelsDir, "missing.onnx");
    }

    private static SKBitmap? ToSkBitmap(Bitmap image)
    {
        using var memory = new MemoryStream();
        image.Save(memory, ImageFormat.Png);
        memory.Position = 0;
        return SKBitmap.Decode(memory);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _idleTimer?.Dispose();
            _idleTimer = null;
            try { _session?.Dispose(); } catch { }
            _session = null;
        }
    }
}
