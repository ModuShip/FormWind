using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// A <see cref="TwListBox"/> row drawn with the built-in template: a toggle icon, a status dot, an icon or a small score ring at the
/// start, a title with a muted line (or a progress bar) under it, and a value, a badge and an action button on the right.
/// Unset parts take no room.
/// <code>
/// list.Items.Add(new TwListItem("Press 3 · spindle bearing", "Line 2 · 6 stops in 30 days")
/// {
///     DotColor = Color.Red, Value = "540 min", ValueCaption = "30 days", Badge = "Chronic", BadgeClasses = "chip-danger",
///     ToggleIconName = "star", ToggledIconName = "star-filled", ActionText = "Sign in",
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

    /// <summary>
    /// A clickable icon at the very start of the row (a bookmark, a star): its name when off; <c>null</c> = no toggle.
    /// Clicking it or pressing Space on the selected row flips <see cref="IsToggled"/> and raises <see cref="TwListBox.ItemToggle"/>.
    /// </summary>
    public string? ToggleIconName { get; set; }

    /// <summary>Icon of the toggle when it is on (e.g. <c>star-filled</c> for <c>star</c>); <c>null</c> = the same icon, drawn in <see cref="ToggleColor"/>.</summary>
    public string? ToggledIconName { get; set; }

    /// <summary>Whether the toggle is on.</summary>
    public bool IsToggled { get; set; }

    /// <summary>Color of the toggle when it is on; empty = <c>accent-*</c> or the first palette color.</summary>
    public Color ToggleColor { get; set; } = Color.Empty;

    /// <summary>What screen readers call the toggle (e.g. "Bookmark"); <c>null</c> = <see cref="TwTexts.Toggle"/>.</summary>
    public string? ToggleText { get; set; }

    /// <summary>
    /// Text of a button near the end of the row ("Sign in"); <c>null</c> = none. Clicking it or pressing Ctrl+Enter on the
    /// selected row raises <see cref="TwListBox.ItemActionClick"/>.
    /// </summary>
    public string? ActionText { get; set; }

    /// <summary>Classes of the action button (default <c>btn-ghost px-3 py-1 text-xs</c>; its <c>hover:</c> and <c>active:</c> variants are used).</summary>
    public string? ActionClasses { get; set; }

    /// <summary>An inline progress bar on the second line, 0–1 (<c>null</c> = none), followed by <see cref="ProgressText"/>.</summary>
    public double? ProgressValue { get; set; }

    /// <summary>Text after the bar ("0 / 2 done"); <see cref="Subtitle"/>, when set, follows it after a dot.</summary>
    public string? ProgressText { get; set; }

    /// <summary>Color of the bar's fill; empty = <c>accent-*</c> or the first palette color.</summary>
    public Color ProgressColor { get; set; } = Color.Empty;

    public object? Tag { get; set; }

    /// <summary>Whether the row has a second line (title and subtitle, value caption or progress bar).</summary>
    public bool IsTwoLine => !string.IsNullOrEmpty(Subtitle) || !string.IsNullOrEmpty(ValueCaption) || ProgressValue != null;

    /// <summary>What screen readers read: the texts joined with commas.</summary>
    public string AccessibleText =>
        string.Join(", ", new[] { Title, Subtitle, ProgressText, Value, ValueCaption, Badge, Score is double s && ScoreText is null ? Math.Round(s * 100).ToString(System.Globalization.CultureInfo.CurrentCulture) : ScoreText }
            .Where(t => !string.IsNullOrEmpty(t)));

    public override string ToString() => Title;
}

/// <summary>Event data of <see cref="TwListBox.ItemToggle"/> and <see cref="TwListBox.ItemActionClick"/>.</summary>
public sealed class TwListItemEventArgs : EventArgs
{
    public TwListItemEventArgs(int index, TwListItem item)
    {
        Index = index;
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    /// <summary>The row's position in <see cref="TwListBox.Items"/>.</summary>
    public int Index { get; }

    /// <summary>The item; for <see cref="TwListBox.ItemToggle"/> its <see cref="TwListItem.IsToggled"/> is already the new value.</summary>
    public TwListItem Item { get; }
}

/// <summary>The clickable parts of a <see cref="TwListItem"/> row.</summary>
internal enum TwListItemPart
{
    None,
    Toggle,
    Action,
}

/// <summary>Where the parts of a row are (list coordinates); computed for painting and for hit-testing alike.</summary>
internal sealed class TwListItemLayout
{
    public bool Rtl;
    public Rectangle Toggle, Dot, Icon, Ring, Badge, Action, Value, Text;
    public int RingDiameter;
}

/// <summary>Draws a <see cref="TwListItem"/> into a <see cref="TwDataList"/> row.</summary>
internal static class TwListItemPainter
{
    private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
    internal const string DefaultActionClasses = "btn-ghost px-3 py-1 text-xs";

