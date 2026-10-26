using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Pictures of a part of a window as it looks on screen, for transitions and fades. <see cref="Control.DrawToBitmap"/>
/// repaints the controls into the bitmap in reverse order: overlapping controls come out wrong, text in nested scrolled
/// panels is drawn twice and a form's removed title bar is drawn back. So the picture is copied from the screen when the
/// part is on screen and not covered by another application's window; otherwise the window's own content is asked from
/// the desktop window manager (<c>PrintWindow</c>), and <c>DrawToBitmap</c> is only the last resort.
/// </summary>
internal static class TwCapture
{
    private const int RdwInvalidate = 0x0001, RdwAllChildren = 0x0080, RdwUpdateNow = 0x0100, RdwFrame = 0x0400;
    private const int PwRenderFullContent = 0x00000002;
    private const int GwHwndPrev = 3, GwOwner = 4;
    private const uint WdaExcludeFromCapture = 0x11;
    private const uint QsPaint = 0x0020;
    private const int HorzRes = 8, VertRes = 10, DesktopVertRes = 117, DesktopHorzRes = 118;
    private const int DwmwaExtendedFrameBounds = 9, DwmwaCloaked = 14;

    /// <summary>How the last <see cref="Take"/> got its picture (for tests).</summary>
    internal enum Source
    {
        None,
        Screen,
        PrintWindow,
        DrawToBitmap,
    }

    [ThreadStatic]
    private static Source _lastSource;

    internal static Source LastSource => _lastSource;

    /// <summary>Paints the pending (invalidated) parts of <paramref name="control"/> and its children now.</summary>
    public static void PaintNow(Control control)
    {
        if (control is null || !control.IsHandleCreated || control.IsDisposed) return;
        RedrawWindow(control.Handle, IntPtr.Zero, IntPtr.Zero, RdwUpdateNow | RdwAllChildren);
    }

    /// <summary>Repaints <paramref name="control"/>, its children and (for a form) its frame now.</summary>
    public static void RepaintNow(Control control)
    {
        if (control is null || !control.IsHandleCreated || control.IsDisposed) return;
        RedrawWindow(control.Handle, IntPtr.Zero, IntPtr.Zero, RdwInvalidate | RdwFrame | RdwUpdateNow | RdwAllChildren);
    }

    /// <summary>
    /// Picture of <paramref name="screenBounds"/> (a part of <paramref name="control"/>, screen coordinates) after the
    /// pending paints are done; <c>null</c> when none can be taken.
    /// </summary>
    public static Bitmap? Take(Control control, Rectangle screenBounds)
    {
        _lastSource = Source.None;
        if (control is null || !control.IsHandleCreated || screenBounds.Width <= 0 || screenBounds.Height <= 0) return null;
        var top = control.TopLevelControl ?? control;
        // Ekran, masaüstü pencere yöneticisinin bir sonraki birleştirmesinde güncellenir: yeni boyanan içerik ancak ondan
        // sonra okunabilir. Boyanacak bir şey yoksa ekran zaten günceldir ve beklenmez (bir kareye kadar).
        bool painting = IsPaintPending();
        PaintNow(top);

        if (IsShownUncovered(top, screenBounds) && (!painting || Flush()) && FromScreen(top, screenBounds) is { } shown)
        {
            _lastSource = Source.Screen;
            return shown;
        }
        if (FromWindowManager(top, screenBounds) is { } printed)
        {
            _lastSource = Source.PrintWindow;
            return printed;
        }
        if (FromDrawing(control, screenBounds) is { } drawn)
        {
            _lastSource = Source.DrawToBitmap;
            return drawn;
        }
        return null;
    }

    /// <summary>
    /// Picture of the window's own content in <paramref name="screenBounds"/> under <paramref name="cover"/> (a picture
    /// FormWind laid over it for a transition); <c>null</c> when none can be taken. The cover is left out of screen capture
    /// for the moment of the copy (Windows 10 2004 and later); otherwise the content is asked from the desktop window
    /// manager, which renders the whole window and is several times slower.
    /// </summary>
    public static Bitmap? TakeContent(Control control, Rectangle screenBounds, Form? cover)
    {
        _lastSource = Source.None;
        if (control is null || !control.IsHandleCreated || screenBounds.Width <= 0 || screenBounds.Height <= 0) return null;
        var top = control.TopLevelControl ?? control;
        PaintNow(top);
        if (cover is { IsHandleCreated: true } && IsShownUncovered(top, screenBounds) && ExcludeFromCapture(cover, true))
        {
            try
            {
                if (Flush() && FromScreen(top, screenBounds) is { } shown)
                {
                    _lastSource = Source.Screen;
                    return shown;
                }
            }
            finally
            {
                ExcludeFromCapture(cover, false);
            }
        }
        if (FromWindowManager(top, screenBounds) is { } printed)
        {
            _lastSource = Source.PrintWindow;
            return printed;
        }
        if (FromDrawing(control, screenBounds) is { } drawn)
        {
            _lastSource = Source.DrawToBitmap;
            return drawn;
        }
        return null;
    }

