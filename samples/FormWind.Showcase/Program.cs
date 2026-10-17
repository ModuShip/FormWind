using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace FormWind.Showcase
{
    internal static class Program
    {
        /// <summary>
        /// Normal çalıştırma: vitrin formunu açar (<c>--controls</c>: kontroller formu, <c>--presets</c>: hazır görünümler ve
        /// AutoStyle; <c>--grid</c>: DataGridView özellikleri; <c>--charts</c>: grafik panosu; <c>--downtime</c>: duruş panosu (ısı haritası, halkalar, zaman çizelgesi, adımlar); <c>--preset fluent</c> temayı o paketle başlatır).
        /// <c>--snapshot çıktı.png [--dark] [--controls|--presets|--grid|--charts|--downtime] [--preset ad] [--focus] [--dpi 192]</c>: formu ekran dışında açıp
        /// görüntüsünü kaydeder.
        /// <c>--dpi</c> formu başka DPI'lı bir ekrana taşınmış gibi ölçekler (WM_DPICHANGED); yalnızca uygulama PerMonitorV2
        /// çalışıyorsa etkilidir (App.config'teki DpiAwareness açıklamasına bakın). <c>--check-native-fonts</c> çizimden sonra
        /// metin kutularının native fontunun geçerli olduğunu denetler; geçersizse süreç 3 koduyla çıkar (DPI testleri için).
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            TwTheme.Current = TwTheme.LoadIfExists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tw.config.json"));
            int presetIndex = Array.IndexOf(args, "--preset");
            if (presetIndex >= 0 && presetIndex + 1 < args.Length)
                TwTheme.Current = TwTheme.FromJson("{ \"preset\": \"" + args[presetIndex + 1] + "\" }");

            bool controls = Array.IndexOf(args, "--controls") >= 0;
            bool presets = Array.IndexOf(args, "--presets") >= 0;
            bool grid = Array.IndexOf(args, "--grid") >= 0;
            bool charts = Array.IndexOf(args, "--charts") >= 0;
            bool downtime = Array.IndexOf(args, "--downtime") >= 0;
            if (args.Length >= 2 && args[0] == "--snapshot")
            {
                bool dark = Array.IndexOf(args, "--dark") >= 0;
                using var form = downtime ? new DowntimeForm { DarkMode = dark } : charts ? new ChartsForm { DarkMode = dark } : grid ? new GridForm { DarkMode = dark } : presets ? new PresetsForm { DarkMode = dark }
                    : controls ? (Form)new ControlsForm { DarkMode = dark } : new MainForm { DarkMode = dark };
                if (Array.IndexOf(args, "--focus") >= 0 && form is MainForm main) main.Shown += (_, _) => main.FocusFirstInput();
                if (form is GridForm gridForm) gridForm.Shown += (_, _) => gridForm.ShowcaseState();
                int dpiIndex = Array.IndexOf(args, "--dpi");
                int? dpi = dpiIndex >= 0 && dpiIndex + 1 < args.Length ? int.Parse(args[dpiIndex + 1]) : (int?)null;
                using var bitmap = Snapshots.Render(form, dpi);
                bitmap.Save(args[1], ImageFormat.Png);
                if (Array.IndexOf(args, "--check-native-fonts") >= 0)
                {
                    var broken = Snapshots.TextBoxesWithInvalidFont(form);
                    File.WriteAllText(args[1] + ".fonts.txt", string.Join(Environment.NewLine, broken));
                    if (broken.Count > 0) return 3;
                }
                if (dpi != null)
                {
                    // Görsel DPI testleri referansı sürecin sistem DPI'ı ve gerçekleşen form DPI'ı ile seçer.
                    using var screen = Graphics.FromHwnd(IntPtr.Zero);
                    File.WriteAllText(args[1] + ".txt", $"system={screen.DpiX:0};form={form.DeviceDpi}");
                }
                return 0;
            }

            Application.Run(downtime ? new DowntimeForm() : charts ? new ChartsForm() : grid ? new GridForm() : presets ? new PresetsForm() : controls ? (Form)new ControlsForm() : new MainForm());
            return 0;
        }
    }

    /// <summary>Formu ekran dışında açıp görüntüsünü alır. Görsel regresyon testleri de bunu kullanır.</summary>
    public static class Snapshots
    {
        public static Bitmap Render(Form form) => Render(form, null);

        /// <param name="form">Çizilecek form.</param>
        /// <param name="dpi">Verilirse form bu DPI'lı bir ekrana taşınmış gibi ölçeklenir (yalnızca PerMonitorV2).</param>
        public static Bitmap Render(Form form, int? dpi)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-10000, -10000);
            form.ShowInTaskbar = false;
            form.Show();
            Application.DoEvents();

            if (dpi is int target && target != form.DeviceDpi)
            {
                // WM_DPICHANGED: wParam = yeni DPI (x, y), lParam = önerilen pencere dikdörtgeni.
                float factor = target / (float)form.DeviceDpi;
                var rect = new[] { form.Left, form.Top, form.Left + (int)(form.Width * factor), form.Top + (int)(form.Height * factor) };
                var handle = System.Runtime.InteropServices.GCHandle.Alloc(rect, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    SendMessage(form.Handle, 0x02E0, (IntPtr)((target << 16) | target), handle.AddrOfPinnedObject());
                }
                finally
                {
                    handle.Free();
                }
                Application.DoEvents();
            }

            var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            return bitmap;
        }

        /// <summary>
        /// Native fontu kontrolün <c>Font</c>'uyla uyuşmayan metin kutuları: WM_GETFONT'un verdiği handle silinmişse ya da
        /// (silinen handle'ın değeri başka bir fonta verildiğinden) başka boyutta bir fontu gösteriyorsa.
        /// </summary>
        public static System.Collections.Generic.List<string> TextBoxesWithInvalidFont(Control root)
        {
            var broken = new System.Collections.Generic.List<string>();
            using var screen = Graphics.FromHwnd(IntPtr.Zero);
            float dpi = screen.DpiY;
            Visit(root);
            return broken;

            void Visit(Control control)
            {
                if (control is TextBoxBase && control.IsHandleCreated)
                {
                    var font = SendMessage(control.Handle, 0x0031 /* WM_GETFONT */, IntPtr.Zero, IntPtr.Zero);
                    var logFont = new LogFont();
                    int expected = (int)Math.Round(control.Font.SizeInPoints * dpi / 72f);
                    if (font != IntPtr.Zero && (GetObject(font, System.Runtime.InteropServices.Marshal.SizeOf(typeof(LogFont)), logFont) == 0
                                                || Math.Abs(Math.Abs(logFont.Height) - expected) > 1))
                        broken.Add($"{control.Parent?.Name}/{control.Name}: native {logFont.Height} px, Font {expected} px");
                }
                foreach (Control child in control.Controls) Visit(child);
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private sealed class LogFont
        {
            public int Height, Width, Escapement, Orientation, Weight;
            public byte Italic, Underline, StrikeOut, CharSet, OutPrecision, ClipPrecision, Quality, PitchAndFamily;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)]
            public string FaceName = "";
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int GetObject(IntPtr handle, int size, [System.Runtime.InteropServices.Out] LogFont buffer);
    }
}
