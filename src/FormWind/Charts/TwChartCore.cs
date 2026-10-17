using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Colors of the chart controls, set by the provider from the classes.</summary>
internal sealed class TwChartColors
{
    public bool Dark;
    /// <summary><c>divide-*</c>: gridlines and the baseline.</summary>
    public Color? Grid;
    /// <summary><c>label:text-*</c>: axis labels, legend and secondary text.</summary>
    public Color? Label;
    /// <summary><c>accent-*</c>: the first series (and the sparkline).</summary>
    public Color? Accent;
    /// <summary>Rising and falling values of <see cref="TwStat"/> (the <c>success</c> and <c>danger</c> colors).</summary>
    public Color? Positive;
    public Color? Negative;
}

internal interface ITwChartControl
{
    void SetChartColors(TwChartColors colors);
}

/// <summary>Palette, scales, number formats and shared drawing of the chart controls.</summary>
internal static class TwCharts
{
    // Doğrulanmış kategorik palet (renk körlüğü ayrımı ve normal görüş tabanı, açık #ffffff ve koyu #1f2937 yüzeylerde).
    // Sıra sabittir: renk varlığı izler, sırası değil; 9. seri yeni bir ton almaz, gri olur.
    internal static readonly Color[] Light = Hex("#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6", "#e34948");
    internal static readonly Color[] Dark = Hex("#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9", "#e66767");

    private static Color[] Hex(params string[] values) => values.Select(ColorTranslator.FromHtml).ToArray();

    /// <summary>Color of a series by position; past the palette, a neutral gray (hues are never cycled).</summary>
    public static Color SeriesColor(int index, bool dark, Color? first)
    {
        if (index == 0 && first is Color accent) return accent;
        var palette = dark ? Dark : Light;
        return index < palette.Length ? palette[index] : ColorTranslator.FromHtml(dark ? "#6b7280" : "#9ca3af");
    }

    /// <summary>The color behind the control (its own background, or the first opaque parent's).</summary>
    public static Color Surface(Control control)
    {
        for (Control? c = control; c != null; c = c.Parent)
            if (c.BackColor.A == 255) return c.BackColor;
        return SystemColors.Control;
    }

    public static Color Blend(Color a, Color b, float amount) => Color.FromArgb(
        (int)Math.Round(a.R + (b.R - a.R) * amount),
        (int)Math.Round(a.G + (b.G - a.G) * amount),
        (int)Math.Round(a.B + (b.B - a.B) * amount));

