using System;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// The 0.1.10-preview.1 user feedback report: chip strip sizing and padding, select column edit rights and validation,
/// menu item fonts and the info tip click.
/// </summary>
[Collection(UiCollection.Name)]
public class Feedback0110Tests
{
    private static Form HostForm(int width = 640, int height = 200) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(width, height) };

    private static Button Chip(int width = 80, int height = 26) => new Button { AutoSize = false, Size = new Size(width, height) };

    [Fact]
    public void AutoSize_chip_strip_prefers_the_size_of_its_chips() => Sta.Run(() =>
    {
        using var form = HostForm();
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var strip = new TwChipStrip { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Gap = 8, Padding = new Padding(4, 2, 4, 2) };
        for (int i = 0; i < 4; i++) strip.Controls.Add(Chip());
        flow.Controls.Add(strip);
        form.Controls.Add(flow);
        form.Show();
        Application.DoEvents();

        var preferred = strip.GetPreferredSize(Size.Empty);
        Assert.Equal(new Size(4 * 80 + 3 * 8 + 8, 26 + 4), preferred);
        Assert.Equal(preferred, strip.Size);
        Assert.All(strip.Chips, chip => Assert.Equal(26, chip.Height));
        Assert.Equal(0, strip.MaxScrollOffset);
    });

    [Fact]
    public void Hidden_chips_take_no_room() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = new TwChipStrip { AutoSize = true, Gap = 8 };
        for (int i = 0; i < 3; i++) strip.Controls.Add(Chip());
        strip.Chips[1].Visible = false;
        form.Controls.Add(strip);
        form.Show();
        Application.DoEvents();

        Assert.Equal(2 * 80 + 8, strip.GetPreferredSize(Size.Empty).Width);
        Assert.Equal(strip.Chips[0].Right + 8, strip.Chips[2].Left);
    });

    [Fact]
    public void AutoSize_chip_strip_keeps_scrolling_within_its_maximum_width() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = new TwChipStrip { AutoSize = true, MaximumSize = new Size(200, 0) };
        for (int i = 0; i < 6; i++) strip.Controls.Add(Chip());
        form.Controls.Add(strip);
        form.Show();
        Application.DoEvents();

        Assert.Equal(200, strip.Width);
        Assert.True(strip.MaxScrollOffset > 0);
        Assert.True(strip.NextButton.Visible);
    });

    [Theory]
    [InlineData(0, 0, 9)]
    [InlineData(6, 0, 12)]
    [InlineData(0, 8, 5)]
    [InlineData(0, 12, 3)]
    public void Chips_are_centered_inside_the_padding(int top, int bottom, int expectedTop) => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = new TwChipStrip { Dock = DockStyle.Top, Height = 44, Padding = new Padding(0, top, 0, bottom) };
        strip.Controls.Add(Chip());
        form.Controls.Add(strip);
        form.Show();
        Application.DoEvents();

        Assert.Equal(expectedTop, strip.Chips[0].Top);
        Assert.Equal(26, strip.Chips[0].Height);
    });

    [Fact]
    public void A_chip_taller_than_the_strip_is_clipped_not_shrunk() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = new TwChipStrip { Dock = DockStyle.Top, Height = 20, Padding = new Padding(0, 2, 0, 2) };
        strip.Controls.Add(Chip());
        form.Controls.Add(strip);
        form.Show();
        Application.DoEvents();

        Assert.Equal(26, strip.Chips[0].Height);
        Assert.Equal(2, strip.Chips[0].Top);
    });

    [Theory]
    [InlineData(TwAlign.Start, 4)]
    [InlineData(TwAlign.Center, 11)]
    [InlineData(TwAlign.End, 18)]
    public void ChipAlignment_places_chips_vertically(TwAlign alignment, int expectedTop) => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = new TwChipStrip { Dock = DockStyle.Top, Height = 48, Padding = new Padding(0, 4, 0, 4), ChipAlignment = alignment };
        strip.Controls.Add(Chip());
        form.Controls.Add(strip);
        form.Show();
        Application.DoEvents();

        Assert.Equal(expectedTop, strip.Chips[0].Top);
    });

    [Fact]
    public void Item_font_classes_start_from_the_family_of_the_menu_it_joins_later() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var menu = new ContextMenuStrip { Font = new Font("Arial", 9f) };
        var header = new ToolStripMenuItem("Başlık");
        provider.SetClasses(header, "text-[11px] font-semibold");
        menu.Items.Add(header);

        Assert.StartsWith("Arial", header.Font.FontFamily.Name);
        Assert.Equal(8.25f, header.Font.SizeInPoints, 2);

        menu.Font = new Font("Courier New", 9f);
        Assert.StartsWith("Courier New", header.Font.FontFamily.Name);
        Assert.Equal(8.25f, header.Font.SizeInPoints, 2);

        provider.SetClasses(header, "");
        Assert.Equal(menu.Font, header.Font);
    });

    [Theory]
    [InlineData("line")]
    [InlineData("top")]
    [InlineData("left")]
    [InlineData("x1")]
    public void Color_names_that_start_like_a_border_side_still_color_the_whole_border(string name) => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        provider.Theme.SetColor(name, Color.Red, Color.Blue);
        var style = provider.Theme.Parse("border-" + name).Resolve(TwState.None);
        Assert.Equal(Color.Red.ToArgb(), style.BorderColor?.ToArgb());
    });

    [Fact]
    public void Color_name_rule_is_reported_in_the_exception() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var error = Assert.Throws<ArgumentException>(() => provider.Theme.SetColor("ui-card", Color.Red, Color.Blue));
        Assert.Contains("a-z and 0-9", error.Message);
    });

    private static void Invoke(Control control, string method, params object[] args) =>
        control.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null,
            args.Select(a => a.GetType()).ToArray(), null)!.Invoke(control, args);

    [Fact]
    public void Info_tip_click_raises_Click_without_the_card_when_asked() => Sta.Run(() =>
    {
        using var form = HostForm();
        var tip = new TwInfoTip { Title = "OEE", Description = "Kullanılabilirlik × performans × kalite.", ClickOpensCard = false };
        int clicks = 0;
        tip.Click += (_, _) => clicks++;
        form.Controls.Add(tip);
        form.Show();
        Application.DoEvents();

        Invoke(tip, "OnClick", EventArgs.Empty);
        Assert.Equal(1, clicks);
        Assert.False(tip.IsOpen);

        Invoke(tip, "OnKeyDown", new KeyEventArgs(Keys.Enter));
        Assert.Equal(2, clicks);
        Assert.False(tip.IsOpen);

        Invoke(tip, "OnKeyDown", new KeyEventArgs(Keys.Space));
        Assert.True(tip.IsOpen);
        Invoke(tip, "OnClick", EventArgs.Empty);
        Assert.False(tip.IsOpen);
        Assert.Equal(3, clicks);
    });

    private static (DataGridView Grid, DataView View, TwSelectColumn Line) SelectGrid(Form form)
    {
        var table = new DataTable();
        table.Columns.Add("Category", typeof(string));
        table.Columns.Add("Line", typeof(string));
        table.Rows.Add("Pres", "P1");
        table.Rows.Add("Kaynak", DBNull.Value);
        table.Rows.Add("Pres", DBNull.Value);
        table.AcceptChanges();

        var view = new DataView(table);
        var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, RowHeadersVisible = false };
        grid.RowTemplate.Height = 40;
        var category = new DataGridViewTextBoxColumn { DataPropertyName = "Category", ReadOnly = true };
        var line = new TwSelectColumn { DataPropertyName = "Line", Width = 200 };
        line.Items.AddRange(new object[] { "P1", "P2", "K1" });
        grid.Columns.AddRange(category, line);
        grid.DataSource = view;
        form.Controls.Add(grid);
        form.Show();
        form.Activate();
        Application.DoEvents();
        return (grid, view, line);
    }

    [Fact]
    public void Select_list_does_not_open_when_CellBeginEdit_cancels() => Sta.Run(() =>
    {
        using var form = HostForm();
        var (grid, _, _) = SelectGrid(form);
        int ended = 0;
        grid.CellBeginEdit += (_, e) => e.Cancel = e.RowIndex == 1;
        grid.CellEndEdit += (_, _) => ended++;

        ((TwSelectCell)grid[1, 1]).ShowDropDown(1);
        Assert.Null(TwSelectCell.OpenMenu);

        ((TwSelectCell)grid[1, 0]).ShowDropDown(0);
        var menu = TwSelectCell.OpenMenu;
        Assert.NotNull(menu);
        menu!.Close();
        Application.DoEvents();
        Assert.Equal(1, ended);
    });

    [Fact]
    public void Typing_does_not_choose_when_CellBeginEdit_cancels() => Sta.Run(() =>
    {
        using var form = HostForm();
        var (grid, view, _) = SelectGrid(form);
        grid.CellBeginEdit += (_, e) => e.Cancel = true;
        grid.CurrentCell = grid[1, 1];
        grid.Focus();

        var keyPress = new KeyPressEventArgs('k');
        typeof(DataGridViewCell).GetMethod("OnKeyPress", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(grid[1, 1], new object[] { keyPress, 1 });
        Assert.Equal(DBNull.Value, view[1]["Line"]);
    });

    [Fact]
    public void Validate_runs_for_every_painted_row_including_empty_values() => Sta.Run(() =>
    {
        using var form = HostForm();
        var (grid, _, line) = SelectGrid(form);
        var validated = new System.Collections.Generic.List<object?>();
        line.Validate = value => { validated.Add(value); return "Zorunlu"; };

        // Shared rows (Index = -1) used to pass for the new row while AllowUserToAddRows = false (NewRowIndex = -1).
        using (var picture = new Bitmap(grid.Width, grid.Height)) grid.DrawToBitmap(picture, new Rectangle(Point.Empty, grid.Size));

        Assert.Contains("P1", validated);
        Assert.Contains(null, validated);
        for (int row = 0; row < 3; row++)
            Assert.Equal("Zorunlu", ((TwSelectCell)grid.Rows.SharedRow(row).Cells[1]).GetError(row));
    });

    [Fact]
    public void Rows_beyond_the_bound_list_are_skipped_while_the_grid_catches_up() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(640, 200) };
        var table = new DataTable();
        table.Columns.Add("Line", typeof(string));
        for (int i = 0; i < 3; i++) table.Rows.Add("P1");
        var view = new DataView(table);
        int? stale = null;
        DataGridView? grid = null;
        TwSelectColumn? line = null;
        var calls = new System.Collections.Generic.List<int>();
        // Bound before the grid: runs while the grid still has the old row count.
        // Reads through the shared row: unsharing a row the list no longer has would break the grid's next painting.
        System.ComponentModel.ListChangedEventHandler onChanged = (_, e) =>
        {
            if (grid is null || line is null || e.ListChangedType != System.ComponentModel.ListChangedType.Reset) return;
            stale = grid.Rows.Count - 1;
            Assert.True(stale >= view.Count);
            Assert.Same(line.Items, line.ItemsAt(stale.Value));
            Assert.Null(((TwSelectCell)grid.Rows.SharedRow(stale.Value).Cells[0]).GetError(stale.Value));
        };
        view.ListChanged += onChanged;
        // Errors are collected instead of opening the grid's (modal) error dialog.
        var errors = new System.Collections.Generic.List<Exception>();
        System.Threading.ThreadExceptionEventHandler onError = (_, e) => errors.Add(e.Exception);
        Application.ThreadException += onError;
        try
        {
            grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false };
            grid.DataError += (_, e) => errors.Add(e.Exception ?? new InvalidOperationException("DataError without an exception"));
            line = new TwSelectColumn { DataPropertyName = "Line", Validate = _ => "x" };
            line.Items.Add("P1");
            line.ItemsFor = row => { calls.Add(row); _ = view[row]["Line"]; return null; };
            grid.Columns.Add(line);
            grid.DataSource = view;
            form.Controls.Add(grid);
            form.Show();
            Application.DoEvents();

            calls.Clear();
            view.RowFilter = "1 = 0";
            view.ListChanged -= onChanged;
            Application.DoEvents();
            grid.DataSource = null;
        }
        finally
        {
            view.ListChanged -= onChanged;
            Application.ThreadException -= onError;
        }
        Assert.Empty(errors.Select(e => e.ToString()));
        Assert.NotNull(stale);
        Assert.DoesNotContain(stale!.Value, calls);
    });
}
