using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// The success mark of a <see cref="TwToast"/>: a soft circle in the text color with a check that draws itself from its
/// short stroke to the end of the long one. Without animation (or before <see cref="Play"/>) the check is drawn whole.
/// </summary>
internal sealed class TwCheckGlyph : Control
{
    // Onay işaretinin köşeleri, karenin kenarına göre (kısa çizgi aşağı, uzun çizgi sağ üste).
    private static readonly PointF[] Shape = { new PointF(0.28f, 0.52f), new PointF(0.44f, 0.67f), new PointF(0.73f, 0.36f) };

    private readonly TwTween _draw;

    public TwCheckGlyph()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        AccessibleRole = AccessibleRole.Graphic;
        _draw = new TwTween(this);
        FitToFont();
    }

    /// <summary>Drawn share of the check (0–1); 1 when it does not animate.</summary>
    internal float Progress => _draw.Progress;

    /// <summary>Draws the check from nothing after <paramref name="delayMs"/>; whole at once when animations are off.</summary>
    public void Play(int delayMs)
    {
        _draw.Start(TwMotion.Durations.L, TwEasing.InOut, delayMs);
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        FitToFont();
    }

    // İkon yazı tipiyle aynı boyda bir kare (text-lg ile büyür).
    private void FitToFont()
    {
        int side = (int)Math.Ceiling(Font.Height * 1.15f);
        Size = new Size(side, side);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        TwPainter.PaintParentBackground(this, e.Graphics);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float side = Math.Min(Width, Height) - 1;
        using (var circle = new SolidBrush(Color.FromArgb(38, ForeColor))) g.FillEllipse(circle, 0.5f, 0.5f, side, side);

        var points = Array.ConvertAll(Shape, p => new PointF(p.X * side, p.Y * side));
        float first = Distance(points[0], points[1]), second = Distance(points[1], points[2]);
        float drawn = (first + second) * Math.Max(0f, Math.Min(1f, Progress));
        if (drawn <= 0.01f) return;
        using var pen = new Pen(ForeColor, Math.Max(1.5f, side / 10f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        if (drawn <= first)
        {
            g.DrawLine(pen, points[0], Along(points[0], points[1], drawn / first));
            return;
        }
        g.DrawLines(pen, new[] { points[0], points[1], Along(points[1], points[2], (drawn - first) / second) });
    }

    private static float Distance(PointF a, PointF b) => (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

    private static PointF Along(PointF a, PointF b, float t) => new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _draw.Dispose();
        base.Dispose(disposing);
    }
}
