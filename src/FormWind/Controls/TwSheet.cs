using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>How <see cref="TwSheet"/> shows a form inside its owner.</summary>
public sealed class TwSheetOptions
{
    /// <summary>Fills the owner's client area (less <see cref="Margin"/>) instead of keeping the form's own size, centered.</summary>
    public bool FullScreen { get; set; }

    /// <summary>Space kept between the sheet and the owner's edges (96 DPI pixels).</summary>
    public int Margin { get; set; } = 32;

    /// <summary>Color laid over the owner while the sheet is open.</summary>
    public Color DimColor { get; set; } = Color.Black;

    /// <summary>Opacity of <see cref="DimColor"/> (0–1).</summary>
    public double DimOpacity { get; set; } = 0.4;

    /// <summary>With <see cref="TwSheet.Attach(Form, TwSheetOptions?)"/>: which modal forms become sheets (<c>null</c> = all of them).</summary>
    public Func<Form, bool>? Filter { get; set; }

    internal TwSheetOptions Copy() => (TwSheetOptions)MemberwiseClone();
}

/// <summary>
/// Opens a form as a sheet inside its owner window: without its frame, centered over the owner's content (or filling it,
/// <see cref="TwSheetOptions.FullScreen"/>), with the owner dimmed behind it. The form stays a normal modal dialog —
/// <c>DialogResult</c>, <c>AcceptButton</c> / <c>CancelButton</c> and validation work as before — and gets its frame back when
/// it closes. <see cref="Attach(Form)"/> turns every modal form the owner opens into a sheet, so existing
/// <c>ShowDialog(this)</c> calls need no change.
/// <code>
/// using var editor = new RecordEditorForm(record);
/// if (TwSheet.ShowDialog(editor, this) == DialogResult.OK) Save(record);
///
/// TwSheet.Attach(this);                       // or: all dialogs of the main window as sheets
/// new RecordEditorForm(record).ShowDialog(this);
/// </code>
/// </summary>
public static class TwSheet
{
    private static readonly ConditionalWeakTable<Form, TwSheetOptions> Hosts = new ConditionalWeakTable<Form, TwSheetOptions>();
    private static readonly ConditionalWeakTable<Form, Request> Pending = new ConditionalWeakTable<Form, Request>();
    private static readonly ConditionalWeakTable<Form, SheetState> Open = new ConditionalWeakTable<Form, SheetState>();

    [ThreadStatic]
    private static ShowHook? _hook;

    /// <summary>Shows <paramref name="sheet"/> modally as a sheet inside <paramref name="owner"/>.</summary>
    public static DialogResult ShowDialog(Form sheet, Form owner) => ShowDialog(sheet, owner, null);

    /// <summary>Shows <paramref name="sheet"/> modally as a sheet inside <paramref name="owner"/>.</summary>
    public static DialogResult ShowDialog(Form sheet, Form owner, TwSheetOptions? options)
    {
        if (sheet is null) throw new ArgumentNullException(nameof(sheet));
        if (owner is null) throw new ArgumentNullException(nameof(owner));
        EnsureHook();
        Pending.Remove(sheet);
        Pending.Add(sheet, new Request(owner, (options ?? new TwSheetOptions()).Copy()));
        try
        {
            return sheet.ShowDialog(owner);
        }
        finally
        {
            Pending.Remove(sheet);
        }
    }

    /// <summary>Every modal form <paramref name="host"/> owns opens as a sheet from now on.</summary>
    public static void Attach(Form host) => Attach(host, null);

    /// <summary>Modal forms <paramref name="host"/> owns (those <see cref="TwSheetOptions.Filter"/> accepts) open as sheets from now on.</summary>
    public static void Attach(Form host, TwSheetOptions? options)
    {
        if (host is null) throw new ArgumentNullException(nameof(host));
        EnsureHook();
        Hosts.Remove(host);
        Hosts.Add(host, (options ?? new TwSheetOptions()).Copy());
    }

    /// <summary>Forms <paramref name="host"/> opens later get their own window again.</summary>
    public static void Detach(Form host)
    {
        if (host != null) Hosts.Remove(host);
    }

