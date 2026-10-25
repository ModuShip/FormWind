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

// Kolonlu ağaç tablo: TwGrid.SetTree.
[Collection(UiCollection.Name)]
public class GridTreeTests
{
    private sealed class Cause
    {
        public Cause(string name, int? minutes = null, int? stops = null, params Cause[] children)
        {
            Name = name;
            Minutes = minutes;
            Stops = stops;
            Children = children;
        }

        public string Name { get; }
        public int? Minutes { get; }
        public int? Stops { get; }
        public IReadOnlyList<Cause> Children { get; }
    }

    private static (Form Form, DataGridView Grid, DataGridViewColumn Name, DataGridViewColumn Minutes, DataGridViewColumn Stops) NewGrid()
    {
        var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(640, 400) };
        var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, RowHeadersVisible = false };
        var name = new DataGridViewTextBoxColumn { HeaderText = "Neden", DataPropertyName = "Name", Width = 240 };
        var minutes = new DataGridViewTextBoxColumn { HeaderText = "Dakika", DataPropertyName = "Minutes" };
        var stops = new DataGridViewTextBoxColumn { HeaderText = "Duruş", DataPropertyName = "Stops" };
        grid.Columns.AddRange(name, minutes, stops);
        form.Controls.Add(grid);
        return (form, grid, name, minutes, stops);
    }

    private static TwGridTreeNode[] Sample() => new[]
    {
        new TwGridTreeNode("Hat 1", null, null)
        {
            Nodes =
            {
                new TwGridTreeNode("Pres 3", null, null) { Nodes = { new TwGridTreeNode("Mil yatağı", 540, 6), new TwGridTreeNode("Kalıp değişimi", 120, 2) } },
                new TwGridTreeNode("Testere", 90, 3),
            },
        },
        new TwGridTreeNode("Hat 2", 30, 1),
    };

    private static void Key(DataGridView grid, Keys key) =>
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(grid, new object[] { new KeyEventArgs(key) });

    private static bool[] Shown(DataGridView grid) => grid.Rows.Cast<DataGridViewRow>().Select(r => r.Visible).ToArray();

    [Fact]
    public void Tree_rows_show_roots_and_expand_with_the_keyboard() => Sta.Run(() =>
    {
        var (form, grid, name, _, _) = NewGrid();
        using (form)
        {
            var tree = TwGrid.SetTree(grid, name, Sample());
            form.Show();

            Assert.Same(tree, TwGrid.GetTree(grid));
            Assert.Equal(6, grid.Rows.Count);                                      // tüm düğümler satırdır
            Assert.False(grid.AllowUserToAddRows);
            Assert.Equal(new[] { true, false, false, false, false, true }, Shown(grid));
            Assert.Equal("Mil yatağı", grid.Rows[2].Cells[0].Value);
            Assert.Equal(2, tree.NodeAt(2)!.Level);

            grid.CurrentCell = grid.Rows[0].Cells[0];
            Key(grid, Keys.Right);                                                 // açar
            Assert.Equal(new[] { true, true, false, false, true, true }, Shown(grid));
            Key(grid, Keys.Right);                                                 // ilk çocuğa iner
            Assert.Equal(1, grid.CurrentCell!.RowIndex);
            Key(grid, Keys.Add);
            Assert.True(tree.NodeAt(1)!.IsExpanded);
            Key(grid, Keys.Left);                                                  // kapatır
            Assert.False(tree.NodeAt(1)!.IsExpanded);
            Key(grid, Keys.Left);                                                  // üst düğüme çıkar
            Assert.Equal(0, grid.CurrentCell!.RowIndex);

            // Kapanan dalın içindeki geçerli hücre dalın kendisine taşınır.
            tree.ExpandAll();
            Assert.All(Shown(grid), Assert.True);
            grid.CurrentCell = grid.Rows[3].Cells[1];
            tree.NodeAt(0)!.IsExpanded = false;
            Assert.Equal((0, 1), (grid.CurrentCell!.RowIndex, grid.CurrentCell.ColumnIndex));
            Assert.Equal(new[] { true, false, false, false, false, true }, Shown(grid));
            Key(grid, Keys.Multiply);                                              // * tüm alt dalları açar
            Assert.All(Shown(grid), Assert.True);
            form.Close();
        }
    });

    [Fact]
    public void Tree_arrow_toggles_with_the_mouse_and_raises_events() => Sta.Run(() =>
    {
        var (form, grid, name, _, _) = NewGrid();
        using (form)
        {
            var tree = TwGrid.SetTree(grid, name, Sample());
            form.Show();
            var expanded = new List<string>();
            tree.NodeExpanded += (_, e) => expanded.Add((string)e.Node.Values![0]!);
            tree.NodeCollapsed += (_, e) => expanded.Add("-" + e.Node.Values![0]);

            var arrow = tree.GetExpanderBounds(0);
            Assert.False(arrow.IsEmpty);
            Assert.True(tree.GetExpanderBounds(5).IsEmpty);                        // çocuksuz düğüm
            var cell = grid.GetCellDisplayRectangle(0, 0, false);
            var click = new DataGridViewCellMouseEventArgs(0, 0, arrow.X + arrow.Width / 2 - cell.X, cell.Height / 2, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
            typeof(DataGridView).GetMethod("OnCellMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(grid, new object[] { click });
            Assert.True(tree.NodeAt(0)!.IsExpanded);
            var text = new DataGridViewCellMouseEventArgs(0, 0, cell.Width - 4, cell.Height / 2, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
            typeof(DataGridView).GetMethod("OnCellMouseClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(grid, new object[] { text });
            Assert.True(tree.NodeAt(0)!.IsExpanded);                               // metne tıklamak açıp kapamaz
            tree.CollapseAll();
            Assert.Equal(new[] { "Hat 1", }, expanded);
            tree.NodeAt(0)!.IsExpanded = true;
            tree.NodeAt(0)!.IsExpanded = false;
            Assert.Equal(new[] { "Hat 1", "Hat 1", "-Hat 1" }, expanded);

            // Girinti: alt düzeydeki düğümün metni daha sağdan başlar (dolgu); çizim hata vermez.
            tree.ExpandAll();
            var style0 = new DataGridViewCellFormattingEventArgs(0, 0, "Hat 1", typeof(string), new DataGridViewCellStyle());
            var style2 = new DataGridViewCellFormattingEventArgs(0, 2, "Mil", typeof(string), new DataGridViewCellStyle());
            var format = typeof(DataGridView).GetMethod("OnCellFormatting", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(DataGridViewCellFormattingEventArgs) }, null)!;
            format.Invoke(grid, new object[] { style0 });
            format.Invoke(grid, new object[] { style2 });
            Assert.True(style2.CellStyle!.Padding.Left > style0.CellStyle!.Padding.Left);
            using var bitmap = new Bitmap(grid.Width, grid.Height);
            grid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, grid.Size));
            form.Close();
        }
    });

    [Fact]
    public void Parent_rows_show_totals_and_the_footer_counts_leaves_once() => Sta.Run(() =>
    {
        var (form, grid, name, minutes, stops) = NewGrid();
        using (form)
        {
            var tree = TwGrid.SetTree(grid, name, Sample());
            tree.SetTotal(minutes, TwAggregate.Sum);
            tree.SetTotal(stops, TwAggregate.Max);
            var footer = TwGrid.AddFooter(grid);
            footer.SetAggregate(minutes, TwAggregate.Sum);
            footer.SetAggregate(stops, TwAggregate.Count);
            form.Show();

            Assert.Equal(750m, grid.Rows[0].Cells[1].Value);                        // 540 + 120 + 90
            Assert.Equal(660m, grid.Rows[1].Cells[1].Value);
            Assert.Equal(6, grid.Rows[0].Cells[2].Value);                           // en çok (yapraklardan)
            Assert.Equal(540, grid.Rows[2].Cells[1].Value);                         // yaprak kendi değerini taşır
            Assert.Equal("780", footer.GetText(minutes));                           // yalnızca yapraklar: 750 + 30
            Assert.Equal("4", footer.GetText(stops));
            form.Close();
        }
    });

    [Fact]
    public void Header_click_sorts_within_parents() => Sta.Run(() =>
    {
        var (form, grid, name, minutes, _) = NewGrid();
        using (form)
        {
            var tree = TwGrid.SetTree(grid, name, Sample());
            tree.SetTotal(minutes, TwAggregate.Sum);
            tree.ExpandAll();
            form.Show();
            Assert.Equal(DataGridViewColumnSortMode.Programmatic, minutes.SortMode);

            tree.Sort(minutes, ListSortDirection.Descending);
            var order = grid.Rows.Cast<DataGridViewRow>().Select(r => (string?)r.Cells[0].Value).ToArray();
            Assert.Equal(new[] { "Hat 1", "Pres 3", "Mil yatağı", "Kalıp değişimi", "Testere", "Hat 2" }, order);
            Assert.Equal(SortOrder.Descending, minutes.HeaderCell.SortGlyphDirection);

            tree.Sort(minutes, ListSortDirection.Ascending);
            order = grid.Rows.Cast<DataGridViewRow>().Select(r => (string?)r.Cells[0].Value).ToArray();
            Assert.Equal(new[] { "Hat 2", "Hat 1", "Testere", "Pres 3", "Kalıp değişimi", "Mil yatağı" }, order);
            Assert.All(Shown(grid), Assert.True);                                  // açık dallar açık kalır
            Assert.Same(tree.Roots[1].Nodes[1], tree.NodeAt(3));
            Assert.Same(tree.NodeAt(1), tree.NodeAt(2)!.Parent);
            form.Close();
        }
    });

    [Fact]
    public void Tree_reads_data_objects_and_rejects_bound_grids() => Sta.Run(() =>
    {
        var (form, grid, name, minutes, _) = NewGrid();
        using (form)
        {
            var data = new[] { new Cause("Hat 1", null, null, new Cause("Pres 3", 540, 6), new Cause("Testere", 90, 3)), new Cause("Hat 2", 30, 1) };
            var tree = TwGrid.SetTree(grid, name, data, c => c.Children);
            tree.SetTotal(minutes, TwAggregate.Sum);
            form.Show();
            Assert.Equal(4, grid.Rows.Count);
            Assert.Equal("Pres 3", grid.Rows[1].Cells[0].Value);
            Assert.Equal(630m, grid.Rows[0].Cells[1].Value);
            Assert.Same(data[0].Children[0], tree.NodeAt(1)!.DataItem);

            // Düğüm eklenip Refresh çağrılınca satırlar yeniden kurulur.
            tree.Roots[1].Nodes.Add(new TwGridTreeNode { DataItem = new Cause("Robot", 15, 1) });
            tree.Refresh();
            Assert.Equal(5, grid.Rows.Count);
            Assert.False(grid.Rows[4].Visible);
            Assert.Equal(30, grid.Rows[3].Cells[1].Value);                          // Hat 2'nin kendi değeri var: toplam yazılmaz
            form.Close();
        }

        using var bound = new DataGridView { DataSource = new BindingList<Cause>() };
        var column = new DataGridViewTextBoxColumn();
        bound.Columns.Add(column);
        Assert.Throws<InvalidOperationException>(() => TwGrid.SetTree(bound, column, Array.Empty<TwGridTreeNode>()));
    });
}