    /// <summary>
    /// The visible bounds of a form on screen: Windows 10 and 11 count invisible resize borders in <see cref="Control.Bounds"/>,
    /// which would show the desktop behind them.
    /// </summary>
    public static Rectangle VisibleFormBounds(Form form)
    {
        var bounds = form.Bounds;
        if (!form.IsHandleCreated) return bounds;
        try
        {
            if (DwmGetWindowAttribute(form.Handle, DwmwaExtendedFrameBounds, out Rect rect, Marshal.SizeOf(typeof(Rect))) == 0)
            {
                var frame = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                // DPI sanallaştırması altında DWM fiziksel piksel verir; Bounds'un içinde değilse kullanılmaz.
                if (frame.Width > 0 && frame.Height > 0 && bounds.Contains(frame)) return frame;
            }
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        return bounds;
    }

    // Bu iş parçacığının bir penceresinde bekleyen boyama var mı (QS_PAINT).
    private static bool IsPaintPending() => (GetQueueStatus(QsPaint) & QsPaint) != 0;

    private static bool Flush()
    {
        try { DwmFlush(); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        return true;
    }

    // Parça tek bir ekranın içinde ve üstünde başka bir uygulamanın penceresi yok. Pencere sırası tek tek gezilir: tıklamayı
    // geçiren katmanlı pencereler (bildirimler, ekran kaplamaları) WindowFromPoint'te görünmez ama ekran resmine girer.
    private static bool IsShownUncovered(Control top, Rectangle bounds)
    {
        if (top is Form { WindowState: FormWindowState.Minimized } || !top.Visible) return false;
        if (!Screen.AllScreens.Any(s => s.Bounds.Contains(bounds))) return false;
        var root = top.Handle;
        int guard = 0;
        for (var window = GetWindow(root, GwHwndPrev); window != IntPtr.Zero && guard < 4096; window = GetWindow(window, GwHwndPrev), guard++)
        {
            if (!IsWindowVisible(window) || BelongsTo(window, root) || IsCloaked(window)) continue;
            if (!GetWindowRect(window, out var rect)) continue;
            if (Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom).IntersectsWith(bounds)) return false;
        }
        return guard < 4096;
    }

    /// <summary>
    /// Whether screen pixels are at the coordinates this thread's windows use. A DPI-unaware (or, on several monitors,
    /// system-aware) application on a scaled display sees virtualized coordinates while the screen is read in physical
    /// pixels: the copy would come from the wrong place.
    /// </summary>
    internal static bool ScreenMatchesWindows()
    {
        try
        {
            if (GetAwarenessFromDpiAwarenessContext(GetThreadDpiAwarenessContext()) == 2) return true;   // monitör başına
        }
        catch (EntryPointNotFoundException) { }
        if (Screen.AllScreens.Length != 1) return false;
        var screen = GetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero) return false;
        try
        {
            return GetDeviceCaps(screen, DesktopHorzRes) == GetDeviceCaps(screen, HorzRes)
                && GetDeviceCaps(screen, DesktopVertRes) == GetDeviceCaps(screen, VertRes);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    private static bool IsCloaked(IntPtr window)
    {
        try { return DwmGetWindowAttribute(window, DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0; }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
    }

    private static bool BelongsTo(IntPtr window, IntPtr root)
    {
        for (int depth = 0; window != IntPtr.Zero && depth < 16; depth++, window = GetWindow(window, GwOwner))
            if (window == root) return true;
        return false;
    }

    // Ekran koordinatları pencereninkilerle aynıysa doğrudan; değilse (ölçekli ekranda DPI farkında olmayan uygulama)
    // iş parçacığı bir anlığına monitör başına DPI farkında olur, fiziksel pikseller okunup pencerenin boyutuna küçültülür.
    private static Bitmap? FromScreen(Control top, Rectangle bounds)
    {
        if (ScreenMatchesWindows()) return FromScreen(bounds);
        var topLeft = new PointStruct(bounds.Left, bounds.Top);
        var bottomRight = new PointStruct(bounds.Right, bounds.Bottom);
        IntPtr old;
        try
        {
            if (!LogicalToPhysicalPointForPerMonitorDPI(top.Handle, ref topLeft) || !LogicalToPhysicalPointForPerMonitorDPI(top.Handle, ref bottomRight)) return null;
            old = SetThreadDpiAwarenessContext(new IntPtr(-4));   // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
        if (old == IntPtr.Zero) return null;
        Bitmap? physical;
        try
        {
            physical = FromScreen(Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y));
        }
        finally
        {
            SetThreadDpiAwarenessContext(old);
        }
        if (physical is null || physical.Size == bounds.Size) return physical;
        using (physical)
        {
            var scaled = new Bitmap(bounds.Width, bounds.Height);
            using var graphics = Graphics.FromImage(scaled);
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            graphics.DrawImage(physical, new Rectangle(Point.Empty, bounds.Size));
            return scaled;
        }
    }

    /// <summary>Leaves <paramref name="window"/> out of screen capture (or puts it back); <c>false</c> when Windows cannot.</summary>
    internal static bool ExcludeFromCapture(Form window, bool exclude)
    {
        try { return SetWindowDisplayAffinity(window.Handle, exclude ? WdaExcludeFromCapture : 0); }
        catch (EntryPointNotFoundException) { return false; }
    }

    private static Bitmap? FromScreen(Rectangle bounds)
    {
        Bitmap? picture = null;
        try
        {
            picture = new Bitmap(bounds.Width, bounds.Height);
            using var graphics = Graphics.FromImage(picture);
            graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            return picture;
        }
        catch (Exception ex) when (ex is Win32Exception or ArgumentException or ExternalException)
        {
            picture?.Dispose();
            return null;   // kilitli ekran, uzak masaüstü bağlantısı kesik …
        }
    }

    private static Bitmap? FromWindowManager(Control top, Rectangle bounds)
    {
        if (!top.Visible || !GetWindowRect(top.Handle, out var rect)) return null;
        var window = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        var part = Rectangle.Intersect(bounds, window);
        if (part != bounds || window.Width <= 0 || window.Height <= 0) return null;
        try
        {
            using var whole = new Bitmap(window.Width, window.Height);
            using (var graphics = Graphics.FromImage(whole))
            {
                var hdc = graphics.GetHdc();
                bool ok;
                try { ok = PrintWindow(top.Handle, hdc, PwRenderFullContent); }
                finally { graphics.ReleaseHdc(hdc); }
                if (!ok) return null;
            }
            var crop = new Rectangle(bounds.X - window.X, bounds.Y - window.Y, bounds.Width, bounds.Height);
            // Yeni gösterilen pencerede pencere yöneticisi henüz içerik vermeyebilir: tamamen siyah resim başarısız sayılır.
            return IsBlank(whole, crop) ? null : whole.Clone(crop, whole.PixelFormat);
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or EntryPointNotFoundException or OutOfMemoryException)
        {
            return null;
        }
    }

    // 5×5 noktanın hepsi opak siyah (ya da tamamen saydam) ise resim boş sayılır.
    private static bool IsBlank(Bitmap picture, Rectangle part)
    {
        for (int i = 0; i < 5; i++)
        for (int j = 0; j < 5; j++)
        {
            int argb = picture.GetPixel(part.X + (part.Width - 1) * i / 4, part.Y + (part.Height - 1) * j / 4).ToArgb();
            if (argb != unchecked((int)0xFF000000) && argb != 0) return false;
        }
        return true;
    }

    private static Bitmap? FromDrawing(Control control, Rectangle bounds)
    {
        try
        {
            var origin = control.Parent is null ? control.Location : control.Parent.PointToScreen(control.Location);
            using var whole = new Bitmap(control.Width, control.Height);
            control.DrawToBitmap(whole, new Rectangle(Point.Empty, control.Size));
            var part = Rectangle.Intersect(new Rectangle(bounds.X - origin.X, bounds.Y - origin.Y, bounds.Width, bounds.Height),
                new Rectangle(Point.Empty, control.Size));
            return part.Width > 0 && part.Height > 0 ? whole.Clone(part, whole.PixelFormat) : null;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or ExternalException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hWnd, IntPtr rect, IntPtr region, int flags);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X, Y;

        public PointStruct(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool LogicalToPhysicalPointForPerMonitorDPI(IntPtr hWnd, ref PointStruct point);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

    [DllImport("user32.dll")]
    private static extern uint GetQueueStatus(uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    private static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int index);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, int flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out Rect value, int size);
}
