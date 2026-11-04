using System;
using System.Drawing.Imaging;
using System.Windows.Forms;
using FormWind;

namespace DurusMerkezi
{
    internal static class Program
    {
        /// <summary>
        /// Duruş Merkezi: üretim duruşlarının vardiya kapanışı, girişi, kök neden analizi, kronik arıza takibi ve raporlaması.
        /// Veriler örnektir. <c>--light</c> açık temayla başlatır; <c>--snapshot çıktı.png [--page 0-4] [--light]</c> bir
        /// ekranın tüm içeriğini (kaydırılan kısım dahil) görüntü olarak kaydeder.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            TwTheme.Current = TwTheme.FromJson(Theme.Json);
            TwToast.Position = TwToastPosition.TopRight;
            // design.md süreleri: sayfa geçişi 180 ms, panel kayması 260 ms, sheet girişi 320 ms ve çıkışı 200 ms; sheet 12 px kayar.
            TwMotion.Timings.CrossFade = 180;
            TwMotion.Timings.Slide = 260;
            TwMotion.Timings.WindowIn = 320;
            TwMotion.Timings.WindowOut = 200;
            TwWindowMotion.Distance = 12;
            bool light = Array.IndexOf(args, "--light") >= 0;
            int pageIndex = Array.IndexOf(args, "--page");
            int page = pageIndex >= 0 && pageIndex + 1 < args.Length ? int.Parse(args[pageIndex + 1]) : 0;

            if (args.Length >= 2 && args[0] == "--snapshot")
            {
                using var form = new MainForm { DarkMode = !light };
                form.ShowPage(page);
                using var bitmap = form.RenderFullPage();
                bitmap.Save(args[1], ImageFormat.Png);
                return 0;
            }

            var main = new MainForm { DarkMode = !light };
            main.ShowPage(page);
            Application.Run(main);
            return 0;
        }
    }
}
