using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Pencere handle'ı oluşturan (DrawToBitmap) testler. WinForms handle'ları STA iş parçacığı bekler ve
/// paralel testlerle aynı anda oluşturulduğunda diğer testleri etkileyebilir; bu yüzden sırayla, STA'da çalışırlar.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UiCollection
{
    public const string Name = "UI";
}

[Collection(UiCollection.Name)]
public class FontAndIconTests
{
    private readonly TwTheme _theme = new TwTheme();

    private static readonly Func<string, bool> AllInstalled = _ => true;

    [Theory]
    [InlineData("font-thin", 100)]
    [InlineData("font-light", 300)]
    [InlineData("font-normal", 400)]
    [InlineData("font-medium", 500)]
    [InlineData("font-semibold", 600)]
    [InlineData("font-bold", 700)]
    [InlineData("font-black", 900)]
    public void Parses_font_weights(string classes, int expected) =>
        Assert.Equal(expected, _theme.Parse(classes).Resolve(TwState.None).FontWeight);

    [Fact]
    public void Font_cache_shares_instances()
    {
        int before = TwFonts.CachedCount;
        var a = TwFonts.Get("Segoe UI", 10.5f, FontStyle.Regular);
        Assert.Same(a, TwFonts.Get("Segoe UI", 10.501f, FontStyle.Regular));
        Assert.True(TwFonts.CachedCount <= before + 1);
    }

    [Theory]
    [InlineData(600, "Segoe UI Semibold", false)] // birebir
    [InlineData(700, "Segoe UI", true)]           // ailenin kendisi + kalın
    [InlineData(500, "Segoe UI", false)]          // 400–500: yukarıda 500'e kadar yok → aşağı (400)
    [InlineData(800, "Segoe UI Black", false)]    // > 500: önce daha kalın
    [InlineData(100, "Segoe UI Light", false)]    // < 400: aşağıda yok → yukarı (300)
    [InlineData(200, "Segoe UI Light", false)]
    public void Resolves_weights_with_css_fallback(int weight, string face, bool bold)
    {
        Assert.Equal((face, bold), _theme.ResolveFontWeight("Segoe UI", weight, AllInstalled));
    }

    [Fact]
    public void Missing_weight_faces_fall_back_to_regular_or_bold()
    {
        Func<string, bool> noneInstalled = _ => false;

        Assert.Equal(("Segoe UI", false), _theme.ResolveFontWeight("Segoe UI", 300, noneInstalled));
        Assert.Equal(("Segoe UI", true), _theme.ResolveFontWeight("Segoe UI", 600, noneInstalled));
    }

    [Fact]
    public void Unknown_family_uses_synthetic_bold()
    {
        Assert.Equal(("Tahoma", true), _theme.ResolveFontWeight("Tahoma", 800, AllInstalled));
        Assert.Equal(("Tahoma", false), _theme.ResolveFontWeight("Tahoma", 300, AllInstalled));
    }

    [Fact]
    public void Theme_json_adds_weights_and_icons()
    {
        var theme = TwTheme.FromJson(@"{
            ""fontWeights"": { ""Inter"": { ""600"": ""Inter SemiBold"" } },
            ""iconFont"": ""My Icons"",
            ""icons"": { ""invoice"": ""E8A5"" }
        }");

        Assert.Equal(("Inter SemiBold", false), theme.ResolveFontWeight("Inter", 600, AllInstalled));
        Assert.Equal("My Icons", theme.IconFontFamily);
        Assert.True(theme.TryGetIcon("invoice", out var glyph));
        Assert.Equal('', glyph);
    }

    [Fact]
    public void Invalid_icon_code_point_throws()
    {
        Assert.Throws<FormatException>(() => TwTheme.FromJson(@"{ ""icons"": { ""x"": ""zz"" } }"));
    }

    [Theory]
    [InlineData("plus", '')]
    [InlineData("PLUS", '')]
    [InlineData("#E74D", '')]
    public void Resolves_icon_names_and_code_points(string name, char expected)
    {
        Assert.True(_theme.TryGetIcon(name, out var glyph));
        Assert.Equal(expected, glyph);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-icon")]
    [InlineData("#XYZ")]
    [InlineData("#1F600")] // BMP dışı
    public void Rejects_unknown_icons(string name) => Assert.False(_theme.TryGetIcon(name, out _));

    [Fact]
    public void Icon_names_are_unique_and_sorted()
    {
        var names = TwIcons.Names.ToList();
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal), names);
        Assert.True(names.Count >= 70);
    }

    [Fact]
    public void Semibold_class_uses_the_semibold_face_when_installed()
    {
        if (!TwFonts.IsInstalled("Segoe UI Semibold")) return; // Windows 10+ dışında atla

        using var label = new Label { Font = new Font("Segoe UI", 9f) };
        using var provider = new TailwindProvider();
        provider.SetClasses(label, "font-semibold");

        Assert.Equal("Segoe UI Semibold", label.Font.FontFamily.Name);
        Assert.False(label.Font.Bold);
    }

    [Fact]
    public void TwIcon_sizes_itself_from_font_and_draws_the_glyph() => Sta.Run(() =>
    {
        if (!TwFonts.IsInstalled(TwIcons.DefaultFontFamily)) return;

        using var small = new TwIcon { IconName = "search", Font = new Font("Segoe UI", 9f) };
        using var large = new TwIcon { IconName = "search", Font = new Font("Segoe UI", 18f) };
        var smallSize = small.GetPreferredSize(Size.Empty);
        var largeSize = large.GetPreferredSize(Size.Empty);

        Assert.True(smallSize.Width > 0 && smallSize.Height > 0);
        Assert.True(largeSize.Width > smallSize.Width);
        Assert.True(HasInk(large, Color.Black));
    });

    [Fact]
    public void TwButton_draws_icon_next_to_text() => Sta.Run(() =>
    {
        if (!TwFonts.IsInstalled(TwIcons.DefaultFontFamily)) return;

        using var plain = new TwButton { Text = "Kaydet", Size = new Size(160, 40), BackColor = Color.White, ForeColor = Color.Black };
        using var withIcon = new TwButton { Text = "Kaydet", Size = new Size(160, 40), BackColor = Color.White, ForeColor = Color.Black, IconName = "save" };

        // İkonlu buton, metnin solunda ek mürekkep içerir: en soldaki koyu pikselin konumu sola kayar.
        Assert.True(LeftmostInk(withIcon) < LeftmostInk(plain) - 8);
    });

    private static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    private static bool HasInk(Control control, Color ink)
    {
        control.Size = control.GetPreferredSize(Size.Empty);
        control.ForeColor = ink;
        control.BackColor = Color.White;
        using var bitmap = Render(control);
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
                if (bitmap.GetPixel(x, y).GetBrightness() < 0.5f) return true;
        return false;
    }

    private static int LeftmostInk(Control control)
    {
        using var bitmap = Render(control);
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
                if (bitmap.GetPixel(x, y).GetBrightness() < 0.5f) return x;
        return int.MaxValue;
    }
}