    /// <summary>Text color that stays legible on a filled color: white on dark and saturated fills, near-black on light ones.</summary>
    public static Color ReadableOn(Color fill)
    {
        static double Channel(int c)
        {
            double v = c / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        double luminance = 0.2126 * Channel(fill.R) + 0.7152 * Channel(fill.G) + 0.0722 * Channel(fill.B);
        // Eşit kontrast noktası ≈ 0,18; paletin orta tonlarında (mavi, yeşil) beyaz metin tercih edilir, sarı ve açık tonlarda koyu.
        return luminance > 0.35 ? ColorTranslator.FromHtml("#111827") : Color.White;
    }

    /// <summary>Secondary text: the <c>label:</c> color or the text color blended toward the surface.</summary>
    public static Color Muted(Control control, TwChartColors colors) => colors.Label ?? Blend(control.ForeColor, Surface(control), 0.4f);

    /// <summary>Gridlines: the <c>divide-*</c> color or one step off the surface.</summary>
    public static Color Grid(Control control, TwChartColors colors) => colors.Grid ?? Blend(Surface(control), control.ForeColor, 0.12f);

    /// <summary>Axis bounds and step with round numbers ("nice numbers"); bars and areas include zero.</summary>
    public static (double Min, double Max, double Step) NiceScale(double min, double max, int ticks, bool includeZero)
    {
        if (includeZero)
        {
            min = Math.Min(0, min);
            max = Math.Max(0, max);
        }
        if (double.IsNaN(min) || double.IsInfinity(min) || double.IsNaN(max) || double.IsInfinity(max)) return (0, 1, 0.25);
        if (max - min < 1e-12)
        {
            double pad = Math.Abs(max) > 0 ? Math.Abs(max) * 0.5 : 1;
            if (min >= 0 && includeZero) max += pad;
            else
            {
                min -= pad;
                max += pad;
            }
        }
        double step = Nice((max - min) / Math.Max(1, ticks - 1), round: true);
        return (Math.Floor(min / step) * step, Math.Ceiling(max / step) * step, step);
    }

    private static double Nice(double value, bool round)
    {
        double exponent = Math.Floor(Math.Log10(value));
        double fraction = value / Math.Pow(10, exponent);
        double nice = round
            ? fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10
            : fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        return nice * Math.Pow(10, exponent);
    }

    /// <summary>Compact number for axis ticks: 1.200 → 1,2K (Turkish: 1,2B), 3.000.000 → 3M (Mn).</summary>
    public static string Compact(double value)
    {
        var texts = TwTexts.Current;
        double abs = Math.Abs(value);
        (double Divisor, string Suffix) unit = abs >= 1e9 ? (1e9, texts.BillionSuffix)
            : abs >= 1e6 ? (1e6, texts.MillionSuffix)
            : abs >= 1e3 ? (1e3, texts.ThousandSuffix)
            : (1, "");
        return (value / unit.Divisor).ToString("0.#", CultureInfo.CurrentCulture) + unit.Suffix;
    }

    /// <summary>A value with the chart's format (default: up to two decimals with group separators).</summary>
    public static string Format(double value, string? format) =>
        double.IsNaN(value) ? "—" : value.ToString(string.IsNullOrEmpty(format) ? "#,0.##" : format, CultureInfo.CurrentCulture);

    /// <summary>A bar with a rounded data end (4 px) and a square end at the baseline.</summary>
    public static void FillBar(Graphics g, RectangleF bar, float radius, bool up, Color color)
    {
        if (bar.Width <= 0 || bar.Height <= 0) return;
        float r = Math.Min(radius, Math.Min(bar.Width / 2f, bar.Height));
        using var brush = new SolidBrush(color);
        if (r < 0.5f)
        {
            g.FillRectangle(brush, bar);
            return;
        }
        using var path = new GraphicsPath();
        float d = r * 2;
        if (up)
        {
            path.AddLine(bar.Left, bar.Bottom, bar.Left, bar.Top + r);
            path.AddArc(bar.Left, bar.Top, d, d, 180, 90);
            path.AddArc(bar.Right - d, bar.Top, d, d, 270, 90);
            path.AddLine(bar.Right, bar.Top + r, bar.Right, bar.Bottom);
        }
        else
        {
            path.AddLine(bar.Right, bar.Top, bar.Right, bar.Bottom - r);
            path.AddArc(bar.Right - d, bar.Bottom - d, d, d, 0, 90);
            path.AddArc(bar.Left, bar.Bottom - d, d, d, 90, 90);
            path.AddLine(bar.Left, bar.Bottom - r, bar.Left, bar.Top);
        }
        path.CloseFigure();
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillPath(brush, path);
        g.Restore(state);
    }

    /// <summary>A marker of at least 8 px with a 2 px ring in the surface color (stays legible where it crosses a line).</summary>
    public static void DrawMarker(Graphics g, PointF center, float scale, Color color, Color surface)
    {
        float r = 4 * scale, ring = 2 * scale;
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var back = new SolidBrush(surface)) g.FillEllipse(back, center.X - r - ring, center.Y - r - ring, (r + ring) * 2, (r + ring) * 2);
        using (var fill = new SolidBrush(color)) g.FillEllipse(fill, center.X - r, center.Y - r, r * 2, r * 2);
        g.Restore(state);
    }

