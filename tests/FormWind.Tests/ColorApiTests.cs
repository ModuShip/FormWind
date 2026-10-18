using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// Kendi çizen kod (CellPainting, OwnerDraw ListView) class renklerini sağlayıcıdan sorar.
[Collection(UiCollection.Name)]
public class ColorApiTests
{
    private static int Argb(string hex) => ColorTranslator.FromHtml(hex).ToArgb();

    [Fact]
    public void Get_colors_resolves_components_parts_and_the_providers_mode()
    {
        using var provider = new TailwindProvider();

        var selected = provider.GetColors("list", "selected");
        Assert.Equal(Argb("#eff6ff"), selected.Back!.Value.ToArgb());                    // primary-50
        Assert.Equal(Argb("#1e3a8a"), selected.Fore!.Value.ToArgb());                    // primary-900
        Assert.Null(selected.Border);

        var hover = provider.GetColors("list", "item", TwState.Hover);
        Assert.Equal(Argb("#f3f4f6"), hover.Back!.Value.ToArgb());                       // surface-100

        provider.DarkMode = true;
        Assert.Equal(Argb("#172554"), provider.GetColors("list", "selected").Back!.Value.ToArgb());   // primary-950
        Assert.Equal(Argb("#ffffff"), provider.GetColors("bg-white text-white", null).Back!.Value.ToArgb());
    }

    [Fact]
    public void Get_current_colors_uses_the_controls_classes_and_state() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var button = new Button();
        var plain = new Button();
        provider.SetClasses(button, "btn-ghost");

        Assert.Equal(Argb("#374151"), provider.GetCurrentColors(button).Fore!.Value.ToArgb());     // surface-700
        button.Enabled = false;
        Assert.Equal(Argb("#6b7280"), provider.GetCurrentColors(button).Fore!.Value.ToArgb());     // disabled:text-surface-500
        Assert.Null(provider.GetCurrentColors(plain).Fore);
        button.Dispose();
        plain.Dispose();
    });
}
