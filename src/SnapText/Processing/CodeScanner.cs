using System.Drawing;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace SnapText.Processing;

/// <summary>
/// 二维码 / 条码识别（视频里常见的"扫码关注""扫码看链接"）。
/// 与 OCR 并行执行，不增加取字延迟；识别到码时以码的内容为准。
/// </summary>
internal static class CodeScanner
{
    public static string? TryDecode(Bitmap bitmap)
    {
        try
        {
            // 小图放大后更容易解出码（视频里的码通常只有几十像素）
            using var prepared = Ocr.OcrPreprocess.Prepare(bitmap);

            var reader = new BarcodeReader
            {
                AutoRotate = true,
                Options = new DecodingOptions
                {
                    TryHarder = true,
                    TryInverted = true,
                    PossibleFormats = new List<BarcodeFormat>
                    {
                        BarcodeFormat.QR_CODE,
                        BarcodeFormat.DATA_MATRIX,
                        BarcodeFormat.AZTEC,
                        BarcodeFormat.PDF_417,
                        BarcodeFormat.CODE_128,
                        BarcodeFormat.CODE_39,
                        BarcodeFormat.CODE_93,
                        BarcodeFormat.EAN_13,
                        BarcodeFormat.EAN_8,
                        BarcodeFormat.UPC_A,
                        BarcodeFormat.UPC_E,
                        BarcodeFormat.ITF,
                        BarcodeFormat.CODABAR
                    }
                }
            };

            var result = reader.Decode(prepared);
            var text = result?.Text?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex)
        {
            Log.Warn("Barcode decode failed: " + ex.Message);
            return null;
        }
    }
}
