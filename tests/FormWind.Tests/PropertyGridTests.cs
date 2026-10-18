using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class PropertyGridTests
{
    private static Color Color(string hex) => ColorTranslator.FromHtml(hex);

    [Fact]
    public void Classes_set_the_grids_own_color_properties_and_removing_them_restores_the_originals() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var grid = new PropertyGrid();
        var originalView = grid.ViewBackColor;
        var originalLine = grid.LineColor;
        var originalSelected = grid.SelectedItemWithFocusBackColor;

        provider.SetClasses(grid, "bg-gray-800 text-gray-100 divide-gray-700 header:bg-gray-900 header:text-gray-300 selected:bg-blue-600 selected:text-white");

        Assert.Equal(Color("#1f2937").ToArgb(), grid.ViewBackColor.ToArgb());
        Assert.Equal(Color("#1f2937").ToArgb(), grid.HelpBackColor.ToArgb());
        Assert.Equal(Color("#1f2937").ToArgb(), grid.CommandsBackColor.ToArgb());
        Assert.Equal(Color("#f3f4f6").ToArgb(), grid.ViewForeColor.ToArgb());
        Assert.Equal(Color("#f3f4f6").ToArgb(), grid.HelpForeColor.ToArgb());
        Assert.Equal(Color("#111827").ToArgb(), grid.LineColor.ToArgb());                 // kategori satırları
        Assert.Equal(Color("#d1d5db").ToArgb(), grid.CategoryForeColor.ToArgb());
        Assert.Equal(Color("#374151").ToArgb(), grid.ViewBorderColor.ToArgb());
        Assert.Equal(Color("#374151").ToArgb(), grid.CategorySplitterColor.ToArgb());
        Assert.Equal(Color("#2563eb").ToArgb(), grid.SelectedItemWithFocusBackColor.ToArgb());
        Assert.Equal(System.Drawing.Color.White.ToArgb(), grid.SelectedItemWithFocusForeColor.ToArgb());
        // Salt okunur özellikler: yazı ile zeminin ortası.
        Assert.Equal(System.Drawing.Color.FromArgb((0xf3 + 0x1f) / 2, (0xf4 + 0x29) / 2, (0xf6 + 0x37) / 2).ToArgb(), grid.DisabledItemForeColor.ToArgb());

        provider.SetClasses(grid, "");
        Assert.Equal(originalView.ToArgb(), grid.ViewBackColor.ToArgb());
        Assert.Equal(originalLine.ToArgb(), grid.LineColor.ToArgb());
        Assert.Equal(originalSelected.ToArgb(), grid.SelectedItemWithFocusBackColor.ToArgb());
    });

    [Fact]
    public void Auto_style_gives_property_grids_the_properties_component_and_dark_mode_follows() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        var grid = new PropertyGrid { SelectedObject = new Button() };
        form.Controls.Add(grid);
        provider.ContainerControl = form;
        provider.AutoStyle = true;

        Assert.Equal("properties", provider.GetAutomaticClasses(grid));
        Assert.Equal(Color("#ffffff").ToArgb(), grid.ViewBackColor.ToArgb());

        provider.DarkMode = true;
        Assert.Equal(Color("#1f2937").ToArgb(), grid.ViewBackColor.ToArgb());           // surface-800
        Assert.Equal(Color("#111827").ToArgb(), grid.LineColor.ToArgb());               // surface-900
    });
}
