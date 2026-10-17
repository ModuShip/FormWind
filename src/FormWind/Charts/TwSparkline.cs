using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Shape of a <see cref="TwSparkline"/> (and of a TwSparklineColumn).</summary>
public enum TwSparklineKind
{
    Line,
    Area,
    Bar,
}

/// <summary>
/// Small chart without axes for a trend next to a number: a 2 px line ending in a marker, a light area or bars. The color is
/// <c>accent-*</c> (default: the first palette color); bars below zero use <see cref="NegativeColor"/>. Missing values
/// (<see cref="double.NaN"/>) leave a gap.
/// </summary>
[ToolboxItem(true)]
[Description("Small trend chart without axes.")]
public class TwSparkline : Control, ITwChartControl
{
    private IReadOnlyList<double> _values = Array.Empty<double>();
    private TwSparklineKind _kind;
    private bool _showMarker = true;
    private TwChartColors _colors = new TwChartColors();

    public TwSparkline()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        BackColor = Color.Transparent;
    }

    protected override Size DefaultSize => new Size(120, 32);

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<double> Values
    {
        get => _values;
        set
        {
            _values = value ?? Array.Empty<double>();
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        }
    }

    [Category("Appearance"), DefaultValue(TwSparklineKind.Line)]
    public TwSparklineKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            Invalidate();
        }
    }

    /// <summary>A marker on the last point of a line or area.</summary>
    [Category("Appearance"), DefaultValue(true)]
    public bool ShowMarker
    {
        get => _showMarker;
        set
        {
            _showMarker = value;
            Invalidate();
        }
    }

    /// <summary>Line or bar color; empty = <c>accent-*</c> or the first palette color.</summary>
    [Category("Appearance")]
    public Color LineColor { get; set; } = Color.Empty;

    private bool ShouldSerializeLineColor() => !LineColor.IsEmpty;

    [Category("Appearance")]
    public Color NegativeColor { get; set; } = ColorTranslator.FromHtml("#e34948");

    private bool ShouldSerializeNegativeColor() => NegativeColor.ToArgb() != ColorTranslator.FromHtml("#e34948").ToArgb();

    void ITwChartControl.SetChartColors(TwChartColors colors)
    {
        _colors = colors;
        Invalidate();
    }

    /// <summary>The color the line is drawn with.</summary>
    internal Color EffectiveColor => !LineColor.IsEmpty ? LineColor : TwCharts.SeriesColor(0, _colors.Dark, _colors.Accent);

    protected override void OnPaint(PaintEventArgs e)
    {
        TwPainter.PaintParentBackground(this, e.Graphics);
        if (BackColor.A > 0) using (var brush = new SolidBrush(BackColor)) e.Graphics.FillRectangle(brush, ClientRectangle);
        float scale = TwDpi.DeviceDpi(this) / 96f;
        int inset = (int)Math.Ceiling(6 * scale);
        var area = Rectangle.FromLTRB(Padding.Left + inset, Padding.Top + inset, Width - Padding.Right - inset, Height - Padding.Bottom - inset);
        var color = Enabled ? EffectiveColor : TwCharts.Blend(EffectiveColor, TwCharts.Surface(this), 0.5f);
        TwCharts.DrawSparkline(e.Graphics, area, _values, _kind, color, NegativeColor, TwCharts.Surface(this), scale, _showMarker);
    }

    /// <summary>Text for screen readers: the last value, the lowest and the highest.</summary>
    internal string Summary
    {
        get
        {
            var numbers = _values.Where(v => !double.IsNaN(v)).ToList();
            if (numbers.Count == 0) return "";
            string F(double v) => v.ToString("#,0.##", CultureInfo.CurrentCulture);
            return F(numbers[numbers.Count - 1]) + " (" + F(numbers.Min()) + " – " + F(numbers.Max()) + ")";
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new SparklineAccessibleObject(this);

    private sealed class SparklineAccessibleObject : ControlAccessibleObject
    {
        private readonly TwSparkline _owner;

        public SparklineAccessibleObject(TwSparkline owner) : base(owner) => _owner = owner;

        public override AccessibleRole Role => _owner.AccessibleRole != AccessibleRole.Default ? _owner.AccessibleRole : AccessibleRole.Chart;

        public override string? Value
        {
            get => _owner.Summary;
            set { }
        }
    }
}
