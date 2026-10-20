using System;
using Microsoft.Win32;

namespace FormWind;

/// <summary>
/// Windows' text size setting (Settings → Accessibility → Text size, <c>HKCU\Software\Microsoft\Accessibility\TextScaleFactor</c>,
/// 100–225 %). Windows enlarges the system fonts (and with them WinForms' default font) by it; <c>text-*</c> sizes are
/// multiplied by it like <c>rem</c> sizes in a browser (see <see cref="TwFonts.RespectTextScale"/>). Styles are reapplied
/// when it changes at run time.
/// </summary>
internal static class TwTextScale
{
    private static bool _subscribed;
    private static float? _cached;

    /// <summary>Simulates the setting in tests (1–2.25); <c>null</c> = the system setting.</summary>
    internal static float? Override { get; set; }

    /// <summary>The multiplier for <c>text-*</c> sizes: 1 when the setting is off or <see cref="TwFonts.RespectTextScale"/> is false.</summary>
    public static float Factor => TwFonts.RespectTextScale ? Override ?? (_cached ??= Read()) : 1f;

    /// <summary>The setting changed.</summary>
    public static event EventHandler? Changed
    {
        add
        {
            EnsureSubscribed();
            ChangedCore += value;
        }
        remove => ChangedCore -= value;
    }

    private static event EventHandler? ChangedCore;

    /// <summary>Notifies listeners of a change (system event, <see cref="TwFonts.RespectTextScale"/> or tests).</summary>
    internal static void NotifyChanged()
    {
        _cached = null;
        ChangedCore?.Invoke(null, EventArgs.Empty);
    }

    private static float Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility");
            if (key?.GetValue("TextScaleFactor") is int percent) return Math.Max(100, Math.Min(225, percent)) / 100f;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is System.IO.IOException)
        {
            // Okunamazsa ölçek yok sayılır.
        }
        return 1f;
    }

    private static void EnsureSubscribed()
    {
        if (_subscribed) return;
        _subscribed = true;
        // Metin boyutu değişince Windows WM_SETTINGCHANGE yayınlar; kategori sürüme göre değiştiğinden değer karşılaştırılır.
        SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            float before = _cached ?? Read();
            _cached = null;
            if (Math.Abs(Read() - before) > 0.001f) NotifyChanged();
        };
    }
}
