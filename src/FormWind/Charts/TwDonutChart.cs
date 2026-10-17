using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>A part of a <see cref="TwDonutChart"/>.</summary>
public sealed class TwChartSlice
{
    private string _label = "";
    private double _value;
    private Color _color = Color.Empty;

    public TwChartSlice()
    {
    }

    public TwChartSlice(string label, double value)
    {
        _label = label ?? "";
        _value = value;
    }

    public string Label
    {
        get => _label;
        set
        {
            _label = value ?? "";
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The slice's value; negative and missing values are drawn as zero.</summary>
    public double Value
    {
        get => _value;
        set
        {
            _value = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Empty = the palette color of the slice's position.</summary>
    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    internal double Amount => double.IsNaN(_value) || double.IsInfinity(_value) ? 0 : Math.Max(0, _value);

    internal event EventHandler? Changed;
}

/// <summary>The slices of a <see cref="TwDonutChart"/>; changes repaint the chart.</summary>
public sealed class TwChartSliceCollection : Collection<TwChartSlice>
{
    private readonly Action _changed;

    internal TwChartSliceCollection(Action changed) => _changed = changed;

    public TwChartSlice Add(string label, double value)
    {
        var slice = new TwChartSlice(label, value);
        Add(slice);
        return slice;
    }

    protected override void InsertItem(int index, TwChartSlice item)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));
        base.InsertItem(index, item);
        item.Changed += OnChanged;
        _changed();
    }

    protected override void SetItem(int index, TwChartSlice item)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));
        this[index].Changed -= OnChanged;
        base.SetItem(index, item);
        item.Changed += OnChanged;
        _changed();
    }

    protected override void RemoveItem(int index)
    {
        this[index].Changed -= OnChanged;
        base.RemoveItem(index);
        _changed();
    }

    protected override void ClearItems()
    {
        foreach (var item in this) item.Changed -= OnChanged;
        base.ClearItems();
        _changed();
    }

    private void OnChanged(object? sender, EventArgs e) => _changed();
}

/// <summary>
/// Donut chart: parts of a whole with the total (or <see cref="CenterText"/>) in the middle and a legend with values and
/// shares. Slices take the palette in order with a 2 px gap in the surface color; past eight slices the rest are folded
/// into <see cref="TwTexts.Other"/>. Hovering or the arrow keys highlight a slice and show its value; screen readers get
/// one item per slice. Legend and center label use <c>label:text-*</c>.
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ItemClick))]
[Description("Donut chart with a legend.")]
public class TwDonutChart : Control, ITwChartControl, ITwChartItems
{
    internal const int MaxSlices = 8;

    private float _thickness = 0.28f;
    private string? _centerText;
    private string? _centerLabel;
    private string? _valueFormat;
    private bool _showLegend = true;
    private int _hover = -1;
    private TwChartColors _colors = new TwChartColors();

