using System;
using System.Drawing;
using System.Windows.Forms;
using Krypton.Toolkit;

namespace FormWind.Krypton;

/// <summary>
/// Maps variants to Krypton's state palettes: base → StateCommon, hover → StateTracking,
/// active → StatePressed, focus → OverrideFocus (StateActive for input controls), disabled → StateDisabled,
/// <c>checked:</c> → StateChecked* (<see cref="KryptonCheckButton"/>). Krypton handles the drawing of hover and pressed states
/// itself; corners are anti-aliased.
/// <para>
/// Supported: buttons (<see cref="KryptonButton"/>, <see cref="KryptonCheckButton"/>), <see cref="KryptonPanel"/>,
/// <see cref="KryptonLabel"/> (and <see cref="KryptonLinkLabel"/>), <see cref="KryptonCheckBox"/>, <see cref="KryptonRadioButton"/>,
/// input controls (<see cref="KryptonTextBox"/>, <see cref="KryptonMaskedTextBox"/>, <see cref="KryptonRichTextBox"/>,
/// <see cref="KryptonNumericUpDown"/>, <see cref="KryptonDateTimePicker"/>, <see cref="KryptonComboBox"/> with <c>item:</c> and
/// <c>menu:</c> for the drop-down), <see cref="KryptonGroupBox"/> (<c>caption:</c> for the title),
/// <see cref="KryptonHeaderGroup"/> (<c>header:</c> for the primary header), lists (<see cref="KryptonListBox"/>,
/// <see cref="KryptonCheckedListBox"/> with <c>item:</c> and <c>selected:</c>), <see cref="KryptonForm"/> (<c>caption:</c>
/// for the title bar) and <see cref="KryptonDataGridView"/>.
/// </para>
/// <para>
/// <see cref="KryptonDataGridView"/> is styled structurally (row height, grid lines, row headers, sort arrow) by FormWind's
/// DataGridView applier; because Krypton paints cells and headers with its own palette, the colors are also written to the
/// palette's <c>StateCommon</c> and <c>StateSelected</c> states: base classes and <c>cell:</c> → data cells, <c>header:</c> →
/// column headers, <c>row-header:</c> → row headers, <c>selected:</c> → selected cells, <c>bg-*</c> → empty area,
/// <c>divide-*</c> → cell borders.
/// </para>
/// </summary>
public sealed class KryptonApplier : IStyleApplier
{
    /// <summary>Registers the applier. Call it in <c>Program.Main</c> before any form is opened.</summary>
    public static void Register() => TwAppliers.Register(new KryptonApplier());

    public bool CanApply(Control control) =>
        control is KryptonButton or KryptonPanel or KryptonLabel or KryptonDataGridView
        or KryptonCheckBox or KryptonRadioButton
        or KryptonTextBox or KryptonMaskedTextBox or KryptonRichTextBox or KryptonNumericUpDown or KryptonDateTimePicker or KryptonComboBox
        or KryptonGroupBox or KryptonHeaderGroup or KryptonListBox or KryptonCheckedListBox or KryptonForm;

