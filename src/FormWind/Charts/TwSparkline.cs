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
    private TwChartMotion? _motion;
    private double[] _fromValues = Array.Empty<double>();
    private (double Min, double Max)? _fromRange;

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
            // Ekranda görünen değerler ve ölçek, yeni değerlere geçişin başlangıcıdır.
            var (shown, range) = ShownValues();
            var from = shown.ToArray();
            var fromRange = range ?? TwCharts.SparklineRange(_values, _kind);
            _values = value ?? Array.Empty<double>();
            Motion.DataChanged(AnimateOnShow, AnimateChanges, _values.Count > 0, _values.Count, () =>
            {
                _fromValues = from;
                _fromRange = fromRange;
            });
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

    // ---- Animasyon ----

    private TwChartMotion Motion => _motion ??= new TwChartMotion(this);

    /// <summary>
    /// The first time the sparkline is shown with values, a line or area draws from left to right and bars grow from zero one
    /// after another (35 ms apart).
    /// Not when animations are off. <see cref="AnimateIn"/> plays it again.
    /// </summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool AnimateOnShow { get; set; } = true;

    /// <summary>
    /// When the values change after the sparkline was shown, the line or bars move from the old values to the new ones and the
    /// scale follows (350 ms). A different number of values is drawn at once.
    /// Not when animations are off.
    /// </summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool AnimateChanges { get; set; } = true;

    /// <summary>Plays the opening animation now (e.g. after loading new data); nothing happens when animations are off.</summary>
    public void AnimateIn()
    {
        Motion.PlayIntro(_values.Count);
        Invalidate();
    }

    /// <summary>Share (0–1) of the opening animation that has passed; 1 when none runs.</summary>
    internal float IntroProgress => Motion.IntroOverall;

    /// <summary>Progress of the move to new data; 1 when none runs.</summary>
    internal float ChangeProgress => Motion.ChangeProgress;

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        Motion.TryIntro(AnimateOnShow, _values.Count > 0, _values.Count);
    }

    /// <summary>The values a paint now would draw.</summary>
    internal IReadOnlyList<double> PaintedValues => ShownValues().Values;

    /// <summary>The values painted now and the scale they use (<c>null</c> = their own).</summary>
    private (IReadOnlyList<double> Values, (double Min, double Max)? Range) ShownValues()
    {
        if (Motion.IntroRunning && _kind == TwSparklineKind.Bar)
        {
            var grown = new double[_values.Count];
            for (int i = 0; i < grown.Length; i++) grown[i] = double.IsNaN(_values[i]) ? double.NaN : _values[i] * Math.Max(0f, Motion.IntroProgress(i));
            return (grown, TwCharts.SparklineRange(_values, _kind));
        }
        if (Motion.ChangeRunning && _fromValues.Length == _values.Count)
        {
            float p = Motion.ChangeProgress;
            var moving = new double[_values.Count];
            for (int i = 0; i < moving.Length; i++)
            {
                double from = _fromValues[i], to = _values[i];
                moving[i] = double.IsNaN(from) || double.IsInfinity(from) || double.IsNaN(to) || double.IsInfinity(to) ? to : from + (to - from) * p;
            }
            var target = TwCharts.SparklineRange(_values, _kind);
            if (_fromRange is (double, double) a && target is (double, double) b)
                return (moving, (a.Min + (b.Min - a.Min) * p, a.Max + (b.Max - a.Max) * p));
            return (moving, target);
        }
        return (_values, null);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _motion?.Dispose();
        base.Dispose(disposing);
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
        Motion.MarkPainted(_values.Count > 0);
        var (values, range) = ShownValues();
        var state = e.Graphics.Save();
        // Açılışta çizgi ve alan soldan sağa açılır (çubuklar tabandan büyür).
        if (Motion.IntroRunning && _kind != TwSparklineKind.Bar) e.Graphics.IntersectClip(new RectangleF(0, 0, Width * Motion.IntroOverall, Height));
        TwCharts.DrawSparkline(e.Graphics, area, values, _kind, color, NegativeColor, TwCharts.Surface(this), scale, _showMarker, range);
        e.Graphics.Restore(state);
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
