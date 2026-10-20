using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Native controls that Windows paints in system colors while disabled, ignoring <c>BackColor</c>: in dark mode they stayed
/// light (KeePass's entry list while no database is open, Bulk Crap Uninstaller while it loads). When the classes give the
/// control a background, FormWind paints them in their colors while disabled; the text is the text color mixed halfway
/// toward the background (<see cref="TwDisabledText.TextColor(Control)"/>).
/// <list type="bullet">
/// <item><description><see cref="ListView"/> (not owner-drawn): the background (<c>WM_ERASEBKGND</c>, and the custom draw
/// pre-paint stage for double-buffered lists) and the items (<c>NM_CUSTOMDRAW</c>); selected items get a shade of the
/// background instead of the system highlight.</description></item>
/// <item><description>Editable <see cref="ComboBox"/> (<c>DropDown</c>, <c>Simple</c>): the edit area asks its combo box
/// for colors with <c>WM_CTLCOLORSTATIC</c> while disabled.</description></item>
/// </list>
/// Enabled controls are left to Windows.
/// </summary>
internal sealed class TwDisabledNative : NativeWindow
{
    private const int WmEraseBkgnd = 0x0014;
    private const int WmCtlColorStatic = 0x0138;
    private const int WmReflectNotify = 0x2000 + 0x004E;
    private const int NmCustomDraw = -12;
    private const int CddsPrePaint = 0x00000001;
    private const int CddsItemPrePaint = 0x00010001;
    private const int CdrfNotifyItemDraw = 0x00000020;
    private const int CdisSelected = 0x0001;
    private const int CdisFocus = 0x0010;
    private const int LvmGetItemState = 0x102C;
    private const int LvisSelected = 0x0002;

    private static readonly ConditionalWeakTable<Control, TwDisabledNative> Table = new ConditionalWeakTable<Control, TwDisabledNative>();

    private readonly Control _control;
    private bool _active;
    private IntPtr _brush;
    private Color _brushColor;

    private TwDisabledNative(Control control)
    {
        _control = control;
        control.HandleCreated += (_, _) => { if (_active) AssignHandle(control.Handle); };
        control.HandleDestroyed += (_, _) =>
        {
            ReleaseHandle();
            DeleteBrush();
        };
    }

    /// <summary>Whether the control can be painted while disabled.</summary>
    public static bool Supports(Control control) =>
        control is ListView { OwnerDraw: false } || control is ComboBox { DropDownStyle: not ComboBoxStyle.DropDownList };

    /// <summary>Whether FormWind paints the control while it is disabled.</summary>
    internal static bool IsActive(Control control) => Table.TryGetValue(control, out var state) && state._active;

    /// <param name="control">The control.</param>
    /// <param name="active">The classes give the control a background.</param>
    public static void Update(Control control, bool active)
    {
        active &= Supports(control);
        if (!Table.TryGetValue(control, out var state))
        {
            if (!active) return;
            state = Table.GetValue(control, c => new TwDisabledNative(c));
        }
        if (state._active == active) return;
        state._active = active;
        if (active)
        {
            if (control.IsHandleCreated) state.AssignHandle(control.Handle);
        }
        else
        {
            state.ReleaseHandle();
            state.DeleteBrush();
        }
        if (!control.Enabled) control.Invalidate(true);
    }

    protected override void WndProc(ref Message m)
    {
        if (!_active || _control.Enabled)
        {
            base.WndProc(ref m);
            return;
        }
        try
        {
            if (_control is ListView list && HandleList(list, ref m)) return;
            if (_control is ComboBox && m.Msg == WmCtlColorStatic)
            {
                SetBkColor(m.WParam, ColorTranslator.ToWin32(_control.BackColor));
                SetTextColor(m.WParam, ColorTranslator.ToWin32(TwDisabledText.TextColor(_control)));
                m.Result = Brush(_control.BackColor);
                return;
            }
        }
        catch (Exception ex) when (!TwErrors.IsCritical(ex))
        {
            TwErrors.Trace("disabled native control", ex);
        }
        base.WndProc(ref m);
    }

