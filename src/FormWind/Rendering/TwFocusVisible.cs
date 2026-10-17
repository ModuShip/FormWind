using System;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Tracks whether the last input came from the keyboard or the mouse (like <c>:focus-visible</c> in browsers). Focus rings and
/// the <c>focus-visible:</c> variant show only when focus came from the keyboard (Tab, arrows), not on a control clicked with
/// the mouse. Tracking is added to the thread's message loop when the first FormWind control or provider is created.
/// </summary>
public static class TwFocusVisible
{
    [ThreadStatic] private static bool _installed;
    [ThreadStatic] private static bool _keyboard;

    /// <summary>Whether the last input came from the keyboard. Initially <c>false</c>: no ring is drawn for the focus given at startup.</summary>
    public static bool LastInputWasKeyboard
    {
        get => _keyboard;
        set => _keyboard = value;
    }

    internal static void EnsureInstalled()
    {
        if (_installed) return;
        _installed = true;
        Application.AddMessageFilter(new Filter());
    }

    private sealed class Filter : IMessageFilter
    {
        private const int WmKeyDown = 0x0100, WmSysKeyDown = 0x0104;
        private const int WmLButtonDown = 0x0201, WmRButtonDown = 0x0204, WmMButtonDown = 0x0207, WmXButtonDown = 0x020B;
        private const int WmNcLButtonDown = 0x00A1, WmPointerDown = 0x0246;

        public bool PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WmKeyDown:
                case WmSysKeyDown:
                    // Tek başına değiştirici tuşlar (Ctrl, Shift, Alt, Win) gezinme sayılmaz.
                    var key = (Keys)(int)(long)m.WParam;
                    if (key is not (Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)) _keyboard = true;
                    break;
                case WmLButtonDown:
                case WmRButtonDown:
                case WmMButtonDown:
                case WmXButtonDown:
                case WmNcLButtonDown:
                case WmPointerDown:
                    _keyboard = false;
                    break;
            }
            return false;
        }
    }
}
