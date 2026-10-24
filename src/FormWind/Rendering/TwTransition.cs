using System;

namespace FormWind;

/// <summary>Timing curve of a transition (<c>ease-out</c>, <c>ease-linear</c>, <c>ease-in</c>, <c>ease-in-out</c>).</summary>
public enum TwEasing
{
    /// <summary>Starts fast and slows down (<c>ease-out</c>); the default.</summary>
    Out = 0,

    /// <summary>Constant speed (<c>ease-linear</c>).</summary>
    Linear = 1,

    /// <summary>Starts slow and speeds up (<c>ease-in</c>).</summary>
    In = 2,

    /// <summary>Slow at both ends (<c>ease-in-out</c>).</summary>
    InOut = 3,
}

/// <summary>Which changes a <c>transition</c> class animates.</summary>
[Flags]
public enum TwTransitionProperty
{
    None = 0,

    /// <summary>Background, text and border colors (<c>transition-colors</c>).</summary>
    Colors = 1,

    /// <summary>Shadows and the focus ring (<c>transition-shadow</c>).</summary>
    Shadow = 2,

    /// <summary>Everything that can be animated (<c>transition</c>, <c>transition-all</c>).</summary>
    All = Colors | Shadow,
}

/// <summary>A repeating animation (<c>animate-*</c>).</summary>
public enum TwAnimation
{
    /// <summary><c>animate-none</c>.</summary>
    None = 0,

    /// <summary><c>animate-spin</c>: one full turn per second (loading indicators).</summary>
    Spin = 1,

    /// <summary><c>animate-pulse</c>: fades to half opacity and back every 2 seconds (placeholders).</summary>
    Pulse = 2,
}

/// <summary>
/// The timing classes of a control with its own animation (switch knob, tab indicator, list row hover): <c>duration-*</c>,
/// <c>delay-*</c> and <c>ease-*</c> replace the control's defaults, <c>transition-none</c> makes the change immediate.
/// </summary>
internal readonly struct TwTimingClasses
{
    public TwTimingClasses(bool? transition, int? durationMs, int? delayMs, TwEasing? easing)
    {
        Transition = transition;
        DurationMs = durationMs;
        DelayMs = delayMs;
        Easing = easing;
    }

    public bool? Transition { get; }
    public int? DurationMs { get; }
    public int? DelayMs { get; }
    public TwEasing? Easing { get; }

    /// <summary>The control's animation with these classes; <paramref name="defaultDurationMs"/> when no <c>duration-*</c>.</summary>
    public TwTransition For(int defaultDurationMs) => Transition == false
        ? TwTransition.None
        : new TwTransition(DurationMs ?? defaultDurationMs, DelayMs ?? 0, Easing ?? TwEasing.Out);
}

/// <summary>A control whose own animation follows the timing classes (see <see cref="TwTimingClasses"/>).</summary>
internal interface ITwTimed
{
    void SetTiming(TwTimingClasses timing);
}

/// <summary>Timing of a class transition: what is animated, how long, after what delay and with which curve.</summary>
internal readonly struct TwTransition
{
    /// <summary>Tailwind's default duration (ms).</summary>
    public const int DefaultDuration = 150;

    public static readonly TwTransition None = default;

    public TwTransition(int durationMs, int delayMs = 0, TwEasing easing = TwEasing.Out, TwTransitionProperty properties = TwTransitionProperty.All)
    {
        DurationMs = Math.Max(0, durationMs);
        DelayMs = Math.Max(0, delayMs);
        Easing = easing;
        Properties = properties;
    }

    public int DurationMs { get; }
    public int DelayMs { get; }
    public TwEasing Easing { get; }
    public TwTransitionProperty Properties { get; }

    /// <summary>Delay plus duration (ms).</summary>
    public int TotalMs => DelayMs + DurationMs;

    /// <summary>Whether changes of <paramref name="property"/> take time (otherwise they are immediate).</summary>
    public bool Animates(TwTransitionProperty property) => (Properties & property) != 0 && TotalMs > 0;

    /// <summary>Eased progress (0–1) <paramref name="elapsedMs"/> after the start; 0 during the delay.</summary>
    public float Progress(double elapsedMs)
    {
        if (elapsedMs < DelayMs) return 0f;
        if (DurationMs <= 0) return 1f;
        return TwMotion.Ease(Easing, (float)((elapsedMs - DelayMs) / DurationMs));
    }
}
