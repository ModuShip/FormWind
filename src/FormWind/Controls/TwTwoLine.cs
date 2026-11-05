using System;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// The value of a <see cref="TwTwoLineColumn"/> cell: a bold title, a muted second line and an optional status dot.
/// <code>
/// row.Cells[0].Value = new TwTwoLine("PR-2041", "Press 3 · spindle bearing") { DotColor = Color.Red };
/// </code>
/// </summary>
public sealed class TwTwoLine : IComparable
{
    public TwTwoLine()
    {
    }

    public TwTwoLine(string? title, string? subtitle = null)
    {
        Title = title ?? "";
        Subtitle = subtitle;
    }

    public string Title { get; set; } = "";

    /// <summary>Muted line under the title; <c>null</c> or empty = a single line.</summary>
    public string? Subtitle { get; set; }

    /// <summary>A status dot before the title (empty = none).</summary>
    public Color DotColor { get; set; } = Color.Empty;

    /// <summary>Sorting by a column of these compares the titles, then the subtitles (culture-aware, ignoring case).</summary>
    public int CompareTo(object? obj)
    {
        if (obj is not TwTwoLine other) return 1;
        int result = string.Compare(Title, other.Title, StringComparison.CurrentCultureIgnoreCase);
        return result != 0 ? result : string.Compare(Subtitle, other.Subtitle, StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>What copy and screen readers get: the title and the subtitle joined with a comma.</summary>
    public override string ToString() => string.IsNullOrEmpty(Subtitle) ? Title : Title + ", " + Subtitle;
}

/// <summary>
/// Draws "bold title + muted second line (+ status dot)", shared by <see cref="TwListItem"/> rows, <see cref="TwTwoLineColumn"/>
/// cells and the tree column of <see cref="TwGridTree"/>, so the three look the same.
/// </summary>
internal static class TwTwoLinePainter
{
    private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    public static int LineHeight(Font font) => TextRenderer.MeasureText("Ag", font, Size.Empty, Flags).Height;

    public static int Gap(float scale) => Math.Max(1, (int)Math.Round(2 * scale));

    /// <summary>Height in pixels of the two lines together (without padding).</summary>
    public static int TextHeight(Font titleFont, Font subtitleFont, float scale) =>
        LineHeight(titleFont) + LineHeight(subtitleFont) + Gap(scale);

    /// <summary>
    /// Draws the title and, when given, the second line under it; the two are centered vertically in <paramref name="area"/>.
    /// A single line is centered. <paramref name="alignRight"/> right-aligns the text (right-to-left layouts).
    /// </summary>
    public static void Paint(Graphics g, Rectangle area, string? main, Font mainFont, Color mainColor, string? sub, Font subFont, Color subColor,
                             Color back, bool alignRight, float scale)
    {
        if (area.Width <= 0 || area.Height <= 0) return;
        if (string.IsNullOrEmpty(sub))
        {
            DrawLine(g, main, area, mainFont, mainColor, back, alignRight ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft);
            return;
        }
        TwoLines(area, mainFont, subFont, scale, out var first, out var second);
        var alignment = alignRight ? ContentAlignment.TopRight : ContentAlignment.TopLeft;
        DrawLine(g, main, first, mainFont, mainColor, back, alignment);
        DrawLine(g, sub, second, subFont, subColor, back, alignment);
    }

    /// <summary>The two lines' rectangles, centered vertically in <paramref name="area"/> as a pair.</summary>
    public static void TwoLines(Rectangle area, Font mainFont, Font subFont, float scale, out Rectangle first, out Rectangle second)
    {
        int top = LineHeight(mainFont), bottom = LineHeight(subFont), gap = Gap(scale);
        int y = area.Y + (area.Height - top - bottom - gap) / 2;
        first = new Rectangle(area.X, y, area.Width, top);
        second = new Rectangle(area.X, y + top + gap, area.Width, bottom);
    }

    public static void DrawLine(Graphics g, string? text, Rectangle bounds, Font font, Color color, Color back, ContentAlignment alignment)
    {
        if (string.IsNullOrEmpty(text)) return;
        var flags = TwPainter.ToTextFormatFlags(alignment) | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, text, font, bounds, color, back, flags);
    }

    /// <summary>The 8 px status dot, centered vertically in <paramref name="area"/> and placed at its leading edge.</summary>
    public static void DrawDot(Graphics g, Rectangle area, bool rtl, Color color, float scale)
    {
        int d = (int)Math.Round(8 * scale);
        TwPainter.FillRounded(g, new Rectangle(rtl ? area.Right - d : area.X, area.Y + (area.Height - d) / 2, d, d), d / 2f, color);
    }

    /// <summary>Width of the dot with the gap after it.</summary>
    public static int DotWidth(float scale) => (int)Math.Round(8 * scale) + (int)Math.Round(10 * scale);
}
