using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace FormWind.Design;

/// <summary>
/// One field of the smart tag's class builder (background, padding, hover background …): the classes of some groups under one
/// variant. Reading returns those classes; writing replaces them and keeps the rest of the string. Because the fields are plain
/// properties with <see cref="TypeConverter"/>s, they work in the out-of-process (.NET 8+) Designer too.
/// </summary>
internal sealed class TwClassCategory
{
    private readonly HashSet<string> _groups;
    private readonly Func<TwTheme, IEnumerable<string>> _values;

    public TwClassCategory(string variant, string[] groups, Func<TwTheme, IEnumerable<string>> values)
    {
        Variant = variant;
        _groups = new HashSet<string>(groups, StringComparer.Ordinal);
        _values = values;
    }

    public string Variant { get; }

    public string Get(string? classes) => TwClassMerge.Select(classes, Variant, _groups);

    public string Set(string? classes, string? value) => TwParser.Normalize(TwClassMerge.Replace(classes, Variant, _groups, value));

    /// <summary>Suggested values (free text may be typed too).</summary>
    public IEnumerable<string> Values(TwTheme theme) => _values(theme);
}

internal static class TwClassCategories
{
    private static IEnumerable<string> Colors(TwTheme theme, string prefix) => theme.ColorKeys().Select(key => prefix + "-" + key);

    private static Func<TwTheme, IEnumerable<string>> Fixed(params string[] values) => _ => values;

    public static readonly TwClassCategory Background = new TwClassCategory("", new[] { "bg-color" }, t => Colors(t, "bg"));
    public static readonly TwClassCategory TextColor = new TwClassCategory("", new[] { "text-color" }, t => Colors(t, "text"));
    public static readonly TwClassCategory BorderColor = new TwClassCategory("", new[] { "border-color" }, t => Colors(t, "border"));

    public static readonly TwClassCategory Border = new TwClassCategory("", new[] { "border-width", "border-side" },
        Fixed("border-0", "border", "border-2", "border-4", "border-8", "border-b", "border-t", "border-x", "border-y"));

    public static readonly TwClassCategory Padding = new TwClassCategory("", new[] { "p" },
        Fixed("p-0", "p-1", "p-2", "p-3", "p-4", "p-5", "p-6", "p-8", "px-2 py-1", "px-3 py-1.5", "px-3 py-2", "px-4 py-2", "px-6 py-3"));

    public static readonly TwClassCategory Corners = new TwClassCategory("", new[] { "rounded" },
        Fixed("rounded-none", "rounded-sm", "rounded", "rounded-md", "rounded-lg", "rounded-xl", "rounded-2xl", "rounded-3xl", "rounded-full"));

    public static readonly TwClassCategory TextSize = new TwClassCategory("", new[] { "font-size" },
        Fixed("text-xs", "text-sm", "text-base", "text-lg", "text-xl", "text-2xl", "text-3xl", "text-4xl"));

    public static readonly TwClassCategory FontWeight = new TwClassCategory("", new[] { "font-weight" },
        Fixed("font-thin", "font-extralight", "font-light", "font-normal", "font-medium", "font-semibold", "font-bold", "font-extrabold", "font-black"));

    public static readonly TwClassCategory HoverBackground = new TwClassCategory("hover:", new[] { "bg-color" }, t => Colors(t, "hover:bg"));
    public static readonly TwClassCategory DarkBackground = new TwClassCategory("dark:", new[] { "bg-color" }, t => Colors(t, "dark:bg"));
    public static readonly TwClassCategory DarkTextColor = new TwClassCategory("dark:", new[] { "text-color" }, t => Colors(t, "dark:text"));

    /// <summary>The component names (<c>btn-primary</c>, <c>card</c> …) in the class string.</summary>
    public static string Components(TwTheme theme, string? classes) =>
        string.Join(" ", TwClassMerge.Split(classes).Where(t => theme.GetComponent(t) != null));

    /// <summary>
    /// Replaces the components in the class string with <paramref name="component"/> and keeps the utilities. The component goes
    /// first, so the utilities after it still override it.
    /// </summary>
    public static string WithComponent(TwTheme theme, string? classes, string? component)
    {
        var utilities = TwClassMerge.Split(classes).Where(t => theme.GetComponent(t) is null);
        return string.Join(" ", TwClassMerge.Split(component).Concat(utilities));
    }
}

/// <summary>Drop-down of a <see cref="TwClassCategory"/>'s suggested values; the values come from the edited provider's theme.</summary>
internal abstract class TwClassValuesConverter : StringConverter
{
    private readonly TwClassCategory _category;

    protected TwClassValuesConverter(TwClassCategory category) => _category = category;

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) =>
        new StandardValuesCollection(_category.Values(TwDesignTheme.Of(context)).ToArray());
}

internal sealed class TwBackgroundValues : TwClassValuesConverter { public TwBackgroundValues() : base(TwClassCategories.Background) { } }
internal sealed class TwTextColorValues : TwClassValuesConverter { public TwTextColorValues() : base(TwClassCategories.TextColor) { } }
internal sealed class TwBorderColorValues : TwClassValuesConverter { public TwBorderColorValues() : base(TwClassCategories.BorderColor) { } }
internal sealed class TwBorderValues : TwClassValuesConverter { public TwBorderValues() : base(TwClassCategories.Border) { } }
internal sealed class TwPaddingValues : TwClassValuesConverter { public TwPaddingValues() : base(TwClassCategories.Padding) { } }
internal sealed class TwCornersValues : TwClassValuesConverter { public TwCornersValues() : base(TwClassCategories.Corners) { } }
internal sealed class TwTextSizeValues : TwClassValuesConverter { public TwTextSizeValues() : base(TwClassCategories.TextSize) { } }
internal sealed class TwFontWeightValues : TwClassValuesConverter { public TwFontWeightValues() : base(TwClassCategories.FontWeight) { } }
internal sealed class TwHoverBackgroundValues : TwClassValuesConverter { public TwHoverBackgroundValues() : base(TwClassCategories.HoverBackground) { } }
internal sealed class TwDarkBackgroundValues : TwClassValuesConverter { public TwDarkBackgroundValues() : base(TwClassCategories.DarkBackground) { } }
internal sealed class TwDarkTextColorValues : TwClassValuesConverter { public TwDarkTextColorValues() : base(TwClassCategories.DarkTextColor) { } }

/// <summary>Drop-down of the theme's components; those suited to the control type come first.</summary>
internal sealed class TwComponentValues : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var theme = TwDesignTheme.Of(context);
        var suggested = context?.Instance is TailwindProviderDesigner.ClassesActionList list
            ? list.SuggestedComponents().ToList()
            : new List<string>();
        var others = theme.Components.Keys.Where(k => !suggested.Contains(k)).OrderBy(k => k, StringComparer.Ordinal);
        return new StandardValuesCollection(suggested.Concat(others).ToArray());
    }
}
