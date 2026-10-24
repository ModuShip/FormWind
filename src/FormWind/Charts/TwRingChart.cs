using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>A ring of a <see cref="TwRingChart"/>: a value filled against its <see cref="Maximum"/>.</summary>
public sealed class TwChartRing : TwChartItem
{
    private double _value;
    private double _maximum = 100;

    public TwChartRing()
    {
    }

    public TwChartRing(string label, double value, double maximum = 100)
    {
        Label = label;
        _value = value;
        _maximum = maximum;
    }

    public double Value
    {
        get => _value;
        set
        {
            _value = value;
            OnChanged();
        }
    }

    /// <summary>The value of a full ring (default 100).</summary>
    public double Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            OnChanged();
        }
    }

    /// <summary>Filled share, 0–1; missing values and non-positive maximums are empty.</summary>
    internal double Fraction => double.IsNaN(_value) || double.IsInfinity(_value) || !(_maximum > 0) ? 0 : Math.Max(0, Math.Min(1, _value / _maximum));
}

/// <summary>
/// Concentric progress rings, outermost first, with a large text in the middle: a single completion ring ("6/9"), a score,
/// or the parts of a product such as OEE = availability × performance × quality. Each ring fills clockwise from 12 o'clock
/// over a track (<c>divide-*</c>) with round ends; rings take the palette in order (the first can be set with
/// <c>accent-*</c>). With two or more rings a legend lists each value beside them. Hovering or the arrow keys highlight a
/// ring and show its value in the middle; screen readers get one item per ring.
/// <code>
/// rings.CenterText = "71,4"; rings.CenterLabel = "OEE %";
/// rings.SetData(("Availability", 82.1), ("Performance", 91.3), ("Quality", 95.2));
/// </code>
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ItemClick))]
[Description("Concentric progress rings.")]
public class TwRingChart : Control, ITwChartControl, ITwChartItems
{
    private float _thickness = 0.16f;
    private string? _centerText;
    private string _centerLabel = "";
    private string? _valueFormat;
    private bool _showLegend = true;
    private int _hover = -1;
    private TwChartColors _colors = new TwChartColors();

