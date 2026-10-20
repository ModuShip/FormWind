#if !NETFRAMEWORK
using System.Drawing;
using System.Windows.Forms;
using FormWind.Harness;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class HarnessTests
{
    // Class renkleri okunabilir görünse de ekrana başka renk çizilebilir (Windows'un devre dışı yazısı, kendi paletiyle
    // çizen kontroller); görüntüden ölçüm bunu yakalar.
    // RightToLeftLayout'lu formun PrintWindow görüntüsü aynalı gelir; çevrilmezse kontrollerin ekran dikdörtgenleri yanlış
    // bölgeye düşer ve okunur yazılar da görünmez sayılırdı.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Screen_audit_reports_text_that_is_invisible_on_screen(bool mirrored) => Sta.Run(() =>
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, ClientSize = new Size(300, 120), BackColor = Color.White };
        if (mirrored)
        {
            form.RightToLeft = RightToLeft.Yes;
            form.RightToLeftLayout = true;
        }
        var readable = new Label { Name = "readable", Text = "Okunur yazı", Location = new Point(10, 10), Size = new Size(110, 30), ForeColor = Color.Black, Font = new Font("Segoe UI", 12f) };
        var hidden = new Label { Name = "hidden", Text = "Görünmez yazı", Location = new Point(10, 60), Size = new Size(110, 30), ForeColor = Color.FromArgb(250, 250, 250), Font = new Font("Segoe UI", 12f) };
        form.Controls.Add(readable);
        form.Controls.Add(hidden);
        form.Show();
        form.Refresh(); // PrintWindow pencerenin çizilmiş içeriğini alır
        Application.DoEvents();

        var origin = ScreenContrast.WindowOrigin(form);
        using var image = ScreenContrast.Capture(form) ?? Render(form);
        var issue = Assert.Single(ScreenContrast.Audit(form, image, origin));
        Assert.Same(hidden, issue.Control);
        Assert.Equal(Color.White.ToArgb(), issue.Background.ToArgb());

        var (fore, back) = ScreenContrast.Measure(readable, image, origin);
        Assert.True(TwContrast.Ratio(fore!.Value, back!.Value) > 10);
        form.Close();
    });

    [Fact]
    public void Capture_includes_the_window_frame() => Sta.Run(() =>
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, ClientSize = new Size(200, 100) };
        form.Show();
        // Ekran dışındaki yeni pencerenin yüzeyi birkaç kare sonra hazır olur; o zamana kadar Capture null döner.
        Bitmap? image = null;
        for (int i = 0; i < 40 && image is null; i++)
        {
            form.Refresh();
            Application.DoEvents();
            image = ScreenContrast.Capture(form);
            if (image is null) System.Threading.Thread.Sleep(25);
        }
        using var captured = image;
        Assert.NotNull(image);
        Assert.Equal(form.Size, image!.Size);
        form.Close();
    });

    // formwind-app şablonuyla görüldü: harness FormWind kullanan uygulamanın formunu kendi class'larıyla yeniden stilleyip flex
    // yerleşimini bozuyor, sahte kontrast sorunları raporluyordu. Formun kendi sağlayıcısı bulunur ve o kullanılır.
    [Fact]
    public void Finds_the_applications_own_provider()
    {
        using var own = new OwnProviderForm();
        using var plain = new Form();
        Assert.Same(own.Provider, FormWindHarness.AppProvider(own));
        Assert.Null(FormWindHarness.AppProvider(plain));
    }

    private class BaseWithProvider : Form
    {
        private readonly TailwindProvider _tw = new TailwindProvider();
        public TailwindProvider Provider => _tw;

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tw.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class OwnProviderForm : BaseWithProvider
    {
    }

    // Standart DateTimePicker eşlemede yoktu; koyu temada açık kalır ve "unstyled" diye raporlanırdı.
    [Fact]
    public void Map_styles_date_time_pickers_for_both_modes()
    {
        using var picker = new DateTimePicker();
        var match = System.Linq.Enumerable.First(FormWindHarness.Map, m => m.Type.IsInstanceOfType(picker));
        Assert.Equal(typeof(DateTimePicker), match.Type);
        Assert.Contains("dark:bg-", match.Classes);
        Assert.Contains("dark:text-", match.Classes);
    }

    // Ekrandaki çizim (WM_PAINT) DrawToBitmap'ten ayrı yoldan geçer: görsel stilsiz DateTimePicker zemini BeginPaint'te
    // ekrana siler ve WM_PRINTCLIENT'te yazıyı çizmez; bellekteki çizimde siyah yazı siyah zeminde kayboluyordu.
    [Fact]
    public void Dark_date_time_picker_text_is_readable_on_screen() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, ClientSize = new Size(260, 60) };
        var picker = new DateTimePicker { Location = new Point(10, 10), Width = 220, Value = new System.DateTime(2026, 10, 18) };
        form.Controls.Add(picker);
        form.Show();
        provider.SetClasses(picker, "bg-white text-gray-900 dark:bg-gray-900 dark:text-gray-100");

        Bitmap? image = null;
        for (int i = 0; i < 40 && image is null; i++)
        {
            form.Refresh();
            Application.DoEvents();
            image = ScreenContrast.Capture(form);
            if (image is null) System.Threading.Thread.Sleep(25);
        }
        using var captured = image;
        Assert.NotNull(image);
        var origin = ScreenContrast.WindowOrigin(form);
        var area = picker.RectangleToScreen(picker.ClientRectangle);
        int y = area.Top - origin.Y + area.Height / 2;
        // Seçili alanın (ilk sayı) sağındaki ay adı: koyu zemin ve açık yazı birlikte.
        var row = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(area.Left - origin.X + area.Width / 4, area.Width / 3), x => image!.GetPixel(x, y)));
        Assert.Contains(row, p => p.GetBrightness() < 0.2f);
        Assert.Contains(row, p => p.GetBrightness() > 0.7f);
        form.Close();
    });

    private static Bitmap Render(Form form)
    {
        var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        return bitmap;
    }
}
#endif
