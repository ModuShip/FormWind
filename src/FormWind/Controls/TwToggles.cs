using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Checkbox / switch colors: <c>accent-*</c> (checked) and <c>fill-*</c> (unchecked).</summary>
internal interface ITwToggle
{
    void SetToggleColors(Color? accent, Color? fill);
}

/// <summary>Shared drawing for the owner-drawn CheckBox, RadioButton and Switch.</summary>
internal static class TwToggleDrawing
{
    public static readonly Color DefaultAccent = ColorTranslator.FromHtml("#2563eb"); // blue-600
    public static readonly Color DefaultFill = Color.White;
    public static readonly Color DefaultBorder = ColorTranslator.FromHtml("#d1d5db"); // gray-300
    public static readonly Color DefaultTrack = ColorTranslator.FromHtml("#e5e7eb"); // gray-200

    public static int Scale(Control control, int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(control) / 96f);

    public static Color Dim(Color color, bool enabled) => enabled ? color : Color.FromArgb(color.A / 2, color);

    public static Size PreferredSize(Control control, int indicatorWidth, int indicatorHeight, int gap)
    {
        var text = string.IsNullOrEmpty(control.Text)
            ? Size.Empty
            : TextRenderer.MeasureText(control.Text, control.Font, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        int width = indicatorWidth + (text.Width > 0 ? gap + text.Width : 0);
        int height = Math.Max(indicatorHeight, text.Height);
        return new Size(width + control.Padding.Horizontal, height + control.Padding.Vertical);
    }

    /// <summary>Paints the background, returns the indicator's position and draws the text.</summary>
    public static Rectangle Layout(Control control, Graphics graphics, int indicatorWidth, int indicatorHeight, int gap)
    {
        TwPainter.PaintParentBackground(control, graphics);
        if (control.BackColor.A > 0)
        {
            using var back = new SolidBrush(control.BackColor);
            graphics.FillRectangle(back, control.ClientRectangle);
        }

        var content = new Rectangle(
            control.Padding.Left,
            control.Padding.Top,
            Math.Max(0, control.Width - control.Padding.Horizontal),
            Math.Max(0, control.Height - control.Padding.Vertical));
        // Sağdan sola: gösterge sağda, metin solunda.
        bool rtl = TwRtl.NeedsMirroring(control);
        var indicator = new Rectangle(rtl ? content.Right - indicatorWidth : content.X, content.Y + (content.Height - indicatorHeight) / 2, indicatorWidth, indicatorHeight);

        if (!string.IsNullOrEmpty(control.Text))
        {
            var textBounds = rtl
                ? new Rectangle(content.X, content.Y, Math.Max(0, indicator.Left - gap - content.X), content.Height)
                : new Rectangle(indicator.Right + gap, content.Y, Math.Max(0, content.Right - indicator.Right - gap), content.Height);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            if (rtl) flags = TwRtl.Mirror(flags);
            TextRenderer.DrawText(graphics, control.Text, control.Font, textBounds,
                control.Enabled ? control.ForeColor : SystemColors.GrayText, flags);
        }
        return indicator;
    }

    public static void DrawCheck(Graphics graphics, Rectangle box, Color color, float width)
    {
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        graphics.DrawLines(pen, new[]
        {
            new PointF(box.Left + box.Width * 0.25f, box.Top + box.Height * 0.52f),
            new PointF(box.Left + box.Width * 0.43f, box.Top + box.Height * 0.70f),
            new PointF(box.Left + box.Width * 0.76f, box.Top + box.Height * 0.32f),
        });
        graphics.Restore(state);
    }

    public static void FillEllipse(Graphics graphics, RectangleF bounds, Color color)
    {
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, bounds);
        graphics.Restore(state);
    }
}

/// <summary>
/// Owner-drawn checkbox. The checked color is set with <c>accent-*</c>, the unchecked fill with <c>fill-*</c>, the box border
/// with <c>border-*</c>, corners with <c>rounded-*</c>, text with <c>text-*</c>.
/// </summary>
[ToolboxItem(true)]
[Description("Stillenebilir onay kutusu.")]
public class TwCheckBox : CheckBox, ITwChrome, ITwToggle
{
    private TwChrome _chrome = TwChrome.None;
    private Color? _accent;
    private Color? _fill;

    public TwCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
    }

    private int BoxSize => TwToggleDrawing.Scale(this, 16);

    private int Gap => TwToggleDrawing.Scale(this, 8);

    void ITwChrome.SetChrome(TwChrome chrome)
    {
        _chrome = chrome;
        Invalidate();
    }

    void ITwToggle.SetToggleColors(Color? accent, Color? fill)
    {
        _accent = accent;
        _fill = fill;
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize) =>
        TwToggleDrawing.PreferredSize(this, BoxSize, BoxSize, Gap);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var box = TwToggleDrawing.Layout(this, g, BoxSize, BoxSize, Gap);
        var accent = TwToggleDrawing.Dim(_accent ?? TwToggleDrawing.DefaultAccent, Enabled);
        var radius = _chrome.Radius > 0 ? _chrome.Radius : TwToggleDrawing.Scale(this, 4);
        bool on = CheckState != CheckState.Unchecked;

        TwPainter.FillRounded(g, box, radius, on ? accent : TwToggleDrawing.Dim(_fill ?? TwToggleDrawing.DefaultFill, Enabled));
        var borderColor = on ? accent : TwToggleDrawing.Dim(_chrome.HasBorder ? _chrome.BorderColor : TwToggleDrawing.DefaultBorder, Enabled);
        var borderWidth = _chrome.HasBorder ? _chrome.BorderTop : Math.Max(1f, TwDpi.DeviceDpi(this) / 96f);
        TwPainter.StrokeRounded(g, box, radius, borderWidth, borderColor);

        if (CheckState == CheckState.Checked)
        {
            TwToggleDrawing.DrawCheck(g, box, Color.White, Math.Max(1.5f, box.Width / 8f));
        }
        else if (CheckState == CheckState.Indeterminate)
        {
            using var brush = new SolidBrush(Color.White);
            g.FillRectangle(brush, box.Left + box.Width / 4f, box.Top + box.Height / 2f - box.Height / 16f, box.Width / 2f, box.Height / 8f);
        }
    }
}