    /// <summary>Height (96 DPI) of a two-line row for the list's font.</summary>
    public static int TwoLineHeight(TwDataList list)
    {
        float scale = TwDpi.DeviceDpi(list) / 96f;
        int line = TextRenderer.MeasureText("Ag", list.Font, Size.Empty, Flags).Height;
        return (int)Math.Ceiling((line * 2 + 2 * scale) / scale) + 18;
    }

    private static Font TitleFont(TwDataList list) => TwFonts.Create(new TwStyle { FontWeight = 600 }, list.Font, TwTheme.Current);

    private static TwStyle ActionStyle(TwListItem item, bool dark, TwListItemPart hot, TwListItemPart pressed)
    {
        var state = dark ? TwState.Dark : TwState.None;
        if (pressed == TwListItemPart.Action) state |= TwState.Active | TwState.Hover;
        else if (hot == TwListItemPart.Action) state |= TwState.Hover;
        return TwTheme.Current.Parse(string.IsNullOrWhiteSpace(item.ActionClasses) ? DefaultActionClasses : item.ActionClasses!).Resolve(state);
    }

    private static TwStyle BadgeStyle(TwListItem item, bool dark) =>
        TwTheme.Current.Parse(string.IsNullOrWhiteSpace(item.BadgeClasses) ? "chip" : item.BadgeClasses!).Resolve(dark ? TwState.Dark : TwState.None);

    // Hap biçimli kutu: yazı + dolgu; yükseklik satırı aşmaz.
    private static Size PillSize(string text, Font font, TwStyle style, float scale, int maxHeight)
    {
        var size = TextRenderer.MeasureText(text, font, Size.Empty, Flags);
        var padding = style.Padding.ApplyTo(new Padding(TwSides.Scale(8, scale), TwSides.Scale(2, scale), TwSides.Scale(8, scale), TwSides.Scale(2, scale)), scale);
        return new Size(size.Width + padding.Horizontal, Math.Min(maxHeight, size.Height + padding.Vertical));
    }

    /// <summary>The row's parts for a row at <paramref name="bounds"/> (list coordinates).</summary>
    public static TwListItemLayout Layout(TwDataList list, TwListItem item, Rectangle bounds, bool dark)
    {
        var padding = list.ScaledItemPadding;
        var content = new Rectangle(bounds.X + padding.Left, bounds.Y + padding.Top, Math.Max(0, bounds.Width - padding.Horizontal), Math.Max(0, bounds.Height - padding.Vertical));
        float scale = TwDpi.DeviceDpi(list) / 96f;
        int S(float px) => (int)Math.Round(px * scale);
        bool rtl = TwRtl.NeedsMirroring(list);
        int left = content.Left, right = content.Right;
        var layout = new TwListItemLayout { Rtl = rtl };
        var title = TitleFont(list);

        // Baş: anahtar simgesi, durum noktası, simge ve skor halkası (verilenler, bu sırayla).
        Rectangle Lead(int width)
        {
            var area = new Rectangle(rtl ? right - width : left, content.Y, width, content.Height);
            if (rtl) right -= width + S(10);
            else left += width + S(10);
            return area;
        }
        if (item.ToggleIconName != null) layout.Toggle = Lead(S(18));
        if (!item.DotColor.IsEmpty) layout.Dot = Lead(S(8));
        if (item.IconName != null) layout.Icon = Lead(S(18));
        if (item.Score != null)
        {
            layout.RingDiameter = Math.Min(content.Height - S(4), S(item.IsTwoLine ? 32 : 24));
            layout.Ring = Lead(layout.RingDiameter);
        }

        // Son: eylem düğmesi, rozet, sonra değer (sağdan sola).
        Rectangle Trail(Size size)
        {
            var pill = new Rectangle(rtl ? left : right - size.Width, bounds.Y + (bounds.Height - size.Height) / 2, size.Width, size.Height);
            if (rtl) left += size.Width + S(10);
            else right -= size.Width + S(10);
            return pill;
        }
        if (!string.IsNullOrEmpty(item.ActionText))
        {
            var style = ActionStyle(item, dark, TwListItemPart.None, TwListItemPart.None);
            var font = TwFonts.Create(style, list.Font, TwTheme.Current, TwDpi.FontScale(list));
            layout.Action = Trail(PillSize(item.ActionText!, font, style, scale, Math.Max(0, bounds.Height - S(4))));
        }
        if (!string.IsNullOrEmpty(item.Badge))
        {
            var style = BadgeStyle(item, dark);
            var font = TwFonts.Create(style, list.Font, TwTheme.Current, TwDpi.FontScale(list));
            layout.Badge = Trail(PillSize(item.Badge!, font, style, scale, int.MaxValue));
        }
        if (!string.IsNullOrEmpty(item.Value) || !string.IsNullOrEmpty(item.ValueCaption))
        {
            int valueWidth = Math.Max(
                string.IsNullOrEmpty(item.Value) ? 0 : TextRenderer.MeasureText(item.Value, title, Size.Empty, Flags).Width,
                string.IsNullOrEmpty(item.ValueCaption) ? 0 : TextRenderer.MeasureText(item.ValueCaption, list.Font, Size.Empty, Flags).Width);
            // Değer başlığa yer bırakır: satırın en çok yarısını alır.
            valueWidth = Math.Min(valueWidth, Math.Max(0, (right - left) / 2));
            layout.Value = new Rectangle(rtl ? left : right - valueWidth, content.Y, valueWidth, content.Height);
            if (rtl) left += valueWidth + S(12);
            else right -= valueWidth + S(12);
        }
        layout.Text = new Rectangle(left, content.Y, Math.Max(0, right - left), content.Height);
        return layout;
    }

