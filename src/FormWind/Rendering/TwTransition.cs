using System;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Transitions between two looks of a part of the window: a page switch, a theme change. The old look is kept as a
/// picture over the part, the change is applied underneath and painted, then the picture fades away.
/// <code>
/// TwTransition.CrossFade(pnlPage, () => ShowPage(next));
/// TwTransition.CrossFade(this, () => provider.DarkMode = !provider.DarkMode);   // the whole form, title bar included
/// </code>
/// <para>
/// The picture is what is on screen once the pending paints are done (overlapping controls, a custom title bar and
/// scrolled panels come out as shown). When the part is covered by another application's window or is off screen, the
/// window's content is asked from the desktop window manager instead, and <see cref="Control.DrawToBitmap"/> is the last
/// resort, which redraws the controls and can get overlapping controls wrong. Anything above the part on screen, such as
/// a notification of the same window, is in the picture too and fades with it.
/// </para>
/// </summary>
public static class TwTransition
{
    /// <summary>Applies <paramref name="change"/> to <paramref name="host"/> and cross-fades from its old look (250 ms).</summary>
    public static void CrossFade(Control host, Action change) => CrossFade(host, change, TwMotion.Durations.M);

    /// <summary>
    /// Applies <paramref name="change"/> to <paramref name="host"/> (a form, a panel) and fades its old look away over the new
    /// one in <paramref name="durationMs"/>. The new look is painted before the fade starts, so the whole fade is seen
    /// however long the change takes. The picture lets clicks through and covers only the visible part of the host.
    /// When animations are off, the host is not visible or no picture can be taken, the change is simply applied.
    /// </summary>
    public static void CrossFade(Control host, Action change, int durationMs)
    {
        if (host is null) throw new ArgumentNullException(nameof(host));
        if (change is null) throw new ArgumentNullException(nameof(change));
        if (!TwMotion.CanAnimate(host) || durationMs <= 0 || host.Width <= 0 || host.Height <= 0)
        {
            _lastCovered = false;
            change();
            return;
        }

        var bounds = ScreenBounds(host);
        var visible = VisibleScreenBounds(host, bounds);
        var picture = visible.IsEmpty ? null : TwCapture.Take(host, visible);

        // Eski görüntü önce ekrana çıkar; değişiklik onun altında uygulanıp boyanır, solma ancak ondan sonra başlar.
        var cover = picture is null ? null : TwFadeOut.Cover(picture, visible, host.FindForm(), roundCorners: host is Form);
        _lastCovered = cover != null;
        try
        {
            change();
        }
        finally
        {
            if (cover != null)
            {
                TwCapture.PaintNow(host);
                cover.Fade(durationMs, TwEasing.InOut);
            }
        }
    }

    /// <summary>
    /// Shows <paramref name="panel"/> (a side panel, an inspector) sliding in from <paramref name="edge"/> (250 ms). The layout
    /// is done once: the panel is made visible at once under a picture of the old look, a picture of the panel slides into
    /// its place and the rest of the parent cross-fades to its new layout. Nothing is laid out or repainted frame by frame,
    /// so a grid beside the panel does not tear. Without animation the panel is simply shown.
    /// <code>
    /// TwTransition.SlideIn(pnlInspector, TwEdge.Right);
    /// </code>
    /// </summary>
    public static void SlideIn(Control panel, TwEdge edge) => Slide(panel, edge, true, TwMotion.Durations.M);

    /// <summary>Shows <paramref name="panel"/> sliding in from <paramref name="edge"/> in <paramref name="durationMs"/> (see <see cref="SlideIn(Control, TwEdge)"/>).</summary>
    public static void SlideIn(Control panel, TwEdge edge, int durationMs) => Slide(panel, edge, true, durationMs);

    /// <summary>
    /// Hides <paramref name="panel"/> sliding out toward <paramref name="edge"/> (250 ms); the layout is done once, as with
    /// <see cref="SlideIn(Control, TwEdge)"/>. Without animation the panel is simply hidden.
    /// </summary>
    public static void SlideOut(Control panel, TwEdge edge) => Slide(panel, edge, false, TwMotion.Durations.M);

    /// <summary>Hides <paramref name="panel"/> sliding out toward <paramref name="edge"/> in <paramref name="durationMs"/>.</summary>
    public static void SlideOut(Control panel, TwEdge edge, int durationMs) => Slide(panel, edge, false, durationMs);


