using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// A <see cref="TwListBox"/> row drawn with the built-in template: a status dot, an icon or a small score ring at the start,
/// a title with a muted line under it, and a value and a badge on the right. Unset parts take no room.
/// <code>
/// list.Items.Add(new TwListItem("Press 3 · spindle bearing", "Line 2 · 6 stops in 30 days")
/// {
///     DotColor = Color.Red, Value = "540 min", ValueCaption = "30 days", Badge = "Chronic", BadgeClasses = "chip-danger",
/// });
/// </code>
/// </summary>
public class TwListItem
{
    public TwListItem()
    {
    }

    public TwListItem(string title, string? subtitle = null)
    {
        Title = title ?? "";
        Subtitle = subtitle;
    }

    public string Title { get; set; } = "";

    /// <summary>Muted line under the title; <c>null</c> = a single-line row.</summary>
    public string? Subtitle { get; set; }

    /// <summary>A status dot at the start (empty = none).</summary>
    public Color DotColor { get; set; } = Color.Empty;

    /// <summary>An icon at the start (<see cref="TwIconRenderer.DrawIcon"/>); <c>null</c> = none.</summary>
    public string? IconName { get; set; }

    /// <summary>Color of the icon; empty = the muted text color.</summary>
    public Color IconColor { get; set; } = Color.Empty;

    /// <summary>A small score ring at the start, 0–1 (<c>null</c> = none), such as a risk score.</summary>
    public double? Score { get; set; }

    /// <summary>Text in the ring; <c>null</c> = the score out of 100 ("72").</summary>
    public string? ScoreText { get; set; }

    /// <summary>Color of the ring's arc; empty = <c>accent-*</c> or the first palette color.</summary>
    public Color ScoreColor { get; set; } = Color.Empty;

    /// <summary>Value on the right ("540 min"), drawn in the title's weight.</summary>
    public string? Value { get; set; }

    /// <summary>Muted text under <see cref="Value"/>.</summary>
    public string? ValueCaption { get; set; }

    /// <summary>A pill at the end of the row ("Chronic").</summary>
    public string? Badge { get; set; }

    /// <summary>Classes of the badge (default <c>chip</c>; e.g. <c>chip-danger</c> or <c>bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-300</c>).</summary>
    public string? BadgeClasses { get; set; }

    public object? Tag { get; set; }

    /// <summary>Whether the row has a second line (title and subtitle or value caption).</summary>
    public bool IsTwoLine => !string.IsNullOrEmpty(Subtitle) || !string.IsNullOrEmpty(ValueCaption);

    /// <summary>What screen readers read: the texts joined with commas.</summary>
    public string AccessibleText =>
        string.Join(", ", new[] { Title, Subtitle, Value, ValueCaption, Badge, Score is double s && ScoreText is null ? Math.Round(s * 100).ToString(System.Globalization.CultureInfo.CurrentCulture) : ScoreText }
            .Where(t => !string.IsNullOrEmpty(t)));

    public override string ToString() => Title;
}

/// <summary>Draws a <see cref="TwListItem"/> into a <see cref="TwDataList"/> row.</summary>
internal static class TwListItemPainter
{
    private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    /// <summary>Height (96 DPI) of a two-line row for the list's font.</summary>
    public static int TwoLineHeight(TwDataList list)
    {
        float scale = TwDpi.DeviceDpi(list) / 96f;
        int line = TextRenderer.MeasureText("Ag", list.Font, Size.Empty, Flags).Height;
        return (int)Math.Ceiling((line * 2 + 2 * scale) / scale) + 18;
    }

