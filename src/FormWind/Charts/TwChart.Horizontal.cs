using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

// Yatay çubuk grafik (TwChartKind.HorizontalBar): kategoriler satır, değer ekseni altta.
public partial class TwChart
{
    internal sealed class HorizontalLayout
    {
        public Rectangle Plot;
        public Rectangle Legend;
        public double Min, Max, Step;
        public float Band;
        public int LabelWidth;
        public int LabelStep = 1;
        public bool Axis;

        public float X(double value) => Max - Min <= 0 ? Plot.Left : (float)(Plot.Left + (value - Min) / (Max - Min) * Plot.Width);

        public float Y(int index) => Plot.Top + Band * (index + 0.5f);
    }

    private const TextFormatFlags HorizontalFlags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    private string RowLabel(int index) => CategoryLabel(index).Replace('\n', ' ');

    private bool AnyValueLabels => Series.Any(x => x.ShowValues);

    // Yığılmış satırın değer etiketi toplamdır; yan yana çubuklarda her çubuğun değeri.
    private string ValueLabel(int series, int category) =>
        TwCharts.Format(_stacked ? StackTotal(category) : Series[series].ValueAt(category), _valueFormat);

    private double StackTotal(int category)
    {
        double total = 0;
        bool any = false;
        foreach (var series in Series)
        {
            double v = series.ValueAt(category);
            if (double.IsNaN(v)) continue;
            total += v;
            any = true;
        }
        return any ? total : double.NaN;
    }

    internal HorizontalLayout ComputeHorizontalLayout()
    {
        var layout = new HorizontalLayout();
        float s = DpiScale;
        int line = TextRenderer.MeasureText("Ag", Font, Size.Empty, HorizontalFlags).Height;
        var content = Rectangle.FromLTRB(Padding.Left, Padding.Top, Math.Max(Padding.Left, Width - Padding.Right), Math.Max(Padding.Top, Height - Padding.Bottom));
        int top = content.Top;
        if (LegendVisible)
        {
            layout.Legend = new Rectangle(content.Left, top, content.Width, line);
            top += line + (int)(12 * s);
        }
        int count = CategoryCount;
        // Etiketler tam yazılır; çok uzunsa genişliğin en çok %40'ını alır ve "…" ile kısalır.
        int widest = Enumerable.Range(0, count).Select(i => TextRenderer.MeasureText(RowLabel(i), Font, Size.Empty, HorizontalFlags).Width).DefaultIfEmpty(0).Max();
        layout.LabelWidth = Math.Min(widest, (int)(content.Width * 0.4f));
        // Her seri değerini çubuğun ucunda gösteriyorsa alt eksen yazılmaz.
        layout.Axis = !(Series.Count > 0 && Series.All(x => x.ShowValues));
        int bottom = content.Bottom - (layout.Axis ? line + (int)(6 * s) : 0);
        int left = content.Left + (layout.LabelWidth > 0 ? layout.LabelWidth + (int)(10 * s) : 0);
        int valueWidth = 0;
        if (AnyValueLabels)
        {
            for (int c = 0; c < count; c++)
                for (int i = 0; i < Series.Count; i++)
                    if (Series[i].ShowValues) valueWidth = Math.Max(valueWidth, TextRenderer.MeasureText(ValueLabel(i, c), Font, Size.Empty, HorizontalFlags).Width);
            valueWidth += (int)(6 * s);
        }
        var (min, max, includeZero) = ValueRange(TwChartAxis.Primary);
        int plotWidth = Math.Max(1, content.Right - valueWidth - left);
        int ticks = Math.Max(2, Math.Min(8, plotWidth / (int)Math.Max(1, 72 * s) + 1));
        (layout.Min, layout.Max, layout.Step) = Override(TwCharts.NiceScale(min, max, ticks, includeZero), _axisMinimum, _axisMaximum, _axisInterval);
        // Son tik etiketi plot'un sağından taşmasın.
        int lastTick = layout.Axis ? TextRenderer.MeasureText(TickText(layout.Max), Font, Size.Empty, HorizontalFlags).Width / 2 : 0;
        int right = content.Right - Math.Max(valueWidth, lastTick);
        layout.Plot = new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
        layout.Band = count == 0 ? layout.Plot.Height : layout.Plot.Height / (float)count;
        layout.LabelStep = Math.Max(1, (int)Math.Ceiling(line / Math.Max(1f, layout.Band)));
        return layout;
    }

