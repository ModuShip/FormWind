using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class VirtualRowsTests
{
    private sealed class Order
    {
        public int Id { get; set; }
        public string Customer { get; set; } = "";
    }

    private static DataGridView NewGrid()
    {
        var grid = new DataGridView();
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "no", DataPropertyName = "Id" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Customer" });
        return grid;
    }

    [Fact]
    public void Loads_only_the_pages_that_are_read_and_caches_them() => Sta.Run(() =>
    {
        using var grid = NewGrid();
        var requests = new List<(int Start, int Count)>();
        using var rows = new TwVirtualRows<Order>(grid, (start, count) =>
        {
            requests.Add((start, count));
            return Enumerable.Range(start, count).Select(i => new Order { Id = i, Customer = "Müşteri " + i }).ToList();
        }, pageSize: 50, cachedPages: 2);
        rows.Reset(1_000_000);

        Assert.True(grid.VirtualMode);
        Assert.Equal(1_000_000, grid.RowCount);
        Assert.Equal(123_456, grid[0, 123_456].Value);                  // DataPropertyName
        Assert.Equal("Müşteri 123457", grid[1, 123_457].Value);         // kolon adı
        Assert.Equal((123_450, 50), Assert.Single(requests));

        Assert.Equal(999_999, grid[0, 999_999].Value);                  // son sayfa
        Assert.Equal(123_499, grid[0, 123_499].Value);                  // önbellekten
        Assert.Equal(2, rows.PagesLoaded);

        Assert.Equal(0, grid[0, 0].Value);                              // üçüncü sayfa: en eski (999 950) atılır
        Assert.Equal(123_400 + 50, grid[0, 123_450].Value);
        Assert.Equal(3, rows.PagesLoaded);
        Assert.Equal(999_999, grid[0, 999_999].Value);
        Assert.Equal(4, rows.PagesLoaded);
    });

    [Fact]
    public void Cell_value_and_reset() => Sta.Run(() =>
    {
        using var grid = NewGrid();
        int version = 1;
        using var rows = new TwVirtualRows<Order>(grid, (start, count) =>
            Enumerable.Range(start, count).Select(i => new Order { Id = i * version }).ToList(), pageSize: 10)
        {
            CellValue = (order, column) => column.Name == "no" ? "#" + order.Id : null,
        };
        rows.Reset(25);

        Assert.Equal("#7", grid[0, 7].Value);
        Assert.Null(grid[1, 7].Value);
        Assert.Equal(24, rows.GetItem(24)!.Id);
        Assert.Null(rows.GetItem(25));

        version = 2;
        rows.Reset(5);                                                  // önbellek boşalır
        Assert.Equal(5, grid.RowCount);
        Assert.Equal("#8", grid[0, 4].Value);
    });
}
