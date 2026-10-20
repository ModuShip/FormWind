using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Dark background for a standard <see cref="DateTimePicker"/>. With visual styles Windows draws the edit area, the up-down
/// buttons and the drop-down calendar from the theme and ignores <c>BackColor</c>; no theme name (<c>DarkMode_Explorer</c>,
/// <c>DarkMode_CFD</c> …) darkens them. When the classes give the picker a dark background, Windows' drawing is rendered into
/// a bitmap and its lightness inverted: neutral pixels map onto the picker's colors (the theme's white onto <c>BackColor</c>,
/// its black text onto <c>ForeColor</c>), colored ones (the selected field, the check box) keep their hue. The layout, the selected field and keyboard editing stay Windows'. The drop-down calendar's theme ignores its colors
/// and font and draws ClearType text whose colored edges would survive the inversion; it is shown without the theme instead,
/// in the calendar colors of the classes (<c>MCM_SETCOLOR</c>), resized to the unthemed layout. A light background leaves
/// Windows' drawing alone. Without visual styles the picker has a 3D client edge outside its client area; that frame
/// (<c>WM_NCPAINT</c>, and <c>WM_PRINT</c> for <see cref="Control.DrawToBitmap"/>) is inverted the same way.
/// </summary>
internal static class TwDateTimePickerPainter
{
    private const int WmPaint = 0x000F;
    private const int WmPrintClient = 0x0318;
    private const int PrfClient = 0x0004;
    private const int PrfEraseBkgnd = 0x0008;
    private const int PrfNonClient = 0x0002;
    private const int WmNcPaint = 0x0085;
    private const int WmPrint = 0x0317;
    private const int DtmGetMonthCal = 0x1008;
    private const int McmSetColor = 0x100A;
    private const int McmGetMinReqRect = 0x1009;
    private const int GwChild = 5;
    private const int WmSetFont = 0x0030;
    private const int WmGetFont = 0x0031;
    private const byte AntialiasedQuality = 4;

    private static readonly ConditionalWeakTable<DateTimePicker, Picker> Pickers = new ConditionalWeakTable<DateTimePicker, Picker>();

    /// <summary>Whether FormWind paints the picker (its background is dark).</summary>
    internal static bool IsPainted(DateTimePicker picker) => Pickers.TryGetValue(picker, out var state) && state.Active;

    /// <param name="picker">The picker to paint.</param>
    /// <param name="back">The background from the classes; a dark one turns the painting on.</param>
    /// <param name="fore">The text color from the classes; without one a readable color is chosen.</param>
    /// <param name="calendar">Colors of the drop-down calendar.</param>
    public static void Update(DateTimePicker picker, Color? back, Color? fore, TwCalendarColors calendar)
    {
        bool active = back is Color color && IsDark(color);
        if (!Pickers.TryGetValue(picker, out var state))
        {
            if (!active) return;
            state = Pickers.GetValue(picker, p => new Picker(p));
        }
        state.Fore = fore;
        state.Calendar = calendar;
        state.SetActive(active);
    }

    /// <summary>Whether light text reads better on <paramref name="color"/> than dark text.</summary>
    internal static bool IsDark(Color color) =>
        color.A > 0 && TwContrast.Ratio(Color.White, Opaque(color)) > TwContrast.Ratio(Color.Black, Opaque(color));

    private static Color Opaque(Color color) => Color.FromArgb(255, color);

    /// <summary>
    /// Maps one pixel drawn for a light background onto <paramref name="back"/>/<paramref name="fore"/> (BGRA).
    /// </summary>
    internal static int Invert(int pixel, Color back, Color fore)
    {
        int b = pixel & 0xFF, g = (pixel >> 8) & 0xFF, r = (pixel >> 16) & 0xFF;
        int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        int lightness = (max + min) / 2;

        // Nötr: beyaz zemin → back, siyah yazı → fore, aradaki griler (kenarlık, ikon) ikisinin karışımı.
        int nr = (back.R * lightness + fore.R * (255 - lightness)) / 255;
        int ng = (back.G * lightness + fore.G * (255 - lightness)) / 255;
        int nb = (back.B * lightness + fore.B * (255 - lightness)) / 255;

        // Renkli: her kanala aynı sayı eklenir; ton ve doygunluk kalır, HSL açıklığı 255 - L olur (açık mavi seçim → koyu mavi).
        int chroma = max - min;
        if (chroma > 16)
        {
            int shift = 255 - max - min;
            int weight = Math.Min(48, chroma - 16);
            nr = (nr * (48 - weight) + (r + shift) * weight) / 48;
            ng = (ng * (48 - weight) + (g + shift) * weight) / 48;
            nb = (nb * (48 - weight) + (b + shift) * weight) / 48;
        }
        return unchecked((int)0xFF000000) | (nr << 16) | (ng << 8) | nb;
    }

