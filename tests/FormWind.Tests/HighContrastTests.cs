using System.Drawing;
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
}
