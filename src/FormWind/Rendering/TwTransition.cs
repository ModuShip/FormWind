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

    /// <summary>
    /// Shoots about 10% past the target, swings back and settles (a damped spring). Progress goes above 1 for a moment, so
    /// values drawn with it should allow that (colors are clamped).
    /// </summary>
    Spring = 4,
}

/// <summary>
/// A custom timing curve like CSS <c>cubic-bezier(x1, y1, x2, y2)</c>: the curve starts at (0, 0) and ends at (1, 1) with
/// control points (x1, y1) and (x2, y2). x is time and must stay between 0 and 1; y is progress and may go outside 0–1
/// (overshoot).
/// <code>
/// var curve = new TwCubicBezier(0.34, 1.56, 0.64, 1);   // back-out
/// _tween.Start(250, curve);
/// </code>
/// </summary>
public readonly struct TwCubicBezier : IEquatable<TwCubicBezier>
{
    private const int NewtonIterations = 8;
    private const double Epsilon = 1e-6;

    public TwCubicBezier(double x1, double y1, double x2, double y2)
    {
        if (double.IsNaN(x1) || x1 < 0 || x1 > 1) throw new ArgumentOutOfRangeException(nameof(x1), x1, "x1 must be between 0 and 1.");
        if (double.IsNaN(x2) || x2 < 0 || x2 > 1) throw new ArgumentOutOfRangeException(nameof(x2), x2, "x2 must be between 0 and 1.");
        if (double.IsNaN(y1) || double.IsInfinity(y1)) throw new ArgumentOutOfRangeException(nameof(y1), y1, "y1 must be a finite number.");
        if (double.IsNaN(y2) || double.IsInfinity(y2)) throw new ArgumentOutOfRangeException(nameof(y2), y2, "y2 must be a finite number.");
        X1 = x1;
        Y1 = y1;
        X2 = x2;
        Y2 = y2;
    }

    public double X1 { get; }
    public double Y1 { get; }
    public double X2 { get; }
    public double Y2 { get; }

    /// <summary>Progress at linear time <paramref name="t"/> (clamped to 0–1); 0 at the start, 1 at the end.</summary>
    public float Ease(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return (float)Sample(Y1, Y2, SolveX(t));
    }

    // Eğri parametresi s için B(s) = 3(1-s)²s·p1 + 3(1-s)s²·p2 + s³.
    private static double Sample(double p1, double p2, double s) => ((1 - 3 * p2 + 3 * p1) * s + (3 * p2 - 6 * p1)) * s * s + 3 * p1 * s;

    private static double Slope(double p1, double p2, double s) => 3 * (1 - 3 * p2 + 3 * p1) * s * s + 2 * (3 * p2 - 6 * p1) * s + 3 * p1;

    // x(s) = t'yi çözer: önce Newton, eğim düzse ikiye bölme (x tekdüzedir, çünkü x1, x2 0–1 arasında).
    private double SolveX(double t)
    {
        double s = t;
        for (int i = 0; i < NewtonIterations; i++)
        {
            double error = Sample(X1, X2, s) - t;
            if (Math.Abs(error) < Epsilon) return s;
            double slope = Slope(X1, X2, s);
            if (Math.Abs(slope) < Epsilon) break;
            s -= error / slope;
        }

        double low = 0, high = 1;
        s = t;
        while (high - low > Epsilon)
        {
            double x = Sample(X1, X2, s);
            if (Math.Abs(x - t) < Epsilon) break;
            if (x < t) low = s;
            else high = s;
            s = (low + high) / 2;
        }
        return s;
    }

    public bool Equals(TwCubicBezier other) => X1 == other.X1 && Y1 == other.Y1 && X2 == other.X2 && Y2 == other.Y2;

    public override bool Equals(object? obj) => obj is TwCubicBezier other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = X1.GetHashCode();
            hash = hash * 31 + Y1.GetHashCode();
            hash = hash * 31 + X2.GetHashCode();
            return hash * 31 + Y2.GetHashCode();
        }
    }

    public static bool operator ==(TwCubicBezier left, TwCubicBezier right) => left.Equals(right);

    public static bool operator !=(TwCubicBezier left, TwCubicBezier right) => !left.Equals(right);

    public override string ToString() => string.Format(System.Globalization.CultureInfo.InvariantCulture, "cubic-bezier({0}, {1}, {2}, {3})", X1, Y1, X2, Y2);
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
        Curve = null;
    }

    /// <summary>A transition with a custom curve (<see cref="Easing"/> is ignored).</summary>
    public TwTransition(int durationMs, int delayMs, TwCubicBezier curve, TwTransitionProperty properties = TwTransitionProperty.All)
        : this(durationMs, delayMs, TwEasing.Out, properties) => Curve = curve;

    public int DurationMs { get; }
    public int DelayMs { get; }
    public TwEasing Easing { get; }
    public TwTransitionProperty Properties { get; }

    /// <summary>Custom curve used instead of <see cref="Easing"/>.</summary>
    public TwCubicBezier? Curve { get; }

    /// <summary>Delay plus duration (ms).</summary>
    public int TotalMs => DelayMs + DurationMs;

    /// <summary>Whether changes of <paramref name="property"/> take time (otherwise they are immediate).</summary>
    public bool Animates(TwTransitionProperty property) => (Properties & property) != 0 && TotalMs > 0;

    /// <summary>
    /// Eased progress <paramref name="elapsedMs"/> after the start: 0 during the delay, 1 at the end; between them it can
    /// leave 0–1 for curves that overshoot (<see cref="TwEasing.Spring"/>, a <see cref="TwCubicBezier"/>).
    /// </summary>
    public float Progress(double elapsedMs)
    {
        if (elapsedMs < DelayMs) return 0f;
        if (DurationMs <= 0) return 1f;
        float t = (float)((elapsedMs - DelayMs) / DurationMs);
        return Curve is TwCubicBezier curve ? curve.Ease(t) : TwMotion.Ease(Easing, t);
    }
}