    private bool HandleList(ListView list, ref Message m)
    {
        if (list.OwnerDraw) return false;
        if (m.Msg == WmEraseBkgnd)
        {
            using (var g = Graphics.FromHdc(m.WParam))
            using (var brush = new SolidBrush(list.BackColor))
                g.FillRectangle(brush, list.ClientRectangle);
            m.Result = (IntPtr)1;
            return true;
        }
        if (m.Msg != WmReflectNotify || m.LParam == IntPtr.Zero) return false;
        var header = Marshal.PtrToStructure<NmHdr>(m.LParam);
        if (header.HwndFrom != list.Handle || header.Code != NmCustomDraw) return false;

        var draw = Marshal.PtrToStructure<NmListViewCustomDraw>(m.LParam);
        if (draw.DrawStage == CddsPrePaint)
        {
            // Çift tamponlu listede zemin WM_PAINT içinde sistem rengiyle çizilmiştir; ilk aşamada örtülür.
            using (var g = Graphics.FromHdc(draw.Hdc))
            using (var brush = new SolidBrush(list.BackColor))
                g.FillRectangle(brush, Rectangle.FromLTRB(draw.Left, draw.Top, draw.Right, draw.Bottom));
            m.Result = (IntPtr)CdrfNotifyItemDraw;
            return true;
        }
        if (draw.DrawStage == CddsItemPrePaint)
        {
            // Seçim durumu uItemState'te güvenilir değildir (devre dışı liste); öğeden okunur.
            int index = (int)draw.ItemSpec.ToUInt64();
            bool selected = ((int)SendMessage(list.Handle, LvmGetItemState, (IntPtr)index, (IntPtr)LvisSelected) & LvisSelected) != 0;
            draw.ItemState &= ~(CdisSelected | CdisFocus);
            draw.TextColor = ColorTranslator.ToWin32(TwDisabledText.TextColor(list));
            draw.TextBackColor = ColorTranslator.ToWin32(selected ? SelectionShade(list.BackColor, list.ForeColor) : list.BackColor);
            Marshal.StructureToPtr(draw, m.LParam, false);
            m.Result = IntPtr.Zero;
            return true;
        }
        return false;
    }

    /// <summary>Background of a selected item in a disabled list: the background moved 15 % toward the text.</summary>
    internal static Color SelectionShade(Color back, Color fore) => Color.FromArgb(
        back.R + (fore.R - back.R) * 15 / 100,
        back.G + (fore.G - back.G) * 15 / 100,
        back.B + (fore.B - back.B) * 15 / 100);

    private IntPtr Brush(Color color)
    {
        if (_brush == IntPtr.Zero || _brushColor.ToArgb() != color.ToArgb())
        {
            DeleteBrush();
            _brush = CreateSolidBrush(ColorTranslator.ToWin32(color));
            _brushColor = color;
        }
        return _brush;
    }

    private void DeleteBrush()
    {
        if (_brush == IntPtr.Zero) return;
        DeleteObject(_brush);
        _brush = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NmHdr
    {
        public IntPtr HwndFrom;
        public UIntPtr IdFrom;
        public int Code;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NmListViewCustomDraw
    {
        public NmHdr Header;
        public int DrawStage;
        public IntPtr Hdc;
        public int Left, Top, Right, Bottom;
        public UIntPtr ItemSpec;
        public int ItemState;
        public IntPtr ItemParam;
        public int TextColor;
        public int TextBackColor;
        public int SubItem;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("gdi32.dll")]
    private static extern int SetBkColor(IntPtr hdc, int color);

    [DllImport("gdi32.dll")]
    private static extern int SetTextColor(IntPtr hdc, int color);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(int color);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}
