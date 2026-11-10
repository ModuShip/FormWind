using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Event data of a <see cref="TwHeatmap"/> cell.</summary>
public sealed class TwHeatmapCellEventArgs : EventArgs
{
    public TwHeatmapCellEventArgs(int row, int column, double value)
    {
        Row = row;
        Column = column;
        Value = value;
    }

    public int Row { get; }

    public int Column { get; }

    /// <summary>The cell's value (<see cref="double.NaN"/> = missing).</summary>
    public double Value { get; }
}

/// <summary>
/// Event data of <see cref="TwHeatmap.CellText"/>: the texts a cell shows in the tooltip and gives to screen readers.
/// </summary>
public sealed class TwHeatmapCellTextEventArgs : EventArgs
{
    public TwHeatmapCellTextEventArgs(int row, int column, double value, string valueText)
    {
        Row = row;
        Column = column;
        Value = value;
        ValueText = valueText ?? "";
    }

    public int Row { get; }

    public int Column { get; }

    /// <summary>The cell's value (<see cref="double.NaN"/> = missing).</summary>
    public double Value { get; }

    /// <summary>
    /// The cell's name, the tooltip's title ("17 Sep"); <c>null</c> = the row and column labels ("Thu · 14 Sep").
    /// </summary>
    public string? Title { get; set; }

    /// <summary>The formatted value ("4 events"); starts as the value in <see cref="TwHeatmap.ToolTipValueFormat"/>.</summary>
    public string ValueText { get; set; }
}

/// <summary>How a <see cref="TwHeatmap"/> draws cells without a value (<see cref="double.NaN"/>).</summary>
public enum TwHeatmapEmptyCell
{
    /// <summary>In the lowest shade, like the scale's low end.</summary>
    Low,

    /// <summary>A flat gray (<see cref="TwHeatmap.EmptyCellColor"/>) that is not part of the scale.</summary>
    Solid,

    /// <summary>Diagonal lines on the gray ("no production").</summary>
    Hatched,

    /// <summary>Nothing; the surface shows through.</summary>
    None,
}

/// <summary>
/// A grid of colored cells: rows (lines, machines, shifts) against columns (days, hours, factors), each shaded by its value
/// from <see cref="LowColor"/> to <see cref="HighColor"/> (default: one step off the surface to <c>accent-*</c>). When the
/// values cross zero (a correlation matrix) the scale diverges: negative values shade toward <see cref="NegativeColor"/>.
/// Missing values (<see cref="double.NaN"/>) are drawn as <see cref="EmptyCells"/> says (the lowest shade by default; flat or
/// hatched gray for "no production") and named <see cref="EmptyCellText"/> in the tooltip, the legend and for screen
/// readers. Row labels sit on the left and column labels on top, with
/// a gradient legend; hovering or the arrow keys show a cell's value, and screen readers get one item per cell.
/// <code>
/// heatmap.SetData(new[] { "Line 1", "Line 2" }, new[] { "Mon", "Tue", "Wed" },
///     new double[,] { { 12, 40, 8 }, { 0, 95, 30 } });
/// </code>
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(CellClick))]
[Description("Grid of cells shaded by value.")]
public class TwHeatmap : Control, ITwChartControl, ITwChartItems
{
    private IReadOnlyList<string> _rows = Array.Empty<string>();
    private IReadOnlyList<string> _columns = Array.Empty<string>();
    private double[,] _values = new double[0, 0];
    private double _minimum = double.NaN;
    private double _maximum = double.NaN;
    private Color _lowColor = Color.Empty;
    private Color _highColor = Color.Empty;
    private Color _negativeColor = Color.Empty;
    private bool _showValues;
    private bool _showLegend = true;
    private string? _valueFormat;
    private string? _toolTipValueFormat;
    private string _legendMinText = "";
    private string _legendMaxText = "";
    private TwHeatmapEmptyCell _emptyCells;
    private Color _emptyCellColor = Color.Empty;
    private string _emptyCellText = "";
    private int _hover = -1;
    private int _selected = -1;
    private IReadOnlyList<int> _highlightColumns = Array.Empty<int>();
    private Color _highlightTextColor = Color.Empty;
    private Color _selectionColor = Color.Empty;
    private Size _cellSize = Size.Empty;
    private Point? _mouse;
    private TwChartColors _colors = new TwChartColors();
    private TwChartMotion? _motion;
    private Dictionary<(int Row, int Column), Color> _shownColors = new Dictionary<(int, int), Color>();
    private Dictionary<(int Row, int Column), Color> _fromColors = new Dictionary<(int, int), Color>();

