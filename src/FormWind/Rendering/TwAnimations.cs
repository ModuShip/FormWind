using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>A control that draws the repeating <c>animate-*</c> animations itself.</summary>
internal interface ITwAnimated
{
    void SetAnimation(TwAnimation animation);
}

/// <summary>
/// Repeating animations (<c>animate-spin</c>, <c>animate-pulse</c>): a single timer per thread (about 60 frames/s)
/// invalidates the controls that run one. All controls share the same clock, so pulsing placeholders stay in step.
/// A control that cannot animate (<see cref="TwMotion.CanAnimate"/>) is drawn still: no turn and full opacity.
/// </summary>
internal static class TwAnimations
{
    [ThreadStatic]
    private static HashSet<Control>? _controls;

    [ThreadStatic]
    private static TwFrameTimer? _timer;

    /// <summary>Number of controls with a running animation on this thread (for tests).</summary>
    internal static int Count => _controls?.Count ?? 0;

    /// <summary>Starts (or with <see cref="TwAnimation.None"/> stops) invalidating <paramref name="control"/> every frame.</summary>
    public static void Track(Control control, TwAnimation animation)
    {
        if (animation == TwAnimation.None)
        {
            if (_controls?.Remove(control) == true)
            {
                control.VisibleChanged -= OnShown;
                control.HandleCreated -= OnShown;
                control.Disposed -= OnDisposed;
            }
            if (_controls is { Count: 0 }) _timer?.Stop();
            return;
        }
        _controls ??= new HashSet<Control>();
        if (!_controls.Add(control)) return;
        control.Disposed += OnDisposed;
        // The clock stops while none of the controls is shown (see Tick); showing one starts it again.
        control.VisibleChanged += OnShown;
        control.HandleCreated += OnShown;
        if (_timer is null)
        {
            _timer = new TwFrameTimer { Source = "animate" };
            _timer.Tick += (_, _) => Tick();
        }
        _timer.Start();
    }

    private static void OnDisposed(object? sender, EventArgs e) => Track((Control)sender!, TwAnimation.None);

    private static void OnShown(object? sender, EventArgs e)
    {
        if (sender is Control control && IsShowing(control)) _timer?.Start();
    }

    // Only visibility stops the clock: animations turned off in the settings keep it, so turning them on resumes at once.
    private static bool IsShowing(Control control) => control is { IsDisposed: false, IsHandleCreated: true, Visible: true };

    private static void Tick()
    {
        if (_controls is null) return;
        bool any = false;
        foreach (var control in _controls)
        {
            if (!IsShowing(control)) continue;
            any = true;
            if (TwMotion.CanAnimate(control)) control.Invalidate();
        }
        // No control on screen (all on hidden pages): leave the clock so the application can go idle.
        if (!any) _timer?.Stop();
    }

    /// <summary>Whether the shared clock of the repeating animations is running (for tests).</summary>
    internal static bool Running => _timer?.Enabled == true;

    /// <summary>Rotation of <c>animate-spin</c> in degrees (one turn per second); 0 when <paramref name="control"/> cannot animate.</summary>
    public static float SpinAngle(Control control) =>
        TwMotion.CanAnimate(control) ? (Environment.TickCount & int.MaxValue) % 1000 / 1000f * 360f : 0f;

    /// <summary>Opacity of <c>animate-pulse</c> (1 → 0.5 → 1 every 2 seconds); 1 when <paramref name="control"/> cannot animate.</summary>
    public static float PulseOpacity(Control control)
    {
        if (!TwMotion.CanAnimate(control)) return 1f;
        double phase = (Environment.TickCount & int.MaxValue) % 2000 / 2000.0;
        return (float)(0.75 + 0.25 * Math.Cos(phase * 2 * Math.PI));
    }

    /// <summary><paramref name="color"/> with its alpha multiplied by <paramref name="opacity"/>.</summary>
    public static Color Fade(Color color, float opacity) =>
        opacity >= 1f ? color : Color.FromArgb((int)Math.Round(color.A * opacity), color);
}
