using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Options of <see cref="TwOffscreen.Render(Form, TwOffscreenOptions)"/>.</summary>
public sealed class TwOffscreenOptions
{
    /// <summary>Draws the windows the form owns (sheets, popovers, dialogs, drop-downs) over it; default <c>true</c>.</summary>
    public bool IncludeOwned { get; set; } = true;

    /// <summary>
    /// Makes the picture as large as the form and the owned windows together; by default the picture is the form's size and
    /// owned windows are clipped to it. Areas no window covers stay <see cref="Background"/>.
    /// </summary>
    public bool ExpandToOwned { get; set; }

    /// <summary>
    /// Turns <see cref="TwMotion"/> off while the form opens and is drawn, so a window that fades in is complete when it is
    /// pictured; default <c>true</c>. The previous setting comes back afterwards.
    /// </summary>
    public bool DisableMotion { get; set; } = true;

    /// <summary>Color behind the windows (corners, transparent areas); default <see cref="Color.Transparent"/>.</summary>
    public Color Background { get; set; } = Color.Transparent;

    /// <summary>
    /// Closes (and so disposes) a form that <see cref="TwOffscreen"/> opened itself, instead of only hiding it again;
    /// default <c>false</c>. A form that was already visible is never closed.
    /// </summary>
    public bool CloseWhenDone { get; set; }
}

/// <summary>
/// Pictures of a whole window for visual regression tests and documentation. <see cref="Control.DrawToBitmap"/> leaves out
/// the windows a form owns (<see cref="TwSheet"/>, <see cref="TwPopover"/>, dialogs) and ignores <see cref="Form.Opacity"/>,
/// <see cref="Form.TransparencyKey"/> and <see cref="System.Drawing.Region"/> of the form. <see cref="Render(Form)"/> opens a form that is not shown
/// yet outside the visible screen area (it is parked far off-screen and kept out of the taskbar), lets it lay out and paint, asks Windows for the
/// content of the form and of every window it owns (<c>PrintWindow</c>, which works off-screen) and composes them: owned
/// windows over the form in their z-order, opacity applied, transparent areas transparent. The result is a 32-bit bitmap with
/// alpha; the caller disposes it.
/// <code>
/// using var picture = TwOffscreen.Render(form);
/// picture.Save("login-dark.png");
/// </code>
/// A form that is already visible is pictured where it is. The form's windows are created on the calling (UI) thread.
/// </summary>
public static class TwOffscreen
{
    private const int GwOwner = 4;
    private const int PwRenderFullContent = 0x00000002;
    private const int Parked = -32000;

    /// <summary>Renders <paramref name="form"/> with its owned windows; see <see cref="TwOffscreen"/>.</summary>
    public static Bitmap Render(Form form) => Render(form, null);

    /// <summary>Renders <paramref name="form"/>; see <see cref="TwOffscreen"/> and <see cref="TwOffscreenOptions"/>.</summary>
    public static Bitmap Render(Form form, TwOffscreenOptions? options)
    {
        if (form is null) throw new ArgumentNullException(nameof(form));
        if (form.IsDisposed) throw new ObjectDisposedException(form.GetType().Name);
        options ??= new TwOffscreenOptions();

        bool opened = !form.Visible;
        var oldStart = form.StartPosition;
        var oldLocation = form.Location;
        bool oldTaskbar = form.ShowInTaskbar;
        bool oldMotion = TwMotion.Enabled;
        try
        {
            if (options.DisableMotion) TwMotion.Enabled = false;
            if (opened) Open(form);
            Settle(form);
            return Compose(form, options);
        }
        finally
        {
            TwMotion.Enabled = oldMotion;
            if (opened && !form.IsDisposed)
            {
                if (options.CloseWhenDone) form.Close();
                else
                {
                    form.Hide();
                    form.StartPosition = oldStart;
                    form.Location = oldLocation;
                    form.ShowInTaskbar = oldTaskbar;
                }
            }
        }
    }

    // Pencereyi görünür ekranın dışında, etkinleştirmeden açar.
    private static void Open(Form form)
    {
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(Parked, Parked);
        // Show etkinleştirir; park edilmiş pencere ekranda görünmez ama etkin pencere olur (testlerde sorun değil).
        form.Show();
    }

    private static void Settle(Form form)
    {
        form.PerformLayout();
        Application.DoEvents();
        TwCapture.RepaintNow(form);
        Application.DoEvents();
    }

