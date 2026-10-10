using System.Drawing;
using System.Drawing.Imaging;
using SnapText.Interop;

namespace SnapText.Capture;

/// <summary>一次完整的多屏冻结截图（虚拟桌面坐标系，物理像素）。</summary>
internal sealed class ScreenCapture : IDisposable
{
    public Bitmap Bitmap { get; }
    public int VirtualX { get; }
    public int VirtualY { get; }

    private ScreenCapture(Bitmap bitmap, int x, int y)
    {
        Bitmap = bitmap;
        VirtualX = x;
        VirtualY = y;
    }

    /// <summary>抓取整个虚拟桌面（所有显示器）。</summary>
    public static ScreenCapture CaptureAll()
    {
        var x = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
        var y = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);
        var w = Math.Max(1, Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN));
        var h = Math.Max(1, Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN));

        var bitmap = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(x, y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
        }

        return new ScreenCapture(bitmap, x, y);
    }

    /// <summary>只抓取某个物理坐标区域（用于「重复上次区域」）。</summary>
    public static Bitmap CaptureRegion(Rectangle physicalRect)
    {
        var w = Math.Max(1, physicalRect.Width);
        var h = Math.Max(1, physicalRect.Height);
        var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(physicalRect.X, physicalRect.Y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    /// <summary>从冻结帧里裁剪选中区域（虚拟桌面坐标 → 位图内坐标）。</summary>
    public Bitmap Crop(Rectangle virtualRect)
    {
        var local = new Rectangle(
            virtualRect.X - VirtualX,
            virtualRect.Y - VirtualY,
            Math.Max(1, virtualRect.Width),
            Math.Max(1, virtualRect.Height));

        // 允许越界一点点（鼠标可能拖出桌面），做一次裁剪
        var clamped = Rectangle.Intersect(local, new Rectangle(0, 0, Bitmap.Width, Bitmap.Height));
        if (clamped.Width <= 0) clamped = new Rectangle(Math.Max(0, local.X), Math.Max(0, local.Y), 1, 1);

        // 目标尺寸必须用 clamped：若用 local（未裁剪）尺寸，选区越界时 DrawImage 会把
        // 源区域拉伸填满更大的位图，OCR 结果跟着失真
        var output = new Bitmap(clamped.Width, clamped.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(output);
        g.DrawImage(Bitmap, new Rectangle(0, 0, clamped.Width, clamped.Height), clamped, GraphicsUnit.Pixel);
        return output;
    }

    public void Dispose() => Bitmap.Dispose();
}
