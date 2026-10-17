using System;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Optional pixel values for four sides (padding, margin), at 96 DPI.</summary>
public sealed class TwSides
{
    public int? Left { get; set; }
    public int? Top { get; set; }
    public int? Right { get; set; }
    public int? Bottom { get; set; }

    public bool IsEmpty => Left is null && Top is null && Right is null && Bottom is null;

    public void MergeFrom(TwSides other)
    {
        Left = other.Left ?? Left;
        Top = other.Top ?? Top;
        Right = other.Right ?? Right;
        Bottom = other.Bottom ?? Bottom;
    }

    /// <summary>
    /// Applies the defined sides with the DPI scale and takes undefined sides from <paramref name="fallback"/>.
    /// </summary>
    public Padding ApplyTo(Padding fallback, float scale) => new Padding(
        Left is int left ? Scale(left, scale) : fallback.Left,
        Top is int top ? Scale(top, scale) : fallback.Top,
        Right is int right ? Scale(right, scale) : fallback.Right,
        Bottom is int bottom ? Scale(bottom, scale) : fallback.Bottom);

    // WinForms'un ölçeklemesi gibi .5 yukarı yuvarlanır (2 px %125'te 3 olur, bankacı yuvarlamasıyla 2 olurdu).
    public static int Scale(int pixels, float scale) => (int)Math.Round(pixels * scale, MidpointRounding.AwayFromZero);
}