    /// <summary>Whether <paramref name="form"/> is open as a sheet.</summary>
    public static bool IsSheet(Form form) => form != null && Open.TryGetValue(form, out _);

    /// <summary>The dimming layer behind an open sheet (for tests).</summary>
    internal static Form? BackdropOf(Form sheet) => Open.TryGetValue(sheet, out var state) ? state.Backdrop : null;

    private static void EnsureHook() => _hook ??= new ShowHook();

    private static void OnShowing(Form form)
    {
        if (form is TwSheetBackdrop || Open.TryGetValue(form, out _)) return;
        Form? host;
        TwSheetOptions options;
        if (Pending.TryGetValue(form, out var request))
        {
            host = request.Owner;
            options = request.Options;
        }
        else
        {
            if (!form.Modal) return;
            host = form.Owner ?? Control.FromHandle(GetWindow(form.Handle, GwOwner)) as Form;
            if (host is null || !Hosts.TryGetValue(host, out var hostOptions)) return;
            if (hostOptions.Filter != null && !hostOptions.Filter(form)) return;
            options = hostOptions;
        }
        if (host.IsDisposed || !host.IsHandleCreated || host.WindowState == FormWindowState.Minimized) return;
        var state = new SheetState(form, host, options);
        Open.Add(form, state);
        state.Begin();
    }

    private static void OnClosed(SheetState state) => Open.Remove(state.Sheet);

    private sealed class Request
    {
        public Request(Form owner, TwSheetOptions options)
        {
            Owner = owner;
            Options = options;
        }

        public Form Owner { get; }
        public TwSheetOptions Options { get; }
    }

    private sealed class SheetState
    {
        private readonly FormBorderStyle _border;
        private readonly bool _controlBox;

        public SheetState(Form sheet, Form host, TwSheetOptions options)
        {
            Sheet = sheet;
            Host = host;
            Options = options;
            _border = sheet.FormBorderStyle;
            _controlBox = sheet.ControlBox;
        }

        public Form Sheet { get; }
        public Form Host { get; }
        public TwSheetOptions Options { get; }
        public TwSheetBackdrop? Backdrop { get; private set; }

        public void Begin()
        {
            // Çerçeve kalkınca istemci boyutu korunur: sayfa formun tasarlandığı boyda açılır.
            var client = Sheet.ClientSize;
            Sheet.FormBorderStyle = FormBorderStyle.None;
            Sheet.ClientSize = client;
            TwNativeCorners.Round(Sheet);

            Backdrop = new TwSheetBackdrop(Host, Options);
            Backdrop.Show(Host);
            Place();

            Host.LocationChanged += OnHostMoved;
            Host.SizeChanged += OnHostMoved;
            Sheet.Shown += OnShown;
            Sheet.FormClosed += OnFormClosed;
        }

        private void OnShown(object? sender, EventArgs e)
        {
            // Karartma sayfanın hemen altında durur (sahibin diğer pencerelerinin üstünde).
            if (Backdrop is { IsHandleCreated: true } backdrop && Sheet.IsHandleCreated)
                SetWindowPos(backdrop.Handle, Sheet.Handle, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }

        private void OnHostMoved(object? sender, EventArgs e) => Place();

        private void Place()
        {
            if (Host.IsDisposed || Sheet.IsDisposed) return;
            var area = Host.RectangleToScreen(Host.ClientRectangle);
            if (Backdrop != null && !Backdrop.IsDisposed) Backdrop.Bounds = area;
            int margin = (int)Math.Round(Math.Max(0, Options.Margin) * TwDpi.DeviceDpi(Host) / 96f);
            var room = Rectangle.Inflate(area, -margin, -margin);
            if (room.Width < 120 || room.Height < 80) room = area;
            Rectangle bounds;
            if (Options.FullScreen) bounds = room;
            else
            {
                var size = new Size(Math.Min(Sheet.Width, room.Width), Math.Min(Sheet.Height, room.Height));
                bounds = new Rectangle(room.Left + (room.Width - size.Width) / 2, room.Top + (room.Height - size.Height) / 2, size.Width, size.Height);
            }
            Sheet.Bounds = bounds;
        }

        private void OnFormClosed(object? sender, FormClosedEventArgs e)
        {
            Host.LocationChanged -= OnHostMoved;
            Host.SizeChanged -= OnHostMoved;
            Sheet.Shown -= OnShown;
            Sheet.FormClosed -= OnFormClosed;
            Backdrop?.Close();
            Backdrop?.Dispose();
            Backdrop = null;
            if (!Sheet.IsDisposed)
            {
                // Form yeniden ShowDialog ile (sayfa olmadan) açılırsa kendi çerçevesiyle açılır.
                var client = Sheet.ClientSize;
                Sheet.FormBorderStyle = _border;
                Sheet.ControlBox = _controlBox;
                Sheet.ClientSize = client;
            }
            OnClosed(this);
        }
    }