    public void Apply(Control control, TwStyle style, TwApplyContext context)
    {
        if (control is KryptonDataGridView grid)
        {
            TwAppliers.FindBuiltIn(grid).Apply(grid, style, context);
            ApplyGrid(grid, style, context);
            return;
        }

        var parsed = context.Parsed;
        var dark = (context.State & TwState.Dark) != 0 ? TwState.Dark : TwState.None;

        var common = parsed.ResolveExact(TwState.None, dark);
        var tracking = parsed.ResolveExact(TwState.Hover, dark | TwState.Hover);
        var pressed = Merge(tracking, parsed.ResolveExact(TwState.Active, dark | TwState.Hover | TwState.Active));
        var focus = parsed.ResolveExact(TwState.Focus, dark | TwState.Focus);
        var disabled = parsed.ResolveExact(TwState.Disabled, dark | TwState.Disabled);
        // Giriş kontrollerinin etkin durumu (odak ya da fare üzerinde) ve listelerin parçaları.
        var active = Merge(tracking, focus);
        TwStyle Part(string part, TwState state) => Inherit(common, parsed.ResolvePart(part, dark | state));

        var scale = context.Scale;
        var theme = context.Theme;

        switch (control)
        {
            case KryptonButton button:
                ApplyTriple(button.StateCommon.Back, button.StateCommon.Border, button.StateCommon.Content, common, scale, theme, control.Height);
                ApplyTriple(button.StateTracking.Back, button.StateTracking.Border, button.StateTracking.Content, tracking, scale, theme, control.Height);
                ApplyTriple(button.StatePressed.Back, button.StatePressed.Border, button.StatePressed.Content, pressed, scale, theme, control.Height);
                ApplyTriple(button.OverrideFocus.Back, button.OverrideFocus.Border, button.OverrideFocus.Content, focus, scale, theme, control.Height);
                ApplyTriple(button.StateDisabled.Back, button.StateDisabled.Border, button.StateDisabled.Content, disabled, scale, theme, control.Height);
                if (button is KryptonCheckButton check)
                {
                    var on = parsed.ResolveExact(TwState.Checked, dark | TwState.Checked);
                    var onTracking = Merge(on, parsed.ResolveExact(TwState.Checked | TwState.Hover, dark | TwState.Checked | TwState.Hover));
                    var onPressed = Merge(onTracking, parsed.ResolveExact(TwState.Checked | TwState.Active, dark | TwState.Checked | TwState.Hover | TwState.Active));
                    ApplyTriple(check.StateCheckedNormal.Back, check.StateCheckedNormal.Border, check.StateCheckedNormal.Content, on, scale, theme, control.Height);
                    ApplyTriple(check.StateCheckedTracking.Back, check.StateCheckedTracking.Border, check.StateCheckedTracking.Content, onTracking, scale, theme, control.Height);
                    ApplyTriple(check.StateCheckedPressed.Back, check.StateCheckedPressed.Border, check.StateCheckedPressed.Content, onPressed, scale, theme, control.Height);
                    FitPadding(button, check.StateCheckedNormal.Content, check.StateCheckedTracking.Content, check.StateCheckedPressed.Content);
                }
                FitPadding(button, button.StateCommon.Content, button.StateTracking.Content, button.StatePressed.Content,
                           button.OverrideFocus.Content, button.StateDisabled.Content);
                break;

            case KryptonCheckBox checkBox:
                ApplyContentColor(checkBox.StateCommon, common, theme);
                ApplyContentColor(checkBox.OverrideFocus, focus, theme);
                ApplyContentColor(checkBox.StateDisabled, disabled, theme);
                break;

            case KryptonRadioButton radio:
                ApplyContentColor(radio.StateCommon, common, theme);
                ApplyContentColor(radio.OverrideFocus, focus, theme);
                ApplyContentColor(radio.StateDisabled, disabled, theme);
                break;

            case KryptonTextBox input:
                ApplyInputStates(input.StateCommon, input.StateActive, input.StateDisabled, common, active, disabled, scale, theme, control.Height);
                break;

            case KryptonMaskedTextBox input:
                ApplyInputStates(input.StateCommon, input.StateActive, input.StateDisabled, common, active, disabled, scale, theme, control.Height);
                break;

            case KryptonRichTextBox input:
                ApplyInputStates(input.StateCommon, input.StateActive, input.StateDisabled, common, active, disabled, scale, theme, control.Height);
                break;

            case KryptonNumericUpDown input:
                ApplyInputStates(input.StateCommon, input.StateActive, input.StateDisabled, common, active, disabled, scale, theme, control.Height);
                break;

            case KryptonDateTimePicker input:
                ApplyInputStates(input.StateCommon, input.StateActive, input.StateDisabled, common, active, disabled, scale, theme, control.Height);
                break;

            case KryptonComboBox combo:
                ApplyInputStates(combo.StateCommon.ComboBox, combo.StateActive.ComboBox, combo.StateDisabled.ComboBox, common, active, disabled, scale, theme, control.Height);
                ApplyItem(combo.StateCommon.Item.Back, combo.StateCommon.Item.Border, combo.StateCommon.Item.Content, Part("item", TwState.None), theme);
                ApplyItem(combo.StateTracking.Item.Back, combo.StateTracking.Item.Border, combo.StateTracking.Item.Content, Part("item", TwState.Hover), theme);
                SetColor(combo.StateCommon.DropBack, parsed.ResolvePart("menu", dark).BackColor);
                break;

            case KryptonGroupBox group:
                ApplyBack(group.StateCommon.Back, common);
                ApplyBorder(group.StateCommon.Border, common, scale, theme, control.Height);
                ApplyContentColor(group.StateCommon.Content, Inherit(common, parsed.ResolvePart("caption", dark)), theme);
                // Krypton bu durum nesnelerini null olarak bildirir (oluşturucuda atanırlar).
                if (group.StateDisabled is { } groupDisabled)
                {
                    ApplyBack(groupDisabled.Back, disabled);
                    ApplyContentColor(groupDisabled.Content, disabled, theme);
                }
                break;

            case KryptonHeaderGroup headerGroup:
                ApplyBack(headerGroup.StateCommon.Back, common);
                ApplyBorder(headerGroup.StateCommon.Border, common, scale, theme, control.Height);
                var header = parsed.ResolvePart("header", dark);
                ApplyBack(headerGroup.StateCommon.HeaderPrimary.Back, header);
                ApplyContentColor(headerGroup.StateCommon.HeaderPrimary.Content, header, theme);
                ApplyBack(headerGroup.StateDisabled.Back, disabled);
                break;

            case KryptonListBox list:
                ApplyList(list.StateCommon, list.StateTracking.Item, list.StateCheckedNormal.Item, list.StateDisabled, common, Part("item", TwState.None),
                          Part("item", TwState.Hover), Part("selected", TwState.None), disabled, scale, theme);
                break;

            case KryptonCheckedListBox list:
                ApplyList(list.StateCommon, list.StateTracking.Item, list.StateCheckedNormal.Item, list.StateDisabled, common, Part("item", TwState.None),
                          Part("item", TwState.Hover), Part("selected", TwState.None), disabled, scale, theme);
                break;

            case KryptonForm kryptonForm:
                if (kryptonForm.StateCommon is { } formState)
                {
                    ApplyBack(formState.Back, common);
                    var caption = parsed.ResolvePart("caption", dark);
                    ApplyBack(formState.Header.Back, caption);
                    ApplyContentColor(formState.Header.Content, caption, theme);
                }
                break;

            case KryptonPanel panel:
                ApplyBack(panel.StateCommon, common);
                ApplyBack(panel.StateDisabled, disabled);
                break;

            case KryptonLabel label:
                ApplyContent(label.StateCommon, common, scale, theme);
                ApplyContent(label.StateDisabled, disabled, scale, theme);
                break;
        }

        // Krypton kontrolleri de flex/grid konteynerlerinin öğesi olabilir.
        TwLayout.Apply(control, style, scale);
    }

