using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

internal enum TwColorChannel
{
    Back,
    Fore,
}

/// <summary>
/// Color transitions for the <c>transition</c> class. A single timer per thread (about 60 frames/s) runs all
/// animations with an ease-out curve. When a channel gets a new target, the transition continues from the
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

    public static void Animate(Control control, TwColorChannel channel, Color target, int durationMs, Action? completed = null)
    {
        var current = channel == TwColorChannel.Back ? control.BackColor : control.ForeColor;
        if (durationMs <= 0 || current.ToArgb() == target.ToArgb())
        {
            Cancel(control, channel);
            Set(control, channel, target);
            completed?.Invoke();
            return;
        }

        _active ??= new Dictionary<(Control, TwColorChannel), Animation>();
        _active[(control, channel)] = new Animation(current, target, DateTime.UtcNow, durationMs, completed);

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
            var (control, channel) = (pair.Key.Control, pair.Key.Channel);
            var animation = pair.Value;
            if (control.IsDisposed)
            {
                finished.Add(pair.Key);
                continue;
            }

            var t = Math.Min(1.0, (now - animation.Start).TotalMilliseconds / animation.DurationMs);
            var eased = 1 - Math.Pow(1 - t, 3);
            Set(control, channel, Lerp(animation.From, animation.To, eased));
            if (t >= 1) finished.Add(pair.Key);
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
        public Animation(Color from, Color to, DateTime start, int durationMs, Action? completed)
        {
            From = from;
            To = to;
            Start = start;
            DurationMs = durationMs;
            Completed = completed;
        }

        public Color From { get; }
        public Color To { get; }
        public DateTime Start { get; }
        public int DurationMs { get; }
        public Action? Completed { get; }
    }
}
