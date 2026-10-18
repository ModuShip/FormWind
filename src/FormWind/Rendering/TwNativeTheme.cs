using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Switches native scrollbars to the dark theme (<c>SetWindowTheme(hwnd, "DarkMode_Explorer")</c>, Windows 10 1809+).
/// This undocumented theme name is used by Windows Explorer; it has no effect on unsupported systems.
/// The value is reapplied when the handle is recreated.
/// <para>
/// A ListView and its column header get Explorer's dark items view theme (<c>DarkMode_ItemsView</c>): dark scrollbars,
/// header and group headers. The theme draws the header text dark, so it is drawn in the list's text color
/// (<c>NM_CUSTOMDRAW</c>); owner-drawn lists (<see cref="ListView.OwnerDraw"/>) draw their headers themselves.
/// </para>
/// </summary>
internal static class TwNativeTheme
{
    private const string DarkTheme = "DarkMode_Explorer";
    private const string DarkItemsTheme = "DarkMode_ItemsView";
    private const int LvmGetHeader = 0x101F;

    private static readonly ConditionalWeakTable<Control, State> States = new ConditionalWeakTable<Control, State>();

    /// <summary>Controls that can have native scrollbars. Form excluded: FormApplier manages the title bar.</summary>
    public static bool Supports(Control control) =>
        !(control is Form)
        && (control is ScrollableControl || control is TextBoxBase || control is ListBox || control is TreeView
            || control is ListView || control is DataGridView || control is ScrollBar);

    public static bool IsDark(Control control) => States.TryGetValue(control, out var state) && state.Dark;

    public static void Apply(Control control, bool dark)
    {
        var state = States.GetValue(control, c =>
        {
            var created = new State();
            c.HandleCreated += (_, _) => Push(c, created.Dark);
            // DataGridView kaydırma çubuklarının handle'ı grid'inkinden sonra, ilk gösterildiklerinde oluşur.
            if (c is DataGridView)
            {
                foreach (Control child in c.Controls)
                    if (child is ScrollBar bar)
                    {
                        bar.HandleCreated += (_, _) => SetTheme(bar.Handle, created.Dark);
                        bar.VisibleChanged += (_, _) => { if (bar.IsHandleCreated) SetTheme(bar.Handle, created.Dark); };
                    }
            }
            return created;
        });
        if (state.Dark == dark && state.Pushed) return;
        state.Dark = dark;
        state.Pushed = true;
        if (control.IsHandleCreated) Push(control, dark);
    }

    private static void Push(Control control, bool dark)
    {
        if (control is ListView list)
        {
            PushList(list, dark);
            return;
        }
        SetTheme(control.Handle, dark);

        // DataGridView kaydırma çubuklarını ayrı ScrollBar alt kontrolleri olarak çizer.
        if (control is DataGridView)
        {
            foreach (Control child in control.Controls)
                if (child is ScrollBar && child.IsHandleCreated) SetTheme(child.Handle, dark);
        }
    }

    // Liste ve başlığı ItemsView temasını alır; başlık yazısını tema koyu çizdiği için alt sınıf yazı rengini atar.
    private static void PushList(ListView list, bool dark)
    {
        SetTheme(list.Handle, dark, DarkItemsTheme);
        var header = SendMessage(list.Handle, LvmGetHeader, IntPtr.Zero, IntPtr.Zero);
        if (header != IntPtr.Zero) SetTheme(header, dark, DarkItemsTheme);
        var painter = HeaderPainters.GetValue(list, l => new HeaderPainter(l));
        painter.Update(dark && !list.OwnerDraw && header != IntPtr.Zero, header);
    }

    /// <summary>Whether the list's column header text is drawn in its text color (dark mode).</summary>
    internal static bool PaintsHeader(ListView list) => HeaderPainters.TryGetValue(list, out var painter) && painter.Handle != IntPtr.Zero;

    private static readonly ConditionalWeakTable<ListView, HeaderPainter> HeaderPainters = new ConditionalWeakTable<ListView, HeaderPainter>();

    private sealed class HeaderPainter : NativeWindow
    {
        private const int WmNotify = 0x004E;
        private const int NmCustomDraw = -12;
        private const int CddsPrePaint = 0x00000001;
        private const int CddsItemPrePaint = 0x00010001;
        private const int CdrfNotifyItemDraw = 0x00000020;
        private readonly ListView _list;
        private IntPtr _header;

        public HeaderPainter(ListView list)
        {
            _list = list;
            list.HandleDestroyed += (_, _) => ReleaseHandle();
        }

        public void Update(bool active, IntPtr header)
        {
            _header = header;
            if (!active)
            {
                if (Handle != IntPtr.Zero) ReleaseHandle();
                return;
            }
            if (Handle != _list.Handle)
            {
                if (Handle != IntPtr.Zero) ReleaseHandle();
                AssignHandle(_list.Handle);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNotify && m.LParam != IntPtr.Zero)
            {
                var header = Marshal.PtrToStructure<NmHdr>(m.LParam);
                if (header.HwndFrom == _header && header.Code == NmCustomDraw)
                {
                    var draw = Marshal.PtrToStructure<NmCustomDrawInfo>(m.LParam);
                    if (draw.DrawStage == CddsPrePaint)
                    {
                        m.Result = (IntPtr)CdrfNotifyItemDraw;
                        return;
                    }
                    if (draw.DrawStage == CddsItemPrePaint)
                    {
                        SetTextColor(draw.Hdc, ColorTranslator.ToWin32(_list.ForeColor));
                        m.Result = IntPtr.Zero;
                        return;
                    }
                }
            }
            base.WndProc(ref m);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NmHdr
    {
        public IntPtr HwndFrom;
        public UIntPtr IdFrom;
        public int Code;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NmCustomDrawInfo
    {
        public NmHdr Header;
        public int DrawStage;
        public IntPtr Hdc;
        public int Left, Top, Right, Bottom;
        public UIntPtr ItemSpec;
        public uint ItemState;
        public IntPtr ItemParam;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("gdi32.dll")]
    private static extern int SetTextColor(IntPtr hdc, int color);

    private static void SetTheme(IntPtr handle, bool dark, string theme = DarkTheme)
    {
        try
        {
            // null/null varsayılan temayı geri yükler.
            SetWindowTheme(handle, dark ? theme : null, null);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    private sealed class State
    {
        public bool Dark;
        public bool Pushed;
    }
}
