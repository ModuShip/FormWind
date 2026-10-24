using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class GridStep3Tests
{
    public sealed class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public bool Active { get; set; }
    }

    private static int Argb(string hex) => ColorTranslator.FromHtml(hex).ToArgb();

    private static Form HostForm(DataGridView grid)
    {
        var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(420, 260) };
        grid.Dock = DockStyle.Fill;
        form.Controls.Add(grid);
        return form;
    }

    private static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    [Fact]
    public void Sortable_list_sorts_with_culture_and_is_stable()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            var list = new TwSortableBindingList<Person>(new[]
            {
                new Person { Name = "Zeynep", Age = 30 }, new Person { Name = "Çağrı", Age = 25 },
                new Person { Name = "Can", Age = 30 }, new Person { Name = "İpek", Age = 25 }, new Person { Name = "Ilgaz", Age = 40 },
            });
            int resets = 0;
            list.ListChanged += (_, e) => { if (e.ListChangedType == ListChangedType.Reset) resets++; };
            var props = TypeDescriptor.GetProperties(typeof(Person));

            ((IBindingList)list).ApplySort(props["Name"]!, ListSortDirection.Ascending);
            Assert.Equal(new[] { "Can", "Çağrı", "Ilgaz", "İpek", "Zeynep" }, list.Select(p => p.Name));   // Türkçe sıra: C < Ç, I < İ

            ((IBindingList)list).ApplySort(props["Age"]!, ListSortDirection.Descending);
            Assert.Equal(new[] { "Ilgaz", "Can", "Zeynep", "Çağrı", "İpek" }, list.Select(p => p.Name));   // eşit yaşlar önceki sırada
            Assert.True(((IBindingList)list).IsSorted);
            Assert.Equal(2, resets);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Grid_sorts_a_sortable_list_from_the_header_and_draws_the_arrow() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var grid = new DataGridView { AutoGenerateColumns = true, AllowUserToAddRows = false };
        using var form = HostForm(grid);
        grid.DataSource = new TwSortableBindingList<Person>(new[] { new Person { Name = "B", Age = 2 }, new Person { Name = "A", Age = 1 } });
        provider.SetClasses(grid, "table accent-red-600");
        form.Show();
        try
        {
            Assert.Equal(DataGridViewColumnSortMode.Automatic, grid.Columns["Name"]!.SortMode);
            grid.Sort(grid.Columns["Name"]!, ListSortDirection.Ascending);
            Assert.Equal("A", grid.Rows[0].Cells["Name"].Value);
            Assert.Equal(SortOrder.Ascending, grid.Columns["Name"]!.HeaderCell.SortGlyphDirection);

            // Ok, accent rengiyle başlıkta çizilir.
            using var bitmap = Render(grid);
            var header = grid.GetCellDisplayRectangle(grid.Columns["Name"]!.Index, -1, false);
            bool found = false;
            for (int x = header.Left; x < header.Right && !found; x++)
                for (int y = header.Top; y < header.Bottom && !found; y++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    found = pixel.R > 180 && pixel.G < 110 && pixel.B < 110;   // kenar yumuşatmalı kırmızı ok
                }
            Assert.True(found, "Başlıkta accent renkli sıralama oku bulunamadı.");
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Shimmer_sweeps_a_light_band_across_the_skeleton_bars() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var grid = new DataGridView { AllowUserToAddRows = false };
        grid.Columns.Add("Name", "Ad");
        grid.Columns.Add("City", "Şehir");
        using var form = HostForm(grid);
        provider.SetClasses(grid, "table");
        form.Show();
        try
        {
            var body = TwGridDecorator.For(grid).BodyBounds;
            TwGrid.SetLoading(grid, true, TwLoadingStyle.Shimmer);
            Assert.True(TwGrid.IsLoading(grid));
            Assert.Equal(TwLoadingStyle.Shimmer, TwGrid.GetLoadingStyle(grid));

            // Bant başta ızgaranın solunda (görünmez), yarı yolda ortadadır.
            Assert.True(TwGridDecorator.ShimmerCenter(body, 0, 100) < body.Left);
            Assert.Equal(body.Left + body.Width / 2f, TwGridDecorator.ShimmerCenter(body, TwGridDecorator.ShimmerPeriod / 2, 100), 0);
            try
            {
                TwGridDecorator.ShimmerClockForTests = 0;
                using var before = Render(grid);
                TwGridDecorator.ShimmerClockForTests = TwGridDecorator.ShimmerPeriod / 2;
                using var middle = Render(grid);
                int plain = CountColor(before, body, Argb("#e5e7eb"));
                Assert.True(plain > 200, "İskelet satırlar çizilmedi.");
                Assert.True(CountColor(middle, body, Argb("#e5e7eb")) < plain - 50, "Parıltı bandı çubukları aydınlatmadı.");
                Assert.True(CountDifferent(before, middle, body) > 50);
            }
            finally
            {
                TwGridDecorator.ShimmerClockForTests = null;
            }
            TwGrid.SetLoading(grid, false);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Empty_and_loading_states_are_painted() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var grid = new DataGridView { AllowUserToAddRows = false };
        grid.Columns.Add("Name", "Ad");
        grid.Columns.Add("City", "Şehir");
        using var form = HostForm(grid);
        provider.SetClasses(grid, "table");
        form.Show();
        try
        {
            var body = TwGridDecorator.For(grid).BodyBounds;
            using (var plain = Render(grid))
            {
                TwGrid.SetEmptyText(grid, "Kayıt bulunamadı");
                using var empty = Render(grid);
                Assert.True(CountDifferent(plain, empty, body) > 50, "Boş durum metni çizilmedi.");

                TwGrid.SetLoading(grid, true);
                using var loading = Render(grid);
                Assert.True(CountColor(loading, body, Argb("#e5e7eb")) > 200, "İskelet satırlar çizilmedi.");
                Assert.True(TwGrid.IsLoading(grid));
            }

            TwGrid.SetLoading(grid, false);
            grid.Rows.Add("Ada", "İzmir");
            Assert.Equal(1, TwGrid.DataRowCount(grid));
            using var withRow = Render(grid);
            int rowBottom = grid.GetRowDisplayRectangle(0, false).Bottom + 2;
            Assert.Equal(0, CountColor(withRow, Rectangle.FromLTRB(body.Left + 3, rowBottom, body.Right - 3, body.Bottom - 3), Argb("#e5e7eb")));
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Check_and_button_columns_paint_their_own_look() => Sta.Run(() =>
    {
        var grid = new DataGridView { AllowUserToAddRows = false, RowHeadersVisible = false, ColumnHeadersVisible = false };
        var check = new TwCheckColumn { Width = 60, AccentColor = ColorTranslator.FromHtml("#16a34a") };
        var button = new TwButtonColumn { Width = 120, Text = "Sil", UseColumnTextForButtonValue = true, Classes = "rounded-md bg-red-600 text-white" };
        grid.Columns.AddRange(check, button);
        grid.Rows.Add(true, null!);
        grid.Rows.Add(false, null!);
        grid.DefaultCellStyle.BackColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Color.White;
        using var form = HostForm(grid);
        form.Show();
        try
        {
            grid.ClearSelection();
            using var bitmap = Render(grid);
            var checkedCell = grid.GetCellDisplayRectangle(0, 0, false);
            var uncheckedCell = grid.GetCellDisplayRectangle(0, 1, false);
            var buttonCell = grid.GetCellDisplayRectangle(1, 0, false);
            Assert.True(CountColor(bitmap, checkedCell, Argb("#16a34a")) > 30);
            Assert.Equal(0, CountColor(bitmap, uncheckedCell, Argb("#16a34a")));
            Assert.Equal(Argb("#dc2626"), bitmap.GetPixel(buttonCell.X + 8, buttonCell.Y + buttonCell.Height / 2).ToArgb());

            var cell = (TwCheckCell)grid.Rows[0].Cells[0];
            Assert.Equal(CheckState.Checked, cell.StateOf(true));
            Assert.Equal(CheckState.Unchecked, cell.StateOf(null));
            cell.ThreeState = true;
            Assert.Equal(CheckState.Indeterminate, cell.StateOf(null));
        }
        finally
        {
            form.Close();
        }
    });

    private static int CountColor(Bitmap bitmap, Rectangle area, int argb)
    {
        int count = 0;
        area.Intersect(new Rectangle(Point.Empty, bitmap.Size));
        for (int y = area.Top; y < area.Bottom; y++)
            for (int x = area.Left; x < area.Right; x++)
                if (bitmap.GetPixel(x, y).ToArgb() == argb) count++;
        return count;
    }

    private static int CountDifferent(Bitmap a, Bitmap b, Rectangle area)
    {
        int count = 0;
        area.Intersect(new Rectangle(Point.Empty, a.Size));
        for (int y = area.Top; y < area.Bottom; y++)
            for (int x = area.Left; x < area.Right; x++)
                if (a.GetPixel(x, y) != b.GetPixel(x, y)) count++;
        return count;
    }
}
