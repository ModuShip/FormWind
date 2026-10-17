using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace FormWind;

/// <summary>
/// Ready-made field rules (for <see cref="TwField.Validator"/>): take the value and return an error text, or <c>null</c> when valid.
/// <code>field.Validator = TwValidators.TcKimlikNo;</code>
/// </summary>
public static class TwValidators
{
    private static readonly Regex EmailRx = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.CultureInvariant);

    public static string? Email(string value) =>
        EmailRx.IsMatch(value.Trim()) ? null : TwTexts.Current.InvalidEmail;

    /// <summary>Turkish national ID number: 11 digits, the first digit not 0, the 10th and 11th digits are check digits.</summary>
    public static string? TcKimlikNo(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length != 11 || digits[0] == '0') return TwTexts.Current.TcKimlikLength;
        int[] d = digits.Select(c => c - '0').ToArray();
        int odd = d[0] + d[2] + d[4] + d[6] + d[8];
        int even = d[1] + d[3] + d[5] + d[7];
        int tenth = ((odd * 7 - even) % 10 + 10) % 10;
        int eleventh = d.Take(10).Sum() % 10;
        return d[9] == tenth && d[10] == eleventh ? null : TwTexts.Current.InvalidTcKimlik;
    }

    /// <summary>IBAN: country code, length (26 for TR) and mod 97 check; spaces are ignored.</summary>
    public static string? Iban(string value)
    {
        var iban = new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        if (iban.Length < 15 || iban.Length > 34 || !iban.All(char.IsLetterOrDigit)) return TwTexts.Current.InvalidIban;
        if (iban.StartsWith("TR", StringComparison.Ordinal) && iban.Length != 26) return TwTexts.Current.TurkishIbanLength;
        var rearranged = iban.Substring(4) + iban.Substring(0, 4);
        // Harfler 10–35; sayı çok uzun olduğundan mod 97 basamak basamak hesaplanır.
        int remainder = 0;
        foreach (var c in rearranged)
        {
            var part = char.IsLetter(c) ? (c - 'A' + 10).ToString(CultureInfo.InvariantCulture) : c.ToString();
            foreach (var digit in part) remainder = (remainder * 10 + (digit - '0')) % 97;
        }
        return remainder == 1 ? null : TwTexts.Current.InvalidIban;
    }

    /// <summary>Turkish mobile number: 10 digits (5xx…), optionally prefixed with 0 or +90.</summary>
    public static string? TurkishMobile(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("90", StringComparison.Ordinal) && digits.Length == 12) digits = digits.Substring(2);
        if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length == 11) digits = digits.Substring(1);
        return digits.Length == 10 && digits[0] == '5' ? null : TwTexts.Current.InvalidMobile;
    }

    /// <summary>En az <paramref name="length"/> karakter.</summary>
    public static Func<string, string?> MinLength(int length) =>
        value => value.Trim().Length >= length ? null : string.Format(System.Globalization.CultureInfo.CurrentCulture, TwTexts.Current.MinLength, length);
}

/// <summary>Common masks (<see cref="TwTextBox.Mask"/>): <c>0</c> required digit, <c>9</c> optional digit, <c>L</c> letter.</summary>
public static class TwMasks
{
    public const string Phone = "(000) 000 00 00";
    public const string Iban = "TR00 0000 0000 0000 0000 0000 00";
    public const string TcKimlikNo = "00000000000";
    public const string Date = "00/00/0000";
    public const string PostalCode = "00000";
}
