using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Breakpoints of the responsive variants (same as Tailwind, 96 DPI pixels): <c>sm:</c> 640, <c>md:</c> 768, <c>lg:</c> 1024,
/// <c>xl:</c> 1280, <c>2xl:</c> 1536. Instead of the browser window width, the client width of the control's form is
/// measured; <c>md:flex-row</c> applies while the form is 768 px or wider.
/// </summary>
public static class TwBreakpoints
{
    public static int Sm { get; set; } = 640;
    public static int Md { get; set; } = 768;
    public static int Lg { get; set; } = 1024;
    public static int Xl { get; set; } = 1280;
    public static int Xxl { get; set; } = 1536;

    /// <summary>Breakpoint flags for a width (96 DPI pixels).</summary>
    public static TwState ForWidth(int width)
    {
        var state = TwState.None;
        if (width >= Sm) state |= TwState.Sm;
        if (width >= Md) state |= TwState.Md;
        if (width >= Lg) state |= TwState.Lg;
        if (width >= Xl) state |= TwState.Xl;
        if (width >= Xxl) state |= TwState.Xxl;
        return state;
    }

    /// <summary>Breakpoints for the width of the control's form (or its topmost parent).</summary>
    public static TwState For(Control control)
    {
        Control top = control.FindForm() ?? control;
        while (top.Parent != null && top is not Form) top = top.Parent;
        int width = (int)(top.ClientSize.Width * 96f / TwDpi.DeviceDpi(top));
        return ForWidth(width);
    }
}
