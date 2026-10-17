using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Stylable progress bar (instead of ProgressBar): rounded track and fill. Track <c>fill-*</c>, fill <c>accent-*</c>,
/// corners <c>rounded-*</c> (fully rounded by default). With <see cref="Indeterminate"/> a segment slides along the track
/// for work of unknown length.
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(Value))]
[Description("Stylable progress bar.")]
public class TwProgress : Control, ITwChrome
{
    private int _maximum = 100;
    private int _value;
    private bool _indeterminate;
    private float _phase;
    private TwChrome _chrome = TwChrome.None;
    private readonly Timer _timer = new Timer { Interval = 16 };

    public TwProgress()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        BackColor = Color.Transparent;
        _timer.Tick += (_, _) =>
        {
            _phase = (_phase + 0.012f) % 1f;
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
            _value = value;
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
                int x = (int)((Width + width) * _phase) - width;
                return Rectangle.Intersect(new Rectangle(x, 0, width, Height), ClientRectangle);
            }
            return new Rectangle(0, 0, (int)Math.Round(Width * (_value / (float)_maximum)), Height);
        }
    }

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
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }

    private void UpdateTimer()
    {
        bool run = _indeterminate && Visible && IsHandleCreated && !DesignMode;
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