    /// <summary>Bounds of a horizontal bar (or stacked segment); empty when it has no bar.</summary>
    internal RectangleF HorizontalBarBounds(HorizontalLayout layout, int seriesIndex, int category)
    {
        double value = Series[seriesIndex].ValueAt(category);
        if (double.IsNaN(value) || double.IsInfinity(value)) return RectangleF.Empty;
        float s = DpiScale, gap = 2 * s;
        float center = layout.Y(category);
        if (_stacked)
        {
            float thickness = Math.Min(_maxBarWidth * s, layout.Band * 0.6f);
            double baseValue = 0;
            for (int p = 0; p < seriesIndex; p++)
            {
                double v = Series[p].ValueAt(category);
                if (!double.IsNaN(v) && (v >= 0) == (value >= 0)) baseValue += v;
            }
            float from = layout.X(baseValue), to = layout.X(baseValue + value);
            float left = Math.Min(from, to), right = Math.Max(from, to);
            if (Math.Abs(baseValue) >= 1e-12)
            {
                if (value >= 0) left += gap;
                else right -= gap;
            }
            return new RectangleF(left, center - thickness / 2, Math.Max(0, right - left), thickness);
        }
        int m = Series.Count;
        float height = Math.Max(2 * s, Math.Min(_maxBarWidth * s, (layout.Band * 0.72f - (m - 1) * gap) / m));
        float group = m * height + (m - 1) * gap;
        float y = center - group / 2 + seriesIndex * (height + gap);
        float zero = layout.X(Math.Max(layout.Min, Math.Min(layout.Max, 0)));
        float x = layout.X(value);
        return new RectangleF(Math.Min(x, zero), y, Math.Max(1, Math.Abs(x - zero)), height);
    }