    private static Bitmap Compose(Form form, TwOffscreenOptions options)
    {
        var main = TwCapture.VisibleFormBounds(form);
        var windows = new List<(IntPtr Handle, Rectangle Bounds, Form? Form)>();
        if (options.IncludeOwned) windows.AddRange(OwnedWindows(form));
        // Taşınan ya da yeni gösterilen pencerenin yüzeyi bayat olabilir; her pencere yeniden boyanır.
        foreach (var window in windows)
            if (Control.FromHandle(window.Handle) is { } control) TwCapture.RepaintNow(control);
        Application.DoEvents();

        var canvas = main;
        if (options.ExpandToOwned) foreach (var window in windows) canvas = Rectangle.Union(canvas, window.Bounds);
        var result = new Bitmap(Math.Max(1, canvas.Width), Math.Max(1, canvas.Height), PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(result);
        g.Clear(options.Background);

        DrawWindow(g, form.Handle, main, canvas.Location, form);
        foreach (var (handle, bounds, owned) in windows) DrawWindow(g, handle, bounds, canvas.Location, owned);
        return result;
    }

    // Formun sahip olduğu görünür pencereler, alttan üste (çizim sırası). Form olmayanlar da (ToolStrip açılır menüleri) gelir.
    private static List<(IntPtr, Rectangle, Form?)> OwnedWindows(Form form)
    {
        var found = new List<IntPtr>();
        var root = form.Handle;
        uint thread = GetWindowThreadProcessId(root, out _);
        EnumThreadWindows(thread, (handle, _) =>
        {
            if (handle != root && IsWindowVisible(handle) && OwnedBy(handle, root) && !IsSkipped(handle)) found.Add(handle);
            return true;
        }, IntPtr.Zero);
        found.Reverse();   // numaralandırma üstten alta gider

        var result = new List<(IntPtr, Rectangle, Form?)>();
        foreach (var handle in found)
        {
            var control = Control.FromHandle(handle) as Form;
            Rectangle bounds;
            if (control != null) bounds = TwCapture.VisibleFormBounds(control);
            else if (GetWindowRect(handle, out var rect)) bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            else continue;
            if (bounds.Width > 0 && bounds.Height > 0) result.Add((handle, bounds, control));
        }
        return result;
    }

    // Sahip zincirinde (GW_OWNER) kök pencereye ulaşılıyor mu; GetAncestor(GA_ROOTOWNER) sahipli üst düzey pencerelerde zinciri izlemez.
    private static bool OwnedBy(IntPtr handle, IntPtr root)
    {
        for (int depth = 0; depth < 16; depth++)
        {
            handle = GetWindow(handle, GwOwner);
            if (handle == IntPtr.Zero) return false;
            if (handle == root) return true;
        }
        return false;
    }

    // Araç ipuçları ve gölge pencereleri bir görüntüye girmez.
    private static bool IsSkipped(IntPtr handle)
    {
        var name = new StringBuilder(64);
        GetClassName(handle, name, name.Capacity);
        var text = name.ToString();
        return text.StartsWith("tooltips_class", StringComparison.OrdinalIgnoreCase) || text == "SysShadow";
    }

    private static void DrawWindow(Graphics g, IntPtr handle, Rectangle bounds, Point origin, Form? form)
    {
        using var picture = Print(handle, bounds, form != null && form.TransparencyKey != Color.Empty);
        if (picture is null) return;
        var target = new Rectangle(bounds.X - origin.X, bounds.Y - origin.Y, bounds.Width, bounds.Height);

        var state = g.Save();
        try
        {
            // Region (yuvarlatılmış köşeler): pencerenin sol üst köşesine göre; görünür çerçeve ile pencere dikdörtgeni farklı olabilir.
            if (form?.Region is { } region && GetWindowRect(handle, out var rect))
            {
                using var clip = region.Clone();
                clip.Translate(rect.Left - origin.X, rect.Top - origin.Y);
                g.SetClip(clip, CombineMode.Intersect);
            }
            using var attributes = new ImageAttributes();
            if (form is { Opacity: < 1 })
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = (float)form.Opacity });
            if (form != null && form.TransparencyKey != Color.Empty)
                attributes.SetColorKey(form.TransparencyKey, form.TransparencyKey);
            g.CompositingMode = CompositingMode.SourceOver;
            g.DrawImage(picture, target, 0, 0, picture.Width, picture.Height, GraphicsUnit.Pixel, attributes);
        }
        finally
        {
            g.Restore(state);
        }
    }

    // PrintWindow: pencerenin kendi içeriği, ekranda olmasa da. TransparencyKey'li pencerede tam içerik istenmez: masaüstü pencere
    // yöneticisi anahtar rengini siyaha çevirir ve anahtar bir daha bulunamaz. GDI alfa kanalına yazmaz; bu yüzden alfasız biçim kullanılır.
    private static Bitmap? Print(IntPtr handle, Rectangle visible, bool keyed)
    {
        // Önce pencerenin kendi boyaması (WM_PRINT): ekran dışındaki pencerede her zaman aynı sonucu verir. Masaüstü pencere
        // yöneticisinin yüzeyi (tam içerik) park edilmiş pencerede bazen henüz boştur; ancak boş çıkarsa denenir.
        var picture = Print(handle, visible, 0);
        if (picture != null && (keyed || !IsBlank(picture))) return picture;
        picture?.Dispose();
        return keyed ? null : Print(handle, visible, PwRenderFullContent);
    }

    // Tüm örnek noktaları siyah mı (pencere hiçbir şey çizmemiş).
    private static bool IsBlank(Bitmap picture)
    {
        for (int i = 0; i < 5; i++)
        for (int j = 0; j < 5; j++)
            if (picture.GetPixel((picture.Width - 1) * i / 4, (picture.Height - 1) * j / 4).ToArgb() != unchecked((int)0xFF000000)) return false;
        return true;
    }

    private static Bitmap? Print(IntPtr handle, Rectangle visible, int flags)
    {
        if (!GetWindowRect(handle, out var rect)) return null;
        var window = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        if (window.Width <= 0 || window.Height <= 0) return null;
        try
        {
            using var whole = new Bitmap(window.Width, window.Height, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(whole))
            {
                var hdc = graphics.GetHdc();
                bool ok;
                try { ok = PrintWindow(handle, hdc, flags); }
                finally { graphics.ReleaseHdc(hdc); }
                if (!ok) return null;
            }
            var crop = Rectangle.Intersect(new Rectangle(visible.X - window.X, visible.Y - window.Y, visible.Width, visible.Height), new Rectangle(Point.Empty, window.Size));
            return crop.Width > 0 && crop.Height > 0 ? whole.Clone(crop, PixelFormat.Format24bppRgb) : null;
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or OutOfMemoryException)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    private delegate bool EnumThreadWindowsProc(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumThreadWindows(uint thread, EnumThreadWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr handle, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr handle, IntPtr hdc, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr handle, StringBuilder name, int count);
}