    public static void Paint(TwDataList list, TwDataListDrawEventArgs e, TwListItem item, Color? accent)
    {
        var g = e.Graphics;
        var content = e.ContentBounds;
        float scale = TwDpi.DeviceDpi(list) / 96f;
        int S(float px) => (int)Math.Round(px * scale);
        bool rtl = TwRtl.NeedsMirroring(list);
        int left = content.Left, right = content.Right;
        var title = e.FontWithWeight(600);
        bool dark = e.BackColor.GetBrightness() < 0.5f;

        // Baş: durum noktası, simge ve skor halkası (verilenler, bu sırayla).
        void Lead(int width, Action<Rectangle> draw)
        {
            var area = new Rectangle(rtl ? right - width : left, content.Y, width, content.Height);
            draw(area);
            if (rtl) right -= width + S(10);
            else left += width + S(10);
        }
        if (!item.DotColor.IsEmpty)
            Lead(S(8), area => TwPainter.FillRounded(g, new Rectangle(area.X, area.Y + (area.Height - S(8)) / 2, S(8), S(8)), S(4), item.DotColor));
        if (item.IconName != null)
            Lead(S(18), area => e.DrawIcon(item.IconName, area, item.IconColor.IsEmpty ? (Color?)null : item.IconColor));
        if (item.Score is double score)
        {
            int d = Math.Min(content.Height - S(4), S(item.IsTwoLine ? 32 : 24));
            var color = !item.ScoreColor.IsEmpty ? item.ScoreColor : TwCharts.SeriesColor(0, dark, accent);
            var label = item.ScoreText ?? Math.Round(score * 100).ToString(System.Globalization.CultureInfo.CurrentCulture);
            Lead(d, area => TwRingChart.DrawScore(g, new Rectangle(area.X, area.Y + (area.Height - d) / 2, d, d), score, color,
                TwCharts.Blend(e.BackColor, e.ForeColor, 0.14f), label, list.Font, e.ForeColor, scale));
        }

        // Son: rozet, sonra değer (sağdan sola).
        if (!string.IsNullOrEmpty(item.Badge))
        {
            int width = PaintBadge(list, e, item, rtl ? left : right, rtl, dark, scale);
            if (rtl) left += width + S(10);
            else right -= width + S(10);
        }
        if (!string.IsNullOrEmpty(item.Value) || !string.IsNullOrEmpty(item.ValueCaption))
        {
            int valueWidth = Math.Max(
                string.IsNullOrEmpty(item.Value) ? 0 : TextRenderer.MeasureText(g, item.Value, title, Size.Empty, Flags).Width,
                string.IsNullOrEmpty(item.ValueCaption) ? 0 : TextRenderer.MeasureText(g, item.ValueCaption, list.Font, Size.Empty, Flags).Width);
            // Değer başlığa yer bırakır: satırın en çok yarısını alır.
            valueWidth = Math.Min(valueWidth, Math.Max(0, (right - left) / 2));
            var area = new Rectangle(rtl ? left : right - valueWidth, content.Y, valueWidth, content.Height);
            PaintLines(e, area, item.Value, title, e.ForeColor, item.ValueCaption, rtl ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight, scale);
            if (rtl) left += valueWidth + S(12);
            else right -= valueWidth + S(12);
        }

        var text = new Rectangle(left, content.Y, Math.Max(0, right - left), content.Height);
        PaintLines(e, text, item.Title, title, e.ForeColor, item.Subtitle, rtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft, scale);
    }

    // Bir ya da iki satır: üstte ana metin, altta soluk metin; ikisi birlikte dikeyde ortalanır.
    private static void PaintLines(TwDataListDrawEventArgs e, Rectangle area, string? main, Font mainFont, Color mainColor, string? sub, ContentAlignment alignment, float scale)
    {
        bool two = !string.IsNullOrEmpty(sub);
        if (!two)
        {
            e.DrawText(main, area, mainColor, mainFont, alignment);
            return;
        }
        int top = TextRenderer.MeasureText("Ag", mainFont, Size.Empty, Flags).Height;
        int bottom = TextRenderer.MeasureText("Ag", e.Font, Size.Empty, Flags).Height;
        int gap = Math.Max(1, (int)Math.Round(2 * scale));
        int y = area.Y + (area.Height - top - bottom - gap) / 2;
        var horizontal = alignment == ContentAlignment.MiddleRight ? ContentAlignment.TopRight : ContentAlignment.TopLeft;
        if (!string.IsNullOrEmpty(main)) e.DrawText(main, new Rectangle(area.X, y, area.Width, top), mainColor, mainFont, horizontal);
        e.DrawText(sub, new Rectangle(area.X, y + top + gap, area.Width, bottom), e.MutedColor, null, horizontal);
    }

    // Rozet: BadgeClasses'ın renkleri, dolgusu, köşesi ve yazısı; genişliğini döndürür.
    private static int PaintBadge(TwDataList list, TwDataListDrawEventArgs e, TwListItem item, int edge, bool rtl, bool dark, float scale)
    {
        var style = TwTheme.Current.Parse(string.IsNullOrWhiteSpace(item.BadgeClasses) ? "chip" : item.BadgeClasses!).Resolve(dark ? TwState.Dark : TwState.None);
        var font = TwFonts.Create(style, list.Font, TwTheme.Current, TwDpi.FontScale(list));
        var size = TextRenderer.MeasureText(e.Graphics, item.Badge, font, Size.Empty, Flags);
        var padding = style.Padding.ApplyTo(new Padding(TwSides.Scale(8, scale), TwSides.Scale(2, scale), TwSides.Scale(8, scale), TwSides.Scale(2, scale)), scale);
        int width = size.Width + padding.Horizontal, height = size.Height + padding.Vertical;
        var pill = new Rectangle(rtl ? edge : edge - width, e.Bounds.Y + (e.Bounds.Height - height) / 2, width, height);
        var back = style.BackColor ?? Color.FromArgb(dark ? 60 : 24, e.ForeColor);
        TwPainter.FillRounded(e.Graphics, pill, (style.Radius ?? 9999) * scale, back);
        if ((style.BorderWidth ?? 0) > 0 && style.BorderColor is Color border)
            TwPainter.StrokeRounded(e.Graphics, pill, (style.Radius ?? 9999) * scale, Math.Max(1f, scale), border);
        TextRenderer.DrawText(e.Graphics, item.Badge, font, pill, style.ForeColor ?? e.ForeColor,
            Flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        return width;
    }
}
