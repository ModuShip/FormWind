using System;
using System.Collections.Generic;
using System.Linq;

namespace FormWind;

/// <summary>
/// "Did you mean" for unknown classes: the closest class the theme recognizes, by edit distance, with the variant prefixes
/// corrected too (<c>hovr:bg-blu-600</c> → <c>hover:bg-blue-600</c>). Used only when a class is unknown, so it is not cached.
/// </summary>
internal static class TwSuggestions
{
    private static readonly string[] Variants =
    {
        "dark", "forced-colors", "rtl", "ltr", "checked", "focus", "focus-visible", "hover", "group-hover", "active", "disabled",
        "invalid", "sm", "md", "lg", "xl", "2xl", "motion-safe", "motion-reduce",
        "header", "cell", "alt", "selected", "row", "row-header", "match", "group", "footer", "item", "menu", "caption",
        "placeholder", "label", "hint", "error",
    };

    // TwParser.ApplyKeyword'deki sabit class'lar; VocabularyTests her birinin tanındığını denetler.
    internal static readonly string[] Keywords =
    {
        "font-thin", "font-extralight", "font-light", "font-normal", "font-medium", "font-semibold", "font-bold", "font-extrabold",
        "font-black", "font-sans", "font-mono", "italic", "not-italic", "underline", "no-underline", "text-left", "text-center",
        "text-right", "cursor-pointer", "cursor-default", "cursor-text", "hidden", "visible", "block", "flex", "grid", "flex-row",
        "flex-col", "flex-wrap", "flex-nowrap", "flex-1", "flex-auto", "flex-initial", "flex-none", "grow", "grow-0", "shrink",
        "shrink-0", "justify-start", "justify-end", "justify-center", "justify-between", "justify-around", "justify-evenly",
        "items-start", "items-baseline", "items-end", "items-center", "items-stretch", "self-start", "self-end", "self-center",
        "self-stretch", "self-auto", "group", "object-cover", "object-contain", "object-fill", "transition", "transition-colors",
        "transition-all", "transition-shadow", "transition-none", "ease-linear", "ease-in", "ease-out", "ease-in-out",
        "animate-spin", "animate-pulse", "animate-none", "truncate", "backdrop-mica", "backdrop-acrylic", "backdrop-tabbed", "backdrop-none",
        "backdrop-blur", "backdrop-blur-none", "backdrop-blur-sm", "backdrop-blur-md", "backdrop-blur-lg", "backdrop-blur-xl", "backdrop-blur-2xl", "backdrop-blur-3xl",
        "whitespace-nowrap", "whitespace-normal", "divide-x", "divide-y", "divide-x-0", "divide-y-0",
    };

    private static readonly string[] ColorTargets = { "bg", "text", "border", "ring", "from", "via", "to", "divide", "shadow", "fill", "accent", "ring-offset" };
    private static readonly string[] Spacing = { "0", "0.5", "1", "1.5", "2", "2.5", "3", "3.5", "4", "5", "6", "7", "8", "9", "10", "11", "12", "14", "16", "20", "24", "px" };
    private static readonly string[] Fractions = { "full", "1/2", "1/3", "2/3", "1/4", "3/4" };
    private static readonly string[] Sides = { "", "x", "y", "t", "r", "b", "l" };
    private static readonly string[] Named = { "xs", "sm", "md", "lg", "xl", "2xl", "3xl", "4xl", "5xl", "6xl", "7xl" };

    /// <summary>The closest recognized class, or <c>null</c> when nothing is close enough.</summary>
    public static string? For(string token, TwTheme theme)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        token = token.Trim();
        var prefix = "";
        var utility = token;
        int separator = token.LastIndexOf(':');
        if (separator > 0)
        {
            var segments = token.Substring(0, separator).Split(':');
            for (int i = 0; i < segments.Length; i++)
            {
                if (Array.IndexOf(Variants, segments[i]) >= 0) continue;
                var fixedSegment = Closest(segments[i], Variants);
                if (fixedSegment is null) return null;
                segments[i] = fixedSegment;
            }
            prefix = string.Join(":", segments) + ":";
            utility = token.Substring(separator + 1);
        }

        // Ön ekler düzeltildikten sonra class zaten tanınıyorsa yalnızca ön ek yanlıştı.
        if (Known(prefix + utility, theme)) return prefix + utility == token ? null : prefix + utility;

