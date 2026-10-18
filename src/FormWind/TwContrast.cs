using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// WCAG contrast audit. <see cref="Audit"/> scans the current colors of a control tree (dark if dark mode is on):
/// controls with text, ToolStrip/menu colors (normal, hover, drop-down) and DataGridView cell/header/selection
/// colors. When a provider is given, the <c>hover:</c> variants in the classes are resolved and checked too.
/// </summary>
public static class TwContrast
{
    /// <summary>WCAG AA threshold for normal text.</summary>
    public const double TextMinimum = 4.5;

    /// <summary>WCAG AA threshold for large text (18 pt, or 14 pt bold).</summary>
    public const double LargeTextMinimum = 3.0;

    /// <summary>WCAG 2 contrast ratio (1–21). A transparent foreground is blended over the background.</summary>
    public static double Ratio(Color foreground, Color background)
    {
        var fore = Composite(foreground, background);
        double l1 = Luminance(fore), l2 = Luminance(background);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    /// <summary>
    /// Low-contrast color pairs in <paramref name="root"/> and its descendants. Hidden controls are skipped. Disabled controls
    /// are outside the scope of WCAG and skipped by default; <paramref name="includeDisabled"/> checks them too. This helps
    /// when a disabled control is used to display content (e.g. a RichTextBox disabled to hide the caret). For disabled
    /// native text boxes the background is the system background Windows paints (<see cref="SystemColors.Control"/>).
    /// For disabled buttons the text color drawn on screen is checked against 3:1 (to catch invisible text). For colors in
    /// the middle of a <c>transition</c>, the target color is checked, so the result is correct right after a theme change.
    /// </summary>
    public static IReadOnlyList<TwContrastIssue> Audit(Control root, TailwindProvider? provider = null, double minimum = TextMinimum, bool includeDisabled = false)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        var issues = new List<TwContrastIssue>();
        Visit(root, provider, minimum, includeDisabled, issues);
        return issues;
    }

    // Visible, ebeveyn zinciri görünür değilse (form henüz gösterilmemişse) her kontrol için false döner; kontrol
    // yalnızca ebeveyni görünürken kendisi görünmüyorsa gizli sayılır.
    private static bool IsHidden(Control control) => !control.Visible && control.Parent is Control parent && parent.Visible;

    private static void Visit(Control control, TailwindProvider? provider, double minimum, bool includeDisabled, List<TwContrastIssue> issues)
    {
        if (IsHidden(control)) return;
        if (!control.Enabled && includeDisabled && control is TextBoxBase)
        {
            // Windows devre dışı metin kutusunun yazı satırının arkasına sistem zeminini çizer; yazı rengi açıkça
            // atanmamışsa sistemin gri rengini kullanır.
            var disabledFore = TwOriginal.IsExplicit(control, nameof(Control.ForeColor)) ? control.ForeColor : SystemColors.GrayText;
            double disabledRequired = IsLarge(control.Font) ? Math.Min(minimum, LargeTextMinimum) : minimum;
            Check(issues, control, "disabled text", disabledFore, SystemColors.Control, disabledRequired);
        }
        else if (!control.Enabled && includeDisabled && control is Button button)
        {
            // Devre dışı butonun ekrana çizilen yazı rengi (bkz. TwDisabledText); Windows'un kendi çizdiği standart
            // butonlar sistem renklerini kullanır ve denetlenmez. Devre dışı öğeler WCAG kapsamında olmadığından eşik
            // büyük metninki (3:1): amaç görünmez yazıyı yakalamak. Geçişi süren renklerde varılacak renk denetlenir.
            if (button is ITwChrome || button.FlatStyle == FlatStyle.Flat)
            {
                var text = TwDisabledText.TextColor(button, Final(button, TwColorChannel.Fore, button.ForeColor), Final(button, TwColorChannel.Back, button.BackColor));
                Check(issues, control, "disabled text", text, EffectiveBack(control), Math.Min(minimum, LargeTextMinimum));
            }
        }
        else if (!control.Enabled && includeDisabled && TwDisabledText.IsRepainted(control))
        {
            // Yazısı FormWind'in yeniden çizdiği devre dışı onay kutusu, radyo düğmesi ve etiket (bkz. TwDisabledText).
            var text = TwDisabledText.TextColor(control, Final(control, TwColorChannel.Fore, control.ForeColor), Final(control, TwColorChannel.Back, control.BackColor));
            Check(issues, control, "disabled text", text, EffectiveBack(control), Math.Min(minimum, LargeTextMinimum));
        }
        else if (control.Enabled || includeDisabled)
        {
            double required = IsLarge(control.Font) ? Math.Min(minimum, LargeTextMinimum) : minimum;
            var back = EffectiveBack(control);
            var fore = Final(control, TwColorChannel.Fore, control.ForeColor);
            switch (control)
            {
                case ToolStrip strip:
                    CheckToolStrip(strip, required, issues);
                    break;
                case DataGridView grid:
                    CheckGrid(grid, back, minimum, issues);
                    break;
                default:
                    if (CarriesText(control)) Check(issues, control, "text", fore, back, required, KeptBackground(control, provider));
                    // LinkLabel bağlantılarını LinkColor ile çizer (class'larda accent-*).
                    if (control is LinkLabel link && link.Enabled && link.Links.Count > 0) Check(issues, control, "link", link.LinkColor, back, required);
                    break;
            }

            // hover: varyantı: çözülen renkler, belirtilmeyen kanal için kontrolün o anki rengi.
            if (provider?.GetParsed(control) is TwParsed parsed && parsed.HasCondition(TwState.Hover) && CarriesText(control))
            {
                var state = provider.BaseState;
                var hover = parsed.Resolve(state | TwState.Hover);
                var hoverBack = hover.BackColor is Color b ? Composite(b, back) : back;
                Check(issues, control, "hover", hover.ForeColor ?? fore, hoverBack, required);
            }
        }

        foreach (Control child in control.Controls)
            Visit(child, provider, minimum, includeDisabled, issues);
    }

