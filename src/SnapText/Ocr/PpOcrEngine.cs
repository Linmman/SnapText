using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using RapidOcrNet;
using SkiaSharp;
using SnapText.Localization;

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

    public string Name => Loc.T("Ocr_PpLocal");

    public static string ModelsDir => Path.Combine(AppContext.BaseDirectory, "models", "v5");

    // 模型文件名显式写死：不按"文件名包含 det/cls"模糊匹配，避免目录里文件一多选错模型
    private static string DetPath => Path.Combine(ModelsDir, "ch_PP-OCRv5_mobile_det.onnx");
    private static string ClsPath => Path.Combine(ModelsDir, "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx");
    private static string RecPath => Path.Combine(ModelsDir, "ch_PP-OCRv5_rec_mobile.onnx");
    private static string DictPath => Path.Combine(ModelsDir, "ppocrv5_dict.txt");

    public bool IsAvailable
    {
        get
        {
            if (_unavailable) return false;
            try
            {
                return File.Exists(DetPath)
                       && File.Exists(ClsPath)
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
            using var skBitmap = ToSkBitmap(prepared);

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
            throw new InvalidOperationException(Loc.T("Err_ModelMissing", ModelsDir));
        }

        try
        {
            var session = new RapidOcr();
            session.InitModels(DetPath, ClsPath, RecPath, DictPath);
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

    /// <summary>按像素内存布局直接构造 SKBitmap（Format32bppArgb 的内存字节序即 BGRA），省掉老实现 Save→Decode 的一整轮 PNG 编解码。</summary>
    private static SKBitmap ToSkBitmap(Bitmap image)
    {
        try
        {
            var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            var skBitmap = new SKBitmap(info);
            var pixels = BitmapPixels.CopyBgra(image);
            Marshal.Copy(pixels, 0, skBitmap.GetPixels(), pixels.Length);
            return skBitmap;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(Loc.T("Err_Preprocess"), ex);
        }
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
