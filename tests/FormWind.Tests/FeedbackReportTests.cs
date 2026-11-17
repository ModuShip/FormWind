using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// 0.1.9 user feedback: public members the tests reached by reflection, a grid select column good enough for in-table
/// choices (per-row lists, pop-up look, immediate commit), classes on menu items, light/dark color pairs and the capture
/// path of transitions.
/// </summary>
[Collection(UiCollection.Name)]
public class FeedbackReportTests
{
    private static Form HostForm(int width = 640, int height = 300) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(width, height) };

    [Fact]
    public void Frame_message_id_is_the_message_IsFrameMessage_accepts()
    {
        var message = Message.Create(IntPtr.Zero, TwDiagnostics.FrameMessageId, IntPtr.Zero, IntPtr.Zero);
        Assert.True(TwDiagnostics.IsFrameMessage(message));
        Assert.Equal(0x82F1, TwDiagnostics.FrameMessageId);
    }

    [Fact]
    public void Dpi_scale_converts_class_pixels_to_device_pixels() => Sta.Run(() =>
    {
        using var panel = new Panel();
        using (TwDpi.Simulate(144)) Assert.Equal(12, TwDpi.Scale(panel, 8));
        using (TwDpi.Simulate(120)) Assert.Equal(10, TwDpi.Scale(panel, 8));
        Assert.Throws<ArgumentNullException>(() => TwDpi.Scale(null!, 8));
    });

    [Fact]
    public void Select_menu_style_reports_the_resolved_classes() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = HostForm();
        var select = new TwSelect { Size = new Size(160, 30) };
        select.Items.AddRange(new[] { "Bir", "İki" });
        form.Controls.Add(select);
        provider.SetClasses(select, "menu:bg-red-500 item:h-8");
        form.Show();
        Application.DoEvents();

        var style = select.MenuStyle;
        Assert.Equal(ColorTranslator.FromHtml("#ef4444").ToArgb(), style.Back.ToArgb());
        Assert.Equal((int)Math.Round(32 * TwDpi.DeviceDpi(select) / 96f), style.ItemHeight);

        select.ShowDropDown();
        try
        {
            var open = TwMenuStyle.Of(select.DropDownMenu);
            Assert.NotNull(open);
            Assert.Equal(style.Back, open!.Back);
            Assert.Null(TwMenuStyle.Of(new ContextMenuStrip()));
        }
        finally
        {
            select.DropDownMenu?.Close();
        }
    });

    private static (DataGridView Grid, DataTable Table, TwSelectColumn Line) LineGrid(Form form)
    {
        var table = new DataTable();
        table.Columns.Add("Category", typeof(string));
        table.Columns.Add("Line", typeof(string));
        table.Rows.Add("Pres", "P1");
        table.Rows.Add("Kaynak", null);
        table.AcceptChanges();

        var lines = new Dictionary<string, List<object>>
        {
            ["Pres"] = new List<object> { "P1", "P2" },
            ["Kaynak"] = new List<object> { "K1", "K2", "K3" },
        };
        var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, RowHeadersVisible = false };
        grid.RowTemplate.Height = 40;
        var category = new DataGridViewTextBoxColumn { DataPropertyName = "Category", ReadOnly = true };
        var line = new TwSelectColumn { DataPropertyName = "Line", Width = 200 };
        line.Items.Add("—");
        line.ItemsFor = row => lines.TryGetValue(grid.Rows[row].Cells[0].Value as string ?? "", out var list) ? list : null;
        grid.Columns.AddRange(category, line);
        grid.DataSource = table;
        form.Controls.Add(grid);
        form.Show();
        form.Activate();
        Application.DoEvents();
        return (grid, table, line);
    }

    [Fact]
    public void Select_column_lists_the_choices_of_each_row() => Sta.Run(() =>
    {
        using var form = HostForm();
        var (grid, _, line) = LineGrid(form);

        Assert.Equal(new object[] { "P1", "P2" }, line.ItemsAt(0));
        Assert.Equal(new object[] { "K1", "K2", "K3" }, line.ItemsAt(1));
        Assert.Same(line.Items, line.ItemsAt(-1));

        ((TwSelectCell)grid[1, 1]).ShowDropDown(1);
        var menu = TwSelectCell.OpenMenu;
        Assert.NotNull(menu);
        Assert.Equal(new[] { "K1", "K2", "K3" }, menu!.Items.Cast<ToolStripItem>().Select(i => i.Text));
        Assert.NotNull(TwMenuStyle.Of(menu));
        menu.Close();

        // Clone ItemsFor ve CommitOnSelect'i taşır.
        line.CommitOnSelect = true;
        var clone = (TwSelectColumn)line.Clone();
        Assert.Same(line.ItemsFor, clone.ItemsFor);
        Assert.True(clone.CommitOnSelect);
    });

    [Fact]
    public void Select_column_commits_the_choice_to_the_data_row_when_asked() => Sta.Run(() =>
    {
        using var form = HostForm();
        var (grid, table, line) = LineGrid(form);
        line.CommitOnSelect = true;

        grid.CurrentCell = grid[1, 1];
        ((TwSelectCell)grid[1, 1]).ShowDropDown(1);
        var menu = TwSelectCell.OpenMenu!;
        menu.Items[1].PerformClick();
        menu.Close();

        Assert.Equal("K2", table.Rows[1]["Line"]);
        Assert.Equal(DataRowState.Modified, table.Rows[1].RowState);
        Assert.Equal(DataRowState.Unchanged, table.Rows[0].RowState);
        Assert.Equal("K2", grid[1, 1].FormattedValue);
    });

    [Fact]
    public void Select_column_without_commit_keeps_the_grid_timing() => Sta.Run(() =>
    {
        using var form = HostForm();
        var (grid, table, _) = LineGrid(form);

        grid.CurrentCell = grid[1, 1];
        ((TwSelectCell)grid[1, 1]).ShowDropDown(1);
        var menu = TwSelectCell.OpenMenu!;
        menu.Items[0].PerformClick();
        menu.Close();

        Assert.Equal("K1", table.Rows[1]["Line"]);
        Assert.Equal(DataRowState.Unchanged, table.Rows[1].RowState);
    });

    private static Color TextColorOf(TwToolStripRenderer renderer, ToolStripItem item)
    {
        using var bitmap = new Bitmap(100, 20);
        using var g = Graphics.FromImage(bitmap);
        var e = new ToolStripItemTextRenderEventArgs(g, item, item.Text, new Rectangle(0, 0, 100, 20), Color.Black, item.Font, TextFormatFlags.Default);
        renderer.DrawItemText(e);
        return e.TextColor;
    }

    [Fact]
    public void Menu_item_classes_color_the_item_in_the_menu_state() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var archive = new ToolStripMenuItem("Arşivle");
        using var plain = new ToolStripMenuItem("Aç");
        provider.SetClasses(archive, "text-red-600 dark:text-red-400");
        Assert.Equal("text-red-600 dark:text-red-400", provider.GetClasses(archive));
        Assert.Equal("", provider.GetClasses(plain));

        var light = new TwToolStripRenderer(new TwMenuColors());
        var dark = new TwToolStripRenderer(new TwMenuColors { State = TwState.Dark, Fore = Color.White });
        Assert.Equal(ColorTranslator.FromHtml("#dc2626").ToArgb(), TextColorOf(light, archive).ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#f87171").ToArgb(), TextColorOf(dark, archive).ToArgb());
        Assert.Equal(Color.White.ToArgb(), TextColorOf(dark, plain).ToArgb());

        archive.Enabled = false;
        Assert.Equal(128, TextColorOf(light, archive).A);

        provider.SetClasses(archive, null);
        Assert.Equal("", provider.GetClasses(archive));
        archive.Enabled = true;
        Assert.Equal(new TwMenuColors().Fore.ToArgb(), TextColorOf(light, archive).ToArgb());
    });

    [Fact]
    public void Menu_item_font_classes_set_the_font_and_removing_them_restores_it() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        provider.Theme.SetComponent("menu-header", "text-[10px] font-semibold text-gray-500");
        using var menu = new ContextMenuStrip();
        var header = new ToolStripLabel("Duruş #1042");
        menu.Items.Add(header);
        var original = header.Font;

        provider.SetClasses(header, "menu-header");
        // font-semibold: yarı kalın aile ("Segoe UI Semibold") ya da yoksa kalın stil.
        Assert.True(header.Font.Bold || header.Font.Name.Contains("Semibold"), header.Font.ToString());
        Assert.True(header.Font.SizeInPoints < original.SizeInPoints);

        provider.SetClasses(header, "");
        Assert.Equal(original, header.Font);
        Assert.Throws<ArgumentNullException>(() => provider.SetClasses((ToolStripItem)null!, "x"));
    });

    [Fact]
    public void Light_dark_color_pair_resolves_per_mode_with_variants_and_opacity()
    {
        var theme = new TwTheme();
        theme.SetColor("menu", light: "#F8F8FA", dark: "#2C2C2F");
        theme.SetColor("menuline", ColorTranslator.FromHtml("#E2E2E4"), ColorTranslator.FromHtml("#3F3F42"));
        var light = ColorTranslator.FromHtml("#F8F8FA");
        var dark = ColorTranslator.FromHtml("#2C2C2F");

        var parsed = theme.Parse("bg-menu border-menuline hover:bg-menu/50");
        Assert.Empty(parsed.Unknown);
        Assert.Equal(light.ToArgb(), parsed.Resolve(TwState.None).BackColor!.Value.ToArgb());
        Assert.Equal(dark.ToArgb(), parsed.Resolve(TwState.Dark).BackColor!.Value.ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#3F3F42").ToArgb(), parsed.Resolve(TwState.Dark).BorderColor!.Value.ToArgb());
        var hoverDark = parsed.Resolve(TwState.Dark | TwState.Hover).BackColor!.Value;
        Assert.Equal(Color.FromArgb(128, dark).ToArgb(), hoverDark.ToArgb());

        // dark: altında doğrudan koyu değer; sonra gelen dark: sınıfı yine kazanır.
        Assert.Equal(dark.ToArgb(), theme.Parse("dark:text-menu").Resolve(TwState.Dark).ForeColor!.Value.ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#ef4444").ToArgb(),
            theme.Parse("bg-menu dark:bg-red-500").Resolve(TwState.Dark).BackColor!.Value.ToArgb());

        // Bileşen içinden de çalışır; tek renkle yeniden tanımlamak çifti kaldırır.
        theme.SetComponent("panel-menu", "bg-menu");
        Assert.Equal(dark.ToArgb(), theme.Parse("panel-menu").Resolve(TwState.Dark).BackColor!.Value.ToArgb());
        Assert.True(theme.TryGetDarkColor("menu", "", out _));
        theme.SetColor("menu", Color.Red);
        Assert.False(theme.TryGetDarkColor("menu", "", out _));
        Assert.Equal(Color.Red.ToArgb(), theme.Parse("bg-menu").Resolve(TwState.Dark).BackColor!.Value.ToArgb());
        Assert.Throws<FormatException>(() => theme.SetColor("x", "nope", "#000"));
    }

    [Theory]
    [InlineData(TwTransitionCapture.Drawing, "DrawToBitmap")]
    [InlineData(TwTransitionCapture.None, "Background")]
    public void Cross_fade_capture_mode_chooses_how_the_old_look_is_taken(TwTransitionCapture capture, string source) => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(100, 100), Size = new Size(400, 300) };
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        form.Controls.Add(page);
        form.Show();
        Application.DoEvents();

        string? reported = null;
        EventHandler<TwTransitionEventArgs> listener = (_, e) => reported = e.CaptureSource;
        TwDiagnostics.TransitionCompleted += listener;
        bool completed = false;
        try
        {
            TwTransition.CrossFade(page, () => page.BackColor = Color.Black,
                new TwMotionOptions { Capture = capture, DurationMs = 60, Completed = () => completed = true });
        }
        finally
        {
            TwDiagnostics.TransitionCompleted -= listener;
        }
        Assert.True(TwTransition.LastCovered);
        Assert.Equal(source, reported);
        Assert.Equal(Color.Black, page.BackColor);
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!completed && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(10);
        }
        Assert.True(completed);
        form.Close();
    });

    [Fact]
    public void Select_cell_draws_the_arrow_square_from_its_classes() => Sta.Run(() =>
    {
        using var grid = new DataGridView();
        Assert.True(TwInputCell.Colors(grid, "", false, false).ArrowBack.IsEmpty);
        Assert.Equal(ColorTranslator.FromHtml("#2563eb").ToArgb(), TwInputCell.Colors(grid, "accent-blue-600", false, false).ArrowBack.ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#16a34a").ToArgb(),
            TwInputCell.Colors(grid, "accent-blue-600 arrow:bg-green-600", false, false).ArrowBack.ToArgb());
        Assert.True(TwInputCell.Colors(grid, "accent-blue-600 arrow:bg-transparent", false, false).ArrowBack.IsEmpty);

        // Kare, düz ok çizgisinin yerine çizilir: ok alanında kare rengi görünür.
        using var form = HostForm();
        var (lineGrid, _, line) = LineGrid(form);
        line.Classes = "arrow:bg-green-600";
        lineGrid.Invalidate();
        using var bitmap = new Bitmap(lineGrid.Width, lineGrid.Height);
        lineGrid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, lineGrid.Size));
        var cell = lineGrid.GetCellDisplayRectangle(1, 0, false);
        var green = ColorTranslator.FromHtml("#16a34a");
        bool found = false;
        for (int x = cell.Right - 30; x < cell.Right && !found; x++)
            for (int y = cell.Top; y < cell.Bottom && !found; y++)
                found = bitmap.GetPixel(x, y).ToArgb() == green.ToArgb();
        Assert.True(found);
    });
}