    private static void CheckToolStrip(ToolStrip strip, double required, List<TwContrastIssue> issues)
    {
        if (strip.Renderer is TwToolStripRenderer renderer)
        {
            var colors = renderer.Colors;
            Check(issues, strip, "strip", colors.Fore, colors.Back, required);
            Check(issues, strip, "item:hover", colors.ItemHoverFore, Composite(colors.ItemHoverBack, colors.Back), required);
            Check(issues, strip, "menu", colors.Fore, colors.MenuBack, required);
            Check(issues, strip, "menu item:hover", colors.ItemHoverFore, Composite(colors.ItemHoverBack, colors.MenuBack), required);
            return;
        }
        Check(issues, strip, "strip", strip.ForeColor, EffectiveBack(strip), required);
    }

    private static void CheckGrid(DataGridView grid, Color controlBack, double minimum, List<TwContrastIssue> issues)
    {
        var cell = grid.DefaultCellStyle;
        var cellBack = cell.BackColor.IsEmpty ? controlBack : Composite(cell.BackColor, controlBack);
        Check(issues, grid, "cell", cell.ForeColor.IsEmpty ? grid.ForeColor : cell.ForeColor, cellBack, Required(cell.Font ?? grid.Font, minimum));
        if (!cell.SelectionBackColor.IsEmpty && !cell.SelectionForeColor.IsEmpty)
            Check(issues, grid, "selected", cell.SelectionForeColor, Composite(cell.SelectionBackColor, cellBack), Required(cell.Font ?? grid.Font, minimum));
        if (grid.ColumnHeadersVisible && !grid.EnableHeadersVisualStyles)
        {
            var header = grid.ColumnHeadersDefaultCellStyle;
            var headerBack = header.BackColor.IsEmpty ? controlBack : Composite(header.BackColor, controlBack);
            Check(issues, grid, "header", header.ForeColor.IsEmpty ? grid.ForeColor : header.ForeColor, headerBack, Required(header.Font ?? grid.Font, minimum));
        }
    }

    private static double Required(Font font, double minimum) => IsLarge(font) ? Math.Min(minimum, LargeTextMinimum) : minimum;

    private static void Check(List<TwContrastIssue> issues, Control control, string part, Color fore, Color back, double required,
        TwContrastCause cause = TwContrastCause.None)
    {
        if (fore.IsEmpty || back.IsEmpty || fore.A == 0) return;
        double ratio = Ratio(fore, back);
        if (ratio + 1e-9 < required) issues.Add(new TwContrastIssue(control, part, fore, back, ratio, required, cause));
    }

