using System;
using System.Drawing;
using System.Windows.Forms;
using Xunit;

namespace FormWind.Tests;

public sealed class DataGridViewTests : IDisposable
{
    private readonly TailwindProvider _provider = new TailwindProvider();
    private readonly DataGridView _grid = new DataGridView { AllowUserToAddRows = false };

    public DataGridViewTests()
    {
        _grid.Columns.Add("name", "Ad");
        _grid.Columns.Add("amount", "Tutar");
        _grid.Rows.Add("Ayşe", "120");
        _grid.Rows.Add("Mehmet", "80");
    }

    public void Dispose()
    {
        _grid.Dispose();
        _provider.Dispose();
    }

    private static int Argb(string hex) => ColorTranslator.FromHtml(hex).ToArgb();

    [Fact]
    public void Header_part_styles_column_headers()
    {
        _provider.SetClasses(_grid, "header:bg-gray-50 header:text-gray-500 header:font-bold header:h-10 header:px-3 header:whitespace-nowrap");

        Assert.False(_grid.EnableHeadersVisualStyles);
        var header = _grid.ColumnHeadersDefaultCellStyle;
        Assert.Equal(Argb("#f9fafb"), header.BackColor.ToArgb());
        Assert.Equal(Argb("#6b7280"), header.ForeColor.ToArgb());
        Assert.True(header.Font!.Bold);
        Assert.Equal(12, header.Padding.Left);
        Assert.Equal(DataGridViewTriState.False, header.WrapMode);
        Assert.Equal(header.BackColor, header.SelectionBackColor);
        Assert.Equal(DataGridViewColumnHeadersHeightSizeMode.DisableResizing, _grid.ColumnHeadersHeightSizeMode);
        Assert.Equal(40, _grid.ColumnHeadersHeight);
    }

    [Fact]
    public void Cell_alt_and_selected_parts()
    {
        _provider.SetClasses(_grid, "cell:px-3 cell:text-right alt:bg-gray-50 selected:bg-blue-50 selected:text-blue-900");

        Assert.Equal(12, _grid.DefaultCellStyle.Padding.Left);
        Assert.Equal(DataGridViewContentAlignment.MiddleRight, _grid.DefaultCellStyle.Alignment);
        Assert.Equal(Argb("#eff6ff"), _grid.DefaultCellStyle.SelectionBackColor.ToArgb());
        Assert.Equal(Argb("#1e3a8a"), _grid.DefaultCellStyle.SelectionForeColor.ToArgb());
        Assert.Equal(Argb("#f9fafb"), _grid.AlternatingRowsDefaultCellStyle.BackColor.ToArgb());
    }

    // AdvancedDataGridView örneği: Designer'daki açık gri zebra koyu temada açık yazıyla okunmuyordu. Class'lar hücre
    // zeminini verince zebra onların zemininden türetilir; zemin verilmezse uygulamanın tonu kalır.
    [Fact]
    public void Designer_alternating_background_follows_the_class_background()
    {
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(224, 224, 224);

        _provider.SetClasses(_grid, "text-gray-700");
        Assert.Equal(Color.FromArgb(224, 224, 224).ToArgb(), _grid.AlternatingRowsDefaultCellStyle.BackColor.ToArgb());

        _provider.SetClasses(_grid, "bg-gray-800 text-gray-100");
        Assert.DoesNotContain(TwContrast.Audit(_grid, _provider), i => i.Part == "alternating row");
        var zebra = _grid.AlternatingRowsDefaultCellStyle.BackColor;
        Assert.NotEqual(Argb("#1f2937"), zebra.ToArgb());                                   // zebra kalır
        Assert.True(TwContrast.Ratio(ColorTranslator.FromHtml("#f3f4f6"), zebra) >= 4.5, $"zebra {zebra}");

        _provider.SetClasses(_grid, "bg-gray-800 text-gray-100 alt:bg-gray-700");
        Assert.Equal(Argb("#374151"), _grid.AlternatingRowsDefaultCellStyle.BackColor.ToArgb());

        _provider.SetClasses(_grid, "");
        Assert.Equal(Color.FromArgb(224, 224, 224).ToArgb(), _grid.AlternatingRowsDefaultCellStyle.BackColor.ToArgb());

        // Uygulamanın zebrası açık yazıyla kalırsa denetim alternatif satırı raporlar.
        _provider.SetClasses(_grid, "text-gray-100");
        Assert.Contains(TwContrast.Audit(_grid, _provider), i => i.Part == "alternating row");
    }

