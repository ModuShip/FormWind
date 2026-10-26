using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FormWind;

/// <summary>
/// Shared setting for animations: <c>transition</c> classes, <c>animate-*</c> classes and in-control animations (tab
/// indicator sliding, switch knob, row hover fading in, toasts and popovers fading, skeleton row pulse). By default it
/// follows Windows' "Show animations in Windows" setting; when off, changes are immediate. Also gives the easing helpers
/// FormWind's own animations use, for custom controls (see <see cref="TwTween"/>).
/// </summary>
public static class TwMotion
{
    private const int SpiGetClientAreaAnimation = 0x1042;

    private static bool? _override;
    private static bool? _system;
    private static bool _subscribed;

    /// <summary>
    /// Whether animations are on. Can be set (e.g. to turn them off in tests or from an application setting); then the Windows
    /// setting is no longer followed until <see cref="FollowSystem"/>. Styles with <c>motion-safe:</c>/<c>motion-reduce:</c>
    /// variants are reapplied when the value changes.
    /// </summary>
    public static bool Enabled
    {
        get => _override ?? (_system ??= SystemAnimationsEnabled());
        set
        {
            bool old = Enabled;
            _override = value;
            if (old != value) OnChanged();
        }
    }

    /// <summary>Follows Windows' "Show animations" setting again after <see cref="Enabled"/> was set.</summary>
    public static void FollowSystem()
    {
        bool old = Enabled;
        _override = null;
        _system = null;
        if (Enabled != old) OnChanged();
    }

    /// <summary>When <see cref="Enabled"/> changes (set in code or the Windows setting changed).</summary>
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

    private static void OnChanged() => ChangedCore?.Invoke(null, EventArgs.Empty);

    private static void EnsureSubscribed()
    {
        if (_subscribed) return;
        _subscribed = true;
        // SPI_SETCLIENTAREAANIMATION, WM_SETTINGCHANGE'i kategorisi bilinmeyen bir ayar olarak (General) bildirir.
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Accessibility or UserPreferenceCategory.Desktop)) return;
            bool old = Enabled;
            _system = null;
            if (_override is null && Enabled != old) OnChanged();
        };
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

    /// <summary>Whether <paramref name="control"/> should animate now: animations are on, it is visible and not in a designer.</summary>
    public static bool CanAnimate(Control control) =>
        Enabled && control is not null && control.IsHandleCreated && control.Visible && control.Site?.DesignMode != true;

    /// <summary>
    /// Standard durations (ms) for in-control animations; 1 ms each when animations are off, so a change that waits for
    /// one ends at once.
    /// </summary>
    public static class Durations
    {
        /// <summary>120 ms: hover and press feedback, small fades.</summary>
        public static int Xs => Pick(120);

        /// <summary>180 ms: focus rings, toggles, popovers and menus.</summary>
        public static int S => Pick(180);

        /// <summary>250 ms: sliding indicators, panels and values that move.</summary>
        public static int M => Pick(250);

        /// <summary>450 ms: entrances such as charts and notifications.</summary>
        public static int L => Pick(450);

        private static int Pick(int durationMs) => Enabled ? durationMs : 1;
    }

    /// <summary>
    /// Applies the timing curve to linear progress <paramref name="t"/> (clamped to 0–1). The result is 0 at the start and
    /// 1 at the end; <see cref="TwEasing.Spring"/> goes above 1 for a moment in between.
    /// </summary>
    public static float Ease(TwEasing easing, float t)
    {
        t = Math.Max(0f, Math.Min(1f, t));
        switch (easing)
        {
            case TwEasing.Linear:
                return t;
            case TwEasing.In:
                return t * t * t;
            case TwEasing.InOut:
                if (t < 0.5f) return 4 * t * t * t;
                float rest = -2 * t + 2;
                return 1 - rest * rest * rest / 2;
            case TwEasing.Spring:
                // Sönümlü kosinüs (en çok ~%10 taşma); son terim t = 1'de tam 1 vermesi için e^-8 artığını siler.
                if (t >= 1f) return 1f;
                return (float)(1 - Math.Exp(-8 * t) * Math.Cos(3 * Math.PI * t) - t * Math.Exp(-8));
            default:
                float inverse = 1 - t;
                return 1 - inverse * inverse * inverse;
        }
    }

    internal static float EaseOut(float t) => Ease(TwEasing.Out, t);

    /// <summary>The rectangle <paramref name="t"/> of the way from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static RectangleF Lerp(RectangleF from, RectangleF to, float t) => new RectangleF(
        from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t,
        from.Width + (to.Width - from.Width) * t, from.Height + (to.Height - from.Height) * t);

    /// <summary>
    /// The color <paramref name="t"/> of the way from <paramref name="from"/> to <paramref name="to"/> (alpha included);
    /// channels are clamped to 0–255 when <paramref name="t"/> overshoots.
    /// </summary>
    public static Color Blend(Color from, Color to, float t) => Color.FromArgb(
        Channel(from.A + (to.A - from.A) * t), Channel(from.R + (to.R - from.R) * t),
        Channel(from.G + (to.G - from.G) * t), Channel(from.B + (to.B - from.B) * t));

    internal static int Channel(double value) => value <= 0 ? 0 : value >= 255 ? 255 : (int)value;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(int action, int param, out bool value, int winIni);
}