    private static void ApplyGrid(KryptonDataGridView grid, TwStyle style, TwApplyContext context)
    {
        var parsed = context.Parsed;
        var state = context.State & ~(TwState.Hover | TwState.Active);
        var cells = new TwStyle { BackColor = style.BackColor, ForeColor = style.ForeColor };
        cells.MergeFrom(parsed.ResolvePart("cell", state));
        var header = parsed.ResolvePart("header", state);
        var rowHeader = parsed.ResolvePart("row-header", state);
        var selected = parsed.ResolvePart("selected", state);
        var divide = style.DivideColor;

        var common = grid.StateCommon;
        SetColor(common.Background, style.BackColor);
        SetTriple(common.DataCell.Back, common.DataCell.Border, common.DataCell.Content, cells.BackColor, cells.ForeColor, divide);
        SetTriple(common.HeaderColumn.Back, common.HeaderColumn.Border, common.HeaderColumn.Content, header.BackColor, header.ForeColor, divide);
        SetTriple(common.HeaderRow.Back, common.HeaderRow.Border, common.HeaderRow.Content, rowHeader.BackColor ?? header.BackColor, rowHeader.ForeColor ?? header.ForeColor, divide);
        var picked = grid.StateSelected;
        SetTriple(picked.DataCell.Back, picked.DataCell.Border, picked.DataCell.Content, selected.BackColor, selected.ForeColor, null);
        // Seçim başlıkları vurgulamasın (FormWind'in DataGridView davranışıyla aynı).
        SetTriple(picked.HeaderColumn.Back, picked.HeaderColumn.Border, picked.HeaderColumn.Content, header.BackColor, header.ForeColor, divide);
        SetTriple(picked.HeaderRow.Back, picked.HeaderRow.Border, picked.HeaderRow.Content, rowHeader.BackColor ?? header.BackColor, rowHeader.ForeColor ?? header.ForeColor, divide);
    }

