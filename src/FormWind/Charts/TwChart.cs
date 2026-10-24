using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Mark of a <see cref="TwChart"/> series.</summary>
public enum TwChartKind
{
    Line,
    Area,
    Bar,

    /// <summary>
    /// The whole chart as horizontal bars: one row per category with its full label on the left and the value axis along
    /// the bottom (a breakdown by line or by cause). Set on the chart, not on a series; series are side by side or
    /// <see cref="TwChart.Stacked"/>, <see cref="TwChart.ReferenceLines"/> are vertical, markers and the right axis are not drawn.
    /// </summary>
    HorizontalBar,
}

/// <summary>Which points of a line or area series get a marker.</summary>
public enum TwChartPoints
{
    /// <summary>The last point (and the highlighted category).</summary>
    Last,
    /// <summary>Every point.</summary>
    All,
    /// <summary>Only the highlighted category.</summary>
    None,
}

/// <summary>A named list of values in a <see cref="TwChart"/>, one per category. <see cref="double.NaN"/> = missing.</summary>
public sealed class TwChartSeries
{
    private string _name = "";
    private IReadOnlyList<double> _values = Array.Empty<double>();
    private Color _color = Color.Empty;
    private TwChartKind? _kind;
    private TwChartAxis _axis;
    private bool _dashed;
    private bool _showValues;
    private TwChartPoints _points;
    private IReadOnlyList<Color>? _pointColors;

    public TwChartSeries()
    {
    }

    public TwChartSeries(string name, IReadOnlyList<double> values)
    {
        _name = name ?? "";
        _values = values ?? Array.Empty<double>();
    }

    public string Name
    {
        get => _name;
        set => Set(ref _name, value ?? "");
    }

