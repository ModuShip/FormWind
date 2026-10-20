using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class HighContrastTests
{
    private readonly TwTheme _theme = new TwTheme();

    [Fact]
    public void Forced_colors_replace_class_colors_with_system_colors()
    {
        var parsed = _theme.Parse("bg-blue-600 text-white border border-blue-700 shadow-lg ring-2 ring-blue-500 bg-gradient-to-r from-blue-500 selected:bg-blue-600 selected:text-white");
        var style = parsed.Resolve(TwState.HighContrast);

        Assert.Null(style.BackColor);
        Assert.Null(style.ForeColor);
        Assert.Equal(1, style.BorderWidth);                                   // kenarlık kalır, rengi sistemin
        Assert.Equal(SystemColors.WindowText, style.BorderColor);
        Assert.Equal(SystemColors.Highlight, style.RingColor);
        Assert.False(style.HasGradient);
        Assert.Null(style.Shadow);

        var selected = parsed.ResolvePart("selected", TwState.HighContrast);
        Assert.Equal(SystemColors.Highlight, selected.BackColor);
        Assert.Equal(SystemColors.HighlightText, selected.ForeColor);

        Assert.Equal(ColorTranslator.FromHtml("#2563eb").ToArgb(), parsed.Resolve(TwState.None).BackColor!.Value.ToArgb());
    }

    [Fact]
    public void Forced_colors_variant_is_applied_on_top()
    {
        var parsed = _theme.Parse("bg-blue-600 forced-colors:border-2 forced-colors:bg-[#000000]");
        Assert.Empty(parsed.Unknown);

        var style = parsed.Resolve(TwState.HighContrast | TwState.Dark);
        Assert.Equal(Color.Black.ToArgb(), style.BackColor!.Value.ToArgb());
        Assert.Equal(2, style.BorderWidth);
        Assert.Null(parsed.Resolve(TwState.None).BorderWidth);               // mod kapalıyken uygulanmaz
    }

    [Fact]
    public void Provider_follows_high_contrast_changes() => Sta.Run(() =>
    {
        try
        {
            using var provider = new TailwindProvider();
            using var button = new Button();
            var original = button.BackColor;
            provider.SetClasses(button, "bg-blue-600 text-white px-4");
            Assert.Equal(ColorTranslator.FromHtml("#2563eb").ToArgb(), button.BackColor.ToArgb());

            TwHighContrast.Override = true;
            TwHighContrast.NotifyChanged();
            Assert.True(provider.HighContrastActive);
            Assert.Equal(original, button.BackColor);                       // sistem rengine döndü
            Assert.Equal(16, button.Padding.Left);                          // boşluk değişmez

            provider.RespectHighContrast = false;
            Assert.Equal(ColorTranslator.FromHtml("#2563eb").ToArgb(), button.BackColor.ToArgb());

            provider.RespectHighContrast = true;
            TwHighContrast.Override = false;
            TwHighContrast.NotifyChanged();
            Assert.Equal(ColorTranslator.FromHtml("#2563eb").ToArgb(), button.BackColor.ToArgb());
        }
        finally
        {
            TwHighContrast.Override = null;
        }
    });

    // Son eklenen çizimler (sekme şeridi, ProgressBar renkleri, ListView koyu teması ve başlık yazısı, devre dışı yazı,
    // PropertyGrid işaretleri, LinkLabel accent renkleri, DateTimePicker, satır hatası şeridi, başlık çubuğu, koyu ipuçları)
    // Yüksek Karşıtlık'ta Windows'a ve sistem renklerine dönmeli; mod kapanınca geri gelmeli.
    [Fact]
    public void New_drawings_return_to_windows_in_high_contrast() => Sta.Run(() =>
    {
        try
        {
            using var provider = new TailwindProvider { DarkMode = true };
            using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
            var tabs = new TabControl { TabPages = { new TabPage("Genel"), new TabPage("Gelişmiş") } };
            var progress = new ProgressBar { Value = 40 };
            var list = new ListView { View = View.Details, Columns = { "Ad" } };
            var link = new LinkLabel { Text = "Web sitesi" };
            var originalLink = link.LinkColor;
            var button = new Button { Text = "Kaydet", Enabled = false };
            var grid = new PropertyGrid { SelectedObject = new Button() };
            var originalGlyphs = PropertyGridApplier.VisualGlyphs(grid);
            var table = new DataGridView { Columns = { { "ad", "Ad" } } };
            var date = new DateTimePicker();
            form.Controls.AddRange(new Control[] { tabs, progress, list, link, button, grid, table, date });
            form.Show();
            provider.SetClasses(form, "bg-gray-900 text-gray-100");
            provider.SetClasses(tabs, "tab-control");
            provider.SetClasses(progress, "range");
            provider.SetClasses(list, "list");
            provider.SetClasses(link, "link-label");
            provider.SetClasses(button, "btn-ghost");
            provider.SetClasses(grid, "properties");
            provider.SetClasses(table, "table");
            provider.SetClasses(date, "select");

            void AssertFormWindDraws(bool expected)
            {
                Assert.Equal(expected, TabControlApplier.IsPainted(tabs));
                Assert.Equal(expected, ProgressBarApplier.Colors(progress).HasValue);
                Assert.Equal(expected, TwNativeTheme.IsDark(list));
                Assert.Equal(expected, TwNativeTheme.PaintsHeader(list));
                Assert.Equal(expected, link.LinkColor.ToArgb() != originalLink.ToArgb());
                Assert.Equal(expected, PropertyGridApplier.VisualGlyphs(grid) != originalGlyphs);
                Assert.Equal(expected, TwDateTimePickerPainter.IsPainted(date));
                Assert.Equal(expected, TwTitleBar.For(form).Dark);
                Assert.Equal(expected, TwNativeToolTips.IsDark);
            }

            AssertFormWindDraws(true);
            TwHighContrast.Override = true;
            TwHighContrast.NotifyChanged();
            AssertFormWindDraws(false);
            Assert.Equal(SystemColors.GrayText.ToArgb(), TwDisabledText.TextColor(button).ToArgb());
            Assert.Equal(SystemColors.Highlight, TwGridDecorator.TryGet(table)!.RowErrorColor);

            TwHighContrast.Override = false;
            TwHighContrast.NotifyChanged();
            AssertFormWindDraws(true);
            Assert.NotEqual(SystemColors.GrayText.ToArgb(), TwDisabledText.TextColor(button).ToArgb());
            form.Close();
        }
        finally
        {
            TwHighContrast.Override = null;
        }
    });

    // Görsel stiller kapalı ve Yüksek Karşıtlık denemesinde bulundu: zemin class'ı olan standart buton düz stile geçip
    // kenarlığını kaybediyordu; Yüksek Karşıtlık'ta zemin sistem rengine dönünce düz yazıya benziyordu. Sistem yazı renginde
    // kenarlık alır; Yüksek Karşıtlık kapanınca özgün kenarlık döner.
    [Fact]
    public void Flat_standard_buttons_keep_a_border_in_high_contrast() => Sta.Run(() =>
    {
        try
        {
            using var provider = new TailwindProvider();
            using var button = new Button { Text = "Kaydet" };
            provider.SetClasses(button, "btn-primary");
            Assert.Equal(0, button.FlatAppearance.BorderSize);

            TwHighContrast.Override = true;
            TwHighContrast.NotifyChanged();
            Assert.Equal(FlatStyle.Flat, button.FlatStyle);
            Assert.True(button.FlatAppearance.BorderSize >= 1);
            Assert.Equal(SystemColors.ControlText.ToArgb(), button.FlatAppearance.BorderColor.ToArgb());

            TwHighContrast.Override = false;
            TwHighContrast.NotifyChanged();
            Assert.Equal(0, button.FlatAppearance.BorderSize);
            Assert.True(button.FlatAppearance.BorderColor.IsEmpty);
        }
        finally
        {
            TwHighContrast.Override = null;
        }
    });
}
