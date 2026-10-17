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
    [Fact]
    public void Screen_audit_reports_text_that_is_invisible_on_screen() => Sta.Run(() =>
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, ClientSize = new Size(300, 120), BackColor = Color.White };
        var readable = new Label { Name = "readable", Text = "Okunur yazı", Location = new Point(10, 10), Size = new Size(200, 30), ForeColor = Color.Black, Font = new Font("Segoe UI", 12f) };
        var hidden = new Label { Name = "hidden", Text = "Görünmez yazı", Location = new Point(10, 60), Size = new Size(200, 30), ForeColor = Color.FromArgb(250, 250, 250), Font = new Font("Segoe UI", 12f) };
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

    private static Bitmap Render(Form form)
    {
        var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        return bitmap;
    }
}
#endif