    private sealed class Picker
    {
        private readonly DateTimePicker _picker;
        private readonly Inverter _edit;
        private Inverter? _spin;

        public Picker(DateTimePicker picker)
        {
            _picker = picker;
            _edit = new Inverter(this, frame: true);
            picker.HandleCreated += (_, _) => { if (Active) Attach(); };
            picker.HandleDestroyed += (_, _) =>
            {
                _edit.ReleaseHandle();
                _spin?.ReleaseHandle();
            };
            picker.DropDown += (_, _) => OnDropDown();
        }

        public bool Active { get; private set; }
        public Color? Fore;
        public TwCalendarColors Calendar = new TwCalendarColors();

        public Color Back => Opaque(_picker.BackColor);

        public Color Text
        {
            get
            {
                if (Fore is Color fore) return Opaque(fore);
                var back = Back;
                var current = Opaque(_picker.ForeColor);
                return TwContrast.Ratio(current, back) >= TwContrast.TextMinimum ? current : Color.FromArgb(243, 244, 246);
            }
        }

        public void SetActive(bool active)
        {
            if (Active == active)
            {
                if (active) Invalidate();
                return;
            }
            Active = active;
            if (active)
            {
                Attach();
            }
            else
            {
                _edit.RestoreFont();
                _edit.ReleaseHandle();
                _spin?.ReleaseHandle();
            }
            Invalidate();
        }

        private void Invalidate()
        {
            if (!_picker.IsHandleCreated) return;
            _picker.Invalidate();
            RedrawWindow(_picker.Handle, IntPtr.Zero, IntPtr.Zero, RdwInvalidate | RdwFrame);
            if (_spin != null && _spin.Handle != IntPtr.Zero) InvalidateRect(_spin.Handle, IntPtr.Zero, true);
        }

        private void Attach()
        {
            if (!_picker.IsHandleCreated) return;
            if (_edit.Handle != _picker.Handle)
            {
                if (_edit.Handle != IntPtr.Zero) _edit.ReleaseHandle();
                _edit.AssignHandle(_picker.Handle);
            }
            _edit.SmoothFont(SendMessage(_picker.Handle, WmGetFont, IntPtr.Zero, IntPtr.Zero));
            // ShowUpDown'da yukarı/aşağı butonları ayrı bir alt penceredir (msctls_updown32).
            var child = GetWindow(_picker.Handle, GwChild);
            if (child != IntPtr.Zero)
            {
                _spin ??= new Inverter(this);
                if (_spin.Handle != child)
                {
                    if (_spin.Handle != IntPtr.Zero) _spin.ReleaseHandle();
                    _spin.AssignHandle(child);
                }
            }
        }

