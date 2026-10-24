using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FormWind;

/// <summary>How <see cref="TwStat"/> fits a value that is wider than the card.</summary>
public enum TwStatFit
{
    /// <summary>The value is cut with "…".</summary>
    Ellipsis,

    /// <summary>The value's text gets smaller until it fits (down to the size of the title), then is cut.</summary>
    Shrink,
}

/// <summary>
/// KPI card: a title, a large value, the change with an arrow and an optional trend. The change is colored with the theme's
/// <c>success</c>/<c>danger</c> colors and always carries an arrow and a sign (never color alone); with
/// <see cref="PositiveIsGood"/> = <c>false</c> (costs, errors) a fall is good. The title and caption use <c>label:text-*</c>,
/// the trend <c>accent-*</c>. The <c>stat</c> component gives a card look. A value wider than the card switches to
/// <see cref="ShortValue"/> ("278 h" for "278 h 52 min") and then gets smaller (<see cref="ValueFit"/>) instead of being cut.
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
    private string _shortValue = "";
    private TwStatFit _valueFit = TwStatFit.Shrink;
    private (int Width, string Value, string Short, TwStatFit Fit, Font? Font, string Text, float Points) _fit;

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
        set
        {
            value ??= "";
            if (value != _value) StartCount(_value, value);
            Set(ref _value, value);
        }
    }

    /// <summary>
    /// Counts the number in a new <see cref="Value"/> up (or down) from the previous one, or from zero the first time, in
    /// 800 ms; the text keeps its form ("₺12.400" counts as "₺9.731"). Only a value with a single number is counted, and
    /// not when animations are off.
    /// </summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool AnimateValue { get; set; } = true;

    /// <summary>Time (ms) a changed value takes to count to its new number.</summary>
    internal const int CountDuration = 800;

    private TwTween? _count;
    private TwNumberText? _countTo;
    private double _countFrom;

    private void StartCount(string previous, string next)
    {
        _countTo = null;
        _count?.Stop();
        if (!AnimateValue || !TwMotion.CanAnimate(this)) return;
        _shown = true;
        if (!TwNumberText.TryParse(next, out var target)) return;
        double from = TwNumberText.TryParse(previous, out var old) && old!.SameForm(target!) ? old.Value : 0;
        if (from == target!.Value) return;
        _countTo = target;
        _countFrom = from;
        _count ??= new TwTween(this);
        _count.Start(CountDuration, TwEasing.Out);
        if (!_count.Running) _countTo = null;
    }

    // Değer görünmeden önce atanmışsa (Load, tasarımcı) ilk görünüşte sıfırdan sayılır.
    private bool _shown;

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (_shown || !TwMotion.CanAnimate(this)) return;
        _shown = true;
        if (_countTo is null) StartCount("", _value);
    }

    /// <summary>The value text drawn now: <paramref name="text"/>, or the counting number while a change is counted.</summary>
    internal string CountedText(string text)
    {
        if (_countTo is null || _count is null || !_count.Running || text != _value) return text;
        return _countTo.Format(_countFrom + (_countTo.Value - _countFrom) * _count.Progress);
    }

    /// <summary>A shorter form of <see cref="Value"/> shown when the value does not fit ("278 h" for "278 h 52 min").</summary>
    [Category("Appearance"), DefaultValue("")]
    public string ShortValue
    {
        get => _shortValue;
        set => Set(ref _shortValue, value ?? "");
    }

    /// <summary>What happens when the value (and <see cref="ShortValue"/>) does not fit: smaller text or "…".</summary>
    [Category("Appearance"), DefaultValue(TwStatFit.Shrink)]
    public TwStatFit ValueFit
    {
        get => _valueFit;
        set
        {
            _valueFit = value;
            Invalidate();
        }
    }

    /// <summary>The value text as drawn at the given width and its font size in points (the short form or a smaller size when needed).</summary>
    internal (string Text, float Points) FitValue(int width)
    {
        // Çizimde her seferinde font oluşturulmasın diye son sonuç saklanır.
        var key = (width, _value, _shortValue, _valueFit, ValueFont);
        if (_fit.Font != null && (_fit.Width, _fit.Value, _fit.Short, _fit.Fit, _fit.Font) == key) return (_fit.Text, _fit.Points);
        var result = ComputeFit(width);
        _fit = (width, _value, _shortValue, _valueFit, ValueFont, result.Text, result.Points);
        return result;
    }

    private (string Text, float Points) ComputeFit(int width)
    {
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        int Measure(string text, Font font) => TextRenderer.MeasureText(text, font, Size.Empty, flags).Width;
        var text = _value;
        if (Measure(text, ValueFont) <= width) return (text, ValueFont.SizeInPoints);
        if (_shortValue.Length > 0)
        {
            text = _shortValue;
            if (Measure(text, ValueFont) <= width) return (text, ValueFont.SizeInPoints);
        }
        if (_valueFit == TwStatFit.Ellipsis) return (text, ValueFont.SizeInPoints);
        // Yarım puanlık adımlarla başlık boyutuna kadar küçülür.
        float min = Font.SizeInPoints;
        for (float size = ValueFont.SizeInPoints - 0.5f; size > min; size -= 0.5f)
        {
            using var font = new Font(ValueFont.FontFamily, size, FontStyle.Bold, GraphicsUnit.Point);
            if (Measure(text, font) <= width) return (text, size);
        }
        return (text, min);
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
            // Satır yüksekliği tam boyuttan alınır; küçülen değer aynı satırda dikeyde ortalanır, alttaki satırlar kaymaz.
            int h = TextRenderer.MeasureText(g, _value, ValueFont, Size.Empty, flags).Height;
            var (text, points) = FitValue(content.Width);
            text = CountedText(text);
            var line = new Rectangle(content.X, y, content.Width, h);
            if (points >= ValueFont.SizeInPoints)
                TextRenderer.DrawText(g, text, ValueFont, line, Enabled ? ForeColor : muted, flags);
            else
            {
                using var small = new Font(ValueFont.FontFamily, points, FontStyle.Bold, GraphicsUnit.Point);
                TextRenderer.DrawText(g, text, small, line, Enabled ? ForeColor : muted, flags | TextFormatFlags.Bottom);
            }
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
        if (disposing)
        {
            _valueFont?.Dispose();
            _count?.Dispose();
        }
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
