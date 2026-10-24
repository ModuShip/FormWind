using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FormWind;

/// <summary>
/// Conflict-aware editing of class strings (like tailwind-merge). A class removes the earlier classes of the same group and
/// variant that it fully overrides: <c>px-4</c> removes <c>px-2</c>, <c>pl-1</c> and <c>pr-3</c> but keeps <c>p-2</c> and
/// <c>py-2</c>; <c>hover:bg-red-500</c> removes only <c>hover:bg-*</c>. The grouping is syntactic (no theme lookup); a class it
/// does not know only replaces itself. Used by the Designer's class builder.
/// </summary>
internal static class TwClassMerge
{
    private const RegexOptions Options = RegexOptions.Compiled | RegexOptions.CultureInvariant;
    private static readonly char[] Separators = { ' ', '\t', '\r', '\n' };

    private static readonly Regex SidedRx = new Regex(@"^([pm])([xytrbl]?)-", Options);
    private static readonly Regex BorderSideRx = new Regex(@"^border-([xytrbl])(?:-\d+)?$", Options);
    private static readonly Regex BorderWidthRx = new Regex(@"^border(?:-(?:0|2|4|8))?$", Options);
    private static readonly Regex FontSizeRx = new Regex(@"^text-(?:xs|sm|base|lg|xl|2xl|3xl|4xl|\[[\d.]+px\])$", Options);
    private static readonly Regex RingWidthRx = new Regex(@"^ring(?:-(?:0|1|2|4|8))?$", Options);
    private static readonly Regex RingOffsetWidthRx = new Regex(@"^ring-offset-(?:0|1|2|4|8)$", Options);
    private static readonly Regex ShadowSizeRx = new Regex(@"^shadow(?:-(?:sm|md|lg|xl|2xl|none))?$", Options);
    private static readonly Regex GapRx = new Regex(@"^gap(?:-([xy]))?-", Options);

    private static readonly HashSet<string> FontWeights = new HashSet<string>(StringComparer.Ordinal)
    {
        "font-thin", "font-extralight", "font-light", "font-normal", "font-medium", "font-semibold", "font-bold", "font-extrabold", "font-black",
    };

    // Önekiyle gruplanan class'lar; daha uzun önek önce denenir ("min-w-" "w-"den önce).
    private static readonly (string Prefix, string Group)[] Prefixes =
    {
        ("min-w-", "min-w"), ("max-w-", "max-w"), ("min-h-", "min-h"), ("max-h-", "max-h"), ("w-", "w"), ("h-", "h"),
        ("rounded", "rounded"), ("cursor-", "cursor"), ("justify-", "justify"), ("items-", "items"), ("self-", "self"),
        ("basis-", "basis"), ("grow", "grow"), ("shrink", "shrink"), ("grid-cols-", "grid-cols"), ("col-span-", "col-span"),
        ("grid-rows-", "grid-rows"), ("row-span-", "row-span"), ("duration-", "duration"), ("delay-", "delay"), ("ease-", "ease"),
        ("animate-", "animate"), ("transition", "transition"),
        ("object-", "object"), ("backdrop-", "backdrop"), ("whitespace-", "whitespace"), ("from-", "from"), ("via-", "via"),
        ("to-", "to"), ("fill-", "fill"), ("accent-", "accent"),
    };

    private const int Left = 1, Top = 2, Right = 4, Bottom = 8, All = Left | Top | Right | Bottom;

    /// <summary>Group of a class: its variant prefix (<c>"dark:hover:"</c>, empty for none), conflict group and sides.</summary>
    internal readonly struct Key
    {
        public Key(string variant, string group, int sides)
        {
            Variant = variant;
            Group = group;
            Sides = sides;
        }

        public string Variant { get; }
        public string Group { get; }

        /// <summary>Sides/axes a sided class sets (padding, margin, border sides, gap); 0 for the others.</summary>
        public int Sides { get; }

        /// <summary>Whether a class with this key fully overrides a class with <paramref name="older"/>.</summary>
        public bool Overrides(Key older) =>
            Variant == older.Variant && Group == older.Group && (older.Sides & ~Sides) == 0;
    }

    public static Key KeyOf(string token)
    {
        int separator = token.LastIndexOf(':');
        var variant = separator > 0 ? token.Substring(0, separator + 1) : "";
        var utility = separator > 0 ? token.Substring(separator + 1) : token;
        var (group, sides) = GroupOf(utility);
        return new Key(variant, group, sides);
    }

