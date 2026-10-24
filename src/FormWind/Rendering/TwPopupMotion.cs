using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Opening and closing of popups (<see cref="TwPopover"/>, menus drawn by FormWind): they grow from 95% to full size and fade
/// in (<see cref="OpenDuration"/> ms) from the side they open from, and fade out when they close (<see cref="CloseDuration"/> ms).
/// A window's content cannot be scaled, so while opening the popup stays invisible (it already takes clicks and keys) and
/// a click-through picture of it grows in its place; when no picture can be taken it only fades in. Nothing moves when
/// animations are off.
/// </summary>
internal static class TwPopupMotion
{
    /// <summary>Opening time (ms).</summary>
    internal const int OpenDuration = 140;

    /// <summary>Closing fade time (ms).</summary>
    internal const int CloseDuration = 120;

    /// <summary>Size of the popup when its opening starts.</summary>
    internal const float StartScale = 0.95f;

    private static readonly ConditionalWeakTable<ToolStripDropDown, Opening> Openings = new ConditionalWeakTable<ToolStripDropDown, Opening>();

    [ThreadStatic]
    private static int _active;

    /// <summary>Number of opening pictures on this thread (for tests).</summary>
    internal static int ActiveCount => _active;

    /// <summary>Animates the openings and closings of a menu from now on (once per drop-down).</summary>
    public static void Attach(ToolStripDropDown dropDown)
    {
        if (Openings.TryGetValue(dropDown, out _)) return;
        Openings.Add(dropDown, new Opening());
        // Katmanlı pencere gösterilmeden önce görünmez kurulur; açılınca görüntüsü büyüyerek belirir.
        dropDown.Opening += (_, e) => { if (!e.Cancel) PrepareOpen(dropDown); };
        dropDown.Opened += (_, _) => Open(dropDown, MenuOrigin(dropDown));
        dropDown.Closing += (_, e) => { if (!e.Cancel) Close(dropDown); };
    }

    /// <summary>Call before showing a popup that <see cref="Open"/> will animate.</summary>
    public static void PrepareOpen(ToolStripDropDown dropDown)
    {
        if (TwMotion.Enabled) dropDown.Opacity = 0;
    }

    /// <summary>
    /// Plays the opening of a shown popup, growing from <paramref name="origin"/> (screen coordinates, e.g. the anchor's
    /// side); it is fully visible at once when it cannot animate.
    /// </summary>
    public static void Open(ToolStripDropDown dropDown, Point origin)
    {
        var opening = Openings.GetValue(dropDown, _ => new Opening());
        opening.Stop();
        if (!TwMotion.CanAnimate(dropDown) || dropDown.Width <= 0 || dropDown.Height <= 0)
        {
            dropDown.Opacity = 1;
            return;
        }
        opening.Start(dropDown, origin);
    }

    /// <summary>Fades the last picture of a closing popup out; the popup itself closes at once.</summary>
    public static void Close(ToolStripDropDown dropDown)
    {
        if (Openings.TryGetValue(dropDown, out var opening)) opening.Stop();
        if (dropDown.Visible) TwFadeOut.Play(dropDown, null, CloseDuration, roundCorners: false);
    }

    /// <summary>Whether the opening of <paramref name="dropDown"/> is running.</summary>
    internal static bool IsOpening(ToolStripDropDown dropDown) => Openings.TryGetValue(dropDown, out var opening) && opening.Running;

    // Alt menü sahibinin yanından, bağlam menüsü imlecin olduğu köşeden büyür.
    private static Point MenuOrigin(ToolStripDropDown dropDown)
    {
        if (dropDown.OwnerItem is { } owner && owner.Owner is { IsHandleCreated: true } strip)
        {
            var item = strip.RectangleToScreen(owner.Bounds);
            return new Point(item.Left + item.Width / 2, item.Top + item.Height / 2);
        }
        return Control.MousePosition;
    }

    /// <summary>The point of <paramref name="bounds"/> nearest to <paramref name="origin"/>.</summary>
    internal static Point Clamp(Point origin, Rectangle bounds) => new Point(
        Math.Max(bounds.Left, Math.Min(bounds.Right, origin.X)), Math.Max(bounds.Top, Math.Min(bounds.Bottom, origin.Y)));

    private sealed class Opening
    {
        private ToolStripDropDown? _dropDown;
        private Ghost? _ghost;
        private TwTween? _tween;

        public bool Running => _tween is { Running: true };

