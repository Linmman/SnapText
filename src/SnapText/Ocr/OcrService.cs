using System.Drawing;
using SnapText.Settings;

namespace SnapText.Ocr;

/// <summary>
/// 按设置选择 OCR 引擎：
///   auto   → 优先 PP-OCR 本地引擎，失败自动回落到系统 OCR；
///   ppocr  → 只用 PP-OCR；
///   windows→ 只用系统 OCR。
/// </summary>
internal sealed class OcrService : IOcrEngine, IDisposable
{
    private readonly WindowsOcrEngine _windows = new();
    private readonly PpOcrEngine _pp = new();
    private AppSettings _settings;

    public OcrService(AppSettings settings) => _settings = settings;

    public void UpdateSettings(AppSettings settings) => _settings = settings;

    public string Name => (_settings.Engine?.ToLowerInvariant()) switch
    {
        "windows" => _windows.Name,
        "ppocr" => _pp.Name,
        _ => _pp.IsAvailable ? $"{_pp.Name}（自动）" : $"{_windows.Name}（自动回落）"
    };

    public bool IsAvailable => _pp.IsAvailable || _windows.IsAvailable;

    public async Task<OcrOutcome> RecognizeAsync(Bitmap image, CancellationToken cancellationToken = default)
    {
        Exception? lastError = null;

        foreach (var engine in CreateEngineOrder())
        {
            try
            {
                var outcome = await engine.RecognizeAsync(image, cancellationToken);
                Log.Info($"OCR engine used: {engine.Name}");
                return outcome;
            }
            catch (Exception ex)
            {
                lastError = ex;
                Log.Error($"OCR engine failed: {engine.Name}", ex);
            }
        }

        throw lastError ?? new InvalidOperationException("没有可用的 OCR 引擎。");
    }

    private IEnumerable<IOcrEngine> CreateEngineOrder()
    {
        switch (_settings.Engine?.ToLowerInvariant())
        {
            case "windows":
                return new IOcrEngine[] { _windows };
            case "ppocr":
                return new IOcrEngine[] { _pp };
            default:
                return _pp.IsAvailable
                    ? new IOcrEngine[] { _pp, _windows }
                    : new IOcrEngine[] { _windows };
        }
    }

    public void Dispose() => _pp.Dispose();
}
