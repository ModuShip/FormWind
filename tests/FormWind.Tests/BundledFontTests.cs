using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>Sistemde kurulu olmayan Outfit fontu (SIL OFL 1.1) ile uygulamayla gelen font testleri.</summary>
[Collection(UiCollection.Name)]
public class BundledFontTests
{
    private static string FontPath(string file) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", file);

    [Fact]
    public void Register_makes_an_uninstalled_font_usable()
    {
        var names = TwFonts.Register(FontPath("Outfit-Regular.ttf"));

        Assert.Contains("Outfit", names);
        Assert.True(TwFonts.IsInstalled("Outfit"));
        var font = TwFonts.Get("Outfit", 12f, FontStyle.Regular);
        Assert.Equal("Outfit", font.FontFamily.Name);
    }

    [Fact]
    public void Font_stack_picks_the_first_available_family()
    {
        TwFonts.Register(FontPath("Outfit-Regular.ttf"));

        Assert.Equal("Outfit", TwFonts.ResolveFamily("Not A Font, 'Outfit', Segoe UI"));
        Assert.Equal("Segoe UI", TwFonts.ResolveFamily("Not A Font, Segoe UI"));
        Assert.Equal(TwFonts.DefaultFamily, TwFonts.ResolveFamily("Nope1, Nope2"));
    }

    [Fact]
    public void Theme_json_registers_fonts_relative_to_its_folder_and_maps_weights()
    {
        var folder = AppDomain.CurrentDomain.BaseDirectory;
        var theme = new TwTheme();
        theme.ApplyJson(@"{
            ""fonts"": [""Fonts/Outfit-Regular.ttf"", ""Fonts/Outfit-SemiBold.ttf"", ""Fonts/Missing.ttf""],
            ""fontFamily"": ""Outfit, Segoe UI"",
            ""fontWeights"": { ""Outfit"": { ""600"": ""Outfit SemiBold"" } }
        }", folder);

        var style = theme.Parse("font-sans font-semibold").Resolve(TwState.None);
        var font = TwFonts.Create(style, new Font("Segoe UI", 9f), theme);

        Assert.True(TwFonts.IsInstalled("Outfit SemiBold"));
        Assert.Equal("Outfit SemiBold", font.FontFamily.Name);
    }

    [Fact]
    public void Gdi_text_rendering_uses_the_registered_font() => Sta.Run(() =>
    {
        TwFonts.Register(FontPath("Outfit-Regular.ttf"));
        var outfit = TwFonts.Get("Outfit", 24f, FontStyle.Regular);
        var segoe = TwFonts.Get("Segoe UI", 24f, FontStyle.Regular);

        // TextRenderer GDI kullanır: font GDI'ye de kaydedilmediyse yedek fonta (Microsoft Sans Serif) düşerdi.
        // "a" Outfit'te belirgin biçimde geniştir (24 pt'de 10 harf: Outfit ~207 px, Segoe UI ~180 px).
        var outfitWidth = TextRenderer.MeasureText("aaaaaaaaaa", outfit).Width;
        var segoeWidth = TextRenderer.MeasureText("aaaaaaaaaa", segoe).Width;
        var fallbackWidth = TextRenderer.MeasureText("aaaaaaaaaa", new Font("Microsoft Sans Serif", 24f)).Width;
        Assert.True(outfitWidth > segoeWidth + 10, $"Outfit {outfitWidth}, Segoe UI {segoeWidth}");
        Assert.NotEqual(fallbackWidth, outfitWidth);
    });
}