    /// <summary>A small chart without axes (sparkline): a 2 px line, a 10% area or bars; lines end with a marker.</summary>
    public static void DrawSparkline(Graphics g, Rectangle area, IReadOnlyList<double> numbers, TwSparklineKind kind, Color color, Color negative, Color surface, float scale, bool marker = true)
    {
        var values = numbers.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
        if (values.Count == 0 || area.Width < 4 || area.Height < 4) return;
        double min = values.Min(), max = values.Max();
        if (kind == TwSparklineKind.Bar)
        {
            min = Math.Min(0, min);
            max = Math.Max(0, max);
        }
        double span = max - min;
        float Y(double v) => span == 0 ? area.Y + area.Height / 2f : (float)(area.Bottom - (v - min) / span * area.Height);

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (kind == TwSparklineKind.Bar)
        {
            float slot = area.Width / (float)numbers.Count;
            float gap = Math.Min(2 * scale, slot / 3);
            float width = Math.Max(1f, slot - gap);
            float zero = Y(0);
            for (int i = 0; i < numbers.Count; i++)
            {
                if (double.IsNaN(numbers[i])) continue;
                float y = Y(numbers[i]);
                var bar = new RectangleF(area.X + i * slot + gap / 2, Math.Min(y, zero), width, Math.Max(1f, Math.Abs(zero - y)));
                FillBar(g, bar, Math.Min(2 * scale, width / 2), numbers[i] >= 0, numbers[i] < 0 ? negative : color);
            }
        }
        else
        {
            // Eksik değerler (NaN) çizgide boşluk bırakır.
            var segments = new List<List<PointF>>();
            List<PointF>? current = null;
            for (int i = 0; i < numbers.Count; i++)
            {
                if (double.IsNaN(numbers[i]))
                {
                    current = null;
                    continue;
                }
                float x = numbers.Count == 1 ? area.X + area.Width / 2f : area.X + i * (area.Width - 1) / (float)(numbers.Count - 1);
                if (current is null) segments.Add(current = new List<PointF>());
                current.Add(new PointF(x, Y(numbers[i])));
            }
            using var pen = new Pen(color, Math.Max(1f, 2 * scale)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            foreach (var points in segments)
            {
                if (kind == TwSparklineKind.Area && points.Count > 1)
                {
                    var fill = points.Concat(new[] { new PointF(points[points.Count - 1].X, area.Bottom), new PointF(points[0].X, area.Bottom) }).ToArray();
                    using var brush = new SolidBrush(Color.FromArgb(26, color));
                    g.FillPolygon(brush, fill);
                }
                if (points.Count > 1) g.DrawLines(pen, points.ToArray());
            }
            var last = segments.LastOrDefault()?.LastOrDefault();
            if (marker && last is PointF end) DrawMarker(g, end, scale * 0.75f, color, surface);
        }
        g.Restore(state);
    }

    /// <summary>A rounded tooltip box with lines of text, each optionally keyed with a colored swatch.</summary>
    public static void DrawTooltip(Graphics g, Control owner, Point anchor, string title, IReadOnlyList<(Color? Key, string Text)> lines, float scale)
    {
        var font = owner.Font;
        using var bold = new Font(font, FontStyle.Bold);
        int pad = (int)(8 * scale), gap = (int)(4 * scale), key = (int)(10 * scale);
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        var titleSize = TextRenderer.MeasureText(g, title, bold, Size.Empty, flags);
        int width = titleSize.Width, height = titleSize.Height;
        var sizes = lines.Select(l => TextRenderer.MeasureText(g, l.Text, font, Size.Empty, flags)).ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            width = Math.Max(width, sizes[i].Width + (lines[i].Key.HasValue ? key + gap : 0));
            height += gap + sizes[i].Height;
        }
        var box = new Rectangle(anchor.X + (int)(12 * scale), anchor.Y - height / 2 - pad, width + pad * 2, height + pad * 2);
        if (box.Right > owner.ClientSize.Width - 2) box.X = anchor.X - (int)(12 * scale) - box.Width;
        box.X = Math.Max(2, box.X);
        box.Y = Math.Max(2, Math.Min(owner.ClientSize.Height - box.Height - 2, box.Y));

        // Kutu metin rengiyle (ters yüzey) çizilir: hem açık hem koyu temada grafikten ayrılır.
        var surface = Surface(owner);
        var back = owner.ForeColor;
        var fore = surface;
        TwPainter.FillRounded(g, box, 6 * scale, Color.FromArgb(240, back));
        int y = box.Y + pad;
        TextRenderer.DrawText(g, title, bold, new Point(box.X + pad, y), fore, flags);
        y += titleSize.Height;
        for (int i = 0; i < lines.Count; i++)
        {
            y += gap;
            int x = box.X + pad;
            if (lines[i].Key is Color swatch)
            {
                TwPainter.FillRounded(g, new Rectangle(x, y + (sizes[i].Height - key) / 2, key, key), 2 * scale, swatch);
                x += key + gap;
            }
            TextRenderer.DrawText(g, lines[i].Text, font, new Point(x, y), fore, flags);
            y += sizes[i].Height;
        }
    }
}

/// <summary>Accessible items of a chart (categories, slices) for screen readers.</summary>
internal sealed class TwChartAccessibleObject : TwListAccessibleObject
{
    private readonly ITwChartItems _chart;

    public TwChartAccessibleObject(Control owner, ITwChartItems chart) : base(owner, AccessibleRole.Chart) => _chart = chart;

    protected override int Count => _chart.ItemCount;

    protected override int ActiveIndex => _chart.ActiveItem;

    protected override int IndexAt(Point client) => _chart.ItemAt(client);

    protected override AccessibleObject CreateChild(int index) => new Item(Host, this, index, _chart);

    public override string? Value
    {
        get => _chart.Summary;
        set { }
    }

    private sealed class Item : TwItemAccessibleObject
    {
        private readonly ITwChartItems _chart;

        public Item(Control owner, AccessibleObject parent, int index, ITwChartItems chart) : base(owner, parent, index) => _chart = chart;

        protected override Rectangle ClientBounds => _chart.ItemBounds(Index);

        public override string? Name
        {
            get => _chart.ItemText(Index);
            set { }
        }

        public override AccessibleRole Role => AccessibleRole.ListItem;

        public override AccessibleStates State
        {
            get
            {
                var state = BaseState;
                if (_chart.ActiveItem == Index) state |= AccessibleStates.Selected | (Owner.Focused ? AccessibleStates.Focused : 0);
                return state;
            }
        }

        public override void Select(AccessibleSelection flags)
        {
            if ((flags & (AccessibleSelection.TakeSelection | AccessibleSelection.TakeFocus)) != 0) _chart.ActiveItem = Index;
            if ((flags & AccessibleSelection.TakeFocus) != 0) Owner.Focus();
        }
    }
}

/// <summary>Items a chart exposes to screen readers and the keyboard.</summary>
internal interface ITwChartItems
{
    int ItemCount { get; }
    int ActiveItem { get; set; }
    int ItemAt(Point client);
    Rectangle ItemBounds(int index);
    string ItemText(int index);
    string Summary { get; }
}