    internal static void SetTriple(PaletteBack back, PaletteBorder border, PaletteDataGridViewContentStates content, Color? backColor, Color? foreColor, Color? borderColor)
    {
        SetColor(back, backColor);
        content.Color1 = foreColor ?? Color.Empty;
        border.Color1 = borderColor ?? Color.Empty;
    }

    internal static void SetColor(PaletteBack back, Color? color)
    {
        back.Color1 = color ?? Color.Empty;
        back.ColorStyle = color.HasValue ? PaletteColorStyle.Solid : PaletteColorStyle.Inherit;
    }

    // Sabit boyutlu butonda padding yazıyı sığdıracak kadar kısılır (FormWind'in WinForms butonlarındaki gibi); yoksa Krypton
    // yazıyı "…" ile kırpar. AutoSize butonda dokunulmaz.
    private static void FitPadding(KryptonButton button, params PaletteContent[] contents)
    {
        if (button.AutoSize || string.IsNullOrEmpty(button.Values.Text)) return;
        // Kenarlık ve odak çerçevesi; yuvarlak köşe de yatayda yer kaplar.
        int frame = 8 + (int)Math.Max(0f, button.StateCommon.Border.Rounding);
        foreach (var content in contents)
        {
            var padding = content.Padding;
            var text = TextRenderer.MeasureText(button.Values.Text, content.ShortText.Font ?? button.Font);
            var (left, right) = Fit(padding.Left, padding.Right, button.Width - text.Width - frame);
            var (top, bottom) = Fit(padding.Top, padding.Bottom, button.Height - text.Height - frame);
            content.Padding = new Padding(left, top, right, bottom);
        }
    }

    // -1: paletten miras (dokunulmaz).
    private static (int First, int Second) Fit(int first, int second, int available)
    {
        if (first < 0 || second < 0 || first + second <= available) return (first, second);
        if (available <= 0) return (0, 0);
        double ratio = available / (double)(first + second);
        return ((int)(first * ratio), (int)(second * ratio));
    }

    private static void ApplyInputStates(PaletteInputControlTripleRedirect common, PaletteInputControlTripleStates active, PaletteInputControlTripleStates disabled,
                                         TwStyle commonStyle, TwStyle activeStyle, TwStyle disabledStyle, float scale, TwTheme theme, int height)
    {
        ApplyInput(common.Back, common.Border, common.Content, commonStyle, scale, theme, height);
        ApplyInput(active.Back, active.Border, active.Content, activeStyle, scale, theme, height);
        ApplyInput(disabled.Back, disabled.Border, disabled.Content, disabledStyle, scale, theme, height);
    }

    private static void ApplyList(PaletteListStateRedirect common, PaletteTriple tracking, PaletteTriple selected, PaletteListState disabled,
                                  TwStyle commonStyle, TwStyle item, TwStyle hover, TwStyle selectedStyle, TwStyle disabledStyle, float scale, TwTheme theme)
    {
        ApplyBack(common.Back, commonStyle);
        ApplyBorder(common.Border, commonStyle, scale, theme);
        ApplyItem(common.Item.Back, common.Item.Border, common.Item.Content, item, theme);
        ApplyItem(tracking.Back, tracking.Border, tracking.Content, hover, theme);
        ApplyItem(selected.Back, selected.Border, selected.Content, selectedStyle, theme);
        ApplyBack(disabled.Back, disabledStyle);
    }

    // Liste ve açılır liste öğesi: zemin ve yazı (öğe kenarlığı çizilmez, dolgu Krypton'un).
    private static void ApplyItem(PaletteBack back, PaletteBorder border, PaletteContent content, TwStyle style, TwTheme theme)
    {
        ApplyBack(back, style);
        border.Color1 = style.BorderColor ?? Color.Empty;
        ApplyContentColor(content, style, theme);
    }

    // Yazı rengi ve fontu; dolguya dokunmaz (grup başlığı, başlık çubuğu, onay kutusu metni).
    internal static void ApplyContentColor(PaletteContent content, TwStyle style, TwTheme theme)
    {
        content.ShortText.Color1 = style.ForeColor ?? Color.Empty;
        content.ShortText.Font = CreateFont(style, theme);
    }