    // Class'lar yazı rengini verip zemini vermiyorsa ve zemin uygulamanın açıkça atadığı, ebeveyninkinden farklı bir
    // renkse: uygulamanın tema motoru zemini kopyalamış, FormWind ebeveyni değiştirmiştir (yazı yeni, zemin eski).
    private static TwContrastCause KeptBackground(Control control, TailwindProvider? provider)
    {
        if (provider?.GetParsed(control) is not TwParsed parsed) return TwContrastCause.None;
        var style = parsed.Resolve(provider.BaseState);
        if (style.ForeColor is null || style.BackColor != null) return TwContrastCause.None;
        if (!TwOriginal.IsExplicit(control, nameof(Control.BackColor))) return TwContrastCause.None;
        if (control.Parent is Control parent && EffectiveBack(parent).ToArgb() == EffectiveBack(control).ToArgb()) return TwContrastCause.None;
        return TwContrastCause.KeptBackground;
    }

    // Kendi metnini çizen kontroller; paneller ve konteynerler yalnızca zemindir.
    private static bool CarriesText(Control control) =>
        control is Label || control is ButtonBase || control is TextBoxBase || control is ComboBox || control is ListBox
        || control is TreeView || control is ListView || control is GroupBox || control is UpDownBase || control is TwTextBox;

    private static Color Final(Control control, TwColorChannel channel, Color current) =>
        TwAnimator.TryGetTarget(control, channel, out var target) ? target : current;

    // Saydam (ya da yarı saydam) zemin ebeveynin zeminine karıştırılır.
    private static Color EffectiveBack(Control control)
    {
        var back = Final(control, TwColorChannel.Back, control.BackColor);
        if (back.A == 255) return back;
        var under = control.Parent is Control parent ? EffectiveBack(parent) : SystemColors.Control;
        return Composite(back, under);
    }

    private static Color Composite(Color color, Color under)
    {
        if (color.A == 255) return color;
        double a = color.A / 255.0;
        return Color.FromArgb(
            (int)Math.Round(color.R * a + under.R * (1 - a)),
            (int)Math.Round(color.G * a + under.G * (1 - a)),
            (int)Math.Round(color.B * a + under.B * (1 - a)));
    }

