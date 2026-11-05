using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>0.1.6 step 4: value-dependent badges, data list columns with a sortable header, period filters.</summary>
[Collection(UiCollection.Name)]
public sealed class Step4ApiTests
{
    private static int Argb(string hex) => ColorTranslator.FromHtml(hex).ToArgb();

    private static int Count(Bitmap bitmap, Rectangle area, int argb, int tolerance = 30)
    {
        var target = Color.FromArgb(argb);
        int count = 0;
        for (int x = Math.Max(0, area.Left); x < Math.Min(bitmap.Width, area.Right); x++)
            for (int y = Math.Max(0, area.Top); y < Math.Min(bitmap.Height, area.Bottom); y++)
            {
                var c = bitmap.GetPixel(x, y);
                if (Math.Abs(c.R - target.R) + Math.Abs(c.G - target.G) + Math.Abs(c.B - target.B) < tolerance) count++;
            }
        return count;
    }

    private static void Invoke(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { args.GetType() }, null)!
            .Invoke(control, new object[] { args });

    private static Form NewForm(int width = 640, int height = 400) =>
        new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000), Size = new Size(width, height), ShowInTaskbar = false };

    // ---- TwBadgeColumn.Selector ----

    [Fact]
    public void Badge_selector_chooses_text_and_classes_from_the_value() => Sta.Run(() =>
    {
        using var grid = new DataGridView { AllowUserToAddRows = false, RowHeadersVisible = false, ColumnHeadersVisible = false, Size = new Size(300, 120) };
        var column = new TwBadgeColumn { Name = "delta", Width = 200, Variants = "*: bg-blue-500 text-white" };
        column.Selector = v => v switch
        {
            double d when d >= 0 => new TwBadge("+" + d + " %", "bg-green-500 text-white"),
            double d => new TwBadge(d + " %", "bg-red-500 text-white"),
            _ => new TwBadge("", null),
        };
        grid.Columns.Add(column);
        grid.Rows.Add(12.0);
        grid.Rows.Add(-3.0);
        grid.Rows.Add(DBNull.Value);
        grid.ClearSelection();

        using var bitmap = new Bitmap(grid.Width, grid.Height);
        grid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, grid.Size));
        Rectangle Row(int i) => Rectangle.Intersect(grid.GetColumnDisplayRectangle(0, false), grid.GetRowDisplayRectangle(i, false));

        // Seçici Variants'ı geçersiz kılar: ne mavi var, yeşil ve kırmızı değere göre; boş metin rozet çizmez.
        Assert.True(Count(bitmap, Row(0), Argb("#22c55e")) > 50, "pozitif yeşil değil");
        Assert.True(Count(bitmap, Row(1), Argb("#ef4444")) > 50, "negatif kırmızı değil");
        Assert.Equal(0, Count(bitmap, Row(0), Argb("#3b82f6")));
        Assert.Equal(0, Count(bitmap, Row(2), Argb("#22c55e")) + Count(bitmap, Row(2), Argb("#ef4444")) + Count(bitmap, Row(2), Argb("#3b82f6")));

        // Seçiciyi kaldırınca Variants geri gelir ve çoğaltma seçiciyi taşır.
        Assert.Same(column.Selector, ((TwBadgeColumn)column.Clone()).Selector);
        column.Selector = null;
        using var again = new Bitmap(grid.Width, grid.Height);
        grid.DrawToBitmap(again, new Rectangle(Point.Empty, grid.Size));
        Assert.True(Count(again, Row(0), Argb("#3b82f6")) > 50, "Variants geri gelmedi");
    });

    [Fact]
    public void Badge_with_a_null_text_keeps_the_formatted_text()
    {
        var badge = new TwBadge(null, "bg-gray-100");
        Assert.Null(badge.Text);
        Assert.Equal("bg-gray-100", badge.Classes);
        Assert.Equal("", new TwBadge("x", null).Classes);
    }

    // ---- TwDataList columns and header ----

    private static TwDataList NewList(int count = 5)
    {
        var list = new TwDataList { Size = new Size(400, 200), Padding = new Padding(4), ItemHeight = 40, ItemCount = count, SmoothScrolling = false };
        list.Columns.Add(new TwDataListColumn("Name") { Weight = 2, Sortable = true });
        list.Columns.Add(new TwDataListColumn("Amount", 80) { Alignment = HorizontalAlignment.Right, Sortable = true });
        list.Columns.Add(new TwDataListColumn("Note") { Weight = 1 });
        return list;
    }

    [Fact]
    public void Columns_share_the_row_width_and_line_up_in_every_row() => Sta.Run(() =>
    {
        using var list = NewList();
        var content = list.GetColumnBounds(0, 0);
        var a = Enumerable.Range(0, 3).Select(c => list.GetColumnBounds(c, 0)).ToArray();
        var b = Enumerable.Range(0, 3).Select(c => list.GetColumnBounds(c, 3)).ToArray();

        // Aynı x ve genişlik her satırda; sütunlar sırayla ve aralıklı dizilir.
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(a[i].X, b[i].X);
            Assert.Equal(a[i].Width, b[i].Width);
        }
        Assert.True(a[0].Right < a[1].X && a[1].Right < a[2].X);
        Assert.Equal(list.ScalePixels(80), a[1].Width);
        // Weight 2 : 1, kalan genişlik tümüyle dağıtılır (yuvarlama son sütunda toplanır).
        Assert.InRange(a[0].Width - 2 * a[2].Width, -3, 3);
        var area = list.ColumnArea;
        Assert.Equal(area.Right, a[2].Right);
        Assert.Equal(area.X, a[0].X);

        // Çizim olayındaki sütun, satırın içerik alanındadır.
        var args = (TwDataListDrawEventArgs)Activator.CreateInstance(typeof(TwDataListDrawEventArgs), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { list, Graphics.FromHwnd(IntPtr.Zero), 0, list.GetItemBounds(0), false, false }, null)!;
        Assert.Equal(a[1].X, args.GetColumnBounds(1).X);
        Assert.Equal(args.ContentBounds.Height, args.GetColumnBounds(1).Height);
        Assert.Equal(Rectangle.Empty, args.GetColumnBounds(7));
        Assert.NotEqual(0, content.Width);
    });

    [Fact]
    public void Header_columns_match_the_rows_also_with_a_scrollbar() => Sta.Run(() =>
    {
        using var form = NewForm();
        var list = NewList(3);
        list.Dock = DockStyle.Fill;
        var header = list.CreateHeader();
        form.Controls.Add(list);
        form.Controls.Add(header);
        form.Show();

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(list.GetColumnBounds(i, 0).X, header.GetColumnBounds(i).X);
            Assert.Equal(list.GetColumnBounds(i, 0).Width, header.GetColumnBounds(i).Width);
        }

        // Çok satır kaydırma çubuğunu getirir: satırlar ve başlık birlikte daralır.
        int before = list.GetColumnBounds(2, 0).Right;
        list.ItemCount = 1000;
        Assert.True(list.GetColumnBounds(2, 0).Right < before);
        Assert.Equal(list.GetColumnBounds(2, 0).Right, header.GetColumnBounds(2).Right);
        Assert.Equal(DockStyle.Top, header.Dock);
        Assert.Same(list, header.List);
    });

    [Fact]
    public void Toggling_sorts_ascending_then_descending_and_raises_the_event() => Sta.Run(() =>
    {
        using var list = NewList();
        int raised = 0;
        list.SortChanged += (_, _) => raised++;

        list.SortColumn = 1;                    // koddan atama olay doğurmaz
        Assert.Equal(SortOrder.Ascending, list.SortOrder);
        Assert.Equal(0, raised);

        list.ToggleSort(1);
        Assert.Equal((1, SortOrder.Descending, 1), (list.SortColumn, list.SortOrder, raised));
        list.ToggleSort(1);
        Assert.Equal((1, SortOrder.Ascending, 2), (list.SortColumn, list.SortOrder, raised));
        list.ToggleSort(0);                     // yeni sütun artan başlar
        Assert.Equal((0, SortOrder.Ascending, 3), (list.SortColumn, list.SortOrder, raised));

        list.ToggleSort(2);                     // sıralanamaz sütun
        list.ToggleSort(9);
        Assert.Equal((0, 3), (list.SortColumn, raised));

        list.SortColumn = -1;
        Assert.Equal(SortOrder.None, list.SortOrder);
        list.Columns.RemoveAt(1);
        list.ToggleSort(0);
        list.Columns.Clear();                   // sütun gidince sıralama sıfırlanır
        Assert.Equal(-1, list.SortColumn);
    });

    [Fact]
    public void Header_click_and_keyboard_sort_sortable_columns_only() => Sta.Run(() =>
    {
        using var form = NewForm();
        var list = NewList();
        list.Dock = DockStyle.Fill;
        var header = list.CreateHeader();
        form.Controls.Add(list);
        form.Controls.Add(header);
        form.Show();
        int raised = 0;
        list.SortChanged += (_, _) => raised++;

        Point Center(int c) => new Point(header.GetColumnBounds(c).X + header.GetColumnBounds(c).Width / 2, header.Height / 2);
        Invoke(header, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, Center(1).X, Center(1).Y, 0));
        Assert.Equal((1, SortOrder.Ascending, 1), (list.SortColumn, list.SortOrder, raised));
        Invoke(header, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, Center(1).X, Center(1).Y, 0));
        Assert.Equal(SortOrder.Descending, list.SortOrder);
        Invoke(header, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, Center(2).X, Center(2).Y, 0));    // Note sıralanamaz
        Assert.Equal((1, 2), (list.SortColumn, raised));

        // Klavye: ←/→ yalnız sıralanabilir sütunlara durur, Enter / Boşluk sıralar.
        Assert.True(header.TabStop);
        Invoke(header, "OnKeyDown", new KeyEventArgs(Keys.Home));
        Assert.Equal(0, header.FocusColumn);
        Invoke(header, "OnKeyDown", new KeyEventArgs(Keys.Right));
        Assert.Equal(1, header.FocusColumn);
        Invoke(header, "OnKeyDown", new KeyEventArgs(Keys.Right));
        Assert.Equal(1, header.FocusColumn);    // Note atlanır, son sıralanabilirde kalır
        Invoke(header, "OnKeyDown", new KeyEventArgs(Keys.Left));
        Invoke(header, "OnKeyDown", new KeyEventArgs(Keys.Enter));
        Assert.Equal((0, SortOrder.Ascending, 3), (list.SortColumn, list.SortOrder, raised));
        Invoke(header, "OnKeyDown", new KeyEventArgs(Keys.Space));
        Assert.Equal((SortOrder.Descending, 4), (list.SortOrder, raised));

        // Sıralanabilir sütun yoksa başlık Tab durağı olmaz.
        foreach (var column in list.Columns) column.Sortable = false;
        Assert.False(header.TabStop);
    });

    [Fact]
    public void Header_paints_names_and_the_sort_glyph_and_is_accessible() => Sta.Run(() =>
    {
        using var form = NewForm();
        var list = NewList();
        list.Dock = DockStyle.Fill;
        list.Colors = new TwDataListColors { Back = Color.White, Fore = Color.Black, GroupFore = Color.FromArgb(10, 200, 20) };
        var header = list.CreateHeader();
        form.Controls.Add(list);
        form.Controls.Add(header);
        form.Show();

        using (var plain = new Bitmap(header.Width, header.Height))
        {
            header.DrawToBitmap(plain, new Rectangle(Point.Empty, header.Size));
            // Adlar GroupFore ile, sıralı sütun yok.
            Assert.True(Count(plain, header.ClientRectangle, Color.FromArgb(10, 200, 20).ToArgb(), 90) > 20, "adlar çizilmedi");
        }

        list.SortColumn = 1;
        list.SortOrder = SortOrder.Descending;
        using (var sorted = new Bitmap(header.Width, header.Height))
        {
            header.DrawToBitmap(sorted, new Rectangle(Point.Empty, header.Size));
            // Sıralı sütunun adı ve oku ana metin rengiyle (siyah) çizilir.
            Assert.True(Count(sorted, header.GetColumnBounds(1), Color.Black.ToArgb(), 120) > 10, "ok çizilmedi");
        }

        // Ekran okuyucu: sütun başına bir başlık; sıralı olanın adı durumunu söyler; varsayılan eylem sıralar.
        var root = header.AccessibilityObject;
        Assert.Equal(3, root.GetChildCount());
        Assert.Equal(AccessibleRole.ColumnHeader, root.GetChild(0)!.Role);
        Assert.Equal("Name", root.GetChild(0)!.Name);
        Assert.Equal("Amount, " + TwTexts.Current.SortedDescending, root.GetChild(1)!.Name);
        Assert.Equal(TwTexts.Current.SortBy, root.GetChild(0)!.DefaultAction);
        Assert.True(string.IsNullOrEmpty(root.GetChild(2)!.DefaultAction));
        root.GetChild(0)!.DoDefaultAction();
        Assert.Equal((0, SortOrder.Ascending), (list.SortColumn, list.SortOrder));
        Assert.Equal("Name, " + TwTexts.Current.SortedAscending, root.GetChild(0)!.Name);
    });

    // ---- TwFilterBar Period ----

    private static TwFilterBar NewBar(Form form, out TwFilter filter, Func<DateTime>? clock = null)
    {
        if (clock != null) TwPeriodPicker.Clock = clock;
        var bar = new TwFilterBar { Dock = DockStyle.Top };
        filter = new TwFilter("date", "Date", TwFilterKind.Period);
        bar.Filters.Add(filter);
        bar.Filters.Add(new TwFilter("line", "Line", TwFilterKind.Select) { Options = { "L1", "L2" } });
        form.Controls.Add(bar);
        form.Show();
        return bar;
    }

    [Fact]
    public void Period_filter_shows_the_picker_text_as_a_chip() => Sta.Run(() =>
    {
        var original = TwPeriodPicker.Clock;
        try
        {
            using var form = NewForm();
            var bar = NewBar(form, out var filter, () => new DateTime(2026, 6, 30));
            int changed = 0;
            bar.FiltersChanged += (_, _) => changed++;

            Assert.NotNull(filter.Period);
            Assert.Null(bar.Find("line")!.Period);
            Assert.Null(bar.ChipOf("date"));
            Assert.Null(bar.GetPeriod("date"));

            var preset = TwPeriodPreset.LastDays(90).Text;
            bar.SetValue("date", preset);
            Assert.Equal(1, changed);
            Assert.Equal("Date: " + preset, bar.ChipOf("date")!.Text);
            Assert.True(bar.ChipOf("date")!.Removable);
            Assert.Equal(preset, bar.Values["date"]);
            Assert.Equal((new DateTime(2026, 4, 2), new DateTime(2026, 6, 30)), bar.GetPeriod("date")!.Value);

            // Özel aralık: değer seçicinin aralık metnidir.
            bar.SetPeriod("date", new DateTime(2026, 3, 31), new DateTime(2026, 1, 1));
            Assert.Equal(2, changed);
            Assert.Equal("Date: " + TwPeriodPicker.RangeText(new DateTime(2026, 1, 1), new DateTime(2026, 3, 31)), bar.ChipOf("date")!.Text);
            Assert.Equal((new DateTime(2026, 1, 1), new DateTime(2026, 3, 31)), bar.GetPeriod("date")!.Value);

            // Tanınmayan metin yok sayılır; kaldırma çipi siler.
            bar.SetValue("date", "no such period");
            Assert.Equal(2, changed);
            bar.SetValue("date", null);
            Assert.Equal(3, changed);
            Assert.Null(bar.ChipOf("date"));
            Assert.Null(bar.GetPeriod("date"));

            // LoadValues sessizdir.
            bar.LoadValues(new Dictionary<string, string?> { ["date"] = TwPeriodPreset.Yesterday().Text });
            Assert.Equal(3, changed);
            Assert.Equal((new DateTime(2026, 6, 29), new DateTime(2026, 6, 29)), bar.GetPeriod("date")!.Value);
            bar.ClearFilters();
            Assert.Null(bar.GetValue("date"));
            Assert.Throws<ArgumentException>(() => bar.SetPeriod("line", DateTime.Today, DateTime.Today));
        }
        finally
        {
            TwPeriodPicker.Clock = original;
        }
    });

    [Fact]
    public void Period_filter_is_added_from_the_menu_and_changed_in_the_popup() => Sta.Run(() =>
    {
        var original = TwPeriodPicker.Clock;
        try
        {
            using var form = NewForm();
            var bar = NewBar(form, out var filter, () => new DateTime(2026, 6, 30));
            int changed = 0;
            bar.FiltersChanged += (_, _) => changed++;

            // Menüde "Date" tıklanınca seçicinin o anki dönemi uygulanır ve panel değiştirmek için açılır.
            bar.ShowAddMenu();
            var menu = bar.OpenMenu!;
            Assert.Equal(new[] { "Date", "Line" }, menu.Items.Cast<ToolStripItem>().Select(i => i.Text));
            ((ToolStripMenuItem)menu.Items[0]).PerformClick();
            Assert.Equal(1, changed);
            Assert.NotNull(bar.ChipOf("date"));
            Assert.True(filter.Period!.DroppedDown);

            // Panelde başka bir hazır dönem seçmek çipi ve değeri günceller.
            filter.Period.PresetIndex = 2;
            Assert.Equal(2, changed);
            Assert.Equal("Date: " + TwPeriodPreset.LastDays(7).Text, bar.ChipOf("date")!.Text);
            filter.Period.ClosePanel();
            Assert.False(filter.Period.DroppedDown);

            // Çipe tıklamak paneli yeniden açar.
            bar.ShowEditor("date");
            Assert.True(filter.Period.DroppedDown);
            filter.Period.ClosePanel();

            // Uygulanmamış filtrede seçicinin değişimi çip oluşturmaz.
            bar.SetValue("date", null);
            changed = 0;
            filter.Period.PresetIndex = 4;
            Assert.Equal(0, changed);
            Assert.Null(bar.ChipOf("date"));
        }
        finally
        {
            TwPeriodPicker.Clock = original;
        }
    });
}