    // Parçanın yazı rengi verilmemişse kontrolün yazı rengi kullanılır (DataGridView hücreleri gibi).
    private static TwStyle Inherit(TwStyle common, TwStyle part)
    {
        var style = new TwStyle { ForeColor = common.ForeColor };
        style.MergeFrom(part);
        return style;
    }

    private static TwStyle Merge(TwStyle first, TwStyle second)
    {
        var merged = new TwStyle();
        merged.MergeFrom(first);
        merged.MergeFrom(second);
        return merged;
    }

    internal static void ApplyTriple(PaletteBack back, PaletteBorder border, PaletteContent content, TwStyle style, float scale, TwTheme theme, int height = 0)
    {
        ApplyBack(back, style);
        ApplyBorder(border, style, scale, theme, height);
        ApplyContent(content, style, scale, theme);
    }

    // Giriş kontrolleri (KryptonTextBox) ayrı, daha dar palet tipleri kullanır.
    internal static void ApplyInput(PaletteInputControlBackStates back, PaletteBorder border, PaletteInputControlContentStates content, TwStyle style, float scale, TwTheme theme, int height = 0)
    {
        back.Color1 = style.BackColor ?? Color.Empty;
        ApplyBorder(border, style, scale, theme, height);
        content.Color1 = style.ForeColor ?? Color.Empty;
        content.Font = CreateFont(style, theme);
        content.TextH = ToRelativeAlign(style.TextAlign);
        content.Padding = style.Padding.IsEmpty ? new Padding(-1) : style.Padding.ApplyTo(new Padding(-1), scale);
    }

    internal static void ApplyBack(PaletteBack back, TwStyle style)
    {
        if (style.BackColor is Color color)
        {
            back.Color1 = color;
            back.ColorStyle = PaletteColorStyle.Solid;
        }
        else
        {
            back.Color1 = Color.Empty;
            back.ColorStyle = PaletteColorStyle.Inherit;
        }
    }

    // height: kontrolün yüksekliği; köşe yarıçapı bunun yarısını geçmez (0: palet, en çok 16 px).
    internal static void ApplyBorder(PaletteBorder border, TwStyle style, float scale, TwTheme theme, int height = 0)
    {
        if (style.BorderWidth is int width)
        {
            border.Width = width == 0 ? 0 : TwSides.Scale(width, scale);
            border.DrawBorders = width > 0 ? PaletteDrawBorders.All : PaletteDrawBorders.None;
            border.Color1 = style.BorderColor ?? theme.DefaultBorderColor;
        }
        else
        {
            border.Width = -1;
            border.DrawBorders = PaletteDrawBorders.Inherit;
            border.Color1 = style.BorderColor ?? Color.Empty;
        }

        // rounded-full (9999) Krypton'da içerik alanını yok eder (yazı çizilmez); yarıçap yüksekliğin yarısıyla sınırlanır.
        border.Rounding = style.Radius is int radius ? Math.Min(radius * scale, height > 0 ? height / 2f : 16f * scale) : -1;
    }

    internal static void ApplyContent(PaletteContent content, TwStyle style, float scale, TwTheme theme)
    {
        content.ShortText.Color1 = style.ForeColor ?? Color.Empty;
        content.ShortText.Font = CreateFont(style, theme);
        content.ShortText.TextH = ToRelativeAlign(style.TextAlign);
        content.Padding = style.Padding.IsEmpty ? new Padding(-1) : style.Padding.ApplyTo(new Padding(-1), scale);
    }

    // Krypton'da null font = paletten miras. Boyut verilmezse Krypton'un varsayılanı olan 9 pt kullanılır.
    internal static Font? CreateFont(TwStyle style, TwTheme theme)
    {
        if (!style.HasFont) return null;
        return TwFonts.Create(style, TwFonts.Get(theme.FontFamilySans, 9f, FontStyle.Regular), theme);
    }

    private static PaletteRelativeAlign ToRelativeAlign(TwTextAlign? align) => align switch
    {
        TwTextAlign.Left => PaletteRelativeAlign.Near,
        TwTextAlign.Center => PaletteRelativeAlign.Center,
        TwTextAlign.Right => PaletteRelativeAlign.Far,
        _ => PaletteRelativeAlign.Inherit,
    };
}
