using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SnapText.Ocr;

/// <summary>位图像素的整块拷贝：一次 LockBits 替代逐像素 GetPixel / 整图 PNG 编解码。</summary>
internal static class BitmapPixels
{
    /// <summary>按 BGRA 平铺拷出全部像素。按 32bpp 请求时 GDI+ 负责格式转换，Stride 恒等于宽×4（无行尾填充）。</summary>
    public static byte[] CopyBgra(Bitmap image)
    {
        var rect = new Rectangle(0, 0, image.Width, image.Height);
        var data = image.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[data.Stride * image.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            image.UnlockBits(data);
        }
    }
}
