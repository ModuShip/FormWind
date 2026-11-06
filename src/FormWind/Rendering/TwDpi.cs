using System;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// The DPI values FormWind scales with, and a way to simulate another display scale in tests.
/// <para>
/// Class values are 96 DPI pixels (<c>text-[13px]</c>, <c>h-10</c>) and are multiplied by <see cref="DeviceDpi(Control)"/> / 96;
/// font sizes in points are converted with <see cref="SystemDpi"/>. An application that multiplies class values by its own
/// scale factor therefore scales them twice: on a developer machine at 100 % nothing shows, at 125 % text is 25 % too large.
/// <see cref="Simulate(int)"/> makes FormWind behave as on another scale without changing the display:
/// </para>
/// <code>
/// using (TwDpi.Simulate(120))            // 125 %
/// using (var form = new MainForm())
/// using (var picture = TwOffscreen.Render(form))
///     picture.Save("main-125.png");
/// </code>
/// </summary>
/// <remarks>
/// On .NET Framework, WinForms applies the <c>DpiAwareness</c> setting from app.config when the first <see cref="Control"/> is
/// created. GDI+ reads the screen DPI once when it starts: if a <c>Font</c> is created before that (e.g. while loading the theme),
/// GDI+ stays at 96 DPI in a PerMonitorV2 application and native controls (TextBox) draw their fonts too small. FormWind
/// initializes WinForms' DPI handling before touching GDI+.
/// </remarks>
public static class TwDpi
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

    /// <summary>
    /// Ratio between the control's screen and the system DPI (1 in DPI-unaware and system-aware applications, unless
    /// <see cref="Simulate(int)"/> is active); <c>text-*</c> font sizes are multiplied by it.
    /// </summary>
    public static float FontScale(Control control) => DeviceDpi(control) / (float)SystemDpi;

    /// <summary>
    /// DPI of the screen the control is on. On .NET Framework some controls (e.g. Label) do not update their own
    /// <c>DeviceDpi</c> when the screen changes; the form's value is used.
    /// </summary>
    public static int DeviceDpi(Control control) => DeviceDpiOverride ?? control.FindForm()?.DeviceDpi ?? control.DeviceDpi;

    // Testler: sınıf değerlerinin ölçeklendiği ekran DPI'si (ör. %150 için 144). Uygulamalar Simulate kullanır.
    internal static int? DeviceDpiOverride { get; set; }

    /// <summary>The DPI <see cref="Simulate(int)"/> currently simulates, or <c>null</c> when the real displays are used.</summary>
    public static int? SimulatedDpi => DeviceDpiOverride;

    /// <summary>
    /// Makes FormWind draw as on a display with <paramref name="dpi"/> (120 = 125 %, 144 = 150 %) until the returned scope is
    /// disposed: <see cref="DeviceDpi(Control)"/> returns it, so class sizes (<c>h-10</c>, <c>p-4</c>) and <c>text-*</c> fonts
    /// get the pixel sizes they would have there (<c>text-[13px]</c> at 120 is 16.25 px). Fonts are still rendered by GDI at the
    /// real <see cref="SystemDpi"/>, which does not change. Controls styled inside the scope get the simulated sizes; controls
    /// styled before it keep theirs until they are styled again (<see cref="TailwindProvider.RefreshAll"/>). Fonts that do not
    /// come from a <c>text-*</c> class and WinForms' own scaling (<c>AutoScaleMode</c>, native control metrics) do not change.
    /// The setting is process-wide: use it from one UI thread, and dispose nested scopes in reverse order (each restores the
    /// value it replaced).
    /// </summary>
    public static IDisposable Simulate(int dpi)
    {
        if (dpi < 48 || dpi > 960) throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "48–960");
        var scope = new SimulationScope(DeviceDpiOverride);
        DeviceDpiOverride = dpi;
        return scope;
    }

    private sealed class SimulationScope : IDisposable
    {
        private readonly int? _previous;
        private bool _disposed;

        public SimulationScope(int? previous) => _previous = previous;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DeviceDpiOverride = _previous;
        }
    }

    internal static bool EnsureInitialized()
    {
#if NETFRAMEWORK
        if (!_initialized)
        {
            _initialized = true;
            // WinForms DPI ayarını (app.config DpiAwareness) ilk Control'de uygular. Kontrol oluşturmadan aynı başlatıcı
            // çağrılır: FormWind Main'den önce (statik alanda TwFonts.Register) başlasa da uygulamanın
            // SetCompatibleTextRenderingDefault ve EnableVisualStyles çağrılarına bir kontrol önce gelmiş olmaz.
            var init = typeof(Control).Assembly.GetType("System.Windows.Forms.DpiHelper")?.GetMethod("InitializeDpiHelperForWinforms",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, System.Type.EmptyTypes, null);
            try
            {
                if (init != null) init.Invoke(null, null);
                else using (new Control()) { }
            }
            catch (System.Reflection.TargetInvocationException)
            {
                using (new Control()) { }
            }
        }
#endif
        return true;
    }
}
