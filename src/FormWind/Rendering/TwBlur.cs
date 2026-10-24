using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FormWind;

/// <summary>A fast blur (three box passes ≈ Gaussian) for <c>backdrop-blur-*</c> surfaces.</summary>
internal static class TwBlur
{
    /// <summary>Blurs a 32 bpp ARGB bitmap in place; <paramref name="radius"/> is the CSS blur radius in pixels.</summary>
    public static void Apply(Bitmap bitmap, float radius)
    {
        if (bitmap is null) throw new ArgumentNullException(nameof(bitmap));
        int w = bitmap.Width, h = bitmap.Height;
        if (radius < 0.5f || w == 0 || h == 0) return;
        // Üç kutu geçişi standart sapması radius/2 olan bir Gauss'a yaklaşır (CSS blur() yarıçapı σ'dır; yarısı yeterince yakındır).
        int box = Math.Max(1, (int)Math.Round(radius / 2));
        var rect = new Rectangle(0, 0, w, h);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride / 4;
            var pixels = new int[stride * h];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            var temp = new int[pixels.Length];
            for (int pass = 0; pass < 3; pass++)
            {
                Horizontal(pixels, temp, w, h, stride, box);
                Vertical(temp, pixels, w, h, stride, box);
            }
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    // Kayan toplamla kutu ortalaması; kenarlarda kenar pikseli tekrarlanır (görüntünün dışı karartılmaz).
    private static void Horizontal(int[] source, int[] target, int w, int h, int stride, int r)
    {
        float scale = 1f / (r * 2 + 1);
        for (int y = 0; y < h; y++)
        {
            int row = y * stride;
            int a = 0, red = 0, green = 0, blue = 0;
            for (int i = -r; i <= r; i++) Add(source[row + Clamp(i, w)], ref a, ref red, ref green, ref blue, 1);
            for (int x = 0; x < w; x++)
            {
                target[row + x] = Pack(a * scale, red * scale, green * scale, blue * scale);
                Add(source[row + Clamp(x - r, w)], ref a, ref red, ref green, ref blue, -1);
                Add(source[row + Clamp(x + r + 1, w)], ref a, ref red, ref green, ref blue, 1);
            }
        }
    }

    private static void Vertical(int[] source, int[] target, int w, int h, int stride, int r)
    {
        float scale = 1f / (r * 2 + 1);
        for (int x = 0; x < w; x++)
        {
            int a = 0, red = 0, green = 0, blue = 0;
            for (int i = -r; i <= r; i++) Add(source[Clamp(i, h) * stride + x], ref a, ref red, ref green, ref blue, 1);
            for (int y = 0; y < h; y++)
            {
                target[y * stride + x] = Pack(a * scale, red * scale, green * scale, blue * scale);
                Add(source[Clamp(y - r, h) * stride + x], ref a, ref red, ref green, ref blue, -1);
                Add(source[Clamp(y + r + 1, h) * stride + x], ref a, ref red, ref green, ref blue, 1);
            }
        }
    }

    private static int Clamp(int value, int length) => value < 0 ? 0 : value >= length ? length - 1 : value;

    private static void Add(int pixel, ref int a, ref int r, ref int g, ref int b, int sign)
    {
        a += sign * ((pixel >> 24) & 0xff);
        r += sign * ((pixel >> 16) & 0xff);
        g += sign * ((pixel >> 8) & 0xff);
        b += sign * (pixel & 0xff);
    }

    private static int Pack(float a, float r, float g, float b) =>
        ((int)(a + 0.5f) << 24) | ((int)(r + 0.5f) << 16) | ((int)(g + 0.5f) << 8) | (int)(b + 0.5f);
}
