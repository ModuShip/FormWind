using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// TwListItem'ın anahtar simgesi, eylem düğmesi ve ilerleme çubuğu; iki satırlı tablo hücresi (TwTwoLineColumn) ve ağaç hücresi.
[Collection(UiCollection.Name)]
public class TwoLineAndListPartsTests
{
    private static Form NewForm(int width = 640, int height = 400) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(width, height) };

    private static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(Math.Max(1, control.Width), Math.Max(1, control.Height));
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    private static int Count(Bitmap bitmap, Rectangle area, Color target, int tolerance = 24)
    {
        area = Rectangle.Intersect(area, new Rectangle(Point.Empty, bitmap.Size));
        int count = 0;
        for (int x = area.Left; x < area.Right; x++)
            for (int y = area.Top; y < area.Bottom; y++)
            {
                var c = bitmap.GetPixel(x, y);
                if (Math.Abs(c.R - target.R) + Math.Abs(c.G - target.G) + Math.Abs(c.B - target.B) < tolerance) count++;
            }
        return count;
    }

    private static void Call(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, new object[] { args });

    private static Point Center(Rectangle r) => new Point(r.X + r.Width / 2, r.Y + r.Height / 2);

    private static void Click(Control control, Point at, Point? releaseAt = null)
    {
        var to = releaseAt ?? at;
        Call(control, "OnMouseMove", new MouseEventArgs(MouseButtons.None, 0, at.X, at.Y, 0));
        Call(control, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, at.X, at.Y, 0));
        Call(control, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, to.X, to.Y, 0));
    }

    private static void Key(Control control, Keys key) => Call(control, "OnKeyDown", new KeyEventArgs(key));

    private static (Form Form, TwListBox List) NewList(params TwListItem[] items)
    {
        var form = NewForm();
        var list = new TwListBox { Dock = DockStyle.Fill, SmoothScrolling = false };
        form.Controls.Add(list);
        foreach (var item in items) list.Items.Add(item);
        form.Show();
        return (form, list);
    }

    private static TwListItem Full() => new TwListItem("Kullanıcı kontrolü", "Vardiya A")
    {
        ToggleIconName = "star", ToggledIconName = "star-filled", ToggleText = "Yer imi",
        ActionText = "Giriş yap", Badge = "Yeni",
    };

    [Fact]
    public void Row_parts_are_laid_out_without_overlapping_and_only_where_they_exist() => Sta.Run(() =>
    {
        var (form, list) = NewList(Full(), new TwListItem("Düz satır"),
            new TwListItem("Kayıtlar") { ProgressValue = 0, ProgressText = "0 / 2 kayıt tamam" });
        using (form)
        {
            var row = list.GetItemBounds(0);
            var toggle = list.GetToggleBounds(0);
            var action = list.GetActionBounds(0);
            Assert.False(toggle.IsEmpty);
            Assert.False(action.IsEmpty);
            Assert.True(row.Contains(toggle) && row.Contains(action));
            Assert.True(toggle.Right < action.Left);
            Assert.True(toggle.Left < row.Left + row.Width / 4);
            Assert.True(action.Right > row.Right - row.Width / 4);   // rozet eylemin solunda
            Assert.Equal(Rectangle.Empty, list.GetToggleBounds(1));
            Assert.Equal(Rectangle.Empty, list.GetActionBounds(1));
            Assert.Equal(Rectangle.Empty, list.GetToggleBounds(99));

            // İlerleme çubuğu ikinci satırı açar: satır iki satır yüksekliğindedir.
            Assert.True(list.GetItemBounds(2).Height > list.GetItemBounds(1).Height);
            Assert.True(new TwListItem("x") { ProgressValue = 0.5 }.IsTwoLine);
        }
    });

    [Fact]
    public void Clicking_the_toggle_flips_it_and_raises_ItemToggle_once_per_click() => Sta.Run(() =>
    {
        var (form, list) = NewList(Full(), Full());
        using (form)
        {
            var raised = new List<(int Index, bool Value)>();
            list.ItemToggle += (_, e) => raised.Add((e.Index, e.Item.IsToggled));
            var activated = 0;
            list.ItemActivate += (_, _) => activated++;

            Click(list, Center(list.GetToggleBounds(1)));
            Assert.Equal(new[] { (1, true) }, raised);
            Assert.True(((TwListItem)list.Items[1]).IsToggled);
            Assert.False(((TwListItem)list.Items[0]).IsToggled);

            Click(list, Center(list.GetToggleBounds(1)));
            Assert.Equal(new[] { (1, true), (1, false) }, raised);

            // Çift tık satırı etkinleştirmez, düğme olmayan yer etkinleştirir.
            Call(list, "OnMouseDoubleClick", new MouseEventArgs(MouseButtons.Left, 2, Center(list.GetToggleBounds(1)).X, Center(list.GetToggleBounds(1)).Y, 0));
            Assert.Equal(0, activated);

            // Dışarıda basıp düğmede bırakmak tıklama sayılmaz.
            raised.Clear();
            Click(list, new Point(list.GetItemBounds(0).Right - 3, list.GetItemBounds(0).Y + 2), Center(list.GetToggleBounds(0)));
            Assert.Empty(raised);
        }
    });

    [Fact]
    public void Clicking_the_action_button_raises_ItemActionClick_and_releasing_elsewhere_does_not() => Sta.Run(() =>
    {
        var (form, list) = NewList(Full(), new TwListItem("Düz"));
        using (form)
        {
            var clicked = new List<(int Index, string? Text)>();
            list.ItemActionClick += (_, e) => clicked.Add((e.Index, e.Item.ActionText));
            var toggled = 0;
            list.ItemToggle += (_, _) => toggled++;

            Click(list, Center(list.GetActionBounds(0)));
            Assert.Equal(new[] { (0, (string?)"Giriş yap") }, clicked);
            Assert.Equal(0, toggled);

            Click(list, Center(list.GetActionBounds(0)), Center(list.GetItemBounds(0)));   // başka yerde bırakıldı
            Assert.Single(clicked);

            Click(list, Center(list.GetItemBounds(1)));   // eylemi olmayan satır
            Assert.Single(clicked);
        }
    });

    [Fact]
    public void Keyboard_toggles_with_space_and_presses_the_action_with_ctrl_enter() => Sta.Run(() =>
    {
        var (form, list) = NewList(new TwListItem("Düz"), Full());
        using (form)
        {
            int toggled = 0, actions = 0, activated = 0;
            list.ItemToggle += (_, _) => toggled++;
            list.ItemActionClick += (_, _) => actions++;
            list.ItemActivate += (_, _) => activated++;

            list.SelectedIndex = 0;
            Key(list, Keys.Space);
            Call(list, "OnKeyDown", new KeyEventArgs(Keys.Enter | Keys.Control));
            Assert.Equal((0, 0), (toggled, actions));   // satırın parçası yok
            Assert.Equal(1, activated);                 // Ctrl+Enter parçası olmayan satırda yine Enter gibi etkinleştirir
            activated = 0;

            list.SelectedIndex = 1;
            Key(list, Keys.Space);
            Assert.True(((TwListItem)list.Items[1]).IsToggled);
            Call(list, "OnKeyDown", new KeyEventArgs(Keys.Enter | Keys.Control));
            Assert.Equal((1, 1), (toggled, actions));
            Assert.Equal(0, activated);   // Ctrl+Enter satırı etkinleştirmez

            Key(list, Keys.Enter);
            Assert.Equal(1, activated);
        }
    });

    [Fact]
    public void Row_parts_are_exposed_to_screen_readers_with_names_states_and_default_actions() => Sta.Run(() =>
    {
        var (form, list) = NewList(Full(), new TwListItem("Düz"));
        using (form)
        {
            var row = list.AccessibilityObject.GetChild(0)!;
            Assert.Equal(2, row.GetChildCount());
            Assert.Equal(0, list.AccessibilityObject.GetChild(1)!.GetChildCount());

            var toggle = row.GetChild(0)!;
            var action = row.GetChild(1)!;
            Assert.Equal("Yer imi", toggle.Name);
            Assert.Equal(AccessibleRole.CheckButton, toggle.Role);
            Assert.Equal(AccessibleStates.None, toggle.State & AccessibleStates.Checked);
            Assert.Equal("Giriş yap", action.Name);
            Assert.Equal(AccessibleRole.PushButton, action.Role);
            Assert.Same(row, toggle.Parent);
            Assert.False(toggle.Bounds.IsEmpty);

            var clicks = 0;
            list.ItemActionClick += (_, _) => clicks++;
            toggle.DoDefaultAction();
            Assert.Equal(AccessibleStates.Checked, list.AccessibilityObject.GetChild(0)!.GetChild(0)!.State & AccessibleStates.Checked);
            action.DoDefaultAction();
            Assert.Equal(1, clicks);

            // Adı verilmeyen anahtar TwTexts'ten adlandırılır.
            ((TwListItem)list.Items[0]).ToggleText = null;
            Assert.Equal(TwTexts.Current.Toggle, list.AccessibilityObject.GetChild(0)!.GetChild(0)!.Name);
        }
    });

    [Fact]
    public void Row_parts_are_drawn_with_their_colors_and_follow_right_to_left() => Sta.Run(() =>
    {
        var progressColor = Color.FromArgb(220, 20, 60);
        var toggleColor = Color.FromArgb(255, 140, 0);
        var item = Full();
        item.IsToggled = true;
        item.ToggleColor = toggleColor;
        item.ProgressValue = 0.5;
        item.ProgressText = "1 / 2 kayıt tamam";
        item.ProgressColor = progressColor;
        var (form, list) = NewList(item);
        using (form)
        {
            using (var bitmap = Render(list))
            {
                var row = list.GetItemBounds(0);
                Assert.True(Count(bitmap, list.GetToggleBounds(0), toggleColor) > 5);        // dolu yıldız
                Assert.True(Count(bitmap, new Rectangle(row.X, row.Y, row.Width / 2, row.Height), progressColor) > 20);   // çubuğun dolu kısmı
                // Eylem düğmesi çerçeve ve zemin çizer: alanında satır zemininden farklı pikseller vardır.
                var action = list.GetActionBounds(0);
                Assert.True(Count(bitmap, action, bitmap.GetPixel(action.X - 3, action.Y + action.Height / 2), 6) < action.Width * action.Height);
            }

            // Sağdan sola düzende parçalar yer değiştirir.
            form.RightToLeft = RightToLeft.Yes;
            list.RightToLeft = RightToLeft.Yes;
            var rtlRow = list.GetItemBounds(0);
            Assert.True(list.GetToggleBounds(0).Left > rtlRow.Left + rtlRow.Width / 2);
            Assert.True(list.GetActionBounds(0).Right < rtlRow.Left + rtlRow.Width / 2);
            using var rtlBitmap = Render(list);
            Assert.True(Count(rtlBitmap, list.GetToggleBounds(0), toggleColor) > 5);
            // Tıklama yeni yerlerde çalışır.
            int toggled = 0;
            list.ItemToggle += (_, _) => toggled++;
            Click(list, Center(list.GetToggleBounds(0)));
            Assert.Equal(1, toggled);
        }
    });

    [Fact]
    public void Row_parts_are_hit_tested_after_scaling_and_with_a_draw_handler_the_template_is_off() => Sta.Run(() =>
    {
        var (form, list) = NewList(Full());
        using (form)
        {
            var toggle = list.GetToggleBounds(0);
            list.Font = new Font(list.Font.FontFamily, list.Font.SizeInPoints * 1.5f);
            Assert.Equal(toggle.Width, list.GetToggleBounds(0).Width);   // simge alanı DPI ile ölçeklenir, yazı boyutuyla değil
            Assert.True(list.GetActionBounds(0).Height > 0);

            list.DrawItem += (_, _) => { };
            Assert.Equal(Rectangle.Empty, list.GetToggleBounds(0));
            Assert.Equal(0, list.AccessibilityObject.GetChild(0)!.GetChildCount());
        }
    });

    // ---- E1: TwTwoLineColumn ----

    private sealed class Record
    {
        public Record(string code, string machine, Color status)
        {
            Code = code;
            Machine = machine;
            Status = status;
        }

        public string Code { get; }
        public string Machine { get; }
        public Color Status { get; }
    }

    private static (Form Form, DataGridView Grid, TwTwoLineColumn Column) NewTable(bool rtl = false)
    {
        var form = NewForm(500, 300);
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false, AllowUserToAddRows = false, AutoGenerateColumns = false, RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No,
            BackgroundColor = Color.White, EnableHeadersVisualStyles = false,
        };
        var column = new TwTwoLineColumn { HeaderText = "Kayıt", DataPropertyName = "Code", SubtitleMember = nameof(Record.Machine), DotColorMember = nameof(Record.Status), Width = 300 };
        grid.Columns.Add(column);
        grid.DataSource = new BindingList<Record>
        {
            new Record("PR-2041", "Pres 3 · mil yatağı", Color.FromArgb(220, 20, 60)),
            new Record("PR-2042", "", Color.Empty),
        };
        form.Controls.Add(grid);
        form.Show();
        return (form, grid, column);
    }

    [Fact]
    public void Two_line_cell_reports_the_height_of_two_lines_and_fits_the_rows() => Sta.Run(() =>
    {
        var (form, grid, column) = NewTable();
        using (form)
        {
            var cellTwo = grid.Rows[0].Cells[0];
            var cellOne = grid.Rows[1].Cells[0];
            using var g = grid.CreateGraphics();
            var method = typeof(DataGridViewCell).GetMethod("GetPreferredSize", BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(Graphics), typeof(DataGridViewCellStyle), typeof(int), typeof(Size) }, null)!;
            Size Preferred(DataGridViewCell cell) => (Size)method.Invoke(cell, new object[] { g, cell.InheritedStyle, cell.RowIndex, Size.Empty })!;
            int twoLines = Preferred(cellTwo).Height, oneLine = Preferred(cellOne).Height;
            Assert.True(twoLines > oneLine + grid.Font.Height / 2);
            Assert.True(twoLines >= grid.Font.Height * 2);

            // FitRowHeight iki satırın sığacağı yüksekliği verir; otomatik satır boyutu da aynı değere yakın çıkar.
            Assert.True(column.GetPreferredRowHeight() >= twoLines - 2);
            int before = grid.Rows[0].Height;
            column.FitRowHeight();
            Assert.True(grid.Rows[0].Height > before);
            Assert.Equal(column.GetPreferredRowHeight(), grid.Rows[0].Height);
            Assert.True(grid.RowTemplate.Height >= column.GetPreferredRowHeight());

            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            Assert.True(grid.Rows[0].Height > grid.Rows[1].Height);   // alt satırı olmayan kayıt kısa kalır
        }
    });

    [Fact]
    public void Two_line_cell_reads_members_draws_the_dot_and_formats_for_screen_readers() => Sta.Run(() =>
    {
        var (form, grid, column) = NewTable();
        using (form)
        {
            column.MonospaceTitle = true;
            column.FitRowHeight();
            Assert.Equal("PR-2041, Pres 3 · mil yatağı", grid.Rows[0].Cells[0].FormattedValue);   // ekran okuyucu başlığı ve üyeden gelen alt satırı okur
            Assert.Equal("PR-2042", grid.Rows[1].Cells[0].FormattedValue);

            grid.ClearSelection();
            using var bitmap = Render(grid);
            var cell = grid.GetCellDisplayRectangle(0, 0, false);
            Assert.True(Count(bitmap, cell, Color.FromArgb(220, 20, 60)) > 10);      // durum noktası
            // İkinci satır başlıktan soluk: başlığın koyu pikselleri üst yarıda, soluk pikseller alt yarıda.
            var upper = new Rectangle(cell.X + 20, cell.Y, cell.Width - 20, cell.Height / 2);
            var lower = new Rectangle(cell.X + 20, cell.Y + cell.Height / 2, cell.Width - 20, cell.Height / 2);
            Assert.True(Count(bitmap, upper, Color.Black, 200) > 10);
            Assert.True(Count(bitmap, lower, Color.White, 6) < lower.Width * lower.Height);   // altta da yazı var
            Assert.True(Count(bitmap, lower, Color.Black, 200) < Count(bitmap, upper, Color.Black, 200));

            // Alt satırı olmayan kayıtta nokta ve ikinci satır yoktur.
            var plain = grid.GetCellDisplayRectangle(0, 1, false);
            Assert.Equal(0, Count(bitmap, plain, Color.FromArgb(220, 20, 60)));

            // ValueSelector üyelerin yerine geçer; TwTwoLine değeri de doğrudan kullanılır.
            column.ValueSelector = item => item is Record r ? new TwTwoLine("[" + r.Code + "]", "özel") : null;
            Assert.Equal("[PR-2041], özel", grid.Rows[0].Cells[0].FormattedValue);
        }
    });

    [Fact]
    public void Two_line_cell_takes_a_TwTwoLine_value_sorts_by_title_and_follows_selection_colors() => Sta.Run(() =>
    {
        var form = NewForm(500, 300);
        using (form)
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false, AllowUserToAddRows = false, BackgroundColor = Color.White };
            var column = new TwTwoLineColumn { HeaderText = "Kayıt", Width = 300 };
            grid.Columns.Add(column);
            grid.Rows.Add(new TwTwoLine("B-2", "ikinci") { DotColor = Color.SeaGreen });
            grid.Rows.Add(new TwTwoLine("A-1", "birinci"));
            form.Controls.Add(grid);
            form.Show();
            column.FitRowHeight();

            Assert.Equal("B-2, ikinci", grid.Rows[0].Cells[0].FormattedValue);
            Assert.True(new TwTwoLine("A-1").CompareTo(new TwTwoLine("B-2")) < 0);
            grid.Sort(column, ListSortDirection.Ascending);
            Assert.Equal("A-1", Assert.IsType<TwTwoLine>(grid.Rows[0].Cells[0].Value).Title);

            // Seçili satır seçim renkleriyle çizilir; yazı seçim yazı rengindedir.
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 41, 59);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.Rows[0].Selected = true;
            using var bitmap = Render(grid);
            var cell = grid.GetCellDisplayRectangle(0, 0, false);
            Assert.True(Count(bitmap, cell, Color.FromArgb(30, 41, 59)) > cell.Width * cell.Height / 2);
            Assert.True(Count(bitmap, cell, Color.White, 150) > 10);
        }
    });

    [Fact]
    public void Two_line_cell_mirrors_in_right_to_left_grids_and_draws_in_dark_colors() => Sta.Run(() =>
    {
        var (form, grid, column) = NewTable(rtl: true);
        using (form)
        {
            column.FitRowHeight();
            grid.ClearSelection();
            using (var bitmap = Render(grid))
            {
                var cell = grid.GetCellDisplayRectangle(0, 0, false);
                var right = new Rectangle(cell.X + cell.Width / 2, cell.Y, cell.Width / 2, cell.Height);
                var left = new Rectangle(cell.X, cell.Y, cell.Width / 2, cell.Height);
                Assert.True(Count(bitmap, right, Color.FromArgb(220, 20, 60)) > 10);   // nokta sağ kenarda
                Assert.Equal(0, Count(bitmap, left, Color.FromArgb(220, 20, 60)));
            }

            grid.RightToLeft = RightToLeft.No;
            grid.DefaultCellStyle.BackColor = Color.FromArgb(17, 24, 39);
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(243, 244, 246);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(55, 65, 81);
            grid.BackgroundColor = grid.DefaultCellStyle.BackColor;
            grid.ClearSelection();
            using var dark = Render(grid);
            var darkCell = grid.GetCellDisplayRectangle(0, 0, false);
            Assert.True(Count(dark, darkCell, Color.FromArgb(243, 244, 246), 220) > 10);              // başlık açık renkte
            Assert.True(Count(dark, darkCell, Color.FromArgb(17, 24, 39), 6) > darkCell.Width * darkCell.Height / 2);
        }
    });

    // ---- G2: TwGridTree iki satırlı ağaç hücresi ----

    private static (Form Form, DataGridView Grid, DataGridViewColumn Name) NewTree()
    {
        var form = NewForm(640, 400);
        var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false, BackgroundColor = Color.White };
        var name = new DataGridViewTextBoxColumn { HeaderText = "Neden", Width = 300 };
        var minutes = new DataGridViewTextBoxColumn { HeaderText = "Dakika" };
        grid.Columns.AddRange(name, minutes);
        form.Controls.Add(grid);
        return (form, grid, name);
    }

    private static TwGridTreeNode[] TreeNodes() => new[]
    {
        new TwGridTreeNode("Hat 1", null) { Nodes = { new TwGridTreeNode("Pres 3", 540), new TwGridTreeNode("Testere", 90) } },
        new TwGridTreeNode("Hat 2", 30),
    };

    [Fact]
    public void Tree_rows_with_a_subtitle_are_taller_and_draw_two_lines() => Sta.Run(() =>
    {
        var (form, grid, name) = NewTree();
        using (form)
        {
            var tree = TwGrid.SetTree(grid, name, TreeNodes());
            form.Show();
            tree.ExpandAll();
            int single = grid.Rows[0].Height;
            Assert.Equal(single, grid.Rows[3].Height);

            tree.SubtitleSelector = node => node.HasChildren ? "Hat · " + node.Nodes.Count + " alt grup" : null;
            Assert.True(grid.Rows[0].Height > single);                  // iki satır ister
            Assert.Equal(single, grid.Rows[1].Height);                  // alt satırı olmayan düğüm değişmez
            Assert.Equal(single, grid.Rows[3].Height);
            Assert.Equal("Hat · 2 alt grup", tree.GetSubtitle(tree.NodeAt(0)!));
            Assert.Null(tree.GetSubtitle(tree.NodeAt(1)!));
            Assert.True(tree.NodeAt(1)!.IsVisible);

            // Düğümün kendi Subtitle'ı seçiciden önce gelir; toplamlar ve değerler etkilenmez.
            tree.NodeAt(3)!.Subtitle = "tek satırlık değil";
            tree.Refresh();
            Assert.True(grid.Rows[3].Height > single);
            Assert.Equal("Hat 2", grid.Rows[3].Cells[0].Value);

            using var bitmap = Render(grid);
            var cell = grid.GetCellDisplayRectangle(0, 0, false);
            var lower = new Rectangle(cell.X + 40, cell.Y + cell.Height / 2 + 2, cell.Width - 60, cell.Height / 2 - 4);
            Assert.True(Count(bitmap, lower, Color.White, 6) < lower.Width * lower.Height);   // alt yarıda soluk yazı var
            var oneLine = grid.GetCellDisplayRectangle(0, 1, false);
            var oneLineLower = new Rectangle(oneLine.X + 40, oneLine.Y + oneLine.Height - 4, oneLine.Width - 60, 2);
            Assert.Equal(oneLineLower.Width * oneLineLower.Height, Count(bitmap, oneLineLower, Color.White, 6));   // tek satırda altı boş

            // Ok ve girinti yerinde kalır: çocuk satırın oku hâlâ tıklanır.
            Assert.False(tree.GetExpanderBounds(0).IsEmpty);
        }
    });

    [Fact]
    public void Tree_column_can_be_a_two_line_column_that_receives_the_subtitle() => Sta.Run(() =>
    {
        var form = NewForm(640, 400);
        using (form)
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false, BackgroundColor = Color.White };
            var name = new TwTwoLineColumn { HeaderText = "Neden", Width = 300 };
            grid.Columns.Add(name);
            form.Controls.Add(grid);
            var tree = TwGrid.SetTree(grid, name, new[] { new TwGridTreeNode("Hat 1") { Subtitle = "2 alt grup", Nodes = { new TwGridTreeNode("Pres 3") } } });
            form.Show();

            var value = Assert.IsType<TwTwoLine>(grid.Rows[0].Cells[0].Value);
            Assert.Equal(("Hat 1", "2 alt grup"), (value.Title, value.Subtitle));
            Assert.IsNotType<TwTwoLine>(grid.Rows[1].Cells[0].Value);
            Assert.True(grid.Rows[0].Height > grid.Rows[1].Height);
        }
    });
}