    public IReadOnlyList<double> Values
    {
        get => _values;
        set
        {
            _values = value ?? Array.Empty<double>();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Empty = the palette color of the series' position (colors follow the series, never their rank).</summary>
    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The series' own mark (e.g. a line over bars); <c>null</c> = the chart's <see cref="TwChart.Kind"/>.</summary>
    public TwChartKind? Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The axis the series is measured on; a secondary series adds a scale on the right.</summary>
    public TwChartAxis Axis
    {
        get => _axis;
        set
        {
            _axis = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Lines and areas are drawn dashed (a forecast, a scenario, a previous period).</summary>
    public bool Dashed
    {
        get => _dashed;
        set
        {
            _dashed = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Values are written on the chart: above each bar, or in a badge at a line's last point.</summary>
    public bool ShowValues
    {
        get => _showValues;
        set
        {
            _showValues = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Markers on a line or area: the last point (default), every point or none.</summary>
    public TwChartPoints Points
    {
        get => _points;
        set
        {
            _points = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>A color per category for bars (a Pareto chart's "vital few" and "trivial many"); empty entries and
    /// <c>null</c> use <see cref="Color"/>.</summary>
    public IReadOnlyList<Color>? PointColors
    {
        get => _pointColors;
        set
        {
            _pointColors = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    internal Color PointColor(int index) => _pointColors != null && index >= 0 && index < _pointColors.Count ? _pointColors[index] : Color.Empty;

    internal event EventHandler? Changed;

    internal double ValueAt(int index) => index >= 0 && index < _values.Count ? _values[index] : double.NaN;

    private void Set(ref string field, string value)
    {
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>The series of a <see cref="TwChart"/>; changes repaint the chart.</summary>
public sealed class TwChartSeriesCollection : Collection<TwChartSeries>
{
    private readonly Action _changed;

    internal TwChartSeriesCollection(Action changed) => _changed = changed;

    public TwChartSeries Add(string name, IReadOnlyList<double> values)
    {
        var series = new TwChartSeries(name, values);
        Add(series);
        return series;
    }

    protected override void InsertItem(int index, TwChartSeries item)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));
        base.InsertItem(index, item);
        item.Changed += OnChanged;
        _changed();
    }

    protected override void SetItem(int index, TwChartSeries item)
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

/// <summary>Event data of a chart item (a category of <see cref="TwChart"/>, a slice of <see cref="TwDonutChart"/>).</summary>
public sealed class TwChartItemEventArgs : EventArgs
{
    public TwChartItemEventArgs(int index, string label)
    {
        Index = index;
        Label = label;
    }

    public int Index { get; }

    public string Label { get; }
}

/// <summary>
/// Line, area and bar chart over categories (months, days, products) with one value axis. Series take the validated palette
/// in order (the first can be set with <c>accent-*</c>); gridlines use <c>divide-*</c>, axis and legend text
/// <c>label:text-*</c>, the background <c>bg-*</c>. Hovering or the arrow keys show a tooltip with every series' value at a
/// category; screen readers get one item per category. A legend is shown for two or more series. The <c>chart</c>
/// component gives the default look in light and dark mode. A series on <see cref="TwChartAxis.Secondary"/> gets its own
/// scale on the right (a Pareto chart: counts as bars, the cumulative share as a line); <see cref="ReferenceLines"/> mark
/// targets and <see cref="Markers"/> a category such as "Today".
/// <code>
/// chart.SetData(new[] { "Jan", "Feb", "Mar" },
///     new TwChartSeries("Revenue", new[] { 120.0, 150, 170 }),
///     new TwChartSeries("Costs", new[] { 90.0, 95, 110 }));
/// </code>
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ItemClick))]
[Description("Line, area and bar chart.")]
public partial class TwChart : Control, ITwChartControl, ITwChartItems
{
    private IReadOnlyList<string> _categories = Array.Empty<string>();
    private TwChartKind _kind;
    private bool _stacked;
    private bool _showLegend = true;
    private bool _showGrid = true;
    private string? _valueFormat;
    private string? _axisFormat;
    private string? _secondaryAxisFormat;
    private int _maxBarWidth = 24;
    private double _axisMinimum = double.NaN, _axisMaximum = double.NaN, _axisInterval = double.NaN;
    private double _secondaryAxisMinimum = double.NaN, _secondaryAxisMaximum = double.NaN, _secondaryAxisInterval = double.NaN;
    private HorizontalAlignment _legendAlignment = HorizontalAlignment.Left;
    private int _hover = -1;
    private Point? _mouse;
    private TwChartColors _colors = new TwChartColors();

    public TwChart()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Color.Transparent;
        Series = new TwChartSeriesCollection(OnDataChanged);
        ReferenceLines = new TwChartItemCollection<TwChartReferenceLine>(Invalidate);
        Markers = new TwChartItemCollection<TwChartMarker>(Invalidate);
    }

    protected override Size DefaultSize => new Size(420, 240);

    protected override Padding DefaultPadding => new Padding(8);

    /// <summary>Raised when a category is clicked or Enter is pressed on it.</summary>
    public event EventHandler<TwChartItemEventArgs>? ItemClick;

    /// <summary>Raised when the highlighted category changes (mouse or keyboard).</summary>
    public event EventHandler? HoverIndexChanged;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwChartSeriesCollection Series { get; }

    /// <summary>Dashed horizontal lines at values (targets, limits); the scale is widened to show them.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwChartItemCollection<TwChartReferenceLine> ReferenceLines { get; }

    /// <summary>Dashed vertical lines at categories, each with a label on top.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwChartItemCollection<TwChartMarker> Markers { get; }

    /// <summary>Category labels along the horizontal axis.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> Categories
    {
        get => _categories;
        set
        {
            _categories = value ?? Array.Empty<string>();
            OnDataChanged();
        }
    }

    [Category("Appearance"), DefaultValue(TwChartKind.Line)]
    public TwChartKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            OnDataChanged();
        }
    }

    /// <summary>Bar series are stacked into one bar per category instead of side by side.</summary>
    [Category("Appearance"), DefaultValue(false)]
    public bool Stacked
    {
        get => _stacked;
        set
        {
            _stacked = value;
            OnDataChanged();
        }
    }

    /// <summary>Legend above the plot (shown for two or more series).</summary>
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

    [Category("Appearance"), DefaultValue(true)]
    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            _showGrid = value;
            Invalidate();
        }
    }

    /// <summary>Format of values in the tooltip and for screen readers (default <c>#,0.##</c>; <c>C0</c>, <c>P0</c> …).</summary>
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

    /// <summary>Format of the axis ticks; <c>null</c> = compact numbers (1,2K / 1,2B).</summary>
    [Category("Appearance"), DefaultValue(null)]
    public string? AxisFormat
    {
        get => _axisFormat;
        set
        {
            _axisFormat = value;
            Invalidate();
        }
    }

    /// <summary>Widest a bar may be (96 DPI pixels); bars stay narrower when the categories are crowded. A Pareto chart
    /// with a few categories reads better with wide bars (e.g. 56).</summary>
    [Category("Appearance"), DefaultValue(24)]
    public int MaxBarWidth
    {
        get => _maxBarWidth;
        set
        {
            _maxBarWidth = Math.Max(2, value);
            Invalidate();
        }
    }

    /// <summary>Bottom of the left axis; <see cref="double.NaN"/> = from the data (bars and areas start at zero).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double AxisMinimum
    {
        get => _axisMinimum;
        set => SetAxis(ref _axisMinimum, value);
    }

    /// <summary>Top of the left axis; <see cref="double.NaN"/> = from the data.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double AxisMaximum
    {
        get => _axisMaximum;
        set => SetAxis(ref _axisMaximum, value);
    }

    /// <summary>Distance between the left axis' ticks and gridlines; <see cref="double.NaN"/> = a round step for the height.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double AxisInterval
    {
        get => _axisInterval;
        set => SetAxis(ref _axisInterval, value);
    }

    /// <summary>Bottom of the right axis; <see cref="double.NaN"/> = from the data.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double SecondaryAxisMinimum
    {
        get => _secondaryAxisMinimum;
        set => SetAxis(ref _secondaryAxisMinimum, value);
    }

    /// <summary>Top of the right axis; <see cref="double.NaN"/> = from the data.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double SecondaryAxisMaximum
    {
        get => _secondaryAxisMaximum;
        set => SetAxis(ref _secondaryAxisMaximum, value);
    }

    /// <summary>Distance between the right axis' ticks; <see cref="double.NaN"/> = a round step.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double SecondaryAxisInterval
    {
        get => _secondaryAxisInterval;
        set => SetAxis(ref _secondaryAxisInterval, value);
    }

    private void SetAxis(ref double field, double value)
    {
        field = value;
        Invalidate();
    }

    /// <summary>Where the legend sits above the plot.</summary>
    [Category("Appearance"), DefaultValue(HorizontalAlignment.Left)]
    public HorizontalAlignment LegendAlignment
    {
        get => _legendAlignment;
        set
        {
            _legendAlignment = value;
            Invalidate();
        }
    }

    /// <summary>Format of the right axis' ticks; <c>null</c> = compact numbers. <c>'%'0</c> writes %80.</summary>
    [Category("Appearance"), DefaultValue(null)]
    public string? SecondaryAxisFormat
    {
        get => _secondaryAxisFormat;
        set
        {
            _secondaryAxisFormat = value;
            Invalidate();
        }
    }

    /// <summary>The highlighted category (-1 = none).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int HoverIndex
    {
        get => _hover;
        set
        {
            value = value < 0 || value >= CategoryCount ? -1 : value;
            if (value == _hover) return;
            _hover = value;
            Invalidate();
            HoverIndexChanged?.Invoke(this, EventArgs.Empty);
            if (value >= 0 && Focused) AccessibilityNotifyClients(AccessibleEvents.Focus, value);
        }
    }

    /// <summary>Replaces the categories and series in one step.</summary>
    public void SetData(IEnumerable<string> categories, params TwChartSeries[] series)
    {
        _categories = (categories ?? Enumerable.Empty<string>()).ToList();
        Series.Clear();
        foreach (var item in series ?? Array.Empty<TwChartSeries>()) Series.Add(item);
        OnDataChanged();
    }

    /// <summary>Number of categories (the longest of the labels and the series).</summary>
    public int CategoryCount => Math.Max(_categories.Count, Series.Count == 0 ? 0 : Series.Max(s => s.Values.Count));

    internal string CategoryLabel(int index) => index < _categories.Count ? _categories[index] : (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>The color a series is drawn with.</summary>
    public Color GetSeriesColor(int index)
    {
        var series = Series[index];
        return !series.Color.IsEmpty ? series.Color : TwCharts.SeriesColor(index, _colors.Dark, _colors.Accent);
    }

    // Yatay çubuk grafikte her seri yatay çubuktur.
    private TwChartKind KindOf(TwChartSeries series) => IsHorizontal ? TwChartKind.HorizontalBar : series.Kind ?? _kind;

    private bool IsHorizontal => _kind == TwChartKind.HorizontalBar;

    private static bool IsBar(TwChartKind kind) => kind is TwChartKind.Bar or TwChartKind.HorizontalBar;

    private void OnDataChanged()
    {
        if (_hover >= CategoryCount) _hover = -1;
        TryPlayIntro();
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

    internal sealed class ChartLayout
    {
        public Rectangle Plot;
        public Rectangle Legend;
        public double Min, Max, Step;
        /// <summary>Scale of the right axis (only when <see cref="Secondary"/>).</summary>
        public double Min2, Max2, Step2;
        public bool Secondary;
        public float Band;
        public int LabelStep = 1;
        public int AxisWidth, AxisWidth2;

        public float Y(double value) => Map(value, Min, Max);

        public float Y(double value, TwChartAxis axis) => axis == TwChartAxis.Secondary && Secondary ? Map(value, Min2, Max2) : Map(value, Min, Max);

        private float Map(double value, double min, double max) => max - min <= 0 ? Plot.Bottom : (float)(Plot.Bottom - (value - min) / (max - min) * Plot.Height);

        public float X(int index) => Plot.Left + Band * (index + 0.5f);
    }

    /// <summary>Whether a right axis is drawn: some series are on each axis.</summary>
    private bool HasSecondaryAxis => !IsHorizontal && Series.Any(x => x.Axis == TwChartAxis.Secondary) && Series.Any(x => x.Axis == TwChartAxis.Primary);

    private TwChartAxis AxisOf(TwChartAxis axis) => axis == TwChartAxis.Secondary && HasSecondaryAxis ? TwChartAxis.Secondary : TwChartAxis.Primary;

    // Değer aralığı: yığılmış çubuklarda (sol eksen) kategori toplamları; çubuk ve alan sıfırı içerir; referans çizgileri görünür kalır.
    private (double Min, double Max, bool IncludeZero) ValueRange(TwChartAxis axis)
    {
        double min = double.MaxValue, max = double.MinValue;
        bool includeZero = false;
        bool stacked = _stacked && axis == TwChartAxis.Primary;
        foreach (var series in Series.Where(x => AxisOf(x.Axis) == axis))
        {
            var kind = KindOf(series);
            if (kind != TwChartKind.Line) includeZero = true;
            if (IsBar(kind) && stacked) continue;
            foreach (var v in series.Values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)))
            {
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
        }
        if (stacked)
        {
            var bars = Series.Where(x => IsBar(KindOf(x))).ToList();
            for (int i = 0; i < CategoryCount; i++)
            {
                double up = 0, down = 0;
                foreach (var series in bars)
                {
                    double v = series.ValueAt(i);
                    if (double.IsNaN(v)) continue;
                    if (v >= 0) up += v;
                    else down += v;
                }
                min = Math.Min(min, down);
                max = Math.Max(max, up);
            }
        }
        foreach (var line in ReferenceLines.Where(x => AxisOf(x.Axis) == axis && !double.IsNaN(x.Value) && !double.IsInfinity(x.Value)))
        {
            min = Math.Min(min, line.Value);
            max = Math.Max(max, line.Value);
        }
        if (min > max) (min, max) = (0, 1);
        return (min, max, includeZero);
    }

    internal ChartLayout ComputeLayout()
    {
        var layout = new ChartLayout();
        float s = DpiScale;
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        int line = TextRenderer.MeasureText("Ag", Font, Size.Empty, flags & ~TextFormatFlags.EndEllipsis).Height;
        var content = Rectangle.FromLTRB(Padding.Left, Padding.Top, Math.Max(Padding.Left, Width - Padding.Right), Math.Max(Padding.Top, Height - Padding.Bottom));
        int top = content.Top;
        if (LegendVisible)
        {
            layout.Legend = new Rectangle(content.Left, top, content.Width, line);
            top += line + (int)(12 * s);
        }
        // Üstte işaret etiketleri ve çubuk değerleri için bir satırlık yer.
        if (Markers.Any(x => x.Label.Length > 0) || Series.Any(x => x.ShowValues && KindOf(x) == TwChartKind.Bar))
            top += line + (int)(4 * s);
        int count = CategoryCount;
        // Kategori etiketleri "\n" ile birden çok satıra bölünebilir.
        int labelLines = Enumerable.Range(0, count).Select(i => CategoryLabel(i).Split('\n').Length).DefaultIfEmpty(1).Max();
        int xAxis = line * labelLines + (int)(8 * s);
        int plotHeight = Math.Max(1, content.Bottom - xAxis - top);

        int ticks = Math.Max(2, Math.Min(8, plotHeight / (int)Math.Max(1, 44 * s) + 1));
        var (min, max, includeZero) = ValueRange(TwChartAxis.Primary);
        (layout.Min, layout.Max, layout.Step) = Override(TwCharts.NiceScale(min, max, ticks, includeZero), _axisMinimum, _axisMaximum, _axisInterval);
        var measure = flags & ~TextFormatFlags.EndEllipsis;
        layout.AxisWidth = Ticks(layout.Min, layout.Max, layout.Step).Select(t => TextRenderer.MeasureText(TickText(t), Font, Size.Empty, measure).Width).DefaultIfEmpty(0).Max();
        if (HasSecondaryAxis)
        {
            layout.Secondary = true;
            var (min2, max2, zero2) = ValueRange(TwChartAxis.Secondary);
            (layout.Min2, layout.Max2, layout.Step2) = Override(TwCharts.NiceScale(min2, max2, ticks, zero2), _secondaryAxisMinimum, _secondaryAxisMaximum, _secondaryAxisInterval);
            layout.AxisWidth2 = Ticks(layout.Min2, layout.Max2, layout.Step2).Select(t => TextRenderer.MeasureText(SecondaryTickText(t), Font, Size.Empty, measure).Width).DefaultIfEmpty(0).Max();
        }
        int left = content.Left + layout.AxisWidth + (int)(10 * s);
        int right = layout.Secondary ? content.Right - layout.AxisWidth2 - (int)(10 * s) : content.Right;
        layout.Plot = new Rectangle(left, top, Math.Max(1, right - left), plotHeight);
        layout.Band = count == 0 ? layout.Plot.Width : layout.Plot.Width / (float)count;

        // Sığmayan kategori etiketleri atlanır (çakışmasınlar diye).
        int widest = 0;
        for (int i = 0; i < count; i++)
            foreach (var part in CategoryLabel(i).Split('\n'))
                widest = Math.Max(widest, TextRenderer.MeasureText(part, Font, Size.Empty, flags & ~TextFormatFlags.EndEllipsis).Width);
        layout.LabelStep = Math.Max(1, (int)Math.Ceiling((widest + 8 * s) / Math.Max(1f, layout.Band)));
        return layout;
    }

    private bool LegendVisible => _showLegend && Series.Count >= 2;

    /// <summary>The automatic scale with the set bounds and interval applied; a set bound keeps the automatic step when
    /// it divides the range, otherwise the range is split into about as many steps.</summary>
    private static (double Min, double Max, double Step) Override((double Min, double Max, double Step) nice, double minimum, double maximum, double interval)
    {
        double min = double.IsNaN(minimum) || double.IsInfinity(minimum) ? nice.Min : minimum;
        double max = double.IsNaN(maximum) || double.IsInfinity(maximum) ? nice.Max : maximum;
        if (max - min < 1e-12) max = min + 1;
        if (interval > 0 && !double.IsInfinity(interval)) return (min, max, interval);
        double steps = Math.Max(1, Math.Round((max - min) / nice.Step));
        return (min, max, (max - min) / steps);
    }

    private static IEnumerable<double> Ticks(double min, double max, double step)
    {
        // Kayan nokta birikmesini önlemek için adım sayısıyla üretilir.
        int steps = (int)Math.Round((max - min) / step);
        for (int i = 0; i <= steps; i++) yield return min + i * step;
    }

    private string TickText(double value) => _axisFormat is null ? TwCharts.Compact(value) : TwCharts.Format(value, _axisFormat);

    private string SecondaryTickText(double value) => _secondaryAxisFormat is null ? TwCharts.Compact(value) : TwCharts.Format(value, _secondaryAxisFormat);

    // ---- Çizim ----

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        TwPainter.PaintParentBackground(this, g);
        if (BackColor.A > 0) using (var brush = new SolidBrush(BackColor)) g.FillRectangle(brush, ClientRectangle);
        if (IsHorizontal)
        {
            // Giriş animasyonunda çubuklar soldan büyür (etiketler de soldan açılır).
            ClipIntro(g, ClientRectangle);
            PaintHorizontal(g);
            return;
        }
        float s = DpiScale;
        var layout = ComputeLayout();
        var plot = layout.Plot;
        var surface = TwCharts.Surface(this);
        var muted = TwCharts.Muted(this, _colors);
        var grid = TwCharts.Grid(this, _colors);
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        int count = CategoryCount;

        // Izgara ve eksen etiketleri: ince, düz, geri planda.
        using (var pen = new Pen(grid, 1))
        {
            foreach (var tick in Ticks(layout.Min, layout.Max, layout.Step))
            {
                int y = (int)Math.Round(layout.Y(tick));
                if (_showGrid || Math.Abs(tick) < layout.Step / 1000) g.DrawLine(pen, plot.Left, y, plot.Right, y);
                var text = TickText(tick);
                int h = TextRenderer.MeasureText(text, Font, Size.Empty, flags & ~TextFormatFlags.EndEllipsis).Height;
                TextRenderer.DrawText(g, text, Font, new Rectangle(plot.Left - (int)(10 * s) - layout.AxisWidth, y - h / 2, layout.AxisWidth, h), muted, flags | TextFormatFlags.Right);
            }
        }
        // Sağ eksen: yalnızca etiketler (ızgara sol eksene aittir).
        if (layout.Secondary)
        {
            foreach (var tick in Ticks(layout.Min2, layout.Max2, layout.Step2))
            {
                int y = (int)Math.Round(layout.Y(tick, TwChartAxis.Secondary));
                var text = SecondaryTickText(tick);
                int h = TextRenderer.MeasureText(text, Font, Size.Empty, flags & ~TextFormatFlags.EndEllipsis).Height;
                TextRenderer.DrawText(g, text, Font, new Rectangle(plot.Right + (int)(10 * s), y - h / 2, layout.AxisWidth2, h), muted, flags);
            }
        }
        int lineHeight = TextRenderer.MeasureText("Ag", Font, Size.Empty, flags).Height;
        for (int i = 0; i < count; i += layout.LabelStep)
        {
            int x = (int)layout.X(i), y = plot.Bottom + (int)(6 * s);
            foreach (var text in CategoryLabel(i).Split('\n'))
            {
                var size = TextRenderer.MeasureText(text, Font, Size.Empty, flags & ~TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, text, Font, new Rectangle(x - size.Width / 2, y, size.Width, size.Height), muted, flags);
                y += lineHeight;
            }
        }

        // Vurgulanan kategori: çubuklarda hafif bir şerit, çizgilerde dikey kılavuz.
        bool anyBars = Series.Any(x => KindOf(x) == TwChartKind.Bar);
        if (_hover >= 0)
        {
            if (anyBars)
            {
                using var band = new SolidBrush(Color.FromArgb(_colors.Dark ? 28 : 18, ForeColor));
                g.FillRectangle(band, plot.Left + layout.Band * _hover, plot.Top, layout.Band, plot.Height);
            }
            else
            {
                using var guide = new Pen(TwCharts.Blend(grid, ForeColor, 0.25f), 1);
                float x = layout.X(_hover);
                g.DrawLine(guide, x, plot.Top, x, plot.Bottom);
            }
        }

        var state = g.Save();
        g.SetClip(Rectangle.Inflate(plot, (int)(6 * s), (int)(6 * s)));
        ClipIntro(g, Rectangle.Inflate(plot, (int)(6 * s), (int)(6 * s)));
        PaintBars(g, layout, surface, s);
        PaintGuideLines(g, layout, muted, s);
        for (int i = 0; i < Series.Count; i++)
            if (KindOf(Series[i]) == TwChartKind.Area) PaintLine(g, layout, i, area: true, s);
        for (int i = 0; i < Series.Count; i++)
            if (KindOf(Series[i]) != TwChartKind.Bar) PaintLine(g, layout, i, area: false, s);
        PaintMarkers(g, layout, surface, s);
        g.Restore(state);
        PaintGuideLabels(g, layout, muted, surface, s);
        // Değer etiketleri çubuklarıyla birlikte, açılış bitince görünür.
        if (IntroProgress >= 1f) PaintValueLabels(g, layout, muted, s);

        if (LegendVisible) PaintLegend(g, layout.Legend, muted, s);
        if (_hover >= 0 && Series.Count > 0)
        {
            var anchor = new Point((int)layout.X(_hover), _mouse?.Y ?? plot.Top + plot.Height / 2);
            var lines = Series.Select((x, i) => ((Color?)GetSeriesColor(i), (Series.Count > 1 ? x.Name + ": " : "") + TwCharts.Format(x.ValueAt(_hover), _valueFormat))).ToList();
            TwCharts.DrawTooltip(g, this, anchor, CategoryLabel(_hover), lines, s);
        }
        if (Focused && ShowFocusCues)
            TwPainter.StrokeRounded(g, ClientRectangle, 6 * s, 2 * s, Color.FromArgb(160, GetSeriesColorOrAccent()));
    }

    private Color GetSeriesColorOrAccent() => _colors.Accent ?? TwCharts.SeriesColor(0, _colors.Dark, null);

    /// <summary>Bounds of a bar (or a stacked segment) of a series at a category; empty when it has no bar.</summary>
    internal RectangleF BarBounds(ChartLayout layout, int seriesIndex, int category)
    {
        var bars = Enumerable.Range(0, Series.Count).Where(i => KindOf(Series[i]) == TwChartKind.Bar).ToList();
        int position = bars.IndexOf(seriesIndex);
        double value = Series[seriesIndex].ValueAt(category);
        if (position < 0 || double.IsNaN(value)) return RectangleF.Empty;
        float s = DpiScale, gap = 2 * s;
        float zero = layout.Y(Math.Max(layout.Min, Math.Min(layout.Max, 0)));
        float center = layout.X(category);
        if (_stacked)
        {
            // Segmentler arasında yüzey renginde 2 px boşluk: her segment altındakinden gap kadar ayrılır.
            float thickness = Math.Min(_maxBarWidth * s, layout.Band * 0.6f);
            double baseValue = 0;
            for (int p = 0; p < position; p++)
            {
                double v = Series[bars[p]].ValueAt(category);
                if (!double.IsNaN(v) && (v >= 0) == (value >= 0)) baseValue += v;
            }
            float from = layout.Y(baseValue), to = layout.Y(baseValue + value);
            bool first = Math.Abs(baseValue) < 1e-12;
            float top = Math.Min(from, to), bottom = Math.Max(from, to);
            if (!first)
            {
                if (value >= 0) bottom -= gap;
                else top += gap;
            }
            return new RectangleF(center - thickness / 2, top, thickness, Math.Max(0, bottom - top));
        }
        var axis = AxisOf(Series[seriesIndex].Axis);
        zero = layout.Y(axis == TwChartAxis.Secondary ? Math.Max(layout.Min2, Math.Min(layout.Max2, 0)) : Math.Max(layout.Min, Math.Min(layout.Max, 0)), axis);
        int m = bars.Count;
        float width = Math.Max(2 * s, Math.Min(_maxBarWidth * s, (layout.Band * 0.72f - (m - 1) * gap) / m));
        float group = m * width + (m - 1) * gap;
        float x = center - group / 2 + position * (width + gap);
        float y = layout.Y(value, axis);
        return new RectangleF(x, Math.Min(y, zero), width, Math.Max(1, Math.Abs(zero - y)));
    }

    private void PaintBars(Graphics g, ChartLayout layout, Color surface, float s)
    {
        int count = CategoryCount;
        var bars = Enumerable.Range(0, Series.Count).Where(i => KindOf(Series[i]) == TwChartKind.Bar).ToList();
        for (int c = 0; c < count; c++)
        {
            foreach (int i in bars)
            {
                var bar = BarBounds(layout, i, c);
                if (bar.IsEmpty) continue;
                double value = Series[i].ValueAt(c);
                // Yığında yalnızca dış uç yuvarlanır; iç segmentler köşesizdir.
                bool outer = !_stacked || bars.SkipWhile(b => b != i).Skip(1).All(b =>
                {
                    double v = Series[b].ValueAt(c);
                    return double.IsNaN(v) || v == 0 || (v >= 0) != (value >= 0);
                });
                var point = Series[i].PointColor(c);
                var color = point.IsEmpty ? GetSeriesColor(i) : point;
                if (!Enabled) color = TwCharts.Blend(color, surface, 0.5f);
                TwCharts.FillBar(g, bar, outer ? 4 * s : 0, value >= 0, color);
            }
        }
    }

    private void PaintLine(Graphics g, ChartLayout layout, int index, bool area, float s)
    {
        var series = Series[index];
        var color = GetSeriesColor(index);
        if (!Enabled) color = TwCharts.Blend(color, TwCharts.Surface(this), 0.5f);
        var segments = new List<List<PointF>>();
        List<PointF>? current = null;
        for (int i = 0; i < CategoryCount; i++)
        {
            double v = series.ValueAt(i);
            if (double.IsNaN(v) || double.IsInfinity(v))
            {
                current = null;
                continue;
            }
            if (current is null) segments.Add(current = new List<PointF>());
            current.Add(new PointF(layout.X(i), YOf(layout, series, v)));
        }
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var axis = AxisOf(series.Axis);
        float zero = axis == TwChartAxis.Secondary
            ? layout.Y(Math.Max(layout.Min2, Math.Min(layout.Max2, 0)), axis)
            : layout.Y(Math.Max(layout.Min, Math.Min(layout.Max, 0)));
        foreach (var points in segments)
        {
            if (area)
            {
                if (points.Count < 2) continue;
                var fill = points.Concat(new[] { new PointF(points[points.Count - 1].X, zero), new PointF(points[0].X, zero) }).ToArray();
                using var brush = new SolidBrush(Color.FromArgb(series.Dashed ? 13 : 26, color));
                g.FillPolygon(brush, fill);
            }
            else if (points.Count > 1)
            {
                using var pen = new Pen(color, 2 * s) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
                if (series.Dashed)
                {
                    // Kesik uçlar düz kalır; yuvarlak uçlar kısa çizgileri noktaya çevirir.
                    pen.StartCap = pen.EndCap = LineCap.Flat;
                    pen.DashPattern = new[] { 3f, 2f };
                }
                g.DrawLines(pen, points.ToArray());
            }
        }
        g.Restore(state);
    }

    // Seçici işaretler: her çizginin son noktası ve vurgulanan kategorideki noktalar.
    private void PaintMarkers(Graphics g, ChartLayout layout, Color surface, float s)
    {
        for (int i = 0; i < Series.Count; i++)
        {
            if (KindOf(Series[i]) == TwChartKind.Bar) continue;
            var color = GetSeriesColor(i);
            int last = -1;
            for (int c = CategoryCount - 1; c >= 0; c--)
            {
                if (double.IsNaN(Series[i].ValueAt(c))) continue;
                last = c;
                break;
            }
            var points = Series[i].Points;
            if (points == TwChartPoints.All)
            {
                for (int c = 0; c < CategoryCount; c++)
                    if (c != _hover && !double.IsNaN(Series[i].ValueAt(c)))
                        TwCharts.DrawMarker(g, new PointF(layout.X(c), YOf(layout, Series[i], Series[i].ValueAt(c))), s * 0.75f, color, surface);
            }
            else if (points == TwChartPoints.Last && last >= 0)
                TwCharts.DrawMarker(g, new PointF(layout.X(last), YOf(layout, Series[i], Series[i].ValueAt(last))), s, color, surface);
            if (_hover >= 0 && (_hover != last || points != TwChartPoints.Last) && !double.IsNaN(Series[i].ValueAt(_hover)))
                TwCharts.DrawMarker(g, new PointF(layout.X(_hover), YOf(layout, Series[i], Series[i].ValueAt(_hover))), s, color, surface);
        }
    }

    private float YOf(ChartLayout layout, TwChartSeries series, double value) => layout.Y(value, AxisOf(series.Axis));

    private static Pen GuidePen(Color color, float s) => new Pen(color, Math.Max(1f, s)) { DashPattern = new[] { 4f, 3f } };

    // Referans çizgileri ve işaretler: ince, kesikli; serilerin altında kalır.
    private void PaintGuideLines(Graphics g, ChartLayout layout, Color muted, float s)
    {
        var plot = layout.Plot;
        foreach (var line in ReferenceLines)
        {
            if (double.IsNaN(line.Value) || double.IsInfinity(line.Value)) continue;
            float y = layout.Y(line.Value, AxisOf(line.Axis));
            using var pen = GuidePen(line.Color.IsEmpty ? muted : line.Color, s);
            g.DrawLine(pen, plot.Left, y, plot.Right, y);
        }
        foreach (var marker in Markers)
        {
            if (!marker.ShowLine || marker.Index < 0 || marker.Index >= CategoryCount) continue;
            float x = layout.X(marker.Index);
            using var pen = GuidePen(marker.Color.IsEmpty ? muted : marker.Color, s);
            g.DrawLine(pen, x, plot.Top, x, plot.Bottom);
        }
    }

    private void PaintGuideLabels(Graphics g, ChartLayout layout, Color muted, Color surface, float s)
    {
        var plot = layout.Plot;
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        int pad = (int)(2 * s);
        // Referans etiketi çizginin sağ ucunun üstünde, yüzey renginde bir zeminle.
        foreach (var line in ReferenceLines)
        {
            if (line.Label.Length == 0 || double.IsNaN(line.Value) || double.IsInfinity(line.Value)) continue;
            int y = (int)layout.Y(line.Value, AxisOf(line.Axis));
            var size = TextRenderer.MeasureText(g, line.Label, Font, Size.Empty, flags);
            var box = new Rectangle(plot.Right - size.Width - pad * 2, y - size.Height - pad, size.Width + pad * 2, size.Height);
            using (var back = new SolidBrush(Color.FromArgb(220, surface))) g.FillRectangle(back, box);
            TextRenderer.DrawText(g, line.Label, Font, new Point(box.X + pad, box.Y), line.Color.IsEmpty ? muted : line.Color, flags);
        }
        // İşaret etiketi çizginin üstünde bir hap.
        foreach (var marker in Markers)
        {
            if (marker.Label.Length == 0 || marker.Index < 0 || marker.Index >= CategoryCount) continue;
            var size = TextRenderer.MeasureText(g, marker.Label, Font, Size.Empty, flags);
            int px = (int)(6 * s), py = (int)(1 * s);
            var box = new Rectangle((int)layout.X(marker.Index) - size.Width / 2 - px, plot.Top - size.Height - py * 2 - (int)(4 * s), size.Width + px * 2, size.Height + py * 2);
            box.X = Math.Max(0, Math.Min(Width - box.Width, box.X));
            var color = marker.Color.IsEmpty ? muted : marker.Color;
            TwPainter.FillRounded(g, box, box.Height / 2f, TwCharts.Blend(surface, color, 0.18f));
            TextRenderer.DrawText(g, marker.Label, Font, new Point(box.X + px, box.Y + py), marker.Color.IsEmpty ? ForeColor : color, flags);
        }
    }

    // Değer etiketleri: çubukların üstünde; çizgilerde son noktanın yanında seri renginde bir rozet.
    private void PaintValueLabels(Graphics g, ChartLayout layout, Color muted, float s)
    {
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        for (int i = 0; i < Series.Count; i++)
        {
            var series = Series[i];
            if (!series.ShowValues) continue;
            if (KindOf(series) == TwChartKind.Bar)
            {
                for (int c = 0; c < CategoryCount; c++)
                {
                    var bar = BarBounds(layout, i, c);
                    if (bar.IsEmpty) continue;
                    double value = series.ValueAt(c);
                    var text = TwCharts.Format(value, _valueFormat);
                    var size = TextRenderer.MeasureText(g, text, Font, Size.Empty, flags);
                    int y = value >= 0 ? (int)bar.Top - size.Height - (int)(3 * s) : (int)bar.Bottom + (int)(3 * s);
                    TextRenderer.DrawText(g, text, Font, new Point((int)(bar.Left + bar.Width / 2 - size.Width / 2f), y), ForeColor, flags);
                }
                continue;
            }
            int last = -1;
            for (int c = CategoryCount - 1; c >= 0 && last < 0; c--)
                if (!double.IsNaN(series.ValueAt(c))) last = c;
            if (last < 0) continue;
            var label = TwCharts.Format(series.ValueAt(last), _valueFormat);
            var labelSize = TextRenderer.MeasureText(g, label, Font, Size.Empty, flags);
            int padX = (int)(6 * s), padY = (int)(2 * s);
            var point = new PointF(layout.X(last), YOf(layout, series, series.ValueAt(last)));
            var badge = new Rectangle((int)point.X - labelSize.Width - padX * 2 - (int)(8 * s), (int)point.Y - labelSize.Height / 2 - padY,
                labelSize.Width + padX * 2, labelSize.Height + padY * 2);
            badge.X = Math.Max(0, badge.X);
            badge.Y = Math.Max(0, Math.Min(Height - badge.Height, badge.Y));
            // Rozet seri renginin hafif bir tonu, yazı seri renginde (çizginin üstünde göze batmadan okunur).
            var color = GetSeriesColor(i);
            TwPainter.FillRounded(g, badge, 4 * s, TwCharts.Blend(TwCharts.Surface(this), color, 0.22f));
            TextRenderer.DrawText(g, label, Font, new Point(badge.X + padX, badge.Y + padY), color, flags);
        }
    }

    private void PaintLegend(Graphics g, Rectangle area, Color muted, float s)
    {
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
        int x = area.Left, key = (int)(10 * s), gap = (int)(6 * s);
        if (_legendAlignment != HorizontalAlignment.Left)
        {
            int total = 0;
            for (int i = 0; i < Series.Count; i++)
                total += key + gap + (KindOf(Series[i]) == TwChartKind.Line ? (int)(2 * s) : 0)
                         + TextRenderer.MeasureText(Series[i].Name, Font, Size.Empty, flags & ~TextFormatFlags.EndEllipsis).Width + (i < Series.Count - 1 ? (int)(16 * s) : 0);
            int free = Math.Max(0, area.Width - total);
            x += _legendAlignment == HorizontalAlignment.Right ? free : free / 2;
        }
        for (int i = 0; i < Series.Count && x < area.Right; i++)
        {
            var color = GetSeriesColor(i);
            // Anahtar işaretin biçimini taşır: çizgi için kısa bir çizgi, çubuk ve alan için kare.
            if (KindOf(Series[i]) == TwChartKind.Line)
            {
                using var pen = new Pen(color, 2 * s) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                if (Series[i].Dashed)
                {
                    pen.StartCap = pen.EndCap = LineCap.Flat;
                    pen.DashPattern = new[] { 2f, 1.5f };
                }
                var state = g.Save();
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawLine(pen, x, area.Top + area.Height / 2f, x + key + 2 * s, area.Top + area.Height / 2f);
                g.Restore(state);
                x += key + (int)(2 * s) + gap;
            }
            else
            {
                TwPainter.FillRounded(g, new Rectangle(x, area.Top + (area.Height - key) / 2, key, key), 2 * s, color);
                x += key + gap;
            }
            var name = Series[i].Name;
            int width = Math.Min(TextRenderer.MeasureText(name, Font, Size.Empty, flags & ~TextFormatFlags.EndEllipsis).Width, Math.Max(0, area.Right - x));
            TextRenderer.DrawText(g, name, Font, new Rectangle(x, area.Top, width, area.Height), ForeColor, flags);
            x += width + (int)(16 * s);
        }
    }

    // ---- Etkileşim ----

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _mouse = e.Location;
        int index = ItemAt(e.Location);
        if (index == _hover) Invalidate();
        HoverIndex = index;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _mouse = null;
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
        if (e.Button == MouseButtons.Left && index >= 0) ItemClick?.Invoke(this, new TwChartItemEventArgs(index, CategoryLabel(index)));
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & ~Keys.Shift) is Keys.Left or Keys.Right or Keys.Home or Keys.End
        || IsHorizontal && (keyData & ~Keys.Shift) is Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int count = CategoryCount;
        if (count == 0) return;
        _mouse = null;
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Up: HoverIndex = _hover <= 0 ? 0 : _hover - 1; break;
            case Keys.Right or Keys.Down: HoverIndex = _hover < 0 ? 0 : Math.Min(count - 1, _hover + 1); break;
            case Keys.Home: HoverIndex = 0; break;
            case Keys.End: HoverIndex = count - 1; break;
            case Keys.Enter when _hover >= 0: ItemClick?.Invoke(this, new TwChartItemEventArgs(_hover, CategoryLabel(_hover))); break;
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
        if (_mouse is null) HoverIndex = -1;
        Invalidate();
    }

    // ---- Erişilebilirlik ----

    int ITwChartItems.ItemCount => CategoryCount;

    int ITwChartItems.ActiveItem
    {
        get => _hover;
        set => HoverIndex = value;
    }

    /// <summary>The category under a client point (-1 = none).</summary>
    public int ItemAt(Point client)
    {
        if (IsHorizontal) return HorizontalItemAt(client);
        var plot = ComputeLayout();
        int count = CategoryCount;
        if (count == 0 || client.X < plot.Plot.Left || client.X >= plot.Plot.Right || client.Y < Padding.Top || client.Y > Height - Padding.Bottom) return -1;
        return Math.Min(count - 1, (int)((client.X - plot.Plot.Left) / plot.Band));
    }

    Rectangle ITwChartItems.ItemBounds(int index)
    {
        if (IsHorizontal) return HorizontalRowBounds(index);
        var layout = ComputeLayout();
        return Rectangle.Round(new RectangleF(layout.Plot.Left + layout.Band * index, layout.Plot.Top, layout.Band, layout.Plot.Height));
    }

    /// <summary>Text of a category for screen readers: the label and every series' value.</summary>
    public string GetItemText(int index) =>
        CategoryLabel(index) + ": " + string.Join(", ", Series.Select(x => (Series.Count > 1 ? x.Name + " " : "") + TwCharts.Format(x.ValueAt(index), _valueFormat)));

    string ITwChartItems.ItemText(int index) => GetItemText(index);

    string ITwChartItems.Summary => string.Join("; ", Series.Select(x =>
    {
        var values = x.Values.Where(v => !double.IsNaN(v)).ToList();
        return values.Count == 0 ? x.Name : x.Name + " " + TwCharts.Format(values.Min(), _valueFormat) + " – " + TwCharts.Format(values.Max(), _valueFormat);
    }));

    protected override AccessibleObject CreateAccessibilityInstance() => new TwChartAccessibleObject(this, this);
}
