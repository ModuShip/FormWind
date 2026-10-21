using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace FormWind.Mcp;

public sealed record UnknownClass(
    [property: Description("The class as written, variant prefixes included.")] string Class,
    [property: Description("The closest recognized class, or null when nothing is close.")] string? Suggestion);

public sealed record ValidationResult(
    [property: Description("True when every class is recognized.")] bool Valid,
    IReadOnlyList<UnknownClass> Unknown,
    [property: Description("Theme components used in the classes (btn-primary, card …).")] IReadOnlyList<string> Components);

public sealed record ComponentExpansion(string Name, string Classes);

public sealed record StateStyle(
    [property: Description("The state the style is resolved for: base, or variants such as dark, hover, dark:hover.")] string State,
    [property: Description("The control part (header, cell, item …), or null for the control itself.")] string? Part,
    [property: Description("Properties the classes set in this state. Colors are #rrggbb (#rrggbbaa when translucent); pixel values are at 96 DPI.")]
    IReadOnlyDictionary<string, object> Style);

public sealed record Explanation(
    string Classes,
    IReadOnlyList<UnknownClass> Unknown,
    IReadOnlyList<ComponentExpansion> Components,
    IReadOnlyList<StateStyle> States);

public sealed record ComponentInfo(string Name, string Classes);

public sealed record ComponentList(
    string Preset,
    IReadOnlyList<ComponentInfo> Components,
    [property: Description("AutoStyle: the component each control type gets (base types count too; empty = left unstyled).")]
    IReadOnlyDictionary<string, string> AutoStyle);

public sealed record VocabularyCategory(string Name, IReadOnlyList<string> Patterns, IReadOnlyList<string> Examples);

public sealed record Vocabulary(
    IReadOnlyList<VocabularyCategory> Categories,
    [property: Description("Color names usable in color classes (bg-{name}-{shade}); the theme's own colors first.")] IReadOnlyList<string> Colors,
    IReadOnlyList<string> Shades,
    IReadOnlyList<string> Variants);

