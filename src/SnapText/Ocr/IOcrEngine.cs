using System.Drawing;

namespace SnapText.Ocr;

/// <summary>OCR 结果。Confidence 仅在引擎能提供逐字置信度时非空（PP-OCR 有，系统 OCR 没有）。</summary>
internal sealed record OcrOutcome(string Text, double? Confidence = null);

internal interface IOcrEngine
{
    string Name { get; }

    bool IsAvailable { get; }

    Task<OcrOutcome> RecognizeAsync(Bitmap image, CancellationToken cancellationToken = default);
}
