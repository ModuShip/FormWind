using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Right-to-left (RTL) helpers. When a control's inherited <see cref="Control.RightToLeft"/> is <c>Yes</c>, FormWind mirrors
/// the layout and the parts it paints itself; when the form has <c>RightToLeftLayout</c>, Windows already mirrors the windows
/// and FormWind does not mirror again.
/// </summary>
internal static class TwRtl
{
    public static bool IsRtl(Control control) => control.RightToLeft == RightToLeft.Yes;

    /// <summary>Whether the window is drawn with Windows' mirrored layout (WS_EX_LAYOUTRTL).</summary>
    public static bool IsMirroredByWindows(Control control)
    {
        for (var current = control; current != null; current = current.Parent)
        {
            if (current is Form { RightToLeftLayout: true, RightToLeft: RightToLeft.Yes }) return true;
            if (current is TabControl { RightToLeftLayout: true, RightToLeft: RightToLeft.Yes }) return true;
        }
        return false;
    }

    /// <summary>Whether FormWind has to mirror its layout and painting itself.</summary>
    public static bool NeedsMirroring(Control control) => IsRtl(control) && !IsMirroredByWindows(control);

    /// <summary>Converts text flags to right-to-left reading: left and right alignment swap.</summary>
    public static TextFormatFlags Mirror(TextFormatFlags flags)
    {
        flags |= TextFormatFlags.RightToLeft;
        if ((flags & TextFormatFlags.HorizontalCenter) != 0) return flags;
        return (flags & TextFormatFlags.Right) != 0 ? flags & ~TextFormatFlags.Right : flags | TextFormatFlags.Right;
    }
}
