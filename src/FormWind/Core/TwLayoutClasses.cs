using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace FormWind;

/// <summary>
/// The classes that change a control's size or position: width and height, spacing, flex and grid layout, font size and
/// line height, in any variant or part (<c>row:h-10</c>, <c>md:w-64</c>). <see cref="TailwindProvider.PreserveLayout"/>
/// leaves them out, so the bounds laid out in the Designer stay as they are.
/// </summary>
internal static class TwLayoutClasses
{
    private static readonly Regex Layout = new Regex(
        @"^(?:"
        + @"(?:w|h|size|min-w|max-w|min-h|max-h)-.+"
        + @"|(?:p|px|py|pt|pr|pb|pl|ps|pe|m|mx|my|mt|mr|mb|ml|ms|me)-.+"
        + @"|(?:inline-)?(?:flex|grid)"
        + @"|(?:flex|grid|gap|justify|items|self|content|place|basis|order|col|row)-.+"
        + @"|(?:grow|shrink)(?:-.+)?"
        + @"|text-(?:xs|sm|base|lg|xl|[2-9]xl)(?:/.+)?"
        + @"|text-\[[0-9.]+(?:px|pt|rem|em)\]"
        + @"|leading-.+"
        + @")$",
        RegexOptions.CultureInvariant);

    /// <summary>The classes without the layout classes.</summary>
    public static string Strip(string? classes)
    {
        if (string.IsNullOrWhiteSpace(classes)) return classes ?? "";
        var kept = classes!.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Where(c => !IsLayout(c));
        return string.Join(" ", kept);
    }

    /// <summary>Whether the class sets a size, spacing, layout or font size.</summary>
    public static bool IsLayout(string token)
    {
        int colon = LastVariantColon(token);
        var utility = colon < 0 ? token : token.Substring(colon + 1);
        utility = utility.TrimStart('!', '-');
        return Layout.IsMatch(utility);
    }

    // The colon that ends the variants; colons inside [arbitrary values] belong to the value.
    private static int LastVariantColon(string token)
    {
        int depth = 0, last = -1;
        for (int i = 0; i < token.Length; i++)
        {
            char c = token[i];
            if (c == '[') depth++;
            else if (c == ']') depth = Math.Max(0, depth - 1);
            else if (c == ':' && depth == 0) last = i;
        }
        return last;
    }
}