    private static void Slide(Control panel, TwEdge edge, bool show, int durationMs)
    {
        if (panel is null) throw new ArgumentNullException(nameof(panel));
        var host = panel.Parent;
        _lastCovered = false;
        if (host is null || panel.Visible == show || !TwMotion.CanAnimate(host) || durationMs <= 0)
        {
            panel.Visible = show;
            return;
        }

        var hostBounds = VisibleScreenBounds(host, host.RectangleToScreen(host.ClientRectangle));
        var before = hostBounds.IsEmpty ? null : TwCapture.Take(host, hostBounds);
        if (before is null)
        {
            panel.Visible = show;
            return;
        }

        // Kapanışta panelin resmi ve yeri eski görüntüden alınır; açılışta yeri ancak yerleşimden sonra bilinir, eski
        // görüntünün tamamı arka plan için saklanır.
        var place = Rectangle.Empty;
        Bitmap? front = null, back = null;
        if (show) back = (Bitmap)before.Clone();
        else
        {
            place = Rectangle.Intersect(host.RectangleToScreen(panel.Bounds), hostBounds);
            front = Crop(before, place, hostBounds);
        }

        // Eski görüntü üstte kalırken yerleşim bir kez yapılır ve boyanır.
        var cover = TwFadeOut.Cover(before, hostBounds, host.FindForm(), roundCorners: false);
        try
        {
            panel.Visible = show;
            TwCapture.PaintNow(host);
        }
        catch
        {
            cover?.Fade(0, TwEasing.Linear);
            front?.Dispose();
            back?.Dispose();
            throw;
        }
        if (cover is null)
        {
            front?.Dispose();
            back?.Dispose();
            return;
        }
        _lastCovered = true;

        if (show)
        {
            var whole = back!;
            place = Rectangle.Intersect(host.RectangleToScreen(panel.Bounds), hostBounds);
            front = place.IsEmpty ? null : TwCapture.TakeContent(host, place, cover);
            back = front is null ? null : Crop(whole, place, hostBounds);
            whole.Dispose();
        }
        else back = place.IsEmpty ? null : TwCapture.TakeContent(host, place, cover);

        // Panelin yerinde: arka plan üzerinde kayan panel resmi; geri kalan alan yeni yerleşime solar.
        if (front != null && back != null)
        {
            var away = TwWindowMotion.Offset(edge, edge is TwEdge.Left or TwEdge.Right ? place.Width : place.Height);
            var slider = new SlideGhost(back, front, place, show ? away : Point.Empty, show ? Point.Empty : away);
            if (slider.TryShow(host.FindForm())) slider.Play(durationMs, show ? TwEasing.Out : TwEasing.In);
        }
        else
        {
            front?.Dispose();
            back?.Dispose();
        }
        cover.Fade(durationMs, TwEasing.InOut);
    }

    private static Bitmap? Crop(Bitmap picture, Rectangle part, Rectangle pictureBounds)
    {
        var rect = new Rectangle(part.X - pictureBounds.X, part.Y - pictureBounds.Y, part.Width, part.Height);
        rect.Intersect(new Rectangle(Point.Empty, picture.Size));
        return rect.Width > 0 && rect.Height > 0 ? picture.Clone(rect, picture.PixelFormat) : null;
    }

    /// <summary>A click-through window over the panel's place: the background picture with the panel's picture sliding over it.</summary>
    private sealed class SlideGhost : Form
    {
        private const int WsExNoActivate = 0x08000000, WsExToolWindow = 0x00000080, WsExTransparent = 0x00000020;
        private readonly Bitmap _back, _front;
        private readonly Point _from, _to;
        private TwTween? _tween;

        public SlideGhost(Bitmap back, Bitmap front, Rectangle bounds, Point from, Point to)
        {
            _back = back;
            _front = front;
            _from = from;
            _to = to;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WsExNoActivate | WsExToolWindow | WsExTransparent;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TwFadeOut.Ghost.DisableTransitions(Handle);
        }

        public bool TryShow(Form? owner)
        {
            try
            {
                if (owner is { IsDisposed: false, Visible: true }) Show(owner);
                else Show();
                Update();
                return true;
            }
            catch (Exception ex) when (!TwErrors.IsCritical(ex))
            {
                Dispose();
                return false;
            }
        }

        public void Play(int durationMs, TwEasing easing)
        {
            _tween = new TwTween(this);
            _tween.Completed += (_, _) => Close();
            _tween.Start(durationMs, easing);
            if (!_tween.Running) Close();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            float p = _tween?.Progress ?? 0f;
            e.Graphics.DrawImageUnscaled(_back, 0, 0);
            e.Graphics.DrawImageUnscaled(_front, (int)Math.Round(_from.X + (_to.X - _from.X) * p), (int)Math.Round(_from.Y + (_to.Y - _from.Y) * p));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tween?.Dispose();
                _back.Dispose();
                _front.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Whether the last <see cref="CrossFade(Control, Action, int)"/> showed a picture (for tests).</summary>
    [ThreadStatic]
    private static bool _lastCovered;

    internal static bool LastCovered => _lastCovered;

    // Form: pencerenin görünen tamamı (başlık çubuğu dahil, görünmez kenarlıklar hariç); alt kontrol: ebeveyn içindeki sınırları.
    private static Rectangle ScreenBounds(Control host) =>
        host is Form form && host.Parent is null ? TwCapture.VisibleFormBounds(form)
        : host.Parent is null ? host.Bounds : host.Parent.RectangleToScreen(host.Bounds);

    /// <summary>The part of <paramref name="bounds"/> not clipped away by the host's parents.</summary>
    internal static Rectangle VisibleScreenBounds(Control host, Rectangle bounds)
    {
        var visible = bounds;
        for (var parent = host.Parent; parent != null && !visible.IsEmpty; parent = parent.Parent)
            visible.Intersect(parent.RectangleToScreen(parent.ClientRectangle));
        return visible.Width > 0 && visible.Height > 0 ? visible : Rectangle.Empty;
    }
}
