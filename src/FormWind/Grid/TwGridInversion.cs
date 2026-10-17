using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

public static partial class TwGrid
{
    /// <summary>
    /// Dark mode for grids that paint their own cells (owner-drawn grids that ignore the cell styles and stay light): in dark
    /// mode the grid is styled with its light classes and its whole picture is inverted with the hue kept (as the browsers'
    /// forced dark mode does), so hard-coded light colors turn dark too. Images and colored marks are inverted as well.
    /// Scrollbars, the editing control and row details are separate windows: they get dark colors instead.
    /// </summary>
    public static void EnableDarkInversion(DataGridView grid, bool enable = true) => TwGridInversion.SetEnabled(grid, enable);

    /// <summary>Whether the grid is drawn inverted right now (inversion enabled and the provider in dark mode).</summary>
    public static bool IsDarkInverted(DataGridView grid) => TwGridInversion.IsActive(grid);
}

/// <summary>
/// Paints a DataGridView inverted: on WM_PAINT the grid draws itself into a bitmap (WM_PRINTCLIENT) and the bitmap is copied
/// through a color matrix (invert + 180° hue rotation: light backgrounds turn dark, blue stays blue).
/// </summary>
internal sealed class TwGridInversion : NativeWindow
{
    private const int WmPaint = 0x000F;
    private const int WmPrintClient = 0x0318;
    private const int PrfClient = 0x0004;
    private const int PrfEraseBkgnd = 0x0008;

    private static readonly ConditionalWeakTable<DataGridView, TwGridInversion> Table = new ConditionalWeakTable<DataGridView, TwGridInversion>();

    // 1 - H·c; H: CSS hue-rotate(180deg) matrisi. GDI+ satır vektörü kullanır: M[giriş][çıkış].
    private static readonly ColorMatrix Matrix = new ColorMatrix(new[]
    {
        new[] { 0.574f, -0.426f, -0.426f, 0f, 0f },
        new[] { -1.430f, -0.430f, -1.430f, 0f, 0f },
        new[] { -0.144f, -0.144f, 0.856f, 0f, 0f },
        new[] { 0f, 0f, 0f, 1f, 0f },
        new[] { 1f, 1f, 1f, 0f, 1f },
    });

    private readonly DataGridView _grid;
    private bool _enabled;
    private bool _active;

    private TwGridInversion(DataGridView grid)
    {
        _grid = grid;
        grid.HandleCreated += (_, _) => { if (_enabled) AssignHandle(grid.Handle); };
        grid.HandleDestroyed += (_, _) => ReleaseHandle();
    }

    public static bool IsEnabled(DataGridView grid) => Table.TryGetValue(grid, out var inversion) && inversion._enabled;

    public static bool IsActive(DataGridView grid) => Table.TryGetValue(grid, out var inversion) && inversion._enabled && inversion._active;

    public static void SetEnabled(DataGridView grid, bool enable)
    {
        if (grid is null) throw new ArgumentNullException(nameof(grid));
        var inversion = Table.GetValue(grid, g => new TwGridInversion(g));
        if (inversion._enabled == enable) return;
        inversion._enabled = enable;
        if (enable && grid.IsHandleCreated) inversion.AssignHandle(grid.Handle);
        else if (!enable) inversion.ReleaseHandle();
        // Sağlayıcı stili yeniden uygulasın: çevrilen grid açık class'larla stillenir.
        inversion._reapply?.Invoke();
        grid.Invalidate();
    }

    private Action? _reapply;

    /// <summary>
    /// Called by the binding before styling: records whether the provider is dark and returns the state the grid is styled
    /// with (without dark when the grid is inverted). <paramref name="reapply"/> restyles the grid when inversion is toggled.
    /// </summary>
    public static TwState Update(DataGridView grid, TwState state, Action reapply)
    {
        var inversion = Table.GetValue(grid, g => new TwGridInversion(g));
        inversion._reapply = reapply;
        if (!inversion._enabled)
        {
            if (inversion._active)
            {
                inversion._active = false;
                grid.Invalidate();
            }
            return state;
        }
        bool dark = (state & TwState.Dark) != 0;
        if (inversion._active != dark)
        {
            inversion._active = dark;
            grid.Invalidate();
        }
        return state & ~TwState.Dark;
    }

    /// <summary>A color as the inverted grid shows it (for the editing control, which is not inverted).</summary>
    public static Color Invert(Color color)
    {
        float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f;
        float Channel(int j) => r * Matrix[0, j] + g * Matrix[1, j] + b * Matrix[2, j] + Matrix[4, j];
        int Byte(float v) => (int)Math.Round(Math.Max(0, Math.Min(1, v)) * 255);
        return Color.FromArgb(color.A, Byte(Channel(0)), Byte(Channel(1)), Byte(Channel(2)));
    }

    /// <summary>Renders the grid inverted (also used by the tests).</summary>
    internal Bitmap Render()
    {
        var size = _grid.ClientSize;
        // GDI saydamlık kanalını yazmaz; alfasız biçimde çizilir ki metin saydam görünmesin.
        var picture = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height), PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(picture))
        {
            var hdc = g.GetHdc();
            try
            {
                SendMessage(_grid.Handle, WmPrintClient, hdc, (IntPtr)(PrfClient | PrfEraseBkgnd));
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
        }
        var inverted = new Bitmap(picture.Width, picture.Height, PixelFormat.Format32bppRgb);
        using (var g = Graphics.FromImage(inverted))
        using (var attributes = new ImageAttributes())
        {
            attributes.SetColorMatrix(Matrix);
            g.DrawImage(picture, new Rectangle(Point.Empty, picture.Size), 0, 0, picture.Width, picture.Height, GraphicsUnit.Pixel, attributes);
        }
        picture.Dispose();
        return inverted;
    }

    internal static Bitmap RenderInverted(DataGridView grid) => Table.GetValue(grid, g => new TwGridInversion(g)).Render();

    protected override void WndProc(ref Message m)
    {
        if (m.Msg != WmPaint || !_active || !_enabled || _grid.IsDisposed)
        {
            base.WndProc(ref m);
            return;
        }

        var paint = new PaintStruct();
        var hdc = BeginPaint(m.HWnd, ref paint);
        try
        {
            using var picture = Render();
            using var target = Graphics.FromHdc(hdc);
            var clip = Rectangle.FromLTRB(paint.Left, paint.Top, paint.Right, paint.Bottom);
            target.DrawImage(picture, clip, clip, GraphicsUnit.Pixel);
        }
        finally
        {
            EndPaint(m.HWnd, ref paint);
        }
        m.Result = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public IntPtr Hdc;
        public bool Erase;
        public int Left, Top, Right, Bottom;
        public bool Restore;
        public bool IncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Reserved;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hwnd, ref PaintStruct paint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hwnd, ref PaintStruct paint);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
}