    // WH_CALLWNDPROC: WM_SHOWWINDOW pencere görünür olmadan gönderilir; form çerçevesiz ve yerinde açılır (titreme olmaz).
    private sealed class ShowHook
    {
        private readonly HookProc _proc;
        private IntPtr _handle;

        public ShowHook()
        {
            _proc = Callback;
            _handle = SetWindowsHookEx(WhCallWndProc, _proc, IntPtr.Zero, GetCurrentThreadId());
        }

        private IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && lParam != IntPtr.Zero && Marshal.ReadInt32(lParam, 2 * IntPtr.Size) == WmShowWindow
                && Marshal.ReadIntPtr(lParam, IntPtr.Size) != IntPtr.Zero)
            {
                var hwnd = Marshal.ReadIntPtr(lParam, IntPtr.Size == 8 ? 24 : 12);
                if (Control.FromHandle(hwnd) is Form form)
                {
                    try
                    {
                        OnShowing(form);
                    }
                    catch (Exception ex) when (!TwErrors.IsCritical(ex))
                    {
                        // Sayfaya çevrilemeyen form kendi penceresiyle açılır.
                    }
                }
            }
            return CallNextHookEx(_handle, code, wParam, lParam);
        }
    }

    private const int WhCallWndProc = 4;
    private const int WmShowWindow = 0x0018;
    private const int GwOwner = 4;
    private const int SwpNoSize = 0x0001;
    private const int SwpNoMove = 0x0002;
    private const int SwpNoActivate = 0x0010;

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, int flags);
}

/// <summary>The dimming layer over a sheet's owner; it takes no clicks and is never activated.</summary>
internal sealed class TwSheetBackdrop : Form
{
    private readonly double _target;
    private Timer? _fade;
    private DateTime _start;

    public TwSheetBackdrop(Form host, TwSheetOptions options)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        ControlBox = false;
        Text = "";
        BackColor = options.DimColor;
        _target = Math.Max(0, Math.Min(1, options.DimOpacity));
        Bounds = host.RectangleToScreen(host.ClientRectangle);
        bool animate = TwMotion.Enabled && TwMotion.Durations.S > 1;
        Opacity = animate ? 0.01 : Math.Max(0.01, _target);
        if (animate)
        {
            _start = DateTime.UtcNow;
            _fade = new Timer { Interval = 15 };
            _fade.Tick += (_, _) =>
            {
                double t = Math.Min(1, (DateTime.UtcNow - _start).TotalMilliseconds / Math.Max(1, TwMotion.Durations.S));
                Opacity = Math.Max(0.01, _target * t);
                if (t >= 1) _fade?.Stop();
            };
            _fade.Start();
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int WmMouseActivate = 0x0021, MaNoActivateAndEat = 4;
        if (m.Msg == WmMouseActivate)
        {
            m.Result = (IntPtr)MaNoActivateAndEat;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fade?.Dispose();
            _fade = null;
        }
        base.Dispose(disposing);
    }
}

/// <summary>Windows 11 rounded corners for a frameless window.</summary>
internal static class TwNativeCorners
{
    public static void Round(Form form)
    {
        if (!form.IsHandleCreated) return;
        try
        {
            int round = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(form.Handle, 33, ref round, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Eski Windows: köşeler düz kalır.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
