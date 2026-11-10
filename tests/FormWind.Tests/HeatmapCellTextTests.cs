using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// TwHeatmap.CellText and ToolTipValueFormat: one source for the tooltip and screen reader text.
[Collection(UiCollection.Name)]
public class HeatmapCellTextTests
{
    private static TwHeatmap NewHeatmap() => new TwHeatmap
    {
        Size = new Size(400, 200),
        RowLabels = new[] { "Mon", "Thu" },
        ColumnLabels = new[] { "14 Sep", "21 Sep" },
        Values = new double[,] { { 0, 2 }, { 5, double.NaN } },
    };

    [Fact]
    public void Item_text_is_unchanged_without_a_handler() => Sta.Run(() =>
    {
        var heatmap = NewHeatmap();

        Assert.Equal("Thu, 14 Sep: 5", heatmap.GetItemText(1, 0));
    });

    [Fact]
    public void Cell_text_handler_replaces_title_and_value_for_screen_readers() => Sta.Run(() =>
    {
        var heatmap = NewHeatmap();
        heatmap.CellText += (_, e) =>
        {
            e.Title = $"Day {e.Row}-{e.Column}";
            e.ValueText = $"{e.Value:0} events";
        };

        Assert.Equal("Day 1-0: 5 events", heatmap.GetItemText(1, 0));
    });

    [Fact]
    public void Null_title_keeps_the_default_name() => Sta.Run(() =>
    {
        var heatmap = NewHeatmap();
        heatmap.CellText += (_, e) => e.ValueText = "x";

        Assert.Equal("Mon, 21 Sep: x", heatmap.GetItemText(0, 1));
    });

    [Fact]
    public void Cell_text_handler_gets_the_cell_and_its_value() => Sta.Run(() =>
    {
        var heatmap = NewHeatmap();
        TwHeatmapCellTextEventArgs? received = null;
        heatmap.CellText += (_, e) => received = e;

        heatmap.GetItemText(1, 0);

        Assert.NotNull(received);
        Assert.Equal(1, received!.Row);
        Assert.Equal(0, received.Column);
        Assert.Equal(5, received.Value);
        Assert.Null(received.Title);
    });

    [Fact]
    public void Tooltip_value_format_shows_zero_hidden_by_value_format() => Sta.Run(() =>
    {
        var culture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var heatmap = NewHeatmap();
            heatmap.ValueFormat = "#";
            Assert.Equal("Mon, 14 Sep: ", heatmap.GetItemText(0, 0));

            heatmap.ToolTipValueFormat = "0";
            Assert.Equal("Mon, 14 Sep: 0", heatmap.GetItemText(0, 0));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culture;
        }
    });

    [Fact]
    public void Empty_cell_text_wins_over_tooltip_value_format() => Sta.Run(() =>
    {
        var heatmap = NewHeatmap();
        heatmap.EmptyCellText = "No production";
        heatmap.ToolTipValueFormat = "0";

        Assert.Equal("Thu, 21 Sep: No production", heatmap.GetItemText(1, 1));
    });

    [Fact]
    public void Painting_the_tooltip_asks_for_the_hovered_cell_text() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(440, 240) };
        var heatmap = NewHeatmap();
        form.Controls.Add(heatmap);
        form.Show();
        var asked = new List<(int Row, int Column)>();
        heatmap.CellText += (_, e) => asked.Add((e.Row, e.Column));
        heatmap.HoverIndex = 2; // row 1, column 0

        using var bitmap = new Bitmap(heatmap.Width, heatmap.Height);
        heatmap.DrawToBitmap(bitmap, new Rectangle(Point.Empty, heatmap.Size));

        Assert.Contains((1, 0), asked);
        form.Close();
    });
}
