using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
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
    /// <summary>Applies <paramref name="change"/> to <paramref name="host"/> and cross-fades from its old look (<see cref="TwMotion.Timings.CrossFade"/>).</summary>
    public static void CrossFade(Control host, Action change) => CrossFade(host, change, (TwMotionOptions?)null);

    /// <summary>
    /// Applies <paramref name="change"/> to <paramref name="host"/> (a form, a panel) and fades its old look away over the new
    /// one in <paramref name="durationMs"/>. The new look is painted before the fade starts, so the whole fade is seen
    /// however long the change takes. The picture lets clicks through and covers only the visible part of the host.
    /// When animations are off, the host is not visible or no picture can be taken, the change is simply applied.
    /// </summary>
    public static void CrossFade(Control host, Action change, int durationMs) =>
        CrossFade(host, change, new TwMotionOptions { DurationMs = durationMs });

    /// <summary>
    /// Applies <paramref name="change"/> to <paramref name="host"/> and cross-fades from its old look with <paramref name="options"/>
    /// (duration and curve; ease-in-out by default). See <see cref="CrossFade(Control, Action, int)"/>.
    /// </summary>
    public static void CrossFade(Control host, Action change, TwMotionOptions? options)
    {
        if (host is null) throw new ArgumentNullException(nameof(host));
        if (change is null) throw new ArgumentNullException(nameof(change));
        var timing = TwMotionOptions.Timing(options, TwMotion.Timings.CrossFade, TwEasing.InOut);
        if (!TwMotion.CanAnimate(host) || timing.TotalMs <= 0 || host.Width <= 0 || host.Height <= 0)
        {
            _lastCovered = false;
            change();
            TwMotionOptions.Complete(options);
            return;
        }

        long start = Stopwatch.GetTimestamp();
        var bounds = ScreenBounds(host);
        var visible = VisibleScreenBounds(host, bounds);
        // A fade still running over the host is in the picture taken from the screen (it shows what is seen now).
        var picture = visible.IsEmpty ? null : TwCapture.Take(host, visible);
        var source = TwCapture.LastSource;
        long captured = Stopwatch.GetTimestamp();

        // Eski görüntü önce ekrana çıkar; değişiklik onun altında uygulanıp boyanır, solma ancak ondan sonra başlar.
        var cover = picture is null ? null : TwFadeOut.Cover(picture, visible, host.FindForm(), roundCorners: host is Form);
        _lastCovered = cover != null;
        // Quick navigation: the earlier fade ends now instead of fading on above the new one.
        if (cover != null) ReplaceCover(host, cover);
        long covered = Stopwatch.GetTimestamp(), changed = covered;
        bool applied = false;
        try
        {
            change();
            applied = true;
            changed = Stopwatch.GetTimestamp();
        }
        finally
        {
            if (cover != null)
            {
                TwCapture.PaintNow(host);
                if (applied) cover.FormClosed += (_, _) => TwMotionOptions.Complete(options);
                cover.Fade(timing, Point.Empty);
            }
        }
        if (cover is null) TwMotionOptions.Complete(options);
        if (applied && TwDiagnostics.HasTransitionListeners)
        {
            long painted = Stopwatch.GetTimestamp();
            TwDiagnostics.OnTransition(new TwTransitionEventArgs(host, "CrossFade", source.ToString(),
                Ms(captured - start), Ms(covered - captured), Ms(changed - covered), cover is null ? 0 : Ms(painted - changed),
                Ms(painted - start), cover is null ? 0 : timing.TotalMs));
        }
    }

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    // The cover fading over each host now; a new cross-fade on the same host closes the old one.
    private static readonly ConditionalWeakTable<Control, TwFadeOut.Ghost> Covers = new ConditionalWeakTable<Control, TwFadeOut.Ghost>();

    private static void ReplaceCover(Control host, TwFadeOut.Ghost cover)
    {
        if (Covers.TryGetValue(host, out var old))
        {
            Covers.Remove(host);
            if (!old.IsDisposed) old.Close();
        }
        Covers.Add(host, cover);
        cover.FormClosed += (_, _) =>
        {
            if (Covers.TryGetValue(host, out var current) && ReferenceEquals(current, cover)) Covers.Remove(host);
        };
    }

    /// <summary>Whether a cross-fade picture is still over <paramref name="host"/> (for tests).</summary>
    internal static bool IsFading(Control host) => Covers.TryGetValue(host, out var cover) && !cover.IsDisposed;

    /// <summary>
    /// Shows <paramref name="panel"/> (a side panel, an inspector) sliding in from <paramref name="edge"/>
    /// (<see cref="TwMotion.Timings.Slide"/>). The layout is done once: the panel is made visible at once under a picture of
    /// the old look, a picture of the panel slides into its place and the rest of the parent cross-fades to its new layout.
    /// Nothing is laid out or repainted frame by frame, so a grid beside the panel does not tear. Without animation the panel
    /// is simply shown.
    /// <code>
    /// TwTransition.SlideIn(pnlInspector, TwEdge.Right);
    /// </code>
    /// </summary>
    public static void SlideIn(Control panel, TwEdge edge) => Slide(panel, edge, true, null);

    /// <summary>Shows <paramref name="panel"/> sliding in from <paramref name="edge"/> in <paramref name="durationMs"/> (see <see cref="SlideIn(Control, TwEdge)"/>).</summary>
    public static void SlideIn(Control panel, TwEdge edge, int durationMs) =>
        Slide(panel, edge, true, new TwMotionOptions { DurationMs = durationMs });

    /// <summary>
    /// Shows <paramref name="panel"/> sliding in from <paramref name="edge"/> with <paramref name="options"/>: duration and curve
    /// (ease-out by default), <see cref="TwMotionOptions.IncludeMargin"/> to slide the gap beside the panel with it and
    /// <see cref="TwMotionOptions.FadeRest"/> to put the rest of the parent in its new layout at once.
    /// </summary>
    public static void SlideIn(Control panel, TwEdge edge, TwMotionOptions? options) => Slide(panel, edge, true, options);

    /// <summary>
    /// Hides <paramref name="panel"/> sliding out toward <paramref name="edge"/> (<see cref="TwMotion.Timings.Slide"/>); the
    /// layout is done once, as with <see cref="SlideIn(Control, TwEdge)"/>. Without animation the panel is simply hidden.
    /// </summary>
    public static void SlideOut(Control panel, TwEdge edge) => Slide(panel, edge, false, null);

    /// <summary>Hides <paramref name="panel"/> sliding out toward <paramref name="edge"/> in <paramref name="durationMs"/>.</summary>
    public static void SlideOut(Control panel, TwEdge edge, int durationMs) =>
        Slide(panel, edge, false, new TwMotionOptions { DurationMs = durationMs });

    /// <summary>
    /// Hides <paramref name="panel"/> sliding out toward <paramref name="edge"/> with <paramref name="options"/> (ease-in by
    /// default; see <see cref="SlideIn(Control, TwEdge, TwMotionOptions)"/>).
    /// </summary>
    public static void SlideOut(Control panel, TwEdge edge, TwMotionOptions? options) => Slide(panel, edge, false, options);

    private static void Slide(Control panel, TwEdge edge, bool show, TwMotionOptions? options)
    {
        if (panel is null) throw new ArgumentNullException(nameof(panel));
        var host = panel.Parent;
        _lastCovered = false;
        var timing = TwMotionOptions.Timing(options, TwMotion.Timings.Slide, show ? TwEasing.Out : TwEasing.In);
        bool fadeRest = options?.FadeRest ?? true, margin = options?.IncludeMargin ?? false;
        if (host is null || panel.Visible == show || !TwMotion.CanAnimate(host) || timing.TotalMs <= 0)
        {
            panel.Visible = show;
            TwMotionOptions.Complete(options);
            return;
        }

        var hostBounds = VisibleScreenBounds(host, host.RectangleToScreen(host.ClientRectangle));
        var before = hostBounds.IsEmpty ? null : TwCapture.Take(host, hostBounds);
        if (before is null)
        {
            panel.Visible = show;
            TwMotionOptions.Complete(options);
            return;
        }

        // Kapanışta panelin resmi ve yeri eski görüntüden alınır; açılışta yeri ancak yerleşimden sonra bilinir, eski
        // görüntünün tamamı arka plan için saklanır.
        var place = Rectangle.Empty;
        Bitmap? front = null, back = null;
        if (show) back = (Bitmap)before.Clone();
        else
        {
            place = Rectangle.Intersect(host.RectangleToScreen(Place(panel, margin)), hostBounds);
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
            TwMotionOptions.Complete(options);
            return;
        }
        _lastCovered = true;
        var done = TwMotionOptions.Latch(options, 2);   // örtü + kayan resim
        cover.FormClosed += (_, _) => done();

        if (show)
        {
            var whole = back!;
            place = Rectangle.Intersect(host.RectangleToScreen(Place(panel, margin)), hostBounds);
            front = place.IsEmpty ? null : TwCapture.TakeContent(host, place, cover);
            // Kalan alan solmadan yeni yerleşimine geçince panelin yerindeki eski içerik (tablo sütunları) görünmesin.
            back = front is null ? null : fadeRest ? Crop(whole, place, hostBounds) : Fill(place.Size, host.BackColor);
            whole.Dispose();
        }
        else back = place.IsEmpty ? null : TwCapture.TakeContent(host, place, cover);

        // Panelin yerinde: arka plan üzerinde kayan panel resmi; geri kalan alan yeni yerleşime solar ya da hemen açılır.
        if (front != null && back != null)
        {
            var away = TwWindowMotion.Offset(edge, edge is TwEdge.Left or TwEdge.Right ? place.Width : place.Height);
            var slider = new SlideGhost(back, front, place, show ? away : Point.Empty, show ? Point.Empty : away);
            if (slider.TryShow(host.FindForm()))
            {
                slider.FormClosed += (_, _) => done();
                slider.Play(timing);
            }
            else done();
        }
        else
        {
            front?.Dispose();
            back?.Dispose();
            done();
        }
        cover.Fade(RestTiming(timing, fadeRest), Point.Empty);
    }

    /// <summary>
    /// Changes the size of a docked panel (a sidebar collapsing to icons, an inspector widening) to <paramref name="size"/>
    /// (<see cref="TwMotion.Timings.Slide"/>): the width for a panel docked left, right or not docked, the height for one
    /// docked top or bottom. As with <see cref="SlideIn(Control, TwEdge)"/> the layout is done once: the new size is applied
    /// under a picture of the old look, the picture of the panel at its larger size opens or closes from its docked edge and
    /// the rest cross-fades to its new layout. Without animation the size is simply set.
    /// <code>
    /// TwTransition.Resize(sidebar, collapsed ? 56 : 240);
    /// </code>
    /// </summary>
    public static void Resize(Control panel, int size) => Resize(panel, size, null);

    /// <summary>
    /// Changes the size of a docked panel with <paramref name="options"/> (duration, curve — ease-in-out by default —
    /// and <see cref="TwMotionOptions.FadeRest"/>); see <see cref="Resize(Control, int)"/>.
    /// </summary>
    public static void Resize(Control panel, int size, TwMotionOptions? options)
    {
        if (panel is null) throw new ArgumentNullException(nameof(panel));
        if (size < 0) throw new ArgumentOutOfRangeException(nameof(size), size, "A size cannot be negative.");
        bool vertical = panel.Dock is DockStyle.Top or DockStyle.Bottom;
        _lastCovered = false;
        _lastRevealed = false;
        _lastRevealFadedBack = false;
        if ((vertical ? panel.Height : panel.Width) == size)
        {
            TwMotionOptions.Complete(options);
            return;
        }
        Resize(panel, () =>
        {
            if (vertical) panel.Height = size;
            else panel.Width = size;
        }, options);
    }

    /// <summary>
    /// Applies <paramref name="change"/>, which changes the size of a docked panel and anything else that goes with it (labels
    /// hidden in a collapsed sidebar), under a picture of the old look and animates the panel to its new size (see
    /// <see cref="Resize(Control, int)"/>).
    /// <code>
    /// TwTransition.Resize(sidebar, () =>
    /// {
    ///     foreach (var label in navLabels) label.Visible = !collapsed;
    ///     sidebar.Width = collapsed ? 56 : 240;
    /// });
    /// </code>
    /// </summary>
    public static void Resize(Control panel, Action change) => Resize(panel, change, null);

    /// <summary>Applies <paramref name="change"/> and animates the docked panel to its new size with <paramref name="options"/> (see <see cref="Resize(Control, Action)"/>).</summary>
    public static void Resize(Control panel, Action change, TwMotionOptions? options)
    {
        if (panel is null) throw new ArgumentNullException(nameof(panel));
        if (change is null) throw new ArgumentNullException(nameof(change));
        bool vertical = panel.Dock is DockStyle.Top or DockStyle.Bottom, far = panel.Dock is DockStyle.Right or DockStyle.Bottom;

        var host = panel.Parent;
        _lastCovered = false;
        _lastRevealed = false;
        _lastRevealFadedBack = false;
        var timing = TwMotionOptions.Timing(options, TwMotion.Timings.Slide, TwEasing.InOut);
        if (host is null || !TwMotion.CanAnimate(host) || timing.TotalMs <= 0)
        {
            change();
            TwMotionOptions.Complete(options);
            return;
        }

        // Örtü yalnızca ebeveyni değil formun istemci alanını kaplar: change, kontrolleri ebeveynin dışına (forma) taşıyabilir.
        var root = host.FindForm() ?? host;
        var coverBounds = VisibleScreenBounds(root, root.RectangleToScreen(root.ClientRectangle));
        var before = coverBounds.IsEmpty ? null : TwCapture.Take(root, coverBounds);
        if (before is null)
        {
            change();
            TwMotionOptions.Complete(options);
            return;
        }
        bool wasShown = panel.Visible;
        var oldPlace = wasShown ? host.RectangleToScreen(panel.Bounds) : Rectangle.Empty;
        var cover = TwFadeOut.Cover((Bitmap)before.Clone(), coverBounds, host.FindForm(), roundCorners: false);
        bool isShown;
        Rectangle newPlace;
        try
        {
            change();
            // Sonradan gösterilen ya da panele taşınan çocukların tutamacı olmayabilir; boyanmamış kalırlarsa resimde arkası görünür.
            isShown = panel.Visible;
            if (isShown) CreateHandles(panel);
            newPlace = isShown ? host.RectangleToScreen(panel.Bounds) : Rectangle.Empty;
            if (isShown) TwCapture.RepaintNow(panel);
            TwCapture.PaintNow(root);
        }
        catch
        {
            cover?.Fade(0, TwEasing.Linear);
            before.Dispose();
            throw;
        }
        if (cover is null)
        {
            before.Dispose();
            TwMotionOptions.Complete(options);
            return;
        }
        _lastCovered = true;

        // Gizli bir panel, yapıştığı kenarda kalınlığı sıfır bir şerit sayılır: açılış ve kapanış aynı yoldan oynar.
        bool animated = wasShown || isShown;
        if (!wasShown && isShown) oldPlace = Flat(newPlace, vertical, far);
        else if (wasShown && !isShown) newPlace = Flat(oldPlace, vertical, far);

        var done = TwMotionOptions.Latch(options, 2);   // örtü + açılan resim
        cover.FormClosed += (_, _) => done();
        // Yeri değişmeyen (ya da hep gizli kalan) bir değişiklik yalnızca çapraz solar.
        if (!animated || newPlace == oldPlace)
        {
            before.Dispose();
            done();
            cover.Fade(timing, Point.Empty);
            return;
        }

        // Panelin büyük hâlinin resmi, küçük hâlin görüntüsü üstünde yapıştığı kenardan açılır ya da kapanır.
        var place = Rectangle.Intersect(Rectangle.Union(oldPlace, newPlace), coverBounds);
        bool grows = (vertical ? newPlace.Height : newPlace.Width) > (vertical ? oldPlace.Height : oldPlace.Width);
        var after = place.Width <= 0 || place.Height <= 0 ? null : TwCapture.TakeContent(root, place, cover);
        var oldPart = after is null ? null : Crop(before, place, coverBounds);
        before.Dispose();
        var wide = Rectangle.Intersect(grows ? newPlace : oldPlace, place);
        var large = grows ? after : oldPart;
        var front = large is null || wide.Width <= 0 || wide.Height <= 0 ? null : Crop(large, wide, place);
        // Büyürken panelin henüz örtmediği şeritte: kalan alan solarken eski içerik zemin rengi üstünde solarak kaybolur
        // (bir anda silinip boş bir alan bırakmaz). Kalan alan solmuyorsa (FadeRest = false) yeni yerine geçmiş yazılar iki
        // kez görünmesin diye eski içerik hiç kalmaz: zemin rengi üstünde panelin eski resmi. Küçülürken arka plan zaten
        // yeni yerleşimdir.
        bool fadeBack = grows && (options?.FadeRest ?? true);
        var back = !grows ? after : fadeBack ? oldPart : Underlay(oldPart, Rectangle.Intersect(oldPlace, place), place, host.BackColor);
        if (grows) after?.Dispose();
        if (!fadeBack) oldPart?.Dispose();
        if (front != null && back != null)
        {
            var reveal = new RevealGhost(back, front, place, new Point(wide.X - place.X, wide.Y - place.Y), vertical, far,
                vertical ? oldPlace.Height : oldPlace.Width, vertical ? newPlace.Height : newPlace.Width,
                fadeBack ? host.BackColor : (Color?)null);
            if (reveal.TryShow(host.FindForm()))
            {
                _lastRevealed = true;
                _lastRevealFadedBack = fadeBack;
                reveal.FormClosed += (_, _) => done();
                reveal.Play(timing);
            }
            else done();
        }
        else
        {
            front?.Dispose();
            back?.Dispose();
            done();
        }
        cover.Fade(RestTiming(timing, options?.FadeRest ?? true), Point.Empty);
    }

    // Yapıştığı kenarda kalınlığı sıfır olan şerit (gizli panelin yeri).
    private static Rectangle Flat(Rectangle place, bool vertical, bool far) =>
        vertical ? new Rectangle(place.X, far ? place.Bottom : place.Y, place.Width, 0)
                 : new Rectangle(far ? place.Right : place.X, place.Y, 0, place.Height);

    // Görünür alt kontrollerin tutamaçlarını oluşturur (boyanabilsinler diye); görünmeyenlere dokunmaz.
    private static void CreateHandles(Control parent)
    {
        if (!parent.IsHandleCreated) _ = parent.Handle;
        foreach (Control child in parent.Controls)
            if (child.Visible) CreateHandles(child);
    }

    // Zemin rengiyle dolu bir resim; üstünde yalnızca panelin eski yeri (part) eski resimden alınır.
    private static Bitmap? Underlay(Bitmap? picture, Rectangle part, Rectangle pictureBounds, Color color)
    {
        if (picture is null) return null;
        var under = Fill(picture.Size, color);
        if (part.Width <= 0 || part.Height <= 0) return under;
        var rect = new Rectangle(part.X - pictureBounds.X, part.Y - pictureBounds.Y, part.Width, part.Height);
        using (var g = Graphics.FromImage(under)) g.DrawImage(picture, rect, rect, GraphicsUnit.Pixel);
        return under;
    }

    // Kalan alan ya ana geçişle aynı sürede solar ya da kayan resim ekrana çıktıktan sonra hemen açılır.
    private static TwTiming RestTiming(TwTiming timing, bool fade) =>
        fade ? TwTiming.Exact(timing.DurationMs, 0, TwEasing.InOut) : TwTiming.None;

    // Panelin yeri; istenirse kenar payıyla birlikte (Margin'in bıraktığı boşluk da panelle kayar).
    private static Rectangle Place(Control panel, bool margin)
    {
        var bounds = panel.Bounds;
        if (!margin) return bounds;
        var m = panel.Margin;
        return Rectangle.FromLTRB(bounds.Left - m.Left, bounds.Top - m.Top, bounds.Right + m.Right, bounds.Bottom + m.Bottom);
    }

    private static Bitmap Fill(Size size, Color color)
    {
        var picture = new Bitmap(size.Width, size.Height);
        using var g = Graphics.FromImage(picture);
        g.Clear(Color.FromArgb(255, color));
        return picture;
    }

    private static Bitmap? Crop(Bitmap picture, Rectangle part, Rectangle pictureBounds)
    {
        var rect = new Rectangle(part.X - pictureBounds.X, part.Y - pictureBounds.Y, part.Width, part.Height);
        rect.Intersect(new Rectangle(Point.Empty, picture.Size));
        return rect.Width > 0 && rect.Height > 0 ? picture.Clone(rect, picture.PixelFormat) : null;
    }

    /// <summary>A click-through window over a panel's place that draws a background picture and a moving picture over it.</summary>
    private abstract class PictureGhost : Form
    {
        private const int WsExNoActivate = 0x08000000, WsExToolWindow = 0x00000080, WsExTransparent = 0x00000020;
        protected readonly Bitmap Back, Front;
        private TwTween? _tween;

        protected PictureGhost(Bitmap back, Bitmap front, Rectangle bounds)
        {
            Back = back;
            Front = front;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected float Progress => _tween?.Progress ?? 0f;

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

        public void Play(TwTiming timing)
        {
            _tween = new TwTween(this);
            _tween.Completed += (_, _) => Close();
            _tween.Start(timing);
            if (!_tween.Running) Close();
        }


        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tween?.Dispose();
                Back.Dispose();
                Front.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>The panel's picture sliding over the background picture.</summary>
    private sealed class SlideGhost : PictureGhost
    {
        private readonly Point _from, _to;

        public SlideGhost(Bitmap back, Bitmap front, Rectangle bounds, Point from, Point to) : base(back, front, bounds)
        {
            _from = from;
            _to = to;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            float p = Progress;
            e.Graphics.DrawImageUnscaled(Back, 0, 0);
            e.Graphics.DrawImageUnscaled(Front, (int)Math.Round(_from.X + (_to.X - _from.X) * p), (int)Math.Round(_from.Y + (_to.Y - _from.Y) * p));
        }
    }

    /// <summary>The picture of the panel at its larger size, shown from its docked edge up to the size reached so far.</summary>
    private sealed class RevealGhost : PictureGhost
    {
        private readonly Point _at;
        private readonly bool _vertical, _far;
        private readonly int _from, _to;

        private readonly Color? _fadeOn;

        // fadeOn: arka plan resmi (panelin yerinin eski içeriği) bu zemin rengi üstünde ilerledikçe solar; null = arka plan
        // resmi olduğu gibi kalır.
        public RevealGhost(Bitmap back, Bitmap front, Rectangle bounds, Point at, bool vertical, bool far, int from, int to, Color? fadeOn)
            : base(back, front, bounds)
        {
            _at = at;
            _vertical = vertical;
            _far = far;
            _from = from;
            _to = to;
            _fadeOn = fadeOn;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            float p = Progress;
            if (_fadeOn is Color color && p > 0f)
            {
                e.Graphics.Clear(Color.FromArgb(255, color));
                using var attributes = new System.Drawing.Imaging.ImageAttributes();
                attributes.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = Math.Max(0f, 1f - p) });
                e.Graphics.DrawImage(Back, new Rectangle(Point.Empty, Back.Size), 0, 0, Back.Width, Back.Height, GraphicsUnit.Pixel, attributes);
            }
            else e.Graphics.DrawImageUnscaled(Back, 0, 0);
            int length = _vertical ? Front.Height : Front.Width;
            int shown = Math.Max(0, Math.Min(length, (int)Math.Round(_from + (_to - _from) * p)));
            var clip = _vertical
                ? new Rectangle(_at.X, _far ? _at.Y + length - shown : _at.Y, Front.Width, shown)
                : new Rectangle(_far ? _at.X + length - shown : _at.X, _at.Y, shown, Front.Height);
            e.Graphics.SetClip(clip);
            e.Graphics.DrawImageUnscaled(Front, _at.X, _at.Y);
        }
    }

    /// <summary>Whether the last <see cref="CrossFade(Control, Action, int)"/> showed a picture (for tests).</summary>
    [ThreadStatic]
    private static bool _lastCovered;

    internal static bool LastCovered => _lastCovered;

    /// <summary>Whether the last <see cref="Resize(Control, Action)"/> showed the panel opening or closing, not only a cross-fade (for tests).</summary>
    [ThreadStatic]
    private static bool _lastRevealed;

    internal static bool LastRevealed => _lastRevealed;

    /// <summary>Whether the last growing reveal faded the old content of the panel's place out (for tests).</summary>
    [ThreadStatic]
    private static bool _lastRevealFadedBack;

    internal static bool LastRevealFadedBack => _lastRevealFadedBack;

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
