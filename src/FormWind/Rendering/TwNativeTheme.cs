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
/// (<c>NM_CUSTOMDRAW</c>, after the application and WinForms have handled the message). Header cells the application
/// draws itself (owner-drawn lists, ObjectListView's header styles) are left alone; cells it leaves to Windows get the
/// text color.
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
        if (control is TreeView tree)
        {
            var keeper = TreeKeepers.GetValue(tree, t => new TreeKeeper(t));
            keeper.Pushing = true;
            try
            {
                SetTheme(tree.Handle, dark);
            }
            finally
            {
                keeper.Pushing = false;
            }
            keeper.Update(dark);
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
        var painter = HeaderPainters.GetValue(list, l => new HeaderPainter(l));
        IntPtr header;
        painter.Pushing = true;
        try
        {
            SetTheme(list.Handle, dark, DarkItemsTheme);
            header = SendMessage(list.Handle, LvmGetHeader, IntPtr.Zero, IntPtr.Zero);
            if (header != IntPtr.Zero) SetTheme(header, dark, DarkItemsTheme);
        }
        finally
        {
            painter.Pushing = false;
        }
        painter.Pushes++;
        // Görsel stiller kapalıyken (EnableVisualStyles çağrılmamış, uzak masaüstü, klasik tema) DarkMode_ItemsView uygulanamaz
        // ve başlık klasik açık gri kalır: yazısı açık renge çevrilseydi okunmazdı (#F0F0F0 üzerinde 1,04:1).
        painter.Update(dark && header != IntPtr.Zero && ThemesActive, header);
    }

    /// <summary>How many times the list was given its theme (tests).</summary>
    internal static int ListPushes(ListView list) => HeaderPainters.TryGetValue(list, out var painter) ? painter.Pushes : 0;

    /// <summary>Whether the list's column header text is drawn in its text color where Windows draws it (dark mode).</summary>
    /// <summary>Windows'un temaları uygulamada etkin (comctl32 v6 ve görsel stiller); testler <see cref="ThemesOverride"/> ile benzetir.</summary>
    internal static bool ThemesActive => ThemesOverride ?? Application.RenderWithVisualStyles;

    /// <summary>Tests: whether themes are active (<c>null</c> = <see cref="Application.RenderWithVisualStyles"/>).</summary>
    internal static bool? ThemesOverride { get; set; }

    internal static bool PaintsHeader(ListView list) => HeaderPainters.TryGetValue(list, out var painter) && painter.Handle != IntPtr.Zero;

    private static readonly ConditionalWeakTable<TreeView, TreeKeeper> TreeKeepers = new ConditionalWeakTable<TreeView, TreeKeeper>();

    // KeePass grup ağacına SetWindowTheme(..., "explorer") verir: seçili düğüm açık temanın koyu yazısıyla çiziliyordu.
    // Koyu temadaki ağaç uygulamanın verdiği temadan sonra koyu temayı geri alır (ListView için bkz. HeaderPainter).
    private sealed class TreeKeeper : NativeWindow
    {
        private const int WmThemeChanged = 0x031A;
        private readonly TreeView _tree;
        public bool Pushing;
        public int Pushes;

        public TreeKeeper(TreeView tree)
        {
            _tree = tree;
            tree.HandleDestroyed += (_, _) => ReleaseHandle();
        }

        public void Update(bool dark)
        {
            Pushes++;
            if (!dark)
            {
                if (Handle != IntPtr.Zero) ReleaseHandle();
            }
            else if (Handle != _tree.Handle)
            {
                if (Handle != IntPtr.Zero) ReleaseHandle();
                AssignHandle(_tree.Handle);
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WmThemeChanged && !Pushing) Push(_tree, true);
        }
    }

    /// <summary>How many times the tree was given its theme (tests).</summary>
    internal static int TreePushes(TreeView tree) => TreeKeepers.TryGetValue(tree, out var keeper) ? keeper.Pushes : 0;

    private static readonly ConditionalWeakTable<ListView, HeaderPainter> HeaderPainters = new ConditionalWeakTable<ListView, HeaderPainter>();

    private sealed class HeaderPainter : NativeWindow
    {
        private const int WmNotify = 0x004E;
        private const int NmCustomDraw = -12;
        private const int CddsPrePaint = 0x00000001;
        private const int CddsItemPrePaint = 0x00010001;
        private const int CdrfSkipDefault = 0x00000004;
        private const int CdrfNotifyItemDraw = 0x00000020;
        private const int WmThemeChanged = 0x031A;
        private readonly ListView _list;
        private IntPtr _header;

        /// <summary>FormWind is setting the theme itself; its own WM_THEMECHANGED is not answered.</summary>
        public bool Pushing;
        public int Pushes;

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
            // Uygulama listeye kendi temasını verdi (KeePass OnLoad'da SetWindowTheme(..., "explorer") çağırır): açık
            // temanın grup başlıkları ve kaydırma çubukları koyu zeminde kalmasın diye koyu tema geri verilir.
            if (m.Msg == WmThemeChanged && !Pushing)
            {
                base.WndProc(ref m);
                PushList(_list, true);
                return;
            }
            if (m.Msg == WmNotify && m.LParam != IntPtr.Zero)
            {
                var header = Marshal.PtrToStructure<NmHdr>(m.LParam);
                if (header.HwndFrom == _header && header.Code == NmCustomDraw)
                {
                    // Önce uygulama ve WinForms işler (ObjectListView font seçer, owner-draw liste hücreyi kendisi
                    // çizebilir); çizimi Windows'a bırakılan hücrelerin yazı rengi atanır.
                    var draw = Marshal.PtrToStructure<NmCustomDrawInfo>(m.LParam);
                    base.WndProc(ref m);
                    long result = m.Result.ToInt64();
                    if ((result & CdrfSkipDefault) != 0) return;
                    if (draw.DrawStage == CddsPrePaint) m.Result = (IntPtr)(result | CdrfNotifyItemDraw);
                    else if (draw.DrawStage == CddsItemPrePaint) SetTextColor(draw.Hdc, ColorTranslator.ToWin32(_list.ForeColor));
                    return;
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
