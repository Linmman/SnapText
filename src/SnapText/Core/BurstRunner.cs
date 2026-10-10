using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SnapText.Capture;
using SnapText.Ocr;

namespace SnapText.Core;

internal sealed record BurstOutcome(string Text, double? Confidence, int InputFrames, int OcrFrames, int AgreementCount);

/// <summary>
/// 自适应多帧取字：对同一区域连续采样几帧 → 跳过重复帧 → 逐帧 OCR → 选"最稳/最准"的一帧。
/// 画面静止时只识别一帧（和单帧取字一样快）；画面在动（播放中的视频）才多花几百毫秒换稳定结果。
/// 目的：视频播放中的单帧可能糊了、只抓到半截、或正好在切换，多帧里总有一帧是干净的。
/// </summary>
internal static class BurstRunner
{
    /// <summary>运动场景最多采样多少帧（间隔 ~90ms，采样窗口约 0.45 秒）。</summary>
    public const int FrameCount = 6;

    /// <summary>静止探针：两帧间隔；相同即视为画面静止。</summary>
    private const int ProbeIntervalMs = 80;

    private const int FrameIntervalMs = 90;
    private const double SameFrameAvgDiff = 6.0;   // 16×16 灰度平均差小于此值视为同一帧

    /// <summary>
    /// 自适应取字（「取字」与「重复上次区域」主流程用）：
    /// 先抓两帧探测——画面静止 → 只识别一帧；画面在动 → 继续采满帧数、去重后逐帧识别并选最优。
    /// </summary>
    /// <param name="onMultiFrame">探测到画面在动时回调一次（用于显示"多帧选优"进度提示）。</param>
    public static async Task<BurstOutcome> RunAdaptiveAsync(
        Rectangle region,
        IOcrEngine ocr,
        Action? onMultiFrame = null,
        CancellationToken cancellationToken = default)
    {
        var frames = new List<Bitmap>();
        try
        {
            frames.Add(ScreenCapture.CaptureRegion(region));
            await Task.Delay(ProbeIntervalMs, cancellationToken);
            frames.Add(ScreenCapture.CaptureRegion(region));

            if (!IsSameFrame(frames[0], frames[^1]))
            {
                // 画面在动：多帧的成本只在这种场景付出
                onMultiFrame?.Invoke();
                for (var i = frames.Count; i < FrameCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Delay(FrameIntervalMs, cancellationToken);
                    frames.Add(ScreenCapture.CaptureRegion(region));
                }
            }

            return await SelectBestAsync(frames, ocr, cancellationToken);
        }
        finally
        {
            foreach (var frame in frames)
                frame.Dispose();
        }
    }

    /// <summary>
    /// 从给定帧里选出最优文本（供 CLI 与自动化测试复用）。位图所有权仍归调用方。
    /// 打分：置信度优先（×100），字数只做次要权重——避免"认得少但很自信"的半截帧胜出。
    /// </summary>
    public static async Task<BurstOutcome> SelectBestAsync(
        IReadOnlyList<Bitmap> frames,
        IOcrEngine ocr,
        CancellationToken cancellationToken = default)
    {
        // 跳过与上一帧几乎相同的画面（视频暂停时只 OCR 一帧；重复帧不含新信息）
        var distinct = new List<Bitmap>();
        foreach (var frame in frames)
        {
            if (distinct.Count > 0 && IsSameFrame(distinct[^1], frame))
                continue;
            distinct.Add(frame);
        }

        var bestText = string.Empty;
        double? bestConfidence = null;
        var bestScore = double.MinValue;
        var texts = new List<string>();

        foreach (var frame in distinct)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var outcome = await ocr.RecognizeAsync(frame, cancellationToken);
            var text = outcome.Text?.Trim() ?? string.Empty;
            texts.Add(text);

            var score = Score(text, outcome.Confidence);
            if (score > bestScore)
            {
                bestScore = score;
                bestText = text;
                bestConfidence = outcome.Confidence;
            }
        }

        var normalizedBest = Normalize(bestText);
        var agreement = normalizedBest.Length == 0
            ? 0
            : texts.Count(t => Normalize(t) == normalizedBest);

        return new BurstOutcome(bestText, bestConfidence, frames.Count, distinct.Count, agreement);
    }

    private static double Score(string text, double? confidence)
    {
        if (string.IsNullOrWhiteSpace(text)) return -1;
        return (confidence ?? 0.9) * 100 + Math.Min(text.Length, 60);
    }

    private static string Normalize(string text) =>
        string.Concat(text.Where(c => !char.IsWhiteSpace(c)));

    private static bool IsSameFrame(Bitmap a, Bitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
            return false;

        using var smallA = Downscale(a);
        using var smallB = Downscale(b);

        long diff = 0;
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var pa = smallA.GetPixel(x, y);
                var pb = smallB.GetPixel(x, y);
                diff += Math.Abs(pa.R - pb.R) + Math.Abs(pa.G - pb.G) + Math.Abs(pa.B - pb.B);
            }
        }

        return diff / (16.0 * 16 * 3) < SameFrameAvgDiff;
    }

    private static Bitmap Downscale(Bitmap source)
    {
        var small = new Bitmap(16, 16, PixelFormat.Format24bppRgb);
        using var g = Graphics.FromImage(small);
        g.InterpolationMode = InterpolationMode.Bilinear;
        g.DrawImage(source, new Rectangle(0, 0, 16, 16));
        return small;
    }
}
