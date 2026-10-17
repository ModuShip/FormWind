using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// Arama, aralık süzmesi, gruplar, alt toplam, yeni hücre tipleri, satır detayları, düzen, dışa aktarma, yoğunluk ve
// koyu tema çevirmesi.
[Collection(UiCollection.Name)]
public class GridFeatureTests
{
    public sealed class Order
    {
        public string Customer { get; set; } = "";
        public string City { get; set; } = "";
        public int Amount { get; set; }
        public DateTime Date { get; set; }
    }

    private static TwSortableBindingList<Order> Orders() => new TwSortableBindingList<Order>(new[]
    {
        new Order { Customer = "Ayşe Yılmaz", City = "İzmir", Amount = 30, Date = new DateTime(2025, 3, 1) },
        new Order { Customer = "Mehmet Kaya", City = "Ankara", Amount = 10, Date = new DateTime(2025, 1, 15) },
        new Order { Customer = "Zeynep Ak", City = "İzmir", Amount = 20, Date = new DateTime(2025, 2, 10) },
        new Order { Customer = "Ali Demir", City = "Bursa", Amount = -5, Date = new DateTime(2024, 12, 31, 18, 0, 0) },
        new Order { Customer = "Can Öz", City = "Ankara", Amount = 50, Date = new DateTime(2025, 1, 31) },
    });