        // Açılan takvim her açılışta yeniden oluşturulur. Temasız çizimin yerleşimi temalınınkinden büyüktür; takvim ve
        // onu taşıyan açılır pencere yeni boyuta getirilir.
        private void OnDropDown()
        {
            if (!Active) return;
            var calendar = SendMessage(_picker.Handle, DtmGetMonthCal, IntPtr.Zero, IntPtr.Zero);
            if (calendar == IntPtr.Zero) return;
            try
            {
                SetWindowTheme(calendar, string.Empty, string.Empty);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                return;
            }
            var back = Opaque(Calendar.Back);
            var fore = Opaque(Calendar.Fore);
            // Klasik takvim gün adlarını ve bugün işaretini başlık zemininin renginde çizer; zemin üzerinde okunur olmalı.
            var title = Opaque(Calendar.Accent);
            if (TwContrast.Ratio(title, back) < TwContrast.LargeTextMinimum) title = Mix(title, fore);
            var titleText = TwContrast.Ratio(back, title) >= TwContrast.Ratio(fore, title) ? back : fore;
            SetColor(calendar, 0, back);                              // MCSC_BACKGROUND
            SetColor(calendar, 1, fore);                              // MCSC_TEXT
            SetColor(calendar, 2, title);                             // MCSC_TITLEBK
            SetColor(calendar, 3, titleText);                         // MCSC_TITLETEXT
            SetColor(calendar, 4, back);                              // MCSC_MONTHBK
            SetColor(calendar, 5, Composite(Calendar.Muted, back));   // MCSC_TRAILINGTEXT

            var required = new Rect();
            if (SendMessage(calendar, McmGetMinReqRect, IntPtr.Zero, ref required) == IntPtr.Zero) return;
            int width = required.Right - required.Left, height = required.Bottom - required.Top;
            GetWindowRect(calendar, out var bounds);
            int grow = width - (bounds.Right - bounds.Left), growDown = height - (bounds.Bottom - bounds.Top);
            if (grow <= 0 && growDown <= 0) return;
            SetWindowPos(calendar, IntPtr.Zero, 0, 0, width, height, SwpNoMove | SwpNoZOrder | SwpNoActivate);
            var host = GetParent(calendar);
            if (host != IntPtr.Zero && host != _picker.Handle && GetWindowRect(host, out var outer))
                SetWindowPos(host, IntPtr.Zero, 0, 0, outer.Right - outer.Left + Math.Max(0, grow), outer.Bottom - outer.Top + Math.Max(0, growDown),
                    SwpNoMove | SwpNoZOrder | SwpNoActivate);
        }

        private static void SetColor(IntPtr calendar, int part, Color color) =>
            SendMessage(calendar, McmSetColor, (IntPtr)part, (IntPtr)ColorTranslator.ToWin32(color));

        private static Color Mix(Color a, Color b) => Color.FromArgb((a.R + b.R) / 2, (a.G + b.G) / 2, (a.B + b.B) / 2);

        private static Color Composite(Color color, Color under)
        {
            int alpha = color.A;
            return Color.FromArgb(
                (color.R * alpha + under.R * (255 - alpha)) / 255,
                (color.G * alpha + under.G * (255 - alpha)) / 255,
                (color.B * alpha + under.B * (255 - alpha)) / 255);
        }
    }

    private sealed class Inverter : NativeWindow
    {
        private readonly Picker _owner;
        // DateTimePicker'ın kendi penceresi: font ve çerçeve de bu alt sınıftan geçer.
        private readonly bool _frame;
        // Pencereye verilen font ve gri tonlu kopyası.
        private IntPtr _source, _smooth;

        public Inverter(Picker owner, bool frame = false)
        {
            _owner = owner;
            _frame = frame;
        }

        /// <summary>
        /// Gives the window a grayscale-antialiased copy of <paramref name="font"/>. ClearType's colored subpixel edges would
        /// keep their color when the lightness is inverted; grayscale edges become blends of the two colors.
        /// </summary>
        public void SmoothFont(IntPtr font)
        {
            if (Handle != IntPtr.Zero && font != IntPtr.Zero && font != _smooth) SendMessage(Handle, WmSetFont, font, (IntPtr)1);
        }

        /// <summary>Gives the window back the font it was given (painting turned off).</summary>
        public void RestoreFont()
        {
            if (Handle != IntPtr.Zero && _source != IntPtr.Zero) SendMessage(Handle, WmSetFont, _source, (IntPtr)1);
        }

