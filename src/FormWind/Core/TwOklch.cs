using System;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FormWind;

/// <summary>
/// Converts OKLCH colors (the Tailwind v4 palette, <c>oklch(62.3% 0.214 259.8)</c>) to sRGB. Colors outside sRGB are
/// approximated with CSS Color 4 gamut mapping, as browsers do on sRGB displays: hue and lightness are kept and chroma is
/// reduced until the perceptual difference (ΔEOK) is below 0.02.
/// </summary>
public static class TwOklch
{
    private const double JustNoticeable = 0.02;
    private const double Epsilon = 0.0001;

    private static readonly Regex Syntax = new Regex(
        @"^\s*oklch\(\s*(?<l>[-+0-9.]+%?)\s+(?<c>[-+0-9.]+%?|none)\s+(?<h>[-+0-9.]+(?:deg)?|none)\s*(?:/\s*(?<a>[-+0-9.]+%?)\s*)?\)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>Converts an OKLCH color to sRGB.</summary>
    /// <param name="lightness">Lightness, 0–1 (0–100% in CSS).</param>
    /// <param name="chroma">Chroma (0 is gray; at most ~0.32 in the Tailwind palette).</param>
    /// <param name="hue">Hue in degrees.</param>
    /// <param name="alpha">Opacity, 0–1.</param>
    public static Color ToColor(double lightness, double chroma, double hue, double alpha = 1)
    {
        var (r, g, b) = GamutMap(lightness, Math.Max(0, chroma), hue);
        return Color.FromArgb(ToByte(alpha), ToByte(r), ToByte(g), ToByte(b));
    }

    /// <summary>Parses <c>oklch(L C H)</c> or <c>oklch(L C H / A)</c> text (L and A can be percentages or numbers).</summary>
    public static bool TryParse(string? text, out Color color)
    {
        color = Color.Empty;
        if (text is null) return false;
        var match = Syntax.Match(text);
        if (!match.Success) return false;

        if (!TryNumber(match.Groups["l"].Value, 1, out double l)) return false;
        double c = 0, h = 0, a = 1;
        if (match.Groups["c"].Value != "none" && !TryNumber(match.Groups["c"].Value, 0.4, out c)) return false;
        if (match.Groups["h"].Value != "none" && !TryNumber(match.Groups["h"].Value.Replace("deg", ""), 1, out h)) return false;
        if (match.Groups["a"].Success && !TryNumber(match.Groups["a"].Value, 1, out a)) return false;

        color = ToColor(l, c, h, Math.Max(0, Math.Min(1, a)));
        return true;
    }

    // Yüzde, CSS'teki referans aralığına göre ölçeklenir (L ve A için %100 = 1, C için %100 = 0,4).
    private static bool TryNumber(string text, double percentScale, out double value)
    {
        bool percent = text.EndsWith("%", StringComparison.Ordinal);
        if (!double.TryParse(percent ? text.Substring(0, text.Length - 1) : text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return false;
        if (percent) value = value / 100 * percentScale;
        return true;
    }

    // CSS Color 4, "binary search gamut mapping with local MINDE".
    private static (double R, double G, double B) GamutMap(double l, double c, double h)
    {
        if (l >= 1) return (1, 1, 1);
        if (l <= 0) return (0, 0, 0);

        var origin = ToSrgb(l, c, h);
        if (InGamut(origin)) return origin;

        var clipped = Clip(origin);
        if (DeltaEok(clipped, l, c, h) < JustNoticeable) return clipped;

        double min = 0, max = c;
        bool minInGamut = true;
        while (max - min > Epsilon)
        {
            double chroma = (min + max) / 2;
            var current = ToSrgb(l, chroma, h);
            if (minInGamut && InGamut(current))
            {
                min = chroma;
                continue;
            }
            clipped = Clip(current);
            double e = DeltaEok(clipped, l, chroma, h);
            if (e < JustNoticeable)
            {
                if (JustNoticeable - e < Epsilon) return clipped;
                minInGamut = false;
                min = chroma;
            }
            else
            {
                max = chroma;
            }
        }
        return clipped;
    }

    private static (double R, double G, double B) ToSrgb(double l, double c, double h)
    {
        double radians = h * Math.PI / 180;
        double a = c * Math.Cos(radians), b = c * Math.Sin(radians);

        double l_ = l + 0.3963377774 * a + 0.2158037573 * b;
        double m_ = l - 0.1055613458 * a - 0.0638541728 * b;
        double s_ = l - 0.0894841775 * a - 1.2914855480 * b;
        double lc = l_ * l_ * l_, mc = m_ * m_ * m_, sc = s_ * s_ * s_;

        return (
            Encode(+4.0767416621 * lc - 3.3077115913 * mc + 0.2309699292 * sc),
            Encode(-1.2684380046 * lc + 2.6097574011 * mc - 0.3413193965 * sc),
            Encode(-0.0041960863 * lc - 0.7034186147 * mc + 1.7076147010 * sc));
    }

    // sRGB → OKLab (kırpılmış rengin hedefe uzaklığı için).
    private static (double L, double A, double B) ToOklab((double R, double G, double B) rgb)
    {
        double r = Decode(rgb.R), g = Decode(rgb.G), b = Decode(rgb.B);
        double l = Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        double m = Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        double s = Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return (
            0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    private static double DeltaEok((double R, double G, double B) rgb, double l, double c, double h)
    {
        var lab = ToOklab(rgb);
        double radians = h * Math.PI / 180;
        double dl = lab.L - l, da = lab.A - c * Math.Cos(radians), db = lab.B - c * Math.Sin(radians);
        return Math.Sqrt(dl * dl + da * da + db * db);
    }

    private static bool InGamut((double R, double G, double B) rgb) =>
        rgb.R >= -1e-6 && rgb.R <= 1 + 1e-6 && rgb.G >= -1e-6 && rgb.G <= 1 + 1e-6 && rgb.B >= -1e-6 && rgb.B <= 1 + 1e-6;

    private static (double R, double G, double B) Clip((double R, double G, double B) rgb) =>
        (Clamp(rgb.R), Clamp(rgb.G), Clamp(rgb.B));

    private static double Clamp(double value) => value < 0 ? 0 : value > 1 ? 1 : value;

    private static double Encode(double linear) =>
        Math.Abs(linear) <= 0.0031308 ? 12.92 * linear : Math.Sign(linear) * (1.055 * Math.Pow(Math.Abs(linear), 1 / 2.4) - 0.055);

    private static double Decode(double encoded) =>
        Math.Abs(encoded) <= 0.04045 ? encoded / 12.92 : Math.Sign(encoded) * Math.Pow((Math.Abs(encoded) + 0.055) / 1.055, 2.4);

    private static double Cbrt(double value) => value < 0 ? -Math.Pow(-value, 1.0 / 3) : Math.Pow(value, 1.0 / 3);

    private static int ToByte(double value) => (int)Math.Round(Clamp(value) * 255, MidpointRounding.AwayFromZero);
}
