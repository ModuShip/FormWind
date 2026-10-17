using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Adds rounded corners (Region), borders and (on Panel only) gradients to standard controls.
/// Region does not anti-alias, so corners look slightly jagged; use the <c>Tw*</c> controls for a smooth result.
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
        if (_chrome.Radius <= 0 || _control.Width <= 0 || _control.Height <= 0)
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

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        try
        {
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
}
