using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Title-bar-less window: the form's caption is removed and its content reaches the top edge, so a side bar or tool bar can
/// sit where the title bar was, while the window stays a real Windows window. Empty areas of the form and of the
/// <see cref="AddDragSurface">drag surfaces</see> (and the labels and icons on them) move the window; double-clicking them
/// maximizes or restores, dragging to a screen edge snaps (Aero Snap), right-clicking opens the system menu, and the
/// edges resize as before (the top edge too, within <see cref="ResizeBorder"/>). Add <see cref="TwCaptionButtons"/> for
/// minimize / maximize / close.
/// <code>
/// var chrome = TwWindowChrome.Attach(this);
/// chrome.AddDragSurface(toolbar);
/// toolbar.Controls.Add(new TwCaptionButtons { Dock = DockStyle.Right });
/// </code>
/// </summary>
public sealed class TwWindowChrome
{
    private static readonly ConditionalWeakTable<Form, TwWindowChrome> Table = new ConditionalWeakTable<Form, TwWindowChrome>();

    private readonly HashSet<Control> _surfaces = new HashSet<Control>();
    private readonly Dictionary<Control, ChildHook> _children = new Dictionary<Control, ChildHook>();
    private readonly FormHook _hook;
    private bool _attached = true;
    private int _resizeBorder = 6;

    private TwWindowChrome(Form form)
    {
        Form = form;
        _hook = new FormHook(this);
        form.HandleCreated += OnFormHandleCreated;
        if (form.IsHandleCreated) _hook.AssignHandle(form.Handle);
        Track(form);
        RefreshFrame();
    }

    /// <summary>Removes <paramref name="form"/>'s title bar (again, if it was removed and <see cref="Detach"/>ed).</summary>
    public static TwWindowChrome Attach(Form form)
    {
        if (form is null) throw new ArgumentNullException(nameof(form));
        if (Table.TryGetValue(form, out var existing))
        {
            if (!existing._attached)
            {
                existing._attached = true;
                existing.RefreshFrame();
            }
            return existing;
        }
        var chrome = new TwWindowChrome(form);
        Table.Add(form, chrome);
        return chrome;
    }

    /// <summary>The chrome attached to <paramref name="form"/>; <c>null</c> when none is.</summary>
    public static TwWindowChrome? Get(Form form) => form != null && Table.TryGetValue(form, out var chrome) && chrome._attached ? chrome : null;

    public Form Form { get; }

    /// <summary>Height of the band at the top edge that resizes the window (96 DPI pixels).</summary>
    public int ResizeBorder
    {
        get => _resizeBorder;
        set => _resizeBorder = Math.Max(0, value);
    }

    /// <summary>Whether empty areas of the form itself move the window (default <c>true</c>).</summary>
    public bool DragFromForm { get; set; } = true;

    /// <summary>Empty areas of <paramref name="control"/> (a tool bar, a side bar's header) move the window.</summary>
    public void AddDragSurface(Control control)
    {
        if (control is null) throw new ArgumentNullException(nameof(control));
        _surfaces.Add(control);
    }

    public void RemoveDragSurface(Control control) => _surfaces.Remove(control);

    /// <summary>Gives the form its title bar back.</summary>
    public void Detach()
    {
        if (!_attached) return;
        _attached = false;
        RefreshFrame();
    }

    /// <summary>Whether a point (screen coordinates) moves the window.</summary>
    public bool IsDragPoint(Point screen)
    {
        if (!_attached || !Form.IsHandleCreated) return false;
        var target = Deepest(screen);
        return target == Form ? DragFromForm : IsSurface(target);
    }

    private float Scale => TwDpi.DeviceDpi(Form) / 96f;

    private bool Resizable => Form.FormBorderStyle is FormBorderStyle.Sizable or FormBorderStyle.SizableToolWindow && Form.WindowState == FormWindowState.Normal;

    private bool HasFrame => Form.FormBorderStyle != FormBorderStyle.None;

    private void OnFormHandleCreated(object? sender, EventArgs e)
    {
        if (_hook.Handle != Form.Handle) _hook.AssignHandle(Form.Handle);
        RefreshFrame();
    }

