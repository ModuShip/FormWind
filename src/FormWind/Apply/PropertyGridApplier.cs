using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Styles a PropertyGrid through its own color properties; its inner parts (grid view, help pane, commands pane) are not
/// styled as controls:
/// <list type="bullet">
/// <item><description>base classes: <c>bg-*</c> the grid view, help and commands panes and the toolbar area; <c>text-*</c>
/// their text and category names; <c>divide-{color}</c> the lines between them (view, help and commands borders,
/// category splitter)</description></item>
/// <item><description><c>header:</c> category rows and the left margin (<c>LineColor</c>, <c>CategoryForeColor</c>)</description></item>
/// <item><description><c>selected:</c> the selected property while the grid has focus</description></item>
/// </list>
/// Read-only properties are drawn with the text color mixed halfway toward the background. Removing the classes restores
/// the original colors.
/// </summary>
internal sealed class PropertyGridApplier : WinFormsApplier
{
    private static readonly ConditionalWeakTable<PropertyGrid, Original> Originals = new ConditionalWeakTable<PropertyGrid, Original>();

    public override bool CanApply(Control control) => control is PropertyGrid;

    public override void Apply(Control control, TwStyle style, TwApplyContext context)
    {
        var grid = (PropertyGrid)control;
        var original = Originals.GetValue(grid, Original.Capture);

        base.Apply(control, style, context);

        var state = context.State & ~(TwState.Hover | TwState.Active);
        var header = context.Parsed.ResolvePart("header", state);
        var selected = context.Parsed.ResolvePart("selected", state);
        // PropertyGrid saydam renkleri reddeder; saydam class'lar özgün renge düşer.
        var back = Opaque(style.BackColor);
        var fore = Opaque(style.ForeColor);
        var line = Opaque(style.DivideColor);

        Set(grid.ViewBackColor, back ?? original.ViewBack, v => grid.ViewBackColor = v);
        Set(grid.HelpBackColor, back ?? original.HelpBack, v => grid.HelpBackColor = v);
        Set(grid.CommandsBackColor, back ?? original.CommandsBack, v => grid.CommandsBackColor = v);
        Set(grid.ViewForeColor, fore ?? original.ViewFore, v => grid.ViewForeColor = v);
        Set(grid.HelpForeColor, fore ?? original.HelpFore, v => grid.HelpForeColor = v);
        Set(grid.CommandsForeColor, fore ?? original.CommandsFore, v => grid.CommandsForeColor = v);
        Set(grid.LineColor, Opaque(header.BackColor) ?? original.Line, v => grid.LineColor = v);
        Set(grid.CategoryForeColor, Opaque(header.ForeColor) ?? fore ?? original.CategoryFore, v => grid.CategoryForeColor = v);
        Set(grid.ViewBorderColor, line ?? original.ViewBorder, v => grid.ViewBorderColor = v);
        Set(grid.HelpBorderColor, line ?? original.HelpBorder, v => grid.HelpBorderColor = v);
        Set(grid.CommandsBorderColor, line ?? original.CommandsBorder, v => grid.CommandsBorderColor = v);
        Set(grid.CategorySplitterColor, line ?? original.CategorySplitter, v => grid.CategorySplitterColor = v);
        Set(grid.SelectedItemWithFocusBackColor, Opaque(selected.BackColor) ?? original.SelectedBack, v => grid.SelectedItemWithFocusBackColor = v);
        Set(grid.SelectedItemWithFocusForeColor, Opaque(selected.ForeColor) ?? original.SelectedFore, v => grid.SelectedItemWithFocusForeColor = v);
        var disabled = fore is Color f && (back ?? grid.ViewBackColor) is Color b
            ? Color.FromArgb((f.R + b.R) / 2, (f.G + b.G) / 2, (f.B + b.B) / 2)
            : original.DisabledFore;
        Set(grid.DisabledItemForeColor, disabled, v => grid.DisabledItemForeColor = v);

        // Kaydırma çubuğu iç görünümün ScrollBar alt kontrolüdür; PropertyGrid'in kendi penceresine atanan tema ona geçmez.
        if (!context.DesignMode)
        {
            bool dark = context.DarkScrollbars && (context.State & TwState.Dark) != 0;
            foreach (var bar in ScrollBars(grid))
                if (dark || TwNativeTheme.IsDark(bar)) TwNativeTheme.Apply(bar, dark);
        }
    }

    private static System.Collections.Generic.IEnumerable<ScrollBar> ScrollBars(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is ScrollBar bar) yield return bar;
            foreach (var nested in ScrollBars(child)) yield return nested;
        }
    }

    private static Color? Opaque(Color? color) => color is Color c && c.A == 255 ? c : null;

    private static void Set(Color current, Color value, System.Action<Color> assign)
    {
        if (current.ToArgb() != value.ToArgb()) assign(value);
    }

    private sealed class Original
    {
        public Color ViewBack, ViewFore, HelpBack, HelpFore, CommandsBack, CommandsFore, Line, CategoryFore;
        public Color ViewBorder, HelpBorder, CommandsBorder, CategorySplitter, SelectedBack, SelectedFore, DisabledFore;

        public static Original Capture(PropertyGrid grid) => new Original
        {
            ViewBack = grid.ViewBackColor,
            ViewFore = grid.ViewForeColor,
            HelpBack = grid.HelpBackColor,
            HelpFore = grid.HelpForeColor,
            CommandsBack = grid.CommandsBackColor,
            CommandsFore = grid.CommandsForeColor,
            Line = grid.LineColor,
            CategoryFore = grid.CategoryForeColor,
            ViewBorder = grid.ViewBorderColor,
            HelpBorder = grid.HelpBorderColor,
            CommandsBorder = grid.CommandsBorderColor,
            CategorySplitter = grid.CategorySplitterColor,
            SelectedBack = grid.SelectedItemWithFocusBackColor,
            SelectedFore = grid.SelectedItemWithFocusForeColor,
            DisabledFore = grid.DisabledItemForeColor,
        };
    }
}