    private static double Luminance(Color color)
    {
        static double Channel(int value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static bool IsLarge(Font font) => font.SizeInPoints >= 18f || (font.Bold && font.SizeInPoints >= 14f);

    /// <summary>
    /// Controls whose colors the application paints itself: <see cref="Audit"/> cannot know their on-screen colors and
    /// class cell/item colors may be ignored by them (e.g. a grid that stays light in dark mode). Found:
    /// DataGridViews with <c>CellPainting</c>/<c>RowPrePaint</c>/<c>RowPostPaint</c> handlers or overriding
    /// <c>OnPaint</c>/<c>OnCellPainting</c>; ListView and TabControl with <c>OwnerDraw</c>; ListBox, ComboBox and TreeView
    /// whose <c>DrawMode</c> is not <c>Normal</c>. FormWind's own controls are not reported.
    /// </summary>
    public static IReadOnlyList<TwOwnerDrawn> FindOwnerDrawn(Control root)
    {
        if (root is null) throw new ArgumentNullException(nameof(root));
        var found = new List<TwOwnerDrawn>();
        CollectOwnerDrawn(root, found);
        return found;
    }

    private static void CollectOwnerDrawn(Control control, List<TwOwnerDrawn> found)
    {
        if (!IsHidden(control) && OwnerDrawReason(control) is string reason) found.Add(new TwOwnerDrawn(control, reason));
        foreach (Control child in control.Controls)
            CollectOwnerDrawn(child, found);
    }

    private static string? OwnerDrawReason(Control control)
    {
        if (control.GetType().Namespace == typeof(TwContrast).Namespace) return null;
        switch (control)
        {
            case DataGridView grid:
                var reasons = new List<string>();
                foreach (var name in new[] { "CellPainting", "RowPrePaint", "RowPostPaint" })
                    if (HasHandlers(grid, name)) reasons.Add(name + " event");
                foreach (var name in new[] { "OnPaint", "OnCellPainting", "OnRowPrePaint", "OnRowPostPaint" })
                    if (Overrides(grid, name)) reasons.Add(name + " override");
                return reasons.Count > 0 ? string.Join(", ", reasons) : null;
            case ListView { OwnerDraw: true }:
            case TabControl { DrawMode: TabDrawMode.OwnerDrawFixed } tabs when !TabControlApplier.IsPainted(tabs):
                return "OwnerDraw";
            case ListBox { DrawMode: not DrawMode.Normal } list:
                return "DrawMode." + list.DrawMode;
            case ComboBox { DrawMode: not DrawMode.Normal } combo:
                return "DrawMode." + combo.DrawMode;
            case TreeView { DrawMode: not TreeViewDrawMode.Normal } tree:
                return "DrawMode." + tree.DrawMode;
            default:
                return null;
        }
    }

    private static bool Overrides(Control control, string method)
    {
        for (var type = control.GetType(); type != null && type != typeof(DataGridView); type = type.BaseType)
        {
            var declared = type.GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
            if (declared != null) return true;
        }
        return false;
    }

    // Olay anahtarı: .NET Framework'te EVENT_DATAGRIDVIEWCELLPAINTING, .NET'te s_cellPaintingEvent.
    private static bool HasHandlers(DataGridView grid, string eventName)
    {
        const System.Reflection.BindingFlags Static = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var key = (typeof(DataGridView).GetField("EVENT_DATAGRIDVIEW" + eventName.ToUpperInvariant(), Static)
                   ?? typeof(DataGridView).GetField("s_" + char.ToLowerInvariant(eventName[0]) + eventName.Substring(1) + "Event", Static))?.GetValue(null);
        if (key is null) return false;
        var events = typeof(System.ComponentModel.Component).GetProperty("Events", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(grid) as System.ComponentModel.EventHandlerList;
        return events?[key] != null;
    }
}

/// <summary>Why a contrast issue occurs, when <see cref="TwContrast.Audit"/> can tell.</summary>
public enum TwContrastCause
{
    /// <summary>No specific cause: the colors themselves do not contrast.</summary>
    None,

    /// <summary>
    /// The classes set only the text color, and the background is a color the application set explicitly that differs from
    /// the parent's (typically copied by the application's theme engine before FormWind restyled the parent). Add a
    /// <c>bg-*</c> class or let the control inherit its parent's background (<see cref="TailwindProvider.InheritCopiedBackColors"/>,
    /// <see cref="TailwindProvider.FollowParentBackColor"/>).
    /// </summary>
    KeptBackground,
}

/// <summary>A foreground/background pair below the threshold.</summary>
public sealed class TwContrastIssue
{
    internal TwContrastIssue(Control control, string part, Color foreground, Color background, double ratio, double required,
        TwContrastCause cause = TwContrastCause.None)
    {
        Control = control;
        Part = part;
        Foreground = foreground;
        Background = background;
        Ratio = ratio;
        Required = required;
        Cause = cause;
    }

    public Control Control { get; }

    /// <summary>Checked part: <c>text</c>, <c>link</c>, <c>disabled text</c>, <c>hover</c>, <c>strip</c>, <c>item:hover</c>, <c>menu</c>, <c>cell</c>, <c>selected</c>, <c>header</c>.</summary>
    public string Part { get; }

    public Color Foreground { get; }

    public Color Background { get; }

    public double Ratio { get; }

    public double Required { get; }

    public TwContrastCause Cause { get; }

    public override string ToString() =>
        $"{Control.GetType().Name} '{Control.Name}' {Part}: {Ratio:0.00}:1 < {Required:0.0}:1 " +
        $"(#{Foreground.R:X2}{Foreground.G:X2}{Foreground.B:X2} on #{Background.R:X2}{Background.G:X2}{Background.B:X2})" +
        (Cause == TwContrastCause.KeptBackground
            ? "; the classes set only the text color and the background is the application's own: add bg-* or inherit the parent's background"
            : "");
}

/// <summary>A control whose colors the application paints itself (see <see cref="TwContrast.FindOwnerDrawn"/>).</summary>
public sealed class TwOwnerDrawn
{
    internal TwOwnerDrawn(Control control, string reason)
    {
        Control = control;
        Reason = reason;
    }

    public Control Control { get; }

    /// <summary>Reason: <c>CellPainting event</c>, <c>OnPaint override</c>, <c>OwnerDraw</c>, <c>DrawMode.OwnerDrawFixed</c>…</summary>
    public string Reason { get; }

    public override string ToString() => $"{Control.GetType().Name} '{Control.Name}': {Reason}";
}
