using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;

namespace FormWind;

/// <summary>Readable summary of what a class string resolves to (<see cref="TwTheme.Explain"/>).</summary>
internal static class TwExplain
{
    private static readonly char[] Separators = { ' ', '\t', '\r', '\n' };

    // TwStyle'ın değer taşıyan özellikleri; Has* gibi türetilmişler dışarıda.
    private static readonly PropertyInfo[] Properties = Array.FindAll(
        typeof(TwStyle).GetProperties(BindingFlags.Public | BindingFlags.Instance),
        p => p.GetIndexParameters().Length == 0 && !p.Name.StartsWith("Has", StringComparison.Ordinal));

    public static string Describe(string? classes, TwTheme theme)
    {
        var text = new List<string>();
        foreach (var token in (classes ?? "").Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            text.Add(token);
            var parsed = theme.Parse(token);
            var lines = new List<string>();
            foreach (var variant in parsed.Variants)
                foreach (var (name, value) in Values(variant.Style))
                    lines.Add(Label(variant) + name + " = " + value);

            if (parsed.Unknown.Count > 0)
            {
                var suggestion = theme.SuggestClass(token);
                text.Add("  unknown class" + (suggestion is null ? "" : $" (did you mean {suggestion}?)"));
            }
            else if (lines.Count == 0) text.Add("  no effect");
            foreach (var line in lines) text.Add("  " + line);
        }
        return text.Count == 0 ? "(no classes)" : string.Join(Environment.NewLine, text);
    }

    // Koşullar ve parça: "[Hover] ", "[Dark, Hover] header: "; temel class'ların etiketi yoktur.
    private static string Label(TwVariant variant)
    {
        var label = variant.Conditions == TwState.None ? "" : "[" + variant.Conditions.ToString().Replace(" ", "") + "] ";
        return variant.Part is null ? label : label + variant.Part + ": ";
    }

    private static IEnumerable<(string Name, string Value)> Values(TwStyle style)
    {
        foreach (var property in Properties)
        {
            var value = property.GetValue(style);
            if (value is TwSides sides)
            {
                if (!sides.IsEmpty) yield return (property.Name, Format(sides));
            }
            else if (value != null) yield return (property.Name, Format(value));
        }
    }

    private static string Format(TwSides sides)
    {
        var parts = new List<string>();
        if (sides.Left is int left) parts.Add("left " + left + "px");
        if (sides.Top is int top) parts.Add("top " + top + "px");
        if (sides.Right is int right) parts.Add("right " + right + "px");
        if (sides.Bottom is int bottom) parts.Add("bottom " + bottom + "px");
        return string.Join(", ", parts);
    }

    private static string Format(object value) => value switch
    {
        Color color => color.A == 255
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.R:X2}{color.G:X2}{color.B:X2} (alpha {color.A / 255.0:0.##})",
        int number when number == int.MaxValue => "full",
        int pixels => pixels.ToString(CultureInfo.InvariantCulture),
        float number => number.ToString(CultureInfo.InvariantCulture),
        System.Windows.Forms.Cursor cursor => cursor.ToString() ?? "",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
