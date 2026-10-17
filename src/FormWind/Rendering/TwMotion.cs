using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Shared setting for in-control animations (tab indicator sliding, row hover fading in, skeleton row pulse). By default
/// it follows Windows' "Show animations in Windows" setting; when off, changes are immediate.
/// </summary>
public static class TwMotion
{
    private const int SpiGetClientAreaAnimation = 0x1042;

    private static bool? _enabled;

    /// <summary>Whether animations are on. Can be set (e.g. to turn them off in tests or from an application setting).</summary>
    public static bool Enabled
    {
        get => _enabled ??= SystemAnimationsEnabled();
        set => _enabled = value;
    }

    private static bool SystemAnimationsEnabled()
    {
        try
        {
            return !SystemParametersInfo(SpiGetClientAreaAnimation, 0, out var enabled, 0) || enabled;
        }
        catch (EntryPointNotFoundException)
        {
            return true;
        }
    }

    /// <summary>Animates while the control is visible and animations are on (not at design time and not while invisible).</summary>
    internal static bool CanAnimate(Control control) =>
        Enabled && control.IsHandleCreated && control.Visible && control.Site?.DesignMode != true;

    internal static float EaseOut(float t)
    {
        t = Math.Max(0f, Math.Min(1f, t));
        float inverse = 1 - t;
        return 1 - inverse * inverse * inverse;
    }

    internal static RectangleF Lerp(RectangleF from, RectangleF to, float t) => new RectangleF(
        from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t,
        from.Width + (to.Width - from.Width) * t, from.Height + (to.Height - from.Height) * t);

    internal static Color Blend(Color from, Color to, float t) => Color.FromArgb(
        (int)(from.A + (to.A - from.A) * t), (int)(from.R + (to.R - from.R) * t),
        (int)(from.G + (to.G - from.G) * t), (int)(from.B + (to.B - from.B) * t));

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(int action, int param, out bool value, int winIni);
}

/// <summary>Progress of a single value over time; the owner reads <see cref="Progress"/> every frame and repaints.</summary>
internal sealed class TwTween : IDisposable
{
    private readonly Control _owner;
    private readonly Timer _timer = new Timer { Interval = 16 };
    private DateTime _start;
    private int _duration;

    public TwTween(Control owner)
    {
        _owner = owner;
        _timer.Tick += (_, _) =>
        {
            if (Progress >= 1f) _timer.Stop();
            _owner.Invalidate();
        };
    }

    public bool Running => _timer.Enabled;

    /// <summary>Progress between 0 and 1 with ease-out applied (1 without animation).</summary>
    public float Progress => _timer.Enabled || _duration > 0 && (DateTime.UtcNow - _start).TotalMilliseconds < _duration
        ? TwMotion.EaseOut((float)((DateTime.UtcNow - _start).TotalMilliseconds / _duration))
        : 1f;

    /// <summary>Starts a new animation; finishes immediately if it cannot animate.</summary>
    public void Start(int durationMs)
    {
        if (!TwMotion.CanAnimate(_owner) || durationMs <= 0)
        {
            _duration = 0;
            _timer.Stop();
            return;
        }
        _start = DateTime.UtcNow;
        _duration = durationMs;
        _timer.Start();
    }

    public void Stop()
    {
        _duration = 0;
        _timer.Stop();
    }

    public void Dispose() => _timer.Dispose();
}
