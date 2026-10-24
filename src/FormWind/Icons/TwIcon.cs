using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// A lightweight control that draws an icon. The color is set with <c>text-*</c> (ForeColor), the size with the <c>text-*</c> font size:
/// <c>text-base</c> = a 16 px icon. AutoSize is on by default. <c>animate-spin</c> turns it (loading indicator),
/// <c>animate-pulse</c> fades it in and out.
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(IconName))]
[Description("Draws an icon from the Windows icon font or a theme SVG.")]
public class TwIcon : Control, ITwAnimated
{
    private string _iconName = "";
    private TwAnimation _animation;

    public TwIcon()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        AutoSize = true;
    }

    [Category("FormWind"), DefaultValue(""), TypeConverter(typeof(TwIconNameConverter))]
    [Description("Icon name (e.g. plus, search, user) or a code point (#E710).")]
    public string IconName
    {
        get => _iconName;
        set
        {
            _iconName = value ?? "";
            if (AutoSize) Parent?.PerformLayout(this, nameof(IconName));
            Invalidate();
        }
    }

    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), DefaultValue(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set => base.AutoSize = value;
    }

    protected override Size DefaultSize => new Size(16, 16);

    /// <summary>Size of the square icon box (the font size in pixels).</summary>
    internal int EmPixels => TwIconRenderer.EmPixels(Font.SizeInPoints, TwDpi.DeviceDpi(this));

    public override Size GetPreferredSize(Size proposedSize)
    {
        var em = EmPixels;
        return new Size(em + Padding.Horizontal, em + Padding.Vertical);
    }

    // AutoSize kontrolün tercih edilen boyutu değişince ebeveyn yeniden yerleşmeli (Label bunu kendisi yapar).
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (AutoSize) Parent?.PerformLayout(this, nameof(Font));
        Invalidate();
    }

    protected override void OnPaddingChanged(EventArgs e)
    {
        base.OnPaddingChanged(e);
        if (AutoSize) Parent?.PerformLayout(this, nameof(Padding));
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!TwTheme.Current.HasIcon(_iconName)) return;

        var bounds = new Rectangle(
            Padding.Left,
            Padding.Top,
            Math.Max(0, Width - Padding.Horizontal),
            Math.Max(0, Height - Padding.Vertical));
        var color = _animation == TwAnimation.Pulse ? TwAnimations.Fade(ForeColor, TwAnimations.PulseOpacity(this)) : ForeColor;
        float angle = _animation == TwAnimation.Spin ? TwAnimations.SpinAngle(this) : 0f;
        if (angle != 0f)
        {
            // Simge kutusunun ortası etrafında döner.
            var state = e.Graphics.Save();
            e.Graphics.TranslateTransform(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
            e.Graphics.RotateTransform(angle);
            e.Graphics.TranslateTransform(-(bounds.X + bounds.Width / 2f), -(bounds.Y + bounds.Height / 2f));
            TwIconRenderer.DrawIcon(e.Graphics, _iconName, EmPixels, bounds, color);
            e.Graphics.Restore(state);
        }
        else TwIconRenderer.DrawIcon(e.Graphics, _iconName, EmPixels, bounds, color);
    }

    /// <summary>The running <c>animate-*</c> animation (spin or pulse).</summary>
    [Browsable(false)]
    public TwAnimation Animation => _animation;

    void ITwAnimated.SetAnimation(TwAnimation animation)
    {
        if (_animation == animation) return;
        _animation = animation;
        TwAnimations.Track(this, animation);
        Invalidate();
    }
}
