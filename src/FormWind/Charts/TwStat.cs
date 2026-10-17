using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// KPI card: a title, a large value, the change with an arrow and an optional trend. The change is colored with the theme's
/// <c>success</c>/<c>danger</c> colors and always carries an arrow and a sign (never color alone); with
/// <see cref="PositiveIsGood"/> = <c>false</c> (costs, errors) a fall is good. The title and caption use <c>label:text-*</c>,
/// the trend <c>accent-*</c>. The <c>stat</c> component gives a card look.
/// <code>
/// stat.Title = "Revenue"; stat.Value = "₺1,2 Mn"; stat.Delta = 0.124; stat.Caption = "vs last month";
/// stat.Trend = new[] { 820.0, 940, 910, 1040, 1200 };
/// </code>
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Value))]
[Description("KPI card with a value, its change and a trend.")]
public class TwStat : Control, ITwChrome, ITwChartControl
{
    private string _title = "";
    private string _value = "";
    private double? _delta;
    private string _deltaFormat = "+0.#%;-0.#%;0%";
    private string? _deltaText;
    private string _caption = "";
    private bool _positiveIsGood = true;
    private IReadOnlyList<double> _trend = Array.Empty<double>();
    private TwSparklineKind _trendKind = TwSparklineKind.Area;
    private TwChrome _chrome = TwChrome.None;
    private TwChartColors _colors = new TwChartColors();
    private Font? _valueFont;