        public void Start(ToolStripDropDown dropDown, Point origin)
        {
            _dropDown = dropDown;
            _ghost = Ghost.TryCreate(dropDown, Clamp(origin, dropDown.Bounds));
            if (_ghost != null) _active++;
            _tween = new TwTween(dropDown);
            _tween.Tick += (_, _) => Step();
            _tween.Completed += (_, _) => Stop();
            _tween.Start(OpenDuration, TwEasing.Out);
            Step();
            if (!_tween.Running) Stop();
        }

        private void Step()
        {
            if (_dropDown is null || _tween is null) return;
            float p = _tween.Progress;
            // Görüntü yoksa yalnızca solarak belirir.
            if (_ghost is null) _dropDown.Opacity = p;
            else _ghost.Draw(StartScale + (1 - StartScale) * p, p);
        }

        public void Stop()
        {
            if (_dropDown is { IsDisposed: false } dropDown) dropDown.Opacity = 1;
            if (_ghost != null)
            {
                _ghost.Close();
                _ghost.Dispose();
                _ghost = null;
                _active--;
            }
            _tween?.Dispose();
            _tween = null;
            _dropDown = null;
        }
    }

    /// <summary>A click-through, per-pixel transparent window that draws the popup's picture scaled about an origin.</summary>
    private sealed class Ghost : Form
    {
        private const int WsExLayered = 0x00080000, WsExTransparent = 0x00000020, WsExNoActivate = 0x08000000,
            WsExToolWindow = 0x00000080, WsExTopMost = 0x00000008;

        private readonly Bitmap _picture;
        private readonly PointF _origin;

        private Ghost(Bitmap picture, Rectangle bounds, Point origin)
        {
            _picture = picture;
            _origin = new PointF(origin.X - bounds.X, origin.Y - bounds.Y);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
        }

        public static Ghost? TryCreate(ToolStripDropDown dropDown, Point origin)
        {
            Bitmap picture;
            try
            {
                // Alfasız: GDI'nin çizdiği zemin piksellerinin alfası 0 kalır; açılır pencere zaten opak bir dikdörtgendir.
                picture = new Bitmap(dropDown.Width, dropDown.Height, PixelFormat.Format32bppRgb);
                dropDown.DrawToBitmap(picture, new Rectangle(Point.Empty, dropDown.Size));
            }
            catch (Exception ex) when (ex is ArgumentException or ExternalException or InvalidOperationException)
            {
                return null;
            }
            var ghost = new Ghost(picture, dropDown.Bounds, origin);
            try
            {
                ghost.Show();
                return ghost;
            }
            catch (Exception ex) when (!TwErrors.IsCritical(ex))
            {
                ghost.Dispose();
                return null;
            }
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WsExLayered | WsExTransparent | WsExNoActivate | WsExToolWindow | WsExTopMost;
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e) { }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        /// <summary>Shows the picture at <paramref name="scale"/> of its size about the origin, with <paramref name="opacity"/>.</summary>
        public void Draw(float scale, float opacity)
        {
            if (!IsHandleCreated) return;
            using var frame = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(frame))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TranslateTransform(_origin.X, _origin.Y);
                g.ScaleTransform(scale, scale);
                g.TranslateTransform(-_origin.X, -_origin.Y);
                g.DrawImage(_picture, 0, 0, _picture.Width, _picture.Height);
            }
            Present(frame, (byte)Math.Round(255 * Math.Max(0f, Math.Min(1f, opacity))));
        }

        private void Present(Bitmap frame, byte alpha)
        {
            IntPtr screen = GetDC(IntPtr.Zero), memory = CreateCompatibleDC(screen), bitmap = IntPtr.Zero, old = IntPtr.Zero;
            try
            {
                bitmap = frame.GetHbitmap(Color.FromArgb(0));
                old = SelectObject(memory, bitmap);
                var size = new SIZE { cx = frame.Width, cy = frame.Height };
                var source = new POINT();
                var top = new POINT { x = Left, y = Top };
                var blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
                UpdateLayeredWindow(Handle, screen, ref top, ref size, memory, ref source, 0, ref blend, 2);
            }
            finally
            {
                if (old != IntPtr.Zero) SelectObject(memory, old);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                DeleteDC(memory);
                ReleaseDC(IntPtr.Zero, screen);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _picture.Dispose();
            base.Dispose(disposing);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x, y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx, cy; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);
    }
}
