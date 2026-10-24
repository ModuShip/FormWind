using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Stylable progress bar (instead of ProgressBar): rounded track and fill. Track <c>fill-*</c>, fill <c>accent-*</c>,
/// corners <c>rounded-*</c> (fully rounded by default). With <see cref="Indeterminate"/> a segment slides along the track
/// for work of unknown length; when <see cref="TwMotion.Enabled"/> is off the segment stands still in the middle. A new
/// <see cref="Value"/> slides the fill to its place in 300 ms (<c>duration-*</c>, <c>ease-*</c>; <c>transition-none</c> = at once).
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Value))]
[Description("Stylable progress bar.")]
public class TwProgress : Control, ITwChrome, ITwTimed
{
    private int _maximum = 100;
    private int _value;
    private bool _indeterminate;
    private float _phase;
    private TwChrome _chrome = TwChrome.None;
    private readonly Timer _timer = new Timer { Interval = 16 };
    private TwTween? _fill;
    private float _fillFrom;
    private TwTimingClasses _timing;

    /// <summary>Default time (ms) the fill takes to reach a new value.</summary>
    internal const int FillDuration = 300;

    public TwProgress()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        BackColor = Color.Transparent;
        _timer.Tick += (_, _) =>
        {
            if (!TwMotion.Enabled) _timer.Stop();
            else _phase = (_phase + 0.012f) % 1f;
            Invalidate();
        };
    }

    [Category("Behavior"), DefaultValue(100)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(1, value);
            Value = _value;
            Invalidate();
        }
    }

    [Category("Behavior"), DefaultValue(0)]
    public int Value
    {
        get => _value;
        set
        {
            value = Math.Max(0, Math.Min(_maximum, value));
            if (value == _value) return;
            // Dolgu o an gösterilen yerden yeni değere kayar (önceki kayış sürüyorsa onun ara konumundan).
            _fillFrom = FillFraction;
            _value = value;
            _fill ??= new TwTween(this);
            _fill.Start(_timing.For(FillDuration));
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
        }
    }

    /// <summary>Work of unknown length: a sliding segment is shown instead of the value.</summary>
    [Category("Behavior"), DefaultValue(false)]
    public bool Indeterminate
    {
        get => _indeterminate;
        set
        {
            _indeterminate = value;
            UpdateTimer();
            Invalidate();
        }
    }

    /// <summary>Animation position (0–1); can be set for tests and snapshots.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float Phase
    {
        get => _phase;
        set
        {
            _phase = value - (float)Math.Floor(value);
            Invalidate();
        }
    }

    /// <summary>Whether the sliding segment's timer is running (for tests).</summary>
    internal bool Animating => _timer.Enabled;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color TrackColor { get; set; } = ColorTranslator.FromHtml("#e5e7eb");   // gray-200

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BarColor { get; set; } = ColorTranslator.FromHtml("#2563eb");     // blue-600

    protected override Size DefaultSize => new Size(160, 8);

    void ITwChrome.SetChrome(TwChrome chrome)
    {
        if (_chrome.Equals(chrome)) return;
        _chrome = chrome;
        Invalidate();
    }

    /// <summary>Position of the fill (or the sliding segment) in client coordinates.</summary>
    public Rectangle BarBounds
    {
        get
        {
            if (_indeterminate)
            {
                int width = Math.Max(Height, Width * 2 / 5);
                int x = TwMotion.Enabled ? (int)((Width + width) * _phase) - width : (Width - width) / 2;
                return Rectangle.Intersect(new Rectangle(x, 0, width, Height), ClientRectangle);
            }
            return new Rectangle(0, 0, (int)Math.Round(Width * FillFraction), Height);
        }
    }

    /// <summary>The filled part (0–1) as drawn now: between the old and the new value while the fill slides.</summary>
    internal float FillFraction
    {
        get
        {
            float target = _value / (float)_maximum;
            float progress = _fill?.Progress ?? 1f;
            return progress >= 1f ? target : _fillFrom + (target - _fillFrom) * progress;
        }
    }

    void ITwTimed.SetTiming(TwTimingClasses timing) => _timing = timing;

    protected override AccessibleObject CreateAccessibilityInstance() => new ProgressAccessibleObject(this);

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        UpdateTimer();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UpdateTimer();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        TwPainter.PaintParentBackground(this, g);
        float radius = _chrome.Radius > 0 ? _chrome.Radius : Height / 2f;
        TwPainter.FillRounded(g, ClientRectangle, radius, TrackColor);
        var bar = BarBounds;
        if (bar.Width <= 0) return;

        // Dolgu rayın şekliyle kırpılır: kısa dolgu da yuvarlak uçlu görünür.
        using var clip = TwPainter.CreateRoundedRect(ClientRectangle, radius);
        var state = g.Save();
        g.SetClip(clip);
        TwPainter.FillRounded(g, bar, Math.Min(radius, bar.Width / 2f), Enabled ? BarColor : Color.FromArgb(110, BarColor));
        g.Restore(state);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _fill?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void UpdateTimer()
    {
        bool run = _indeterminate && !DesignMode && TwMotion.CanAnimate(this);
        if (run) _timer.Start();
        else _timer.Stop();
    }

    private sealed class ProgressAccessibleObject : ControlAccessibleObject
    {
        private readonly TwProgress _progress;

        public ProgressAccessibleObject(TwProgress progress) : base(progress) => _progress = progress;

        public override AccessibleRole Role => _progress.AccessibleRole != AccessibleRole.Default ? _progress.AccessibleRole : AccessibleRole.ProgressBar;

        public override string? Value
        {
            get => _progress.Indeterminate ? "" : $"%{Math.Round(_progress.Value * 100.0 / _progress.Maximum)}";
            set { }
        }
    }
}