/// <summary>Owner-drawn radio button. Colors are set with the same classes as <see cref="TwCheckBox"/>.</summary>
[ToolboxItem(true)]
[Description("Stylable radio button.")]
public class TwRadioButton : RadioButton, ITwChrome, ITwToggle
{
    private TwChrome _chrome = TwChrome.None;
    private Color? _accent;
    private Color? _fill;

    public TwRadioButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
    }

    private int CircleSize => TwToggleDrawing.Scale(this, 16);

    private int Gap => TwToggleDrawing.Scale(this, 8);

    void ITwChrome.SetChrome(TwChrome chrome)
    {
        _chrome = chrome;
        Invalidate();
    }

    void ITwToggle.SetToggleColors(Color? accent, Color? fill)
    {
        _accent = accent;
        _fill = fill;
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize) =>
        TwToggleDrawing.PreferredSize(this, CircleSize, CircleSize, Gap);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var circle = TwToggleDrawing.Layout(this, g, CircleSize, CircleSize, Gap);
        var accent = TwToggleDrawing.Dim(_accent ?? TwToggleDrawing.DefaultAccent, Enabled);
        var radius = circle.Width / 2f;

        TwPainter.FillRounded(g, circle, radius, Checked ? accent : TwToggleDrawing.Dim(_fill ?? TwToggleDrawing.DefaultFill, Enabled));
        var borderColor = Checked ? accent : TwToggleDrawing.Dim(_chrome.HasBorder ? _chrome.BorderColor : TwToggleDrawing.DefaultBorder, Enabled);
        TwPainter.StrokeRounded(g, circle, radius, _chrome.HasBorder ? _chrome.BorderTop : Math.Max(1f, TwDpi.DeviceDpi(this) / 96f), borderColor);

        if (Checked)
        {
            var dot = RectangleF.Inflate(circle, -circle.Width * 0.32f, -circle.Height * 0.32f);
            TwToggleDrawing.FillEllipse(g, dot, Color.White);
        }
    }
}

/// <summary>
/// On/off switch (Tailwind's switch pattern: 44×24 px track, 20 px knob). The track is <c>accent-*</c> when on and
/// <c>fill-*</c> when off. The knob slides to its new position in 150 ms.
/// </summary>
[ToolboxItem(true)]
[Description("Stylable on/off switch.")]
public class TwSwitch : CheckBox, ITwChrome, ITwToggle
{
    private readonly Timer _timer = new Timer { Interval = 15 };
    private Color? _accent;
    private Color? _fill;
    private float _position;
    private DateTime _animationStart;
    private float _animationFrom;

    public TwSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        _timer.Tick += (_, _) => Animate();
    }

    /// <summary>Knob position: 0 = off, 1 = on (values in between during animation).</summary>
    [Browsable(false)]
    public float ThumbPosition => _position;

    private int TrackWidth => TwToggleDrawing.Scale(this, 44);

    private int TrackHeight => TwToggleDrawing.Scale(this, 24);

    private int Gap => TwToggleDrawing.Scale(this, 12);

    void ITwChrome.SetChrome(TwChrome chrome) => Invalidate();

    void ITwToggle.SetToggleColors(Color? accent, Color? fill)
    {
        _accent = accent;
        _fill = fill;
        Invalidate();
    }

    public override Size GetPreferredSize(Size proposedSize) =>
        TwToggleDrawing.PreferredSize(this, TrackWidth, TrackHeight, Gap);

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        _position = Checked ? 1 : 0;
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        base.OnCheckedChanged(e);
        if (!IsHandleCreated)
        {
            _position = Checked ? 1 : 0;
            return;
        }
        _animationFrom = _position;
        _animationStart = DateTime.UtcNow;
        _timer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }

    private void Animate()
    {
        const double duration = 150;
        var t = Math.Min(1, (DateTime.UtcNow - _animationStart).TotalMilliseconds / duration);
        var eased = 1 - Math.Pow(1 - t, 3); // ease-out
        float target = Checked ? 1 : 0;
        _position = (float)(_animationFrom + (target - _animationFrom) * eased);
        if (t >= 1)
        {
            _position = target;
            _timer.Stop();
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var track = TwToggleDrawing.Layout(this, g, TrackWidth, TrackHeight, Gap);
        var on = TwToggleDrawing.Dim(_accent ?? TwToggleDrawing.DefaultAccent, Enabled);
        var off = TwToggleDrawing.Dim(_fill ?? TwToggleDrawing.DefaultTrack, Enabled);
        TwPainter.FillRounded(g, track, track.Height / 2f, Blend(off, on, _position));

        float inset = TwToggleDrawing.Scale(this, 2);
        float thumb = track.Height - inset * 2;
        // Sağdan sola: açık konum solda.
        float travel = (track.Width - inset * 2 - thumb) * (TwRtl.NeedsMirroring(this) ? 1 - _position : _position);
        float x = track.Left + inset + travel;
        TwToggleDrawing.FillEllipse(g, new RectangleF(x, track.Top + inset, thumb, thumb), Color.White);
    }

    private static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
        (int)(from.A + (to.A - from.A) * amount),
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));
}
