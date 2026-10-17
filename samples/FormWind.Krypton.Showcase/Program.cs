using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using FormWind.Krypton;

namespace FormWind.KryptonShowcase;

internal static class Program
{
    /// <summary>
    /// <c>--preset fluent</c>: başlangıç görünümü. <c>--snapshot çıktı.png [--dark]</c>: formu ekran dışında açıp görüntüsünü
    /// kaydeder (README görüntüleri).
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        bool snapshot = args.Length >= 2 && args[0] == "--snapshot";
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // Görüntüler 96 DPI'da (ekranın ölçeğinden bağımsız) alınır.
        Application.SetHighDpiMode(snapshot ? HighDpiMode.DpiUnaware : HighDpiMode.PerMonitorV2);
        KryptonApplier.Register();

        int presetIndex = Array.IndexOf(args, "--preset");
        TwTheme.Current = presetIndex >= 0 && presetIndex + 1 < args.Length
            ? TwTheme.FromJson("{ \"preset\": \"" + args[presetIndex + 1] + "\" }")
            : new TwTheme(TwPreset.Fluent);

        using var form = new MainForm { DarkMode = Array.IndexOf(args, "--dark") >= 0 };
        if (snapshot)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-10000, -10000);
            form.ShowInTaskbar = false;
            form.Show();
            Application.DoEvents();
            // KryptonForm kendi çerçevesini çizdiğinden DrawToBitmap'i desteklemez; pencere PrintWindow ile çizdirilir.
            using var bitmap = new Bitmap(form.Width, form.Height);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var hdc = graphics.GetHdc();
                try
                {
                    PrintWindow(form.Handle, hdc, 2 /* PW_RENDERFULLCONTENT */);
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }
            bitmap.Save(args[1], ImageFormat.Png);
            return 0;
        }

        Application.Run(form);
        return 0;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
}
