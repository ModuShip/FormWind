using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// The calendar layout and row highlights of <see cref="TwHeatmap"/> (0.1.8 report: a calendar heatmap and highlighted
/// rows kept in the application).
/// </summary>
[Collection(UiCollection.Name)]
public class HeatmapCalendarTests
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    [Fact]
    public void A_calendar_puts_each_day_under_its_weekday_and_week() => Sta.Run(() =>
    {
        using var heatmap = new TwHeatmap();
        var days = Enumerable.Range(1, 59).Select(d => (double)d).ToArray();   // January and February 2026
        heatmap.SetCalendar(new DateTime(2026, 1, 1, 15, 30, 0), days, Turkish);

        Assert.Equal(7, heatmap.RowCount);
        Assert.Equal(9, heatmap.ColumnCount);                                  // 29 Dec 2025 … 1 Mar 2026
        Assert.True(double.IsNaN(heatmap.GetValue(0, 0)));                     // Monday 29 December: before the start
        Assert.Equal(1, heatmap.GetValue(3, 0));                               // Thursday 1 January
        Assert.Equal(new DateTime(2026, 1, 1), heatmap.GetDate(3, 0));
        Assert.Equal(3 * 9, heatmap.GetCell(new DateTime(2026, 1, 1, 23, 0, 0)));
        Assert.Equal(new DateTime(2026, 2, 1), heatmap.GetDate(6, 4));         // Sunday 1 February
        Assert.Equal(32, heatmap.GetValue(6, 4));
        Assert.Equal(54, heatmap.GetValue(0, 8));                              // Monday 23 February
        Assert.True(double.IsNaN(heatmap.GetValue(6, 8)));                     // Sunday 1 March: after the last value
        Assert.Equal(-1, heatmap.GetCell(new DateTime(2025, 12, 1)));
        Assert.Equal(-1, heatmap.GetCell(new DateTime(2026, 6, 1)));
    });

    [Fact]
    public void A_calendar_labels_weekdays_and_the_weeks_where_months_start() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(500, 220) };
        var heatmap = new TwHeatmap { Dock = DockStyle.Fill, ShowValues = true };
        form.Controls.Add(heatmap);
        heatmap.SetCalendar(new DateTime(2026, 1, 1), Enumerable.Range(1, 59).Select(d => (double)d).ToArray(), Turkish);

        Assert.Equal(new DateTime(2026, 1, 1), heatmap.GetDate(3, 0));
        using var picture = new Bitmap(heatmap.Width, heatmap.Height);
        heatmap.DrawToBitmap(picture, new Rectangle(Point.Empty, heatmap.Size));   // paints with values in a text batch

        var rows = Enumerable.Range(0, 7).Select(r => heatmap.GetDate(r, 1)!.Value.DayOfWeek).ToArray();
        Assert.Equal(DayOfWeek.Monday, rows[0]);
        Assert.Equal(DayOfWeek.Sunday, rows[6]);
    });

    [Fact]
    public void Ordinary_data_ends_the_calendar() => Sta.Run(() =>
    {
        using var heatmap = new TwHeatmap();
        heatmap.SetCalendar(new DateTime(2026, 1, 1), new double[] { 1, 2, 3 }, Turkish);
        heatmap.SetData(new[] { "Hat 1" }, new[] { "Pzt" }, new double[,] { { 4 } });
        Assert.Null(heatmap.GetDate(0, 0));
        Assert.Equal(-1, heatmap.GetCell(new DateTime(2026, 1, 1)));
        Assert.Throws<ArgumentNullException>(() => heatmap.SetCalendar(DateTime.Today, null!));
    });

    [Fact]
    public void Highlighted_rows_are_drawn_on_a_band() => Sta.Run(() =>
    {
        using var heatmap = new TwHeatmap { Size = new Size(400, 200), BackColor = Color.White, ShowLegend = false };
        heatmap.SetData(new[] { "Hat 1", "Hat 2", "Hat 3" }, new[] { "Pzt", "Sal", "Çar" }, new double[,] { { 1, 2, 3 }, { 4, 5, 6 }, { 7, 8, 9 } });
        using var plain = new Bitmap(400, 200);
        heatmap.DrawToBitmap(plain, new Rectangle(0, 0, 400, 200));

        heatmap.HighlightRows = new[] { 1, 7 };                                 // a row that does not exist is ignored
        using var highlighted = new Bitmap(400, 200);
        heatmap.DrawToBitmap(highlighted, new Rectangle(0, 0, 400, 200));

        var middle = heatmap.GetCellBounds(1, 0);
        var between = new Point(middle.Right, middle.Top + middle.Height / 2);   // the gap between two cells of the row
        Assert.NotEqual(plain.GetPixel(between.X, between.Y).ToArgb(), highlighted.GetPixel(between.X, between.Y).ToArgb());
        heatmap.HighlightRows = null!;
        Assert.Empty(heatmap.HighlightRows);
    });
}
