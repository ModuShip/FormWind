using System.Diagnostics.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Applies the computed style to a family of controls.</summary>
[Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
public interface IStyleApplier
{
    bool CanApply(Control control);

    /// <param name="control">The control to style.</param>
    /// <param name="style">The merged style for the control's current state.</param>
    /// <param name="context">Parse result, state, DPI scale and the control's original values.</param>
    void Apply(Control control, TwStyle style, TwApplyContext context);
}

[Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
public sealed class TwApplyContext
{
    internal TwApplyContext(TwParsed parsed, TwState state, float scale, TwTheme theme, TwOriginal original, bool designMode)
    {
        Parsed = parsed ?? throw new ArgumentNullException(nameof(parsed));
        State = state;
        Scale = scale;
        Theme = theme ?? throw new ArgumentNullException(nameof(theme));
        Original = original ?? throw new ArgumentNullException(nameof(original));
        DesignMode = designMode;
    }

    public TwParsed Parsed { get; }
    public TwState State { get; }

    /// <summary><c>DeviceDpi / 96</c>.</summary>
    public float Scale { get; }

    public TwTheme Theme { get; }
    public TwOriginal Original { get; }
    public bool DesignMode { get; }

    /// <summary>Whether native scrollbars are darkened in dark mode too (<see cref="TailwindProvider.DarkScrollbars"/>).</summary>
    public bool DarkScrollbars { get; init; } = true;
}

/// <summary>
/// Registered appliers. The last registered is tried first; if none matches, <see cref="WinFormsApplier"/> is used.
/// </summary>
[Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
public static class TwAppliers
{
    private static readonly object Gate = new object();
    private static readonly List<IStyleApplier> Registered = new List<IStyleApplier>
    {
        new DataGridViewApplier(),
        new ToolStripApplier(),
        new FormApplier(),
    };
    private static readonly IStyleApplier Fallback = new WinFormsApplier();
    private static readonly IStyleApplier[] BuiltIn = Registered.ToArray();

    public static void Register(IStyleApplier applier)
    {
        if (applier is null) throw new ArgumentNullException(nameof(applier));
        lock (Gate) Registered.Insert(0, applier);
    }

    public static IStyleApplier Find(Control control)
    {
        lock (Gate)
        {
            foreach (var applier in Registered)
            {
                if (applier.CanApply(control)) return applier;
            }
        }
        return Fallback;
    }

    /// <summary>
    /// FormWind's own applier; registered appliers are skipped. A control library's applier can leave structural styling
    /// (e.g. DataGridView row height, grid lines) to it and fill only its own palette.
    /// </summary>
    public static IStyleApplier FindBuiltIn(Control control)
    {
        if (control is null) throw new ArgumentNullException(nameof(control));
        foreach (var applier in BuiltIn)
        {
            if (applier.CanApply(control)) return applier;
        }
        return Fallback;
    }
}