    private void PaintHorizontal(Graphics g)
    {
        float s = DpiScale;
        var layout = ComputeHorizontalLayout();
        var plot = layout.Plot;
        var surface = TwCharts.Surface(this);
        var muted = TwCharts.Muted(this, _colors);
        var grid = TwCharts.Grid(this, _colors);
        int count = CategoryCount;

        using (var pen = new Pen(grid, 1))
        {
            foreach (var tick in Ticks(layout.Min, layout.Max, layout.Step))
            {
                int x = (int)Math.Round(layout.X(tick));
                if (_showGrid || Math.Abs(tick) < layout.Step / 1000) g.DrawLine(pen, x, plot.Top, x, plot.Bottom);
                if (!layout.Axis) continue;
                var text = TickText(tick);
                var size = TextRenderer.MeasureText(text, Font, Size.Empty, HorizontalFlags);
                TextRenderer.DrawText(g, text, Font, new Point(x - size.Width / 2, plot.Bottom + (int)(6 * s)), muted, HorizontalFlags);
            }
        }

        int line = TextRenderer.MeasureText("Ag", Font, Size.Empty, HorizontalFlags).Height;
        for (int c = 0; c < count; c += layout.LabelStep)
        {
            var row = new Rectangle(Padding.Left, (int)(layout.Y(c) - line / 2f), layout.LabelWidth, line);
            TextRenderer.DrawText(g, RowLabel(c), Font, row, c == _hover ? ForeColor : muted, HorizontalFlags | TextFormatFlags.EndEllipsis);
        }

        if (_hover >= 0)
        {
            using var band = new SolidBrush(Color.FromArgb(_colors.Dark ? 28 : 18, ForeColor));
            g.FillRectangle(band, plot.Left, plot.Top + layout.Band * _hover, plot.Width, layout.Band);
        }

        var state = g.Save();
        g.SetClip(Rectangle.Inflate(plot, (int)(6 * s), (int)(6 * s)));
        for (int c = 0; c < count; c++)
        {
            for (int i = 0; i < Series.Count; i++)
            {
                var bar = HorizontalBarBounds(layout, i, c);
                if (bar.IsEmpty) continue;
                double value = Series[i].ValueAt(c);
                bool outer = !_stacked || Enumerable.Range(i + 1, Series.Count - i - 1).All(b =>
                {
                    double v = Series[b].ValueAt(c);
                    return double.IsNaN(v) || v == 0 || (v >= 0) != (value >= 0);
                });
                var point = Series[i].PointColor(c);
                var color = point.IsEmpty ? GetSeriesColor(i) : point;
                if (!Enabled) color = TwCharts.Blend(color, surface, 0.5f);
                TwCharts.FillHorizontalBar(g, bar, outer ? 4 * s : 0, value >= 0, color);
            }
        }
        foreach (var reference in ReferenceLines)
        {
            if (double.IsNaN(reference.Value) || double.IsInfinity(reference.Value)) continue;
            float x = layout.X(reference.Value);
            using var pen = GuidePen(reference.Color.IsEmpty ? muted : reference.Color, s);
            g.DrawLine(pen, x, plot.Top, x, plot.Bottom);
        }
        g.Restore(state);

        // Referans etiketi çizginin üst ucunda.
        foreach (var reference in ReferenceLines)
        {
            if (reference.Label.Length == 0 || double.IsNaN(reference.Value) || double.IsInfinity(reference.Value)) continue;
            var size = TextRenderer.MeasureText(g, reference.Label, Font, Size.Empty, HorizontalFlags);
            int x = Math.Max(0, Math.Min(Width - size.Width, (int)layout.X(reference.Value) + (int)(4 * s)));
            using (var back = new SolidBrush(Color.FromArgb(220, surface))) g.FillRectangle(back, x, plot.Top, size.Width, size.Height);
            TextRenderer.DrawText(g, reference.Label, Font, new Point(x, plot.Top), reference.Color.IsEmpty ? muted : reference.Color, HorizontalFlags);
        }

        // Değer etiketleri çubuğun ucunda (yığında satırın sonunda bir kez).
        for (int c = 0; c < count; c++)
        {
            for (int i = 0; i < Series.Count; i++)
            {
                if (!Series[i].ShowValues) continue;
                RectangleF bar;
                if (_stacked)
                {
                    bar = Enumerable.Range(0, Series.Count).Select(j => HorizontalBarBounds(layout, j, c)).Where(b => !b.IsEmpty)
                        .Aggregate(RectangleF.Empty, (a, b) => a.IsEmpty ? b : RectangleF.Union(a, b));
                }
                else bar = HorizontalBarBounds(layout, i, c);
                if (bar.IsEmpty) continue;
                double value = _stacked ? StackTotal(c) : Series[i].ValueAt(c);
                var text = ValueLabel(i, c);
                var size = TextRenderer.MeasureText(g, text, Font, Size.Empty, HorizontalFlags);
                int x = value >= 0 ? (int)bar.Right + (int)(6 * s) : (int)bar.Left - (int)(6 * s) - size.Width;
                TextRenderer.DrawText(g, text, Font, new Point(x, (int)(bar.Top + bar.Height / 2 - size.Height / 2f)), ForeColor, HorizontalFlags);
                if (_stacked) break;
            }
        }

        if (LegendVisible) PaintLegend(g, layout.Legend, muted, s);
        if (_hover >= 0 && Series.Count > 0)
        {
            float end = Enumerable.Range(0, Series.Count).Select(i => HorizontalBarBounds(layout, i, _hover)).Where(b => !b.IsEmpty)
                .Select(b => b.Right).DefaultIfEmpty(plot.Left + plot.Width / 2f).Max();
            var anchor = new Point(_mouse?.X ?? (int)end, (int)layout.Y(_hover));
            var lines = Series.Select((x, i) => ((Color?)GetSeriesColor(i), (Series.Count > 1 ? x.Name + ": " : "") + TwCharts.Format(x.ValueAt(_hover), _valueFormat))).ToList();
            TwCharts.DrawTooltip(g, this, anchor, RowLabel(_hover), lines, s);
        }
        if (Focused && ShowFocusCues)
            TwPainter.StrokeRounded(g, ClientRectangle, 6 * s, 2 * s, Color.FromArgb(160, GetSeriesColorOrAccent()));
    }

    // Satırın tamamı (etiket dahil) o kategoriye aittir.
    private int HorizontalItemAt(Point client)
    {
        var layout = ComputeHorizontalLayout();
        int count = CategoryCount;
        if (count == 0 || client.Y < layout.Plot.Top || client.Y >= layout.Plot.Bottom || client.X < Padding.Left || client.X > Width - Padding.Right) return -1;
        return Math.Min(count - 1, (int)((client.Y - layout.Plot.Top) / layout.Band));
    }

    private Rectangle HorizontalRowBounds(int index)
    {
        var layout = ComputeHorizontalLayout();
        return Rectangle.Round(new RectangleF(Padding.Left, layout.Plot.Top + layout.Band * index, Width - Padding.Horizontal, layout.Band));
    }
}