    public TwHeatmap()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Color.Transparent;
    }

    protected override Size DefaultSize => new Size(480, 200);

    protected override Padding DefaultPadding => new Padding(8);

    /// <summary>Raised when a cell is clicked or Enter is pressed on it.</summary>
    public event EventHandler<TwHeatmapCellEventArgs>? CellClick;

    /// <summary>Raised when the highlighted cell changes (mouse or keyboard).</summary>
    public event EventHandler? HoverIndexChanged;

    /// <summary>
    /// Raised when a cell's tooltip or screen reader text is built; change <see cref="TwHeatmapCellTextEventArgs.Title"/> or
    /// <see cref="TwHeatmapCellTextEventArgs.ValueText"/> when the labels alone don't say what the cell is (a week column
    /// whose Thursday is 17 Sep).
    /// </summary>
    public event EventHandler<TwHeatmapCellTextEventArgs>? CellText;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> RowLabels
    {
        get => _rows;
        set
        {
            _rows = value ?? Array.Empty<string>();
            OnDataChanged();
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<string> ColumnLabels
    {
        get => _columns;
        set
        {
            _columns = value ?? Array.Empty<string>();
            OnDataChanged();
        }
    }

    /// <summary>The cell values, [row, column].</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double[,] Values
    {
        get => _values;
        set
        {
            _values = value ?? new double[0, 0];
            OnDataChanged();
        }
    }

    /// <summary>Value of the lightest cell; <see cref="double.NaN"/> = the data's lowest (zero when every value is positive).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            Invalidate();
        }
    }

    /// <summary>Value of the strongest cell; <see cref="double.NaN"/> = the data's highest. Larger values are drawn at full strength.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            Invalidate();
        }
    }

    /// <summary>Color of the lowest (or zero) value; empty = one step off the surface.</summary>
    [Category("Appearance")]
    public Color LowColor
    {
        get => _lowColor;
        set
        {
            _lowColor = value;
            Invalidate();
        }
    }

    private bool ShouldSerializeLowColor() => !_lowColor.IsEmpty;

    private void ResetLowColor() => LowColor = Color.Empty;

    /// <summary>Color of the highest value; empty = <c>accent-*</c> or the first palette color.</summary>
    [Category("Appearance")]
    public Color HighColor
    {
        get => _highColor;
        set
        {
            _highColor = value;
            Invalidate();
        }
    }

    private bool ShouldSerializeHighColor() => !_highColor.IsEmpty;

    private void ResetHighColor() => HighColor = Color.Empty;

    /// <summary>Color of the most negative value on a diverging scale; empty = a palette color unlike <see cref="HighColor"/>.</summary>
    [Category("Appearance")]
    public Color NegativeColor
    {
        get => _negativeColor;
        set
        {
            _negativeColor = value;
            Invalidate();
        }
    }

    private bool ShouldSerializeNegativeColor() => !_negativeColor.IsEmpty;

    private void ResetNegativeColor() => NegativeColor = Color.Empty;

    /// <summary>Each cell shows its value (in a legible color on its shade).</summary>
    [Category("Appearance"), DefaultValue(false)]
    public bool ShowValues
    {
        get => _showValues;
        set
        {
            _showValues = value;
            Invalidate();
        }
    }

    /// <summary>Gradient legend in the top right with the scale's ends.</summary>
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

    /// <summary>Format of the values in cells, the tooltip and for screen readers (default <c>#,0.##</c>).</summary>
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

    /// <summary>
    /// Format of the value in the tooltip and for screen readers; <c>null</c> = <see cref="ValueFormat"/>. Useful when the
    /// cells hide zeros (<c>#</c>) but the tooltip should still say 0.
    /// </summary>
    [Category("Appearance"), DefaultValue(null)]
    public string? ToolTipValueFormat
    {
        get => _toolTipValueFormat;
        set
        {
            _toolTipValueFormat = value;
            Invalidate();
        }
    }

    /// <summary>Text at the legend's low end; empty = the formatted minimum.</summary>
    [Category("Appearance"), DefaultValue("")]
    public string LegendMinText
    {
        get => _legendMinText;
        set
        {
            _legendMinText = value ?? "";
            Invalidate();
        }
    }

    /// <summary>Text at the legend's high end ("120+ min"); empty = the formatted maximum.</summary>
    [Category("Appearance"), DefaultValue("")]
    public string LegendMaxText
    {
        get => _legendMaxText;
        set
        {
            _legendMaxText = value ?? "";
            Invalidate();
        }
    }

    /// <summary>How cells without a value (<see cref="double.NaN"/>) are drawn.</summary>
    [Category("Appearance"), DefaultValue(TwHeatmapEmptyCell.Low)]
    public TwHeatmapEmptyCell EmptyCells
    {
        get => _emptyCells;
        set
        {
            _emptyCells = value;
            Invalidate();
        }
    }

    /// <summary>Color of <see cref="TwHeatmapEmptyCell.Solid"/> and <see cref="TwHeatmapEmptyCell.Hatched"/> cells; empty = a gray off the surface.</summary>
    [Category("Appearance")]
    public Color EmptyCellColor
    {
        get => _emptyCellColor;
        set
        {
            _emptyCellColor = value;
            Invalidate();
        }
    }

    private bool ShouldSerializeEmptyCellColor() => !_emptyCellColor.IsEmpty;

    private void ResetEmptyCellColor() => EmptyCellColor = Color.Empty;

    /// <summary>
    /// What a missing value means ("No production"): shown in the tooltip and read by screen readers instead of "—", and,
    /// with a gray or hatched <see cref="EmptyCells"/>, added to the legend.
    /// </summary>
    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string EmptyCellText
    {
        get => _emptyCellText;
        set
        {
            _emptyCellText = value ?? "";
            Invalidate();
        }
    }

    /// <summary>The highlighted cell as <c>row × ColumnCount + column</c> (-1 = none).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int HoverIndex
    {
        get => _hover;
        set
        {
            value = value < 0 || value >= RowCount * ColumnCount ? -1 : value;
            if (value == _hover) return;
            _hover = value;
            Invalidate();
            HoverIndexChanged?.Invoke(this, EventArgs.Empty);
            if (value >= 0 && Focused) AccessibilityNotifyClients(AccessibleEvents.Focus, value);
        }
    }

    /// <summary>
    /// The selected cell as <c>row × ColumnCount + column</c> (-1 = none), drawn with an outline in <see cref="SelectionColor"/>
    /// (the text color by default, so it stands out from a scale in the accent color) that stays when the mouse moves on. Set it from code, or turn <see cref="SelectOnClick"/> on so a click or Enter selects the cell
    /// (before <see cref="CellClick"/>).
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectedIndex
    {
        get => _selected;
        set
        {
            value = value < 0 || value >= RowCount * ColumnCount ? -1 : value;
            if (value == _selected) return;
            _selected = value;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            if (value >= 0) AccessibilityNotifyClients(AccessibleEvents.Selection, value);
        }
    }

    /// <summary>Raised when <see cref="SelectedIndex"/> changes.</summary>
    public event EventHandler? SelectedIndexChanged;

    /// <summary>A click or Enter selects the cell (<see cref="SelectedIndex"/>); off by default, so only code selects.</summary>
    [Category("Behavior"), DefaultValue(false)]
    public bool SelectOnClick { get; set; }

    /// <summary>
    /// Columns drawn on a faint band with their label in the text color (today, the current shift, a weekend); empty = none.
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<int> HighlightColumns
    {
        get => _highlightColumns;
        set
        {
            _highlightColumns = (value ?? Array.Empty<int>()).ToArray();
            Invalidate();
        }
    }

    /// <summary>
    /// Text color of the labels of <see cref="HighlightColumns"/> (a Sunday header in red without the application covering it);
    /// empty (the default) = the control's text color.
    /// </summary>
    [Category("Appearance")]
    public Color HighlightTextColor
    {
        get => _highlightTextColor;
        set
        {
            _highlightTextColor = value;
            Invalidate();
        }
    }

    private bool ShouldSerializeHighlightTextColor() => !_highlightTextColor.IsEmpty;

    private void ResetHighlightTextColor() => HighlightTextColor = Color.Empty;

    /// <summary>
    /// Outline color of the selected cell (<see cref="SelectedIndex"/>); empty (the default) = the control's text color, which
    /// stays visible on every shade of the scale. Before 0.1.6 the outline used <c>accent-*</c>, which blends into an accent scale.
    /// </summary>
    [Category("Appearance")]
    public Color SelectionColor
    {
        get => _selectionColor;
        set
        {
            _selectionColor = value;
            Invalidate();
        }
    }

    private bool ShouldSerializeSelectionColor() => !_selectionColor.IsEmpty;

    private void ResetSelectionColor() => SelectionColor = Color.Empty;

    /// <summary>
    /// Fixed size of a cell (96 DPI pixels); <see cref="Size.Empty"/> (the default) fills the control. A width or height of 0
    /// fills in that direction only. With a fixed size the grid starts at the top left and <c>GetPreferredSize</c> (and
    /// <c>AutoSize</c>) give the size that shows every cell.
    /// </summary>
    [Category("Layout")]
    public Size CellSize
    {
        get => _cellSize;
        set
        {
            value = new Size(Math.Max(0, value.Width), Math.Max(0, value.Height));
            if (value == _cellSize) return;
            _cellSize = value;
            if (AutoSize) Parent?.PerformLayout(this, nameof(CellSize));
            Invalidate();
        }
    }

    private bool ShouldSerializeCellSize() => !_cellSize.IsEmpty;

    private void ResetCellSize() => CellSize = Size.Empty;

    /// <summary>
    /// Where a cell is drawn, in client coordinates (<see cref="Rectangle.Empty"/> when the row or column does not exist), for
    /// placing a popover or an editor next to it.
    /// </summary>
    public Rectangle GetCellBounds(int row, int column) =>
        row < 0 || column < 0 || row >= RowCount || column >= ColumnCount ? Rectangle.Empty : Rectangle.Round(ComputeLayout().Cell(row, column));

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (_cellSize.IsEmpty) return base.GetPreferredSize(proposedSize);
        var layout = ComputeLayout();
        int width = _cellSize.Width > 0 ? (int)Math.Ceiling(layout.Grid.Left + layout.CellWidth * ColumnCount) + Padding.Right : Width;
        int height = _cellSize.Height > 0 ? (int)Math.Ceiling(layout.Grid.Top + layout.CellHeight * RowCount) + Padding.Bottom : Height;
        return new Size(width, height);
    }

    /// <summary>Replaces the labels and values in one step.</summary>
    public void SetData(IEnumerable<string> rows, IEnumerable<string> columns, double[,] values)
    {
        _rows = (rows ?? Enumerable.Empty<string>()).ToList();
        _columns = (columns ?? Enumerable.Empty<string>()).ToList();
        _values = values ?? new double[0, 0];
        OnDataChanged();
    }

    public int RowCount => Math.Max(_rows.Count, _values.GetLength(0));

    public int ColumnCount => Math.Max(_columns.Count, _values.GetLength(1));

    public double GetValue(int row, int column) =>
        row >= 0 && column >= 0 && row < _values.GetLength(0) && column < _values.GetLength(1) ? _values[row, column] : double.NaN;

    private string RowLabel(int row) => row < _rows.Count ? _rows[row] : "";

    private string ColumnLabel(int column) => column < _columns.Count ? _columns[column] : "";

    /// <summary>Raised after the data, scale or colors change (the legend control repaints).</summary>
    internal event EventHandler? AppearanceChanged;

    protected override void OnInvalidated(InvalidateEventArgs e)
    {
        base.OnInvalidated(e);
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDataChanged()
    {
        if (_hover >= RowCount * ColumnCount) _hover = -1;
        if (_selected >= RowCount * ColumnCount) SelectedIndex = -1;
        Motion.DataChanged(AnimateOnShow, AnimateChanges, _values.Length > 0, IntroCount,
            () => _fromColors = new Dictionary<(int, int), Color>(_shownColors));
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.Reorder, -1);
    }

    void ITwChartControl.SetChartColors(TwChartColors colors)
    {
        _colors = colors;
        Invalidate();
    }

    // ---- Animasyon ----

    private TwChartMotion Motion => _motion ??= new TwChartMotion(this);

    /// <summary>
    /// The first time the heatmap is shown with data, the cells fade in from the top left corner to the bottom right (600 ms each).
    /// Not when animations are off. <see cref="AnimateIn"/> plays it again.
    /// </summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool AnimateOnShow { get; set; } = true;

    /// <summary>
    /// When the values change after the heatmap was shown, each cell moves from its old shade to the new one (350 ms).
    /// Not when animations are off.
    /// </summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool AnimateChanges { get; set; } = true;

    /// <summary>Plays the opening animation now (e.g. after loading new data); nothing happens when animations are off.</summary>
    public void AnimateIn()
    {
        Motion.PlayIntro(IntroCount);
        Invalidate();
    }

    /// <summary>Share (0–1) of the opening animation that has passed; 1 when none runs.</summary>
    internal float IntroProgress => Motion.IntroOverall;

    /// <summary>Progress of the move to new data; 1 when none runs.</summary>
    internal float ChangeProgress => Motion.ChangeProgress;

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        Motion.TryIntro(AnimateOnShow, _values.Length > 0, IntroCount);
    }

    // Hücreler köşegen sırasıyla açılır: aynı köşegendekiler (satır + sütun) birlikte.
    private int IntroCount => Math.Max(1, RowCount + ColumnCount - 1);

    /// <summary>The shade of a cell as the last paint drew it.</summary>
    internal Color? PaintedCellColor(int row, int column) => _shownColors.TryGetValue((row, column), out var shown) ? shown : null;

    /// <summary>The shade of a cell as painted now: moving from its old shade while the values change (new cells fade in).</summary>
    private Color ShownCellColor(int row, int column, Color target, Color surface)
    {
        var shown = Motion.ChangeRunning
            ? TwMotion.Blend(_fromColors.TryGetValue((row, column), out var from) ? from : surface, target, Motion.ChangeProgress)
            : target;
        _shownColors[(row, column)] = shown;
        return shown;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _motion?.Dispose();
        base.Dispose(disposing);
    }

    // ---- Renk ölçeği ----

    /// <summary>The scale's ends: the set bounds, else the data's; all-positive data starts at zero.</summary>
    internal (double Min, double Max) Scale()
    {
        double min = double.MaxValue, max = double.MinValue;
        foreach (double v in _values)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) continue;
            min = Math.Min(min, v);
            max = Math.Max(max, v);
        }
        if (min > max) (min, max) = (0, 1);
        if (min > 0) min = 0;
        if (!double.IsNaN(_minimum)) min = _minimum;
        if (!double.IsNaN(_maximum)) max = _maximum;
        if (max - min < 1e-12) max = min + 1;
        return (min, max);
    }

    private Color Low => !_lowColor.IsEmpty ? _lowColor : TwCharts.Blend(TwCharts.Surface(this), ForeColor, _colors.Dark ? 0.08f : 0.06f);

    private Color High => !_highColor.IsEmpty ? _highColor : TwCharts.SeriesColor(0, _colors.Dark, _colors.Accent);

    private Color Negative
    {
        get
        {
            if (!_negativeColor.IsEmpty) return _negativeColor;
            var blue = TwCharts.SeriesColor(0, _colors.Dark, null);
            return High.ToArgb() == blue.ToArgb() ? TwCharts.SeriesColor(1, _colors.Dark, null) : blue;
        }
    }

    // Boş hücre grisi ölçeğin düşük ucundan ayrılsın diye yüzeyden bir adım daha uzaktır.
    private Color Empty => !_emptyCellColor.IsEmpty ? _emptyCellColor : TwCharts.Blend(TwCharts.Surface(this), ForeColor, _colors.Dark ? 0.16f : 0.12f);

    private Color HatchLine => TwCharts.Blend(Empty, ForeColor, _colors.Dark ? 0.22f : 0.18f);

    private bool EmptyInLegend => _emptyCellText.Length > 0 && _emptyCells is TwHeatmapEmptyCell.Solid or TwHeatmapEmptyCell.Hatched;

    private string FormatToolTipValue(double value) =>
        double.IsNaN(value) && _emptyCellText.Length > 0 ? _emptyCellText : TwCharts.Format(value, _toolTipValueFormat ?? _valueFormat);

    // Tooltip and screen reader share one source so a CellText handler changes both.
    private TwHeatmapCellTextEventArgs DescribeCell(int row, int column)
    {
        double value = GetValue(row, column);
        var args = new TwHeatmapCellTextEventArgs(row, column, value, FormatToolTipValue(value));
        CellText?.Invoke(this, args);
        return args;
    }

    private string CellName(int row, int column, string separator) =>
        string.Join(separator, new[] { RowLabel(row), ColumnLabel(column).Replace('\n', ' ') }.Where(x => x.Length > 0));

    // Değeri olmayan hücre EmptyCells'e göre: en düşük ton, düz gri, taralı gri ya da hiç.
    private void PaintEmptyCell(Graphics g, Rectangle box, float radius, float s, Color surface)
    {
        var back = Enabled ? Empty : TwCharts.Blend(Empty, surface, 0.5f);
        switch (_emptyCells)
        {
            case TwHeatmapEmptyCell.Low:
                TwPainter.FillRounded(g, box, radius, Enabled ? Low : TwCharts.Blend(Low, surface, 0.5f));
                break;
            case TwHeatmapEmptyCell.Solid:
                TwPainter.FillRounded(g, box, radius, back);
                break;
            case TwHeatmapEmptyCell.Hatched:
                TwCharts.FillHatched(g, box, radius, back, Enabled ? HatchLine : TwCharts.Blend(HatchLine, surface, 0.5f), s);
                break;
        }
    }

    /// <summary>The shade of a value; zero is the low color on a diverging scale.</summary>
    public Color GetCellColor(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return Low;
        var (min, max) = Scale();
        if (min < 0 && max > 0)
        {
            return value >= 0
                ? TwCharts.Blend(Low, High, (float)Math.Min(1, value / max))
                : TwCharts.Blend(Low, Negative, (float)Math.Min(1, value / min));
        }
        float t = (float)Math.Max(0, Math.Min(1, (value - min) / (max - min)));
        return TwCharts.Blend(Low, High, t);
    }

    // ---- Yerleşim ----

    private float DpiScale => TwDpi.DeviceDpi(this) / 96f;

    private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    internal sealed class HeatmapLayout
    {
        public Rectangle Grid;
        public Rectangle Legend;
        public int RowLabelWidth;
        public int ColumnLabelStep = 1;
        public float CellWidth, CellHeight;

        public RectangleF Cell(int row, int column) => new RectangleF(Grid.Left + column * CellWidth, Grid.Top + row * CellHeight, CellWidth, CellHeight);
    }

    internal HeatmapLayout ComputeLayout()
    {
        var layout = new HeatmapLayout();
        float s = DpiScale;
        int line = TextRenderer.MeasureText("Ag", Font, Size.Empty, Flags).Height;
        var content = Rectangle.FromLTRB(Padding.Left, Padding.Top, Math.Max(Padding.Left, Width - Padding.Right), Math.Max(Padding.Top, Height - Padding.Bottom));
        int top = content.Top;
        if (_showLegend)
        {
            layout.Legend = new Rectangle(content.Left, top, content.Width, line);
            top += line + (int)(8 * s);
        }
        // Sütun etiketleri "\n" ile birden çok satıra bölünebilir (uzun etmen adları).
        int labelLines = _columns.Where(x => x.Length > 0).Select(x => x.Split('\n').Length).DefaultIfEmpty(0).Max();
        if (labelLines > 0) top += line * labelLines + (int)(6 * s);
        layout.RowLabelWidth = _rows.Select(x => TextRenderer.MeasureText(x, Font, Size.Empty, Flags).Width).DefaultIfEmpty(0).Max();
        int left = content.Left + (layout.RowLabelWidth > 0 ? layout.RowLabelWidth + (int)(12 * s) : 0);
        layout.Grid = Rectangle.FromLTRB(left, top, Math.Max(left + 1, content.Right), Math.Max(top + 1, content.Bottom));
        int rows = RowCount, columns = ColumnCount;
        layout.CellWidth = _cellSize.Width > 0 ? _cellSize.Width * s : columns == 0 ? layout.Grid.Width : layout.Grid.Width / (float)columns;
        layout.CellHeight = _cellSize.Height > 0 ? _cellSize.Height * s : rows == 0 ? layout.Grid.Height : layout.Grid.Height / (float)rows;
        if (_cellSize.Width > 0) layout.Grid.Width = Math.Max(1, (int)Math.Ceiling(layout.CellWidth * columns));
        if (_cellSize.Height > 0) layout.Grid.Height = Math.Max(1, (int)Math.Ceiling(layout.CellHeight * rows));
        // Sığmayan sütun etiketleri atlanır (gün numaraları gibi).
        int widest = _columns.SelectMany(x => x.Split('\n')).Select(x => TextRenderer.MeasureText(x, Font, Size.Empty, Flags).Width).DefaultIfEmpty(0).Max();
        layout.ColumnLabelStep = Math.Max(1, (int)Math.Ceiling((widest + 6 * s) / Math.Max(1f, layout.CellWidth)));
        return layout;
    }

    // ---- Çizim ----

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        TwPainter.PaintParentBackground(this, g);
        if (BackColor.A > 0) using (var brush = new SolidBrush(BackColor)) g.FillRectangle(brush, ClientRectangle);
        _shownColors.Clear();
        Motion.MarkPainted(_values.Length > 0);
        float s = DpiScale;
        var layout = ComputeLayout();
        var muted = TwCharts.Muted(this, _colors);
        var surface = TwCharts.Surface(this);
        int rows = RowCount, columns = ColumnCount;
        float gap = Math.Min(3 * s, Math.Min(layout.CellWidth, layout.CellHeight) / 4);
        int line = TextRenderer.MeasureText("Ag", Font, Size.Empty, Flags).Height;

        for (int r = 0; r < rows; r++)
        {
            var cell = layout.Cell(r, 0);
            var text = RowLabel(r);
            if (text.Length > 0)
                TextRenderer.DrawText(g, text, Font, new Rectangle(Padding.Left, (int)cell.Y, layout.RowLabelWidth, (int)Math.Ceiling(cell.Height)), muted, Flags | TextFormatFlags.VerticalCenter);
        }
        // Vurgulu sütunlar: hücrelerin arkasında soluk bir şerit, etiketi metin renginde.
        foreach (int c in _highlightColumns)
        {
            if (c < 0 || c >= columns || rows == 0) continue;
            var band = RectangleF.Union(layout.Cell(0, c), layout.Cell(rows - 1, c));
            band.Inflate(0, gap);
            TwPainter.FillRounded(g, Rectangle.Round(band), Math.Min(6 * s, band.Width / 4),
                TwCharts.Blend(surface, ForeColor, _colors.Dark ? 0.14f : 0.08f));
        }
        for (int c = 0; c < columns; c++)
        {
            bool highlighted = _highlightColumns.Contains(c);
            if (c % layout.ColumnLabelStep != 0 && !highlighted) continue;
            var text = ColumnLabel(c);
            if (text.Length == 0) continue;
            var cell = layout.Cell(0, c);
            // Satırlar ızgaranın hemen üstünde biter; kısa etiketler alta hizalanır.
            var parts = text.Split('\n');
            int y = layout.Grid.Top - (int)(6 * s) - parts.Length * line;
            foreach (var part in parts)
            {
                var size = TextRenderer.MeasureText(part, Font, Size.Empty, Flags);
                TextRenderer.DrawText(g, part, Font, new Point((int)(cell.X + cell.Width / 2 - size.Width / 2f), y), highlighted ? (_highlightTextColor.IsEmpty ? ForeColor : _highlightTextColor) : muted, Flags);
                y += line;
            }
        }

        float radius = Math.Min(4 * s, Math.Min(layout.CellWidth, layout.CellHeight) / 4);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                var cell = layout.Cell(r, c);
                var box = Rectangle.Round(new RectangleF(cell.X + gap / 2, cell.Y + gap / 2, Math.Max(1, cell.Width - gap), Math.Max(1, cell.Height - gap)));
                double value = GetValue(r, c);
                var color = GetCellColor(value);
                if (!Enabled) color = TwCharts.Blend(color, surface, 0.5f);
                if (!double.IsNaN(value)) color = ShownCellColor(r, c, color, surface);
                if (double.IsNaN(value)) PaintEmptyCell(g, box, radius, s, surface);
                else TwPainter.FillRounded(g, box, radius, color);
                if (r * columns + c == _selected)
                    TwPainter.StrokeRounded(g, Rectangle.Inflate(box, (int)s, (int)s), radius + s, 2 * s, _selectionColor.IsEmpty ? ForeColor : _selectionColor);
                else if (r * columns + c == _hover)
                    TwPainter.StrokeRounded(g, Rectangle.Inflate(box, (int)s, (int)s), radius + s, 2 * s, ForeColor);
                if (_showValues && !double.IsNaN(value))
                {
                    var text = TwCharts.Format(value, _valueFormat);
                    var size = TextRenderer.MeasureText(text, Font, Size.Empty, Flags);
                    if (size.Width <= box.Width - 2 && size.Height <= box.Height)
                        TextRenderer.DrawText(g, text, Font, box, TwCharts.ReadableOn(color), Flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                // Açılışta hücre yüzey renginde bir örtünün altından belirir.
                float opened = Motion.IntroProgress(r + c);
                if (opened < 1f)
                    TwPainter.FillRounded(g, Rectangle.Inflate(box, (int)Math.Ceiling(s), (int)Math.Ceiling(s)), radius + s, Color.FromArgb((int)(255 * (1 - Math.Max(0f, opened))), surface));
            }
        }

        if (!layout.Legend.IsEmpty) PaintLegend(g, layout.Legend, muted, s);
        if (_hover >= 0 && columns > 0)
        {
            int r = _hover / columns, c = _hover % columns;
            var cell = layout.Cell(r, c);
            var anchor = _mouse ?? new Point((int)(cell.Right), (int)(cell.Y + cell.Height / 2));
            var text = DescribeCell(r, c);
            var title = text.Title ?? CellName(r, c, " · ");
            double value = text.Value;
            var key = double.IsNaN(value) && _emptyCells != TwHeatmapEmptyCell.Low ? (Color?)Empty : GetCellColor(value);
            TwCharts.DrawTooltip(g, this, anchor, title.Length > 0 ? title : (r + 1) + " · " + (c + 1),
                new[] { (key, text.ValueText) }, s);
        }
        if (Focused && ShowFocusCues)
            TwPainter.StrokeRounded(g, ClientRectangle, 6 * s, 2 * s, Color.FromArgb(160, _colors.Accent ?? TwCharts.SeriesColor(0, _colors.Dark, null)));
    }

    /// <summary>
    /// A separate control that draws this heatmap's legend (the gradient between the scale's ends and the empty-cell key),
    /// for placing it elsewhere when the heatmap is narrow: in a card header, next to a title. The heatmap stops drawing its
    /// own legend (<see cref="ShowLegend"/> = <c>false</c>); the legend follows the heatmap's data, colors and texts.
    /// </summary>
    public TwHeatmapLegend CreateLegend()
    {
        ShowLegend = false;
        return new TwHeatmapLegend { Heatmap = this };
    }

    private void PaintLegend(Graphics g, Rectangle area, Color muted, float s) => PaintLegend(g, area, muted, s, Font);

    /// <summary>Width the legend needs in <paramref name="font"/>.</summary>
    internal Size LegendSize(Font font, float s)
    {
        var (low, high) = LegendTexts();
        var lowSize = TextRenderer.MeasureText(low, font, Size.Empty, Flags);
        var highSize = TextRenderer.MeasureText(high, font, Size.Empty, Flags);
        int gap = (int)(8 * s);
        int width = highSize.Width + gap + (int)(96 * s) + gap + lowSize.Width;
        if (EmptyInLegend)
            width += gap * 2 + TextRenderer.MeasureText(_emptyCellText, font, Size.Empty, Flags).Width + (int)(6 * s) + (int)(12 * s);
        return new Size(width, Math.Max(lowSize.Height, highSize.Height));
    }

    private (string Low, string High) LegendTexts()
    {
        var (min, max) = Scale();
        return (_legendMinText.Length > 0 ? _legendMinText : TwCharts.Format(min, _valueFormat),
            _legendMaxText.Length > 0 ? _legendMaxText : TwCharts.Format(max, _valueFormat));
    }

    // Gösterge: alanın sağında min ve max metinleri arasında bir renk geçişi (varsa solunda boş hücre anahtarı).
    internal void PaintLegend(Graphics g, Rectangle area, Color muted, float s, Font font)
    {
        var (min, max) = Scale();
        var (low, high) = LegendTexts();
        var lowSize = TextRenderer.MeasureText(low, font, Size.Empty, Flags);
        var highSize = TextRenderer.MeasureText(high, font, Size.Empty, Flags);
        int bar = (int)(96 * s), gap = (int)(8 * s), height = (int)(6 * s);
        int x = area.Right - highSize.Width;
        TextRenderer.DrawText(g, high, font, new Point(x, area.Top), muted, Flags);
        x -= gap + bar;
        var strip = new Rectangle(x, area.Top + (area.Height - height) / 2, bar, height);
        // Geçiş, ıraksak ölçekte negatif uçtan başlar; parçalı çizim her iki ucun rengini doğru gösterir.
        int steps = Math.Max(1, bar / 2);
        for (int i = 0; i < steps; i++)
        {
            double v = min + (max - min) * (i + 0.5) / steps;
            using var brush = new SolidBrush(GetCellColor(v));
            g.FillRectangle(brush, strip.X + strip.Width * i / (float)steps, strip.Y, strip.Width / (float)steps + 0.5f, strip.Height);
        }
        x -= gap + lowSize.Width;
        TextRenderer.DrawText(g, low, font, new Point(x, area.Top), muted, Flags);
        if (!EmptyInLegend) return;

        // Boş hücre anahtarı: geçişin solunda küçük bir örnek kutu ve anlamı.
        var emptySize = TextRenderer.MeasureText(_emptyCellText, font, Size.Empty, Flags);
        x -= gap * 2 + emptySize.Width;
        TextRenderer.DrawText(g, _emptyCellText, font, new Point(x, area.Top), muted, Flags);
        int swatch = Math.Min(area.Height, (int)(12 * s));
        x -= (int)(6 * s) + swatch;
        var box = new Rectangle(x, area.Top + (area.Height - swatch) / 2, swatch, swatch);
        if (_emptyCells == TwHeatmapEmptyCell.Hatched) TwCharts.FillHatched(g, box, 2 * s, Empty, HatchLine, s);
        else TwPainter.FillRounded(g, box, 2 * s, Empty);
    }

    /// <summary>The muted text color the legend uses.</summary>
    internal Color LegendTextColor => TwCharts.Muted(this, _colors);

    // ---- Etkileşim ----

    /// <summary>The cell under a client point as <c>row × ColumnCount + column</c> (-1 = none).</summary>
    public int ItemAt(Point client)
    {
        var layout = ComputeLayout();
        int rows = RowCount, columns = ColumnCount;
        if (rows == 0 || columns == 0 || !layout.Grid.Contains(client)) return -1;
        int c = Math.Min(columns - 1, (int)((client.X - layout.Grid.Left) / layout.CellWidth));
        int r = Math.Min(rows - 1, (int)((client.Y - layout.Grid.Top) / layout.CellHeight));
        return r * columns + c;
    }

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
        if (e.Button == MouseButtons.Left && index >= 0) RaiseCellClick(index);
    }

    private void RaiseCellClick(int index)
    {
        int columns = ColumnCount;
        if (SelectOnClick) SelectedIndex = index;
        CellClick?.Invoke(this, new TwHeatmapCellEventArgs(index / columns, index % columns, GetValue(index / columns, index % columns)));
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & ~Keys.Shift) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int rows = RowCount, columns = ColumnCount;
        if (rows == 0 || columns == 0) return;
        _mouse = null;
        int r = _hover < 0 ? 0 : _hover / columns, c = _hover < 0 ? 0 : _hover % columns;
        switch (e.KeyCode)
        {
            case Keys.Left when _hover >= 0: c = Math.Max(0, c - 1); break;
            case Keys.Right when _hover >= 0: c = Math.Min(columns - 1, c + 1); break;
            case Keys.Up when _hover >= 0: r = Math.Max(0, r - 1); break;
            case Keys.Down when _hover >= 0: r = Math.Min(rows - 1, r + 1); break;
            case Keys.Left or Keys.Right or Keys.Up or Keys.Down: break;
            case Keys.Home: c = 0; break;
            case Keys.End: c = columns - 1; break;
            case Keys.Enter when _hover >= 0:
                RaiseCellClick(_hover);
                e.Handled = true;
                return;
            default: return;
        }
        HoverIndex = r * columns + c;
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

    int ITwChartItems.ItemCount => RowCount * ColumnCount;

    int ITwChartItems.ActiveItem
    {
        get => _hover;
        set => HoverIndex = value;
    }

    Rectangle ITwChartItems.ItemBounds(int index)
    {
        int columns = ColumnCount;
        return columns == 0 ? Rectangle.Empty : GetCellBounds(index / columns, index % columns);
    }

    /// <summary>Text of a cell for screen readers: row, column and value, or what <see cref="CellText"/> gives.</summary>
    public string GetItemText(int row, int column)
    {
        var text = DescribeCell(row, column);
        var name = text.Title ?? CellName(row, column, ", ");
        return (name.Length > 0 ? name + ": " : "") + text.ValueText;
    }

    string ITwChartItems.ItemText(int index) => ColumnCount == 0 ? "" : GetItemText(index / ColumnCount, index % ColumnCount);

    string ITwChartItems.Summary
    {
        get
        {
            var (min, max) = Scale();
            return TwCharts.Format(min, _valueFormat) + " – " + TwCharts.Format(max, _valueFormat);
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new TwChartAccessibleObject(this, this);
}

/// <summary>
/// The legend of a <see cref="TwHeatmap"/> as its own control (<see cref="TwHeatmap.CreateLegend"/>): the gradient between the
/// scale's ends and, with <see cref="TwHeatmap.EmptyCellText"/>, a sample of the empty cell. <c>AutoSize</c> by default.
/// </summary>
[ToolboxItem(false)]
public class TwHeatmapLegend : Control
{
    private TwHeatmap? _heatmap;

    public TwHeatmapLegend()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        BackColor = Color.Transparent;
        AutoSize = true;
    }

    /// <summary>The heatmap whose legend is drawn.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwHeatmap? Heatmap
    {
        get => _heatmap;
        set
        {
            if (_heatmap == value) return;
            if (_heatmap != null) _heatmap.AppearanceChanged -= OnHeatmapChanged;
            _heatmap = value;
            if (_heatmap != null) _heatmap.AppearanceChanged += OnHeatmapChanged;
            OnHeatmapChanged(this, EventArgs.Empty);
        }
    }

    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), DefaultValue(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set => base.AutoSize = value;
    }

    private float DpiScale => TwDpi.DeviceDpi(this) / 96f;

    private void OnHeatmapChanged(object? sender, EventArgs e)
    {
        if (AutoSize && _heatmap != null && GetPreferredSize(Size.Empty) != Size) Parent?.PerformLayout(this, nameof(Heatmap));
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (_heatmap is null) return new Size(Padding.Horizontal, Padding.Vertical);
        var size = _heatmap.LegendSize(Font, DpiScale);
        return new Size(size.Width + Padding.Horizontal, size.Height + Padding.Vertical);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        TwPainter.PaintParentBackground(this, e.Graphics);
        if (BackColor.A > 0) using (var brush = new SolidBrush(BackColor)) e.Graphics.FillRectangle(brush, ClientRectangle);
        if (_heatmap is null) return;
        var area = Rectangle.FromLTRB(Padding.Left, Padding.Top, Width - Padding.Right, Height - Padding.Bottom);
        _heatmap.PaintLegend(e.Graphics, area, _heatmap.LegendTextColor, DpiScale, Font);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Heatmap = null;
        base.Dispose(disposing);
    }
}
