using System.Diagnostics.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace FormWind;

/// <summary>A variant (e.g. <c>dark:hover</c> or <c>dark:header</c>) and its style.</summary>
internal sealed class TwVariant
{
    internal TwVariant(TwState conditions, string? part, string name)
    {
        Conditions = conditions;
        Part = part;
        Name = name;
    }

    /// <summary>States required for the variant to apply. <see cref="TwState.None"/> = base classes.</summary>
    public TwState Conditions { get; }

    /// <summary>
    /// A part of the control (e.g. <c>header</c>, <c>cell</c> on a DataGridView). <c>null</c> = the control itself.
    /// Part styles are used only by the applier that knows that part.
    /// </summary>
    public string? Part { get; }

    public string Name { get; }

    public TwStyle Style { get; } = new TwStyle();
}

/// <summary>The parse result of a class string. Immutable and cached by the theme.</summary>
[Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
public sealed class TwParsed
{
    public static readonly TwParsed Empty = new TwParsed("", Array.Empty<TwVariant>(), Array.Empty<string>());

    internal TwParsed(string classes, IReadOnlyList<TwVariant> variants, IReadOnlyList<string> unknown)
    {
        Classes = classes;
        Variants = variants;
        Unknown = unknown;
    }

    public string Classes { get; }

    /// <summary>Variants in application order.</summary>
    internal IReadOnlyList<TwVariant> Variants { get; }

    /// <summary>Unknown classes (with their variant prefixes).</summary>
    public IReadOnlyList<string> Unknown { get; }

    /// <summary>For the control itself: merges, in order, the variants whose conditions are within <paramref name="state"/>.</summary>
    public TwStyle Resolve(TwState state) => ResolvePart(null, state);

    /// <summary>For a part (<c>null</c> = the control itself): merges the variants whose conditions are within <paramref name="state"/>.</summary>
    /// <remarks>
    /// If <paramref name="state"/> contains <see cref="TwState.HighContrast"/>, the colors of other variants are converted to system
    /// colors; <c>forced-colors:</c> variants (last, since they have the highest bit) are applied on top.
    /// </remarks>
    public TwStyle ResolvePart(string? part, TwState state)
    {
        var style = new TwStyle();
        bool forced = (state & TwState.HighContrast) != 0;
        foreach (var variant in Variants)
        {
            if (variant.Part != part || (variant.Conditions & state) != variant.Conditions) continue;
            if (forced && (variant.Conditions & TwState.HighContrast) != 0)
            {
                style.ApplyForcedColors(part, state);
                forced = false;
            }
            style.MergeFrom(variant.Style);
        }
        if (forced) style.ApplyForcedColors(part, state);
        return style;
    }

    /// <summary>Whether any variant (including part variants) uses one of the given states.</summary>
    public bool UsesAny(TwState condition)
    {
        foreach (var variant in Variants)
            if ((variant.Conditions & condition) != 0) return true;
        return false;
    }

    /// <summary>Whether the control itself has a variant with <paramref name="condition"/> among its conditions.</summary>
    public bool HasCondition(TwState condition)
    {
        foreach (var variant in Variants)
        {
            if (variant.Part is null && (variant.Conditions & condition) != 0) return true;
        }
        return false;
    }

    /// <summary>Whether the part has a variant with <paramref name="condition"/> among its conditions (e.g. <c>row:hover:</c>).</summary>
    public bool HasPartCondition(string part, TwState condition)
    {
        foreach (var variant in Variants)
        {
            if (variant.Part == part && (variant.Conditions & condition) == condition) return true;
        }
        return false;
    }

    /// <summary>
    /// Merges the variants whose conditions contain all of <paramref name="required"/> and stay within
    /// <paramref name="allowed"/> (control itself only). For libraries that keep states separate (e.g. Krypton).
    /// </summary>
    public TwStyle ResolveExact(TwState required, TwState allowed)
    {
        var style = new TwStyle();
        foreach (var variant in Variants)
        {
            if (variant.Part is null
                && (variant.Conditions & required) == required
                && (variant.Conditions & ~allowed) == 0)
                style.MergeFrom(variant.Style);
        }
        return style;
    }
}
