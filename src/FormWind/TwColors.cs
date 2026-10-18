using System.Drawing;

namespace FormWind;

/// <summary>
/// Colors resolved from classes for code that paints controls itself (see <see cref="TailwindProvider.GetColors(string, string?, TwState)"/>).
/// A color the classes do not set is <c>null</c>.
/// </summary>
public sealed class TwColors
{
    internal TwColors(TwStyle style)
    {
        Back = style.BackColor;
        Fore = style.ForeColor;
        Border = style.BorderColor;
        Accent = style.AccentColor;
        Fill = style.FillColor;
    }

    /// <summary><c>bg-*</c>.</summary>
    public Color? Back { get; }

    /// <summary><c>text-*</c>.</summary>
    public Color? Fore { get; }

    /// <summary><c>border-*</c>.</summary>
    public Color? Border { get; }

    /// <summary><c>accent-*</c>.</summary>
    public Color? Accent { get; }

    /// <summary><c>fill-*</c>.</summary>
    public Color? Fill { get; }

    public override string ToString() => $"back={Hex(Back)} fore={Hex(Fore)} border={Hex(Border)} accent={Hex(Accent)} fill={Hex(Fill)}";

    private static string Hex(Color? color) => color is Color c ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" + (c.A < 255 ? $"/{c.A}" : "") : "-";
}
