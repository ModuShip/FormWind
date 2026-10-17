using System.Drawing;
using Xunit;

namespace FormWind.Tests;

public class PaletteV4Tests
{
    private static string Hex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    [Theory]
    [InlineData("oklch(100% 0 0)", "#ffffff")]
    [InlineData("oklch(0% 0 0)", "#000000")]
    [InlineData("oklch(62.8% 0.2577 29.23)", "#ff0000")]     // sRGB kırmızısı
    [InlineData("oklch(0.866 0.2948 142.5)", "#00ff00")]       // sayı biçiminde açıklık
    [InlineData("oklch(45.2% 0.313 264.05)", "#0000ff")]
    public void Converts_reference_colors(string css, string hex)
    {
        Assert.True(TwOklch.TryParse(css, out var color));
        Assert.Equal(hex, Hex(color));
    }

    // sRGB'ye sığmayan v4 renkleri tarayıcılardaki gibi gamut eşlemesiyle yaklaştırılır (yayımlanan hex karşılıkları).
    [Theory]
    [InlineData("red", "#fb2c36")]
    [InlineData("green", "#00c950")]
    [InlineData("blue", "#2b7fff")]
    [InlineData("gray", "#6a7282")]
    public void V4_shade_500_matches_browser_rendering(string name, string hex)
    {
        new TwTheme(TwPaletteVersion.V4).TryGetColor(name, "500", out var color);
        Assert.Equal(hex, Hex(color));
    }

    [Fact]
    public void Parses_alpha_and_rejects_garbage()
    {
        Assert.True(TwOklch.TryParse("oklch(62.8% 0.2577 29.23 / 50%)", out var half));
        Assert.Equal(128, half.A);
        Assert.False(TwOklch.TryParse("oklch(red)", out _));
        Assert.False(TwOklch.TryParse("#ff0000", out _));
    }

    [Fact]
    public void V4_palette_is_complete_and_differs_from_v3()
    {
        var v3 = new TwTheme();
        var v4 = new TwTheme(TwPaletteVersion.V4);
        Assert.Equal(TwPaletteVersion.V4, v4.Palette);
        foreach (var pair in TwPaletteV4.Colors)
            foreach (var shade in TwPalette.Shades)
                Assert.True(v4.TryGetColor(pair.Key, shade, out _), pair.Key + "-" + shade);

        v3.TryGetColor("blue", "500", out var blue3);
        v4.TryGetColor("blue", "500", out var blue4);
        Assert.Equal("#3b82f6", Hex(blue3));
        Assert.NotEqual(Hex(blue3), Hex(blue4));
        Assert.Equal(blue4, v4.Parse("bg-blue-500").Resolve(TwState.None).BackColor);
    }

    [Fact]
    public void Theme_file_selects_the_palette_and_accepts_oklch()
    {
        var theme = TwTheme.FromJson(@"{ ""palette"": ""v4"", ""colors"": { ""brand"": ""oklch(62.8% 0.2577 29.23)"" } }");
        Assert.Equal(TwPaletteVersion.V4, theme.Palette);
        Assert.Equal("#ff0000", Hex(theme.Parse("bg-brand").Resolve(TwState.None).BackColor!.Value));
        Assert.Throws<System.FormatException>(() => TwTheme.FromJson(@"{ ""palette"": ""v5"" }"));
    }
}
