using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DurusMerkezi
{
    /// <summary>
    /// Windows başlık çubuğunu ve kenar çizgisini kaldırır; pencerenin geri kalan davranışı (kenarlardan boyutlandırma,
    /// ekran kenarına yapıştırma, simge durumuna küçültme animasyonu, DWM gölgesi, Windows 11 yuvarlak köşeleri) korunur.
    /// Başlık alanı istemci alanına katılır (WM_NCCALCSIZE); verilen yüzeylerden sürüklemek pencereyi taşır, çift tıklamak
    /// büyütür ve üst kenardaki ince şerit pencereyi yukarıdan boyutlandırır.
    /// </summary>
    internal sealed class WindowFrame : NativeWindow, IMessageFilter, IDisposable
    {
        private const int WM_NCCALCSIZE = 0x0083, WM_NCLBUTTONDOWN = 0x00A1, WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONDBLCLK = 0x0203;
        private const int HTCAPTION = 2, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;
        private const int TopResizeBand = 5, CornerBand = 12;

        private readonly Form _form;
        private readonly Func<Control, bool> _isDragSurface;

        public WindowFrame(Form form, Func<Control, bool> isDragSurface)
        {
            _form = form;
            _isDragSurface = isDragSurface;
            form.HandleCreated += (_, _) => Attach();
            form.HandleDestroyed += (_, _) => ReleaseHandle();
            if (form.IsHandleCreated) Attach();
            Application.AddMessageFilter(this);
        }

        private void Attach()
        {
            AssignHandle(_form.Handle);
            // Çerçeve yeniden hesaplansın: başlık alanı istemci alanına geçer.
            SetWindowPos(_form.Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);
            int round = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(_form.Handle, 33, ref round, sizeof(int));
        }

        /// <summary>Windows 11'in pencere çevresine çizdiği ince kenarın rengi (temaya uysun diye).</summary>
        public void SetBorderColor(Color color)
        {
            if (!_form.IsHandleCreated) return;
            int value = color.R | (color.G << 8) | (color.B << 16);
            DwmSetWindowAttribute(_form.Handle, 34, ref value, sizeof(int));
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                // Varsayılan hesap sol, sağ ve alt kenarları (görünmez boyutlandırma kenarları) bırakır; üstte başlık
                // yerine pencerenin kendi üst kenarı kullanılır. Büyütülmüş pencere ekranın dışına taşan çerçeve kadar içeri alınır.
                var before = Marshal.PtrToStructure<NcCalcSizeParams>(m.LParam);
                base.WndProc(ref m);
                var after = Marshal.PtrToStructure<NcCalcSizeParams>(m.LParam);
                after.Rect0.Top = before.Rect0.Top + (IsZoomed(_form.Handle) ? FrameThickness() : 0);
                Marshal.StructureToPtr(after, m.LParam, false);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        private int FrameThickness()
        {
            uint dpi = GetDpiForWindow(_form.Handle);
            return GetSystemMetricsForDpi(33 /* SM_CYSIZEFRAME */, dpi) + GetSystemMetricsForDpi(92 /* SM_CXPADDEDBORDER */, dpi);
        }

        // Pencerenin çocuk kontrollerine giden fare iletileri: sürükleme, çift tıklama ve üst kenardan boyutlandırma.
        bool IMessageFilter.PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_MOUSEMOVE && m.Msg != WM_LBUTTONDOWN && m.Msg != WM_LBUTTONDBLCLK) return false;
            var control = Control.FromChildHandle(m.HWnd);
            if (control == null || control.FindForm() != _form) return false;

            var screen = Cursor.Position;
            int hit = TopEdgeHit(screen);
            if (hit != 0)
            {
                if (m.Msg == WM_MOUSEMOVE) Cursor.Current = hit == HTTOP ? Cursors.SizeNS : hit == HTTOPLEFT ? Cursors.SizeNWSE : Cursors.SizeNESW;
                else if (m.Msg == WM_LBUTTONDOWN) SendNc(hit);
                return m.Msg != WM_MOUSEMOVE;
            }
            if (m.Msg == WM_MOUSEMOVE || !_isDragSurface(control)) return false;
            if (m.Msg == WM_LBUTTONDBLCLK)
                _form.WindowState = _form.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            else SendNc(HTCAPTION);
            return true;
        }

        private int TopEdgeHit(Point screen)
        {
            if (_form.WindowState != FormWindowState.Normal) return 0;
            var client = _form.RectangleToScreen(_form.ClientRectangle);
            if (screen.Y - client.Top >= TopResizeBand || screen.Y < client.Top) return 0;
            if (screen.X - client.Left < CornerBand) return HTTOPLEFT;
            if (client.Right - screen.X <= CornerBand) return HTTOPRIGHT;
            return HTTOP;
        }

        private void SendNc(int hit)
        {
            ReleaseCapture();
            SendMessage(_form.Handle, WM_NCLBUTTONDOWN, (IntPtr)hit, IntPtr.Zero);
        }

        public void Dispose()
        {
            Application.RemoveMessageFilter(this);
            ReleaseHandle();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NcCalcSizeParams
        {
            public NativeRect Rect0, Rect1, Rect2;
            public IntPtr Position;
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetricsForDpi(int index, uint dpi);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);
    }

    /// <summary>
    /// macOS tarzı pencere düğmeleri: kapat (kırmızı), simge durumuna küçült (sarı), büyüt/geri al (yeşil). Fare üzerindeyken
    /// simgeler belirir; pencere etkin değilken düğmeler gri olur. Klavyeyle de erişilebilir (Tab, Sol/Sağ ok, Enter).
    /// </summary>
    internal sealed class TrafficLights : Control
    {
        private static readonly Color[] Fill = { ColorTranslator.FromHtml("#ff5f57"), ColorTranslator.FromHtml("#febc2e"), ColorTranslator.FromHtml("#28c840") };
        private static readonly Color[] Edge = { ColorTranslator.FromHtml("#e0443e"), ColorTranslator.FromHtml("#dea123"), ColorTranslator.FromHtml("#1aab29") };
        private static readonly string[] Names = { "Kapat", "Simge durumuna küçült", "Büyüt" };

        private bool _hover;
        private int _pressed = -1;
        private int _focus = -1;
        private bool _active = true;

        public TrafficLights()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            TabStop = true;
            AccessibleName = "Pencere düğmeleri";
            AccessibleRole = AccessibleRole.ButtonDropDownGrid;
            Size = new Size(56, 16);
            Cursor = Cursors.Default;
        }

        private float DpiScale => DeviceDpi / 96f;

        private RectangleF Light(int index)
        {
            float d = 12 * DpiScale, gap = 8 * DpiScale;
            return new RectangleF(index * (d + gap) + 1, (Height - d) / 2f, d, d);
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < 3; i++)
                if (RectangleF.Inflate(Light(i), 3 * DpiScale, 3 * DpiScale).Contains(p)) return i;
            return -1;
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            var form = FindForm();
            if (form == null) return;
            form.Activated -= OnActivation;
            form.Deactivate -= OnActivation;
            form.Activated += OnActivation;
            form.Deactivate += OnActivation;
        }

        private void OnActivation(object sender, EventArgs e)
        {
            _active = Form.ActiveForm == FindForm();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            bool maximized = FindForm()?.WindowState == FormWindowState.Maximized;
            for (int i = 0; i < 3; i++)
            {
                var r = Light(i);
                bool lit = _active || _hover || Focused;
                var fill = lit ? Fill[i] : Color.FromArgb(100, 128, 128, 128);
                if (_pressed == i) fill = ControlPaint.Dark(fill, 0.15f);
                using (var brush = new SolidBrush(fill)) g.FillEllipse(brush, r);
                using (var pen = new Pen(lit ? Edge[i] : Color.FromArgb(60, 0, 0, 0), DpiScale * 0.75f)) g.DrawEllipse(pen, r);
                if (Focused && ShowFocusCues && _focus == i)
                    using (var ring = new Pen(Color.FromArgb(200, 59, 130, 246), 2 * DpiScale)) g.DrawEllipse(ring, RectangleF.Inflate(r, 2 * DpiScale, 2 * DpiScale));
                if (!_hover && !(Focused && ShowFocusCues)) continue;

                // Simgeler: ×, −, ve büyütmek için çapraz iki üçgen (geri almak için içe bakan).
                using var glyph = new Pen(Color.FromArgb(150, 0, 0, 0), 1.2f * DpiScale) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
                float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, k = r.Width * 0.22f;
                if (i == 0)
                {
                    g.DrawLine(glyph, cx - k, cy - k, cx + k, cy + k);
                    g.DrawLine(glyph, cx - k, cy + k, cx + k, cy - k);
                }
                else if (i == 1) g.DrawLine(glyph, cx - k * 1.2f, cy, cx + k * 1.2f, cy);
                else
                {
                    using var dark = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
                    float a = k * 1.25f;
                    g.FillPolygon(dark, Triangle(cx, cy, a, true, maximized));
                    g.FillPolygon(dark, Triangle(cx, cy, a, false, maximized));
                }
            }
        }

        // Büyüt simgesi: sol üst ve sağ alt köşelerde iki küçük dik üçgen; büyütülmüşken köşeler merkeze bakar.
        private static PointF[] Triangle(float cx, float cy, float a, bool topLeft, bool inward)
        {
            float gap = a * 0.2f;
            if (topLeft != inward)
                return new[] { new PointF(cx - a, cy - a), new PointF(cx + a - gap * 2, cy - a), new PointF(cx - a, cy + a - gap * 2) };
            return new[] { new PointF(cx + a, cy + a), new PointF(cx - a + gap * 2, cy + a), new PointF(cx + a, cy - a + gap * 2) };
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _pressed = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            _pressed = HitTest(e.Location);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int pressed = _pressed;
            _pressed = -1;
            Invalidate();
            if (pressed >= 0 && pressed == HitTest(e.Location)) Invoke(pressed);
        }

        protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            if (_focus < 0) _focus = 0;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left: _focus = Math.Max(0, _focus - 1); break;
                case Keys.Right: _focus = Math.Min(2, _focus + 1); break;
                case Keys.Enter or Keys.Space when _focus >= 0: Invoke(_focus); break;
                default: return;
            }
            AccessibilityNotifyClients(AccessibleEvents.Focus, _focus);
            Invalidate();
            e.Handled = true;
        }

        private void Invoke(int index)
        {
            var form = FindForm();
            if (form == null) return;
            switch (index)
            {
                case 0: form.Close(); break;
                case 1: form.WindowState = FormWindowState.Minimized; break;
                default: form.WindowState = form.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; break;
            }
            Invalidate();
        }

        protected override AccessibleObject CreateAccessibilityInstance() => new LightsAccessibleObject(this);

        private sealed class LightsAccessibleObject : ControlAccessibleObject
        {
            private readonly TrafficLights _owner;

            public LightsAccessibleObject(TrafficLights owner) : base(owner) => _owner = owner;

            public override int GetChildCount() => 3;

            public override AccessibleObject GetChild(int index) => new Light(_owner, this, index);

            private sealed class Light : AccessibleObject
            {
                private readonly TrafficLights _owner;
                private readonly AccessibleObject _parent;
                private readonly int _index;

                public Light(TrafficLights owner, AccessibleObject parent, int index)
                {
                    _owner = owner;
                    _parent = parent;
                    _index = index;
                }

                public override string Name => _index == 2 && _owner.FindForm()?.WindowState == FormWindowState.Maximized ? "Önceki boyutuna getir" : Names[_index];
                public override AccessibleRole Role => AccessibleRole.PushButton;
                public override AccessibleObject Parent => _parent;
                public override Rectangle Bounds => _owner.RectangleToScreen(Rectangle.Round(_owner.Light(_index)));
                public override string DefaultAction => "Bas";
                public override void DoDefaultAction() => _owner.Invoke(_index);
            }
        }
    }
}
