using System;
using System.Drawing;
using Krypton.Toolkit;

namespace FormWind.Krypton;

/// <summary>
/// Builds a Krypton palette from a FormWind theme, so that Krypton controls without classes (and Krypton's own parts such as
/// headers, header groups, grids and the Krypton form chrome) take the theme's look: the colors of its <c>page</c>,
/// <c>card</c>, <c>btn-ghost</c>, <c>btn-primary</c>, <c>input</c>, <c>list</c> and <c>table</c> components, its corners and
/// its font. With a <see cref="TwPreset"/> the whole Krypton application follows the preset and the provider's dark mode.
/// Classes on a control still override the palette for that control (<see cref="KryptonApplier"/>).
/// <para>
/// The palette starts from Krypton's Microsoft 365 White (light) or Black dark mode palette; parts FormWind does not map
/// (ribbon, navigator tabs, menus drawn by Krypton's renderer) keep that palette's colors.
/// </para>
/// </summary>
public static class TwKryptonTheme
{
    /// <summary>A Krypton palette with the theme's colors for light or dark mode.</summary>
    public static KryptonCustomPaletteBase CreatePalette(TwTheme theme, bool dark)
    {
        if (theme is null) throw new ArgumentNullException(nameof(theme));
        var mode = dark ? TwState.Dark : TwState.None;
        TwStyle Style(string classes, TwState state = TwState.None) => theme.Parse(classes).Resolve(mode | state);
        TwStyle Part(string classes, string part, TwState state = TwState.None)
        {
            var style = new TwStyle { ForeColor = Style(classes).ForeColor };
            style.MergeFrom(theme.Parse(classes).ResolvePart(part, mode | state));
            return style;
        }

        var palette = new KryptonCustomPaletteBase
        {
            BasePalette = dark ? KryptonManager.PaletteMicrosoft365BlackDarkMode : KryptonManager.PaletteMicrosoft365White,
            BaseFont = TwFonts.Get(theme.FontFamilySans, 9f, FontStyle.Regular),
        };

        var page = Style("page");
        var card = Style("card");
        var text = new TwStyle { ForeColor = page.ForeColor };

        // Zemin, yazı ve yüzeyler
        KryptonApplier.ApplyContentColor(palette.Common.StateCommon.Content, text, theme);
        Back(palette.PanelStyles.PanelCommon.StateCommon, page.BackColor);
        Back(palette.PanelStyles.PanelClient.StateCommon, page.BackColor);
        Back(palette.PanelStyles.PanelAlternate.StateCommon, card.BackColor);
        KryptonApplier.ApplyBack(palette.FormStyles.FormCommon.StateCommon.Back, page);
        palette.FormStyles.FormCommon.StateCommon.Border.Color1 = card.BorderColor ?? Color.Empty;
        foreach (var label in new[] { palette.LabelStyles.LabelCommon, palette.LabelStyles.LabelNormalControl, palette.LabelStyles.LabelNormalPanel })
            KryptonApplier.ApplyContentColor(label.StateCommon, text, theme);

        // Başlıklar: form başlığı sayfa zemininde (Windows 11 gibi), başlık grupları kart zemininde.
        Header(palette.HeaderStyles.HeaderForm.StateCommon, page, theme);
        Header(palette.HeaderStyles.HeaderPrimary.StateCommon, Merge(card, Style("title")), theme);
        Header(palette.HeaderStyles.HeaderSecondary.StateCommon, Merge(page, Style("muted")), theme);
        foreach (var control in new[] { palette.ControlStyles.ControlCommon, palette.ControlStyles.ControlClient, palette.ControlStyles.ControlGroupBox })
        {
            KryptonApplier.ApplyBack(control.StateCommon.Back, card);
            control.StateCommon.Border.Color1 = card.BorderColor ?? Color.Empty;
        }

        // Butonlar (KryptonButton ve KryptonCheckButton'ın varsayılan stili): btn-ghost; işaretli btn-primary. ButtonCommon'a
        // yazılmaz: pencere butonları, liste öğeleri ve gezgin butonları ondan miras alır.
        {
            var button = palette.ButtonStyles.ButtonStandalone;
            Triple(button.StateCommon.Back, button.StateCommon.Border, button.StateCommon.Content, Style("btn-ghost"), theme);
            Triple(button.StateTracking.Back, button.StateTracking.Border, button.StateTracking.Content, Style("btn-ghost", TwState.Hover), theme);
            Triple(button.StatePressed.Back, button.StatePressed.Border, button.StatePressed.Content, Style("btn-ghost", TwState.Hover | TwState.Active), theme);
            Triple(button.StateDisabled.Back, button.StateDisabled.Border, button.StateDisabled.Content, Style("btn-ghost", TwState.Disabled), theme);
            Triple(button.StateCheckedNormal.Back, button.StateCheckedNormal.Border, button.StateCheckedNormal.Content, Style("btn-primary"), theme);
            Triple(button.StateCheckedTracking.Back, button.StateCheckedTracking.Border, button.StateCheckedTracking.Content, Style("btn-primary", TwState.Hover), theme);
            Triple(button.StateCheckedPressed.Back, button.StateCheckedPressed.Border, button.StateCheckedPressed.Content, Style("btn-primary", TwState.Hover | TwState.Active), theme);
        }

        // Giriş kontrolleri: input; odakta focus: renkleri.
        foreach (var input in new[] { palette.InputControlStyles.InputControlCommon, palette.InputControlStyles.InputControlStandalone })
        {
            Triple(input.StateCommon.Back, input.StateCommon.Border, input.StateCommon.Content, Style("input"), theme);
            Triple(input.StateActive.Back, input.StateActive.Border, input.StateActive.Content, Style("input", TwState.Focus), theme);
            Triple(input.StateDisabled.Back, input.StateDisabled.Border, input.StateDisabled.Content, Style("input", TwState.Disabled), theme);
        }

        // Liste öğeleri (KryptonListBox, KryptonCheckedListBox): list'in item: ve selected: parçaları.
        var item = palette.ButtonStyles.ButtonListItem;
        Item(item.StateCommon.Border, item.StateCommon.Back, item.StateCommon.Content, Part("list", "item"), theme);
        Item(item.StateTracking.Border, item.StateTracking.Back, item.StateTracking.Content, Part("list", "item", TwState.Hover), theme);
        Item(item.StateCheckedNormal.Border, item.StateCheckedNormal.Back, item.StateCheckedNormal.Content, Part("list", "selected"), theme);
        Item(item.StateCheckedTracking.Border, item.StateCheckedTracking.Back, item.StateCheckedTracking.Content, Part("list", "selected"), theme);

        // Tablolar: table'ın zemini, hücreleri, başlıkları ve seçimi.
        var table = Style("table");
        var cells = Part("table", "cell");
        cells.BackColor ??= table.BackColor;
        var header = Part("table", "header");
        var selected = Part("table", "selected");
        foreach (var grid in new[] { palette.GridStyles.GridCommon, palette.GridStyles.GridList, palette.GridStyles.GridSheet })
        {
            KryptonApplier.SetColor(grid.StateCommon.Background, table.BackColor);
            var common = grid.StateCommon;
            KryptonApplier.SetTriple(common.DataCell.Back, common.DataCell.Border, common.DataCell.Content, cells.BackColor, cells.ForeColor, table.DivideColor);
            KryptonApplier.SetTriple(common.HeaderColumn.Back, common.HeaderColumn.Border, common.HeaderColumn.Content, header.BackColor, header.ForeColor, table.DivideColor);
            KryptonApplier.SetTriple(common.HeaderRow.Back, common.HeaderRow.Border, common.HeaderRow.Content, header.BackColor, header.ForeColor, table.DivideColor);
            var picked = grid.StateSelected;
            KryptonApplier.SetTriple(picked.DataCell.Back, picked.DataCell.Border, picked.DataCell.Content, selected.BackColor, selected.ForeColor, null);
        }

        return palette;
    }

