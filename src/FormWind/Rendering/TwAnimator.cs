using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

internal enum TwColorChannel
{
    Back,
    Fore,
    Border,
}

/// <summary>
/// Animates a number of a control (a position, a width, an opacity) and the color transitions of the <c>transition</c>
/// classes. A single clock per thread (about 60 frames/s) runs all animations with their curve and delay. A new animation
/// of the same control and name replaces the running one; to continue from where it is, pass the last value given to
/// <c>set</c> as <c>from</c>. All FormWind animations share one frame clock per UI thread that does not depend on
/// <c>WM_TIMER</c>, so a control that keeps repainting (a chart opening, a pulsing skeleton) does not stop the others;
/// only a busy UI thread delays frames, and the value then jumps to where it should be by time.
/// <code>
/// TwAnimator.Animate(panel, "width", panel.Width, 320, TwMotion.Durations.M, w => panel.Width = (int)w);
/// </code>
/// </summary>
public static class TwAnimator
{
    [ThreadStatic]
    private static Dictionary<(Control Control, object Key), Animation>? _active;

    [ThreadStatic]
    private static TwFrameTimer? _timer;

    internal static int ActiveCount => _active?.Count ?? 0;

    /// <summary>
    /// Animates a number from <paramref name="from"/> to <paramref name="to"/>, giving each frame's value to
    /// <paramref name="set"/>. When the control cannot animate (<see cref="TwMotion.CanAnimate"/>) or the duration is 0,
    /// <paramref name="set"/> gets <paramref name="to"/> at once.
    /// </summary>
    /// <param name="control">Control whose animation this is; it stops when the control is disposed.</param>
    /// <param name="name">Name of the animated value; a new animation with the same control and name replaces the running one.</param>
    /// <param name="from">Start value.</param>
    /// <param name="to">End value; the last value given to <paramref name="set"/>.</param>
    /// <param name="durationMs">Length of the animation (ms), e.g. <see cref="TwMotion.Durations"/>.</param>
    /// <param name="set">Applies a value (called on the UI thread every frame).</param>
    public static void Animate(Control control, string name, double from, double to, int durationMs, Action<double> set) =>
        Animate(control, name, from, to, new TwTiming(durationMs), set, null);

    /// <summary>Animates a number with a timing curve (see <see cref="Animate(Control, string, double, double, int, Action{double})"/>).</summary>
    /// <param name="control">Control whose animation this is; it stops when the control is disposed.</param>
    /// <param name="name">Name of the animated value; a new animation with the same control and name replaces the running one.</param>
    /// <param name="from">Start value.</param>
    /// <param name="to">End value; the last value given to <paramref name="set"/>.</param>
    /// <param name="durationMs">Length of the animation (ms), e.g. <see cref="TwMotion.Durations"/>.</param>
    /// <param name="set">Applies a value (called on the UI thread every frame).</param>
    /// <param name="easing">Timing curve; with <see cref="TwEasing.Spring"/> values go past the end value for a moment.</param>
    public static void Animate(Control control, string name, double from, double to, int durationMs, Action<double> set, TwEasing easing) =>
        Animate(control, name, from, to, new TwTiming(durationMs, 0, easing), set, null);

    /// <summary>Animates a number with a timing curve and a delay (see <see cref="Animate(Control, string, double, double, int, Action{double})"/>).</summary>
    /// <param name="control">Control whose animation this is; it stops when the control is disposed.</param>
    /// <param name="name">Name of the animated value; a new animation with the same control and name replaces the running one.</param>
    /// <param name="from">Start value.</param>
    /// <param name="to">End value; the last value given to <paramref name="set"/>.</param>
    /// <param name="durationMs">Length of the animation (ms), e.g. <see cref="TwMotion.Durations"/>.</param>
    /// <param name="set">Applies a value (called on the UI thread every frame).</param>
    /// <param name="easing">Timing curve; with <see cref="TwEasing.Spring"/> values go past the end value for a moment.</param>
    /// <param name="delayMs">Wait before it starts moving (ms).</param>
    /// <param name="completed">Called when it reaches the end value (also when that happens at once), not when it is replaced or cancelled.</param>
    public static void Animate(Control control, string name, double from, double to, int durationMs, Action<double> set,
        TwEasing easing, int delayMs, Action? completed) =>
        Animate(control, name, from, to, new TwTiming(durationMs, delayMs, easing), set, completed);

