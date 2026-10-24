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
/// Color transitions for the <c>transition</c> classes. A single timer per thread (about 60 frames/s) runs all
/// animations with the transition's curve and delay. When a channel gets a new target, the transition continues from the
/// current color. Windows generates <c>WM_TIMER</c> only when no other message is queued: while a window keeps
/// invalidating itself during paint, or the UI thread is busy, transitions pause at an intermediate color
/// (<see cref="TryGetTarget"/> still gives the target).
/// </summary>
internal static class TwAnimator
{
    [ThreadStatic]
    private static Dictionary<(Control Control, TwColorChannel Channel), Animation>? _active;

    [ThreadStatic]
    private static Timer? _timer;

    public static int ActiveCount => _active?.Count ?? 0;

    public static bool IsAnimating(Control control, TwColorChannel channel) =>
        _active != null && _active.ContainsKey((control, channel));

    /// <summary>Animates the control's BackColor or ForeColor with an ease-out curve.</summary>
    public static void Animate(Control control, TwColorChannel channel, Color target, int durationMs, Action? completed = null) =>
        Animate(control, channel, target, new TwTransition(durationMs), completed);

    /// <summary>Animates the control's BackColor or ForeColor.</summary>
    public static void Animate(Control control, TwColorChannel channel, Color target, TwTransition timing, Action? completed = null)
    {
        var current = channel == TwColorChannel.Back ? control.BackColor : control.ForeColor;
        Animate(control, channel, current, target, timing, color => Set(control, channel, color), completed);
    }

    /// <summary>Animates a color shown by <paramref name="set"/> (e.g. the border of the control's chrome).</summary>
    public static void Animate(Control control, TwColorChannel channel, Color from, Color target, TwTransition timing, Action<Color> set, Action? completed = null)
    {
        if (timing.TotalMs <= 0 || from.ToArgb() == target.ToArgb())
        {
            Cancel(control, channel);
            set(target);
            completed?.Invoke();
            return;
        }

        _active ??= new Dictionary<(Control, TwColorChannel), Animation>();
        _active[(control, channel)] = new Animation(from, target, DateTime.UtcNow, timing, set, completed);

        if (_timer is null)
        {
            _timer = new Timer { Interval = 16 };
            _timer.Tick += (_, _) => Step(DateTime.UtcNow);
        }
        _timer.Start();
    }

    public static void Cancel(Control control, TwColorChannel channel) => _active?.Remove((control, channel));

    /// <summary>The target color the channel is currently transitioning to.</summary>
    public static bool TryGetTarget(Control control, TwColorChannel channel, out Color target)
    {
        if (_active != null && _active.TryGetValue((control, channel), out var animation))
        {
            target = animation.To;
            return true;
        }
        target = Color.Empty;
        return false;
    }

    /// <summary>Advances the animations to <paramref name="now"/> (used by the timer and tests).</summary>
    internal static void Step(DateTime now)
    {
        if (_active is null || _active.Count == 0)
        {
            _timer?.Stop();
            return;
        }

        var finished = new List<(Control, TwColorChannel)>();
        foreach (var pair in _active)
        {
            var animation = pair.Value;
            if (pair.Key.Control.IsDisposed)
            {
                finished.Add(pair.Key);
                continue;
            }

            var elapsed = (now - animation.Start).TotalMilliseconds;
            animation.Set(Lerp(animation.From, animation.To, animation.Timing.Progress(elapsed)));
            if (elapsed >= animation.Timing.TotalMs) finished.Add(pair.Key);
        }

        foreach (var key in finished)
        {
            var animation = _active[key];
            _active.Remove(key);
            if (!key.Item1.IsDisposed) animation.Completed?.Invoke();
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
        (int)Math.Round(from.A + (to.A - from.A) * amount),
        (int)Math.Round(from.R + (to.R - from.R) * amount),
        (int)Math.Round(from.G + (to.G - from.G) * amount),
        (int)Math.Round(from.B + (to.B - from.B) * amount));

    private sealed class Animation
    {
        public Animation(Color from, Color to, DateTime start, TwTransition timing, Action<Color> set, Action? completed)
        {
            From = from;
            To = to;
            Start = start;
            Timing = timing;
            Set = set;
            Completed = completed;
        }

        public Color From { get; }
        public Color To { get; }
        public DateTime Start { get; }
        public TwTransition Timing { get; }
        public Action<Color> Set { get; }
        public Action? Completed { get; }
    }
}
