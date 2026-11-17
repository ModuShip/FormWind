using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Fade-out of a closing window (dialog, popover). The window itself closes at once (so <c>Close</c>, <c>DialogResult</c>
/// and <c>IsOpen</c> behave as without animation); its last picture is copied from the screen into a click-through,
/// non-activating window at the same place, which fades out and disappears. Nothing happens when animations are off, the
/// window is not on a screen or the screen cannot be read.
/// </summary>
public static class TwFadeOut
{
    /// <summary>Number of fading pictures on this thread (for tests).</summary>
    [ThreadStatic]
    private static int _active;

    internal static int ActiveCount => _active;

    /// <param name="window">The window about to close; it must still be visible.</param>
    /// <param name="owner">Window the picture stays above (the dialog's owner); <c>null</c> = none.</param>
    /// <param name="durationMs">Fade time (ms).</param>
    /// <param name="roundCorners">Ask Windows 11 for rounded corners, as the closing window had.</param>
    public static void Play(Control window, Form? owner, int durationMs, bool roundCorners)
    {
        if (!TwMotion.CanAnimate(window) || durationMs <= 0) return;
        var bounds = window is Form form ? TwCapture.VisibleFormBounds(form) : window.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0 || !Screen.AllScreens.Any(s => s.Bounds.IntersectsWith(bounds))) return;

        // Ekranda görünen hâli; başka bir pencerenin altında kalıyorsa pencere yöneticisinden alınır.
        var picture = TwCapture.Take(window, bounds);
        if (picture is null) return;
        PlayPicture(picture, bounds, owner, durationMs, roundCorners, TwEasing.In);
    }

    /// <summary>
    /// Shows <paramref name="picture"/> (which it then owns) in a click-through window at <paramref name="bounds"/> (screen
    /// coordinates) and fades it out; <c>false</c> when the window cannot be shown (the picture is then disposed).
    /// </summary>
    public static bool PlayPicture(Bitmap picture, Rectangle bounds, Form? owner, int durationMs, bool roundCorners, TwEasing easing)
    {
        var ghost = Cover(picture, bounds, owner, roundCorners);
        ghost?.Fade(durationMs, easing);
        return ghost != null;
    }

    /// <summary>
    /// Shows <paramref name="picture"/> (which it then owns) in a click-through window at <paramref name="bounds"/> and
    /// leaves it there until <see cref="Ghost.Fade(int, TwEasing)"/>; <c>null</c> when the window cannot be shown (the picture is then
    /// disposed). The picture is on screen when this returns.
    /// </summary>
    internal static Ghost? Cover(Bitmap picture, Rectangle bounds, Form? owner, bool roundCorners)
    {
        bool owned = owner is { IsDisposed: false, Visible: true };
        // Page transitions cover the same form again and again: its idle picture window is shown again instead of
        // creating a new layered window (handle, DWM attributes) for every transition.
        var ghost = owned ? Ghost.TakeIdle(owner!) : null;
        try
        {
            if (ghost != null)
            {
                ghost.Reuse(picture, bounds, roundCorners);
                ghost.Visible = true;
            }
            else
            {
                ghost = new Ghost(picture, bounds, roundCorners, owned ? owner : null);
                if (owned) ghost.Show(owner);
                else ghost.Show();
            }
        }
        catch (Exception ex) when (!TwErrors.IsCritical(ex))
        {
            TwErrors.Trace("transition cover", ex);
            if (ghost is null) picture.Dispose();
            else ghost.Dispose();
            return null;
        }
        // Görüntü hemen ekrana çıkar; altındaki değişiklik ancak ondan sonra çizilir.
        ghost.Update();
        // Pencere yöneticisi birleştirmesini bekle: aksi halde alttaki değişiklik örtüden önce ekrana ulaşabilir.
        TwCapture.Flush();
        return ghost;
    }

    /// <summary>
    /// A click-through window showing a picture until it fades out. A window owned by a form is hidden at the end and
    /// kept for the form's next cover; <see cref="Finished"/> tells that one use is over.
    /// </summary>
    internal sealed class Ghost : Form
    {
        private const int WsExNoActivate = 0x08000000, WsExToolWindow = 0x00000080, WsExTransparent = 0x00000020;
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, Ghost> Idle =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Form, Ghost>();

        private readonly Form? _poolOwner;
        private Bitmap? _picture;
        private bool _roundCorners;
        private TwTween? _tween;
        private bool _inUse = true;

        public Ghost(Bitmap picture, Rectangle bounds, bool roundCorners, Form? poolOwner)
        {
            _picture = picture;
            _roundCorners = roundCorners;
            _poolOwner = poolOwner;
            // Kullanım başladığı yerde sayılır: Show başarısız olup pencere atılınca EndUse sayacı dengeler.
            _active++;
            if (poolOwner != null) poolOwner.FormClosed += OnPoolOwnerClosed;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            // Katmanlı pencere gösterilmeden önce kurulur; aksi halde ilk karede opak bir kare yanıp söner.
            Opacity = 0.99;
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

        /// <summary>Raised once when this use of the picture is over: the fade ended, or <see cref="Finish"/> or <c>Close</c> ended it.</summary>
        public event EventHandler? Finished;

        /// <summary>Whether the picture is on screen for a cover (between <see cref="Cover"/> and <see cref="Finished"/>).</summary>
        public bool InUse => _inUse && !IsDisposed;

        /// <summary>The idle picture window of <paramref name="owner"/>, taken out of the pool; <c>null</c> when it has none.</summary>
        internal static Ghost? TakeIdle(Form owner)
        {
            if (!Idle.TryGetValue(owner, out var ghost)) return null;
            Idle.Remove(owner);
            return ghost.IsDisposed || !ghost.IsHandleCreated ? null : ghost;
        }

        /// <summary>Whether <paramref name="owner"/> keeps an idle picture window for its next cover (for tests).</summary>
        internal static bool HasIdle(Form owner) => Idle.TryGetValue(owner, out var ghost) && !ghost.IsDisposed;

        // Hazırda bekleyen pencere yeni resim ve yerle yeniden kullanılır; katmanlı pencere görünmeden önce kurulur.
        internal void Reuse(Bitmap picture, Rectangle bounds, bool roundCorners)
        {
            _picture?.Dispose();
            _picture = picture;
            if (!_inUse) _active++;
            _inUse = true;
            Opacity = 0.99;
            Bounds = bounds;
            if (roundCorners != _roundCorners)
            {
                _roundCorners = roundCorners;
                ApplyCorners();
            }
        }

        /// <summary>Ends this use now (a new transition replaces it): the window is hidden for the next cover or closed.</summary>
        public void Finish()
        {
            if (!_inUse || IsDisposed) return;
            _tween?.Dispose();
            _tween = null;
            if (_poolOwner is { IsDisposed: false, Visible: true } owner && !Idle.TryGetValue(owner, out _))
            {
                Visible = false;
                _picture?.Dispose();
                _picture = null;
                EndUse();
                Idle.Add(owner, this);
            }
            else Close();
        }

        private void EndUse()
        {
            if (!_inUse) return;
            _inUse = false;
            _active--;
            Finished?.Invoke(this, EventArgs.Empty);
            // Each use has its own listeners: a reused window must not call the previous transition's handlers.
            Finished = null;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            EndUse();
        }

        private void OnPoolOwnerClosed(object? sender, FormClosedEventArgs e)
        {
            if (sender is Form owner) Idle.Remove(owner);
            if (!IsDisposed && !_inUse) Dispose();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DisableTransitions(Handle);
            if (_roundCorners) ApplyCorners();
        }

        private void ApplyCorners()
        {
            if (!IsHandleCreated) return;
            int corners = _roundCorners ? 2 : 1;   // DWMWCP_ROUND : DWMWCP_DONOTROUND
            try { DwmSetWindowAttribute(Handle, 33, ref corners, sizeof(int)); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        /// <summary>
        /// Turns off Windows' own show and hide animation of a picture window: it would linger on screen after closing
        /// (and come out in the next picture taken from the screen).
        /// </summary>
        internal static void DisableTransitions(IntPtr handle)
        {
            int disabled = 1;
            try { DwmSetWindowAttribute(handle, 3, ref disabled, sizeof(int)); }   // DWMWA_TRANSITIONS_FORCEDISABLED
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_picture != null) e.Graphics.DrawImageUnscaled(_picture, 0, 0);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        /// <summary>Fades the picture out from now on and closes the window at the end.</summary>
        public void Fade(int durationMs, TwEasing easing) => Fade(durationMs, easing, Point.Empty);

        /// <summary>Fades the picture out while moving it by <paramref name="offset"/>, and closes the window at the end.</summary>
        public void Fade(int durationMs, TwEasing easing, Point offset) => Fade(new TwTiming(durationMs, 0, easing), offset);

        /// <summary>Fades the picture out with <paramref name="timing"/> while moving it by <paramref name="offset"/>, and closes the window at the end.</summary>
        public void Fade(TwTiming timing, Point offset)
        {
            if (IsDisposed || !_inUse) return;
            var start = Location;
            _tween?.Dispose();
            var tween = new TwTween(this);
            _tween = tween;
            tween.Tick += (_, _) =>
            {
                float p = tween.Progress;
                Opacity = Math.Max(0, 0.99 * (1 - p));
                if (!offset.IsEmpty) Location = new Point(start.X + (int)Math.Round(offset.X * p), start.Y + (int)Math.Round(offset.Y * p));
            };
            tween.Completed += (_, _) =>
            {
                if (ReferenceEquals(tween, _tween)) Finish();
            };
            tween.Start(timing);
            if (!tween.Running) Finish();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_poolOwner != null)
                {
                    _poolOwner.FormClosed -= OnPoolOwnerClosed;
                    if (Idle.TryGetValue(_poolOwner, out var idle) && ReferenceEquals(idle, this)) Idle.Remove(_poolOwner);
                }
                _tween?.Dispose();
                _picture?.Dispose();
                EndUse();
            }
            base.Dispose(disposing);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
