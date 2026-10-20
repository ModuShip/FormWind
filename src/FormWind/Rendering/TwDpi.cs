using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// On .NET Framework, WinForms applies the <c>DpiAwareness</c> setting from app.config when the first <see cref="Control"/> is created.
/// GDI+ reads the screen DPI once when it starts: if a <c>Font</c> is created before that (e.g. while loading the theme), GDI+ stays at
/// 96 DPI in a PerMonitorV2 application and native controls (TextBox) draw their fonts too small. FormWind calls this before
/// touching GDI+.
/// </summary>
internal static class TwDpi
{
#if NETFRAMEWORK
    private static bool _initialized;
#endif

    private static int _systemDpi;

    /// <summary>
    /// The process's system DPI (the primary screen at startup). Font sizes in points are converted to pixels with this DPI;
    /// under PerMonitorV2 a control on a screen with another DPI needs its font enlarged by <see cref="FontScale"/>.
    /// </summary>
    public static int SystemDpi
    {
        get
        {
            if (_systemDpi == 0)
            {
                EnsureInitialized();
                using var g = System.Drawing.Graphics.FromHwnd(System.IntPtr.Zero);
                _systemDpi = (int)System.Math.Round(g.DpiY);
            }
            return _systemDpi;
        }
    }

    /// <summary>Ratio between the control's screen and the system DPI (1 in DPI-unaware and system-aware applications).</summary>
    public static float FontScale(Control control) => DeviceDpi(control) / (float)SystemDpi;

    /// <summary>
    /// DPI of the screen the control is on. On .NET Framework some controls (e.g. Label) do not update their own
    /// <c>DeviceDpi</c> when the screen changes; the form's value is used.
    /// </summary>
    public static int DeviceDpi(Control control) => DeviceDpiOverride ?? control.FindForm()?.DeviceDpi ?? control.DeviceDpi;

    /// <summary>Tests: the screen DPI FormWind scales class values with (e.g. 144 for 150 %).</summary>
    internal static int? DeviceDpiOverride { get; set; }

    public static bool EnsureInitialized()
    {
#if NETFRAMEWORK
        if (!_initialized)
        {
            _initialized = true;
            // Handle oluşturmaz; yalnızca WinForms'un DPI başlatmasını tetikler.
            using (new Control()) { }
        }
#endif
        return true;
    }
}