    private static Form NewForm() =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(700, 400) };

    private static void WithCulture(string name, Action action)
    {
        var culture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo(name);
        try
        {
            action();
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culture;
        }
    }

    private static (Form Form, DataGridView Grid, TwSortableBindingList<Order> List) BoundGrid(TailwindProvider provider)
    {
        var form = NewForm();
        var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
        form.Controls.Add(grid);
        var list = Orders();
        grid.DataSource = list;
        provider.SetClasses(grid, "table");
        form.Show();
        return (form, grid, list);
    }

    [Fact]
    public void List_searches_filters_ranges_and_groups() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        var list = Orders();
        list.SetSearch("İZMİR", new[] { "City" });                           // Türkçe büyük/küçük harf
        Assert.Equal(2, list.Count);
        list.SetSearch("bur", null);                                         // tüm özellikler
        Assert.Equal(new[] { "Ali Demir" }, list.Select(o => o.Customer));
        list.SetSearch(null);
        Assert.Equal(5, list.Count);

        list.SetRangeFilter("Amount", "10", 30);
        Assert.Equal(new[] { 30, 10, 20 }, list.Select(o => o.Amount));
        Assert.True(list.TryGetRangeFilter("Amount", out var min, out var max));
        Assert.Equal(10, min);
        Assert.Equal(30, max);
        Assert.True(list.HasFilters);
        list.SetRangeFilter("Amount", null, null);
        Assert.False(list.HasFilters);
        Assert.Throws<ArgumentException>(() => list.SetRangeFilter("Yok", 1, 2));

        ((IBindingList)list).ApplySort(TypeDescriptor.GetProperties(typeof(Order))["Amount"]!, ListSortDirection.Descending);
        list.GroupProperty = "City";
        Assert.Equal(new[] { "Ankara", "Ankara", "Bursa", "İzmir", "İzmir" }, list.Select(o => o.City));
        Assert.Equal(new[] { 50, 10 }, list.Take(2).Select(o => o.Amount));   // grup içinde sıralama korunur
        list.GroupProperty = null;
        Assert.Equal(50, list[0].Amount);
        Assert.Equal(typeof(int), list.GetPropertyType("Amount"));
    }));

    [Fact]
    public void Filter_menu_gets_search_and_range_boxes() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var provider = new TailwindProvider();
        var (form, grid, list) = BoundGrid(provider);
        try
        {
            TwGrid.EnableFilterMenu(grid);
            TwGrid.ShowFilterMenu(grid, grid.Columns["Amount"]!);
            var menu = TwGrid.OpenMenu(grid)!;
            var min = menu.Items.OfType<ToolStripTextBox>().Single(i => i.Name == "min");
            var max = menu.Items.OfType<ToolStripTextBox>().Single(i => i.Name == "max");
            Assert.DoesNotContain(menu.Items.OfType<ToolStripTextBox>(), i => i.Name == "search");   // 5 değer: arama yok
            min.Text = "15";
            Assert.Equal(3, grid.Rows.Count);
            max.Text = "abc";                                                // geçersiz: önceki süzme kalır
            Assert.Equal(3, grid.Rows.Count);
            max.Text = "25";
            Assert.Equal(new[] { 20 }, list.Select(o => o.Amount));
            menu.Close();

            // Tarih: üst sınır günün tamamını kapsar.
            TwGrid.ShowFilterMenu(grid, grid.Columns["Date"]!);
            menu = TwGrid.OpenMenu(grid)!;
            menu.Items.OfType<ToolStripMenuItem>().Last().PerformClick();   // süzmeyi kaldır
            list.SetRangeFilter("Amount", null, null);
            TwGrid.ShowFilterMenu(grid, grid.Columns["Date"]!);
            menu = TwGrid.OpenMenu(grid)!;
            menu.Items.OfType<ToolStripTextBox>().Single(i => i.Name == "max").Text = "31.12.2024";
            Assert.Equal(new[] { "Ali Demir" }, list.Select(o => o.Customer));
            menu.Close();
            list.ClearFilters();

            // Çok değerli kolonda arama kutusu: eşleşmeyen değerler gizlenir, "tümünü seç" yalnızca görünenlere uygulanır.
            for (int i = 0; i < 8; i++) list.Add(new Order { Customer = "Müşteri " + i, City = "Şehir " + i, Amount = i });
            TwGrid.ShowFilterMenu(grid, grid.Columns["City"]!);
            menu = TwGrid.OpenMenu(grid)!;
            var search = menu.Items.OfType<ToolStripTextBox>().Single(i => i.Name == "search");
            search.Text = "şehir";
            var values = menu.Items.OfType<ToolStripMenuItem>().Where(i => i.Tag is string).ToList();
            Assert.Equal(8, values.Count(i => i.Available));
            menu.Items.OfType<ToolStripMenuItem>().First(i => i.Tag is null).PerformClick();   // tümünü seç → kaldır
            Assert.Equal(new[] { "Ankara", "Bursa", "İzmir" }, list.GetColumnFilter("City")!.OrderBy(v => v, StringComparer.Ordinal));
            menu.Close();
        }
        finally
        {
            form.Close();
        }
    }));

    [Fact]
    public void Search_filters_bound_and_unbound_grids_and_highlights() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var (form, grid, list) = BoundGrid(provider);
        try
        {
            TwGrid.SetSearchText(grid, "ankara");
            Assert.Equal(2, grid.Rows.Count);
            Assert.Equal("ankara", TwGrid.GetSearchText(grid));
            grid.Columns["City"]!.Visible = false;                           // gizli kolonda aranmaz
            TwGrid.SetSearchText(grid, "ankara");
            Assert.Equal(0, grid.Rows.Count);
            grid.Columns["City"]!.Visible = true;
            TwGrid.SetSearchText(grid, "ankara");
            using (var bitmap = new Bitmap(grid.Width, grid.Height)) grid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, grid.Size));
            TwGrid.SetSearchText(grid, null);
            Assert.Equal(5, list.Count);

            // Bağlı olmayan grid: eşleşmeyen satırlar gizlenir, arama bitince geri gelir.
            var unbound = new DataGridView { AllowUserToAddRows = false, Size = new Size(400, 200) };
            form.Controls.Add(unbound);
            unbound.Columns.Add("name", "Ad");
            unbound.Rows.Add("Ayşe");
            unbound.Rows.Add("Mehmet");
            unbound.Rows.Add("Ayten");
            TwGrid.SetSearchText(unbound, "ay");
            Assert.Equal(new[] { true, false, true }, unbound.Rows.Cast<DataGridViewRow>().Select(r => r.Visible));
            TwGrid.SetSearchText(unbound, "");
            Assert.All(unbound.Rows.Cast<DataGridViewRow>(), r => Assert.True(r.Visible));

            // Arama kutusu bağlantısı: yazılan metin gecikmeyle uygulanır, Escape kutuyu temizler.
            var box = new TextBox();
            form.Controls.Add(box);
            using (TwGrid.AttachSearchBox(unbound, box, delay: 1))
            {
                box.Text = "meh";
                var until = DateTime.Now.AddSeconds(2);
                while (TwGrid.GetSearchText(unbound) != "meh" && DateTime.Now < until) Application.DoEvents();
                Assert.Equal("meh", TwGrid.GetSearchText(unbound));
            }
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Grouping_adds_headers_and_collapses_groups() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var (form, grid, list) = BoundGrid(provider);
        try
        {
            int normal = grid.RowTemplate.Height;
            TwGrid.SetGroupBy(grid, grid.Columns["City"]);
            Assert.Equal("City", list.GroupProperty);
            var groups = TwGrid.GetGroups(grid);
            Assert.Equal(new[] { "Ankara", "Bursa", "İzmir" }, groups.Select(g => g.Key));
            Assert.Equal(new[] { 2, 1, 2 }, groups.Select(g => g.RowCount));
            Assert.Equal(new[] { 0, 2, 3 }, groups.Select(g => g.FirstRow));

            var decorator = TwGridDecorator.For(grid);
            int header = decorator.GroupHeaderHeight;
            Assert.Equal(normal + header, grid.Rows[0].Height);              // başlık grubun ilk satırına eklenir
            Assert.Equal(normal, grid.Rows[1].Height);

            TwGrid.SetGroupExpanded(grid, "Ankara", false);
            Assert.False(grid.Rows[1].Visible);
            Assert.Equal(header, grid.Rows[0].Height);                       // kapalı grup yalnızca başlığını gösterir
            Assert.False(TwGrid.GetGroups(grid)[0].Expanded);
            TwGrid.ExpandAllGroups(grid);
            Assert.True(grid.Rows[1].Visible);
            TwGrid.CollapseAllGroups(grid);
            Assert.All(TwGrid.GetGroups(grid), g => Assert.False(g.Expanded));
            TwGrid.ExpandAllGroups(grid);

            TwGrid.SetGroupText(grid, g => g.Key + " – " + g.RowCount);
            using (var bitmap = new Bitmap(grid.Width, grid.Height)) grid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, grid.Size));

            // Eklenen satır kendi grubunun sonuna girer; gruplar yeniden hesaplanır.
            list.Add(new Order { Customer = "Ece", City = "Bursa", Amount = 1 });
            decorator.RefreshRows();
            Assert.Equal(new[] { 2, 2, 2 }, TwGrid.GetGroups(grid).Select(g => g.RowCount));

            TwGrid.SetGroupBy(grid, null);
            Assert.Null(list.GroupProperty);
            Assert.Empty(TwGrid.GetGroups(grid));
            Assert.Equal(normal, grid.Rows[0].Height);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Unbound_grid_groups_consecutive_rows() => Sta.Run(() =>
    {
        using var grid = new DataGridView { AllowUserToAddRows = false };
        grid.Columns.Add("city", "Şehir");
        grid.Columns.Add("name", "Ad");
        grid.Rows.Add("Ankara", "A");
        grid.Rows.Add("Ankara", "B");
        grid.Rows.Add("Bursa", "C");
        TwGrid.SetGroupBy(grid, grid.Columns["city"]);
        Assert.Equal(new[] { "Ankara", "Bursa" }, TwGrid.GetGroups(grid).Select(g => g.Key));
        Assert.True(grid.Rows[0].Height > grid.Rows[1].Height);
        TwGrid.SetGroupExpanded(grid, "Ankara", false);
        Assert.False(grid.Rows[1].Visible);
        Assert.Throws<ArgumentException>(() => TwGrid.SetGroupBy(grid, new DataGridViewTextBoxColumn()));
    });

    [Fact]
    public void Footer_aggregates_follow_the_rows() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var provider = new TailwindProvider();
        var (form, grid, list) = BoundGrid(provider);
        try
        {
            var footer = TwGrid.AddFooter(grid);
            Assert.Same(form, footer.Parent);
            Assert.Equal(DockStyle.Bottom, footer.Dock);
            footer.SetText(grid.Columns["Customer"]!, "Toplam");
            footer.SetAggregate(grid.Columns["Amount"]!, TwAggregate.Sum, "N0");
            footer.SetAggregate(grid.Columns["City"]!, TwAggregate.Count);
            footer.SetAggregate(grid.Columns["Date"]!, TwAggregate.Max, "d");
            Assert.Equal("Toplam", footer.GetText(grid.Columns["Customer"]!));
            Assert.Equal("105", footer.GetText(grid.Columns["Amount"]!));
            Assert.Equal("5", footer.GetText(grid.Columns["City"]!));
            Assert.Equal("1.03.2025", footer.GetText(grid.Columns["Date"]!));

            footer.SetAggregate(grid.Columns["Amount"]!, TwAggregate.Average, "N1");
            Assert.Equal("21,0", footer.GetText(grid.Columns["Amount"]!));
            footer.SetAggregate(grid.Columns["Amount"]!, TwAggregate.Min);
            Assert.Equal("-5", footer.GetText(grid.Columns["Amount"]!));
            footer.SetValue(grid.Columns["Amount"]!, values => values.Count + " satır");
            Assert.Equal("5 satır", footer.GetText(grid.Columns["Amount"]!));

            footer.SetAggregate(grid.Columns["Amount"]!, TwAggregate.Sum);
            list.SetColumnFilter("City", new[] { "İzmir" });                  // yalnızca gösterilen satırlar
            Assert.Equal("50", footer.GetText(grid.Columns["Amount"]!));
            list.Add(new Order { City = "İzmir", Amount = 7 });
            Assert.Equal("57", footer.GetText(grid.Columns["Amount"]!));

            provider.SetClasses(grid, "table footer:bg-red-100 footer:text-red-900");
            Assert.Equal(ColorTranslator.FromHtml("#fee2e2"), TwGridDecorator.For(grid).Parts.FooterBack);
            using (var bitmap = new Bitmap(footer.Width, footer.Height)) footer.DrawToBitmap(bitmap, new Rectangle(Point.Empty, footer.Size));
            Assert.NotEmpty(footer.ColumnBounds(grid));
        }
        finally
        {
            form.Close();
        }
    }));

    [Fact]
    public void New_cell_types_format_and_paint() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var form = NewForm();
        var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
        form.Controls.Add(grid);
        var spark = new TwSparklineColumn { Name = "spark", Kind = TwSparklineKind.Area };
        var bars = new TwSparklineColumn { Name = "bars", Kind = TwSparklineKind.Bar };
        var date = new TwDateColumn { Name = "date", Relative = true };
        var number = new TwNumberColumn { Name = "number", Culture = "tr-TR" };
        number.DefaultCellStyle.Format = "C2";
        var link = new TwLinkColumn { Name = "link" };
        var tags = new TwTagsColumn { Name = "tags", Variants = "vip: bg-amber-100 text-amber-800; *: bg-gray-100 text-gray-700" };
        grid.Columns.AddRange(spark, bars, date, number, link, tags);
        var now = new DateTime(2025, 6, 1, 12, 0, 0);
        var previous = TwDateCell.Now;
        TwDateCell.Now = () => now;
        try
        {
            grid.Rows.Add(new[] { 1.0, 3, 2, 5 }, "1;-2;3", now.AddMinutes(-5), -1250.5m, "detay", "vip, yeni, iade, kargo");
            grid.Rows.Add("4 2", "", now.AddDays(-30), 10, "", new[] { "yeni" });
            form.Show();

            Assert.Equal(new[] { 1.0, 3, 2, 5 }, TwSparklineCell.Numbers(grid[0, 0].Value));
            Assert.Equal(new[] { 4.0, 2 }, TwSparklineCell.Numbers("4 2"));
            Assert.Equal("1; 3; 2; 5", grid[0, 0].FormattedValue);
            Assert.Equal("5 dk önce", grid["date", 0].FormattedValue);
            Assert.Equal(now.AddDays(-30).ToString("g", CultureInfo.CurrentCulture), grid["date", 1].FormattedValue);
            Assert.Equal("az önce", TwDateCell.RelativeText(now, now));
            Assert.Equal("3 sa önce", TwDateCell.RelativeText(now.AddHours(-3), now));
            Assert.Equal("2 gün önce", TwDateCell.RelativeText(now.AddDays(-2), now));
            Assert.Null(TwDateCell.RelativeText(now.AddDays(1), now));

            Assert.Equal((-1250.5m).ToString("C2", new CultureInfo("tr-TR")), grid["number", 0].FormattedValue);
            Assert.Equal(DataGridViewContentAlignment.MiddleRight, number.DefaultCellStyle.Alignment);
            Assert.Equal("vip, yeni, iade, kargo", grid["tags", 0].FormattedValue);
            Assert.Equal(new[] { "vip", "yeni", "iade", "kargo" }, ((TwTagsCell)grid["tags", 0]).Tags(grid["tags", 0].Value));

            // Negatif sayılar NegativeClasses rengiyle biçimlenir.
            DataGridViewCellStyle? painted = null;
            grid.CellFormatting += (_, e) => { if (e.ColumnIndex == number.Index && e.RowIndex == 0) painted = e.CellStyle; };
            using (var bitmap = new Bitmap(grid.Width, grid.Height)) grid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, grid.Size));
            Assert.NotNull(painted);
            Assert.Equal(DataGridViewContentAlignment.MiddleRight, painted!.Alignment);

            var clone = (TwSparklineColumn)bars.Clone();
            Assert.Equal(TwSparklineKind.Bar, clone.Kind);
            Assert.Equal(",", ((TwTagsColumn)tags.Clone()).Separator);
        }
        finally
        {
            TwDateCell.Now = previous;
            form.Close();
        }
    }));

    [Fact]
    public void Number_cell_colors_negative_values() => Sta.Run(() =>
    {
        using var grid = new DataGridView { AllowUserToAddRows = false };
        var number = new TwNumberColumn { Name = "n" };
        grid.Columns.Add(number);
        grid.Rows.Add((object)(-3));
        grid.Rows.Add((object)4);
        var cell = grid[0, 0];
        var style = cell.InheritedStyle;
        var method = typeof(DataGridViewCell).GetMethod("GetFormattedValue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, new[] { typeof(object), typeof(int), typeof(DataGridViewCellStyle).MakeByRefType(), typeof(TypeConverter), typeof(TypeConverter), typeof(DataGridViewDataErrorContexts) }, null)!;
        var args = new object?[] { -3, 0, style, null, null, DataGridViewDataErrorContexts.Display };
        method.Invoke(cell, args);
        Assert.Equal(ColorTranslator.FromHtml("#dc2626").ToArgb(), ((DataGridViewCellStyle)args[2]!).ForeColor.ToArgb());
        args = new object?[] { 4, 1, grid[0, 1].InheritedStyle, null, null, DataGridViewDataErrorContexts.Display };
        method.Invoke(grid[0, 1], args);
        Assert.NotEqual(ColorTranslator.FromHtml("#dc2626").ToArgb(), ((DataGridViewCellStyle)args[2]!).ForeColor.ToArgb());
    });

    [Fact]
    public void Actions_column_opens_a_menu_and_raises_the_action() => Sta.Run(() =>
    {
        using var form = NewForm();
        var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
        form.Controls.Add(grid);
        grid.Columns.Add("name", "Ad");
        var actions = new TwActionsColumn { Name = "actions", Actions = "Düzenle; -; Sil" };
        grid.Columns.Add(actions);
        grid.Rows.Add("Ayşe");
        form.Show();
        try
        {
            TwGridActionEventArgs? clicked = null;
            actions.ActionClicked += (_, e) => clicked = e;
            actions.MenuOpening += (_, e) => e.Menu.Items[0].Enabled = e.RowIndex == 0;
            var menu = actions.ShowMenu(0)!;
            Assert.Same(menu, TwGrid.OpenMenu(grid));
            Assert.Equal(3, menu.Items.Count);
            Assert.IsType<ToolStripSeparator>(menu.Items[1]);
            menu.Items[2].PerformClick();
            Assert.Equal("Sil", clicked!.Action);
            Assert.Equal(0, clicked.RowIndex);
            Assert.Equal("İşlemler", grid["actions", 0].FormattedValue);
            menu.Close();
            Assert.DoesNotContain("İşlemler", TwGrid.ToDelimitedText(grid));    // işlem kolonu dışa aktarılmaz
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Row_details_expand_below_the_row_and_survive_sorting() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var (form, grid, list) = BoundGrid(provider);
        try
        {
            var expander = new TwExpanderColumn { Name = "expand" };
            grid.Columns.Insert(0, expander);
            int normal = grid.Rows[0].Height;
            int created = 0;
            TwGrid.SetRowDetails(grid, row => { created++; return new Label { Text = ((Order)row.DataBoundItem!).Customer }; }, height: 80);
            Assert.Throws<ArgumentOutOfRangeException>(() => TwGrid.SetRowExpanded(grid, 99, true));

            TwGrid.ToggleRowDetails(grid, 1);
            Assert.True(TwGrid.IsRowExpanded(grid, 1));
            Assert.Equal(1, created);
            Assert.Equal(normal + 80, grid.Rows[1].Height);
            var details = TwGrid.GetRowDetails(grid, 1)!;
            Assert.Equal("Mehmet Kaya", details.Text);
            Assert.Same(grid, details.Parent);
            Assert.Equal("Daralt", grid["expand", 1].FormattedValue);
            grid.Refresh();
            Assert.True(details.Visible);
            Assert.Equal(grid.GetRowDisplayRectangle(1, false).Bottom - 80, details.Top);

            // Satır, bağlı öğesiyle hatırlanır: sıralamadan sonra yeni konumunda açık kalır.
            grid.Sort(grid.Columns["Amount"]!, ListSortDirection.Descending);
            int moved = list.IndexOf(list.First(o => o.Customer == "Mehmet Kaya"));
            Assert.NotEqual(1, moved);
            Assert.True(TwGrid.IsRowExpanded(grid, moved));
            Assert.Same(details, TwGrid.GetRowDetails(grid, moved));

            TwGrid.SetRowExpanded(grid, moved, false);
            Assert.True(details.IsDisposed);
            Assert.Equal(normal, grid.Rows[moved].Height);
            TwGrid.SetRowDetails(grid, null);
            Assert.Throws<InvalidOperationException>(() => TwGrid.SetRowExpanded(grid, 0, true));
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Layout_round_trips_columns_sorting_filters_and_grouping() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var provider = new TailwindProvider();
        var (form, grid, list) = BoundGrid(provider);
        try
        {
            grid.Columns["Amount"]!.DisplayIndex = 0;
            grid.Columns["City"]!.Width = 151;
            grid.Columns["Date"]!.Visible = false;
            grid.Sort(grid.Columns["Customer"]!, ListSortDirection.Descending);
            list.SetColumnFilter("City", new[] { "İzmir", "Ankara" });
            list.SetRangeFilter("Amount", 15, null);
            TwGrid.SetGroupBy(grid, grid.Columns["City"]);
            var saved = TwGrid.SaveLayout(grid);
            Assert.Contains("\"SortColumn\":\"Customer\"", saved);

            // Yeni bir grid'e (ör. uygulamanın sonraki açılışı) yüklenir.
            var (form2, grid2, list2) = BoundGrid(provider);
            try
            {
                TwGrid.LoadLayout(grid2, saved);
                Assert.Equal(0, grid2.Columns["Amount"]!.DisplayIndex);
                Assert.Equal(151, grid2.Columns["City"]!.Width);
                Assert.False(grid2.Columns["Date"]!.Visible);
                Assert.Equal(new[] { "İzmir", "Ankara" }.OrderBy(v => v, StringComparer.Ordinal), list2.GetColumnFilter("City")!.OrderBy(v => v, StringComparer.Ordinal));
                Assert.True(list2.TryGetRangeFilter("Amount", out var min, out _));
                Assert.Equal(15, min);
                Assert.Equal("City", TwGrid.GetGroupBy(grid2)!.Name);
                Assert.Equal(grid2.Columns["Customer"], grid2.SortedColumn);
                Assert.Equal(new[] { "Can Öz", "Ayşe Yılmaz", "Zeynep Ak" }.OrderBy(c => c), list2.Select(o => o.Customer).OrderBy(c => c));
                Assert.Equal("Can Öz", list2[0].Customer);                    // grup (Ankara), sonra azalan ad

                TwGrid.LoadLayout(grid2, "{bozuk");                           // geçersiz düzen yok sayılır
                TwGrid.LoadLayout(grid2, "{\"Columns\":[{\"Name\":\"Yok\",\"Width\":10}]}");
            }
            finally
            {
                form2.Close();
                form2.Dispose();
            }
        }
        finally
        {
            form.Close();
        }
    }));

    [Fact]
    public void Export_writes_visible_columns_with_quoting() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var grid = new DataGridView { AllowUserToAddRows = false };
        grid.Columns.Add("name", "Ad");
        grid.Columns.Add("note", "Not");
        grid.Columns.Add("hidden", "Gizli");
        grid.Columns["hidden"]!.Visible = false;
        var amount = new DataGridViewTextBoxColumn { Name = "amount", HeaderText = "Tutar" };
        amount.DefaultCellStyle.Format = "N2";
        grid.Columns.Add(amount);
        grid.Columns.Add(new DataGridViewButtonColumn { Name = "button" });
        grid.Rows.Add("Ayşe", "a;b \"c\"", "x", 1250.5m, "Sil");
        grid.Rows.Add("Mehmet", "satır\nsonu", "y", 3m, "Sil");

        var text = TwGrid.ToDelimitedText(grid, ';');
        Assert.Equal("Ad;Not;Tutar\r\nAyşe;\"a;b \"\"c\"\"\";1.250,50\r\nMehmet;\"satır\nsonu\";3,00\r\n", text);
        Assert.StartsWith("Ayşe\t\"a;b \"\"c\"\"\"\t1.250,50\r\n", TwGrid.ToDelimitedText(grid, '\t', includeHeaders: false));

        grid.ClearSelection();
        grid["name", 1].Selected = true;
        grid["amount", 1].Selected = true;
        Assert.Equal("Ad\tTutar\r\nMehmet\t3,00\r\n", TwGrid.ToDelimitedText(grid, selectedOnly: true));

        var path = Path.Combine(Path.GetTempPath(), "formwind-export-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            TwGrid.ExportCsv(grid, path);                                      // tr-TR liste ayırıcısı: ;
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
            Assert.StartsWith("Ad;Not;Tutar", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
        }
        finally
        {
            File.Delete(path);
        }
    }));

    [Fact]
    public void Density_variants_and_part_prefixes_apply() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var grid = new DataGridView { AllowUserToAddRows = false };
        grid.Columns.Add("a", "A");
        grid.Rows.Add("x");
        provider.SetClasses(grid, "table table-sm");
        Assert.Equal(32, grid.RowTemplate.Height);
        Assert.Equal(32, grid.ColumnHeadersHeight);
        provider.SetClasses(grid, "table table-lg table-striped table-bordered");
        Assert.Equal(48, grid.RowTemplate.Height);
        Assert.Equal(DataGridViewCellBorderStyle.Single, grid.CellBorderStyle);
        Assert.Equal(ColorTranslator.FromHtml("#f9fafb").ToArgb(), grid.AlternatingRowsDefaultCellStyle.BackColor.ToArgb());

        var parts = TwGridDecorator.For(grid).Parts;
        Assert.NotNull(parts.GroupBack);
        Assert.NotNull(parts.FooterFont);
        Assert.Equal(ColorTranslator.FromHtml("#fde68a"), parts.MatchBack);   // warning-200 = amber-200
        provider.SetClasses(grid, "match:bg-pink-200 match:text-pink-900 group:bg-blue-50");
        Assert.Equal(ColorTranslator.FromHtml("#fbcfe8"), TwGridDecorator.For(grid).Parts.MatchBack);
        Assert.Null(TwGridDecorator.For(grid).Parts.FooterBack);

        foreach (var preset in new[] { TwPreset.Fluent, TwPreset.Antd, TwPreset.Material })
        {
            var theme = new TwTheme(preset);
            Assert.True(theme.Components.ContainsKey("table-sm"));
            Assert.True(theme.Components.ContainsKey("table-striped"));
            Assert.Contains("footer:", theme.Components["table"]);
        }
    });

    [Fact]
    public void Dark_inversion_styles_light_and_inverts_the_picture() => Sta.Run(() =>
    {
        Assert.Equal(Color.FromArgb(0, 0, 0).ToArgb(), TwGridInversion.Invert(Color.White).ToArgb());
        Assert.Equal(Color.White.ToArgb(), TwGridInversion.Invert(Color.Black).ToArgb());
        var blue = TwGridInversion.Invert(ColorTranslator.FromHtml("#2563eb"));
        Assert.True(blue.B > blue.R && blue.B > blue.G);                      // ton korunur

        using var provider = new TailwindProvider();
        using var form = NewForm();
        var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
        form.Controls.Add(grid);
        grid.Columns.Add("a", "A");
        grid.Rows.Add("x");
        // Kendi çizen grid: hücreleri sabit beyazla boyar.
        grid.CellPainting += (_, e) =>
        {
            if (e.RowIndex < 0 || e.Graphics is null) return;
            e.Graphics.FillRectangle(Brushes.White, e.CellBounds);
            e.Handled = true;
        };
        provider.SetClasses(grid, "table");
        provider.DarkMode = true;
        form.Show();
        Assert.False(TwGrid.IsDarkInverted(grid));
        Assert.Equal(ColorTranslator.FromHtml("#1f2937").ToArgb(), grid.DefaultCellStyle.BackColor.ToArgb());

        TwGrid.EnableDarkInversion(grid);
        Assert.True(TwGrid.IsDarkInverted(grid));
        Assert.Equal(Color.White.ToArgb(), grid.DefaultCellStyle.BackColor.ToArgb());   // açık stillerle çizilir
        Assert.False(TwGridState.IsDark(grid));
        var cell = grid.GetCellDisplayRectangle(0, 0, false);
        using (var picture = TwGridInversion.RenderInverted(grid))
        {
            var pixel = picture.GetPixel(cell.X + cell.Width / 2, cell.Y + cell.Height / 2);
            Assert.True(pixel.GetBrightness() < 0.1f, pixel.ToString());            // sabit beyaz koyulaşır
        }
        grid.Refresh();

        provider.DarkMode = false;
        Assert.False(TwGrid.IsDarkInverted(grid));
        TwGrid.EnableDarkInversion(grid, false);
        provider.DarkMode = true;
        Assert.Equal(ColorTranslator.FromHtml("#1f2937").ToArgb(), grid.DefaultCellStyle.BackColor.ToArgb());
        form.Close();
    });

    [Fact]
    public void Turkish_texts_cover_the_grid_features()
    {
        var tr = TwTexts.Turkish;
        Assert.Equal("En az", tr.RangeMin);
        Assert.Equal("{0} dk önce", tr.MinutesAgo);
        Assert.Equal("Min", TwTexts.English.RangeMin);
    }
}
