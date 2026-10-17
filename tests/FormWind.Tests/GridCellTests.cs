using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public sealed class GridCellTests : IDisposable
{
    private readonly TailwindProvider _provider = new TailwindProvider();
    private readonly DataGridView _grid = new DataGridView { AllowUserToAddRows = false, RowHeadersVisible = false, Size = new Size(400, 120) };

    public void Dispose()
    {
        _grid.Dispose();
        _provider.Dispose();
    }

    private static int Argb(string hex) => ColorTranslator.FromHtml(hex).ToArgb();

    private Bitmap Render()
    {
        var bitmap = new Bitmap(_grid.Width, _grid.Height);
        _grid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, _grid.Size));
        return bitmap;
    }

    private static int Count(Bitmap bitmap, Rectangle area, int argb, int tolerance = 30)
    {
        var target = Color.FromArgb(argb);
        int count = 0;
        for (int x = area.Left; x < area.Right; x++)
            for (int y = area.Top; y < area.Bottom; y++)
            {
                var c = bitmap.GetPixel(x, y);
                if (Math.Abs(c.R - target.R) + Math.Abs(c.G - target.G) + Math.Abs(c.B - target.B) < tolerance) count++;
            }
        return count;
    }

    [Fact]
    public void Column_classes_style_cells_header_and_width()
    {
        _grid.Columns.Add("amount", "Tutar");
        _provider.SetColumnClasses(_grid.Columns[0], "text-right font-bold text-red-600 header:text-center w-40 min-w-20");

        var column = _grid.Columns[0];
        Assert.Equal("text-right font-bold text-red-600 header:text-center w-40 min-w-20", _provider.GetColumnClasses(column));
        Assert.Equal(DataGridViewContentAlignment.MiddleRight, column.DefaultCellStyle.Alignment);
        Assert.True(column.DefaultCellStyle.Font!.Bold);
        Assert.Equal(Argb("#dc2626"), column.DefaultCellStyle.ForeColor.ToArgb());
        Assert.Equal(DataGridViewContentAlignment.MiddleCenter, column.HeaderCell.Style.Alignment);
        Assert.Equal(160, column.Width);
        Assert.Equal(80, column.MinimumWidth);
    }

    [Fact]
    public void Column_dark_variants_follow_the_provider()
    {
        _grid.Columns.Add("name", "Ad");
        _provider.SetColumnClasses(_grid.Columns[0], "text-gray-900 dark:text-white");

        _provider.DarkMode = true;
        Assert.Equal(Color.White.ToArgb(), _grid.Columns[0].DefaultCellStyle.ForeColor.ToArgb());
        _provider.DarkMode = false;
        Assert.Equal(Argb("#111827"), _grid.Columns[0].DefaultCellStyle.ForeColor.ToArgb());
    }

    [Fact]
    public void Badge_variants_parse_values_and_fallback()
    {
        var map = TwBadgeColumn.ParseVariants("Alive: bg-green-100 text-green-800 dark:bg-green-900; *: bg-gray-100");

        Assert.Equal("bg-green-100 text-green-800 dark:bg-green-900", map["alive"]);
        Assert.Equal("bg-gray-100", map["*"]);

        var column = new TwBadgeColumn { Variants = "Alive: bg-green-100; *: bg-gray-100" };
        Assert.Equal(Argb("#dcfce7"), column.StyleFor("Alive", null).BackColor!.Value.ToArgb());
        Assert.Equal(Argb("#f3f4f6"), column.StyleFor("Dead", null).BackColor!.Value.ToArgb());
    }

    [Fact]
    public void Badge_and_progress_cells_paint_their_colors() => Sta.Run(() =>
    {
        _grid.Columns.Add(new TwBadgeColumn { Name = "status", Width = 150, Variants = "Alive: bg-green-500 text-white" });
        _grid.Columns.Add(new TwProgressColumn { Name = "progress", Width = 200, BarColor = Color.FromArgb(255, 0, 0) });
        _grid.Rows.Add("Alive", 80.0);
        _grid.ClearSelection();

        using var bitmap = Render();
        var row = _grid.GetRowDisplayRectangle(0, false);
        var status = Rectangle.Intersect(_grid.GetColumnDisplayRectangle(0, false), row);
        var progress = Rectangle.Intersect(_grid.GetColumnDisplayRectangle(1, false), row);

        Assert.True(Count(bitmap, status, Argb("#22c55e")) > 50, "rozet yeşil değil");
        Assert.True(Count(bitmap, progress, Color.FromArgb(255, 0, 0).ToArgb()) > 30, "çubuk kırmızı değil");
    });

    [Fact]
    public void Icon_and_avatar_cells_paint_without_errors() => Sta.Run(() =>
    {
        _grid.Columns.Add(new TwIconColumn { Name = "icon" });
        _grid.Columns.Add(new TwAvatarColumn { Name = "avatar", AvatarSize = 24 });
        using var image = new Bitmap(10, 10);
        using (var g = Graphics.FromImage(image)) g.Clear(Color.Red);
        _grid.Rows.Add("check", image);
        _grid.Rows.Add("not-an-icon", null!);

        using var bitmap = Render();
        var avatar = Rectangle.Intersect(_grid.GetColumnDisplayRectangle(1, false), _grid.GetRowDisplayRectangle(0, false));
        Assert.True(Count(bitmap, avatar, Color.Red.ToArgb()) > 100, "avatar çizilmedi");
    });

    [Fact]
    public void Row_rule_styles_matching_rows_only() => Sta.Run(() =>
    {
        _grid.Columns.Add("status", "Durum");
        _grid.Rows.Add("İptal");
        _grid.Rows.Add("Kargoda");
        _provider.SetRowClasses(_grid, r => (string?)r.Cells[0].Value == "İptal" ? "bg-red-100 text-red-800" : null);

        DataGridViewCellStyle Format(int row)
        {
            var args = new DataGridViewCellFormattingEventArgs(0, row, _grid.Rows[row].Cells[0].Value, typeof(string), new DataGridViewCellStyle());
            typeof(DataGridView).GetMethod("OnCellFormatting", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(DataGridViewCellFormattingEventArgs) }, null)!
                .Invoke(_grid, new object[] { args });
            return args.CellStyle!;
        }

        Assert.Equal(Argb("#fee2e2"), Format(0).BackColor.ToArgb());
        Assert.True(Format(1).BackColor.IsEmpty);

        _provider.SetRowClasses(_grid, null);
        Assert.True(Format(0).BackColor.IsEmpty);
    });
}
