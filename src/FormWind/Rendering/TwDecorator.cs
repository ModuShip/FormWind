using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Adds rounded corners, borders and (on Panel and rounded Label only) gradients to standard controls.
/// A text-only <see cref="Label"/> is redrawn with anti-aliased corners on its parent's color (a pill or badge); other
/// controls are clipped with a Region, which does not anti-alias, so their corners look slightly jagged.
/// </summary>
internal sealed class TwDecorator
{
    private static readonly ConditionalWeakTable<Control, TwDecorator> Table = new ConditionalWeakTable<Control, TwDecorator>();

    private readonly Control _control;
    private TwChrome _chrome = TwChrome.None;

    private TwDecorator(Control control)
    {
        _control = control;
        _control.Paint += OnPaint;
        _control.SizeChanged += OnSizeChanged;
        _control.BackColorChanged += OnSizeChanged;
    }

    public static bool IsAttached(Control control) => Table.TryGetValue(control, out _);

    public static TwDecorator For(Control control) => Table.GetValue(control, c => new TwDecorator(c));

    public void Update(TwChrome chrome)
    {
        if (_chrome.Equals(chrome)) return;
        bool radiusChanged = _chrome.Radius != chrome.Radius;
        _chrome = chrome;
        if (radiusChanged) UpdateRegion();
        _control.Invalidate();
    }

    private void OnSizeChanged(object? sender, System.EventArgs e) => UpdateRegion();

    private void UpdateRegion()
    {
        var old = _control.Region;
        if (_chrome.Radius <= 0 || _control.Width <= 0 || _control.Height <= 0 || PaintsSmoothCorners(_control))
        {
            if (old != null)
            {
                _control.Region = null;
                old.Dispose();
            }
            return;
        }

        var radius = TwPainter.EffectiveRadius(_chrome.Radius, _control.Size);
        using (var path = TwPainter.CreateRoundedRect(new RectangleF(0, 0, _control.Width, _control.Height), radius))
            _control.Region = new Region(path);
        old?.Dispose();
    }

    /// <summary>
    /// Whether the control is a text-only label whose rounded surface is drawn here (anti-aliased, the corners in the
    /// parent's color) instead of being clipped with a Region.
    /// </summary>
    internal static bool PaintsSmoothCorners(Control control) =>
        control is Label label && Table.TryGetValue(control, out var decorator) && decorator._chrome.Radius > 0
        && TwDisabledText.IsTextOnly(label) && label.BackColor.A == 255;

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        try
        {
            if (PaintsSmoothCorners(_control))
            {
                PaintSmoothLabel((Label)_control, e.Graphics);
                return;
            }
            // Gradient yalnızca kendi içeriği olmayan Panel'de çizilir; Button/Label'da metni örterdi.
            if (_chrome.Gradient.HasValue && _control is Panel)
                TwPainter.FillSurface(e.Graphics, _control.ClientRectangle, 0, _control.BackColor, _chrome.Gradient);
            TwPainter.StrokeChrome(e.Graphics, _control.ClientRectangle, _chrome);
        }
        catch (System.Exception ex) when (!TwErrors.IsCritical(ex))
        {
            TwErrors.Trace("border painting", ex);
        }
    }

    // The native label has already drawn a square background and its text; both are covered and drawn again.
    private void PaintSmoothLabel(Label label, Graphics graphics)
    {
        var bounds = label.ClientRectangle;
        TwPainter.PaintParentBackground(label, graphics);
        TwPainter.FillSurface(graphics, bounds, _chrome.Radius, label.BackColor, _chrome.Gradient);
        TwPainter.StrokeChrome(graphics, bounds, _chrome);
        if (string.IsNullOrEmpty(label.Text)) return;
        var text = new Rectangle(bounds.X + label.Padding.Left, bounds.Y + label.Padding.Top,
            System.Math.Max(0, bounds.Width - label.Padding.Horizontal), System.Math.Max(0, bounds.Height - label.Padding.Vertical));
        var color = TwDisabledText.TextColor(label);
        if (!label.UseCompatibleTextRendering)
        {
            TextRenderer.DrawText(graphics, label.Text, label.Font, text, color, TwDisabledText.LabelFlags(label));
            return;
        }
        // GDI+ text, as the label draws it with UseCompatibleTextRendering.
        using var format = CompatibleFormat(label);
        using var brush = new SolidBrush(color);
        graphics.DrawString(label.Text, label.Font, brush, text, format);
    }

    private static StringFormat CompatibleFormat(Label label)
    {
        int align = (int)label.TextAlign;
        var format = new StringFormat
        {
            Alignment = (align & 0x444) != 0 ? StringAlignment.Far : (align & 0x222) != 0 ? StringAlignment.Center : StringAlignment.Near,
            LineAlignment = (align & 0x700) != 0 ? StringAlignment.Far : (align & 0x070) != 0 ? StringAlignment.Center : StringAlignment.Near,
            Trimming = label.AutoEllipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None,
            HotkeyPrefix = label.UseMnemonic ? System.Drawing.Text.HotkeyPrefix.Hide : System.Drawing.Text.HotkeyPrefix.None,
        };
        if (label.RightToLeft == RightToLeft.Yes) format.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
        return format;
    }
}
