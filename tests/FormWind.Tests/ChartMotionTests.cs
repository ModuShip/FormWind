using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>Opening and data-change animations of TwChart, TwRingChart, TwHeatmap, TwTimeline and TwSparkline.</summary>
[Collection(UiCollection.Name)]
public class ChartMotionTests
{
    private static Form Host() =>
        new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, Size = new Size(500, 400) };

    // Animasyonu kapatan/açan testler değeri geri alır (bkz. TestMotion).
    private static void WithMotion(bool enabled, Action body)
    {
        var previous = TwMotion.Enabled;
        TwMotion.Enabled = enabled;
        try
        {
            body();
        }
        finally
        {
            TwMotion.Enabled = previous;
        }
    }

    private static void WaitUntil(Func<bool> condition, int milliseconds = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    private static void Paint(Control control)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
    }

    private static void Between(double low, double high, double actual)
    {
        Assert.InRange(actual, Math.Min(low, high), Math.Max(low, high));
        Assert.NotEqual(high, actual, 3);                                         // henüz hedefte değil
    }

    [Fact]
    public void Chart_bars_grow_from_the_baseline_one_after_another() => Sta.Run(() => WithMotion(true, () =>
    {
        using var form = Host();
        var chart = new TwChart { Dock = DockStyle.Fill, Kind = TwChartKind.Bar };
        chart.SetData(new[] { "A", "B", "C", "D" }, new TwChartSeries("S", new double[] { 40, 40, 40, 40 }));
        form.Controls.Add(chart);
        form.Show();
        Paint(chart);

        var layout = chart.ComputeLayout();
        var target = chart.BarBounds(layout, 0, 0);
        var first = chart.PaintedBarBounds(0, 0);
        var last = chart.PaintedBarBounds(0, 3);
        Assert.True(chart.IntroProgress < 1f);
        Assert.True(first.Height < target.Height);
        Assert.True(last.Height <= first.Height);                                   // sonraki çubuk sonra başlar
        Assert.Equal(target.Bottom, first.Bottom, 1);                               // tabandan büyür
        Assert.Equal(target, chart.BarBounds(layout, 0, 0));                        // geometri hedefi verir

        WaitUntil(() => chart.IntroProgress >= 1f);
        Paint(chart);
        Assert.Equal(chart.BarBounds(layout, 0, 3), chart.PaintedBarBounds(0, 3));
        form.Close();
    }));

    [Fact]
    public void Chart_data_change_moves_bars_and_points_from_their_old_place() => Sta.Run(() => WithMotion(true, () =>
    {
        using var form = Host();
        var chart = new TwChart { Dock = DockStyle.Fill, AnimateOnShow = false, AxisMaximum = 100 };
        chart.SetData(new[] { "A", "B" },
            new TwChartSeries("Bars", new double[] { 20, 20 }) { Kind = TwChartKind.Bar },
            new TwChartSeries("Line", new double[] { 30, 30 }) { Kind = TwChartKind.Line });
        form.Controls.Add(chart);
        form.Show();
        Paint(chart);
        var oldBar = chart.PaintedBarBounds(0, 0);
        var oldPoint = chart.PaintedLinePoint(1, 1)!.Value;
        Assert.Equal(1f, chart.ChangeProgress);

        chart.SetData(new[] { "A", "B" },
            new TwChartSeries("Bars", new double[] { 80, 20 }) { Kind = TwChartKind.Bar },
            new TwChartSeries("Line", new double[] { 30, 90 }) { Kind = TwChartKind.Line });
        Assert.True(chart.ChangeProgress < 1f);
        Assert.Equal(1f, chart.IntroProgress);                                      // baştan çizilmez
        Paint(chart);
        var layout = chart.ComputeLayout();
        Between(oldBar.Top, chart.BarBounds(layout, 0, 0).Top, chart.PaintedBarBounds(0, 0).Top);
        Between(oldPoint.Y, layout.Y(90), chart.PaintedLinePoint(1, 1)!.Value.Y);

        WaitUntil(() => chart.ChangeProgress >= 1f);
        Paint(chart);
        Assert.Equal(chart.BarBounds(layout, 0, 0), chart.PaintedBarBounds(0, 0));
        Assert.Equal(layout.Y(90), chart.PaintedLinePoint(1, 1)!.Value.Y, 1);
        form.Close();
    }));

    [Fact]
    public void Chart_changes_are_immediate_without_animate_changes_or_motion() => Sta.Run(() =>
    {
        using var form = Host();
        var chart = new TwChart { Dock = DockStyle.Fill, AnimateOnShow = false, AnimateChanges = false };
        chart.SetData(new[] { "A" }, new TwChartSeries("S", new double[] { 1 }));
        form.Controls.Add(chart);
        WithMotion(true, () =>
        {
            form.Show();
            Paint(chart);
            chart.Series[0].Values = new double[] { 5 };
            Assert.Equal(1f, chart.ChangeProgress);
        });
        chart.AnimateChanges = true;
        WithMotion(false, () =>
        {
            chart.Series[0].Values = new double[] { 9 };
            Assert.Equal(1f, chart.ChangeProgress);
            chart.AnimateIn();
            Assert.Equal(1f, chart.IntroProgress);
        });
        form.Close();
    });

    [Fact]
    public void Horizontal_bars_grow_from_zero() => Sta.Run(() => WithMotion(true, () =>
    {
        using var form = Host();
        var chart = new TwChart { Dock = DockStyle.Fill, Kind = TwChartKind.HorizontalBar };
        chart.SetData(new[] { "A", "B" }, new TwChartSeries("S", new double[] { 50, 70 }));
        form.Controls.Add(chart);
        form.Show();
        Paint(chart);
        var target = chart.HorizontalBarBounds(chart.ComputeHorizontalLayout(), 0, 0);
        var shown = chart.PaintedBarBounds(0, 0);
        Assert.Equal(target.Left, shown.Left, 1);
        Assert.True(shown.Width < target.Width);
        form.Close();
    }));

    [Fact]
    public void Rings_fill_from_zero_and_set_data_moves_from_the_old_fill() => Sta.Run(() => WithMotion(true, () =>
    {
        using var form = Host();
        var rings = new TwRingChart { Size = new Size(200, 200) };
        rings.SetData(("A", 50), ("B", 50));
        form.Controls.Add(rings);
        form.Show();
        Paint(rings);
        Assert.True(rings.IntroProgress < 1f);
        Assert.True(rings.PaintedFraction(0) < 0.5f);
        Assert.True(rings.PaintedFraction(1) <= rings.PaintedFraction(0));

        WaitUntil(() => rings.IntroProgress >= 1f);
        Paint(rings);
        Assert.Equal(0.5f, rings.PaintedFraction(0), 3);

        // SetData halkaları silip yeniden ekler; geçiş yine ekrandaki dolulukdan başlar, sıfırdan değil.
        rings.SetData(("A", 80), ("B", 50));
        Assert.True(rings.ChangeProgress < 1f);
        Paint(rings);
        Between(0.5, 0.8, rings.PaintedFraction(0));
        WaitUntil(() => rings.ChangeProgress >= 1f);
        Paint(rings);
        Assert.Equal(0.8f, rings.PaintedFraction(0), 3);
        form.Close();
    }));

    [Fact]
    public void Heatmap_cells_fade_in_and_change_shade_gradually() => Sta.Run(() => WithMotion(true, () =>
    {
        using var form = Host();
        var heatmap = new TwHeatmap { Dock = DockStyle.Fill, LowColor = Color.White, HighColor = Color.Black, Minimum = 0, Maximum = 100 };
        heatmap.SetData(new[] { "1", "2" }, new[] { "a", "b" }, new double[,] { { 0, 0 }, { 0, 100 } });
        form.Controls.Add(heatmap);
        form.Show();
        Assert.True(heatmap.IntroProgress < 1f);
        WaitUntil(() => heatmap.IntroProgress >= 1f);
        Paint(heatmap);
        Assert.Equal(Color.White.ToArgb(), heatmap.PaintedCellColor(0, 0)!.Value.ToArgb());

        heatmap.Values = new double[,] { { 100, 0 }, { 0, 100 } };
        Assert.True(heatmap.ChangeProgress < 1f);
        Paint(heatmap);
        Between(255, 0, heatmap.PaintedCellColor(0, 0)!.Value.R);
        Assert.Equal(Color.Black.ToArgb(), heatmap.PaintedCellColor(1, 1)!.Value.ToArgb()); // değişmeyen hücre yerinde

        WaitUntil(() => heatmap.ChangeProgress >= 1f);
        Paint(heatmap);
        Assert.Equal(Color.Black.ToArgb(), heatmap.PaintedCellColor(0, 0)!.Value.ToArgb());
        form.Close();
    }));

    [Fact]
    public void Timeline_dots_pop_up_and_move_when_events_change() => Sta.Run(() => WithMotion(true, () =>
    {
        using var form = Host();
        var timeline = new TwTimeline { Dock = DockStyle.Top, Height = 80 };
        timeline.Items.Add(new TwTimelineEvent(new DateTime(2026, 9, 1), 10));
        timeline.Items.Add(new TwTimelineEvent(new DateTime(2026, 9, 11), 10));
        timeline.Items.Add(new TwTimelineEvent(new DateTime(2026, 9, 21), 10));
        form.Controls.Add(timeline);
        form.Show();
        Paint(timeline);
        var target = timeline.ComputeLayout();
        Assert.True(timeline.IntroProgress < 1f);
        Assert.True(timeline.PaintedDot(2)!.Value.Radius < target.Radii[2]);

        WaitUntil(() => timeline.IntroProgress >= 1f);
        Paint(timeline);
        Assert.Equal(target.Radii[1], timeline.PaintedDot(1)!.Value.Radius, 2);
        float oldX = timeline.PaintedDot(1)!.Value.Center.X;

        timeline.Items[1].Time = new DateTime(2026, 9, 19);
        Assert.True(timeline.ChangeProgress < 1f);
        Paint(timeline);
        Between(oldX, timeline.ComputeLayout().Centers[1].X, timeline.PaintedDot(1)!.Value.Center.X);
        WaitUntil(() => timeline.ChangeProgress >= 1f);
        Paint(timeline);
        Assert.Equal(timeline.ComputeLayout().Centers[1].X, timeline.PaintedDot(1)!.Value.Center.X, 1);
        form.Close();
    }));

    [Fact]
    public void Sparkline_values_move_to_new_ones_and_bars_grow() => Sta.Run(() => WithMotion(true, () =>
    {
        using var form = Host();
        var spark = new TwSparkline { Size = new Size(120, 32), Kind = TwSparklineKind.Bar, Values = new double[] { 4, 4, 4 } };
        form.Controls.Add(spark);
        form.Show();
        Paint(spark);
        Assert.True(spark.IntroProgress < 1f);
        Assert.True(spark.PaintedValues[0] < 4);
        Assert.True(spark.PaintedValues[2] <= spark.PaintedValues[0]);

        WaitUntil(() => spark.IntroProgress >= 1f);
        Paint(spark);
        spark.Values = new double[] { 4, 4, 10 };
        Assert.True(spark.ChangeProgress < 1f);
        Between(4, 10, spark.PaintedValues[2]);
        WaitUntil(() => spark.ChangeProgress >= 1f);
        Assert.Equal(10, spark.PaintedValues[2]);

        // Değer sayısı değişince geçiş yok: yeni değerler hemen çizilir.
        spark.Values = new double[] { 1, 2 };
        Assert.Equal(new double[] { 1, 2 }, spark.PaintedValues);
        form.Close();
    }));
}