    private void RefreshFrame()
    {
        if (!Form.IsHandleCreated) return;
        // Çerçeve yeniden hesaplanır: WM_NCCALCSIZE başlığı kaldırır (ya da geri verir).
        SetWindowPos(Form.Handle, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    // Formun altındaki tüm denetimler izlenir: üst kenardaki boyutlandırma şeridi ve sürükleme yüzeyleri onların üstünde de çalışsın.
    private void Track(Control parent)
    {
        parent.ControlAdded += OnControlAdded;
        parent.ControlRemoved += OnControlRemoved;
        foreach (Control child in parent.Controls) Hook(child);
    }

    private void Hook(Control control)
    {
        if (_children.ContainsKey(control)) return;
        var hook = new ChildHook(this, control);
        _children[control] = hook;
        Track(control);
    }

    private void Unhook(Control control)
    {
        if (!_children.TryGetValue(control, out var hook)) return;
        _children.Remove(control);
        hook.Release();
        control.ControlAdded -= OnControlAdded;
        control.ControlRemoved -= OnControlRemoved;
        foreach (Control child in control.Controls) Unhook(child);
    }

    private void OnControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control != null) Hook(e.Control);
    }

    private void OnControlRemoved(object? sender, ControlEventArgs e)
    {
        if (e.Control != null) Unhook(e.Control);
    }

    private Control Deepest(Point screen)
    {
        Control current = Form;
        while (true)
        {
            var child = current.GetChildAtPoint(current.PointToClient(screen), GetChildAtPointSkip.Invisible);
            if (child is null || child == current) return current;
            current = child;
        }
    }

    private bool IsSurface(Control control)
    {
        if (_surfaces.Contains(control)) return true;
        // Yüzeydeki etiket ve simgeler de sürükler; düğme, kutu gibi etkileşimli denetimler sürüklemez.
        if (control is not (Label and not LinkLabel or TwIcon or PictureBox)) return false;
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
        {
            if (_surfaces.Contains(parent)) return true;
            if (parent == Form) return DragFromForm;
            if (parent is not (Panel or GroupBox or TableLayoutPanel or FlowLayoutPanel)) return false;
        }
        return false;
    }

    private bool InTopBand(Point screen)
    {
        if (!Resizable) return false;
        var bounds = Form.Bounds;
        return screen.Y >= bounds.Top && screen.Y < bounds.Top + Math.Max(1, (int)Math.Round(_resizeBorder * Scale));
    }

    private int HitTest(Point screen)
    {
        if (InTopBand(screen))
        {
            int corner = (int)Math.Round(_resizeBorder * 2 * Scale);
            var bounds = Form.Bounds;
            return screen.X < bounds.Left + corner ? HtTopLeft : screen.X >= bounds.Right - corner ? HtTopRight : HtTop;
        }
        return IsDragPoint(screen) ? HtCaption : HtClient;
    }

    private static Point ScreenPoint(IntPtr lParam)
    {
        long value = lParam.ToInt64();
        return new Point(unchecked((short)(value & 0xffff)), unchecked((short)((value >> 16) & 0xffff)));
    }

    private sealed class FormHook : NativeWindow
    {
        private readonly TwWindowChrome _chrome;

        public FormHook(TwWindowChrome chrome) => _chrome = chrome;

        protected override void WndProc(ref Message m)
        {
            if (!_chrome._attached || !_chrome.HasFrame)
            {
                base.WndProc(ref m);
                return;
            }
            switch (m.Msg)
            {
                case WmNcCalcSize when m.WParam != IntPtr.Zero:
                {
                    // Varsayılan hesap yan ve alt kenarları (yerel boyutlandırma) bırakır; üstteki başlık geri alınır.
                    int top = Marshal.ReadInt32(m.LParam, 4);
                    base.WndProc(ref m);
                    int newTop = top;
                    if (_chrome.Form.WindowState == FormWindowState.Maximized)
                    {
                        // Büyütülmüş pencere ekranın dışına çerçeve kalınlığı kadar taşar; içerik ekranın içinde kalsın.
                        uint dpi = (uint)TwDpi.DeviceDpi(_chrome.Form);
                        newTop = top + SystemMetric(SmCySizeFrame, dpi) + SystemMetric(SmCxPaddedBorder, dpi);
                    }
                    Marshal.WriteInt32(m.LParam, 4, newTop);
                    m.Result = IntPtr.Zero;
                    return;
                }
                case WmNcHitTest:
                {
                    base.WndProc(ref m);
                    if (m.Result.ToInt64() == HtClient) m.Result = (IntPtr)_chrome.HitTest(ScreenPoint(m.LParam));
                    return;
                }
                case WmNcDestroy:
                    base.WndProc(ref m);
                    ReleaseHandle();
                    return;
            }
            base.WndProc(ref m);
        }
    }

