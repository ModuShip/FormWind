using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FormWind;

/// <summary>
/// Styles an existing application with one call in <c>Program.Main</c>: loads <c>tw.config.json</c> and gives every form that
/// opens a <see cref="TailwindProvider"/> with <see cref="TailwindProvider.AutoStyle"/> on. Forms are styled after their
/// <c>Load</c> event and before they first appear (<c>WM_SHOWWINDOW</c>), so they are not seen unstyled; the provider is
/// disposed with the form. <see cref="DarkMode"/> switches every form at once.
/// <list type="bullet">
/// <item><description>Forms that already have a provider (a field of type <see cref="TailwindProvider"/>, e.g. one added in the
/// Designer) are left to it.</description></item>
/// <item><description>Only top-level forms get a provider; MDI children and forms embedded as controls are styled by their
/// parent's provider.</description></item>
/// <item><description>Forms on other UI threads are found by a background check and styled from then on; the first form on such
/// a thread may appear before it is styled.</description></item>
/// </list>
/// Use <see cref="Filter"/> to leave forms out and <see cref="FormAttached"/> to give single controls their own classes.
/// </summary>
public static class TwAutoTheme
{
    private const int WhCallWndProc = 4;
    private const int WmShowWindow = 0x0018;

    private static readonly object Gate = new object();
    private static readonly Dictionary<Form, TailwindProvider> Providers = new Dictionary<Form, TailwindProvider>();
    private static readonly ConditionalWeakTable<Form, object> Dispatched = new ConditionalWeakTable<Form, object>();
    [ThreadStatic] private static ThreadHook? _hook;
    private static bool _installed;
    private static bool _dark;
    private static bool _followWindows;
    private static bool _preferenceSubscribed;
    private static Thread? _watchdog;

    /// <summary>Whether <see cref="Install"/> was called (and <see cref="Uninstall"/> was not).</summary>
    public static bool IsInstalled
    {
        get
        {
            lock (Gate) return _installed;
        }
    }

    /// <summary>
    /// Loads the theme and starts styling forms. Call it in <c>Program.Main</c>, after <c>Application.EnableVisualStyles</c> and
    /// before the first form is created. Calling it again does nothing.
    /// </summary>
    /// <param name="themeFile">
    /// The theme file; default <c>tw.config.json</c> next to the application. If the file does not exist, the theme set in
    /// <see cref="TwTheme.Current"/> (the default theme unless the application set one) is used.
    /// </param>
    public static void Install(string? themeFile = null)
    {
        lock (Gate)
        {
            if (_installed) return;
            _installed = true;
        }
        var path = themeFile ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tw.config.json");
        if (File.Exists(path)) TwTheme.Current = TwTheme.Load(path);
        InstallThread();
        StartWatchdog();
    }

    /// <summary>Stops styling new forms and gives the styled forms their original look back.</summary>
    public static void Uninstall()
    {
        KeyValuePair<Form, TailwindProvider>[] providers;
        lock (Gate)
        {
            if (!_installed) return;
            _installed = false;
            providers = Providers.ToArray();
            Providers.Clear();
        }
        foreach (var pair in providers)
            OnFormThread(pair.Key, () =>
            {
                pair.Value.AutoStyle = false;   // otomatik class'lar kalkınca özgün değerler döner
                pair.Value.Dispose();
            });
        _hook?.Dispose();
    }

    /// <summary>Dark mode of every form styled by <see cref="TwAutoTheme"/> (and of the forms that open later).</summary>
    public static bool DarkMode
    {
        get
        {
            lock (Gate) return _dark;
        }
        set
        {
            KeyValuePair<Form, TailwindProvider>[] providers;
            lock (Gate)
            {
                if (_dark == value) return;
                _dark = value;
                providers = Providers.ToArray();
            }
            foreach (var pair in providers) OnFormThread(pair.Key, () => pair.Value.DarkMode = value);
            DarkModeChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Follows Windows' app mode (Settings → Personalization → Colors, "Choose your app mode"): <see cref="DarkMode"/> is set
    /// to it now and whenever the user changes it.
    /// </summary>
    public static bool FollowWindowsTheme
    {
        get
        {
            lock (Gate) return _followWindows;
        }
        set
        {
            lock (Gate)
            {
                _followWindows = value;
                if (value && !_preferenceSubscribed)
                {
                    _preferenceSubscribed = true;
                    SystemEvents.UserPreferenceChanged += (_, _) =>
                    {
                        if (FollowWindowsTheme) DarkMode = WindowsUsesDarkMode();
                    };
                }
            }
            if (value) DarkMode = WindowsUsesDarkMode();
        }
    }

    /// <summary><see cref="DarkMode"/> changed.</summary>
    public static event EventHandler? DarkModeChanged;

    /// <summary>Decides which forms are styled (<c>true</c> = style); <c>null</c> styles every form.</summary>
    public static Func<Form, bool>? Filter { get; set; }

    /// <summary>
    /// A form got its provider, before it appears. Give single controls their own classes here
    /// (<c>e.Provider.SetClasses(…)</c>); controls without classes keep the automatic ones.
    /// </summary>
    public static event EventHandler<TwAutoThemeEventArgs>? FormAttached;

    /// <summary>The provider <see cref="TwAutoTheme"/> gave <paramref name="form"/>; <c>null</c> if it has none.</summary>
    public static TailwindProvider? GetProvider(Form form)
    {
        if (form is null) throw new ArgumentNullException(nameof(form));
        lock (Gate) return Providers.TryGetValue(form, out var provider) ? provider : null;
    }

    /// <summary>Windows' app mode is dark (<c>AppsUseLightTheme</c> = 0).</summary>
    public static bool WindowsUsesDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is IOException)
        {
            return false;
        }
    }

