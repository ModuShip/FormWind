using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FormWind;

/// <summary>
/// Also darkens the thread's native tooltip windows in dark mode: ToolTip components, ToolStrip item tooltips,
/// ListView/TreeView tooltips. <c>tooltips_class32</c> windows get the <c>DarkMode_Explorer</c> theme when created and first
/// shown (Windows 10 1809+, undocumented; no effect on unsupported systems). Tooltip windows belong to the application's
/// own components, so tracking is thread-wide: tooltips are dark while at least one provider is in dark mode. The hook is
/// installed only while in dark mode.
/// </summary>
internal static class TwNativeToolTips
{
    private const string DarkTheme = "DarkMode_Explorer";
    private const uint EventObjectCreate = 0x8000;
    private const uint EventObjectDestroy = 0x8001;
    private const uint EventObjectShow = 0x8002;
    private const uint WinEventOutOfContext = 0;
    private const int ObjIdWindow = 0;

    [ThreadStatic] private static HashSet<object>? _darkOwners;
    [ThreadStatic] private static HashSet<IntPtr>? _themed;
    [ThreadStatic] private static IntPtr _hook;
    // Native kanca temsilciyi çağırdığı sürece GC'den korunmalı; iş parçacığı bitince de toplanmasın diye statik.
    private static readonly WinEventProc Proc = OnWinEvent;
    // Süreç kapanırken kaldırılmamış kancalar (UnhookWinEvent yalnızca kuran iş parçacığından çağrılabilir)
    // yönetilen koda girmemeli.
    private static volatile bool _shuttingDown;

    static TwNativeToolTips()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _shuttingDown = true;
        AppDomain.CurrentDomain.DomainUnload += (_, _) => _shuttingDown = true;
    }

    public static bool IsDark => _darkOwners is { Count: > 0 };

    /// <summary>Whether the hook is installed on this thread (for tests).</summary>
    internal static bool HookInstalled => _hook != IntPtr.Zero;

    /// <summary>Tooltip windows given the dark theme (for tests).</summary>
    internal static IReadOnlyCollection<IntPtr> Themed => (IReadOnlyCollection<IntPtr>?)_themed ?? Array.Empty<IntPtr>();

    public static void SetDark(object owner, bool dark)
    {
        _darkOwners ??= new HashSet<object>();
        bool before = IsDark;
        if (dark) _darkOwners.Add(owner);
        else _darkOwners.Remove(owner);

        if (IsDark) EnsureHook();
        else RemoveHook();
        if (before != IsDark) ApplyToExisting();
    }

    private static void EnsureHook()
    {
        if (_hook != IntPtr.Zero) return;
        try
        {
            _hook = SetWinEventHook(EventObjectCreate, EventObjectShow, IntPtr.Zero, Proc, GetCurrentProcessId(), GetCurrentThreadId(), WinEventOutOfContext);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    private static void RemoveHook()
    {
        if (_hook == IntPtr.Zero) return;
        try
        {
            UnhookWinEvent(_hook);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
        _hook = IntPtr.Zero;
    }

    // Tema değişince iş parçacığının mevcut ipucu pencereleri yeni temaya geçer.
    private static void ApplyToExisting()
    {
        _themed?.Clear();
        bool dark = IsDark;
        try
        {
            EnumThreadWindows(GetCurrentThreadId(), (hwnd, _) =>
            {
                if (IsToolTip(hwnd)) Apply(hwnd, dark);
                return true;
            }, IntPtr.Zero);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    private static void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (_shuttingDown) return;
        // Native geri çağrıdan kaçan istisna süreci düşürür.
        try
        {
            if (idObject != ObjIdWindow || idChild != 0 || hwnd == IntPtr.Zero) return;
            // Kapanan pencerenin handle'ı unutulur: küme uzun süren uygulamada büyümez ve Windows aynı handle değerini yeni bir
            // ipucu penceresine verirse o pencere "zaten temalandı" sanılıp açık kalmaz.
            if (eventType == EventObjectDestroy)
            {
                _themed?.Remove(hwnd);
                return;
            }
            if (!IsDark) return;
            // Gösterimde yalnızca henüz temalanmamış pencereler; her gösterimde yeniden temalamak boyutu yeniden hesaplatır.
            if (eventType == EventObjectShow && _themed != null && _themed.Contains(hwnd)) return;
            if (IsToolTip(hwnd)) Apply(hwnd, dark: true);
        }
        catch
        {
        }
    }

    private static void Apply(IntPtr hwnd, bool dark)
    {
        try
        {
            // null/null varsayılan temayı geri yükler.
            SetWindowTheme(hwnd, dark ? DarkTheme : null, null);
        }
        catch (DllNotFoundException)
        {
            return;
        }
        catch (EntryPointNotFoundException)
        {
            return;
        }
        if (!dark) return;
        _themed ??= new HashSet<IntPtr>();
        _themed.Add(hwnd);
    }

    // WinForms ipuçları "WindowsForms10.tooltips_class32.app…", ToolStrip ve native kontrollerinki "tooltips_class32".
    private static bool IsToolTip(IntPtr hwnd)
    {
        var name = new StringBuilder(128);
        return GetClassName(hwnd, name, name.Capacity) > 0
            && name.ToString().IndexOf("tooltips_class32", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc proc, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc proc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int count);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();
}