[McpServerToolType]
public sealed class ClassTools
{
    [McpServerTool(Name = "validate_classes", Title = "Validate FormWind classes", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Checks a FormWind class string. Returns the unknown classes with a 'did you mean' suggestion each (FormWind ignores unknown classes silently at run time) and the theme components used.")]
    public static ValidationResult ValidateClasses(
        [Description("Class string, e.g. \"btn-primary px-6 dark:hover:bg-blue-700\".")] string classes,
        [Description(Themes.PresetDescription)] string? preset = null,
        [Description(Themes.ThemeFileDescription)] string? themeFile = null)
    {
        var theme = Themes.Resolve(preset, themeFile);
        var unknown = Unknown(theme, classes);
        return new ValidationResult(unknown.Count == 0, unknown, Components(theme, classes).Select(c => c.Name).ToList());
    }

    [McpServerTool(Name = "explain_classes", Title = "Explain FormWind classes", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Resolves a FormWind class string to the property values it sets (BackColor, ForeColor, Font, Padding, Radius …) in the base state and in every variant the classes use (dark, hover, disabled, parts such as header). Pass states to resolve combinations such as dark:hover.")]
    public static Explanation ExplainClasses(
        [Description("Class string.")] string classes,
        [Description("Extra states to resolve, e.g. [\"dark:hover\", \"dark:disabled\"]. Each one merges every variant it contains, as at run time.")]
        string[]? states = null,
        [Description(Themes.PresetDescription)] string? preset = null,
        [Description(Themes.ThemeFileDescription)] string? themeFile = null)
    {
        var theme = Themes.Resolve(preset, themeFile);
        var parsed = theme.Parse(classes);

        var resolved = new List<StateStyle> { new("base", null, Describe(parsed.Resolve(TwState.None))) };
        var seen = new HashSet<(TwState, string?)> { (TwState.None, null) };
        foreach (var variant in parsed.Variants)
        {
            if (!seen.Add((variant.Conditions, variant.Part))) continue;
            resolved.Add(new StateStyle(variant.Conditions == TwState.None ? "base" : StateName(variant.Name, variant.Part),
                variant.Part, Describe(parsed.ResolvePart(variant.Part, variant.Conditions))));
        }
        foreach (var state in states ?? Array.Empty<string>())
        {
            if (!TwParser.TryParseConditions(state.Trim().TrimEnd(':'), out var conditions, out var part))
                throw ToolErrors.Create("unknown_state", $"Unknown state '{state}'.",
                    "Use variants such as dark, hover, focus, active, disabled, checked or a part such as header, joined with ':' (dark:hover).");
            if (!seen.Add((conditions, part))) continue;
            resolved.Add(new StateStyle(StateName(state.Trim().TrimEnd(':'), part), part, Describe(parsed.ResolvePart(part, conditions))));
        }

        return new Explanation(parsed.Classes, Unknown(theme, classes), Components(theme, classes), resolved);
    }

    [McpServerTool(Name = "list_components", Title = "List FormWind components", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists the theme components (btn-primary, card, input …) with the classes each expands to, and the AutoStyle mapping from control types to components. Every preset defines the same component names; prefer components over long utility strings.")]
    public static ComponentList ListComponents(
        [Description(Themes.PresetDescription)] string? preset = null,
        [Description(Themes.ThemeFileDescription)] string? themeFile = null)
    {
        var theme = Themes.Resolve(preset, themeFile);
        var semantic = new HashSet<string>(TwTheme.SemanticColors.Keys, StringComparer.Ordinal);
        var components = theme.Components
            .Where(pair => !semantic.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new ComponentInfo(pair.Key, pair.Value))
            .ToList();
        var autoStyle = theme.AutoStyle.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
        return new ComponentList(Themes.ParsePreset(preset).ToString().ToLowerInvariant(), components, autoStyle);
    }

    [McpServerTool(Name = "list_vocabulary", Title = "List FormWind class patterns", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists the class patterns FormWind recognizes by category (colors, typography, spacing, sizing, borders, effects, layout, other), the color names and shades of the theme, and the variants. FormWind supports a subset of Tailwind CSS v3; classes outside it are ignored.")]
    public static Vocabulary ListVocabulary(
        [Description("Only this category: colors, typography, spacing, sizing, borders, effects, layout or other.")] string? category = null,
        [Description(Themes.PresetDescription)] string? preset = null,
        [Description(Themes.ThemeFileDescription)] string? themeFile = null)
    {
        var theme = Themes.Resolve(preset, themeFile);
        var categories = VocabularyData.Categories;
        if (!string.IsNullOrWhiteSpace(category))
        {
            categories = categories.Where(c => string.Equals(c.Name, category.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
            if (categories.Count == 0)
                throw ToolErrors.Create("unknown_category", $"Unknown category '{category}'.",
                    $"Use one of: {string.Join(", ", VocabularyData.Categories.Select(c => c.Name))}, or omit it.");
        }
        var colors = theme.ColorKeys().Select(key => key.Split('-')[0]).Distinct(StringComparer.Ordinal).ToList();
        return new Vocabulary(categories, colors, TwPalette.Shades, VocabularyData.Variants);
    }

    private static List<UnknownClass> Unknown(TwTheme theme, string classes) =>
        theme.Parse(classes).Unknown.Select(token => new UnknownClass(token, theme.SuggestClass(token))).ToList();

    private static List<ComponentExpansion> Components(TwTheme theme, string classes) =>
        TwParser.Normalize(classes).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .Select(token => theme.GetComponent(token) is string expansion ? new ComponentExpansion(token, expansion) : null)
            .OfType<ComponentExpansion>()
            .ToList();

    // Parça adı durum adından çıkarılır (dark:header → dark); parça ayrı alanda verilir.
    private static string StateName(string variant, string? part)
    {
        var segments = variant.Split(':').Where(s => s != part).ToList();
        return segments.Count == 0 ? "base" : string.Join(":", segments);
    }

    private static readonly PropertyInfo[] StyleProperties = typeof(TwStyle)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => !p.Name.StartsWith("Has", StringComparison.Ordinal))
        .ToArray();

    /// <summary>The properties a style sets, with JSON-friendly values.</summary>
    internal static IReadOnlyDictionary<string, object> Describe(TwStyle style)
    {
        var result = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var property in StyleProperties)
        {
            var value = property.GetValue(style);
            var described = value switch
            {
                null => null,
                TwSides sides => sides.IsEmpty ? null : Sides(sides),
                Color color => Hex(color),
                TwLength length => length.ToString(),
                Cursor cursor => CursorName(cursor),
                Enum e => e.ToString(),
                float f => (object)Math.Round(f, 3),
                _ => value,
            };
            if (described != null) result[char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1)] = described;
        }
        return result;
    }

    private static object Sides(TwSides sides)
    {
        if (sides.Left == sides.Top && sides.Top == sides.Right && sides.Right == sides.Bottom) return sides.Left!.Value;
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (sides.Left is int left) result["left"] = left;
        if (sides.Top is int top) result["top"] = top;
        if (sides.Right is int right) result["right"] = right;
        if (sides.Bottom is int bottom) result["bottom"] = bottom;
        return result;
    }

    internal static string Hex(Color color) =>
        color.A == 0 ? "transparent"
        : color.A == 255 ? $"#{color.R:x2}{color.G:x2}{color.B:x2}"
        : $"#{color.R:x2}{color.G:x2}{color.B:x2}{color.A:x2}";

    private static string CursorName(Cursor cursor) =>
        cursor == Cursors.Hand ? "pointer" : cursor == Cursors.IBeam ? "text" : cursor == Cursors.Default ? "default"
        : cursor.ToString();
}

/// <summary>Class patterns by category, as llms.txt lists them. VocabularyTests checks that every example is recognized.</summary>
internal static class VocabularyData
{
    public static readonly IReadOnlyList<string> Variants = new[]
    {
        "hover", "focus", "focus-visible", "active", "disabled", "checked", "invalid", "group-hover", "dark", "forced-colors", "rtl", "ltr",
        "sm", "md", "lg", "xl", "2xl",
        "header", "cell", "row", "row-header", "alt", "selected", "match", "group", "footer", "item", "menu", "caption", "placeholder",
        "label", "hint", "error",
    };

    public static readonly IReadOnlyList<VocabularyCategory> Categories = new[]
    {
        new VocabularyCategory("colors",
            new[] { "{bg|text|border|divide|ring|ring-offset|shadow|from|via|to|fill|accent}-{color}-{shade}", "{target}-{color}", "{target}-{color}-{shade}/{0-100}", "{target}-[#rrggbb]" },
            new[] { "bg-blue-600", "text-white", "border-gray-200", "bg-primary-600", "text-surface-500", "bg-black/50", "bg-[#1e3f6e]" }),
        new VocabularyCategory("typography",
            new[] { "text-{xs|sm|base|lg|xl|2xl|3xl|4xl}", "text-[{n}px]", "font-{thin|extralight|light|normal|medium|semibold|bold|extrabold|black}", "font-{sans|mono}", "italic", "not-italic", "underline", "no-underline", "text-{left|center|right}", "truncate", "whitespace-{nowrap|normal}" },
            new[] { "text-sm", "text-2xl", "text-[13px]", "font-semibold", "font-mono", "italic", "text-center", "truncate" }),
        new VocabularyCategory("spacing",
            new[] { "{p|m}{|x|y|t|r|b|l}-{0|0.5|1|1.5|2|…|12|14|16|20|24|px|[{n}px]}", "gap{|-x|-y}-{n}" },
            new[] { "p-4", "px-4", "py-2", "pt-1.5", "m-2", "p-[10px]", "gap-4", "gap-x-2" }),
        new VocabularyCategory("sizing",
            new[] { "{w|h}-{n|px|[{n}px]|full|{a}/{b}}", "{min|max}-{w|h}-{n|xs|sm|md|lg|xl|2xl|…|7xl}", "basis-{n|full|auto|{a}/{b}}" },
            new[] { "w-40", "h-10", "w-full", "w-1/2", "max-w-md", "min-h-8", "basis-1/3" }),
        new VocabularyCategory("borders",
            new[] { "border", "border-{0|2|4|8}", "border-{x|y|t|r|b|l}{|-0|-2|-4|-8}", "rounded{|-none|-sm|-md|-lg|-xl|-2xl|-3xl|-full|-[{n}px]}" },
            new[] { "border", "border-2", "border-b", "border-t-2", "rounded-lg", "rounded-full", "rounded-[10px]" }),
        new VocabularyCategory("effects",
            new[] { "shadow{|-sm|-md|-lg|-xl|-2xl|-none}", "ring{|-0|-1|-2|-4|-8}", "ring-offset-{0|1|2|4|8}", "bg-gradient-to-{r|l|t|b|tr|tl|br|bl}", "transition{|-colors|-all|-none}", "duration-{0|75|100|150|200|300|500|700|1000|[{n}ms]}", "backdrop-{mica|acrylic|tabbed|none}" },
            new[] { "shadow-md", "ring-2", "ring-offset-2", "bg-gradient-to-r", "transition", "duration-150", "backdrop-mica" }),
        new VocabularyCategory("layout",
            new[] { "block", "flex", "grid", "flex-{row|col|wrap|nowrap}", "flex-{1|auto|initial|none}", "grow{|-0}", "shrink{|-0}", "justify-{start|end|center|between|around|evenly}", "items-{start|end|center|stretch|baseline}", "self-{auto|start|end|center|stretch}", "grid-cols-{1-12}", "grid-rows-{1-12}", "col-span-{1-12|full}", "row-span-{1-12|full}" },
            new[] { "flex", "flex-col", "flex-1", "justify-between", "items-center", "grid", "grid-cols-3", "col-span-2", "shrink-0" }),
        new VocabularyCategory("other",
            new[] { "hidden", "visible", "cursor-{pointer|default|text}", "group", "object-{cover|contain|fill}", "divide-{x|y}{|-0}" },
            new[] { "hidden", "cursor-pointer", "group", "object-cover", "divide-y" }),
    };
}