    public static void Paint(TwDataList list, TwDataListDrawEventArgs e, TwListItem item, Color? accent,
                             TwListItemPart hot = TwListItemPart.None, TwListItemPart pressed = TwListItemPart.None)
    {
        var g = e.Graphics;
        float scale = TwDpi.DeviceDpi(list) / 96f;
        int S(float px) => (int)Math.Round(px * scale);
        var title = e.FontWithWeight(600);
        bool dark = e.BackColor.GetBrightness() < 0.5f;
        var layout = Layout(list, item, e.Bounds, dark);
        bool rtl = layout.Rtl;

        if (item.ToggleIconName != null)
        {
            var area = layout.Toggle;
            if (hot == TwListItemPart.Toggle || pressed == TwListItemPart.Toggle)
            {
                int d = Math.Min(area.Height, area.Width + S(8));
                var disc = new Rectangle(area.X + (area.Width - d) / 2, area.Y + (area.Height - d) / 2, d, d);
                TwPainter.FillRounded(g, disc, d / 2f, Color.FromArgb(pressed == TwListItemPart.Toggle ? 56 : 28, e.ForeColor));
            }
            string icon = item.IsToggled ? item.ToggledIconName ?? item.ToggleIconName : item.ToggleIconName;
            Color? color = item.IsToggled
                ? e.Selected ? e.ForeColor : !item.ToggleColor.IsEmpty ? item.ToggleColor : TwCharts.SeriesColor(0, dark, accent)
                : hot == TwListItemPart.Toggle ? e.ForeColor : (Color?)null;
            e.DrawIcon(icon, area, color);
        }
        if (!item.DotColor.IsEmpty) TwTwoLinePainter.DrawDot(g, layout.Dot, rtl, item.DotColor, scale);
        if (item.IconName != null) e.DrawIcon(item.IconName, layout.Icon, item.IconColor.IsEmpty ? (Color?)null : item.IconColor);
        if (item.Score is double score)
        {
            int d = layout.RingDiameter;
            var ring = layout.Ring;
            var color = !item.ScoreColor.IsEmpty ? item.ScoreColor : TwCharts.SeriesColor(0, dark, accent);
            var label = item.ScoreText ?? Math.Round(score * 100).ToString(System.Globalization.CultureInfo.CurrentCulture);
            TwRingChart.DrawScore(g, new Rectangle(ring.X, ring.Y + (ring.Height - d) / 2, d, d), score, color,
                TwCharts.Blend(e.BackColor, e.ForeColor, 0.14f), label, list.Font, e.ForeColor, scale);
        }

        if (!layout.Action.IsEmpty) PaintPill(list, e, layout.Action, item.ActionText!, ActionStyle(item, dark, hot, pressed), scale, dark);
        if (!layout.Badge.IsEmpty) PaintPill(list, e, layout.Badge, item.Badge!, BadgeStyle(item, dark), scale, dark);
        if (!layout.Value.IsEmpty)
            PaintLines(e, layout.Value, item.Value, title, e.ForeColor, item.ValueCaption, rtl ? ContentAlignment.MiddleLeft : ContentAlignment.MiddleRight, scale);

        if (item.ProgressValue is double progress) PaintProgressRow(e, layout.Text, item, title, progress, accent, dark, rtl, scale);
        else PaintLines(e, layout.Text, item.Title, title, e.ForeColor, item.Subtitle, rtl ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft, scale);
    }