    // İş formu denemesi: uygulamanın bej "Tutar" kolonu koyu temada açık yazıyla okunmuyordu, denetim de görmüyordu.
    // Uygulamanın zemin verip yazı vermediği kolon, tasarlandığı özgün yazı rengini alır; denetim kolon çiftini ölçer.
    [Fact]
    public void Columns_with_an_application_background_keep_their_designed_text_color()
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        form.Controls.Add(_grid);
        form.Show();
        var amount = _grid.Columns["amount"]!;
        amount.DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 220);
        Color formatted = Color.Empty;
        _provider.SetClasses(_grid, "bg-gray-800 text-gray-100");
        _grid.CellFormatting += (_, e) => { if (e.ColumnIndex == amount.Index && e.RowIndex == 0) formatted = e.CellStyle!.ForeColor; };
        // Renkler yalnızca çizimde biçimlendirilir (FormattedValue renksiz stil verir).
        void Paint()
        {
            using var bitmap = new Bitmap(_grid.Width, _grid.Height);
            _grid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, _grid.Size));
        }

        Paint();
        Assert.Equal(SystemColors.ControlText.ToArgb(), formatted.ToArgb());
        Assert.DoesNotContain(TwContrast.Audit(_grid, _provider), i => i.Part.StartsWith("column"));

        // Yazı rengini class'lar vermiyorsa (yalnızca zemin) kolon kendi hâlinde kalır.
        _provider.SetClasses(_grid, "bg-white");
        Paint();
        Assert.Equal(_grid.DefaultCellStyle.ForeColor.ToArgb(), formatted.ToArgb());
        form.Controls.Remove(_grid);
    }

    // İş formu denemesi: table bileşeni satır başlıklarını gizler; satır hatası (ErrorText) ikonu da kayboluyordu. Hatalı
    // satırın başında tehlike renginde bir şerit çizilir, hata metni hücre ipucu olur.
    [Fact]
    public void Row_errors_stay_visible_when_the_classes_hide_row_headers()
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, ClientSize = new Size(300, 200) };
        _grid.Dock = DockStyle.Fill;
        form.Controls.Add(_grid);
        form.Show();
        _grid.Rows[1].ErrorText = "Stokta yeterli ürün yok.";
        _provider.SetClasses(_grid, "table");
        Assert.False(_grid.RowHeadersVisible);

        var mark = TwGridDecorator.TryGet(_grid)!.RowErrorColor;
        Assert.NotNull(mark);
        using var bitmap = new Bitmap(_grid.Width, _grid.Height);
        _grid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, _grid.Size));
        Color Edge(int row)
        {
            var r = _grid.GetRowDisplayRectangle(row, false);
            return bitmap.GetPixel(r.Left + 1, r.Top + r.Height / 2);
        }
        Assert.Equal(mark!.Value.ToArgb(), Edge(1).ToArgb());
        Assert.NotEqual(mark.Value.ToArgb(), Edge(0).ToArgb());

        _provider.SetClasses(_grid, "bg-white");                                              // başlıklar görünür
        Assert.True(_grid.RowHeadersVisible);
        Assert.Null(TwGridDecorator.TryGet(_grid)!.RowErrorColor);
        form.Controls.Remove(_grid);
    }

    [Fact]
    public void Base_classes_style_background_cells_and_grid_lines()
    {
        _provider.SetClasses(_grid, "bg-white text-gray-700 divide-y divide-gray-200 border");

        Assert.Equal(Color.White.ToArgb(), _grid.BackgroundColor.ToArgb());
        Assert.Equal(Color.White.ToArgb(), _grid.DefaultCellStyle.BackColor.ToArgb());
        Assert.Equal(Argb("#374151"), _grid.DefaultCellStyle.ForeColor.ToArgb());
        Assert.Equal(DataGridViewCellBorderStyle.SingleHorizontal, _grid.CellBorderStyle);
        Assert.Equal(Argb("#e5e7eb"), _grid.GridColor.ToArgb());
        Assert.Equal(BorderStyle.None, _grid.BorderStyle); // renkli kenarlığı FormWind çizer
    }

    [Fact]
    public void Row_height_applies_to_template_and_existing_rows()
    {
        _provider.SetClasses(_grid, "row:h-10 row-header:hidden");

        Assert.Equal(40, _grid.RowTemplate.Height);
        Assert.All(new[] { _grid.Rows[0], _grid.Rows[1] }, r => Assert.Equal(40, r.Height));
        Assert.False(_grid.RowHeadersVisible);
    }

    [Fact]
    public void Dark_variants_apply_to_parts()
    {
        _provider.SetClasses(_grid, "header:bg-gray-50 dark:header:bg-gray-900");

        _provider.DarkMode = true;
        Assert.Equal(Argb("#111827"), _grid.ColumnHeadersDefaultCellStyle.BackColor.ToArgb());

        _provider.DarkMode = false;
        Assert.Equal(Argb("#f9fafb"), _grid.ColumnHeadersDefaultCellStyle.BackColor.ToArgb());
    }

    [Fact]
    public void Removing_classes_restores_original_grid()
    {
        var originalHeader = new DataGridViewCellStyle(_grid.ColumnHeadersDefaultCellStyle);
        var originalBorder = _grid.CellBorderStyle;
        var originalRowHeight = _grid.RowTemplate.Height;

        _provider.SetClasses(_grid, "table");
        _provider.SetClasses(_grid, "");

        Assert.True(_grid.EnableHeadersVisualStyles);
        Assert.Equal(originalHeader, _grid.ColumnHeadersDefaultCellStyle);
        Assert.Equal(originalBorder, _grid.CellBorderStyle);
        Assert.Equal(originalRowHeight, _grid.RowTemplate.Height);
        Assert.True(_grid.RowHeadersVisible);
    }

    [Fact]
    public void Table_component_is_fully_recognized()
    {
        Assert.Empty(new TwTheme().Parse("table").Unknown);
    }

    [Fact]
    public void Part_classes_do_not_leak_to_other_controls()
    {
        using var label = new Label();
        _provider.SetClasses(label, "header:bg-red-500 cell:text-white");

        Assert.False(TwOriginal.IsExplicit(label, nameof(Control.BackColor)));
    }

    [Fact]
    public void Row_hover_recolors_only_the_hovered_row()
    {
        _provider.SetClasses(_grid, "row:hover:bg-gray-50");
        var hover = TwGridHover.For(_grid);

        hover.SetRow(1);

        var hovered = Format(1);
        var other = Format(0);
        Assert.Equal(Argb("#f9fafb"), hovered.BackColor.ToArgb());
        Assert.Equal(Color.White.ToArgb(), other.BackColor.ToArgb());

        hover.SetRow(-1);
        Assert.Equal(Color.White.ToArgb(), Format(1).BackColor.ToArgb());

        DataGridViewCellStyle Format(int row)
        {
            var args = new DataGridViewCellFormattingEventArgs(0, row, "x", typeof(string), new DataGridViewCellStyle { BackColor = Color.White });
            hover.Format(args);
            return args.CellStyle!;
        }
    }

    [Fact]
    public void Parser_supports_parts_with_states()
    {
        var parsed = new TwTheme().Parse("bg-white header:bg-gray-50 dark:header:bg-gray-900 row:hover:bg-gray-100");

        Assert.Equal(Color.White.ToArgb(), parsed.Resolve(TwState.None).BackColor!.Value.ToArgb());
        Assert.Equal(Argb("#f9fafb"), parsed.ResolvePart("header", TwState.None).BackColor!.Value.ToArgb());
        Assert.Equal(Argb("#111827"), parsed.ResolvePart("header", TwState.Dark).BackColor!.Value.ToArgb());
        Assert.True(parsed.HasPartCondition("row", TwState.Hover));
        Assert.Null(parsed.ResolvePart("row", TwState.None).BackColor);
    }

    [Fact]
    public void Parser_rejects_two_parts_and_unknown_parts()
    {
        var parsed = new TwTheme().Parse("header:cell:bg-white toolbar:bg-white");

        Assert.Equal(new[] { "header:cell:bg-white", "toolbar:bg-white" }, parsed.Unknown);
    }

    [Fact]
    public void Row_height_on_virtual_grid_does_not_unshare_rows()
    {
        using var grid = new DataGridView { VirtualMode = true, AllowUserToAddRows = false };
        grid.Columns.Add("a", "A");
        grid.RowCount = 5000;

        _provider.SetClasses(grid, "row:h-10");

        Assert.Equal(40, grid.Rows[0].Height);
        Assert.Equal(40, grid.Rows[4999].Height);
        Assert.Equal(40 * 5000, grid.Rows.GetRowsHeight(DataGridViewElementStates.None));
        int unshared = 0;
        for (int i = 0; i < grid.Rows.Count; i++)
            if (grid.Rows.SharedRow(i).Index != -1) unshared++;
        Assert.True(unshared < 10, $"{unshared} rows unshared");

        _provider.SetClasses(grid, "");
        Assert.Equal(grid.RowTemplate.Height, grid.Rows.SharedRow(10).Height);
        Assert.NotEqual(40, grid.Rows.SharedRow(10).Height);
    }

    [Fact]
    public void Row_height_applies_to_data_bound_grid()
    {
        using var form = new Form();
        using var grid = new DataGridView { AllowUserToAddRows = false };
        form.Controls.Add(grid);
        grid.DataSource = new[] { new { Name = "Ayşe" }, new { Name = "Mehmet" } };

        _provider.SetClasses(grid, "row:h-10");

        Assert.Equal(40, grid.Rows[0].Height);
        Assert.Equal(40, grid.Rows[1].Height);
    }

    [Fact]
    public void Row_height_on_unbound_grid_still_updates_existing_rows()
    {
        _provider.SetClasses(_grid, "row:h-12");

        Assert.Equal(48, _grid.Rows[0].Height);
        Assert.Equal(48, _grid.Rows[1].Height);
    }

    [Fact]
    public void Adding_rows_adds_no_extra_invalidation_without_overlay()
    {
        // DataGridView RowCount değişince kendisi de geçersiz kılar; FormWind buna ek yapmamalı.
        int Count(bool styled, string? emptyText)
        {
            using var form = new Form();
            using var grid = new DataGridView { VirtualMode = true, AllowUserToAddRows = false };
            grid.Columns.Add("a", "A");
            form.Controls.Add(grid);
            _ = grid.Handle;
            if (styled) _provider.SetClasses(grid, "table");
            if (emptyText != null) TwGrid.SetEmptyText(grid, emptyText);
            int invalidated = 0;
            grid.Invalidated += (_, _) => invalidated++;
            for (int i = 1; i <= 20; i++) grid.RowCount = i * 10;
            grid.RowCount = 0;
            grid.RowCount = 5;
            return invalidated;
        }

        Assert.Equal(Count(styled: false, emptyText: null), Count(styled: true, emptyText: null));
        Assert.True(Count(styled: true, emptyText: "Kayıt yok") > Count(styled: true, emptyText: null), "empty-state changes must repaint");
    }
}
