using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// The colors and item size a FormWind drop-down menu is drawn with (<see cref="TwSelect.MenuStyle"/>,
/// <see cref="Of(ToolStrip)"/>): for tests and visual checks that verify what the <c>menu:</c> and <c>item:</c> classes resolved to.
/// </summary>
public sealed class TwMenuStyle
{
    private TwMenuStyle(TwMenuColors colors)
    {
        Back = colors.MenuBack;
        Fore = colors.Fore;
        Border = colors.MenuBorder;
        ItemHoverBack = colors.ItemHoverBack;
        ItemHoverFore = colors.ItemHoverFore;
        Separator = colors.Separator;
        Accent = colors.Accent;
        ItemHeight = colors.ItemHeight;
    }

    /// <summary>Background of the menu.</summary>
    public Color Back { get; }

    /// <summary>Text color of the items.</summary>
    public Color Fore { get; }

    /// <summary>Border of the menu.</summary>
    public Color Border { get; }

    /// <summary>Background of the item under the mouse or keyboard selection.</summary>
    public Color ItemHoverBack { get; }

    /// <summary>Text color of the item under the mouse or keyboard selection.</summary>
    public Color ItemHoverFore { get; }

    /// <summary>Color of separators.</summary>
    public Color Separator { get; }

    /// <summary>Color of check marks.</summary>
    public Color Accent { get; }

    /// <summary>Height of the items in device pixels (<c>item:h-*</c>); <c>null</c> = the menu's own.</summary>
    public int? ItemHeight { get; }

    /// <summary>The style <paramref name="menu"/> is drawn with; <c>null</c> when FormWind does not draw it.</summary>
    public static TwMenuStyle? Of(ToolStrip? menu) =>
        menu?.Renderer is TwToolStripRenderer renderer ? new TwMenuStyle(renderer.Colors) : null;

    internal static TwMenuStyle From(TwMenuColors colors) => new TwMenuStyle(colors);

    public override string ToString() => $"back {ColorTranslator.ToHtml(Back)}, fore {ColorTranslator.ToHtml(Fore)}, item {ItemHeight?.ToString() ?? "auto"}";
}