    /// <summary>Animates a number with a custom curve (see <see cref="Animate(Control, string, double, double, int, Action{double})"/>).</summary>
    /// <param name="control">Control whose animation this is; it stops when the control is disposed.</param>
    /// <param name="name">Name of the animated value; a new animation with the same control and name replaces the running one.</param>
    /// <param name="from">Start value.</param>
    /// <param name="to">End value; the last value given to <paramref name="set"/>.</param>
    /// <param name="durationMs">Length of the animation (ms), e.g. <see cref="TwMotion.Durations"/>.</param>
    /// <param name="set">Applies a value (called on the UI thread every frame).</param>
    /// <param name="curve">Timing curve (CSS <c>cubic-bezier</c>).</param>
    public static void Animate(Control control, string name, double from, double to, int durationMs, Action<double> set, TwCubicBezier curve) =>
        Animate(control, name, from, to, new TwTiming(durationMs, 0, curve), set, null);

    /// <summary>Animates a number with a custom curve and a delay (see <see cref="Animate(Control, string, double, double, int, Action{double})"/>).</summary>
    /// <param name="control">Control whose animation this is; it stops when the control is disposed.</param>
    /// <param name="name">Name of the animated value; a new animation with the same control and name replaces the running one.</param>
    /// <param name="from">Start value.</param>
    /// <param name="to">End value; the last value given to <paramref name="set"/>.</param>
    /// <param name="durationMs">Length of the animation (ms), e.g. <see cref="TwMotion.Durations"/>.</param>
    /// <param name="set">Applies a value (called on the UI thread every frame).</param>
    /// <param name="curve">Timing curve (CSS <c>cubic-bezier</c>).</param>
    /// <param name="delayMs">Wait before it starts moving (ms).</param>
    /// <param name="completed">Called when it reaches the end value (also when that happens at once), not when it is replaced or cancelled.</param>
    public static void Animate(Control control, string name, double from, double to, int durationMs, Action<double> set,
        TwCubicBezier curve, int delayMs, Action? completed) =>
        Animate(control, name, from, to, new TwTiming(durationMs, delayMs, curve), set, completed);

    private static void Animate(Control control, string name, double from, double to, TwTiming timing, Action<double> set, Action? completed)
    {
        if (control is null) throw new ArgumentNullException(nameof(control));
        if (name is null) throw new ArgumentNullException(nameof(name));
        if (set is null) throw new ArgumentNullException(nameof(set));

        if (!TwMotion.CanAnimate(control) || timing.TotalMs <= 0 || from.Equals(to))
        {
            Cancel(control, name);
            set(to);
            completed?.Invoke();
            return;
        }
        Start(control, name, to, timing, progress => set(progress >= 1f ? to : from + (to - from) * progress), completed);
    }

    /// <summary>Whether a number of <paramref name="control"/> named <paramref name="name"/> is animating.</summary>
    public static bool IsAnimating(Control control, string name) => _active != null && _active.ContainsKey((control, name));

    /// <summary>Stops the animation where it is (<c>completed</c> is not called).</summary>
    public static void Cancel(Control control, string name) => _active?.Remove((control, name));

    internal static bool IsAnimating(Control control, TwColorChannel channel) =>
        _active != null && _active.ContainsKey((control, channel));

    /// <summary>Animates the control's BackColor or ForeColor with an ease-out curve.</summary>
    internal static void Animate(Control control, TwColorChannel channel, Color target, int durationMs, Action? completed = null) =>
        Animate(control, channel, target, new TwTiming(durationMs), completed);

    /// <summary>Animates the control's BackColor or ForeColor.</summary>
    internal static void Animate(Control control, TwColorChannel channel, Color target, TwTiming timing, Action? completed = null)
    {
        var current = channel == TwColorChannel.Back ? control.BackColor : control.ForeColor;
        Animate(control, channel, current, target, timing, color => Set(control, channel, color), completed);
    }

