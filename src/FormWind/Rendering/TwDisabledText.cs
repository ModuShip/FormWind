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
/// <para>
/// A disabled GroupBox draws its caption in the same darkened shade (<c>TextRenderer.DisabledTextColor</c>); with visual
/// styles the caption is redrawn in place when the classes set the text color.
/// </para>
/// </summary>
internal sealed class TwDisabledText
{
    private static readonly ConditionalWeakTable<Control, TwDisabledText> Table = new ConditionalWeakTable<Control, TwDisabledText>();

    private readonly Control? _native;
    private bool _explicit;
    private bool _repaint;
    private bool _forced;

    private TwDisabledText(Control control)
    {
        if (control is not ITwChrome && (control is Button || control is CheckBox || control is RadioButton || control is Label and not LinkLabel || control is GroupBox))
        {
            _native = control;
            control.Paint += OnPaint;
        }
    }

    /// <summary>Whether the disabled text of a native check box, radio button, label or group box is redrawn (<see cref="Update"/>'s repaint).</summary>
    internal static bool IsRepainted(Control control) =>
        Table.TryGetValue(control, out var state) && state._repaint
        && (control is ButtonBase button && CanRepaintCheck(button) || control is Label label && CanRepaintLabel(label)
            || control is GroupBox group && CanRepaintGroup(group));

    // GroupBox.OnPaint'in temalı yolu (aksi hâlde başlık başka yerleşimle çizilir).
    internal static bool CanRepaintGroup(GroupBox group) =>
        group.FlatStyle != FlatStyle.System && Application.RenderWithVisualStyles && group.Width >= 10 && group.Height >= 10
        && !string.IsNullOrEmpty(group.Text);

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
    /// <param name="forced">High Contrast is on: disabled text is drawn in the system's disabled color (<see cref="SystemColors.GrayText"/>).</param>
    public static void Update(Control control, bool explicitColor, bool repaint, bool forced = false)
    {
        if (!Table.TryGetValue(control, out var state))
        {
            if (!explicitColor && !repaint) return;
            state = Table.GetValue(control, c => new TwDisabledText(c));
        }
        if (state._explicit == explicitColor && state._repaint == repaint && state._forced == forced) return;
        state._explicit = explicitColor;
        state._repaint = repaint;
        state._forced = forced;
        if (!control.Enabled) control.Invalidate();
    }

    public static Color TextColor(Control control) => TextColor(control, control.ForeColor, control.BackColor);

    /// <summary>The text color for the given colors (e.g. the targets of a running transition).</summary>
    public static Color TextColor(Control control, Color fore, Color back)
    {
        if (control.Enabled) return fore;
        if (Table.TryGetValue(control, out var state))
        {
            if (state._forced) return SystemColors.GrayText;
            if (state._explicit) return fore;
        }
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
        if (native is GroupBox group)
        {
            if (CanRepaintGroup(group)) PaintGroupCaption(group, e.Graphics);
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
            DrawLikeWinForms(e.Graphics, button, TextBounds(button, Flags(button)), button.TextAlign, button.AutoEllipsis, button.UseMnemonic,
                Padding.Empty, TextColor(button), button.BackColor);
        }
        catch (System.Exception ex) when (!TwErrors.IsCritical(ex))
        {
            TwErrors.Trace("disabled button text", ex);
        }
    }

    // Etiketin tamamı aynı biçimdeki etkin bir etiketin çizimiyle örtülür.
    private static void PaintLabelText(Label label, Graphics graphics)
    {
        try
        {
            DrawLikeWinForms(graphics, label, label.ClientRectangle, label.TextAlign, label.AutoEllipsis, label.UseMnemonic, label.Padding,
                TextColor(label), Opaque(label));
        }
        catch (System.Exception ex) when (!TwErrors.IsCritical(ex))
        {
            TwErrors.Trace("disabled label text", ex);
        }
    }

    // GroupBoxRenderer'ın yerleşimi: başlık çerçevenin 7 px içinden başlar (RTL'de sağdan), ölçülen alana ortak TextRenderer
    // ile aynı biçimle çizilir; çerçeve çizgisi bu alanın altından geçmez.
    private static void PaintGroupCaption(GroupBox group, Graphics graphics)
    {
        try
        {
            var flags = GroupCaptionFlags(group);
            var bounds = GroupCaptionBounds(group, graphics, flags);
            using (var brush = new SolidBrush(Opaque(group)))
                graphics.FillRectangle(brush, bounds);
            TextRenderer.DrawText(graphics, group.Text, group.Font, bounds, TextColor(group), flags);
        }
        catch (System.Exception ex) when (!TwErrors.IsCritical(ex))
        {
            TwErrors.Trace("disabled group caption", ex);
        }
    }