    /// <summary>The form has a field of type <see cref="TailwindProvider"/> holding a provider (it styles itself).</summary>
    internal static bool HasOwnProvider(Form form)
    {
        const System.Reflection.BindingFlags Fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
        for (var type = form.GetType(); type != null && type != typeof(Form); type = type.BaseType)
            foreach (var field in type.GetFields(Fields))
                if (typeof(TailwindProvider).IsAssignableFrom(field.FieldType) && field.GetValue(form) is TailwindProvider) return true;
        return false;
    }

    /// <summary>Gives <paramref name="form"/> a provider unless it is left out; called before the form appears.</summary>
    internal static void Attach(Form form)
    {
        bool dark;
        lock (Gate)
        {
            if (!_installed || Providers.ContainsKey(form)) return;
            dark = _dark;
        }
        if (form.IsDisposed || !form.TopLevel || form.IsMdiChild || HasOwnProvider(form)) return;
        if (Filter is { } filter && !filter(form)) return;

        var provider = new TailwindProvider { DarkMode = dark };
        lock (Gate) Providers[form] = provider;
        form.Disposed += (_, _) =>
        {
            lock (Gate) Providers.Remove(form);
            provider.Dispose();
        };
        provider.ContainerControl = form;
        provider.AutoStyle = true;
        FormAttached?.Invoke(null, new TwAutoThemeEventArgs(form, provider));
    }

    private static void OnFormThread(Form form, Action action)
    {
        if (form.IsDisposed) return;
        if (!form.InvokeRequired)
        {
            action();
            return;
        }
        try
        {
            form.BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // Form tam o an kapandı.
        }
    }

    // Bu iş parçacığına bir kez: görünmek üzere olan formları yakalayan kanca; zaten açık formlar hemen stillenir.
    private static void InstallThread()
    {
        if (!IsInstalled) return;
        _hook ??= new ThreadHook();
        foreach (var form in Application.OpenForms.Cast<Form>().ToArray())
            if (!form.InvokeRequired && form.Visible) Attach(form);
    }

    // Başka UI iş parçacıklarında açılan formlar: her forma bir kez, o formun iş parçacığında kanca kurulur.
    private static void StartWatchdog()
    {
        if (_watchdog != null) return;
        _watchdog = new Thread(() =>
        {
            while (IsInstalled)
            {
                Thread.Sleep(250);
                DispatchToNewThreads();
            }
            _watchdog = null;
        })
        {
            IsBackground = true,
            Name = "FormWind TwAutoTheme",
        };
        _watchdog.Start();
    }

    // Ayrı yöntem: form dizisi bekleme sırasında yerel değişkende kalıp kapanan formu toplanmaktan korumasın (.NET Framework'te
    // uzun süreli kullanım testinde son kapanan form canlı kalıyordu).
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DispatchToNewThreads()
    {
        Form[] forms;
        try
        {
            forms = Application.OpenForms.Cast<Form>().ToArray();
        }
        catch (InvalidOperationException)
        {
            return; // Koleksiyon başka bir iş parçacığında değişti; bir sonraki turda.
        }
        foreach (var form in forms)
        {
            if (form.IsDisposed || !form.IsHandleCreated || Dispatched.TryGetValue(form, out _)) continue;
            Dispatched.Add(form, form);
            try
            {
                form.BeginInvoke(new Action(InstallThread));
            }
            catch (InvalidOperationException)
            {
                // Form tam o an kapandı.
            }
        }
    }

    private sealed class ThreadHook : IDisposable
    {
        private readonly HookProc _proc;
        private IntPtr _handle;

        public ThreadHook()
        {
            _proc = Callback;
            _handle = SetWindowsHookEx(WhCallWndProc, _proc, IntPtr.Zero, GetCurrentThreadId());
        }

        // CWPSTRUCT: lParam, wParam, message, hwnd. Her gönderilen mesajda çalışır; yalnızca mesaj numarası okunur.
        private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && lParam != IntPtr.Zero && Marshal.ReadInt32(lParam, 2 * IntPtr.Size) == WmShowWindow
                && Marshal.ReadIntPtr(lParam, IntPtr.Size) != IntPtr.Zero)
            {
                var hwnd = Marshal.ReadIntPtr(lParam, IntPtr.Size == 8 ? 24 : 12);
                if (Control.FromHandle(hwnd) is Form form)
                {
                    try
                    {
                        Attach(form);
                    }
                    catch (Exception ex) when (!TwErrors.IsCritical(ex))
                    {
                        // Stil hatası formun açılmasını engellemez.
                    }
                }
            }
            return CallNextHookEx(_handle, code, wParam, lParam);
        }

        public void Dispose()
        {
            if (_handle == IntPtr.Zero) return;
            UnhookWindowsHookEx(_handle);
            _handle = IntPtr.Zero;
            if (_hook == this) _hook = null;
        }
    }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}

/// <summary>A form styled by <see cref="TwAutoTheme"/> and its provider.</summary>
public sealed class TwAutoThemeEventArgs : EventArgs
{
    internal TwAutoThemeEventArgs(Form form, TailwindProvider provider)
    {
        Form = form;
        Provider = provider;
    }

    public Form Form { get; }

    public TailwindProvider Provider { get; }
}
