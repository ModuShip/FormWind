using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace FormWind;

/// <summary>
/// Name → code point table for the icon font shipped with Windows (Segoe Fluent Icons / Segoe MDL2 Assets).
/// Both fonts use the same code points for these icons. More icons can be added with <see cref="Register"/> or
/// <c>icons</c> in the theme file; <c>#E710</c> can be written for a code point directly.
/// </summary>
public static class TwIcons
{
    private static readonly Dictionary<string, char> Map = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase)
    {
        // Eylemler
        ["plus"] = '', ["minus"] = '', ["x"] = '', ["check"] = '',
        ["edit"] = '', ["trash"] = '', ["save"] = '', ["search"] = '',
        ["filter"] = '', ["sort"] = '', ["refresh"] = '', ["sync"] = '',
        ["download"] = '', ["upload"] = '', ["print"] = '', ["share"] = '',
        ["copy"] = '', ["cut"] = '', ["paste"] = '', ["undo"] = '',
        ["redo"] = '', ["send"] = '', ["attach"] = '', ["link"] = '',
        ["pin"] = '', ["zoom-in"] = '', ["zoom-out"] = '', ["fullscreen"] = '',
        ["power"] = '', ["external-link"] = '', ["moon"] = '', ["sun"] = '',

        // Gezinme
        ["home"] = '', ["menu"] = '', ["more"] = '', ["settings"] = '',
        ["arrow-left"] = '', ["arrow-right"] = '', ["arrow-up"] = '', ["arrow-down"] = '',
        ["chevron-left"] = '', ["chevron-right"] = '', ["chevron-up"] = '', ["chevron-down"] = '',
        ["list"] = '', ["grid"] = '',

        // Nesneler
        ["user"] = '', ["users"] = '', ["mail"] = '', ["phone"] = '',
        ["calendar"] = '', ["clock"] = '', ["history"] = '', ["bell"] = '',
        ["folder"] = '', ["folder-open"] = '', ["file"] = '', ["image"] = '',
        ["camera"] = '', ["cart"] = '', ["tag"] = '', ["flag"] = '',
        ["globe"] = '', ["map-pin"] = '', ["location"] = '', ["calculator"] = '', ["key"] = '',
        ["lock"] = '', ["unlock"] = '', ["shield"] = '', ["eye"] = '',
        ["eye-off"] = '', ["heart"] = '', ["heart-filled"] = '', ["star"] = '',
        ["star-filled"] = '',

        // Durum
        ["info"] = '', ["warning"] = '', ["error"] = '', ["help"] = '',

        // Sonradan eklenenler (Segoe Fluent Icons ve MDL2'de aynı kod noktaları)
        ["log-out"] = '', ["sign-out"] = '', ["sidebar"] = '', ["panel-right"] = '',
        ["wrench"] = '', ["tool"] = '', ["gauge"] = '', ["paperclip"] = '',
    };

    /// <summary>The first installed icon font; when none is installed (Windows 7/8) "Segoe MDL2 Assets" is returned and icons are not drawn.</summary>
    public static string DefaultFontFamily { get; } =
        TwFonts.IsInstalled("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

    public static IEnumerable<string> Names => Map.Keys.OrderBy(n => n, StringComparer.Ordinal);

    public static void Register(string name, char glyph)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("İkon adı boş olamaz.", nameof(name));
        lock (Map) Map[name] = glyph;
    }

    public static bool TryGetGlyph(string name, out char glyph)
    {
        if (name.StartsWith("#", StringComparison.Ordinal)) return TryParseCodePoint(name.Substring(1), out glyph);
        lock (Map) return Map.TryGetValue(name, out glyph);
    }

    /// <summary>
    /// Whether <paramref name="name"/> draws an icon: a built-in or registered name, an icon of <see cref="TwTheme.Current"/>, or a code
    /// point (<c>#E710</c>). An empty name counts as known (no icon). An unknown name draws nothing; FormWind reports it
    /// through <see cref="TailwindProvider.StyleError"/> when the control is styled, and the FW003 analyzer warns about literals.
    /// </summary>
    public static bool IsKnown(string? name) => string.IsNullOrWhiteSpace(name) || TwTheme.Current.HasIcon(name);

    // Stillenen kontrolün bilinmeyen ikon adı (TwIcon, TwButton, TwChip); null = ikon yok ya da temada/tabloda var.
    internal static string? UnknownIconOf(System.Windows.Forms.Control control, TwTheme theme)
    {
        var name = control switch
        {
            TwIcon icon => icon.IconName,
            TwButton button => button.IconName,
            TwChip chip => chip.IconName,
            _ => null,
        };
        return string.IsNullOrWhiteSpace(name) || theme.HasIcon(name) ? null : name;
    }

    /// <summary>Parses a code point in the form "E710", "#E710" or "U+E710" (BMP only).</summary>
    public static bool TryParseCodePoint(string? value, out char glyph)
    {
        glyph = '\0';
        if (string.IsNullOrWhiteSpace(value)) return false;
        var hex = value!.Trim();
        if (hex.StartsWith("#", StringComparison.Ordinal)) hex = hex.Substring(1);
        else if (hex.StartsWith("U+", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);

        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) || code <= 0 || code > 0xFFFF)
            return false;
        glyph = (char)code;
        return true;
    }
}

/// <summary>Drop-down of icon names in the Properties window (free text can be typed too).</summary>
public sealed class TwIconNameConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) =>
        new StandardValuesCollection(TwIcons.Names.ToArray());
}