/// <summary>
/// Progress of one value over time for a custom control: call <see cref="Start(int, TwEasing, int)"/> when the value changes,
/// read <see cref="Progress"/> in <c>OnPaint</c>. The owner is invalidated every frame (about 60 per second) until the end.
/// When the owner cannot animate (<see cref="TwMotion.CanAnimate"/>) the change is immediate (<see cref="Progress"/> is 1).
/// <code>
/// _tween.Start(200, TwEasing.InOut);
/// // OnPaint: var x = _from + (_to - _from) * _tween.Progress;
/// </code>
/// </summary>
public sealed class TwTween : IDisposable
{
    private readonly Control _owner;
    private readonly TwFrameTimer _timer = new TwFrameTimer();
    private DateTime _start;
    private TwTiming _timing;

    public TwTween(Control owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _timer.Tick += (_, _) =>
        {
            bool done = Elapsed >= _timing.TotalMs;
            if (done) _timer.Stop();
            Tick?.Invoke(this, EventArgs.Empty);
            _owner.Invalidate();
            if (done) Completed?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>Every frame while running, before the owner is invalidated (to drive a property such as a form's Opacity).</summary>
    public event EventHandler? Tick;

    /// <summary>Whether an animation is running.</summary>
    public bool Running => _timer.Enabled;

    /// <summary>
    /// Progress with the curve applied: 0 during the delay, 1 without animation or at the end, between 0 and 1 meanwhile
    /// (curves that overshoot, such as <see cref="TwEasing.Spring"/>, go above 1 for a moment).
    /// </summary>
    public float Progress => _timing.TotalMs > 0 && Elapsed < _timing.TotalMs ? _timing.Progress(Elapsed) : 1f;

    /// <summary>When an animation reaches its end (not when it is stopped or finishes immediately).</summary>
    public event EventHandler? Completed;

    private double Elapsed => (DateTime.UtcNow - _start).TotalMilliseconds;

    /// <summary>Starts a new ease-out animation; finishes immediately if the owner cannot animate.</summary>
    public void Start(int durationMs) => Start(new TwTiming(durationMs));

    /// <summary>Starts a new animation; finishes immediately if the owner cannot animate.</summary>
    /// <param name="durationMs">Length of the animation (ms), e.g. <see cref="TwMotion.Durations"/>.</param>
    /// <param name="easing">Timing curve.</param>
    public void Start(int durationMs, TwEasing easing) => Start(new TwTiming(durationMs, 0, easing));

    /// <summary>Starts a new animation; finishes immediately if the owner cannot animate.</summary>
    /// <param name="durationMs">Length of the animation (ms), e.g. <see cref="TwMotion.Durations"/>.</param>
    /// <param name="easing">Timing curve.</param>
    /// <param name="delayMs">Wait before it starts moving (ms); <see cref="Progress"/> is 0 meanwhile.</param>
    public void Start(int durationMs, TwEasing easing, int delayMs) => Start(new TwTiming(durationMs, delayMs, easing));

    /// <summary>Starts a new animation with a custom curve; finishes immediately if the owner cannot animate.</summary>
    /// <param name="durationMs">Length of the animation (ms), e.g. <see cref="TwMotion.Durations"/>.</param>
    /// <param name="curve">Timing curve (CSS <c>cubic-bezier</c>).</param>
    public void Start(int durationMs, TwCubicBezier curve) => Start(new TwTiming(durationMs, 0, curve));

    /// <summary>Starts a new animation with a custom curve; finishes immediately if the owner cannot animate.</summary>
    /// <param name="durationMs">Length of the animation (ms), e.g. <see cref="TwMotion.Durations"/>.</param>
    /// <param name="curve">Timing curve (CSS <c>cubic-bezier</c>).</param>
    /// <param name="delayMs">Wait before it starts moving (ms); <see cref="Progress"/> is 0 meanwhile.</param>
    public void Start(int durationMs, TwCubicBezier curve, int delayMs) => Start(new TwTiming(durationMs, delayMs, curve));

    internal void Start(TwTiming timing)
    {
        if (!TwMotion.CanAnimate(_owner) || timing.TotalMs <= 0)
        {
            Stop();
            return;
        }
        _start = DateTime.UtcNow;
        _timing = timing;
        _timer.Start();
    }

    /// <summary>Ends the animation; <see cref="Progress"/> becomes 1.</summary>
    public void Stop()
    {
        _timing = TwTiming.None;
        _timer.Stop();
    }

    public void Dispose() => _timer.Dispose();
}
