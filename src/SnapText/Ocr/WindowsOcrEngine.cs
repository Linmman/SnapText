using System.Drawing;
using System.Runtime.InteropServices.WindowsRuntime;
using SnapText.Localization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace SnapText.Ocr;

/// <summary>
/// Windows 系统自带 OCR（Windows.Media.Ocr）：零模型、速度快；
/// 中文需要系统安装“光学字符识别”语言包，视频/艺术字场景精度一般。
/// </summary>
internal sealed class WindowsOcrEngine : IOcrEngine
{
    private OcrEngine? _engine;
    private bool _resolved;

    public string Name => Loc.T("Ocr_Windows");

    public bool IsAvailable => Resolve() is not null;

    private OcrEngine? Resolve()
    {
        if (_resolved) return _engine;
        _resolved = true;

        try
        {
            var languages = OcrEngine.AvailableRecognizerLanguages;
            Log.Info("Windows OCR languages: " + string.Join(", ", languages.Select(l => l.LanguageTag)));

            var best = languages.FirstOrDefault(l => l.LanguageTag.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase))
                       ?? languages.FirstOrDefault(l => l.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                       ?? languages.FirstOrDefault(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase));

            if (best is not null)
            {
                _engine = OcrEngine.TryCreateFromLanguage(best);
                if (_engine is not null)
                {
                    Log.Info($"Windows OCR engine created: {best.LanguageTag}");
                    return _engine;
                }
            }

            _engine = OcrEngine.TryCreateFromUserProfileLanguages();
            Log.Info(_engine is null
                ? "Windows OCR engine unavailable."
                : "Windows OCR engine created from user profile languages.");
        }
        catch (Exception ex)
        {
            Log.Error("Windows OCR init failed", ex);
            _engine = null;
        }

        return _engine;
    }

    public async Task<OcrOutcome> RecognizeAsync(Bitmap image, CancellationToken cancellationToken = default)
    {
        var engine = Resolve() ?? throw new InvalidOperationException(Loc.T("WindowsOcr_Unavailable"));

        using var prepared = OcrPreprocess.Prepare(image);

        // 直接从像素数据构造 SoftwareBitmap（一次内存拷贝），省掉老实现 Save→Decode 的一整轮 PNG 编解码
        var pixels = BitmapPixels.CopyBgra(prepared);
        var softwareBitmap = SoftwareBitmap.CreateCopyFromBuffer(
            pixels.AsBuffer(),
            BitmapPixelFormat.Bgra8,
            prepared.Width,
            prepared.Height,
            BitmapAlphaMode.Straight);

        cancellationToken.ThrowIfCancellationRequested();

        var result = await engine.RecognizeAsync(softwareBitmap);

        cancellationToken.ThrowIfCancellationRequested();

        var lines = result.Lines.Select(l => l.Text).Where(t => !string.IsNullOrWhiteSpace(t));
        // 系统 OCR 不提供逐字置信度
        return new OcrOutcome(string.Join("\n", lines), Confidence: null);
    }
}