    // Bir ya da iki satır: üstte ana metin, altta soluk metin; ikisi birlikte dikeyde ortalanır.
    private static void PaintLines(TwDataListDrawEventArgs e, Rectangle area, string? main, Font mainFont, Color mainColor, string? sub, ContentAlignment alignment, float scale) =>
        TwTwoLinePainter.Paint(e.Graphics, area, main, mainFont, mainColor, sub, e.Font, e.MutedColor, e.BackColor, alignment == ContentAlignment.MiddleRight, scale);

    // İkinci satır: ilerleme çubuğu, yazısı ve (varsa) alt başlık.
    private static void PaintProgressRow(TwDataListDrawEventArgs e, Rectangle area, TwListItem item, Font title, double progress,
                                         Color? accent, bool dark, bool rtl, float scale)
    {
        TwTwoLinePainter.TwoLines(area, title, e.Font, scale, out var first, out var second);
        var alignment = rtl ? ContentAlignment.TopRight : ContentAlignment.TopLeft;
        TwTwoLinePainter.DrawLine(e.Graphics, item.Title, first, title, e.ForeColor, e.BackColor, alignment);

        int barWidth = Math.Min((int)Math.Round(64 * scale), Math.Max(0, second.Width / 2)), barHeight = Math.Max(2, (int)Math.Round(5 * scale));
        var track = new Rectangle(rtl ? second.Right - barWidth : second.X, second.Y + (second.Height - barHeight) / 2, barWidth, barHeight);
        TwPainter.FillRounded(e.Graphics, track, barHeight / 2f, TwCharts.Blend(e.BackColor, e.ForeColor, 0.14f));
        double clamped = Math.Max(0, Math.Min(1, progress));
        if (clamped > 0)
        {
            var color = e.Selected ? e.ForeColor : !item.ProgressColor.IsEmpty ? item.ProgressColor : TwCharts.SeriesColor(0, dark, accent);
            int fillWidth = Math.Max(barHeight, (int)Math.Round(track.Width * clamped));
            TwPainter.FillRounded(e.Graphics, new Rectangle(rtl ? track.Right - fillWidth : track.X, track.Y, fillWidth, barHeight), barHeight / 2f, color);
        }
        string text = string.IsNullOrEmpty(item.ProgressText) ? item.Subtitle ?? "" : string.IsNullOrEmpty(item.Subtitle) ? item.ProgressText! : item.ProgressText + " · " + item.Subtitle;
        int gap = (int)Math.Round(8 * scale);
        var textArea = rtl ? Rectangle.FromLTRB(second.X, second.Y, track.X - gap, second.Bottom) : Rectangle.FromLTRB(track.Right + gap, second.Y, second.Right, second.Bottom);
        if (textArea.Width > 0)
            TwTwoLinePainter.DrawLine(e.Graphics, text, textArea, e.Font, e.MutedColor, e.BackColor, alignment);
    }

    // Hap: stilin renkleri, kenarlığı, köşesi ve yazısı (rozet ve eylem düğmesi).
    private static void PaintPill(TwDataList list, TwDataListDrawEventArgs e, Rectangle pill, string text, TwStyle style, float scale, bool dark)
    {
        var font = TwFonts.Create(style, list.Font, TwTheme.Current, TwDpi.FontScale(list));
        var back = style.BackColor ?? Color.FromArgb(dark ? 60 : 24, e.ForeColor);
        TwPainter.FillRounded(e.Graphics, pill, (style.Radius ?? 9999) * scale, back);
        if ((style.BorderWidth ?? 0) > 0 && style.BorderColor is Color border)
            TwPainter.StrokeRounded(e.Graphics, pill, (style.Radius ?? 9999) * scale, Math.Max(1f, scale), border);
        TextRenderer.DrawText(e.Graphics, text, font, pill, style.ForeColor ?? e.ForeColor,
            Flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