    /// <summary>Animates a color shown by <paramref name="set"/> (e.g. the border of the control's chrome).</summary>
    internal static void Animate(Control control, TwColorChannel channel, Color from, Color target, TwTiming timing, Action<Color> set, Action? completed = null)
    {
        if (timing.TotalMs <= 0 || from.ToArgb() == target.ToArgb())
        {
            Cancel(control, channel);
            set(target);
            completed?.Invoke();
            return;
        }
        Start(control, channel, target, timing, progress => set(Lerp(from, target, progress)), completed);
    }

    internal static void Cancel(Control control, TwColorChannel channel) => _active?.Remove((control, channel));

    /// <summary>The target color the channel is currently transitioning to.</summary>
    internal static bool TryGetTarget(Control control, TwColorChannel channel, out Color target)
    {
        if (_active != null && _active.TryGetValue((control, channel), out var animation))
        {
            target = (Color)animation.Target;
            return true;
        }
        target = Color.Empty;
        return false;
    }

    private static void Start(Control control, object key, object target, TwTiming timing, Action<float> apply, Action? completed)
    {
        _active ??= new Dictionary<(Control, object), Animation>();
        _active[(control, key)] = new Animation(target, DateTime.UtcNow, timing, apply, completed);

        if (_timer is null)
        {
            _timer = new TwFrameTimer();
            _timer.Tick += (_, _) => Step(DateTime.UtcNow);
        }
        _timer.Start();
    }

    /// <summary>Advances the animations to <paramref name="now"/> (used by the timer and tests).</summary>
    internal static void Step(DateTime now)
    {
        if (_active is null || _active.Count == 0)
        {
            _timer?.Stop();
            return;
        }

        var finished = new List<((Control Control, object Key) Key, Animation Animation)>();
        // set geri çağrısı yeni bir animasyon başlatabilir ya da iptal edebilir; sözlük üzerinde değil kopyada dönülür.
        foreach (var pair in new List<KeyValuePair<(Control Control, object Key), Animation>>(_active))
        {
            var animation = pair.Value;
            if (pair.Key.Control.IsDisposed)
            {
                _active.Remove(pair.Key);
                continue;
            }

            var elapsed = (now - animation.Start).TotalMilliseconds;
            bool done = elapsed >= animation.Timing.TotalMs;
            animation.Apply(done ? 1f : animation.Timing.Progress(elapsed));
            if (done) finished.Add((pair.Key, animation));
        }

        foreach (var (key, animation) in finished)
        {
            // Apply sırasında yerine yenisi konduysa (ya da iptal edildiyse) bitmiş sayılmaz.
            if (!_active.TryGetValue(key, out var current) || !ReferenceEquals(current, animation)) continue;
            _active.Remove(key);
            if (!key.Control.IsDisposed) animation.Completed?.Invoke();
        }

        if (_active.Count == 0) _timer?.Stop();
    }

    private static void Set(Control control, TwColorChannel channel, Color color)
    {
        try
        {
            if (channel == TwColorChannel.Back) control.BackColor = color;
            else control.ForeColor = color;
        }
        catch (ArgumentException)
        {
            // Saydam arka planı desteklemeyen kontrol.
        }
    }

    private static Color Lerp(Color from, Color to, double amount) => Color.FromArgb(
        TwMotion.Channel(Math.Round(from.A + (to.A - from.A) * amount)),
        TwMotion.Channel(Math.Round(from.R + (to.R - from.R) * amount)),
        TwMotion.Channel(Math.Round(from.G + (to.G - from.G) * amount)),
        TwMotion.Channel(Math.Round(from.B + (to.B - from.B) * amount)));

    private sealed class Animation
    {
        public Animation(object target, DateTime start, TwTiming timing, Action<float> apply, Action? completed)
        {
            Target = target;
            Start = start;
            Timing = timing;
            Apply = apply;
            Completed = completed;
        }

        public object Target { get; }
        public DateTime Start { get; }
        public TwTiming Timing { get; }
        public Action<float> Apply { get; }
        public Action? Completed { get; }
    }
}
