using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FormWind;

/// <summary>
/// Draws icon glyphs independently of font metrics. The line height of icon fonts does not match the glyph size
/// (Segoe Fluent Icons measures 11 px tall at 15 pt), so text drawing clips the icon; here the glyph is converted
/// to a path and centered in the box by its actual ink bounds.
/// </summary>
internal static class TwIconRenderer
{
    /// <summary>Square icon box for a font size (as on the web: 20 px font = 20 px icon).</summary>
    /// <remarks>
    /// GDI fonts convert points to pixels with the process's system DPI; under PerMonitorV2 the font of a control on a screen
    /// with another DPI is already enlarged, so the icon is computed with the system DPI too. <paramref name="dpi"/> is no
    /// longer used (kept for existing callers).
    /// </remarks>
    public static int EmPixels(float sizeInPoints, int dpi) =>
        Math.Max(1, (int)Math.Round(sizeInPoints * TwDpi.SystemDpi / 72f));

    /// <summary>
    /// Draws a named icon: the theme's SVG icon first, otherwise the glyph in the icon font. <c>false</c> if the icon is not found.
    /// </summary>
    public static bool DrawIcon(Graphics graphics, string? name, int emPixels, Rectangle bounds, Color color, TwTheme? theme = null)
    {
        theme ??= TwTheme.Current;
        if (theme.TryGetSvgIcon(name, out var svg))
        {
            svg!.Draw(graphics, emPixels, bounds, color);
            return true;
        }
        if (!theme.TryGetIcon(name, out var glyph)) return false;
        Draw(graphics, glyph, theme.IconFontFamily, emPixels, bounds, color);
        return true;
    }

    public static void Draw(Graphics graphics, char glyph, string fontFamily, int emPixels, Rectangle bounds, Color color)
    {
        if (color.A == 0 || bounds.Width <= 0 || bounds.Height <= 0) return;

        FontFamily family;
        try
        {
            family = new FontFamily(fontFamily);
        }
        catch (ArgumentException)
        {
            return; // ikon fontu kurulu değil
        }

        using (family)
        using (var path = new GraphicsPath())
        {
            path.AddString(glyph.ToString(), family, (int)FontStyle.Regular, emPixels, PointF.Empty, StringFormat.GenericTypographic);
            var ink = path.GetBounds();
            if (ink.Width <= 0 || ink.Height <= 0) return;

            // Kare em kutusunu bounds içinde ortala, glyph'in mürekkebini de em kutusunun ortasına taşı.
            float boxX = bounds.X + (bounds.Width - emPixels) / 2f;
            float boxY = bounds.Y + (bounds.Height - emPixels) / 2f;
            float dx = boxX + (emPixels - ink.Width) / 2f - ink.X;
            float dy = boxY + (emPixels - ink.Height) / 2f - ink.Y;

            using (var matrix = new Matrix())
            {
                matrix.Translate(dx, dy);
                path.Transform(matrix);
            }

            var state = graphics.Save();
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using (var brush = new SolidBrush(color))
                graphics.FillPath(brush, path);
            graphics.Restore(state);
        }
    }
}