    /// <summary>
    /// Makes Krypton's global palette (<see cref="KryptonManager"/>) the theme of <paramref name="provider"/> and keeps it in sync:
    /// it is rebuilt when the provider's theme (or <see cref="TwTheme.Current"/>) or <see cref="TailwindProvider.DarkMode"/>
    /// changes. Disposing the result stops following and restores the previous global palette.
    /// </summary>
    public static IDisposable Follow(TailwindProvider provider, KryptonManager? manager = null)
    {
        if (provider is null) throw new ArgumentNullException(nameof(provider));
        return new Follower(provider, manager);
    }

    private sealed class Follower : IDisposable
    {
        private readonly TailwindProvider _provider;
        private readonly KryptonManager _manager;
        private readonly bool _ownsManager;
        private readonly PaletteMode _previousMode;
        private readonly KryptonCustomPaletteBase? _previousPalette;
        private KryptonCustomPaletteBase? _palette;
        private bool _disposed;

        public Follower(TailwindProvider provider, KryptonManager? manager)
        {
            _provider = provider;
            _ownsManager = manager is null;
            _manager = manager ?? new KryptonManager();
            _previousMode = _manager.GlobalPaletteMode;
            _previousPalette = _manager.GlobalCustomPalette;
            Apply();
            provider.DarkModeChanged += OnChanged;
            provider.ThemeChanged += OnChanged;
        }

        private void OnChanged(object? sender, EventArgs e) => Apply();

        private void Apply()
        {
            var previous = _palette;
            _palette = CreatePalette(_provider.Theme, _provider.DarkMode);
            _manager.GlobalCustomPalette = _palette;
            _manager.GlobalPaletteMode = PaletteMode.Custom;
            previous?.Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _provider.DarkModeChanged -= OnChanged;
            _provider.ThemeChanged -= OnChanged;
            if (_previousPalette != null) _manager.GlobalCustomPalette = _previousPalette;
            _manager.GlobalPaletteMode = _previousMode;
            _palette?.Dispose();
            if (_ownsManager) _manager.Dispose();
        }
    }

    private static TwStyle Merge(TwStyle first, TwStyle second)
    {
        var merged = new TwStyle();
        merged.MergeFrom(first);
        merged.MergeFrom(second);
        return merged;
    }

    private static void Back(PaletteBack back, Color? color) => KryptonApplier.SetColor(back, color);

    // Palet ölçeklenmez: Krypton kendi DPI ölçeklemesini yapar.
    private static void Triple(PaletteBack back, PaletteBorder border, PaletteContent content, TwStyle style, TwTheme theme)
    {
        KryptonApplier.ApplyBack(back, style);
        KryptonApplier.ApplyBorder(border, style, 1f, theme);
        KryptonApplier.ApplyContentColor(content, style, theme);
    }

    private static void Header(PaletteHeaderRedirect header, TwStyle style, TwTheme theme)
    {
        KryptonApplier.ApplyBack(header.Back, style);
        header.Border.Color1 = style.BorderColor ?? Color.Empty;
        KryptonApplier.ApplyContentColor(header.Content, style, theme);
    }

    private static void Item(PaletteBorder border, PaletteBack back, PaletteContent content, TwStyle style, TwTheme theme)
    {
        // Öğe kenarlığı çizilmez (temel paletin seçim çerçevesi listenin renkleriyle uyuşmaz).
        border.Draw = InheritBool.False;
        KryptonApplier.ApplyBack(back, style);
        KryptonApplier.ApplyContentColor(content, style, theme);
    }
}
