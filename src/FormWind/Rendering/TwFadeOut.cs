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
internal static class TwFadeOut
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
        var bounds = window.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0 || !Screen.AllScreens.Any(s => s.Bounds.IntersectsWith(bounds))) return;

        Bitmap picture;
        try
        {
            picture = new Bitmap(bounds.Width, bounds.Height);
            using var graphics = Graphics.FromImage(picture);
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        }
        catch (Exception ex) when (ex is Win32Exception or ArgumentException or ExternalException)
        {
            return; // kilitli ekran, uzak masaüstü bağlantısı kesik …
        }

        var ghost = new Ghost(picture, bounds, roundCorners);
        try
        {
            if (owner is { IsDisposed: false, Visible: true }) ghost.Show(owner);
            else ghost.Show();
        }
        catch (Exception ex) when (!TwErrors.IsCritical(ex))
        {
            ghost.Dispose();
            return;
        }
        _active++;
        ghost.FormClosed += (_, _) => _active--;
        ghost.Fade(durationMs);
    }

    private sealed class Ghost : Form
    {
        private const int WsExNoActivate = 0x08000000, WsExToolWindow = 0x00000080, WsExTransparent = 0x00000020;
        private readonly Bitmap _picture;
        private readonly bool _roundCorners;
        private TwTween? _tween;

        public Ghost(Bitmap picture, Rectangle bounds, bool roundCorners)
        {
            _picture = picture;
            _roundCorners = roundCorners;
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

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!_roundCorners) return;
            int round = 2;
            try { DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int)); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        protected override void OnPaint(PaintEventArgs e) => e.Graphics.DrawImageUnscaled(_picture, 0, 0);

        protected override void OnPaintBackground(PaintEventArgs e) { }

        public void Fade(int durationMs)
        {
            _tween = new TwTween(this);
            _tween.Tick += (_, _) => Opacity = Math.Max(0, 0.99 * (1 - _tween.Progress));
            _tween.Completed += (_, _) => Close();
            _tween.Start(durationMs, TwEasing.In);
            if (!_tween.Running) Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tween?.Dispose();
                _picture.Dispose();
            }
            base.Dispose(disposing);
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
