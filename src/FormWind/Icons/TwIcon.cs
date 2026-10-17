using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// A lightweight control that draws an icon. The color is set with <c>text-*</c> (ForeColor), the size with the <c>text-*</c> font size:
/// <c>text-base</c> = a 16 px icon. AutoSize is on by default.
/// </summary>
[ToolboxItem(true)]
[DefaultProperty(nameof(IconName))]
[Description("Draws an icon from the Windows icon font or a theme SVG.")]
public class TwIcon : Control
{
    private string _iconName = "";

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
        TwIconRenderer.DrawIcon(e.Graphics, _iconName, EmPixels, bounds, ForeColor);
    }
}
