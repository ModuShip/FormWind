using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FormWind;

/// <summary>
/// A formatted text with one number in it ("₺12.400", "98,4%", "1,2 Mn"), read with the current culture's separators so
/// the number can be counted up and written back in the same form: same prefix and suffix, digits after the decimal
/// separator and digit grouping. A text with no number or more than one ("278 h 52 min") is not countable.
/// </summary>
internal sealed class TwNumberText
{
    private readonly string _prefix;
    private readonly string _suffix;
    private readonly int _decimals;
    private readonly bool _grouped;
    private readonly CultureInfo _culture;

    private TwNumberText(string prefix, double value, string suffix, int decimals, bool grouped, CultureInfo culture)
    {
        _prefix = prefix;
        Value = value;
        _suffix = suffix;
        _decimals = decimals;
        _grouped = grouped;
        _culture = culture;
    }

    public double Value { get; }

    /// <summary>Whether <paramref name="other"/> is the same text around another number (a counted change keeps its form).</summary>
    public bool SameForm(TwNumberText other) => _prefix == other._prefix && _suffix == other._suffix;

    /// <summary>The text with <paramref name="value"/> in place of the number.</summary>
    public string Format(double value)
    {
        var format = (_grouped ? "N" : "F") + _decimals.ToString(CultureInfo.InvariantCulture);
        return _prefix + value.ToString(format, _culture) + _suffix;
    }

    public static bool TryParse(string text, out TwNumberText? number) => TryParse(text, CultureInfo.CurrentCulture, out number);

    public static bool TryParse(string text, CultureInfo culture, out TwNumberText? number)
    {
        number = null;
        if (string.IsNullOrEmpty(text)) return false;
        var info = culture.NumberFormat;
        string group = Regex.Escape(info.NumberGroupSeparator), point = Regex.Escape(info.NumberDecimalSeparator);
        // Fransızca gibi kültürlerde grup ayracı bölünmez boşluktur; normal boşlukla da yazılmış olabilir.
        if (info.NumberGroupSeparator.Trim().Length == 0) group = @"[   ]";
        var rx = new Regex(@"\d{1,3}(?:" + group + @"\d{3})+(?:" + point + @"\d+)?|\d+(?:" + point + @"\d+)?", RegexOptions.CultureInvariant);
        var matches = rx.Matches(text);
        if (matches.Count != 1) return false;

        var match = matches[0];
        string digits = match.Value;
        bool grouped = Regex.IsMatch(digits, group);
        int decimalAt = digits.IndexOf(info.NumberDecimalSeparator, StringComparison.Ordinal);
        int decimals = decimalAt < 0 ? 0 : digits.Length - decimalAt - info.NumberDecimalSeparator.Length;
        string plain = Regex.Replace(digits, group, "");
        if (decimalAt >= 0) plain = plain.Replace(info.NumberDecimalSeparator, ".");
        if (!double.TryParse(plain, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) return false;

        // Eksi işareti sayının önündeyse sayıya aittir.
        string prefix = text.Substring(0, match.Index);
        if (prefix.EndsWith(info.NegativeSign, StringComparison.Ordinal) || prefix.EndsWith("−", StringComparison.Ordinal))
        {
            prefix = prefix.Substring(0, prefix.Length - 1);
            value = -value;
        }
        number = new TwNumberText(prefix, value, text.Substring(match.Index + match.Length), decimals, grouped, culture);
        return true;
    }
}