        protected override void OnHandleChange()
        {
            // Bırakılan (ya da yok edilen) pencere kopyayı artık kullanmıyor.
            if (Handle == IntPtr.Zero && _smooth != IntPtr.Zero)
            {
                DeleteObject(_smooth);
                _smooth = _source = IntPtr.Zero;
            }
            base.OnHandleChange();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmSetFont && _frame)
            {
                // WinForms ve DateTimePicker (takvime) fontu her zaman yeniden gönderebilir; kopyası verilir, eskisi
                // pencere yenisini aldıktan sonra silinir.
                var stale = IntPtr.Zero;
                if (!_owner.Active || m.WParam == IntPtr.Zero)
                {
                    stale = _smooth;
                    _smooth = _source = IntPtr.Zero;
                }
                else if (m.WParam != _smooth && SmoothCopy(m.WParam) is var copy && copy != IntPtr.Zero)
                {
                    stale = _smooth;
                    _source = m.WParam;
                    _smooth = copy;
                    m.WParam = copy;
                }
                base.WndProc(ref m);
                if (stale != IntPtr.Zero) DeleteObject(stale);
                return;
            }
            if (m.Msg == WmPaint && _owner.Active)
            {
                if (m.WParam != IntPtr.Zero)
                {
                    RenderClient(m.WParam);
                }
                else
                {
                    var paint = new PaintStruct();
                    var hdc = BeginPaint(Handle, ref paint);
                    try
                    {
                        RenderClient(hdc);
                    }
                    finally
                    {
                        EndPaint(Handle, ref paint);
                    }
                }
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == WmPrintClient && _owner.Active && (m.LParam.ToInt64() & PrfClient) != 0)
            {
                RenderClient(m.WParam);
                m.Result = IntPtr.Zero;
                return;
            }
            // Görsel stiller kapalıyken 3B kenarlık istemci alanı dışındadır (WS_EX_CLIENTEDGE).
            if (m.Msg == WmNcPaint && _frame && _owner.Active && Frame(out var window, out var inner))
            {
                var hdc = GetWindowDC(Handle);
                if (hdc != IntPtr.Zero)
                {
                    try
                    {
                        ExcludeClipRect(hdc, inner.Left, inner.Top, inner.Right, inner.Bottom);
                        Render(hdc, window, PrfNonClient, inner);
                    }
                    finally
                    {
                        ReleaseDC(Handle, hdc);
                    }
                }
                m.Result = IntPtr.Zero;
                return;
            }
            // DrawToBitmap: istemci alanı WM_PRINTCLIENT'le (yukarıda) zaten çevrilir; yalnızca çerçeve çevrilir.
            if (m.Msg == WmPrint && _frame && _owner.Active && (m.LParam.ToInt64() & PrfNonClient) != 0 && Frame(out window, out inner))
            {
                Render(m.WParam, window, (int)m.LParam.ToInt64(), inner);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        // Pencerenin boyutu ve istemci alanının pencere koordinatlarındaki yeri; çerçeve yoksa false.
        private bool Frame(out Size window, out Rect client)
        {
            window = Size.Empty;
            client = new Rect();
            if (!GetWindowRect(Handle, out var bounds) || !GetClientRect(Handle, out var area)) return false;
            var origin = new Point();
            ClientToScreen(Handle, ref origin);
            window = new Size(bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
            client = new Rect { Left = origin.X - bounds.Left, Top = origin.Y - bounds.Top };
            client.Right = client.Left + area.Right;
            client.Bottom = client.Top + area.Bottom;
            return window.Width != area.Right || window.Height != area.Bottom;
        }

        private void RenderClient(IntPtr target)
        {
            GetClientRect(Handle, out var client);
            Render(target, new Size(client.Right, client.Bottom), -1, null);
        }

        // Windows'un çizimi bir DIB'e alınır, açıklığı çevrilir ve hedefe kopyalanır. print < 0: istemci alanı
        // (WM_PRINTCLIENT), değilse bu bayraklarla WM_PRINT; keep içindeki pikseller (zaten çevrilmiş istemci alanı) kalır.
        private void Render(IntPtr target, Size size, int print, Rect? keep)
        {
            int width = size.Width, height = size.Height;
            if (width <= 0 || height <= 0) return;

            var info = new BitmapInfoHeader
            {
                Size = Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
            };
            var memory = CreateCompatibleDC(target);
            var bitmap = CreateDIBSection(target, ref info, 0, out var bits, IntPtr.Zero, 0);
            if (memory == IntPtr.Zero || bitmap == IntPtr.Zero)
            {
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (memory != IntPtr.Zero) DeleteDC(memory);
                return;
            }
            var old = SelectObject(memory, bitmap);
            try
            {
                // Görsel stilsiz DateTimePicker zemini kendisi çizmez (BeginPaint ekranı siler); bellek pencere rengiyle başlar.
                // WM_PRINTCLIENT'te yazıyı da çizmediği için istemci alanı WM_PAINT'le (wParam = hdc) çizdirilir.
                var fill = new Rect { Right = width, Bottom = height };
                FillRect(memory, ref fill, GetSysColorBrush(ColorWindow));
                var message = print < 0
                    ? Message.Create(Handle, WmPaint, memory, IntPtr.Zero)
                    : Message.Create(Handle, WmPrint, memory, (IntPtr)print);
                base.WndProc(ref message);
                GdiFlush();

                var pixels = new int[width * height];
                Marshal.Copy(bits, pixels, 0, pixels.Length);
                var back = _owner.Back;
                var fore = _owner.Text;
                int last = 0, mapped = Invert(0, back, fore);
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (keep is Rect k)
                    {
                        int x = i % width, y = i / width;
                        if (x >= k.Left && x < k.Right && y >= k.Top && y < k.Bottom) continue;
                    }
                    int pixel = pixels[i] & 0xFFFFFF;
                    if (pixel != last)
                    {
                        last = pixel;
                        mapped = Invert(pixel, back, fore);
                    }
                    pixels[i] = mapped;
                }
                Marshal.Copy(pixels, 0, bits, pixels.Length);
                BitBlt(target, 0, 0, width, height, memory, 0, 0, SrcCopy);
            }
            catch (Exception ex) when (!TwErrors.IsCritical(ex))
            {
                TwErrors.Trace("date picker", ex);
            }
            finally
            {
                SelectObject(memory, old);
                DeleteObject(bitmap);
                DeleteDC(memory);
            }
        }
    }

