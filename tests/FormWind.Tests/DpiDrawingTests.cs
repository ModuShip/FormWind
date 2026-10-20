using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// FormWind'in Windows'un yerine çizdiği yazılar (devre dışı yazı, sekme şeridi) yüksek DPI'da Windows'un yerleşimiyle
/// örtüşmeli. DPI'ın yerleşime etkisi fontun ve işaretin büyümesidir; testler 9, 13,5 ve 18 pt'lik fontlarla (%100,
/// %150 ve %200'deki 9 pt) çizer. Gerçek ekran değişimi harness'te <c>FORMWIND_DPI</c> ile denenir.
/// </summary>
[Collection(UiCollection.Name)]
public class DpiDrawingTests
{
    public static readonly TheoryData<float> Sizes = new() { 9f, 13.5f, 18f };
    public static readonly TheoryData<float, bool> SizesAndDirections = new() { { 9f, false }, { 13.5f, false }, { 18f, false }, { 9f, true }, { 18f, true } };

    // Etkinken Windows'un çizdiği yazı ile devre dışıyken FormWind'in çizdiği yazı (aynı renk) aynı yere düşmeli.
    // %150'de etiket 3, onay kutusu 2 piksel kayıyordu; iki satıra sarılan etiket farklı yerden bölünüyordu. RTL'de yazı
    // sağa yaslanır ve işaret öbür yana geçer.
    [Theory]
    [MemberData(nameof(SizesAndDirections))]
    public void Disabled_text_is_redrawn_where_windows_draws_it(float size, bool rtl) => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, BackColor = Color.White };
        if (rtl) form.RightToLeft = RightToLeft.Yes;
        using var font = new Font("Segoe UI", size);
        int line = font.Height;
        var controls = new Control[]
        {
            new Label { Text = "Devre dışı etiket yazısı", AutoSize = true },
            new Label { Text = "Devre dışı, iki satıra sarılan uzun bir etiket yazısı burada", AutoSize = false, Size = new Size(line * 10, line * 3) },
            new Label { Text = "Sağa yaslı", AutoSize = false, Size = new Size(line * 10, line * 2), TextAlign = ContentAlignment.MiddleRight, Padding = new Padding(3) },
            new CheckBox { Text = "Devre dışı onay kutusu", AutoSize = true, Checked = true },
            new CheckBox { Text = "Sağda işaret", AutoSize = true, CheckAlign = ContentAlignment.MiddleRight },
            new RadioButton { Text = "Devre dışı seçenek", AutoSize = true },
            new Button { Text = "Kaydet", Size = new Size(line * 8, line * 2) },
            new Button { Text = "Sola yaslı", Size = new Size(line * 8, line * 2), TextAlign = ContentAlignment.MiddleLeft },
        };
        int y = 4;
        // Uygulamalar Main'de SetCompatibleTextRenderingDefault(false) çağırır (GDI); test süreci çağırmaz (GDI+).
        controls[0].Text = "GDI ile etiket yazısı";
        var compatible = new Label { Text = "GDI+ ile etiket yazısı", AutoSize = true, UseCompatibleTextRendering = true };
        foreach (var control in controls)
        {
            if (control is Label l) l.UseCompatibleTextRendering = false;
            else ((ButtonBase)control).UseCompatibleTextRendering = false;
        }
        controls = controls.Concat(new Control[] { compatible }).ToArray();
        foreach (var control in controls)
        {
            control.Font = font;
            control.Location = new Point(4, y);
            form.Controls.Add(control);
            y += control.Height + 4;
        }
        form.ClientSize = new Size(line * 16, y);
        form.Show();
        foreach (var control in controls)
            provider.SetClasses(control, control is Button
                ? "bg-white text-gray-900 border border-gray-300 disabled:text-gray-900"
                : "text-gray-900 disabled:text-gray-900");
        Application.DoEvents();

        foreach (var control in controls)
        {
            var enabled = Ink(control);
            control.Enabled = false;
            Assert.True(TwDisabledText.IsRepainted(control) || control is Button, $"{control.GetType().Name} '{control.Text}' yeniden çizilmeli");
            var disabled = Ink(control);
            control.Enabled = true;
            Assert.True(enabled != Rectangle.Empty, $"{control.Text}: yazı bulunamadı");
            Assert.True(Math.Abs(enabled.Left - disabled.Left) <= 1 && Math.Abs(enabled.Right - disabled.Right) <= 1
                        && Math.Abs(enabled.Top - disabled.Top) <= 1 && Math.Abs(enabled.Bottom - disabled.Bottom) <= 1,
                $"{size} pt{(rtl ? " RTL" : "")} {control.GetType().Name} '{control.Text}': Windows {enabled}, FormWind {disabled}");
        }
        form.Close();
    });

    // Sekme yazısı Windows'un sığdırdığı sekmede kısaltılmamalı: TextRenderer'ın iç boşluğu %200'de "Genel"i "Gen…"
    // yapıyordu. İkonlu sekmede ikon, boşluk ve yazı birlikte ortalanır.
    [Theory]
    [MemberData(nameof(SizesAndDirections))]
    public void Tab_strip_text_is_not_shortened(float size, bool rtl) => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        using var font = new Font("Segoe UI", size);
        using var images = new ImageList { ImageSize = new Size(16, 16) };
        var icon = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(icon)) g.Clear(Color.Red);
        images.Images.Add(icon);
        var tabs = new TabControl { Font = font, ImageList = images, Size = new Size(font.Height * 30, font.Height * 6), RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No };
        tabs.TabPages.Add(new TabPage("Genel") { ImageIndex = 0 });
        tabs.TabPages.Add(new TabPage("Gelişmiş ayarlar"));
        tabs.TabPages.Add(new TabPage("Hakkında"));
        // Odak başka kontrolde: odaklı sekme şeridi odak çerçevesi çizer (KeyboardFocusTests), yazı ölçümünü bozmasın.
        var other = new Button { Location = new Point(0, tabs.Height) };
        form.Controls.Add(tabs);
        form.Controls.Add(other);
        form.ClientSize = new Size(tabs.Width, tabs.Height + other.Height);
        form.Show();
        other.Focus();
        provider.SetClasses(tabs, "bg-white text-gray-900 selected:bg-white selected:text-gray-900");

        using var bitmap = new Bitmap(tabs.Width, tabs.Height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            TabControlApplier.PaintTo(tabs, g);
        }
        for (int i = 0; i < tabs.TabCount; i++)
        {
            var tab = tabs.GetTabRect(i);
            var text = tabs.TabPages[i].Text;
            int expected = InkWidth(text, font);
            int left = int.MaxValue, right = -1, iconRight = -1;
            for (int x = tab.Left; x < tab.Right; x++)
                for (int y = tab.Top; y < tab.Bottom; y++)
                {
                    var c = bitmap.GetPixel(x, y);
                    if (c.R > 200 && c.G < 60 && c.B < 60) iconRight = Math.Max(iconRight, x);
                    else if (c.R + c.G + c.B < 3 * 110) { left = Math.Min(left, x); right = Math.Max(right, x); }
                }
            Assert.True(right - left + 1 >= expected - 2, $"{size} pt '{text}': {right - left + 1} px çizildi, yazının tamamı {expected} px (sekme {tab})");
            // Windows ikonu RTL'de de yazının soluna koyar (pencere aynalanmamışsa).
            if (i == 0) Assert.True(iconRight >= 0 && iconRight < left, $"ikon {iconRight}, yazı {left}");
        }
        form.Close();
    });

    // Bulk Crap Uninstaller'da görüldü: devre dışı GroupBox başlığı koyu zeminde zeminden türetilen koyu renkle çiziliyordu.
    // FormWind başlığı GroupBoxRenderer'ın yerleşimiyle yeniden çizer; yerleşim temalı renderer'ın çizimiyle karşılaştırılır
    // (test süreci görsel stilsiz çalıştığından renderer doğrudan temayla çizdirilir).
    [Theory]
    [MemberData(nameof(SizesAndDirections))]
    public void Group_box_captions_are_redrawn_where_the_theme_draws_them(float size, bool rtl) => Sta.Run(() =>
    {
        if (!System.Windows.Forms.VisualStyles.VisualStyleRenderer.IsSupported) return;
        using var font = new Font("Segoe UI", size);
        using var group = new GroupBox { Text = "Devre dışı grup", Font = font, Size = new Size(font.Height * 14, font.Height * 4), RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No };
        var flags = TwDisabledText.GroupCaptionFlags(group);
        bool matching = GroupBoxRenderer.RenderMatchingApplicationState;
        Rectangle themed, redrawn;
        try
        {
            GroupBoxRenderer.RenderMatchingApplicationState = false;
            using var a = new Bitmap(group.Width, group.Height);
            using (var g = Graphics.FromImage(a))
            {
                g.Clear(Color.White);
                GroupBoxRenderer.DrawGroupBox(g, new Rectangle(Point.Empty, group.Size), group.Text, font, Color.Black, flags, System.Windows.Forms.VisualStyles.GroupBoxState.Normal);
            }
            themed = Dark(a, font.Height);
            using var b = new Bitmap(group.Width, group.Height);
            using (var g = Graphics.FromImage(b))
            {
                g.Clear(Color.White);
                TextRenderer.DrawText(g, group.Text, font, TwDisabledText.GroupCaptionBounds(group, g, flags), Color.Black, flags);
            }
            redrawn = Dark(b, font.Height);
        }
        finally
        {
            GroupBoxRenderer.RenderMatchingApplicationState = matching;
        }
        Assert.True(Math.Abs(themed.Left - redrawn.Left) <= 1 && Math.Abs(themed.Right - redrawn.Right) <= 1 && Math.Abs(themed.Top - redrawn.Top) <= 1,
            $"{size} pt{(rtl ? " RTL" : "")}: tema {themed}, FormWind {redrawn}");
    });

    // Başlık satırındaki koyu (yazı) pikseller; çerçeve açık gri kalır.
    private static Rectangle Dark(Bitmap bitmap, int rows)
    {
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < Math.Min(rows, bitmap.Height); y++)
                if (bitmap.GetPixel(x, y) is var c && c.R + c.G + c.B < 3 * 110)
                {
                    left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                }
        return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right, bottom);
    }

    // Yazının kısaltılmadan çizildiğindeki mürekkep genişliği.
    private static int InkWidth(string text, Font font)
    {
        using var bitmap = new Bitmap(2000, 200);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            TextRenderer.DrawText(g, text, font, new Point(10, 10), Color.Black, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
        int left = int.MaxValue, right = -1;
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
                if (bitmap.GetPixel(x, y) is var c && c.R + c.G + c.B < 3 * 110) { left = Math.Min(left, x); right = Math.Max(right, x); }
        return right - left + 1;
    }

    // Yazı pikselleri (koyu) çevresindeki dikdörtgen; işaret ve kenarlık açık renkte kalır ya da dışarıda bırakılır.
    private static Rectangle Ink(Control control)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        int skip = control is ButtonBase and not Button ? Glyph(control) : 0;
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
            {
                if (IsGlyph(control, x, skip)) continue;
                var c = bitmap.GetPixel(x, y);
                if (c.R + c.G + c.B > 3 * 110) continue;        // gray-900 yazı; açık zemin, kenarlık ve işaret dışarıda
                left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
            }
        return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right, bottom);
    }

    // WPF denemesinde bulundu: GDI+ ile çizen (UseCompatibleTextRendering; WPF uygulamalarında varsayılan) devre dışı onay
    // kutusunun yazı alanı GDI ile ölçülüyordu; GDI+'ın yazının iki yanına eklediği boşluğa yetmeyen alanda yazı iki satıra
    // bölünüp kırpılıyordu (%125'te). Fark fontla büyüdüğünden 96 DPI'da büyük bir fontla denenir.
    [Fact]
    public void Disabled_check_box_text_area_fits_gdi_plus_text() => Sta.Run(() =>
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var check = new CheckBox { Text = "Kapalı seçenek", AutoSize = true, Enabled = false, UseCompatibleTextRendering = true, Font = new Font("Segoe UI", 20f) };
        form.Controls.Add(check);
        form.Show();

        var text = TwDisabledText.CheckTextBounds(check);
        using var graphics = check.CreateGraphics();
        using var format = new StringFormat(StringFormat.GenericDefault);
        var needed = Size.Ceiling(graphics.MeasureString(check.Text, check.Font, new SizeF(1000, 1000), format));
        Assert.True(text.Width >= needed.Width, $"yazı alanı {text.Width} px, GDI+ yazısı {needed.Width} px");
        Assert.True(text.Height < check.Font.Height * 2, $"yazı alanı iki satır: {text.Height} px");
        form.Close();
    });

    // İşaretin kapladığı şerit (yazının dışında); mavi dolu işaret koyu sayılabilir.
    private static int Glyph(Control control)
    {
        var text = TwDisabledText.CheckTextBounds((ButtonBase)control);
        return GlyphRight(control) ? control.Width - text.Right : text.Left;
    }

    private static bool GlyphRight(Control control) =>
        control is CheckBox { CheckAlign: ContentAlignment.MiddleRight } ^ (control.RightToLeft == RightToLeft.Yes);

    private static bool IsGlyph(Control control, int x, int skip) =>
        skip > 0 && (GlyphRight(control) ? x >= control.Width - skip + 1 : x < skip - 1);
}