    private sealed class ChildHook : NativeWindow
    {
        private readonly TwWindowChrome _chrome;
        private readonly Control _control;

        public ChildHook(TwWindowChrome chrome, Control control)
        {
            _chrome = chrome;
            _control = control;
            control.HandleCreated += OnHandleCreated;
            if (control.IsHandleCreated) AssignHandle(control.Handle);
        }

        private void OnHandleCreated(object? sender, EventArgs e)
        {
            if (Handle != _control.Handle) AssignHandle(_control.Handle);
        }

        public void Release()
        {
            _control.HandleCreated -= OnHandleCreated;
            if (Handle != IntPtr.Zero) ReleaseHandle();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmNcHitTest && _chrome._attached && _chrome.HasFrame)
            {
                base.WndProc(ref m);
                if (m.Result.ToInt64() == HtClient)
                {
                    // HTTRANSPARENT: isabet testi alttaki pencereye (forma) geçer; form başlık ya da kenar döndürür.
                    var screen = ScreenPoint(m.LParam);
                    if (_chrome.InTopBand(screen) || _chrome.IsSurface(_control)) m.Result = (IntPtr)HtTransparent;
                }
                return;
            }
            if (m.Msg == WmNcDestroy)
            {
                base.WndProc(ref m);
                ReleaseHandle();
                return;
            }
            base.WndProc(ref m);
        }
    }

    private static int SystemMetric(int index, uint dpi)
    {
        try
        {
            return GetSystemMetricsForDpi(index, dpi);
        }
        catch (EntryPointNotFoundException)
        {
            return GetSystemMetrics(index);
        }
    }

    private const int WmNcCalcSize = 0x0083;
    private const int WmNcHitTest = 0x0084;
    private const int WmNcDestroy = 0x0082;
    private const int HtTransparent = -1;
    private const int HtClient = 1;
    private const int HtCaption = 2;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int SmCySizeFrame = 33;
    private const int SmCxPaddedBorder = 92;
    private const int SwpNoSize = 0x0001;
    private const int SwpNoMove = 0x0002;
    private const int SwpNoZOrder = 0x0004;
    private const int SwpNoActivate = 0x0010;
    private const int SwpFrameChanged = 0x0020;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, int flags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int index, uint dpi);
}

/// <summary>
/// Minimize, maximize / restore and close buttons drawn like Windows' own, for a window without a title bar
/// (<see cref="TwWindowChrome"/>). Glyphs use <c>ForeColor</c>; the close button turns red under the mouse.
/// </summary>
[ToolboxItem(true)]
[Description("Minimize, maximize and close buttons for a window without a title bar.")]
public class TwCaptionButtons : Control
{
    private int _hover = -1;
    private int _pressed = -1;
    private Form? _form;