    private static IntPtr SmoothCopy(IntPtr font)
    {
        if (font == IntPtr.Zero) return IntPtr.Zero;
        var logFont = new LogFont();
        if (GetObject(font, Marshal.SizeOf<LogFont>(), ref logFont) == 0) return IntPtr.Zero;
        logFont.Quality = AntialiasedQuality;
        return CreateFontIndirect(ref logFont);
    }

    private const int SrcCopy = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct LogFont
    {
        public int Height, Width, Escapement, Orientation, Weight;
        public byte Italic, Underline, StrikeOut, CharSet, OutPrecision, ClipPrecision, Quality, PitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FaceName;
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetObject(IntPtr obj, int size, ref LogFont logFont);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontIndirect(ref LogFont logFont);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PaintStruct
    {
        public IntPtr Hdc;
        public int Erase;
        public Rect Paint;
        public int Restore;
        public int IncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr BeginPaint(IntPtr hwnd, ref PaintStruct paint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(IntPtr hwnd, ref PaintStruct paint);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr hwnd, IntPtr rect, bool erase);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hwnd, ref Point point);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(IntPtr hwnd, IntPtr update, IntPtr region, int flags);

    [DllImport("user32.dll")]
    private static extern int FillRect(IntPtr hdc, ref Rect rect, IntPtr brush);

    [DllImport("user32.dll")]
    private static extern IntPtr GetSysColorBrush(int index);

    private const int ColorWindow = 5;

    [DllImport("gdi32.dll")]
    private static extern int ExcludeClipRect(IntPtr hdc, int left, int top, int right, int bottom);

    private const int RdwInvalidate = 0x0001;
    private const int RdwFrame = 0x0400;

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, int flags);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref Rect lParam);

    [DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

    private const int SwpNoZOrder = 0x0004;
    private const int SwpNoMove = 0x0002;
    private const int SwpNoActivate = 0x0010;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfoHeader info, int usage, out IntPtr bits, IntPtr section, int offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdc, int x, int y, int width, int height, IntPtr source, int sx, int sy, int rop);

    [DllImport("gdi32.dll")]
    private static extern bool GdiFlush();
}
