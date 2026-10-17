using System;
using System.Drawing;
using Xunit;

namespace FormWind.Tests;

public class ThemeJsonTests
{
    private const string Json = @"{
  // yorumlar ve sondaki virgüller kabul edilir
  ""fontFamily"": ""Calibri"",
  ""spacingUnit"": 8,
  ""colors"": {
    ""brand"": { ""500"": ""#2b579a"", ""600"": ""#234a85"" },
    ""surface"": ""#f3f6fb"",
  },
  ""components"": {
    ""btn-primary"": ""px-2 bg-brand-500 hover:bg-brand-600 font-sans"",
  },
}";

    [Fact]
    public void Loads_colors_components_and_settings()
    {
        var theme = TwTheme.FromJson(Json);
        var parsed = theme.Parse("btn-primary bg-surface");
        var style = parsed.Resolve(TwState.None);

        Assert.Empty(parsed.Unknown);
        Assert.Equal(16, style.Padding.Left);
        Assert.Equal("Calibri", style.FontFamily);
        Assert.Equal(ColorTranslator.FromHtml("#f3f6fb").ToArgb(), style.BackColor!.Value.ToArgb());
        Assert.Equal(
            ColorTranslator.FromHtml("#234a85").ToArgb(),
            parsed.Resolve(TwState.Hover).BackColor!.Value.ToArgb());
    }

    [Fact]
    public void Keeps_default_palette_and_components()
    {
        var theme = TwTheme.FromJson(Json);

        Assert.Empty(theme.Parse("bg-blue-600 card").Unknown);
    }

    [Fact]
    public void Invalid_color_throws_format_exception()
    {
        Assert.Throws<FormatException>(() => TwTheme.FromJson(@"{ ""colors"": { ""brand"": ""#zzzzzz"" } }"));
    }

    [Fact]
    public void Invalid_color_name_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new TwTheme().SetColor("light-blue", Color.Blue));
    }

    [Fact]
    public void Accepts_the_current_file_version_and_files_without_one()
    {
        var theme = TwTheme.FromJson(@"{ ""version"": 1, ""colors"": { ""brand"": ""#2b579a"" } }");

        Assert.Empty(theme.Parse("bg-brand").Unknown);
        Assert.Empty(TwTheme.FromJson(@"{ ""colors"": { ""brand"": ""#2b579a"" } }").Parse("bg-brand").Unknown);
    }

    [Fact]
    public void Rejects_a_newer_file_version()
    {
        var error = Assert.Throws<NotSupportedException>(() => TwTheme.FromJson($@"{{ ""version"": {TwTheme.FileVersion + 1} }}"));

        Assert.Contains("update the FormWind package", error.Message);
    }

    [Theory]
    [InlineData(@"""1""")]
    [InlineData("0")]
    [InlineData("1.5")]
    public void Rejects_an_invalid_file_version(string value)
    {
        Assert.Throws<FormatException>(() => TwTheme.FromJson(@"{ ""version"": " + value + " }"));
    }

    [Fact]
    public void Preset_files_declare_the_current_file_version()
    {
        foreach (var preset in new[] { TwPreset.Fluent, TwPreset.Antd, TwPreset.Material })
            Assert.Contains(@"""version"": " + TwTheme.FileVersion, TwPresets.Json(preset));
    }
}
