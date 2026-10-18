using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Text color of a disabled button. If the classes have <c>disabled:text-*</c>, that color (the control's <c>ForeColor</c>) is used;
/// otherwise the text color is mixed halfway toward the background, so a disabled button is distinguishable from an enabled one.
/// <para>
/// A native flat (<see cref="FlatStyle.Flat"/>) button ignores <c>ForeColor</c> while disabled and draws the text with a
/// darkened shade of the background (<see cref="ControlPaint.Dark(Color)"/>), which is invisible on a dark background. On buttons
/// flattened by FormWind, the text is redrawn with this color while disabled. Buttons with images are left alone.
/// </para>
/// <para>
/// Native check boxes and radio buttons do the same with every style except <see cref="FlatStyle.System"/>; when the classes
/// set their text color, the text beside the glyph is redrawn while disabled (see <see cref="CanRepaintCheck"/>).
/// </para>
/// <para>
/// A disabled native label draws embossed gray text (<see cref="ControlPaint.DrawStringDisabled(Graphics, string, Font, Color, RectangleF, StringFormat)"/>),
/// whose dark shade disappears on a dark background; labels whose text color comes from the classes are redrawn too
/// (not LinkLabels or labels with an image).
/// </para>
/// </summary>
internal sealed class TwDisabledText
{
    private static readonly ConditionalWeakTable<Control, TwDisabledText> Table = new ConditionalWeakTable<Control, TwDisabledText>();

    private readonly Control? _native;
    private bool _explicit;
    private bool _repaint;

    private TwDisabledText(Control control)
    {
        if (control is not ITwChrome && (control is Button || control is CheckBox || control is RadioButton || control is Label and not LinkLabel))
        {
            _native = control;
            control.Paint += OnPaint;
        }
    }

    /// <summary>Whether the disabled text of a native check box, radio button or label is redrawn (<see cref="Update"/>'s repaint).</summary>
    internal static bool IsRepainted(Control control) =>
        Table.TryGetValue(control, out var state) && state._repaint
        && (control is ButtonBase button && CanRepaintCheck(button) || control is Label label && CanRepaintLabel(label));

    internal static bool CanRepaintLabel(Label label) => label is not LinkLabel && label is not ITwChrome && label.Image is null
        && (label.ImageList is null || label.ImageIndex < 0 && string.IsNullOrEmpty(label.ImageKey));

    // Yalnızca yerleşimi güvenle bilinen durum: işaret solda ya da sağda, düz görünüm, resim yok.
    internal static bool CanRepaintCheck(ButtonBase button)
    {
        if (button is not (CheckBox or RadioButton) || button is ITwChrome || button.FlatStyle == FlatStyle.System || HasImage(button)) return false;
        var appearance = button is CheckBox check ? check.Appearance : ((RadioButton)button).Appearance;
        if (appearance != Appearance.Normal) return false;
        var align = button is CheckBox c ? c.CheckAlign : ((RadioButton)button).CheckAlign;
        return align is ContentAlignment.MiddleLeft or ContentAlignment.TopLeft or ContentAlignment.BottomLeft
            or ContentAlignment.MiddleRight or ContentAlignment.TopRight or ContentAlignment.BottomRight;
    }

    /// <param name="control">The button.</param>
    /// <param name="explicitColor">The classes have <c>disabled:text-*</c>; the control's text color is used as is.</param>
    /// <param name="repaint">The native button was flattened by FormWind; the disabled text is redrawn.</param>
    public static void Update(Control control, bool explicitColor, bool repaint)
    {
        if (!Table.TryGetValue(control, out var state))
        {
            if (!explicitColor && !repaint) return;
            state = Table.GetValue(control, c => new TwDisabledText(c));
        }
        if (state._explicit == explicitColor && state._repaint == repaint) return;
        state._explicit = explicitColor;
        state._repaint = repaint;
        if (!control.Enabled) control.Invalidate();
    }

    public static Color TextColor(Control control) => TextColor(control, control.ForeColor, control.BackColor);

    /// <summary>The text color for the given colors (e.g. the targets of a running transition).</summary>
    public static Color TextColor(Control control, Color fore, Color back)
    {
        if (control.Enabled || Table.TryGetValue(control, out var state) && state._explicit) return fore;
        if (back.A == 0) back = control.Parent?.BackColor is Color parent && parent.A == 255 ? parent : Color.Gray;
        return Color.FromArgb(
            (fore.R + back.R) / 2,
            (fore.G + back.G) / 2,
            (fore.B + back.B) / 2);
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        var native = _native!;
        if (native.Enabled || !_repaint) return;
        if (native is Label label)
        {
            if (CanRepaintLabel(label)) PaintLabelText(label, e.Graphics);
            return;
        }
        var button = (ButtonBase)native;
        if (button is not Button)
        {
            if (CanRepaintCheck(button)) PaintCheckText(button, e.Graphics);
            return;
        }
        if (button.FlatStyle != FlatStyle.Flat || HasImage(button)) return;
        try
        {
            // Native yazının üstü zeminle örtülür; kenarlık (TwDecorator) bu olaydan sonra çizilir.
            using (var brush = new SolidBrush(button.BackColor))
                e.Graphics.FillRectangle(brush, button.ClientRectangle);
            var bounds = TextBounds(button);
            TextRenderer.DrawText(e.Graphics, button.Text, button.Font, bounds, TextColor(button), Flags(button, e.Graphics, bounds));
        }
        catch (System.Exception ex) when (!TwErrors.IsCritical(ex))
        {
            TwErrors.Trace("disabled button text", ex);
        }
    }

    // Etiketin tamamı zeminle örtülür ve yazı Label'ın kendi biçimlendirmesiyle (sarma, hiza, padding) yeniden çizilir.
    private static void PaintLabelText(Label label, Graphics graphics)
    {
        try
        {
            var back = label.BackColor.A == 255 ? label.BackColor : label.Parent?.BackColor ?? SystemColors.Control;
            using (var brush = new SolidBrush(back))
                graphics.FillRectangle(brush, label.ClientRectangle);
            // Ölçüldü: Label'ın yazısı NoPadding ile çizilenden fontun yatay iç boşluğunun (yüksekliğin altıda biri) yarısı
            // ve bir piksel kadar içeride başlar; satırlar TextBoxControl olmadan sarılır.
            int inset = (int)System.Math.Ceiling(label.Font.Height / 6.0) / 2;
            var r = label.ClientRectangle;
            var bounds = new Rectangle(r.X + label.Padding.Left + inset + 1, r.Y + label.Padding.Top,
                r.Width - label.Padding.Horizontal - 2 * inset, r.Height - label.Padding.Vertical);
            var flags = TwPainter.ToTextFormatFlags(label.TextAlign) & ~TextFormatFlags.SingleLine;
            flags |= TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
            if (label.AutoEllipsis) flags |= TextFormatFlags.EndEllipsis;
            flags |= label.UseMnemonic ? TextFormatFlags.HidePrefix : TextFormatFlags.NoPrefix;
            if (label.RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
            TextRenderer.DrawText(graphics, label.Text, label.Font, bounds, TextColor(label), flags);
        }
        catch (System.Exception ex) when (!TwErrors.IsCritical(ex))
        {
            TwErrors.Trace("disabled label text", ex);
        }
    }

    // İşaretin yanındaki alan zeminle örtülür ve yazı yeniden çizilir; işaretin kendisine dokunulmaz.
    private static void PaintCheckText(ButtonBase button, Graphics graphics)
    {
        try
        {
            var bounds = CheckTextBounds(button, graphics);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            using (var brush = new SolidBrush(button.BackColor.A == 0 && button.Parent is Control parent ? parent.BackColor : button.BackColor))
                graphics.FillRectangle(brush, bounds);
            TextRenderer.DrawText(graphics, button.Text, button.Font, bounds, TextColor(button), Flags(button, graphics, bounds));
        }
        catch (System.Exception ex) when (!TwErrors.IsCritical(ex))
        {
            TwErrors.Trace("disabled check text", ex);
        }
    }

    // WinForms'un yerleşimi: işaret (13 px, DPI ile ölçeklenir) ve bir boşluk, kalan alan yazının; RTL'de işaret öbür
    // yana geçer.
    internal static Rectangle CheckTextBounds(ButtonBase button, Graphics graphics)
    {
        var align = button is CheckBox c ? c.CheckAlign : ((RadioButton)button).CheckAlign;
        bool glyphLeft = align is ContentAlignment.MiddleLeft or ContentAlignment.TopLeft or ContentAlignment.BottomLeft;
        if (button.RightToLeft == RightToLeft.Yes) glyphLeft = !glyphLeft;
        int glyph = (button is CheckBox
            ? CheckBoxRenderer.GetGlyphSize(graphics, System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedDisabled)
            : RadioButtonRenderer.GetGlyphSize(graphics, System.Windows.Forms.VisualStyles.RadioButtonState.UncheckedDisabled)).Width;
        glyph = System.Math.Max(glyph, button.LogicalToDeviceUnits(13));
        var r = button.ClientRectangle;
        r = new Rectangle(r.X + button.Padding.Left, r.Y + button.Padding.Top, r.Width - button.Padding.Horizontal, r.Height - button.Padding.Vertical);
        // Ölçüldü: WinForms onay kutusunun yazısını işaretten 2, radyo düğmesininkini 1 piksel sonra başlatır.
        int reserved = glyph + (button is CheckBox ? 2 : 1);
        return glyphLeft
            ? new Rectangle(r.X + reserved, r.Y, r.Width - reserved, r.Height)
            : new Rectangle(r.X, r.Y, r.Width - reserved, r.Height);
    }

    private static bool HasImage(ButtonBase button) =>
        button.Image != null || button.ImageList != null && (button.ImageIndex >= 0 || button.ImageKey.Length > 0);

    // WinForms'un düz buton yerleşimi: kenarlık, padding ve odak çerçevesi için birer piksel.
    internal static Rectangle TextBounds(ButtonBase button)
    {
        int border = button.FlatAppearance.BorderSize;
        var r = button.ClientRectangle;
        r = new Rectangle(
            r.X + border + button.Padding.Left + 2,
            r.Y + border + button.Padding.Top + 2,
            r.Width - 2 * border - button.Padding.Horizontal - 4,
            r.Height - 2 * border - button.Padding.Vertical - 4);
        return r;
    }

    internal static TextFormatFlags Flags(ButtonBase button, IDeviceContext graphics, Rectangle bounds)
    {
        var flags = TwPainter.ToTextFormatFlags(button.TextAlign) & ~(TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        // Tek satıra sığan yazı tek satır olarak çizilir: çok satırlı modda dikey ortalama, alan yazıdan alçaksa (sığdırılmış
        // padding) yapılmaz ve yazı native butondakinden yukarıda kalırdı.
        bool singleLine = TextRenderer.MeasureText(graphics, button.Text, button.Font, Size.Empty, TextFormatFlags.SingleLine).Width <= bounds.Width;
        flags |= singleLine ? TextFormatFlags.SingleLine : TextFormatFlags.WordBreak;
        if (button.AutoEllipsis) flags |= TextFormatFlags.EndEllipsis;
        // ShowKeyboardCues korumalıdır; Windows'un varsayılanı gibi alt çizgi Alt'a basılana kadar gizlenir.
        flags |= button.UseMnemonic ? TextFormatFlags.HidePrefix : TextFormatFlags.NoPrefix;
        if (button.RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
        return flags;
    }
}
