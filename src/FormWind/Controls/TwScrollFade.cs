using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// The soft edge a <see cref="TwChipStrip"/> lays between a shown scroll button and the chips under it: the chips show
/// through and fade into the strip's background toward the button. Mouse input passes through to the chip underneath.
/// </summary>
internal sealed class TwScrollFade : Control
{
    private const int WM_NCHITTEST = 0x0084;
    private const int HTTRANSPARENT = -1;

    public TwScrollFade()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        TabStop = false;
        Visible = false;
    }

    /// <summary>The side that touches the button: the fade is opaque there.</summary>
    public bool OpaqueAtLeft { get; set; }

    protected override void WndProc(ref Message m)
    {
        // Tıklama ve tekerlek alttaki çipe gitsin: solma yalnızca görüntüdür.
        if (m.Msg == WM_NCHITTEST)
        {
            m.Result = (IntPtr)HTTRANSPARENT;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Parent is not { } strip || Width <= 0 || Height <= 0) return;
        DrawSiblingsBelow(e.Graphics, strip);
        var back = TwColorsOf.EffectiveBack(strip);
        var bounds = new Rectangle(0, 0, Width, Height);
        // Gradyan sınırı yarım piksel taşar; uçta ince bir çizgi kalmasın diye fırça bir piksel geniş tutulur.
        var brushBounds = Rectangle.Inflate(bounds, 1, 0);
        var transparent = Color.FromArgb(0, back);
        using var brush = new LinearGradientBrush(brushBounds, OpaqueAtLeft ? back : transparent, OpaqueAtLeft ? transparent : back, LinearGradientMode.Horizontal);
        e.Graphics.FillRectangle(brush, bounds);
    }

    // Pencere opaktır: altında kalan çiplerin görüntüsü buraya kopyalanır (z sırasında bu katmanın altındakiler).
    private void DrawSiblingsBelow(Graphics graphics, Control strip)
    {
        var fadeBounds = Bounds;
        int own = strip.Controls.GetChildIndex(this);
        for (int i = strip.Controls.Count - 1; i > own; i--)
        {
            var sibling = strip.Controls[i];
            if (!sibling.Visible || sibling is TwScrollFade || sibling.Width <= 0 || sibling.Height <= 0) continue;
            if (!sibling.Bounds.IntersectsWith(fadeBounds)) continue;
            using var image = new Bitmap(sibling.Width, sibling.Height);
            sibling.DrawToBitmap(image, new Rectangle(Point.Empty, sibling.Size));
            graphics.DrawImage(image, sibling.Left - Left, sibling.Top - Top);
        }
    }
}
