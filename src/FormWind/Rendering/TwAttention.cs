using System;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Draws the eye to a control that just changed, such as a badge whose count went up or a status chip that turned red:
/// rings spread out from its edges and fade, on its parent like shadows and focus rings.
/// <code>
/// lblAlarms.Text = "3";
/// TwAttention.Pulse(lblAlarms);           // two pulses
/// chipStatus.Pulse(count: 1);
/// </code>
/// </summary>
public static class TwAttention
{
    /// <summary>
    /// Sends <paramref name="count"/> pulses (700 ms each) out from <paramref name="control"/> in its most visible color:
    /// its background when it has one, otherwise its text color. Nothing happens when animations are off or the control
    /// cannot animate; a new call restarts the pulses.
    /// </summary>
    public static void Pulse(Control control, int count = 2)
    {
        if (control is null) throw new ArgumentNullException(nameof(control));
        if (count <= 0 || !TwMotion.CanAnimate(control) || control.Parent is null) return;
        var outset = TwOutset.For(control);
        float radius = control is TwChip chip ? chip.DrawnRadius : outset.Spec?.Radius ?? 4 * TwDpi.DeviceDpi(control) / 96f;
        outset.Ping(count, PulseColor(control), radius);
    }

    /// <summary>Whether pulses run around <paramref name="control"/>.</summary>
    public static bool IsPulsing(Control control) => control != null && TwOutset.IsAttached(control) && TwOutset.For(control).Pinging;

    /// <summary>The control's background or text color, whichever stands out more from its parent.</summary>
    internal static Color PulseColor(Control control)
    {
        var surface = TwCharts.Surface(control.Parent!);
        var back = control.BackColor;
        if (back.A < 128) return control.ForeColor;
        return Distance(back, surface) >= Distance(control.ForeColor, surface) ? back : control.ForeColor;
    }

    private static int Distance(Color a, Color b) => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
}
