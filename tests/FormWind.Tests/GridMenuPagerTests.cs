using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class GridMenuPagerTests
{
    public sealed class Order
    {
        public string City { get; set; } = "";
        public string Status { get; set; } = "";
        public int Amount { get; set; }
    }

    private static TwSortableBindingList<Order> Orders() => new TwSortableBindingList<Order>(new[]
    {
        new Order { City = "İzmir", Status = "Kargoda", Amount = 30 },
        new Order { City = "Ankara", Status = "İptal", Amount = 10 },
        new Order { City = "İzmir", Status = "Teslim", Amount = 20 },
        new Order { City = "", Status = "Kargoda", Amount = 40 },
        new Order { City = "Bursa", Status = "Kargoda", Amount = 50 },
    });

    [Fact]
    public void List_filters_by_column_values_and_keeps_sorting()
    {
        var list = Orders();
        Assert.Equal(new[] { TwSortableBindingList<Order>.EmptyValue, "Ankara", "Bursa", "İzmir" }, ((ITwFilterableList)list).DistinctValues("City"));

        list.SetColumnFilter("Status", new[] { "Kargoda" });
        Assert.Equal(3, list.Count);
        ((IBindingList)list).ApplySort(TypeDescriptor.GetProperties(typeof(Order))["Amount"]!, ListSortDirection.Descending);
        Assert.Equal(new[] { 50, 40, 30 }, list.Select(o => o.Amount));

        list.SetColumnFilter("City", new[] { "İzmir", TwSortableBindingList<Order>.EmptyValue });
        Assert.Equal(new[] { 40, 30 }, list.Select(o => o.Amount));
        Assert.True(list.HasFilters);

        list.Add(new Order { City = "Van", Status = "İptal", Amount = 5 });   // eklenen satır görünür ve tüm listeye girer
        Assert.Equal(6, list.AllItems.Count);
        list.ClearFilters();
        Assert.Equal(6, list.Count);
        Assert.Equal(50, list[0].Amount);                                       // sıralama korunur
        list.RemoveAt(0);
        Assert.Equal(5, list.AllItems.Count);
        Assert.Throws<ArgumentException>(() => list.SetColumnFilter("Yok", null));
    }

    [Fact]
    public void Filter_menu_lists_values_and_filters_the_grid() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(500, 300) };
        var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
        form.Controls.Add(grid);
        var list = Orders();
        grid.DataSource = list;
        provider.SetClasses(grid, "table");
        TwGrid.EnableFilterMenu(grid);
        TwGrid.EnableColumnChooser(grid);
        form.Show();
        try
        {
            Assert.False(TwGridDecorator.For(grid).FilterIconBounds(grid.Columns["City"]!.Index).IsEmpty);
            TwGrid.ShowFilterMenu(grid, grid.Columns["City"]!);
            var menu = TwGrid.OpenMenu(grid)!;
            var values = menu.Items.OfType<ToolStripMenuItem>().Where(i => i.Tag is string).ToList();
            Assert.Equal(new[] { "(Boş)", "Ankara", "Bursa", "İzmir" }, values.Select(i => i.Text));   // görünen ad: TwTexts.Blank
            Assert.All(values, i => Assert.True(i.Checked));

            values.First(i => i.Text == "Ankara").Checked = false;
            values.First(i => i.Text == "Bursa").Checked = false;
            Assert.Equal(3, grid.Rows.Count);
            Assert.Equal(new[] { TwSortableBindingList<Order>.EmptyValue, "İzmir" }, list.GetColumnFilter("City")!.OrderBy(v => v, System.StringComparer.Ordinal));
            menu.Close();

            TwGrid.ShowColumnChooser(grid);
            var chooser = TwGrid.OpenMenu(grid)!;
            var columns = chooser.Items.OfType<ToolStripMenuItem>().ToList();
            Assert.Equal(new[] { "City", "Status", "Amount" }, columns.Select(i => i.Text));
            columns[1].Checked = false;
            Assert.False(grid.Columns["Status"]!.Visible);
            columns[0].Checked = false;
            columns[2].Checked = false;                                         // son görünen kolon gizlenemez
            Assert.Equal(1, grid.Columns.GetColumnCount(DataGridViewElementStates.Visible));
            chooser.Close();
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Pager_computes_pages_gaps_and_navigates() => Sta.Run(() =>
    {
        using var pager = new TwPager { Size = new Size(500, 40), TotalItems = 195, PageSize = 10 };
        Assert.Equal(20, pager.PageCount);
        Assert.Equal(new[] { TwPager.Previous, 1, 2, TwPager.Gap, 20, TwPager.Next }, pager.VisiblePages());
        int changes = 0;
        pager.PageChanged += (_, _) => changes++;

        pager.Page = 10;
        Assert.Equal(new[] { TwPager.Previous, 1, TwPager.Gap, 9, 10, 11, TwPager.Gap, 20, TwPager.Next }, pager.VisiblePages());
        pager.Go(TwPager.Next);
        Assert.Equal(11, pager.Page);
        Assert.Equal(100, pager.FirstItemIndex);
        pager.Page = 99;
        Assert.Equal(20, pager.Page);
        Assert.Equal(5, pager.ItemsOnPage);
        pager.Go(TwPager.Next);                                                 // son sayfada devre dışı
        Assert.Equal(20, pager.Page);
        Assert.Equal(3, changes);

        var previous = pager.GetButtonBounds(TwPager.Previous);
        Assert.False(previous.IsEmpty);
        Assert.Equal("Sayfa 20 / 20", pager.AccessibilityObject.Value);
        pager.TotalItems = 15;
        Assert.Equal(2, pager.Page);
    });
}
