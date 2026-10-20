using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Whether a window shows keyboard focus cues (<c>WM_QUERYUISTATE</c>): Windows hides focus rectangles until the keyboard is
/// used, unless "Underline access keys" / focus indicators are turned on. <c>Control.ShowFocusCues</c> is protected; FormWind
/// draws focus rectangles over native drawing it replaces (tab strip, DropDownList box) only when they are shown.
/// </summary>
internal static class TwFocusCues
{
    private const int WmQueryUiState = 0x0129;
    private const int UisfHideFocus = 0x1;

    public static bool Shown(Control control)
    {
        if (!control.IsHandleCreated) return false;
        return ((int)SendMessage(control.Handle, WmQueryUiState, IntPtr.Zero, IntPtr.Zero) & UisfHideFocus) == 0;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
}
