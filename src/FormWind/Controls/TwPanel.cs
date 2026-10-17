using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Layout;

namespace FormWind;

/// <summary>
/// A panel that paints anti-aliased rounded corners and borders. With the <c>flex</c> or <c>grid</c> class it lays out
/// its children with the FormWind layout engine (Dock and Anchor are ignored).
/// </summary>
[ToolboxItem(true)]
[Description("Panel with rounded corners and flex/grid layout.")]
public class TwPanel : Panel, ITwChrome, ITwLayoutHost
{
    private TwChrome _chrome = TwChrome.None;

    public TwPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.SupportsTransparentBackColor,
            true);
    }

    [Category("FormWind"), DefaultValue(0f), Description("Corner radius (pixels). The value from Classes wins when set.")]
    public float CornerRadius
    {
        get => _chrome.Radius;
        set => SetUniform(Math.Max(0f, value), _chrome.BorderTop, _chrome.BorderColor);
    }

    [Category("FormWind"), DefaultValue(0f), Description("Border width (pixels, all sides).")]
    public float BorderWidth
    {
        get => _chrome.BorderTop;
        set => SetUniform(_chrome.Radius, Math.Max(0f, value), _chrome.BorderColor);
    }

    [Category("FormWind"), Description("Border color.")]
    public Color BorderColor
    {
        get => _chrome.BorderColor;
        set => SetUniform(_chrome.Radius, _chrome.BorderTop, value);
    }

    private void SetUniform(float radius, float width, Color color)
    {
        _chrome = new TwChrome(radius, width, width, width, width, color, _chrome.Gradient);
        Invalidate();
    }

    private bool ShouldSerializeBorderColor() => !_chrome.BorderColor.IsEmpty;

    private void ResetBorderColor() => BorderColor = Color.Empty;

    public override LayoutEngine LayoutEngine =>
        TwLayout.IsLayoutContainer(this) ? TwLayout.Engine : base.LayoutEngine;

    /// <summary>Computed from the content for a flex container; with <c>AutoSize = true</c> the panel sizes to its content.</summary>
    public override Size GetPreferredSize(Size proposedSize) =>
        TwLayout.TryMeasure(this, out var size) ? size : base.GetPreferredSize(proposedSize);

    void ITwChrome.SetChrome(TwChrome chrome)
    {
        if (_chrome.Equals(chrome)) return;
        bool borderChanged = BorderInsets() != Insets(chrome);
        _chrome = chrome;
        if (borderChanged) PerformLayout(this, nameof(BorderWidth));
        Invalidate();
    }

    /// <summary>
    /// As in the CSS box model the content is inside the border: children (Dock and flex/grid) are not placed over the border.
    /// </summary>
    public override Rectangle DisplayRectangle
    {
        get
        {
            var rect = base.DisplayRectangle;
            var border = BorderInsets();
            return new Rectangle(
                rect.X + border.Left,
                rect.Y + border.Top,
                Math.Max(0, rect.Width - border.Horizontal),
                Math.Max(0, rect.Height - border.Vertical));
        }
    }

    /// <summary>Border widths (rounded to pixels); the content starts this far inside.</summary>
    public Padding BorderInsets() => Insets(_chrome);

    private static Padding Insets(TwChrome chrome) => new Padding(
        (int)Math.Ceiling(chrome.BorderLeft),
        (int)Math.Ceiling(chrome.BorderTop),
        (int)Math.Ceiling(chrome.BorderRight),
        (int)Math.Ceiling(chrome.BorderBottom));

    // Çocukların doğal boyutları da ölçeklenir (bkz. TwLayout.OnHostScaling); WinForms çocukları bu çağrıdan sonra ölçekler.
    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        TwLayout.OnHostScaling(this, factor);
        base.ScaleControl(factor, specified);
    }

    // Sağdan sola değişince flex/grid yerleşimi aynalanır (bkz. TwLayoutEngine).
    protected override void OnRightToLeftChanged(EventArgs e)
    {
        base.OnRightToLeftChanged(e);
        PerformLayout();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        TwPainter.PaintParentBackground(this, e.Graphics);
        TwPainter.FillSurface(e.Graphics, ClientRectangle, _chrome.Radius, BackColor, _chrome.Gradient);
        TwPainter.StrokeChrome(e.Graphics, ClientRectangle, _chrome);
    }
}