    private static (string Group, int Sides) GroupOf(string utility)
    {
        if (BorderWidthRx.IsMatch(utility)) return ("border-width", 0);
        Match match;
        if ((match = BorderSideRx.Match(utility)).Success) return ("border-side", Sides(match.Groups[1].Value));
        if ((match = SidedRx.Match(utility)).Success) return (match.Groups[1].Value, Sides(match.Groups[2].Value));
        if ((match = GapRx.Match(utility)).Success)
            return ("gap", match.Groups[1].Value switch { "x" => Left | Right, "y" => Top | Bottom, _ => All });

        if (utility.StartsWith("text-", StringComparison.Ordinal))
        {
            if (FontSizeRx.IsMatch(utility)) return ("font-size", 0);
            if (utility is "text-left" or "text-center" or "text-right") return ("text-align", 0);
            return ("text-color", 0);
        }
        if (utility.StartsWith("bg-gradient-to-", StringComparison.Ordinal)) return ("gradient", 0);
        if (utility.StartsWith("bg-", StringComparison.Ordinal)) return ("bg-color", 0);
        if (utility.StartsWith("border-", StringComparison.Ordinal)) return ("border-color", 0);
        if (RingOffsetWidthRx.IsMatch(utility)) return ("ring-offset-width", 0);
        if (utility.StartsWith("ring-offset-", StringComparison.Ordinal)) return ("ring-offset-color", 0);
        if (RingWidthRx.IsMatch(utility)) return ("ring-width", 0);
        if (utility.StartsWith("ring-", StringComparison.Ordinal)) return ("ring-color", 0);
        if (ShadowSizeRx.IsMatch(utility)) return ("shadow", 0);
        if (utility.StartsWith("shadow-", StringComparison.Ordinal)) return ("shadow-color", 0);
        if (utility is "divide-x" or "divide-x-0") return ("divide-x", 0);
        if (utility is "divide-y" or "divide-y-0") return ("divide-y", 0);
        if (utility.StartsWith("divide-", StringComparison.Ordinal)) return ("divide-color", 0);

        if (FontWeights.Contains(utility)) return ("font-weight", 0);
        switch (utility)
        {
            case "font-sans": case "font-mono": return ("font-family", 0);
            case "italic": case "not-italic": return ("italic", 0);
            case "underline": case "no-underline": return ("underline", 0);
            case "hidden": case "visible": return ("visibility", 0);
            case "block": case "flex": case "grid": return ("display", 0);
            case "flex-row": case "flex-col": return ("flex-direction", 0);
            case "flex-wrap": case "flex-nowrap": return ("flex-wrap", 0);
            case "flex-1": case "flex-auto": case "flex-initial": case "flex-none": return ("flex", 0);
        }

        foreach (var (prefix, group) in Prefixes)
            if (utility.StartsWith(prefix, StringComparison.Ordinal)) return (group, 0);

        // Bilinmeyen class (bileşen adı dahil) yalnızca kendisinin yerini alır.
        return ("=" + utility, 0);
    }

    private static int Sides(string axis) => axis switch
    {
        "x" => Left | Right,
        "y" => Top | Bottom,
        "l" => Left,
        "t" => Top,
        "r" => Right,
        "b" => Bottom,
        _ => All,
    };

    public static string[] Split(string? classes) =>
        string.IsNullOrWhiteSpace(classes) ? Array.Empty<string>() : classes!.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Appends the classes of <paramref name="add"/>; each removes the earlier classes it overrides.</summary>
    public static string Merge(string? classes, string? add)
    {
        var tokens = new List<string>(Split(classes));
        foreach (var token in Split(add))
        {
            var key = KeyOf(token);
            tokens.RemoveAll(existing => key.Overrides(KeyOf(existing)));
            tokens.Add(token);
        }
        return string.Join(" ", tokens);
    }

    /// <summary>The classes with the given variant prefix whose group is one of <paramref name="groups"/>.</summary>
    public static string Select(string? classes, string variant, ICollection<string> groups) =>
        string.Join(" ", Split(classes).Where(t => InGroups(KeyOf(t), variant, groups)));

    /// <summary>
    /// Removes the classes that <see cref="Select"/> returns and merges <paramref name="value"/> in their place (at the end, so it
    /// wins over components earlier in the string). An empty value only removes them.
    /// </summary>
    public static string Replace(string? classes, string variant, ICollection<string> groups, string? value)
    {
        var kept = string.Join(" ", Split(classes).Where(t => !InGroups(KeyOf(t), variant, groups)));
        return Merge(kept, value);
    }

    private static bool InGroups(Key key, string variant, ICollection<string> groups) =>
        key.Variant == variant && groups.Contains(key.Group);
}
