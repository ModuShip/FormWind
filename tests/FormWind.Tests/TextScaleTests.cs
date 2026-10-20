using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Windows'un metin boyutu ayarı (Erişilebilirlik → Metin boyutu, %100–225) sistem fontlarını büyütür; <c>text-*</c>
/// class'ları onu yok sayıp kullanıcının büyüttüğü yazıyı küçültüyordu. Ayar <c>TwTextScale.Override</c> ile benzetilir
/// (sistem ayarı değiştirilmez).
/// </summary>
[Collection(UiCollection.Name)]
public class TextScaleTests
{
    [Fact]
    public void Class_font_sizes_follow_the_windows_text_size() => Sta.Run(() =>
    {
        try
        {
            using var provider = new TailwindProvider();
            using var label = new Label();
            provider.SetClasses(label, "text-sm");
            Assert.Equal(10.5f, label.Font.SizeInPoints, 2);                                     // 14 px

            TwTextScale.Override = 1.5f;
            TwTextScale.NotifyChanged();
            Assert.Equal(15.75f, label.Font.SizeInPoints, 2);                                    // yeniden uygulandı

            TwFonts.RespectTextScale = false;
            Assert.Equal(10.5f, label.Font.SizeInPoints, 2);
            TwFonts.RespectTextScale = true;
            Assert.Equal(15.75f, label.Font.SizeInPoints, 2);

            using var plain = new Label();
            provider.SetClasses(plain, "font-bold");                                             // boyut fonttan: çarpılmaz
            Assert.Equal(plain.Parent?.Font.SizeInPoints ?? Control.DefaultFont.SizeInPoints, plain.Font.SizeInPoints, 2);
        }
        finally
        {
            TwFonts.RespectTextScale = true;
            TwTextScale.Override = 1f;
            TwTextScale.NotifyChanged();
        }
    });

    // %225'te sabit boyutlu butonda yazı sığsın diye iç boşluk kısılır (btn'in py-2'si 56 px'lik butona sığmaz); FormWind'in
    // yeniden çizdiği sekme yazısı Windows'un sekme genişliğine sığar, kısaltılmaz. Yazının boşluksuz da sığmadığı sabit
    // kontroller uygulamanın yerleşimidir (WinForms'un büyüyen varsayılan fontunda da kırpılır).
    [Fact]
    public void Large_text_size_trims_padding_and_does_not_shorten_redrawn_text() => Sta.Run(() =>
    {
        try
        {
            TwTextScale.Override = 2.25f;
            TwTextScale.NotifyChanged();
            using var provider = new TailwindProvider();
            using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, ClientSize = new Size(700, 300) };
            var button = new Button { Text = "Kaydet", Size = new Size(200, 56), Location = new Point(0, 220) };
            var tabs = new TabControl { Size = new Size(700, 200), TabPages = { new TabPage("Genel"), new TabPage("Gelişmiş ayarlar") } };
            form.Controls.AddRange(new Control[] { tabs, button });
            form.Show();
            provider.SetClasses(button, "btn btn-primary");
            provider.SetClasses(tabs, "text-sm text-gray-900 bg-white selected:bg-white selected:text-gray-900");
            Assert.Equal(10.5f * 2.25f, tabs.Font.SizeInPoints, 2);

            Assert.True(button.Padding.Vertical < 16, $"padding {button.Padding}");                 // py-2 (2 × 8 px) kısıldı
            var text = TextRenderer.MeasureText(button.Text, button.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine);
            Assert.True(button.ClientSize.Height - button.Padding.Vertical >= text.Height, $"padding {button.Padding}, yazı {text.Height} px, buton {button.ClientSize.Height} px");

            using var bitmap = new Bitmap(tabs.Width, tabs.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                TabControlApplier.PaintTo(tabs, g);
            }
            for (int i = 0; i < tabs.TabCount; i++)
            {
                var tab = tabs.GetTabRect(i);
                // Windows'un yazıyı sığdırdığı sekmede (net472'de bu boyutta "Genel" sığmaz) FormWind kısaltmamalı: koyu yazı
                // pikselleri yazının ölçülen genişliğini kaplar.
                var expected = TextRenderer.MeasureText(tabs.TabPages[i].Text, tabs.Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                if (expected.Width > tab.Width) continue;
                int left = int.MaxValue, right = -1;
                for (int x = tab.Left; x < tab.Right; x++)
                    for (int y = tab.Top; y < tab.Bottom; y++)
                        if (bitmap.GetPixel(x, y) is var c && c.R + c.G + c.B < 3 * 110) { left = Math.Min(left, x); right = Math.Max(right, x); }
                Assert.True(right - left + 1 >= expected.Width - 8, $"'{tabs.TabPages[i].Text}': {right - left + 1} px çizildi, yazı {expected.Width} px");
            }
            form.Close();
        }
        finally
        {
            TwTextScale.Override = 1f;
            TwTextScale.NotifyChanged();
        }
    });
}
