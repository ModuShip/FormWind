using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Stylable slider (instead of TrackBar): a thin rounded track, a filled part and a round thumb.
/// The track is colored with <c>fill-*</c>, the filled part and thumb border with <c>accent-*</c>. Keyboard: ←/→/↑/↓ <see cref="SmallChange"/>,
/// PageUp/PageDown <see cref="LargeChange"/>, Home/End the ends; mouse wheel and dragging.
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValueChanged))]
[DefaultProperty(nameof(Value))]
[Description("Stylable slider.")]
public class TwSlider : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private bool _dragging;
    private bool _hover;
    private bool _focusVisible;

    public TwSlider()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
        TwFocusVisible.EnsureInstalled();
        TabStop = true;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
    }

    public event EventHandler? ValueChanged;

    [Category("Behavior"), DefaultValue(0)]
    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < value) _maximum = value;
            Value = _value;
            Invalidate();
        }
    }

    [Category("Behavior"), DefaultValue(100)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            if (_minimum > value) _minimum = value;
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
            value = Math.Max(_minimum, Math.Min(_maximum, value));
            if (value == _value) return;
            _value = value;
            Invalidate();
            AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [Category("Behavior"), DefaultValue(1)]
    public int SmallChange { get; set; } = 1;

    [Category("Behavior"), DefaultValue(10)]
    public int LargeChange { get; set; } = 10;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color TrackColor { get; set; } = ColorTranslator.FromHtml("#e5e7eb");   // gray-200

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color AccentColor { get; set; } = ColorTranslator.FromHtml("#2563eb");  // blue-600

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ThumbColor { get; set; } = Color.White;

    protected override Size DefaultSize => new Size(160, 24);

    private int Scale(int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(this) / 96f);

    private int ThumbSize => Scale(16);

    /// <summary>The part of the track the thumb center can travel.</summary>
    private (int Left, int Right) TrackRange => (ThumbSize / 2 + 1, Math.Max(ThumbSize / 2 + 2, Width - ThumbSize / 2 - 1));

    public Rectangle ThumbBounds
    {
        get
        {
            var (left, right) = TrackRange;
            float ratio = _maximum == _minimum ? 0 : (_value - _minimum) / (float)(_maximum - _minimum);
            int x = left + (int)Math.Round((right - left) * ratio);
            return new Rectangle(x - ThumbSize / 2, (Height - ThumbSize) / 2, ThumbSize, ThumbSize);
        }
    }

    /// <summary>The value at a horizontal position.</summary>
    public int ValueFromX(int x)
    {
        var (left, right) = TrackRange;
        float ratio = Math.Max(0f, Math.Min(1f, (x - left) / (float)Math.Max(1, right - left)));
        return _minimum + (int)Math.Round(ratio * (_maximum - _minimum));
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new SliderAccessibleObject(this);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (CanFocus) Focus();
        _focusVisible = false;
        _dragging = true;
        Capture = true;
        Value = ValueFromX(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging) Value = ValueFromX(e.X);
        bool hover = ThumbBounds.Contains(e.Location);
        if (hover != _hover)
        {
            _hover = hover;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Capture = false;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Value += Math.Sign(e.Delta) * SmallChange;
        if (e is HandledMouseEventArgs handled) handled.Handled = true;
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _focusVisible = true;
        switch (e.KeyCode)
        {
            case Keys.Left: case Keys.Down: Value -= SmallChange; break;
            case Keys.Right: case Keys.Up: Value += SmallChange; break;
            case Keys.PageDown: Value -= LargeChange; break;
            case Keys.PageUp: Value += LargeChange; break;
            case Keys.Home: Value = _minimum; break;
            case Keys.End: Value = _maximum; break;
            default: return;
        }
        Invalidate();
        e.Handled = true;
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _focusVisible = TwFocusVisible.LastInputWasKeyboard;
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        TwPainter.PaintParentBackground(this, g);
        var (left, right) = TrackRange;
        int trackHeight = Scale(6);
        var track = new Rectangle(left - trackHeight / 2, (Height - trackHeight) / 2, right - left + trackHeight, trackHeight);
        var thumb = ThumbBounds;
        var accent = Enabled ? AccentColor : Color.FromArgb(110, AccentColor);

        TwPainter.FillRounded(g, track, trackHeight / 2f, TrackColor);
        var filled = Rectangle.FromLTRB(track.Left, track.Top, thumb.Left + thumb.Width / 2, track.Bottom);
        if (filled.Width > 0) TwPainter.FillRounded(g, filled, trackHeight / 2f, accent);

        if (Focused && _focusVisible)
            TwPainter.FillRounded(g, Rectangle.Inflate(thumb, Scale(4), Scale(4)), thumb.Width / 2f + Scale(4), Color.FromArgb(60, accent));
        else if (_hover || _dragging)
            TwPainter.FillRounded(g, Rectangle.Inflate(thumb, Scale(3), Scale(3)), thumb.Width / 2f + Scale(3), Color.FromArgb(35, accent));

        TwPainter.FillRounded(g, thumb, thumb.Width / 2f, ThumbColor);
        TwPainter.StrokeRounded(g, thumb, thumb.Width / 2f, Math.Max(1.5f, Scale(2)), accent);
    }

    private sealed class SliderAccessibleObject : ControlAccessibleObject
    {
        private readonly TwSlider _slider;

        public SliderAccessibleObject(TwSlider slider) : base(slider) => _slider = slider;

        public override AccessibleRole Role => _slider.AccessibleRole != AccessibleRole.Default ? _slider.AccessibleRole : AccessibleRole.Slider;

        public override string? Value
        {
            get => _slider.Value.ToString(System.Globalization.CultureInfo.CurrentCulture);
            set
            {
                if (int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.CurrentCulture, out var number))
                    _slider.Value = number;
            }
        }
    }
}