    public TwDonutChart()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Color.Transparent;
        Slices = new TwChartSliceCollection(OnDataChanged);
    }

    protected override Size DefaultSize => new Size(360, 200);

    protected override Padding DefaultPadding => new Padding(8);

    public event EventHandler<TwChartItemEventArgs>? ItemClick;

    public event EventHandler? HoverIndexChanged;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwChartSliceCollection Slices { get; }

    /// <summary>Ring thickness as a share of the radius (0.1–0.6).</summary>
    [Category("Appearance"), DefaultValue(0.28f)]
    public float Thickness
    {
        get => _thickness;
        set
        {
            _thickness = Math.Max(0.1f, Math.Min(0.6f, value));
            Invalidate();
        }
    }

    /// <summary>Large text in the middle; <c>null</c> = the total, formatted with <see cref="ValueFormat"/>.</summary>
    [Category("Appearance"), DefaultValue(null)]
    public string? CenterText
    {
        get => _centerText;
        set
        {
            _centerText = value;
            Invalidate();
        }
    }

    /// <summary>Small text under the middle text; <c>null</c> = <see cref="TwTexts.Total"/>, empty = none.</summary>
    [Category("Appearance"), DefaultValue(null)]
    public string? CenterLabel
    {
        get => _centerLabel;
        set
        {
            _centerLabel = value;
            Invalidate();
        }
    }

    [Category("Appearance"), DefaultValue(null)]
    public string? ValueFormat
    {
        get => _valueFormat;
        set
        {
            _valueFormat = value;
            Invalidate();
        }
    }

    /// <summary>Legend with each slice's value and share: beside the ring when there is room, otherwise below it.</summary>
    [Category("Appearance"), DefaultValue(true)]
    public bool ShowLegend
    {
        get => _showLegend;
        set
        {
            _showLegend = value;
            Invalidate();
        }
    }

    /// <summary>The highlighted slice (-1 = none), an index into <see cref="DisplaySlices"/>.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int HoverIndex
    {
        get => _hover;
        set
        {
            value = value < 0 || value >= DisplaySlices.Count ? -1 : value;
            if (value == _hover) return;
            _hover = value;
            Invalidate();
            HoverIndexChanged?.Invoke(this, EventArgs.Empty);
            if (value >= 0 && Focused) AccessibilityNotifyClients(AccessibleEvents.Focus, value);
        }
    }

    /// <summary>Replaces the slices with labels and values.</summary>
    public void SetData(IEnumerable<string> labels, IEnumerable<double> values)
    {
        Slices.Clear();
        foreach (var (label, value) in labels.Zip(values, (l, v) => (l, v))) Slices.Add(label, value);
    }

    public double Total => Slices.Sum(s => s.Amount);

    /// <summary>The slices as drawn: up to eight, the rest folded into "Other".</summary>
    public IReadOnlyList<TwChartSlice> DisplaySlices
    {
        get
        {
            if (Slices.Count <= MaxSlices) return Slices;
            var shown = Slices.Take(MaxSlices - 1).ToList();
            shown.Add(new TwChartSlice(TwTexts.Current.Other, Slices.Skip(MaxSlices - 1).Sum(s => s.Amount))
            {
                Color = ColorTranslator.FromHtml(_colors.Dark ? "#6b7280" : "#9ca3af"),
            });
            return shown;
        }
    }

    public Color GetSliceColor(int index)
    {
        var slices = DisplaySlices;
        return !slices[index].Color.IsEmpty ? slices[index].Color : TwCharts.SeriesColor(index, _colors.Dark, _colors.Accent);
    }

    private void OnDataChanged()
    {
        if (_hover >= DisplaySlices.Count) _hover = -1;
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.Reorder, -1);
    }

    void ITwChartControl.SetChartColors(TwChartColors colors)
    {
        _colors = colors;
        Invalidate();
    }

    // ---- Yerleşim ----

    private float DpiScale => TwDpi.DeviceDpi(this) / 96f;

    private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

    internal (RectangleF Ring, Rectangle Legend, bool Beside) ComputeLayout()
    {
        float s = DpiScale;
        var content = Rectangle.FromLTRB(Padding.Left, Padding.Top, Math.Max(Padding.Left, Width - Padding.Right), Math.Max(Padding.Top, Height - Padding.Bottom));
        if (!_showLegend || DisplaySlices.Count == 0)
        {
            float d = Math.Min(content.Width, content.Height);
            return (new RectangleF(content.X + (content.Width - d) / 2, content.Y + (content.Height - d) / 2, d, d), Rectangle.Empty, false);
        }
        int line = LegendLine;
        int legendWidth = DisplaySlices.Select((x, i) => TextRenderer.MeasureText(LegendText(i), Font, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis).Width).DefaultIfEmpty(0).Max()
                          + (int)(16 * s);
        int legendHeight = DisplaySlices.Count * line;
        int gap = (int)(16 * s);
        // Yan yana sığıyorsa gösterge sağda; yoksa altta.
        float besideDiameter = Math.Min(content.Height, content.Width - legendWidth - gap);
        if (besideDiameter >= Math.Min(content.Height, 80 * s))
        {
            var ring = new RectangleF(content.X, content.Y + (content.Height - besideDiameter) / 2, besideDiameter, besideDiameter);
            var legend = new Rectangle((int)ring.Right + gap, content.Y + Math.Max(0, (content.Height - legendHeight) / 2), Math.Max(0, content.Right - (int)ring.Right - gap), Math.Min(legendHeight, content.Height));
            return (ring, legend, true);
        }
        float below = Math.Max(0, Math.Min(content.Width, content.Height - legendHeight - gap));
        var top = new RectangleF(content.X + (content.Width - below) / 2, content.Y, below, below);
        return (top, new Rectangle(content.X, (int)top.Bottom + gap, content.Width, legendHeight), false);
    }

    private int LegendLine => TextRenderer.MeasureText("Ag", Font, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis).Height + (int)(8 * DpiScale);

    private string LegendText(int index)
    {
        var slice = DisplaySlices[index];
        double total = Total;
        string share = total > 0 ? (slice.Amount / total).ToString("0.#%", CultureInfo.CurrentCulture) : "0%";
        return slice.Label + "  " + TwCharts.Format(slice.Amount, _valueFormat) + "  (" + share + ")";
    }

    /// <summary>Start and sweep angles of each slice (degrees, clockwise from 12 o'clock).</summary>
    internal List<(float Start, float Sweep)> Angles()
    {
        var result = new List<(float, float)>();
        double total = Total;
        float start = -90;
        foreach (var slice in DisplaySlices)
        {
            float sweep = total > 0 ? (float)(slice.Amount / total * 360) : 0;
            result.Add((start, sweep));
            start += sweep;
        }
        return result;
    }

    // ---- Çizim ----

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        TwPainter.PaintParentBackground(this, g);
        if (BackColor.A > 0) using (var brush = new SolidBrush(BackColor)) g.FillRectangle(brush, ClientRectangle);
        float s = DpiScale;
        var (ring, legend, _) = ComputeLayout();
        var surface = TwCharts.Surface(this);
        var muted = TwCharts.Muted(this, _colors);
        var slices = DisplaySlices;
        var angles = Angles();
        float outer = ring.Width / 2;
        float inner = outer * (1 - _thickness);
        var center = new PointF(ring.X + outer, ring.Y + outer);

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (Total <= 0 && ring.Width > 2)
        {
            // Veri yoksa boş halka.
            using var empty = new Pen(TwCharts.Grid(this, _colors), outer - inner);
            float mid = (outer + inner) / 2;
            g.DrawEllipse(empty, center.X - mid, center.Y - mid, mid * 2, mid * 2);
        }
        for (int i = 0; i < slices.Count; i++)
        {
            var (start, sweep) = angles[i];
            if (sweep <= 0) continue;
            // Vurgulanan dilim biraz dışa taşar.
            float grow = i == _hover ? 4 * s : 0;
            var color = GetSliceColor(i);
            if (!Enabled) color = TwCharts.Blend(color, surface, 0.5f);
            using var path = Wedge(center, outer + grow, inner, start, sweep);
            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }
        // Dilimler arasında yüzey renginde 2 px boşluk.
        if (slices.Count(x => x.Amount > 0) > 1)
        {
            using var pen = new Pen(surface, 2 * s);
            foreach (var (start, sweep) in angles.Where(a => a.Sweep > 0))
            {
                double radians = start * Math.PI / 180;
                g.DrawLine(pen, center.X + (float)Math.Cos(radians) * (inner - 1), center.Y + (float)Math.Sin(radians) * (inner - 1),
                    center.X + (float)Math.Cos(radians) * (outer + 5 * s), center.Y + (float)Math.Sin(radians) * (outer + 5 * s));
            }
        }
        g.Restore(state);

        // Orta metin: toplam (ya da vurgulanan dilim).
        string big, small;
        if (_hover >= 0)
        {
            big = TwCharts.Format(slices[_hover].Amount, _valueFormat);
            small = slices[_hover].Label;
        }
        else
        {
            big = _centerText ?? TwCharts.Format(Total, _valueFormat);
            small = _centerLabel ?? TwTexts.Current.Total;
        }
        using (var bigFont = new Font(Font.FontFamily, Font.SizeInPoints * 1.6f, FontStyle.Bold, GraphicsUnit.Point))
        {
            int width = (int)(inner * 1.6f);
            var bigSize = TextRenderer.MeasureText(big, bigFont, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis);
            var smallSize = small.Length > 0 ? TextRenderer.MeasureText(small, Font, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis) : Size.Empty;
            int total = bigSize.Height + smallSize.Height;
            if (inner > 12 * s && total < inner * 1.8f)
            {
                int y = (int)(center.Y - total / 2f);
                TextRenderer.DrawText(g, big, bigFont, new Rectangle((int)(center.X - width / 2f), y, width, bigSize.Height), ForeColor, Flags | TextFormatFlags.HorizontalCenter);
                if (small.Length > 0)
                    TextRenderer.DrawText(g, small, Font, new Rectangle((int)(center.X - width / 2f), y + bigSize.Height, width, smallSize.Height), muted, Flags | TextFormatFlags.HorizontalCenter);
            }
        }

        if (!legend.IsEmpty)
        {
            int line = LegendLine, key = (int)(10 * s), gap = (int)(8 * s);
            for (int i = 0; i < slices.Count; i++)
            {
                var row = new Rectangle(legend.X, legend.Y + i * line, legend.Width, line);
                if (row.Bottom > legend.Bottom + 1) break;
                if (i == _hover) TwPainter.FillRounded(g, row, 4 * s, Color.FromArgb(_colors.Dark ? 28 : 18, ForeColor));
                TwPainter.FillRounded(g, new Rectangle(row.X + (int)(4 * s), row.Y + (line - key) / 2, key, key), 2 * s, GetSliceColor(i));
                var slice = slices[i];
                int x = row.X + (int)(4 * s) + key + gap;
                int labelWidth = TextRenderer.MeasureText(slice.Label, Font, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis).Width;
                TextRenderer.DrawText(g, slice.Label, Font, new Rectangle(x, row.Y, Math.Min(labelWidth, Math.Max(0, row.Right - x)), line), ForeColor, Flags | TextFormatFlags.VerticalCenter);
                x += labelWidth + gap;
                var rest = LegendText(i).Substring(slice.Label.Length).Trim();
                if (x < row.Right) TextRenderer.DrawText(g, rest, Font, new Rectangle(x, row.Y, row.Right - x, line), muted, Flags | TextFormatFlags.VerticalCenter);
            }
        }
        if (Focused && ShowFocusCues)
            TwPainter.StrokeRounded(g, ClientRectangle, 6 * s, 2 * s, Color.FromArgb(160, _colors.Accent ?? TwCharts.SeriesColor(0, _colors.Dark, null)));
    }

    private static GraphicsPath Wedge(PointF center, float outer, float inner, float start, float sweep)
    {
        var path = new GraphicsPath();
        if (sweep >= 359.99f)
        {
            path.AddEllipse(center.X - outer, center.Y - outer, outer * 2, outer * 2);
            path.AddEllipse(center.X - inner, center.Y - inner, inner * 2, inner * 2);
            return path;
        }
        path.AddArc(center.X - outer, center.Y - outer, outer * 2, outer * 2, start, sweep);
        path.AddArc(center.X - inner, center.Y - inner, inner * 2, inner * 2, start + sweep, -sweep);
        path.CloseFigure();
        return path;
    }

    // ---- Etkileşim ----

    /// <summary>The slice (or legend row) under a client point (-1 = none).</summary>
    public int ItemAt(Point client)
    {
        var (ring, legend, _) = ComputeLayout();
        var slices = DisplaySlices;
        if (legend.Contains(client))
        {
            int row = (client.Y - legend.Y) / LegendLine;
            return row < slices.Count ? row : -1;
        }
        float outer = ring.Width / 2, inner = outer * (1 - _thickness);
        float dx = client.X - (ring.X + outer), dy = client.Y - (ring.Y + outer);
        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < inner || distance > outer + 4 * DpiScale) return -1;
        double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        var angles = Angles();
        for (int i = 0; i < angles.Count; i++)
        {
            double from = angles[i].Start, offset = ((angle - from) % 360 + 360) % 360;
            if (angles[i].Sweep > 0 && offset < angles[i].Sweep) return i;
        }
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        HoverIndex = ItemAt(e.Location);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!Focused) HoverIndex = -1;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (CanFocus) Focus();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int index = ItemAt(e.Location);
        if (e.Button == MouseButtons.Left && index >= 0) ItemClick?.Invoke(this, new TwChartItemEventArgs(index, DisplaySlices[index].Label));
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & ~Keys.Shift) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int count = DisplaySlices.Count;
        if (count == 0) return;
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Up: HoverIndex = _hover <= 0 ? 0 : _hover - 1; break;
            case Keys.Right or Keys.Down: HoverIndex = _hover < 0 ? 0 : Math.Min(count - 1, _hover + 1); break;
            case Keys.Home: HoverIndex = 0; break;
            case Keys.End: HoverIndex = count - 1; break;
            case Keys.Enter when _hover >= 0: ItemClick?.Invoke(this, new TwChartItemEventArgs(_hover, DisplaySlices[_hover].Label)); break;
            default: return;
        }
        e.Handled = true;
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        HoverIndex = -1;
        Invalidate();
    }

    // ---- Erişilebilirlik ----

    int ITwChartItems.ItemCount => DisplaySlices.Count;

    int ITwChartItems.ActiveItem
    {
        get => _hover;
        set => HoverIndex = value;
    }

    Rectangle ITwChartItems.ItemBounds(int index)
    {
        var (ring, legend, _) = ComputeLayout();
        // Dilimin kendisi yay biçimlidir; erişilebilirlik için gösterge satırı (yoksa halkanın tamamı) verilir.
        return legend.IsEmpty ? Rectangle.Round(ring) : new Rectangle(legend.X, legend.Y + index * LegendLine, legend.Width, LegendLine);
    }

    /// <summary>Text of a slice for screen readers: label, value and share.</summary>
    public string GetItemText(int index) => LegendText(index).Replace("  (", " (").Replace("  ", ": ");

    string ITwChartItems.ItemText(int index) => GetItemText(index);

    string ITwChartItems.Summary => (_centerLabel ?? TwTexts.Current.Total) + ": " + (_centerText ?? TwCharts.Format(Total, _valueFormat));

    protected override AccessibleObject CreateAccessibilityInstance() => new TwChartAccessibleObject(this, this);
}
