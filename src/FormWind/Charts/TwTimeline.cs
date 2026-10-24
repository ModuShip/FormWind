using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>An event on a <see cref="TwTimeline"/>: a time and a size (a duration, a cost, a count).</summary>
public sealed class TwTimelineEvent : TwChartItem
{
    private DateTime _time;
    private double _value;

    public TwTimelineEvent()
    {
    }

    public TwTimelineEvent(DateTime time, double value, string label = "")
    {
        _time = time;
        _value = value;
        Label = label;
    }

    public DateTime Time
    {
        get => _time;
        set
        {
            _time = value;
            OnChanged();
        }
    }

    /// <summary>Sets the dot's size (the largest value gets the largest dot) and is written under it.</summary>
    public double Value
    {
        get => _value;
        set
        {
            _value = value;
            OnChanged();
        }
    }
}

/// <summary>
/// Events along a time axis: each a dot placed by its time and sized by its value, with the date above and the value below,
/// joined by a line from the first to the last. Shows how often something recurs and whether the gaps shrink. Dots use the
/// event's color or <c>accent-*</c>; the axis uses <c>divide-*</c> and the texts <c>label:text-*</c>. Hovering or the arrow
/// keys show an event's label, date and value; screen readers get one item per event, in time order.
/// <code>
/// timeline.Items.Add(new TwTimelineEvent(new DateTime(2026, 9, 17), 22, "DR-24410"));
/// timeline.Items.Add(new TwTimelineEvent(new DateTime(2026, 9, 26), 31, "DR-24533"));
/// </code>
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ItemClick))]
[Description("Events on a time axis.")]
public class TwTimeline : Control, ITwChartControl, ITwChartItems
{
    private DateTime? _start;
    private DateTime? _end;
    private string _dateFormat = "d MMM";
    private string? _valueFormat;
    private int _hover = -1;
    private TwChartColors _colors = new TwChartColors();
    private TwChartMotion? _motion;
    private Dictionary<int, (PointF Center, float Radius)> _shownDots = new Dictionary<int, (PointF, float)>();
    private Dictionary<int, (PointF Center, float Radius)> _fromDots = new Dictionary<int, (PointF, float)>();

