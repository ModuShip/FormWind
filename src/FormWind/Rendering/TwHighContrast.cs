using System;
using Microsoft.Win32;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Tracks Windows High Contrast mode. While it is on, <see cref="TailwindProvider"/> uses system colors instead of class colors
/// (see <see cref="TailwindProvider.RespectHighContrast"/>); styles are reapplied when the mode changes at run time.
/// </summary>
internal static class TwHighContrast
{
    private static bool _subscribed;
    private static bool _last;

    /// <summary>Simulates the mode in tests; <c>null</c> = the system setting.</summary>
    internal static bool? Override { get; set; }

    public static bool IsActive => Override ?? SystemInformation.HighContrast;

    /// <summary>The mode was turned on or off.</summary>
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

    /// <summary>Notifies listeners of a change (system event or tests).</summary>
    internal static void NotifyChanged()
    {
        _last = IsActive;
        ChangedCore?.Invoke(null, EventArgs.Empty);
    }

    private static void EnsureSubscribed()
    {
        if (_subscribed) return;
        _subscribed = true;
        _last = IsActive;
        // SystemEvents olayı abonenin UI iş parçacığında tetikler.
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category != UserPreferenceCategory.Accessibility && e.Category != UserPreferenceCategory.Color) return;
            if (IsActive != _last) NotifyChanged();
        };
    }
}
