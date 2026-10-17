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
/// </summary>
internal sealed class TwDisabledText
{
    private static readonly ConditionalWeakTable<Control, TwDisabledText> Table = new ConditionalWeakTable<Control, TwDisabledText>();

    private readonly Button? _native;
    private bool _explicit;
    private bool _repaint;

    private TwDisabledText(Control control)
    {
        if (control is Button button && control is not ITwChrome)
        {
            _native = button;
            button.Paint += OnPaint;
        }
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

    public static Color TextColor(Control control)
    {
        if (control.Enabled || Table.TryGetValue(control, out var state) && state._explicit) return control.ForeColor;
        var fore = control.ForeColor;
        var back = control.BackColor.A == 0 ? Color.Gray : control.BackColor;
        return Color.FromArgb(
            (fore.R + back.R) / 2,
            (fore.G + back.G) / 2,
            (fore.B + back.B) / 2);
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        var button = _native!;
        if (button.Enabled || !_repaint || button.FlatStyle != FlatStyle.Flat || HasImage(button)) return;
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
