using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using SnapText.Localization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

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

        using var memory = new MemoryStream();
        prepared.Save(memory, ImageFormat.Png);
        var bytes = memory.ToArray();

        using var stream = new InMemoryRandomAccessStream();
        var writer = new DataWriter(stream);
        writer.WriteBytes(bytes);
        await writer.StoreAsync();
        await writer.FlushAsync();
        writer.DetachStream();
        writer.Dispose();
        stream.Seek(0);

        cancellationToken.ThrowIfCancellationRequested();

        var decoder = await BitmapDecoder.CreateAsync(stream);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(softwareBitmap);

        cancellationToken.ThrowIfCancellationRequested();

        var lines = result.Lines.Select(l => l.Text).Where(t => !string.IsNullOrWhiteSpace(t));
        // 系统 OCR 不提供逐字置信度
        return new OcrOutcome(string.Join("\n", lines), Confidence: null);
    }
}