    internal static TextFormatFlags GroupCaptionFlags(GroupBox group)
    {
        // ShowKeyboardCues korumalıdır; Windows'un varsayılanı gibi önek alt çizgisi gizlenir.
        var flags = TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak | TextFormatFlags.PreserveGraphicsTranslateTransform
            | TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.HidePrefix;
        if (group.RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.Right | TextFormatFlags.RightToLeft;
        return flags;
    }

    internal static Rectangle GroupCaptionBounds(GroupBox group, IDeviceContext graphics, TextFormatFlags flags)
    {
        const int header = 7;
        var measured = TextRenderer.MeasureText(graphics, group.Text, group.Font, new Size(group.Width - 2 * header, group.Height), flags);
        int x = (flags & TextFormatFlags.Right) != 0 ? group.Width - measured.Width - header + 1 : header - 1;
        return new Rectangle(x, 0, measured.Width, measured.Height);
    }

    // İşaretin yanındaki alan zeminle örtülür ve yazı yeniden çizilir; işaretin kendisine dokunulmaz.
    private static void PaintCheckText(ButtonBase button, Graphics graphics)
    {
        try
        {
            var field = CheckField(button);
            if (field.Width <= 0 || field.Height <= 0) return;
            var back = Opaque(button);
            using (var brush = new SolidBrush(back))
                graphics.FillRectangle(brush, field);
            DrawLikeWinForms(graphics, button, LayoutText(button, Flags(button), field, 0), button.TextAlign, button.AutoEllipsis, button.UseMnemonic,
                Padding.Empty, TextColor(button), back);
        }
        catch (System.Exception ex) when (!TwErrors.IsCritical(ex))
        {
            TwErrors.Trace("disabled check text", ex);
        }
    }

    /// <summary>Where WinForms draws the text of a check box or radio button.</summary>
    internal static Rectangle CheckTextBounds(ButtonBase button) => LayoutText(button, Flags(button), CheckField(button), 0);

    // WinForms'un yerleşimi (ButtonBaseAdapter.LayoutOptions): işaret (onay kutusunda +1 boşlukla) ve bir piksel; kalan
    // alan yazının. RTL'de işaret öbür yana geçer.
    private static Rectangle CheckField(ButtonBase button)
    {
        var align = button is CheckBox c ? c.CheckAlign : ((RadioButton)button).CheckAlign;
        bool glyphLeft = align is ContentAlignment.MiddleLeft or ContentAlignment.TopLeft or ContentAlignment.BottomLeft;
        if (button.RightToLeft == RightToLeft.Yes) glyphLeft = !glyphLeft;
        int full = GlyphWidth(button) + (button is CheckBox ? 1 : 0) + 1;
        var r = Deflate(button.ClientRectangle, button.Padding);
        return glyphLeft
            ? new Rectangle(r.X + full, r.Y, r.Width - full, r.Height)
            : new Rectangle(r.X, r.Y, r.Width - full, r.Height);
    }

    // WinForms işaret boyutunu kontrolün penceresinin DPI'ındaki temadan alır (CheckBoxRenderer.GetGlyphSize'ın HWND'li
    // iç sürümü). Ortak GetGlyphSize sistem DPI'ını kullanır; pencere DPI'ı farklı bir ekrandayken yanlış olur.
    private static int GlyphWidth(ButtonBase button)
    {
        bool check = button is CheckBox;
        if (!Application.RenderWithVisualStyles) return button.LogicalToDeviceUnits(13);
        if (button.IsHandleCreated && ThemeGlyphWidth(button.Handle, check) is int themed) return themed;
        using var g = button.CreateGraphics();
        return (check
            ? CheckBoxRenderer.GetGlyphSize(g, System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedDisabled)
            : RadioButtonRenderer.GetGlyphSize(g, System.Windows.Forms.VisualStyles.RadioButtonState.UncheckedDisabled)).Width;
    }

    private static int? ThemeGlyphWidth(System.IntPtr hwnd, bool check)
    {
        try
        {
            uint dpi = GetDpiForWindow(hwnd);
            if (dpi == 0) return null;
            var theme = OpenThemeDataForDpi(hwnd, "Button", dpi);
            if (theme == System.IntPtr.Zero) return null;
            try
            {
                // BP_CHECKBOX = 3, BP_RADIOBUTTON = 2; durum 1 (normal); TS_DRAW = 2.
                return GetThemePartSize(theme, System.IntPtr.Zero, check ? 3 : 2, 1, System.IntPtr.Zero, 2, out var size) == 0 ? size.Width : null;
            }
            finally
            {
                CloseThemeData(theme);
            }
        }
        catch (System.Exception ex) when (ex is System.EntryPointNotFoundException or System.DllNotFoundException)
        {
            return null;    // Windows 10 1703 öncesi
        }
    }

    private static bool HasImage(ButtonBase button) =>
        button.Image != null || button.ImageList != null && (button.ImageIndex >= 0 || button.ImageKey.Length > 0);

    /// <summary>
    /// Where WinForms draws the text of a flat button (ButtonFlatAdapter): inside the border (one pixel more on the default
    /// button) and 2 px of padding.
    /// </summary>
    internal static Rectangle TextBounds(ButtonBase button, TextFormatFlags flags)
    {
        // IsDefault korumalıdır: kabul butonu, odakta başka bir buton yokken varsayılandır.
        bool isDefault = button.FindForm() is Form form && form.AcceptButton == button
            && !(form.ActiveControl is IButtonControl focused && focused != button);
        int border = button.FlatAppearance.BorderSize + (isDefault ? 1 : 0);
        var field = Rectangle.Inflate(Deflate(button.ClientRectangle, button.Padding), -border - 2, -border - 2);
        return LayoutText(button, flags, field, isDefault ? 1 : 0);
    }

    // ButtonBaseAdapter.LayoutOptions.LayoutTextAndImage (resimsiz): yazı alanın 2 px içinde ölçülür, TextAlign'a göre
    // yerleştirilir ve alanla kesilir.
    private static Rectangle LayoutText(ButtonBase button, TextFormatFlags flags, Rectangle field, int grow)
    {
        var max = Rectangle.Inflate(field, grow - 2, grow - 2);
        // WinForms ölçümü ekran DC'siyle yapar (LayoutOptions.GetTextSize); GDI+ ile çizen kontrolde (UseCompatibleTextRendering,
        // WPF uygulamalarında ve SetCompatibleTextRenderingDefault(true) kullananlarda varsayılan) GDI+ ile ölçer. GDI ile
        // ölçülen alan GDI+'ın yazının iki yanına eklediği boşluğa yetmiyor, yazı (devre dışı onay kutusunda) iki satıra
        // bölünüp kırpılıyordu (WPF'te %125).
        var size = button.UseCompatibleTextRendering
            ? MeasureGdiPlus(button, max.Size)
            : TextRenderer.MeasureText(button.Text, button.Font, max.Size, flags);
        int align = (int)RtlTranslate(button, button.TextAlign);
        var r = new Rectangle(max.X, max.Y, size.Width, size.Height);
        if ((align & 0x444) != 0) r.X += max.Width - size.Width;
        else if ((align & 0x222) != 0) r.X += (max.Width - size.Width) / 2;
        if ((align & 0x700) != 0) r.Y += max.Height - size.Height;
        else if ((align & 0x070) != 0) r.Y += (max.Height - size.Height) / 2;
        return Rectangle.Intersect(r, field);
    }

    private static Size MeasureGdiPlus(Control control, Size max)
    {
        using var graphics = control.IsHandleCreated ? control.CreateGraphics() : Graphics.FromHwnd(System.IntPtr.Zero);
        using var format = new StringFormat(StringFormat.GenericDefault);
        return Size.Ceiling(graphics.MeasureString(control.Text, control.Font, new SizeF(max.Width, max.Height), format));
    }

    [System.ThreadStatic] private static Label? _probe;

    /// <summary>
    /// Draws text exactly as WinForms draws it in its own controls: a hidden, enabled label with the same font, format and
    /// text rendering (GDI, or GDI+ when <c>UseCompatibleTextRendering</c> is on) draws it into a bitmap, which is copied
    /// into place. The text is therefore drawn by the same code as the control's own enabled text; check boxes and buttons
    /// with GDI+ rendering may differ by a pixel (their GDI+ path shifts the text).
    /// </summary>
    internal static void DrawLikeWinForms(Graphics graphics, Control source, Rectangle bounds, ContentAlignment align, bool ellipsis, bool mnemonic,
        Padding padding, Color fore, Color back)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || string.IsNullOrEmpty(source.Text)) return;
        var probe = _probe ??= new Label { AutoSize = false };
        // GDI+ (eski uygulamaların varsayılanı) ya da GDI: kontrolün kendisi gibi.
        probe.UseCompatibleTextRendering = source is ButtonBase button ? button.UseCompatibleTextRendering : ((Label)source).UseCompatibleTextRendering;
        probe.Font = source.Font;
        probe.Text = source.Text;
        probe.TextAlign = align;
        probe.AutoEllipsis = ellipsis;
        probe.UseMnemonic = mnemonic;
        probe.RightToLeft = source.RightToLeft == RightToLeft.Yes ? RightToLeft.Yes : RightToLeft.No;
        probe.Padding = padding;
        probe.ForeColor = fore;
        probe.BackColor = back;
        probe.Size = bounds.Size;
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        probe.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bounds.Size));
        // Gizli etiket uygulamanın yazısını tutmasın (erişilebilirlik araçları gizli pencereleri de dolaşabilir).
        probe.Text = string.Empty;
        // Piksel piksel: bitmap'in çözünürlüğü ekranınkinden farklı olabilir.
        graphics.DrawImage(bitmap, bounds, new Rectangle(Point.Empty, bounds.Size), GraphicsUnit.Pixel);
    }

    /// <summary>The text the hidden drawing label holds (tests: it must not keep the application's text).</summary>
    internal static string? ProbeText => _probe?.Text;

    // Kontrolün görünen zemini: saydamsa ebeveyninki.
    private static Color Opaque(Control control)
    {
        for (var c = control; c != null; c = c.Parent)
            if (c.BackColor.A == 255) return c.BackColor;
        return SystemColors.Control;
    }

    internal static TextFormatFlags Flags(ButtonBase button) =>
        NativeFlags(button, button.TextAlign, button.AutoEllipsis, button.UseMnemonic);

    // ControlPaint.CreateTextFormatFlags: hiza (RTL'de aynalanmış), sarma, kısaltma, önek. ShowKeyboardCues korumalıdır;
    // Windows'un varsayılanı gibi alt çizgi Alt'a basılana kadar gizlenir.
    private static TextFormatFlags NativeFlags(Control control, ContentAlignment alignment, bool ellipsis, bool mnemonic)
    {
        int align = (int)RtlTranslate(control, alignment);
        var flags = TextFormatFlags.Top | TextFormatFlags.Left;
        if ((align & 0x700) != 0) flags |= TextFormatFlags.Bottom;
        else if ((align & 0x070) != 0) flags |= TextFormatFlags.VerticalCenter;
        if ((align & 0x444) != 0) flags |= TextFormatFlags.Right;
        else if ((align & 0x222) != 0) flags |= TextFormatFlags.HorizontalCenter;
        flags |= TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
        if (ellipsis) flags |= TextFormatFlags.EndEllipsis;
        if (control.RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
        flags |= mnemonic ? TextFormatFlags.HidePrefix : TextFormatFlags.NoPrefix;
        return flags;
    }

    // Control.RtlTranslateContent: RTL'de sol ve sağ hizalar yer değiştirir.
    private static ContentAlignment RtlTranslate(Control control, ContentAlignment alignment)
    {
        if (control.RightToLeft != RightToLeft.Yes) return alignment;
        int a = (int)alignment;
        return (ContentAlignment)((a & 0x222) | ((a & 0x111) << 2) | ((a & 0x444) >> 2));
    }

    private static Rectangle Deflate(Rectangle r, Padding padding) =>
        new Rectangle(r.X + padding.Left, r.Y + padding.Top, r.Width - padding.Horizontal, r.Height - padding.Vertical);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width, Height;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(System.IntPtr hwnd);

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern System.IntPtr OpenThemeDataForDpi(System.IntPtr hwnd, string classList, uint dpi);

    [System.Runtime.InteropServices.DllImport("uxtheme.dll")]
    private static extern int GetThemePartSize(System.IntPtr theme, System.IntPtr hdc, int part, int state, System.IntPtr rect, int type, out NativeSize size);

    [System.Runtime.InteropServices.DllImport("uxtheme.dll")]
    private static extern int CloseThemeData(System.IntPtr theme);
}
