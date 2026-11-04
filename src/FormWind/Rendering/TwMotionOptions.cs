using System;

namespace FormWind;

/// <summary>
/// Settings for one transition (<see cref="TwTransition"/>) or window motion (<see cref="TwWindowMotion"/>). Every value is
/// optional; what is not set comes from <see cref="TwMotion.Timings"/> and the method's own default curve.
/// <code>
/// TwWindowMotion.Attach(sheet, TwEdge.Right,
///     enter: new TwMotionOptions { Easing = TwEasing.Spring, Distance = 12 },
///     exit: new TwMotionOptions { Easing = TwEasing.In, Distance = 12 });
/// TwTransition.SlideIn(pnlInspector, TwEdge.Right, new TwMotionOptions { IncludeMargin = true, FadeRest = false });
/// </code>
/// </summary>
public sealed class TwMotionOptions
{
    private int? _durationMs, _distance;

    /// <summary>Length (ms); <c>null</c> takes the matching <see cref="TwMotion.Timings"/> value, 0 makes the change immediate.</summary>
    public int? DurationMs
    {
        get => _durationMs;
        set => _durationMs = value is < 0 ? throw new ArgumentOutOfRangeException(nameof(value), value, "A duration cannot be negative.") : value;
    }

    /// <summary>Timing curve; <c>null</c> keeps the method's default. <see cref="Curve"/> wins when both are set.</summary>
    public TwEasing? Easing { get; set; }

    /// <summary>Custom timing curve (CSS <c>cubic-bezier</c>), used instead of <see cref="Easing"/>.</summary>
    public TwCubicBezier? Curve { get; set; }

    /// <summary>
    /// How far a window slides (96 DPI px) with <see cref="TwWindowMotion"/>; <c>null</c> takes <see cref="TwWindowMotion.Distance"/>,
    /// 0 only fades.
    /// </summary>
    public int? Distance
    {
        get => _distance;
        set => _distance = value is < 0 ? throw new ArgumentOutOfRangeException(nameof(value), value, "A distance cannot be negative.") : value;
    }

    /// <summary>
    /// With <see cref="TwTransition.SlideIn(System.Windows.Forms.Control, TwEdge, TwMotionOptions)"/> and <c>SlideOut</c>: the
    /// panel's <c>Margin</c> slides with it, so the gap beside the panel comes and goes with the panel.
    /// </summary>
    public bool IncludeMargin { get; set; }

    /// <summary>
    /// With panel slides and <see cref="TwTransition.Resize(System.Windows.Forms.Control, int, TwMotionOptions)"/>: whether the
    /// rest of the parent cross-fades to its new layout (the default). <c>false</c> puts the rest in its new layout at once and
    /// only the panel's picture moves, so text that moves (grid columns) is never seen twice.
    /// </summary>
    public bool FadeRest { get; set; } = true;

    internal TwTiming Timing(int defaultDurationMs, TwEasing defaultEasing) => Timing(this, defaultDurationMs, defaultEasing);

    internal static TwTiming Timing(TwMotionOptions? options, int defaultDurationMs, TwEasing defaultEasing)
    {
        int duration = options?.DurationMs ?? defaultDurationMs;
        return options?.Curve is TwCubicBezier curve
            ? new TwTiming(duration, 0, curve)
            : new TwTiming(duration, 0, options?.Easing ?? defaultEasing);
    }
}