    public TwTimeline()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Color.Transparent;
        Items = new TwChartItemCollection<TwTimelineEvent>(OnDataChanged);
    }

    protected override Size DefaultSize => new Size(480, 80);

    protected override Padding DefaultPadding => new Padding(8, 4, 8, 4);

    /// <summary>Raised when an event is clicked or Enter is pressed on it; the index is into <see cref="Items"/>.</summary>
    public event EventHandler<TwChartItemEventArgs>? ItemClick;

    public event EventHandler? HoverIndexChanged;

    /// <summary>The events, in any order (they are drawn and read in time order).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwChartItemCollection<TwTimelineEvent> Items { get; }

    /// <summary>Left end of the axis; <c>null</c> = the first event.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime? Start
    {
        get => _start;
        set
        {
            _start = value;
            Invalidate();
        }
    }

    /// <summary>Right end of the axis; <c>null</c> = the last event.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime? End
    {
        get => _end;
        set
        {
            _end = value;
            Invalidate();
        }
    }

    /// <summary>Format of the dates above the dots (default <c>d MMM</c>: 17 Eyl).</summary>
    [Category("Appearance"), DefaultValue("d MMM")]
    public string DateFormat
    {
        get => _dateFormat;
        set
        {
            _dateFormat = string.IsNullOrEmpty(value) ? "d MMM" : value;
            Invalidate();
        }
    }

    /// <summary>Format of the values under the dots (default <c>#,0.##</c>; <c>0 'dk'</c> writes 22 dk).</summary>
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

    /// <summary>The highlighted event (-1 = none), an index into <see cref="Items"/>.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int HoverIndex
    {
        get => _hover;
        set
        {
            value = value < 0 || value >= Items.Count ? -1 : value;
            if (value == _hover) return;
            _hover = value;
            Invalidate();
            HoverIndexChanged?.Invoke(this, EventArgs.Empty);
            if (value >= 0 && Focused) AccessibilityNotifyClients(AccessibleEvents.Focus, Order().IndexOf(value));
        }
    }

    public Color GetEventColor(int index) =>
        !Items[index].Color.IsEmpty ? Items[index].Color : TwCharts.SeriesColor(0, _colors.Dark, _colors.Accent);

    private void OnDataChanged()
    {
        if (_hover >= Items.Count) _hover = -1;
        Motion.DataChanged(AnimateOnShow, AnimateChanges, Items.Count > 0, Items.Count,
            () => _fromDots = new Dictionary<int, (PointF, float)>(_shownDots), TwEasing.Spring);
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.Reorder, -1);
    }

    void ITwChartControl.SetChartColors(TwChartColors colors)
    {
        _colors = colors;
        Invalidate();
    }

    /// <summary>Indexes of <see cref="Items"/> in time order.</summary>
    private System.Collections.Generic.List<int> Order() => Enumerable.Range(0, Items.Count).OrderBy(i => Items[i].Time).ThenBy(i => i).ToList();

    // ---- Animasyon ----

    private TwChartMotion Motion => _motion ??= new TwChartMotion(this);

    /// <summary>
    /// The first time the timeline is shown with events, the dots pop up in time order with a spring (35 ms apart) and the line
    /// joining them draws from left to right.
    /// Not when animations are off. <see cref="AnimateIn"/> plays it again.
    /// </summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool AnimateOnShow { get; set; } = true;

    /// <summary>
    /// When the events change after the timeline was shown, the dots move and resize from where they are to their new place
    /// (350 ms); new dots grow from nothing.
    /// Not when animations are off.
    /// </summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool AnimateChanges { get; set; } = true;

    /// <summary>Plays the opening animation now (e.g. after loading new data); nothing happens when animations are off.</summary>
    public void AnimateIn()
    {
        Motion.PlayIntro(Items.Count, TwEasing.Spring);
        Invalidate();
    }

    /// <summary>Share (0–1) of the opening animation that has passed; 1 when none runs.</summary>
    internal float IntroProgress => Motion.IntroOverall;

    /// <summary>Progress of the move to new data; 1 when none runs.</summary>
    internal float ChangeProgress => Motion.ChangeProgress;

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        Motion.TryIntro(AnimateOnShow, Items.Count > 0, Items.Count, TwEasing.Spring);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        // Eski konumlar başka bir boyuta aittir; kayma yerine yeni hâl hemen çizilir.
        if (Motion.ChangeRunning) Motion.Stop();
    }

    /// <summary>An event's dot as the last paint drew it.</summary>
    internal (PointF Center, float Radius)? PaintedDot(int index) => _shownDots.TryGetValue(index, out var dot) ? dot : null;

    /// <summary>Turns the target dots into the ones painted now: popping up while opening, moving while the events change.</summary>
    private void ApplyShownDots(List<int> order, PointF[] centers, float[] radii)
    {
        for (int k = 0; k < order.Count; k++)
        {
            int i = order[k];
            if (Motion.IntroRunning) radii[i] *= Math.Max(0f, Motion.IntroProgress(k));
            else if (Motion.ChangeRunning)
            {
                float p = Motion.ChangeProgress;
                var (center, radius) = _fromDots.TryGetValue(i, out var from) ? from : (centers[i], 0f);
                centers[i] = TwChartMotion.Lerp(center, centers[i], p);
                radii[i] = TwChartMotion.Lerp(radius, radii[i], p);
            }
            _shownDots[i] = (centers[i], radii[i]);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _motion?.Dispose();
        base.Dispose(disposing);
    }

    // ---- Yerleşim ----

    private float DpiScale => TwDpi.DeviceDpi(this) / 96f;

    private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    private float MaxRadius => 9 * DpiScale;

    private string DateText(DateTime time) => time.ToString(_dateFormat, CultureInfo.CurrentCulture);

    /// <summary>The axis line (left to right at its height) and each event's dot center and radius.</summary>
    internal (float Left, float Right, float Y, PointF[] Centers, float[] Radii) ComputeLayout()
    {
        float s = DpiScale;
        int line = TextRenderer.MeasureText("Ag", Font, Size.Empty, Flags).Height;
        var content = Rectangle.FromLTRB(Padding.Left, Padding.Top, Math.Max(Padding.Left, Width - Padding.Right), Math.Max(Padding.Top, Height - Padding.Bottom));
        // Tarih üstte, değer altta; uçlardaki etiketler taşmasın diye eksen yarım etiket genişliği içeride başlar.
        int edge = Items.Count == 0 ? 0 : Items.Max(e => Math.Max(TextRenderer.MeasureText(DateText(e.Time), Font, Size.Empty, Flags).Width,
            TextRenderer.MeasureText(TwCharts.Format(e.Value, _valueFormat), Font, Size.Empty, Flags).Width)) / 2;
        float left = content.Left + Math.Max(MaxRadius, edge), right = Math.Max(left + 1, content.Right - Math.Max(MaxRadius, edge));
        float y = content.Top + line + 6 * s + MaxRadius;
        var centers = new PointF[Items.Count];
        var radii = new float[Items.Count];
        if (Items.Count == 0) return (left, right, y, centers, radii);
        var start = _start ?? Items.Min(e => e.Time);
        var end = _end ?? Items.Max(e => e.Time);
        double span = (end - start).TotalSeconds;
        double max = Items.Select(e => Math.Abs(e.Value)).Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).DefaultIfEmpty(0).Max();
        float min = 4 * s;
        for (int i = 0; i < Items.Count; i++)
        {
            double t = span <= 0 ? 0.5 : Math.Max(0, Math.Min(1, (Items[i].Time - start).TotalSeconds / span));
            centers[i] = new PointF(left + (float)t * (right - left), y);
            double v = Math.Abs(Items[i].Value);
            // Alan değerle orantılı: yarıçap karekökle büyür.
            radii[i] = max > 0 && !double.IsNaN(v) ? min + (MaxRadius - min) * (float)Math.Sqrt(Math.Min(1, v / max)) : min;
        }
        return (left, right, y, centers, radii);
    }

    // ---- Çizim ----

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        TwPainter.PaintParentBackground(this, g);
        if (BackColor.A > 0) using (var brush = new SolidBrush(BackColor)) g.FillRectangle(brush, ClientRectangle);
        float s = DpiScale;
        var (left, right, y, centers, radii) = ComputeLayout();
        var surface = TwCharts.Surface(this);
        var muted = TwCharts.Muted(this, _colors);
        var order = Order();
        _shownDots.Clear();
        Motion.MarkPainted(Items.Count > 0);
        ApplyShownDots(order, centers, radii);

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var axis = new Pen(TwCharts.Grid(this, _colors), 2 * s)) g.DrawLine(axis, left - MaxRadius, y, right + MaxRadius, y);
        if (order.Count > 1)
        {
            var color = GetEventColor(order[order.Count - 1]);
            using var link = new Pen(Color.FromArgb(150, Enabled ? color : TwCharts.Blend(color, surface, 0.5f)), 2 * s);
            // Açılışta birleştiren çizgi soldan sağa uzar.
            g.DrawLine(link, centers[order[0]], TwChartMotion.Lerp(centers[order[0]], centers[order[order.Count - 1]], Motion.IntroOverall));
        }
        foreach (int i in order)
        {
            var color = GetEventColor(i);
            if (!Enabled) color = TwCharts.Blend(color, surface, 0.5f);
            if (radii[i] < 0.5f) continue;
            float r = radii[i] + (i == _hover ? 2 * s : 0);
            using (var ring = new SolidBrush(surface)) g.FillEllipse(ring, centers[i].X - r - 2 * s, centers[i].Y - r - 2 * s, (r + 2 * s) * 2, (r + 2 * s) * 2);
            using (var fill = new SolidBrush(color)) g.FillEllipse(fill, centers[i].X - r, centers[i].Y - r, r * 2, r * 2);
        }
        g.Restore(state);

        // Tarih ve değer etiketleri; öncekiyle çakışan atlanır (vurgulanan her zaman yazılır).
        float lastRight = float.MinValue;
        using var bold = new Font(Font, FontStyle.Bold);
        foreach (int i in order)
        {
            // Etiketler noktası açıldıkça yazılır.
            if (Motion.IntroRunning && Motion.IntroProgress(order.IndexOf(i)) < 0.9f) continue;
            var date = DateText(Items[i].Time);
            var value = TwCharts.Format(Items[i].Value, _valueFormat);
            var dateSize = TextRenderer.MeasureText(date, Font, Size.Empty, Flags);
            var valueSize = TextRenderer.MeasureText(value, bold, Size.Empty, Flags);
            float half = Math.Max(dateSize.Width, valueSize.Width) / 2f;
            if (centers[i].X - half < lastRight + 4 * s && i != _hover) continue;
            lastRight = centers[i].X + half;
            TextRenderer.DrawText(g, date, Font, new Point((int)(centers[i].X - dateSize.Width / 2f), (int)(y - MaxRadius - 6 * s - dateSize.Height)), muted, Flags);
            TextRenderer.DrawText(g, value, bold, new Point((int)(centers[i].X - valueSize.Width / 2f), (int)(y + MaxRadius + 6 * s)), ForeColor, Flags);
        }

        if (_hover >= 0)
        {
            var ev = Items[_hover];
            var title = ev.Label.Length > 0 ? ev.Label : DateText(ev.Time);
            var lines = new[] { ((Color?)GetEventColor(_hover), (ev.Label.Length > 0 ? DateText(ev.Time) + " · " : "") + TwCharts.Format(ev.Value, _valueFormat)) };
            TwCharts.DrawTooltip(g, this, Point.Round(centers[_hover]), title, lines, s);
        }
        if (Focused && ShowFocusCues)
            TwPainter.StrokeRounded(g, ClientRectangle, 6 * s, 2 * s, Color.FromArgb(160, _colors.Accent ?? TwCharts.SeriesColor(0, _colors.Dark, null)));
    }

    // ---- Etkileşim ----

    /// <summary>The event whose dot is nearest a client point, within a few pixels (-1 = none).</summary>
    public int ItemAt(Point client)
    {
        var (_, _, _, centers, radii) = ComputeLayout();
        int best = -1;
        double nearest = double.MaxValue;
        for (int i = 0; i < centers.Length; i++)
        {
            double dx = client.X - centers[i].X, dy = client.Y - centers[i].Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance <= radii[i] + 6 * DpiScale && distance < nearest)
            {
                nearest = distance;
                best = i;
            }
        }
        return best;
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
        if (e.Button == MouseButtons.Left && index >= 0) ItemClick?.Invoke(this, new TwChartItemEventArgs(index, Items[index].Label));
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & ~Keys.Shift) is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var order = Order();
        if (order.Count == 0) return;
        int position = order.IndexOf(_hover);
        switch (e.KeyCode)
        {
            case Keys.Left: HoverIndex = order[Math.Max(0, position - 1)]; break;
            case Keys.Right: HoverIndex = order[Math.Min(order.Count - 1, position + 1)]; break;
            case Keys.Home: HoverIndex = order[0]; break;
            case Keys.End: HoverIndex = order[order.Count - 1]; break;
            case Keys.Enter when _hover >= 0: ItemClick?.Invoke(this, new TwChartItemEventArgs(_hover, Items[_hover].Label)); break;
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
    // Ekran okuyucu öğeleri zaman sırasındadır; dizin Order() üzerinden Items'e çevrilir.

    int ITwChartItems.ItemCount => Items.Count;

    int ITwChartItems.ActiveItem
    {
        get => Order().IndexOf(_hover);
        set
        {
            var order = Order();
            HoverIndex = value >= 0 && value < order.Count ? order[value] : -1;
        }
    }

    Rectangle ITwChartItems.ItemBounds(int index)
    {
        var (_, _, _, centers, radii) = ComputeLayout();
        int i = Order()[index];
        float r = radii[i] + 2 * DpiScale;
        return Rectangle.Round(new RectangleF(centers[i].X - r, centers[i].Y - r, r * 2, r * 2));
    }

    /// <summary>Text of an event for screen readers: label, date and value.</summary>
    public string GetItemText(int index)
    {
        var ev = Items[index];
        return (ev.Label.Length > 0 ? ev.Label + ", " : "") + DateText(ev.Time) + ": " + TwCharts.Format(ev.Value, _valueFormat);
    }

    string ITwChartItems.ItemText(int index) => GetItemText(Order()[index]);

    string ITwChartItems.Summary => Items.Count == 0 ? "" : DateText(Items.Min(e => e.Time)) + " – " + DateText(Items.Max(e => e.Time));

    protected override AccessibleObject CreateAccessibilityInstance() => new TwChartAccessibleObject(this, this);
}
