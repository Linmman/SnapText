using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SnapText.Ocr;

internal static class OcrPreprocess
{
    /// <summary>
    /// 小图放大能明显提升 OCR 准确率（视频字幕常常只有十几个像素高）。
    /// 返回的位图由调用方负责释放。
    /// </summary>
    public static Bitmap Prepare(Bitmap source)
    {
        var scale = source.Height < 64 ? 3f : source.Height < 150 ? 2f : 1f;

        while (scale > 1f && (source.Width * scale > 4096 || source.Height * scale > 4096))
            scale -= 0.5f;

        if (scale <= 1f)
            return new Bitmap(source);

        var target = new Bitmap(
            (int)Math.Round(source.Width * scale),
            (int)Math.Round(source.Height * scale),
            PixelFormat.Format32bppArgb);

        using var g = Graphics.FromImage(target);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(source, new Rectangle(0, 0, target.Width, target.Height));
        return target;
    }
}