        // Saydamlık (/50) sözlükte yok: ayrılıp öneriye geri eklenir.
        var opacity = "";
        int slash = utility.LastIndexOf('/');
        if (slash > 0 && slash < utility.Length - 1 && utility.Substring(slash + 1).All(char.IsDigit) && ColorLike(utility.Substring(0, slash)))
        {
            opacity = utility.Substring(slash);
            utility = utility.Substring(0, slash);
        }
        var closest = Closest(utility, Vocabulary(theme));
        return closest != null && Known(prefix + closest + opacity, theme) ? prefix + closest + opacity : null;
    }

    private static bool ColorLike(string utility) => ColorTargets.Any(target => utility.StartsWith(target + "-", StringComparison.Ordinal));

    private static bool Known(string classes, TwTheme theme) => theme.Parse(classes).Unknown.Count == 0;

    /// <summary>The unknown classes for a message, each with its suggestion: <c>bg-blu-600 (did you mean bg-blue-600?)</c>.</summary>
    public static string Describe(IEnumerable<string> unknown, TwTheme theme) =>
        string.Join(", ", unknown.Select(token => For(token, theme) is string suggestion ? $"{token} (did you mean {suggestion}?)" : token));

    /// <summary>Recognized classes without variants: keywords, the common values of each pattern, theme colors and components.</summary>
    internal static IEnumerable<string> Vocabulary(TwTheme theme)
    {
        foreach (var keyword in Keywords) yield return keyword;
        foreach (var size in new[] { "xs", "sm", "base", "lg", "xl", "2xl", "3xl", "4xl" }) yield return "text-" + size;
        foreach (var weight in new[] { "", "-0", "-2", "-4", "-8" }) yield return "border" + weight;
        foreach (var side in Sides.Skip(1)) yield return "border-" + side;
        foreach (var radius in new[] { "", "-none", "-sm", "-md", "-lg", "-xl", "-2xl", "-3xl", "-full" }) yield return "rounded" + radius;
        foreach (var shadow in new[] { "", "-sm", "-md", "-lg", "-xl", "-2xl", "-none" }) yield return "shadow" + shadow;
        foreach (var ring in new[] { "", "-0", "-1", "-2", "-4", "-8" }) yield return "ring" + ring;
        foreach (var offset in new[] { "0", "1", "2", "4", "8" }) yield return "ring-offset-" + offset;
        foreach (var duration in new[] { "0", "75", "100", "150", "200", "300", "500", "700", "1000" })
        {
            yield return "duration-" + duration;
            yield return "delay-" + duration;
        }
        foreach (var direction in new[] { "r", "l", "t", "b", "tr", "tl", "br", "bl" }) yield return "bg-gradient-to-" + direction;
        foreach (var kind in new[] { "p", "m" })
            foreach (var side in Sides)
                foreach (var value in Spacing) yield return kind + side + "-" + value;
        foreach (var axis in new[] { "w", "h" })
        {
            foreach (var value in Spacing.Concat(Fractions)) yield return axis + "-" + value;
            foreach (var bound in new[] { "min", "max" })
                foreach (var value in Spacing.Concat(Named)) yield return bound + "-" + axis + "-" + value;
        }
        foreach (var gap in new[] { "gap", "gap-x", "gap-y" })
            foreach (var value in Spacing) yield return gap + "-" + value;
        foreach (var value in Spacing.Concat(Fractions).Concat(new[] { "auto" })) yield return "basis-" + value;
        for (int i = 1; i <= 12; i++)
        {
            yield return "grid-cols-" + i;
            yield return "grid-rows-" + i;
            yield return "col-span-" + i;
            yield return "row-span-" + i;
        }
        yield return "col-span-full";
        yield return "row-span-full";
        foreach (var color in theme.ColorKeys())
            foreach (var target in ColorTargets) yield return target + "-" + color;
        foreach (var component in theme.Components.Keys) yield return component;
    }

    // En küçük uzaklık kazanır; eşitlikte uzunluğu aynı olan (bg-blue-650 → bg-blue-600, bg-blue-50 değil), sonra listede
    // önce gelen. Eşik, kısa adlarda rastgele eşleşmeyi önler.
    private static string? Closest(string value, IEnumerable<string> candidates)
    {
        int limit = value.Length <= 4 ? 1 : value.Length <= 10 ? 2 : 3;
        string? best = null;
        int bestDistance = limit + 1, bestLength = int.MaxValue;
        foreach (var candidate in candidates)
        {
            int lengthDifference = Math.Abs(candidate.Length - value.Length);
            if (lengthDifference > Math.Min(bestDistance, limit)) continue;
            int distance = Distance(value, candidate, bestDistance + 1);
            if (distance > limit || !Plausible(value, candidate)) continue;
            if (distance < bestDistance || distance == bestDistance && lengthDifference < bestLength)
            {
                best = candidate;
                bestDistance = distance;
                bestLength = lengthDifference;
                if (distance == 0) break;
            }
        }
        return best;
    }

    // Bölüm sayısı aynıysa her bölümdeki değişiklik o bölümün uzunluğuyla sınırlı: bg-blu-600 → bg-blue-600 olur, ama
    // bg-nope-500 → bg-rose-500 (dört harften ikisi farklı) önerilmez.
    private static bool Plausible(string value, string candidate)
    {
        var a = value.Split('-');
        var b = candidate.Split('-');
        if (a.Length != b.Length) return true;
        for (int i = 0; i < a.Length; i++)
        {
            int limit = Math.Max(1, Math.Max(a[i].Length, b[i].Length) / 3);
            if (Distance(a[i], b[i], limit + 1) > limit) return false;
        }
        return true;
    }

    // Yer değiştirmeyi (bleu → blue) tek düzenleme sayan Damerau-Levenshtein (optimal string alignment). Satırın en küçüğü
    // sınırı aşınca erken döner.
    private static int Distance(string a, string b, int bound)
    {
        var previous2 = new int[b.Length + 1];
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) previous[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            int rowMin = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                int value = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) value = Math.Min(value, previous2[j - 2] + 1);
                current[j] = value;
                if (value < rowMin) rowMin = value;
            }
            if (rowMin >= bound) return bound;
            var recycled = previous2;
            previous2 = previous;
            previous = current;
            current = recycled;
        }
        return previous[b.Length];
    }
}
