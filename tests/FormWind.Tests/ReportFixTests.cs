using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// 0.1.4-preview.1 geliştirme raporundaki hatalar: ayar satırı, ağaç tablo, araç çubuğu ve DPI'da yazı boyu.
[Collection(UiCollection.Name)]
public class ReportFixTests
{
    private static Form NewForm(int width = 640, int height = 400) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(width, height) };

    [Fact]
    public void Setting_row_gives_an_autosize_editor_its_preferred_width_and_follows_it() => Sta.Run(() =>
    {
        using var form = NewForm(800);
        var tabs = new TwTabStrip { AutoSize = true };
        tabs.Tabs.AddRange(new[] { "Vardiya bazında", "Hat bazında", "Makine bazında" });
        var row = new TwSettingRow { LabelText = "Görünüm", Dock = DockStyle.Top };
        row.Controls.Add(tabs);
        form.Controls.Add(row);
        form.Show();
        Assert.True(tabs.Width >= tabs.GetPreferredSize(Size.Empty).Width);
        Assert.Empty(tabs.OverflowTabs);

        // 0 = düzenleyicinin kendi genişliği; içerik büyüyünce satır yeniden yerleşir.
        var flow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false };
        var status = new Label { AutoSize = true, Text = "Kaydedildi" };
        flow.Controls.Add(status);
        flow.Controls.Add(new Button { AutoSize = true, Text = "Yenile" });
        var second = new TwSettingRow { LabelText = "Durum", EditorWidth = 0, Dock = DockStyle.Top };
        second.Controls.Add(flow);
        form.Controls.Add(second);
        Application.DoEvents();
        Assert.Equal(flow.GetPreferredSize(Size.Empty).Width, flow.Width);
        status.Text = "Son kayıt 12:04 · 3 değişiklik gönderildi";
        Application.DoEvents();
        Assert.Equal(flow.GetPreferredSize(Size.Empty).Width, flow.Width);
        Assert.True(flow.Right <= second.Width - second.Padding.Right);
    });

    [Fact]
    public void Setting_row_editor_width_set_again_from_editor_layout_does_not_loop() => Sta.Run(() =>
    {
        using var form = NewForm();
        var row = new TwSettingRow { LabelText = "Sınır", Dock = DockStyle.Top };
        var box = new TwNumberBox();
        int layouts = 0;
        box.Layout += (_, _) => { layouts++; row.EditorWidth = 120; };
        box.SizeChanged += (_, _) => row.EditorWidth = 120;
        row.Controls.Add(box);
        form.Controls.Add(row);
        form.Show();
        Application.DoEvents();
        Assert.True(layouts < 50);
        Assert.Equal((int)Math.Round(120 * TwDpi.DeviceDpi(row) / 96f), box.Width);

        // Aynı değer yeniden yerleşim istemez.
        int rowLayouts = 0;
        row.Layout += (_, _) => rowLayouts++;
        row.EditorWidth = 120;
        Assert.Equal(0, rowLayouts);
    });

    [Fact]
    public void Setting_row_keeps_classes_given_to_its_labels_when_the_theme_changes() => Sta.Run(() =>
    {
        using var form = NewForm();
        using var tw = new TailwindProvider();
        var row = new TwSettingRow { LabelText = "Vardiya", Description = "Açıklama", Dock = DockStyle.Top };
        row.Controls.Add(new TwNumberBox());
        form.Controls.Add(row);
        // Beklenen font: aynı class'ları alan bağımsız bir etiketinki (varsayılan aile çerçeveye göre değişir).
        var reference = new Label { Text = "x" };
        form.Controls.Add(reference);
        tw.SetClasses(reference, "font-semibold text-[18px]");
        tw.SetClasses(row.CaptionLabel, "font-semibold text-[18px]");
        tw.SetClasses(row, "setting");
        form.Show();
        void AssertClassFont()
        {
            var font = row.CaptionLabel.Font;
            Assert.Equal(reference.Font.Name, font.Name);
            Assert.Equal(reference.Font.Style, font.Style);
            Assert.Equal(reference.Font.SizeInPoints, font.SizeInPoints, 1);
        }
        AssertClassFont();
        Assert.NotEqual(row.Font.SizeInPoints, row.CaptionLabel.Font.SizeInPoints, 1);
        tw.DarkMode = true;
        Application.DoEvents();
        AssertClassFont();
        tw.DarkMode = false;
        AssertClassFont();
    });

    [Fact]
    public void Grid_tree_rows_take_the_class_row_height() => Sta.Run(() =>
    {
        using var form = NewForm();
        using var tw = new TailwindProvider();
        var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
        var name = new DataGridViewTextBoxColumn { HeaderText = "Ad" };
        grid.Columns.Add(name);
        form.Controls.Add(grid);
        tw.SetClasses(grid, "table");
        form.Show();
        int expected = grid.RowTemplate.Height;
        Assert.True(expected > 30);
        var root = new TwGridTreeNode("Hat 1");
        root.Nodes.Add(new TwGridTreeNode("Pres"));
        TwGrid.SetTree(grid, name, new[] { root }).ExpandAll();
        Assert.All(grid.Rows.Cast<DataGridViewRow>(), r => Assert.Equal(expected, r.Height));
    });

    [Fact]
    public void Heatmap_exposes_selection_highlighted_columns_cell_size_and_cell_bounds() => Sta.Run(() =>
    {
        using var form = NewForm(800);
        var heatmap = new TwHeatmap { Location = new Point(0, 0), Size = new Size(600, 200), ShowLegend = false, AnimateOnShow = false };
        heatmap.SetData(new[] { "Hat 1", "Hat 2" }, new[] { "Pzt", "Sal", "Çar" }, new double[,] { { 1, 2, 3 }, { 4, 5, 6 } });
        form.Controls.Add(heatmap);
        form.Show();

        var bounds = heatmap.GetCellBounds(1, 2);
        Assert.False(bounds.IsEmpty);
        Assert.Equal(1 * 3 + 2, heatmap.ItemAt(new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2)));
        Assert.Equal(Rectangle.Empty, heatmap.GetCellBounds(2, 0));

        int changed = 0;
        heatmap.SelectedIndexChanged += (_, _) => changed++;
        TwHeatmapCellEventArgs? clicked = null;
        heatmap.CellClick += (_, e) => clicked = e;
        heatmap.SelectOnClick = true;
        heatmap.Focus();
        heatmap.HoverIndex = 4;
        heatmap.GetType().GetMethod("OnKeyDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(heatmap, new object[] { new KeyEventArgs(Keys.Enter) });
        Assert.Equal(4, heatmap.SelectedIndex);
        Assert.Equal(1, changed);
        Assert.Equal(1, clicked!.Column);
        heatmap.SelectedIndex = 99;
        Assert.Equal(-1, heatmap.SelectedIndex);

        heatmap.HighlightColumns = new[] { 1 };
        Assert.Equal(new[] { 1 }, heatmap.HighlightColumns);

        // Sabit hücre boyu: ızgara sol üstte, tercih edilen boy tüm hücreleri gösterir.
        heatmap.CellSize = new Size(40, 24);
        float s = TwDpi.DeviceDpi(heatmap) / 96f;
        var cell = heatmap.GetCellBounds(0, 1);
        Assert.InRange(cell.Width, (int)(40 * s) - 1, (int)(40 * s) + 1);
        Assert.InRange(cell.Height, (int)(24 * s) - 1, (int)(24 * s) + 1);
        var preferred = heatmap.GetPreferredSize(Size.Empty);
        Assert.True(preferred.Width >= heatmap.GetCellBounds(0, 2).Right);
        Assert.True(preferred.Height >= heatmap.GetCellBounds(1, 0).Bottom);
        using var bitmap = new Bitmap(heatmap.Width, heatmap.Height);
        heatmap.DrawToBitmap(bitmap, new Rectangle(Point.Empty, heatmap.Size));
    });

    [Fact]
    public void Toolbar_shrink_item_gets_its_content_width_when_there_is_room() => Sta.Run(() =>
    {
        using var form = NewForm(1200);
        using var tw = new TailwindProvider();
        var toolbar = new TwToolbar { Dock = DockStyle.Top, Height = 44 };
        var title = new Label { Text = "Kefeli hat · duruş kayıtları", AutoSize = true, AutoEllipsis = true };
        var save = new TwButton { Text = "Kaydet", AutoSize = true };
        toolbar.Controls.AddRange(new Control[] { title, save });
        toolbar.SetOverflow(title, TwToolbarOverflow.Shrink);
        form.Controls.Add(toolbar);
        form.Show();
        tw.SetClasses(title, "font-semibold text-[15px]");
        Application.DoEvents();
        int wanted = title.GetPreferredSize(Size.Empty).Width;
        Assert.True(title.Width >= wanted, $"{title.Width} < {wanted}");
        tw.DarkMode = true;
        Application.DoEvents();
        wanted = title.GetPreferredSize(Size.Empty).Width;
        Assert.True(title.Width >= wanted, $"{title.Width} < {wanted}");

        // Dar çubukta başlık tek harfe inmeden önce öğeler menüye geçer.
        for (int i = 0; i < 5; i++) toolbar.Controls.Add(new TwButton { Text = "Komut " + i, AutoSize = true });
        form.ClientSize = new Size(520, 400);
        Application.DoEvents();
        Assert.True(toolbar.OverflowButton.Visible);
        Assert.True(title.Width >= Math.Min(wanted, (int)Math.Round(96 * TwDpi.DeviceDpi(toolbar) / 96f)), title.Width.ToString());
    });

    [Fact]
    public void Chip_preferred_size_uses_the_class_height() => Sta.Run(() =>
    {
        using var form = NewForm();
        using var tw = new TailwindProvider();
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var chip = new TwChip { Text = "Açık", AutoSize = true };
        flow.Controls.Add(chip);
        form.Controls.Add(flow);
        tw.SetClasses(chip, "chip h-[26px]");
        form.Show();
        Application.DoEvents();
        int expected = (int)Math.Round(26 * TwDpi.DeviceDpi(chip) / 96f);
        Assert.Equal(expected, chip.GetPreferredSize(Size.Empty).Height);
        Assert.Equal(expected, chip.Height);
        tw.SetClasses(chip, "chip");
        Assert.NotEqual(expected, chip.GetPreferredSize(Size.Empty).Height);
    });

    [Fact]
    public void Dialog_takes_the_application_surface_and_a_button_text() => Sta.Run(() =>
    {
        var surface = ColorTranslator.FromHtml("#2a2a2c");
        TwDialog.DarkBackColor = surface;
        try
        {
            using var dark = TwDialog.Create(new TwDialogOptions { Message = "Ad", Dark = true, ShowInput = true, OkText = "Sakla", InputMaxLength = 12 });
            Assert.Equal(surface.ToArgb(), dark.BackColor.ToArgb());
            Assert.Equal("Sakla", dark.Buttons.Last().Text);
            Assert.Equal(12, dark.InputBox!.MaxLength);
            using var light = TwDialog.Create(new TwDialogOptions { Message = "Ad" });
            Assert.Equal(Color.White.ToArgb(), light.BackColor.ToArgb());
            using var own = TwDialog.Create(new TwDialogOptions { Message = "Ad", Dark = true, BackColor = Color.FromArgb(30, 30, 32) });
            Assert.Equal(Color.FromArgb(30, 30, 32).ToArgb(), own.BackColor.ToArgb());
        }
        finally
        {
            TwDialog.DarkBackColor = Color.Empty;
        }
    });

    [Fact]
    public void Unknown_icon_name_is_reported_as_a_style_error() => Sta.Run(() =>
    {
        using var form = NewForm();
        using var tw = new TailwindProvider();
        var errors = new System.Collections.Generic.List<TwStyleErrorEventArgs>();
        tw.StyleError += (_, e) => errors.Add(e);
        var icon = new TwIcon { IconName = "sidebar" };
        var known = new TwIcon { IconName = "settings" };
        form.Controls.Add(icon);
        form.Controls.Add(known);
        tw.SetClasses(icon, "text-xl");
        tw.SetClasses(known, "text-xl");
        var error = Assert.Single(errors);
        Assert.Same(icon, error.Target);
        Assert.Contains("sidebar", error.Exception.Message);
        Assert.False(TwIcons.IsKnown("sidebar"));
        Assert.True(TwIcons.IsKnown("#E710"));
        Assert.True(TwIcons.IsKnown(""));
    });

    [Fact]
    public void Grid_tree_custom_total_computes_a_ratio_from_children() => Sta.Run(() =>
    {
        using var form = NewForm();
        var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
        var name = new DataGridViewTextBoxColumn();
        var solved = new DataGridViewTextBoxColumn();
        var total = new DataGridViewTextBoxColumn();
        var rate = new DataGridViewTextBoxColumn();
        grid.Columns.AddRange(name, solved, total, rate);
        form.Controls.Add(grid);
        var line = new TwGridTreeNode("Hat 2", null, null, null);
        line.Nodes.Add(new TwGridTreeNode("Pres", 3, 4, 0.75));
        line.Nodes.Add(new TwGridTreeNode("Testere", 1, 4, 0.25));
        var tree = TwGrid.SetTree(grid, name, new[] { line });
        tree.SetTotal(solved, TwAggregate.Sum);
        tree.SetTotal(total, TwAggregate.Sum);
        tree.SetTotal(rate, nodes => nodes.Sum(n => System.Convert.ToDouble(tree.GetValue(n, solved))) / nodes.Sum(n => System.Convert.ToDouble(tree.GetValue(n, total))));
        Assert.Equal(0.5, System.Convert.ToDouble(tree.GetValue(line, rate)), 3);
        Assert.Equal(0.5, System.Convert.ToDouble(grid.Rows[0].Cells[rate.Index].Value), 3);
        tree.SetTotal(rate, (Func<System.Collections.Generic.IEnumerable<TwGridTreeNode>, object?>?)null);
        Assert.Null(tree.GetValue(line, rate));
    });

    [Fact]
    public void Stat_value_scale_unit_and_hidden_trend() => Sta.Run(() =>
    {
        using var form = NewForm();
        var stat = new TwStat { Title = "Duruş", Value = "1.240", Unit = "dk", Size = new Size(240, 140), AnimateValue = false, Trend = new[] { 1.0, 3, 2 } };
        form.Controls.Add(stat);
        form.Show();
        float full = stat.FitValue(400).Points;
        stat.ValueScale = 1.5f;
        Assert.True(stat.FitValue(400).Points < full);
        Assert.Equal(1.5f * stat.Font.SizeInPoints, stat.FitValue(400).Points, 1);
        Assert.Contains("1.240 dk", stat.AccessibilityObject.Value);
        stat.ShowTrend = false;
        using var bitmap = new Bitmap(stat.Width, stat.Height);
        stat.DrawToBitmap(bitmap, new Rectangle(Point.Empty, stat.Size));
    });

    [Fact]
    public void Chip_colors_cross_fade_when_its_classes_change() => Sta.Run(() =>
    {
        using var form = NewForm();
        using var tw = new TailwindProvider();
        var chip = new TwChip { Text = "Açık", AutoSize = true, Location = new Point(10, 10) };
        form.Controls.Add(chip);
        tw.SetClasses(chip, "chip-warning");
        form.Show();
        Application.DoEvents();
        var before = chip.BackColor;
        var target = tw.GetColors("chip-success").Back;
        tw.SetClasses(chip, "chip-success");      // chip bileşeni transition-colors içerir
        Assert.NotEqual(target?.ToArgb(), chip.BackColor.ToArgb());     // geçiş başladı, renk henüz hedefte değil
        Assert.NotEqual(before.ToArgb(), target?.ToArgb());
    });

    [Fact]
    public void Heatmap_legend_can_live_in_its_own_control() => Sta.Run(() =>
    {
        using var form = NewForm();
        var heatmap = new TwHeatmap { Size = new Size(300, 120), AnimateOnShow = false, EmptyCellText = "Üretim yok", EmptyCells = TwHeatmapEmptyCell.Hatched };
        heatmap.SetData(new[] { "A" }, new[] { "1", "2" }, new double[,] { { 1, double.NaN } });
        var legend = heatmap.CreateLegend();
        Assert.False(heatmap.ShowLegend);
        Assert.Same(heatmap, legend.Heatmap);
        form.Controls.Add(heatmap);
        form.Controls.Add(legend);
        form.Show();
        int withEmpty = legend.GetPreferredSize(Size.Empty).Width;
        Assert.True(withEmpty > 100);
        heatmap.EmptyCellText = "";
        Application.DoEvents();
        Assert.True(legend.GetPreferredSize(Size.Empty).Width < withEmpty);
        Assert.Equal(legend.GetPreferredSize(Size.Empty), legend.Size);
        using var bitmap = new Bitmap(legend.Width, legend.Height);
        legend.DrawToBitmap(bitmap, new Rectangle(Point.Empty, legend.Size));
    });

    [Fact]
    public void List_box_applies_many_changes_once_between_begin_and_end_update() => Sta.Run(() =>
    {
        using var form = NewForm();
        var list = new TwCheckedListBox { Dock = DockStyle.Fill };
        form.Controls.Add(list);
        form.Show();
        list.Items.Add("ilk");
        list.SetItemChecked(0, true);
        list.BeginUpdate();
        list.BeginUpdate();
        for (int i = 0; i < 500; i++) list.Items.Add("Satır " + i);
        list.EndUpdate();
        Assert.Equal(1, list.ItemCount);                     // iç içe: henüz uygulanmadı
        list.EndUpdate();
        Assert.Equal(501, list.ItemCount);
        Assert.True(list.GetItemChecked(0));
        Assert.False(list.GetItemChecked(500));
        list.EndUpdate();                                    // fazladan çağrı zararsız
    });

    [Fact]
    public void Capture_and_fade_out_are_public() => Sta.Run(() =>
    {
        using var form = NewForm(200, 120);
        form.BackColor = Color.FromArgb(200, 30, 30);
        form.Location = new Point(40, 40);
        form.Show();
        Application.DoEvents();
        using var picture = TwCapture.Take(form);
        Assert.NotNull(picture);
        Assert.Equal(TwCapture.VisibleFormBounds(form).Size, picture!.Size);
        Assert.Null(TwCapture.Take(new Panel()));
        TwFadeOut.Play(form, null, 0, roundCorners: false);  // 0 ms: hiçbir şey yapmaz
    });

    [Fact]
    public void Setting_row_editor_keeps_the_page_font_and_rows_in_a_card_are_not_indented_twice() => Sta.Run(() =>
    {
        using var form = NewForm(600);
        using var tw = new TailwindProvider();
        var card = new TwPanel { Dock = DockStyle.Top, Height = 160, Padding = new Padding(16) };
        var row = new TwSettingRow { LabelText = "Sınır", Dock = DockStyle.Top };
        var box = new TwTextBox();
        row.Controls.Add(box);
        card.Controls.Add(row);
        form.Controls.Add(card);
        tw.SetClasses(row, "setting text-[15px] h-[64px]");
        form.Show();
        Application.DoEvents();
        Assert.NotEqual(row.Font.SizeInPoints, box.Font.SizeInPoints, 1);
        Assert.Equal(card.Font.SizeInPoints, box.Font.SizeInPoints, 1);
        Assert.Equal(0, row.CaptionLabel.Left);              // card'ın dolgusu yeterli
        Assert.Equal((int)Math.Round(64 * TwDpi.DeviceDpi(row) / 96f), row.Height);

        // Uygulamanın düzenleyiciye verdiği font korunur.
        var own = new Font("Segoe UI", 14f);
        box.Font = own;
        tw.SetClasses(row, "setting text-[13px]");
        Assert.Same(own, box.Font);
    });

    [Fact]
    public void Card_strip_pages_cards_that_do_not_fit() => Sta.Run(() =>
    {
        TwMotion.Enabled = false;
        try
        {
            using var form = NewForm(700, 200);
            var strip = new TwCardStrip { Dock = DockStyle.Fill, MinCardWidth = 200, Gap = 10 };
            var cards = Enumerable.Range(0, 7).Select(i => new TwStat { Title = "Hat " + i }).ToArray();
            strip.Controls.AddRange(cards);
            form.Controls.Add(strip);
            form.Show();

            int perPage = strip.CardsPerPage;
            Assert.True(perPage >= 2 && perPage < 7, perPage.ToString());
            Assert.Equal((int)Math.Ceiling(7 / (double)perPage), strip.PageCount);
            Assert.True(strip.NextButton.Visible);
            Assert.False(strip.PreviousButton.Enabled);
            Assert.Equal("1 / " + strip.PageCount, strip.PageLabel.Text);
            Assert.Equal(perPage, cards.Count(c => c.Visible));
            Assert.True(cards[0].Width >= strip.MinCardWidth * TwDpi.DeviceDpi(strip) / 96f - 1);
            // Kartlar butonların altında; aynı boyda.
            Assert.True(cards[0].Top >= strip.NextButton.Bottom);
            Assert.Equal(cards[0].Width, cards[1].Width, 1.0);

            int changed = 0;
            strip.PageChanged += (_, _) => changed++;
            strip.NextButton.PerformClick();
            Assert.Equal(1, strip.PageIndex);
            Assert.Equal(1, changed);
            Assert.False(cards[0].Visible);
            Assert.True(cards[perPage].Visible);
            Assert.True(strip.PreviousButton.Enabled);

            strip.ShowCard(cards[6]);
            Assert.Equal(strip.PageCount - 1, strip.PageIndex);
            Assert.False(strip.NextButton.Enabled);
            strip.PageIndex = 99;
            Assert.Equal(strip.PageCount - 1, strip.PageIndex);

            // Genişleyince her şey tek sayfaya sığar: düğmeler gizlenir, sayfa geri çekilir.
            // Form ekrandan geniş olamaz (CI ekranı dar), bu yüzden şerit doğrudan genişletilir.
            strip.Dock = DockStyle.None;
            strip.Width = (int)(7 * 230 * TwDpi.DeviceDpi(strip) / 96f);
            Assert.Equal(1, strip.PageCount);
            Assert.Equal(0, strip.PageIndex);
            Assert.False(strip.NextButton.Visible);
            Assert.All(cards, c => Assert.True(c.Visible));
        }
        finally
        {
            TwMotion.Enabled = true;
        }
    });

    [Fact]
    public void Stacked_bar_100_splits_each_row_into_shares() => Sta.Run(() =>
    {
        using var form = NewForm(600, 300);
        var chart = new TwChart { Dock = DockStyle.Fill, Kind = TwChartKind.StackedBar100 };
        chart.SetData(new[] { "Vardiya A", "Vardiya B" },
            new TwChartSeries("Arıza", new[] { 30.0, 10 }),
            new TwChartSeries("Ayar", new[] { 10.0, -5 }),
            new TwChartSeries("Malzeme", new[] { 60.0, 30 }));
        form.Controls.Add(chart);
        form.Show();

        var layout = chart.ComputeHorizontalLayout();
        Assert.Equal(0, layout.Min);
        Assert.Equal(1, layout.Max);
        for (int row = 0; row < 2; row++)
        {
            var bars = Enumerable.Range(0, 3).Select(i => chart.HorizontalBarBounds(layout, i, row)).Where(b => !b.IsEmpty).ToList();
            // Satır tüm çizim genişliğini doldurur; parçalar art arda.
            Assert.Equal(layout.Plot.Left, bars.Min(b => b.Left), 1.5);
            Assert.Equal(layout.Plot.Right, bars.Max(b => b.Right), 1.5);
        }
        var a = chart.HorizontalBarBounds(layout, 0, 0);
        Assert.Equal(layout.Plot.Width * 0.3, a.Width, 1.5);
        // Negatif pay 0 sayılır: B satırında Arıza 10/40.
        var b0 = chart.HorizontalBarBounds(layout, 0, 1);
        Assert.Equal(layout.Plot.Width * 0.25, b0.Width, 1.5);
        using var bitmap = new Bitmap(chart.Width, chart.Height);
        chart.DrawToBitmap(bitmap, new Rectangle(Point.Empty, chart.Size));
    });

    private sealed class Entry
    {
        public string? Note { get; set; }
        public int? Cause { get; set; }
    }

    private sealed class Cause
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    [Fact]
    public void Editable_columns_draw_inputs_validate_and_edit_in_the_focused_cell() => Sta.Run(() =>
    {
        using var form = NewForm(640, 300);
        var data = new System.ComponentModel.BindingList<Entry> { new Entry { Note = "Kayış koptu", Cause = 2 }, new Entry() };
        var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, RowHeadersVisible = false };
        grid.RowTemplate.Height = 40;
        var note = new TwTextColumn { DataPropertyName = "Note", HeaderText = "Açıklama", Width = 240, Placeholder = "Not ekleyin",
            Validate = v => string.IsNullOrWhiteSpace(v as string) ? "Zorunlu" : null };
        var cause = new TwSelectColumn { DataPropertyName = "Cause", HeaderText = "Neden", Width = 200, Placeholder = "Seçin",
            DisplayMember = "Name", ValueMember = "Id" };
        cause.Items.AddRange(new object[] { new Cause { Id = 1, Name = "Arıza" }, new Cause { Id = 2, Name = "Ayar" }, new Cause { Id = 3, Name = "Malzeme yok" } });
        grid.Columns.AddRange(note, cause);
        grid.DataSource = data;
        form.Controls.Add(grid);
        form.Show();
        form.Activate();

        // Doğrulama: boş açıklama hatalı; hata metni hücrenin hatası olur.
        Assert.Null(((TwTextCell)grid[0, 0]).GetError(0));
        Assert.Equal("Zorunlu", ((TwTextCell)grid[0, 1]).GetError(1));
        Assert.Equal("Zorunlu", grid[0, 1].ErrorText);
        // Seçim sütunu değeri değil öğenin adını gösterir.
        Assert.Equal("Ayar", grid[1, 0].FormattedValue);
        Assert.Equal("", grid[1, 1].FormattedValue);
        Assert.Null(grid[1, 0].EditType);
        using (var bitmap = new Bitmap(grid.Width, grid.Height)) grid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, grid.Size));

        // Odaktaki metin hücresi gerçek düzenleyiciyi açar; düzenleyici kutunun içinde kalır.
        grid.Focus();
        grid.CurrentCell = grid[0, 1];
        Application.DoEvents();
        Assert.True(grid.IsCurrentCellInEditMode);
        var cellBounds = grid.GetCellDisplayRectangle(0, 1, false);
        var panel = grid.EditingPanel.Bounds;
        Assert.True(cellBounds.Contains(panel), cellBounds + " / " + panel);
        Assert.True(panel.Width < cellBounds.Width - 8 && panel.Height < cellBounds.Height - 8, panel.ToString());
        grid.EditingControl!.Text = "Yeni not";
        Assert.True(grid.EndEdit());
        Assert.Equal("Yeni not", data[1].Note);
        Assert.Null(((TwTextCell)grid[0, 1]).GetError(1));

        // Seçim hücresi menüyle değişir; değer ValueMember'dır.
        grid.CurrentCell = grid[1, 1];
        ((TwSelectCell)grid[1, 1]).ShowDropDown(1);
        var menu = TwSelectCell.OpenMenu;
        Assert.NotNull(menu);
        Assert.Equal(3, menu!.Items.Count);
        menu.Items[2].PerformClick();
        Assert.Equal(3, data[1].Cause);
        Assert.Equal("Malzeme yok", grid[1, 1].FormattedValue);
        menu.Close();

        // Salt okunur sütunda menü açılmaz.
        cause.ReadOnly = true;
        ((TwSelectCell)grid[1, 0]).ShowDropDown(0);
        Assert.Null(TwSelectCell.OpenMenu);
    });

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

    private static int HitTest(Control control, Point screen) =>
        (int)SendMessage(control.Handle, 0x0084, IntPtr.Zero, (IntPtr)((screen.Y & 0xffff) << 16 | (screen.X & 0xffff)));

    [Fact]
    public void Window_chrome_removes_the_title_bar_and_drags_from_empty_surfaces() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(200, 200), Size = new Size(600, 400) };
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 48 };
        var title = new Label { Text = "Duruş Merkezi", AutoSize = true, Location = new Point(12, 14) };
        var save = new Button { Text = "Kaydet", Location = new Point(200, 10) };
        var buttons = new TwCaptionButtons { Dock = DockStyle.Right };
        toolbar.Controls.AddRange(new Control[] { title, save, buttons });
        var body = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add(body);
        form.Controls.Add(toolbar);
        form.Show();
        int framed = form.Height - form.ClientSize.Height;

        var chrome = TwWindowChrome.Attach(form);
        chrome.AddDragSurface(toolbar);
        Application.DoEvents();
        // Başlık gider: pencere ile istemci arasında yalnız kenarlar kalır.
        Assert.True(form.Height - form.ClientSize.Height < framed - SystemInformation.CaptionHeight / 2, (form.Height - form.ClientSize.Height) + " / " + framed);
        Assert.Same(chrome, TwWindowChrome.Get(form));

        Point Screen(Control c, int x, int y) => c.PointToScreen(new Point(x, y));
        // Araç çubuğunun boş yeri ve üstündeki etiket pencereyi taşır; düğme taşımaz.
        var empty = Screen(toolbar, 150, 30);
        Assert.Equal(-1, HitTest(toolbar, empty));
        Assert.Equal(2, HitTest(form, empty));
        Assert.Equal(-1, HitTest(title, Screen(title, 2, 2)));
        Assert.Equal(1, HitTest(save, Screen(save, 5, 5)));
        Assert.False(chrome.IsDragPoint(Screen(save, 5, 5)));
        Assert.False(chrome.IsDragPoint(Screen(body, 100, 100)) && !chrome.DragFromForm);
        // Üst kenar boyutlandırır.
        var top = new Point(form.Left + 300, form.Top + 1);
        Assert.Equal(12, HitTest(form, top));

        buttons.Press(1);
        Assert.Equal(FormWindowState.Maximized, form.WindowState);
        buttons.Press(1);
        Assert.Equal(FormWindowState.Normal, form.WindowState);

        chrome.Detach();
        Application.DoEvents();
        Assert.Null(TwWindowChrome.Get(form));
        Assert.Equal(framed, form.Height - form.ClientSize.Height);
    });

    [Fact]
    public void Sheet_opens_a_form_framelessly_over_a_dimmed_owner() => Sta.Run(() =>
    {
        TwMotion.Enabled = false;
        try
        {
            using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(100, 100), Size = new Size(900, 640) };
            host.Show();
            using var sheet = new Form { ShowInTaskbar = false, ClientSize = new Size(400, 260), Text = "Kayıt" };
            bool checkedShown = false;
            sheet.Shown += (_, _) =>
            {
                Assert.True(TwSheet.IsSheet(sheet));
                Assert.Equal(FormBorderStyle.None, sheet.FormBorderStyle);
                var area = host.RectangleToScreen(host.ClientRectangle);
                Assert.Equal(area, TwSheet.BackdropOf(sheet)!.Bounds);
                Assert.True(TwSheet.BackdropOf(sheet)!.Visible);
                Assert.True(area.Contains(sheet.Bounds), area + " / " + sheet.Bounds);
                Assert.Equal(new Size(400, 260), sheet.ClientSize);
                // Ortalanmış.
                Assert.InRange(sheet.Left - area.Left - (area.Right - sheet.Right), -2, 2);
                checkedShown = true;
                sheet.DialogResult = DialogResult.OK;
            };
            Assert.Equal(DialogResult.OK, TwSheet.ShowDialog(sheet, host));
            Assert.True(checkedShown);
            Assert.False(TwSheet.IsSheet(sheet));
            Assert.Equal(FormBorderStyle.Sizable, sheet.FormBorderStyle);

            // Attach: mevcut ShowDialog(owner) çağrıları değişmeden sayfa olur; tam ekran sayfa sahibin içini doldurur.
            TwSheet.Attach(host, new TwSheetOptions { FullScreen = true, Margin = 20, Filter = f => f.Text != "Hariç" });
            using var full = new Form { ShowInTaskbar = false, ClientSize = new Size(300, 200), Text = "Tam" };
            Rectangle shown = Rectangle.Empty;
            full.Shown += (_, _) =>
            {
                Assert.True(TwSheet.IsSheet(full));
                shown = full.Bounds;
                full.DialogResult = DialogResult.Cancel;
            };
            Assert.Equal(DialogResult.Cancel, full.ShowDialog(host));
            var inner = Rectangle.Inflate(host.RectangleToScreen(host.ClientRectangle), -(int)Math.Round(20 * TwDpi.DeviceDpi(host) / 96f), -(int)Math.Round(20 * TwDpi.DeviceDpi(host) / 96f));
            Assert.Equal(inner, shown);

            using var excluded = new Form { ShowInTaskbar = false, Text = "Hariç" };
            bool wasSheet = true;
            excluded.Shown += (_, _) => { wasSheet = TwSheet.IsSheet(excluded); excluded.DialogResult = DialogResult.OK; };
            excluded.ShowDialog(host);
            Assert.False(wasSheet);

            TwSheet.Detach(host);
            using var after = new Form { ShowInTaskbar = false };
            after.Shown += (_, _) => { wasSheet = TwSheet.IsSheet(after); after.DialogResult = DialogResult.OK; };
            after.ShowDialog(host);
            Assert.False(wasSheet);
        }
        finally
        {
            TwMotion.Enabled = true;
        }
    });

    [Fact]
    public void Filter_bar_shows_applied_filters_as_chips() => Sta.Run(() =>
    {
        using var form = NewForm(360, 300);
        var bar = new TwFilterBar { Dock = DockStyle.Top };
        bar.Filters.Add(new TwFilter("site", "Tesis", TwFilterKind.Fixed) { Options = { "Gebze", "Bursa" } });
        bar.Filters.Add(new TwFilter("line", "Hat", TwFilterKind.Select) { Options = { "Hat 1", "Hat 2", "Hat 3" } });
        bar.Filters.Add(new TwFilter("cause", "Neden", TwFilterKind.Select) { Options = { "Arıza", "Ayar" } });
        bar.Filters.Add(new TwFilter("text", "Açıklama", TwFilterKind.Text) { Placeholder = "Ara" });
        form.Controls.Add(bar);
        form.Show();
        int changed = 0;
        bar.FiltersChanged += (_, _) => changed++;

        // Değerler olaysız yüklenir; sabit filtre ✕'siz çiptir.
        bar.LoadValues(new System.Collections.Generic.Dictionary<string, string?> { ["site"] = "Gebze", ["line"] = "Hat 2", ["nope"] = "x" });
        Assert.Equal(0, changed);
        Assert.Equal("Tesis: Gebze", bar.ChipOf("site")!.Text);
        Assert.False(bar.ChipOf("site")!.Removable);
        Assert.Equal("Hat: Hat 2", bar.ChipOf("line")!.Text);
        Assert.True(bar.ChipOf("line")!.Removable);
        Assert.Null(bar.ChipOf("cause"));
        Assert.True(bar.AddButton.Visible);

        // "+ Filtre ekle" yalnız uygulanmamış filtreleri gösterir; seçenekler alt menüdedir.
        bar.ShowAddMenu();
        var menu = bar.OpenMenu!;
        Assert.Equal(new[] { "Neden", "Açıklama" }, menu.Items.Cast<ToolStripItem>().Select(i => i.Text));
        ((ToolStripMenuItem)menu.Items[0]).DropDownItems[0].PerformClick();
        Assert.Equal(1, changed);
        Assert.Equal("Arıza", bar.GetValue("cause"));
        menu.Close();

        bar.SetValue("text", "kayış");
        Assert.Equal(2, changed);
        Assert.False(bar.AddButton.Visible);
        // Çipler dar çubukta alt satıra geçer; çubuk boyu onları kapsar.
        var chips = new[] { "site", "line", "cause", "text" }.Select(k => bar.ChipOf(k)!).ToArray();
        Assert.True(chips.Max(c => c.Top) > chips.Min(c => c.Top));
        Assert.True(bar.Height >= chips.Max(c => c.Bottom));
        Assert.Equal(new[] { "Tesis: Gebze", "Hat: Hat 2", "Neden: Arıza", "Açıklama: kayış" }, bar.Controls.OfType<TwChip>().Select(c => c.Text));

        bar.SetValue("line", null);
        Assert.Null(bar.ChipOf("line"));
        Assert.Equal(3, changed);
        bar.ClearFilters();
        Assert.Equal(new System.Collections.Generic.Dictionary<string, string> { ["site"] = "Gebze" }, bar.Values);
        bar.SetValue("site", null); // sabit filtre kaldırılamaz
        Assert.Equal("Gebze", bar.GetValue("site"));
    });

    [Fact]
    public void Command_palette_filters_groups_and_runs_commands() => Sta.Run(() =>
    {
        using var form = NewForm(800, 500);
        form.Show();
        using var palette = new TwCommandPalette();
        string? ran = null;
        palette.Commands.Add(new TwCommand("Duruş kayıtları", () => ran = "kayıt") { Group = "Sayfalar", IconName = "list" });
        palette.Commands.Add(new TwCommand("Analitik", () => ran = "analitik") { Group = "Sayfalar" });
        palette.Commands.Add(new TwCommand("Yeni duruş", () => ran = "yeni") { Group = "İşlemler", Shortcut = "Ctrl+N", Keywords = "kayıt ekle" });
        palette.Commands.Add(new TwCommand("Ayarlar", () => ran = "ayar"));
        string? searched = null;
        palette.AddSearch("kayıtlar", q => searched = q, "Ara");
        string SearchRow(string q) => string.Format(TwTexts.Current.SearchIn, q, "kayıtlar");

        Assert.Equal(4, palette.Find("").Count);
        // Aksan ve büyük/küçük harf yok sayılır; baştan eşleşen önce gelir.
        Assert.Equal(new[] { "Duruş kayıtları", "Yeni duruş", SearchRow("durus") }, palette.Find("durus").Select(c => c.Text));
        Assert.Contains(palette.Find("ekle"), c => c.Text == "Yeni duruş");

        palette.Show(form);
        var window = palette.Window!;
        Assert.NotNull(window);
        Assert.Equal(new[] { "# Sayfalar", "Duruş kayıtları", "Analitik", "# İşlemler", "Yeni duruş", "Ayarlar" }, window.List.RowTexts);
        window.SearchBox.Text = "duruş";
        Assert.Equal("Duruş kayıtları", window.List.Selected!.Text);
        window.List.MoveSelection(1);
        Assert.Equal("Yeni duruş", window.List.Selected!.Text);
        window.List.MoveSelection(1);
        Assert.Equal(SearchRow("duruş"), window.List.Selected!.Text);
        window.RunSelected();
        Application.DoEvents();
        Assert.Equal("duruş", searched);
        Assert.False(palette.IsOpen);

        palette.Show(form);
        palette.Window!.SearchBox.Text = "ayar";
        palette.Window.RunSelected();
        Application.DoEvents();
        Assert.Equal("ayar", ran);

        palette.Show(form);
        palette.Window!.SearchBox.Text = "zzzz";
        Assert.Equal(SearchRow("zzzz"), palette.Window.List.Selected!.Text);
        palette.Close();
    });

    [Fact]
    public void Period_picker_offers_presets_custom_ranges_and_shifts() => Sta.Run(() =>
    {
        var clock = TwPeriodPicker.Clock;
        TwPeriodPicker.Clock = () => new DateTime(2026, 11, 4);
        try
        {
            using var form = NewForm(500, 400);
            var picker = new TwPeriodPicker { Location = new Point(10, 10) };
            form.Controls.Add(picker);
            form.Show();
            int changed = 0;
            picker.PeriodChanged += (_, _) => changed++;

            // Varsayılan: son 30 gün.
            Assert.Equal(new DateTime(2026, 10, 6), picker.Start);
            Assert.Equal(new DateTime(2026, 11, 4), picker.End);
            picker.PresetIndex = 4;
            Assert.Equal(string.Format(TwTexts.Current.LastDays, 90), picker.PeriodText);
            Assert.Equal(new DateTime(2026, 8, 7), picker.Start);
            picker.PresetIndex = picker.Presets.Count - 2; // geçen ay
            Assert.Equal(new DateTime(2026, 10, 1), picker.Start);
            Assert.Equal(new DateTime(2026, 10, 31), picker.End);
            Assert.Equal(2, changed);

            // Panel: hazır dönem tıklanınca uygulanır ve kapanır.
            picker.ShowPanel();
            Assert.True(picker.DroppedDown);
            picker.Panel!.ChooseRow(0);
            Assert.False(picker.DroppedDown);
            Assert.Equal(new DateTime(2026, 11, 4), picker.Start);

            // Özel aralık: takvimde iki tıklama (ters sırada da olur).
            picker.ShowPanel();
            var panel = picker.Panel!;
            panel.ChooseRow(picker.Presets.Count);
            panel.Calendar.DisplayMonth = new DateTime(2026, 9, 1);
            InvokeSelect(panel.Calendar, new DateTime(2026, 9, 20));
            Assert.True(picker.DroppedDown);
            Assert.Equal((new DateTime(2026, 9, 20), new DateTime(2026, 9, 20)), panel.Calendar.Range);
            InvokeSelect(panel.Calendar, new DateTime(2026, 9, 5));
            Assert.False(picker.DroppedDown);
            Assert.Equal(-1, picker.PresetIndex);
            Assert.Equal(new DateTime(2026, 9, 5), picker.Start);
            Assert.Equal(new DateTime(2026, 9, 20), picker.End);
            Assert.Equal(TwPeriodPicker.RangeText(new DateTime(2026, 9, 5), new DateTime(2026, 9, 20)), picker.PeriodText);

            // Vardiya bölümü yalnız vardiyalar verilince; seçilen vardiya metne eklenir.
            picker.Shifts.Add("A");
            picker.Shifts.Add("B");
            picker.ShowPanel();
            picker.Panel!.ChooseSegment(2);
            Assert.Equal("B", picker.Shift);
            Assert.EndsWith(" · B", picker.PeriodText);
            picker.Panel!.ChooseSegment(0);
            Assert.Null(picker.Shift);
            picker.ClosePanel();
            int before = changed;
            picker.PresetIndex = 2;
            Assert.Equal(before + 1, changed);
            using var bitmap = new Bitmap(picker.Width, picker.Height);
            picker.DrawToBitmap(bitmap, new Rectangle(Point.Empty, picker.Size));
        }
        finally
        {
            TwPeriodPicker.Clock = clock;
        }
    });

    // TwCalendar'ın gün seçimi (tıklama / Enter) özel yöntemdir; testte doğrudan çağrılır.
    private static void InvokeSelect(TwCalendar calendar, DateTime date) =>
        typeof(TwCalendar).GetMethod("Select", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(DateTime) }, null)!
            .Invoke(calendar, new object[] { date });
}