    public TwRingChart()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Color.Transparent;
        Rings = new TwChartItemCollection<TwChartRing>(OnDataChanged);
    }

    protected override Size DefaultSize => new Size(160, 160);

    /// <summary>A score ring: a track and an arc of <paramref name="fraction"/> (0–1) with a short text in the middle.</summary>
    internal static void DrawScore(Graphics g, Rectangle box, double fraction, Color color, Color track, string? text, Font font, Color textColor, float scale)
    {
        int d = Math.Min(box.Width, box.Height);
        if (d <= 2) return;
        float width = Math.Max(2 * scale, Math.Min(3.5f * scale, d * 0.14f));
        var ring = new RectangleF(box.X + (box.Width - d) / 2f + width / 2, box.Y + (box.Height - d) / 2f + width / 2, d - width, d - width);
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var pen = new Pen(track, width)) g.DrawEllipse(pen, ring);
        float sweep = (float)(Math.Max(0, Math.Min(1, double.IsNaN(fraction) ? 0 : fraction)) * 360);
        if (sweep > 0)
        {
            using var bar = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (sweep >= 359.9f) g.DrawEllipse(bar, ring);
            else g.DrawArc(bar, ring, -90, sweep);
        }
        g.Restore(state);
        if (string.IsNullOrEmpty(text)) return;
        float inner = d - width * 2;
        float size = Math.Max(5.5f, Math.Min(font.SizeInPoints, inner / scale * 0.4f));
        using var bold = new Font(font.FontFamily, size, FontStyle.Bold, GraphicsUnit.Point);
        TextRenderer.DrawText(g, text, bold, Rectangle.Round(ring), textColor,
            Flags & ~TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    protected override Padding DefaultPadding => new Padding(4);

    public event EventHandler<TwChartItemEventArgs>? ItemClick;

    public event EventHandler? HoverIndexChanged;

    /// <summary>The rings, outermost first.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwChartItemCollection<TwChartRing> Rings { get; }

    /// <summary>Width of a ring as a share of the radius (0.04–0.4); many rings get narrower to fit.</summary>
    [Category("Appearance"), DefaultValue(0.16f)]
    public float Thickness
    {
        get => _thickness;
        set
        {
            _thickness = Math.Max(0.04f, Math.Min(0.4f, value));
            Invalidate();
        }
    }

    /// <summary>Large text in the middle; <c>null</c> = the value of a single ring (nothing for several).</summary>
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

    /// <summary>Small text under the middle text.</summary>
    [Category("Appearance"), DefaultValue("")]
    public string CenterLabel
    {
        get => _centerLabel;
        set
        {
            _centerLabel = value ?? "";
            Invalidate();
        }
    }

    /// <summary>Format of the values (default <c>#,0.##</c>; <c>'%'0.0</c> writes %82,1).</summary>
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

    /// <summary>Legend beside the rings (shown for two or more rings when there is room).</summary>
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

    /// <summary>The highlighted ring (-1 = none).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int HoverIndex
    {
        get => _hover;
        set
        {
            value = value < 0 || value >= Rings.Count ? -1 : value;
            if (value == _hover) return;
            _hover = value;
            Invalidate();
            HoverIndexChanged?.Invoke(this, EventArgs.Empty);
            if (value >= 0 && Focused) AccessibilityNotifyClients(AccessibleEvents.Focus, value);
        }
    }

    /// <summary>Replaces the rings with labeled values out of 100.</summary>
    public void SetData(params (string Label, double Value)[] rings)
    {
        Rings.Clear();
        foreach (var (label, value) in rings ?? Array.Empty<(string, double)>()) Rings.Add(new TwChartRing(label, value));
    }

    public Color GetRingColor(int index) =>
        !Rings[index].Color.IsEmpty ? Rings[index].Color : TwCharts.SeriesColor(index, _colors.Dark, _colors.Accent);

    private void OnDataChanged()
    {
        if (_hover >= Rings.Count) _hover = -1;
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

    private int LegendLine => TextRenderer.MeasureText("Ag", Font, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis).Height + (int)(8 * DpiScale);

    private string LegendText(int index) => Rings[index].Label + "  " + TwCharts.Format(Rings[index].Value, _valueFormat);

    /// <summary>The square the rings fill and the legend beside it (empty when hidden or there is no room).</summary>
    internal (RectangleF Ring, Rectangle Legend) ComputeLayout()
    {
        float s = DpiScale;
        var content = Rectangle.FromLTRB(Padding.Left, Padding.Top, Math.Max(Padding.Left, Width - Padding.Right), Math.Max(Padding.Top, Height - Padding.Bottom));
        float d = Math.Min(content.Width, content.Height);
        if (_showLegend && Rings.Count >= 2)
        {
            int legendWidth = Enumerable.Range(0, Rings.Count).Max(i => TextRenderer.MeasureText(LegendText(i), Font, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis).Width) + (int)(28 * s);
            int gap = (int)(16 * s);
            float beside = Math.Min(content.Height, content.Width - legendWidth - gap);
            if (beside >= Math.Min(content.Height, 64 * s))
            {
                var ring = new RectangleF(content.X, content.Y + (content.Height - beside) / 2, beside, beside);
                int height = Math.Min(Rings.Count * LegendLine, content.Height);
                return (ring, new Rectangle((int)ring.Right + gap, content.Y + (content.Height - height) / 2, Math.Max(0, content.Right - (int)ring.Right - gap), height));
            }
        }
        return (new RectangleF(content.X + (content.Width - d) / 2, content.Y + (content.Height - d) / 2, d, d), Rectangle.Empty);
    }

    /// <summary>Center-line radius and width of each ring, outermost first.</summary>
    internal List<(float Radius, float Width)> RingRadii(RectangleF ring)
    {
        float s = DpiScale, outer = ring.Width / 2, gap = 3 * s;
        int n = Rings.Count;
        var result = new List<(float, float)>();
        if (n == 0 || outer <= 0) return result;
        // Halkalar yarıçapın en çok %70'ini kaplar; ortada metin için yer kalır.
        // Küçük halka (liste satırındaki skor) okunsun diye en az 3 px kalınlıkta çizilir.
        float width = Math.Max(Math.Min(3 * s, outer * 0.3f), Math.Max(2 * s, Math.Min(outer * _thickness, (outer * 0.7f - (n - 1) * gap) / n)));
        for (int i = 0; i < n; i++) result.Add((outer - width / 2 - i * (width + gap), width));
        return result;
    }

    // ---- Çizim ----

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        TwPainter.PaintParentBackground(this, g);
        if (BackColor.A > 0) using (var brush = new SolidBrush(BackColor)) g.FillRectangle(brush, ClientRectangle);
        float s = DpiScale;
        var (ring, legend) = ComputeLayout();
        var surface = TwCharts.Surface(this);
        var muted = TwCharts.Muted(this, _colors);
        var track = TwCharts.Grid(this, _colors);
        var center = new PointF(ring.X + ring.Width / 2, ring.Y + ring.Height / 2);
        var radii = RingRadii(ring);

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = 0; i < radii.Count; i++)
        {
            var (r, w) = radii[i];
            if (r <= 0) continue;
            var box = new RectangleF(center.X - r, center.Y - r, r * 2, r * 2);
            using (var pen = new Pen(track, w)) g.DrawEllipse(pen, box);
            float sweep = (float)(Rings[i].Fraction * 360);
            if (sweep <= 0) continue;
            var color = GetRingColor(i);
            if (!Enabled) color = TwCharts.Blend(color, surface, 0.5f);
            else if (_hover >= 0 && _hover != i) color = TwCharts.Blend(color, surface, 0.35f);
            using var bar = new Pen(color, w) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (sweep >= 359.9f) g.DrawEllipse(bar, box);
            else g.DrawArc(bar, box, -90, sweep);
        }
        g.Restore(state);

        // Orta metin: vurgulanan halkanın değeri ve adı, yoksa CenterText.
        string big, small;
        if (_hover >= 0)
        {
            big = TwCharts.Format(Rings[_hover].Value, _valueFormat);
            small = Rings[_hover].Label;
        }
        else
        {
            big = _centerText ?? (Rings.Count == 1 ? TwCharts.Format(Rings[0].Value, _valueFormat) : "");
            small = _centerLabel;
        }
        float inner = radii.Count == 0 ? ring.Width / 2 : radii[radii.Count - 1].Radius - radii[radii.Count - 1].Width / 2;
        if (inner > 6 * s)
        {
            // Küçük halkada yazı halkanın içine sığacak kadar küçülür (en az 6 pt); büyükte yazı boyutundan aşağı inmez.
            float fit = inner / s * 0.42f;
            float size = Math.Min(Font.SizeInPoints * 2.4f, Math.Max(fit, Math.Max(6f, Math.Min(Font.SizeInPoints, inner / s * 0.6f))));
            using var bigFont = new Font(Font.FontFamily, size, FontStyle.Bold, GraphicsUnit.Point);
            int width = (int)(inner * 1.7f);
            var bigSize = big.Length > 0 ? TextRenderer.MeasureText(big, bigFont, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis) : Size.Empty;
            var smallSize = small.Length > 0 ? TextRenderer.MeasureText(small, Font, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis) : Size.Empty;
            // Alt yazı halkanın içine sığmıyorsa gösterilmez.
            if (bigSize.Height + smallSize.Height > inner * 1.8f)
            {
                small = "";
                smallSize = Size.Empty;
            }
            int total = bigSize.Height + smallSize.Height;
            int y = (int)(center.Y - total / 2f);
            if (big.Length > 0)
                TextRenderer.DrawText(g, big, bigFont, new Rectangle((int)(center.X - width / 2f), y, width, bigSize.Height), ForeColor, Flags | TextFormatFlags.HorizontalCenter);
            if (small.Length > 0)
                TextRenderer.DrawText(g, small, Font, new Rectangle((int)(center.X - width / 2f), y + bigSize.Height, width, smallSize.Height), muted, Flags | TextFormatFlags.HorizontalCenter);
        }

        if (!legend.IsEmpty)
        {
            int line = LegendLine, key = (int)(10 * s), gap = (int)(8 * s);
            for (int i = 0; i < Rings.Count; i++)
            {
                var row = new Rectangle(legend.X, legend.Y + i * line, legend.Width, line);
                if (row.Bottom > legend.Bottom + 1) break;
                if (i == _hover) TwPainter.FillRounded(g, row, 4 * s, Color.FromArgb(_colors.Dark ? 28 : 18, ForeColor));
                TwPainter.FillRounded(g, new Rectangle(row.X + (int)(4 * s), row.Y + (line - key) / 2, key, key), key / 2f, GetRingColor(i));
                int x = row.X + (int)(4 * s) + key + gap;
                var label = Rings[i].Label;
                int labelWidth = Math.Min(TextRenderer.MeasureText(label, Font, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis).Width, Math.Max(0, row.Right - x));
                TextRenderer.DrawText(g, label, Font, new Rectangle(x, row.Y, labelWidth, line), muted, Flags | TextFormatFlags.VerticalCenter);
                x += labelWidth + gap;
                if (x < row.Right)
                    TextRenderer.DrawText(g, TwCharts.Format(Rings[i].Value, _valueFormat), Font, new Rectangle(x, row.Y, row.Right - x, line), ForeColor, Flags | TextFormatFlags.VerticalCenter);
            }
        }
        if (Focused && ShowFocusCues)
            TwPainter.StrokeRounded(g, ClientRectangle, 6 * s, 2 * s, Color.FromArgb(160, _colors.Accent ?? TwCharts.SeriesColor(0, _colors.Dark, null)));
    }

    // ---- Etkileşim ----

    /// <summary>The ring (or legend row) under a client point (-1 = none).</summary>
    public int ItemAt(Point client)
    {
        var (ring, legend) = ComputeLayout();
        if (legend.Contains(client))
        {
            int row = (client.Y - legend.Y) / LegendLine;
            return row < Rings.Count ? row : -1;
        }
        float dx = client.X - (ring.X + ring.Width / 2), dy = client.Y - (ring.Y + ring.Height / 2);
        double distance = Math.Sqrt(dx * dx + dy * dy);
        var radii = RingRadii(ring);
        float slack = 1.5f * DpiScale;
        for (int i = 0; i < radii.Count; i++)
            if (Math.Abs(distance - radii[i].Radius) <= radii[i].Width / 2 + slack) return i;
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
        if (e.Button == MouseButtons.Left && index >= 0) ItemClick?.Invoke(this, new TwChartItemEventArgs(index, Rings[index].Label));
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & ~Keys.Shift) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int count = Rings.Count;
        if (count == 0) return;
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Up: HoverIndex = _hover <= 0 ? 0 : _hover - 1; break;
            case Keys.Right or Keys.Down: HoverIndex = _hover < 0 ? 0 : Math.Min(count - 1, _hover + 1); break;
            case Keys.Home: HoverIndex = 0; break;
            case Keys.End: HoverIndex = count - 1; break;
            case Keys.Enter when _hover >= 0: ItemClick?.Invoke(this, new TwChartItemEventArgs(_hover, Rings[_hover].Label)); break;
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

    int ITwChartItems.ItemCount => Rings.Count;

    int ITwChartItems.ActiveItem
    {
        get => _hover;
        set => HoverIndex = value;
    }

    Rectangle ITwChartItems.ItemBounds(int index)
    {
        var (ring, legend) = ComputeLayout();
        return legend.IsEmpty ? Rectangle.Round(ring) : new Rectangle(legend.X, legend.Y + index * LegendLine, legend.Width, LegendLine);
    }

    /// <summary>Text of a ring for screen readers: label, value and maximum.</summary>
    public string GetItemText(int index) =>
        Rings[index].Label + ": " + TwCharts.Format(Rings[index].Value, _valueFormat) + " / " + TwCharts.Format(Rings[index].Maximum, _valueFormat);

    string ITwChartItems.ItemText(int index) => GetItemText(index);

    string ITwChartItems.Summary => string.Join(" ", new[] { _centerText ?? "", _centerLabel }.Where(x => x.Length > 0));

    protected override AccessibleObject CreateAccessibilityInstance() => new TwChartAccessibleObject(this, this);
}
