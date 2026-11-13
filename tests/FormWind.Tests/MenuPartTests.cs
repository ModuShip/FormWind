using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Menu parts from the 0.1.8 development report: item size from <c>item:h-*</c> / <c>item:py-*</c>, the arrow square of
/// <see cref="TwSelect"/> separate from <c>accent-*</c>, and the menu of a grid select cell following the column's classes.
/// </summary>
[Collection(UiCollection.Name)]
public class MenuPartTests
{
    private static Form HostForm() =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) };

    private static TwSelect StyledSelect(TailwindProvider provider, Form form, string classes)
    {
        var select = new TwSelect { Size = new Size(160, 30) };
        select.Items.AddRange(new[] { "Short", "A considerably longer item" });
        form.Controls.Add(select);
        provider.SetClasses(select, classes);
        form.Show();
        Application.DoEvents();
        return select;
    }

    private static float Scale(Control control) => TwDpi.DeviceDpi(control) / 96f;

    [Fact]
    public void Item_height_class_sets_the_height_of_the_select_menu_items() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = HostForm();
        var select = StyledSelect(provider, form, "border rounded item:h-8");
        try
        {
            select.ShowDropDown();
            var menu = select.DropDownMenu!;
            int expected = (int)Math.Round(32 * Scale(select));
            var items = menu.Items.OfType<ToolStripMenuItem>().ToList();
            Assert.All(items, item => Assert.Equal(expected, item.Height));
            // Öğeler sabit yükseklikte de en uzun metne göre genişler.
            int text = TextRenderer.MeasureText(items[1].Text, menu.Font).Width;
            Assert.True(items[1].Width >= text, $"item width {items[1].Width} < text width {text}");
            menu.Close();
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Item_padding_class_sets_the_padding_of_the_select_menu_items() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = HostForm();
        var select = StyledSelect(provider, form, "border rounded item:py-2");
        try
        {
            select.ShowDropDown();
            int expected = (int)Math.Round(8 * Scale(select));
            var item = select.DropDownMenu!.Items.OfType<ToolStripMenuItem>().First();
            Assert.Equal(expected, item.Padding.Top);
            Assert.Equal(expected, item.Padding.Bottom);
            Assert.True(item.AutoSize);
            select.DropDownMenu.Close();
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Menu_items_keep_their_own_size_without_item_size_classes() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = HostForm();
        var plain = new ContextMenuStrip();
        plain.Items.Add("Plain");
        var padding = plain.Items[0].Padding;
        plain.Dispose();

        var select = StyledSelect(provider, form, "border rounded");
        try
        {
            select.ShowDropDown();
            var item = select.DropDownMenu!.Items.OfType<ToolStripMenuItem>().First();
            Assert.True(item.AutoSize);
            Assert.Equal(padding, item.Padding);
            select.DropDownMenu.Close();
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Menu_items_get_their_own_padding_back_when_the_size_classes_are_removed() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var menu = new ContextMenuStrip();
        menu.Items.Add("First");
        var original = menu.Items[0].Padding;

        provider.SetClasses(menu, "bg-white item:py-3");
        Assert.NotEqual(original, menu.Items[0].Padding);

        provider.SetClasses(menu, "");
        Assert.Equal(original, menu.Items[0].Padding);
    });

    [Theory]
    [InlineData("accent-blue-600", "#2563eb")]
    [InlineData("accent-blue-600 arrow:bg-red-500", "#ef4444")]
    [InlineData("accent-blue-600 arrow:bg-transparent", null)]
    [InlineData("border", null)]
    public void Arrow_square_comes_from_arrow_part_before_accent(string classes, string? expected) => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = HostForm();
        var select = StyledSelect(provider, form, classes);
        try
        {
            if (expected is null) Assert.True(select.ArrowBackColor.IsEmpty, $"arrow square {select.ArrowBackColor}");
            else Assert.Equal(ColorTranslator.FromHtml(expected).ToArgb(), select.ArrowBackColor.ToArgb());
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Arrow_part_leaves_accent_for_the_menu_check() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = HostForm();
        var select = StyledSelect(provider, form, "accent-blue-600 arrow:bg-transparent");
        try
        {
            Assert.Equal(ColorTranslator.FromHtml("#2563eb").ToArgb(), select.MenuColors.Accent.ToArgb());
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Grid_select_menu_follows_the_column_classes() => Sta.Run(() =>
    {
        using var grid = new DataGridView();
        var colors = TwInputCell.MenuColors(grid, "menu:bg-red-500 item:hover:bg-red-700 item:h-9");
        Assert.Equal(ColorTranslator.FromHtml("#ef4444").ToArgb(), colors.MenuBack.ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#b91c1c").ToArgb(), colors.ItemHoverBack.ToArgb());
        Assert.Equal((int)Math.Round(36 * TwCellPainting.Scale(grid)), colors.ItemHeight);
        Assert.Null(colors.BottomBorder);
    });

    [Fact]
    public void Grid_select_menu_matches_the_input_box_in_dark_mode() => Sta.Run(() =>
    {
        using var grid = new DataGridView();
        TwGridState.SetDark(grid, true);
        var box = TwTheme.Current.Parse("input").Resolve(TwState.Dark);
        var colors = TwInputCell.MenuColors(grid, "");
        Assert.NotNull(box.BackColor);
        Assert.Equal(box.BackColor!.Value.ToArgb(), colors.MenuBack.ToArgb());
    });
}
