using System.Diagnostics.CodeAnalysis;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FormWind;

public enum TwShadowSize
{
    None,
    Sm,
    Base,
    Md,
    Lg,
    Xl,
    Xl2,
}

public enum TwGradientDirection
{
    ToRight,
    ToLeft,
    ToTop,
    ToBottom,
    ToTopRight,
    ToTopLeft,
    ToBottomRight,
    ToBottomLeft,
}

/// <summary>A Tailwind gradient: <c>from</c>, an optional <c>via</c> and <c>to</c>. A missing end is the transparent version of the other end.</summary>
public readonly struct TwGradient : IEquatable<TwGradient>
{
    public TwGradient(TwGradientDirection direction, Color? from, Color? via, Color? to)
    {
        Direction = direction;
        var start = from ?? Color.FromArgb(0, to ?? Color.Black);
        From = start;
        Via = via;
        To = to ?? Color.FromArgb(0, via ?? start);
    }

    public TwGradientDirection Direction { get; }
    public Color From { get; }
    public Color? Via { get; }
    public Color To { get; }

    [Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
    public static TwGradient? FromStyle(TwStyle style) =>
        style.HasGradient
            ? new TwGradient(style.GradientDirection!.Value, style.GradientFrom, style.GradientVia, style.GradientTo)
            : null;

    public Brush CreateBrush(Rectangle bounds)
    {
        var (start, end) = Direction switch
        {
            TwGradientDirection.ToRight => (new PointF(bounds.Left, 0), new PointF(bounds.Right, 0)),
            TwGradientDirection.ToLeft => (new PointF(bounds.Right, 0), new PointF(bounds.Left, 0)),
            TwGradientDirection.ToTop => (new PointF(0, bounds.Bottom), new PointF(0, bounds.Top)),
            TwGradientDirection.ToBottom => (new PointF(0, bounds.Top), new PointF(0, bounds.Bottom)),
            TwGradientDirection.ToTopRight => (new PointF(bounds.Left, bounds.Bottom), new PointF(bounds.Right, bounds.Top)),
            TwGradientDirection.ToTopLeft => (new PointF(bounds.Right, bounds.Bottom), new PointF(bounds.Left, bounds.Top)),
            TwGradientDirection.ToBottomRight => (new PointF(bounds.Left, bounds.Top), new PointF(bounds.Right, bounds.Bottom)),
            _ => (new PointF(bounds.Right, bounds.Top), new PointF(bounds.Left, bounds.Bottom)),
        };
        if (start == end) end = new PointF(end.X + 1, end.Y + 1);

        var brush = new LinearGradientBrush(start, end, From, To);
        if (Via is Color via)
        {
            brush.InterpolationColors = new ColorBlend(3)
            {
                Colors = new[] { From, via, To },
                Positions = new[] { 0f, 0.5f, 1f },
            };
        }
        return brush;
    }

    public bool Equals(TwGradient other) =>
        Direction == other.Direction && From.ToArgb() == other.From.ToArgb()
        && Via?.ToArgb() == other.Via?.ToArgb() && To.ToArgb() == other.To.ToArgb();

    public override bool Equals(object? obj) => obj is TwGradient other && Equals(other);

    public override int GetHashCode() => ((int)Direction * 397) ^ From.ToArgb() ^ (To.ToArgb() * 31);
}

/// <summary>Surface information passed to owner-drawn controls: corners, per-side borders and an optional gradient. Values are DPI-scaled.</summary>
public readonly struct TwChrome : IEquatable<TwChrome>
{
    public TwChrome(float radius, float left, float top, float right, float bottom, Color borderColor, TwGradient? gradient = null)
    {
        Radius = radius;
        BorderLeft = left;
        BorderTop = top;
        BorderRight = right;
        BorderBottom = bottom;
        BorderColor = borderColor;
        Gradient = gradient;
    }

    public static TwChrome None => new TwChrome(0, 0, 0, 0, 0, Color.Empty);

    public float Radius { get; }
    public float BorderLeft { get; }
    public float BorderTop { get; }
    public float BorderRight { get; }
    public float BorderBottom { get; }
    public Color BorderColor { get; }
    public TwGradient? Gradient { get; }

    public bool HasBorder => BorderLeft > 0 || BorderTop > 0 || BorderRight > 0 || BorderBottom > 0;

    public bool IsUniformBorder => BorderLeft == BorderTop && BorderTop == BorderRight && BorderRight == BorderBottom;

    [Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
    public static TwChrome FromStyle(TwStyle style, float scale, Color defaultBorderColor)
    {
        float all = style.BorderWidth ?? 0;
        float Side(int? side) => (side ?? all) * scale;
        return new TwChrome(
            (style.Radius ?? 0) * scale,
            Side(style.BorderSides.Left),
            Side(style.BorderSides.Top),
            Side(style.BorderSides.Right),
            Side(style.BorderSides.Bottom),
            style.BorderColor ?? defaultBorderColor,
            TwGradient.FromStyle(style));
    }

    /// <summary>The same chrome with another border color (a frame of a border color transition).</summary>
    internal TwChrome WithBorderColor(Color color) =>
        new TwChrome(Radius, BorderLeft, BorderTop, BorderRight, BorderBottom, color, Gradient);

    public bool Equals(TwChrome other) =>
        Radius == other.Radius && BorderLeft == other.BorderLeft && BorderTop == other.BorderTop
        && BorderRight == other.BorderRight && BorderBottom == other.BorderBottom
        && BorderColor.ToArgb() == other.BorderColor.ToArgb() && Nullable.Equals(Gradient, other.Gradient);

    public override bool Equals(object? obj) => obj is TwChrome other && Equals(other);

    public override int GetHashCode() => Radius.GetHashCode() ^ BorderColor.ToArgb() ^ BorderTop.GetHashCode();
}