    public TwCaptionButtons()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        BackColor = Color.Transparent;
    }

    protected override Size DefaultSize => new Size(138, 32);

    /// <summary>Shows the minimize button.</summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool ShowMinimize { get; set; } = true;

    /// <summary>Shows the maximize / restore button.</summary>
    [Category("Behavior"), DefaultValue(true)]
    public bool ShowMaximize { get; set; } = true;

    /// <summary>Background of the close button under the mouse.</summary>
    [Category("Appearance")]
    public Color CloseHoverBackColor { get; set; } = ColorTranslator.FromHtml("#c42b1c");

    private bool ShouldSerializeCloseHoverBackColor() => CloseHoverBackColor.ToArgb() != ColorTranslator.FromHtml("#c42b1c").ToArgb();

    private float DpiScale => TwDpi.DeviceDpi(this) / 96f;

    private int ButtonWidth => (int)Math.Round(46 * DpiScale);

    private int Count => 1 + (ShowMinimize ? 1 : 0) + (ShowMaximize ? 1 : 0);

    public override Size GetPreferredSize(Size proposedSize) => new Size(ButtonWidth * Count, (int)Math.Round(32 * DpiScale));

    // Düğme sırası: küçült, büyüt, kapat (sağdan sola dillerde ayna).
    private enum Kind
    {
        Minimize,
        Maximize,
        Close,
    }

    private List<(Kind Kind, Rectangle Bounds)> Buttons()
    {
        var kinds = new List<Kind>();
        if (ShowMinimize) kinds.Add(Kind.Minimize);
        if (ShowMaximize) kinds.Add(Kind.Maximize);
        kinds.Add(Kind.Close);
        int width = Math.Min(ButtonWidth, Width / Math.Max(1, kinds.Count));
        var result = new List<(Kind, Rectangle)>();
        bool rtl = TwRtl.NeedsMirroring(this);
        for (int i = 0; i < kinds.Count; i++)
        {
            int slot = kinds.Count - 1 - i; // sağa yaslı
            int x = Width - (slot + 1) * width;
            if (rtl) x = Width - x - width;
            result.Add((kinds[i], new Rectangle(x, 0, width, Height)));
        }
        return result;
    }

    private int IndexAt(Point point)
    {
        var buttons = Buttons();
        for (int i = 0; i < buttons.Count; i++)
            if (buttons[i].Bounds.Contains(point)) return i;
        return -1;
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (_form != null) _form.Resize -= OnFormResize;
        _form = FindForm();
        if (_form != null) _form.Resize += OnFormResize;
    }

    private void OnFormResize(object? sender, EventArgs e) => Invalidate();

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index = IndexAt(e.Location);
        if (index == _hover) return;
        _hover = index;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        _pressed = IndexAt(e.Location);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        int pressed = _pressed;
        _pressed = -1;
        Invalidate();
        if (e.Button != MouseButtons.Left || pressed < 0 || pressed != IndexAt(e.Location)) return;
        PerformAction(Buttons()[pressed].Kind);
    }

    private void PerformAction(Kind kind)
    {
        var form = FindForm();
        if (form is null) return;
        switch (kind)
        {
            case Kind.Minimize: form.WindowState = FormWindowState.Minimized; break;
            case Kind.Maximize: form.WindowState = form.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; break;
            case Kind.Close: form.Close(); break;
        }
    }

    /// <summary>Does what the button of that kind does: <c>0</c> minimize, <c>1</c> maximize / restore, <c>2</c> close.</summary>
    internal void Press(int kind) => PerformAction((Kind)kind);

    protected override void OnPaint(PaintEventArgs e)
    {
        TwPainter.PaintParentBackground(this, e.Graphics);
        if (BackColor.A > 0) using (var back = new SolidBrush(BackColor)) e.Graphics.FillRectangle(back, ClientRectangle);
        var g = e.Graphics;
        float s = DpiScale;
        var buttons = Buttons();
        bool maximized = FindForm()?.WindowState == FormWindowState.Maximized;
        for (int i = 0; i < buttons.Count; i++)
        {
            var (kind, bounds) = buttons[i];
            var fore = ForeColor;
            if (i == _hover || i == _pressed)
            {
                var back = kind == Kind.Close ? CloseHoverBackColor : Color.FromArgb(i == _pressed ? 40 : 24, ForeColor);
                using var brush = new SolidBrush(back);
                g.FillRectangle(brush, bounds);
                if (kind == Kind.Close) fore = Color.White;
            }
            int glyph = (int)Math.Round(10 * s);
            var box = new Rectangle(bounds.Left + (bounds.Width - glyph) / 2, bounds.Top + (bounds.Height - glyph) / 2, glyph, glyph);
            using var pen = new Pen(fore, Math.Max(1f, s));
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = kind == Kind.Close ? SmoothingMode.AntiAlias : SmoothingMode.None;
            switch (kind)
            {
                case Kind.Minimize:
                    g.DrawLine(pen, box.Left, box.Top + box.Height / 2, box.Right, box.Top + box.Height / 2);
                    break;
                case Kind.Maximize when maximized:
                    int offset = (int)Math.Round(2 * s);
                    g.DrawRectangle(pen, box.Left, box.Top + offset, box.Width - offset, box.Height - offset);
                    g.DrawLines(pen, new[] { new Point(box.Left + offset, box.Top + offset), new Point(box.Left + offset, box.Top), new Point(box.Right, box.Top), new Point(box.Right, box.Bottom - offset), new Point(box.Right - offset, box.Bottom - offset) });
                    break;
                case Kind.Maximize:
                    g.DrawRectangle(pen, box.Left, box.Top, box.Width, box.Height);
                    break;
                case Kind.Close:
                    g.DrawLine(pen, box.Left, box.Top, box.Right, box.Bottom);
                    g.DrawLine(pen, box.Right, box.Top, box.Left, box.Bottom);
                    break;
            }
            g.SmoothingMode = smoothing;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _form != null)
        {
            _form.Resize -= OnFormResize;
            _form = null;
        }
        base.Dispose(disposing);
    }
}