    public TwStat()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        BackColor = Color.Transparent;
    }

    protected override Size DefaultSize => new Size(220, 132);

    protected override Padding DefaultPadding => new Padding(16);

    [Category("Appearance"), DefaultValue("")]
    public string Title
    {
        get => _title;
        set => Set(ref _title, value ?? "");
    }

    /// <summary>The headline number, already formatted ("₺1,2 Mn", "98,4%").</summary>
    [Category("Appearance"), DefaultValue("")]
    public string Value
    {
        get => _value;
        set => Set(ref _value, value ?? "");
    }

    /// <summary>Relative change (0.124 = +12.4%); <c>null</c> hides the change row.</summary>
    [Category("Appearance"), DefaultValue(null)]
    public double? Delta
    {
        get => _delta;
        set
        {
            _delta = value;
            Changed();
        }
    }

    /// <summary>Format of <see cref="Delta"/> (default <c>+0.#%;-0.#%;0%</c>).</summary>
    [Category("Appearance"), DefaultValue("+0.#%;-0.#%;0%")]
    public string DeltaFormat
    {
        get => _deltaFormat;
        set => Set(ref _deltaFormat, string.IsNullOrEmpty(value) ? "+0.#%;-0.#%;0%" : value);
    }

    /// <summary>Text shown instead of the formatted <see cref="Delta"/> ("+12 orders"); the sign of Delta still picks the arrow.</summary>
    [Category("Appearance"), DefaultValue(null)]
    public string? DeltaText
    {
        get => _deltaText;
        set
        {
            _deltaText = value;
            Changed();
        }
    }

    /// <summary>Muted text after the change ("vs last month").</summary>
    [Category("Appearance"), DefaultValue("")]
    public string Caption
    {
        get => _caption;
        set => Set(ref _caption, value ?? "");
    }

    /// <summary>Whether a rise is good (green); <c>false</c> for costs and errors, where a fall is green.</summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool PositiveIsGood
    {
        get => _positiveIsGood;
        set
        {
            _positiveIsGood = value;
            Changed();
        }
    }

    /// <summary>Values of the trend drawn at the bottom (empty = none).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<double> Trend
    {
        get => _trend;
        set
        {
            _trend = value ?? Array.Empty<double>();
            Changed();
        }
    }

    [Category("Appearance"), DefaultValue(TwSparklineKind.Area)]
    public TwSparklineKind TrendKind
    {
        get => _trendKind;
        set
        {
            _trendKind = value;
            Invalidate();
        }
    }

    private void Set(ref string field, string value)
    {
        if (field == value) return;
        field = value;
        Changed();
    }

    private void Changed()
    {
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
    }

    void ITwChrome.SetChrome(TwChrome chrome)
    {
        if (_chrome.Equals(chrome)) return;
        _chrome = chrome;
        Invalidate();
    }

    void ITwChartControl.SetChartColors(TwChartColors colors)
    {
        _colors = colors;
        Invalidate();
    }

    /// <summary>Whether the change is good news (green) or bad (red); <c>null</c> when there is no change or it is zero.</summary>
    internal bool? IsGood => _delta is double d && d != 0 ? (d > 0) == _positiveIsGood : null;

    internal string DeltaDisplay => _deltaText ?? (_delta is double d ? d.ToString(_deltaFormat, CultureInfo.CurrentCulture) : "");

    internal Color DeltaColor => IsGood switch
    {
        true => _colors.Positive ?? ColorTranslator.FromHtml(_colors.Dark ? "#4ade80" : "#15803d"),
        false => _colors.Negative ?? ColorTranslator.FromHtml(_colors.Dark ? "#f87171" : "#b91c1c"),
        null => TwCharts.Muted(this, _colors),
    };

    protected override void OnFontChanged(EventArgs e)
    {
        _valueFont?.Dispose();
        _valueFont = null;
        base.OnFontChanged(e);
    }

    private Font ValueFont => _valueFont ??= new Font(Font.FontFamily, Font.SizeInPoints * 2f, FontStyle.Bold, GraphicsUnit.Point);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        TwPainter.PaintParentBackground(this, g);
        var bounds = ClientRectangle;
        if (BackColor.A > 0) TwPainter.FillRounded(g, bounds, _chrome.Radius, BackColor);
        TwPainter.StrokeChrome(g, bounds, _chrome);

        float scale = TwDpi.DeviceDpi(this) / 96f;
        var content = Rectangle.FromLTRB(Padding.Left, Padding.Top, Width - Padding.Right, Height - Padding.Bottom);
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
        var muted = TwCharts.Muted(this, _colors);
        int y = content.Top;
        int gap = (int)(4 * scale);

        if (_title.Length > 0)
        {
            int h = TextRenderer.MeasureText(g, _title, Font, Size.Empty, flags).Height;
            TextRenderer.DrawText(g, _title, Font, new Rectangle(content.X, y, content.Width, h), muted, flags);
            y += h + gap;
        }
        if (_value.Length > 0)
        {
            int h = TextRenderer.MeasureText(g, _value, ValueFont, Size.Empty, flags).Height;
            TextRenderer.DrawText(g, _value, ValueFont, new Rectangle(content.X, y, content.Width, h), Enabled ? ForeColor : muted, flags);
            y += h + gap;
        }

        var delta = DeltaDisplay;
        if (delta.Length > 0 || _caption.Length > 0)
        {
            int h = TextRenderer.MeasureText(g, "Ag", Font, Size.Empty, flags).Height;
            int x = content.X;
            if (delta.Length > 0)
            {
                // Değişim yalnızca renkle anlatılmaz: ok ve işaret her zaman vardır.
                var color = DeltaColor;
                if (_delta is double d && d != 0)
                {
                    int em = TwIconRenderer.EmPixels(Font.SizeInPoints, TwDpi.DeviceDpi(this));
                    TwIconRenderer.DrawIcon(g, d > 0 ? "arrow-up" : "arrow-down", em, new Rectangle(x, y, em, h), color);
                    x += em + gap / 2;
                }
                using var bold = new Font(Font, FontStyle.Bold);
                int w = TextRenderer.MeasureText(g, delta, bold, Size.Empty, flags & ~TextFormatFlags.EndEllipsis).Width;
                TextRenderer.DrawText(g, delta, bold, new Rectangle(x, y, Math.Min(w, content.Right - x), h), color, flags);
                x += w + gap * 2;
            }
            if (_caption.Length > 0 && x < content.Right)
                TextRenderer.DrawText(g, _caption, Font, new Rectangle(x, y, content.Right - x, h), muted, flags);
            y += h + gap * 2;
        }

        if (_trend.Count > 0)
        {
            var area = Rectangle.FromLTRB(content.Left, y, content.Right, content.Bottom);
            if (area.Height >= (int)(16 * scale))
            {
                var color = TwCharts.SeriesColor(0, _colors.Dark, _colors.Accent);
                TwCharts.DrawSparkline(g, Rectangle.Inflate(area, -(int)(4 * scale), -(int)(2 * scale)), _trend, _trendKind, color,
                    ColorTranslator.FromHtml("#e34948"), TwCharts.Surface(this), scale);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _valueFont?.Dispose();
        base.Dispose(disposing);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new StatAccessibleObject(this);

    private sealed class StatAccessibleObject : ControlAccessibleObject
    {
        private readonly TwStat _owner;

        public StatAccessibleObject(TwStat owner) : base(owner) => _owner = owner;

        public override AccessibleRole Role => _owner.AccessibleRole != AccessibleRole.Default ? _owner.AccessibleRole : AccessibleRole.StaticText;

        public override string? Name
        {
            get => _owner.AccessibleName ?? _owner.Title;
            set { }
        }

        public override string? Value
        {
            get
            {
                var parts = new List<string>();
                if (_owner.Value.Length > 0) parts.Add(_owner.Value);
                if (_owner.DeltaDisplay.Length > 0) parts.Add(_owner.DeltaDisplay);
                if (_owner.Caption.Length > 0) parts.Add(_owner.Caption);
                return string.Join(", ", parts);
            }
            set { }
        }
    }
}
