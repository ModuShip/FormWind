using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// TwChart, TwDonutChart, TwSparkline ve TwStat.
[Collection(UiCollection.Name)]
public class ChartTests
{
    private sealed class KeyChart : TwChart
    {
        public void Key(Keys key) => OnKeyDown(new KeyEventArgs(key));
    }

    private static Form NewForm() =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(600, 400) };

    private static void WithCulture(string name, Action action)
    {
        var culture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo(name);
        try
        {
            action();
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = culture;
        }
    }

    private static void Paint(Control control)
    {
        using var bitmap = new Bitmap(Math.Max(1, control.Width), Math.Max(1, control.Height));
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
    }

    [Fact]
    public void Scale_and_compact_numbers_are_round() => WithCulture("tr-TR", () =>
    {
        Assert.Equal((0d, 1200d, 200d), TwCharts.NiceScale(120, 1170, 6, includeZero: true));
        var (min, max, step) = TwCharts.NiceScale(-35, 80, 5, includeZero: false);
        Assert.True(min <= -35 && max >= 80 && step > 0);
        Assert.Equal(0, Math.IEEERemainder(min, step), 6);
        Assert.Equal((2d, 8d, 1d), TwCharts.NiceScale(5, 5, 5, includeZero: false));     // tek değer: aralık açılır

        var previous = TwTexts.Current;
        TwTexts.Current = TwTexts.Turkish;
        try
        {
            Assert.Equal("1,2B", TwCharts.Compact(1200));
            Assert.Equal("3Mn", TwCharts.Compact(3_000_000));
            Assert.Equal("-450", TwCharts.Compact(-450));
            TwTexts.Current = TwTexts.English;
            Assert.Equal("2,5M", TwCharts.Compact(2_500_000));
        }
        finally
        {
            TwTexts.Current = previous;
        }
        Assert.Equal("1.234,5", TwCharts.Format(1234.5, null));
        Assert.Equal("—", TwCharts.Format(double.NaN, null));
    });

    [Fact]
    public void Palette_is_fixed_in_order_and_never_cycles()
    {
        Assert.Equal(ColorTranslator.FromHtml("#2a78d6"), TwCharts.SeriesColor(0, false, null));
        Assert.Equal(ColorTranslator.FromHtml("#3987e5"), TwCharts.SeriesColor(0, true, null));
        Assert.Equal(Color.Pink, TwCharts.SeriesColor(0, false, Color.Pink));          // accent-* yalnızca ilk seri
        Assert.Equal(ColorTranslator.FromHtml("#eb6834"), TwCharts.SeriesColor(1, false, Color.Pink));
        Assert.Equal(8, TwCharts.Light.Distinct().Count());
        Assert.Equal(ColorTranslator.FromHtml("#9ca3af"), TwCharts.SeriesColor(8, false, null));   // 9. seri gri
    }

    [Fact]
    public void Chart_lays_out_series_and_exposes_categories() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var provider = new TailwindProvider();
        using var form = NewForm();
        var chart = new KeyChart { Dock = DockStyle.Fill, Kind = TwChartKind.Bar };
        form.Controls.Add(chart);
        chart.SetData(new[] { "Oca", "Şub", "Mar", "Nis" },
            new TwChartSeries("Gelir", new[] { 120.0, 150, 170, 160 }),
            new TwChartSeries("Gider", new[] { 90.0, 95, double.NaN, 100 }));
        provider.SetClasses(chart, "chart accent-pink-500");
        form.Show();

        Assert.Equal(4, chart.CategoryCount);
        Assert.Equal(ColorTranslator.FromHtml("#ec4899"), chart.GetSeriesColor(0));
        Assert.Equal(ColorTranslator.FromHtml("#eb6834"), chart.GetSeriesColor(1));

        // Çubuklar: en fazla 24 px, aralarında 2 px boşluk; eksik değerin çubuğu yok.
        var layout = chart.ComputeLayout();
        Assert.Equal(0, layout.Min);
        var first = chart.BarBounds(layout, 0, 0);
        var second = chart.BarBounds(layout, 1, 0);
        Assert.True(first.Width <= 24.01f);
        Assert.Equal(2f, second.Left - first.Right, 2);
        Assert.True(chart.BarBounds(layout, 1, 2).IsEmpty);
        Assert.Equal(layout.Y(0), first.Bottom, 1);

        // Yığılmış: ikinci segment birincinin üstünde, 2 px boşlukla.
        chart.Stacked = true;
        layout = chart.ComputeLayout();
        var bottom = chart.BarBounds(layout, 0, 0);
        var top = chart.BarBounds(layout, 1, 0);
        Assert.Equal(bottom.Top - 2, top.Bottom, 1);
        Assert.True(layout.Max >= 210);

        foreach (var kind in new[] { TwChartKind.Line, TwChartKind.Area, TwChartKind.Bar })
        {
            chart.Kind = kind;
            chart.HoverIndex = 1;
            Paint(chart);
        }
        chart.Series[1].Kind = TwChartKind.Line;                                // karışık: çubuk + çizgi
        provider.DarkMode = true;
        Assert.Equal(ColorTranslator.FromHtml("#d95926"), chart.GetSeriesColor(1));
        Paint(chart);

        // Fare ve klavye: kategori bandı, oklar, Enter.
        var band = ((ITwChartItems)chart).ItemBounds(2);
        Assert.Equal(2, chart.ItemAt(new Point(band.X + band.Width / 2, band.Y + 5)));
        Assert.Equal(-1, chart.ItemAt(new Point(1, 1)));
        chart.HoverIndex = -1;
        chart.Key(Keys.Right);
        Assert.Equal(0, chart.HoverIndex);
        chart.Key(Keys.End);
        Assert.Equal(3, chart.HoverIndex);
        TwChartItemEventArgs? clicked = null;
        chart.ItemClick += (_, e) => clicked = e;
        chart.Key(Keys.Enter);
        Assert.Equal("Nis", clicked!.Label);

        var accessible = chart.AccessibilityObject;
        Assert.Equal(AccessibleRole.Chart, accessible.Role);
        Assert.Equal(4, accessible.GetChildCount());
        Assert.Equal("Mar: Gelir 170, Gider —", accessible.GetChild(2)!.Name);
        Assert.Equal("Gelir 120 – 170; Gider 90 – 100", accessible.Value);

        chart.Series.Clear();
        Assert.Equal(4, chart.CategoryCount);                                    // etiketler kalır
        Paint(chart);
        form.Close();
    }));

    [Fact]
    public void Donut_folds_slices_and_finds_them_by_angle() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var form = NewForm();
        var donut = new TwDonutChart { Size = new Size(400, 200) };
        form.Controls.Add(donut);
        donut.SetData(new[] { "Web", "Mağaza", "Telefon" }, new[] { 50.0, 30, 20 });
        form.Show();

        Assert.Equal(100, donut.Total);
        var angles = donut.Angles();
        Assert.Equal(-90f, angles[0].Start);
        Assert.Equal(360f, angles.Sum(a => a.Sweep), 3);
        var (ring, legend, beside) = donut.ComputeLayout();
        Assert.True(beside);                                                     // geniş: gösterge yanda
        float r = ring.Width / 2, mid = r * (1 - donut.Thickness / 2);
        var center = new PointF(ring.X + r, ring.Y + r);
        Assert.Equal(0, donut.ItemAt(Point.Round(new PointF(center.X + 2, center.Y - mid))));       // saat 12'nin hemen sağı
        Assert.Equal(1, donut.ItemAt(Point.Round(new PointF(center.X, center.Y + mid))));           // saat 6: %50–80 arası
        Assert.Equal(-1, donut.ItemAt(Point.Round(center)));                                          // ortadaki boşluk
        Assert.Equal(2, donut.ItemAt(new Point(legend.X + 5, legend.Y + legend.Height - 3)));         // gösterge satırı
        Assert.Equal("Web: 50 (50%)", donut.GetItemText(0));

        donut.Size = new Size(160, 300);
        var narrow = donut.ComputeLayout();
        Assert.False(narrow.Beside, $"{donut.Size} {narrow.Ring} {narrow.Legend}");                             // dar: gösterge altta
        donut.HoverIndex = 1;
        Paint(donut);

        for (int i = 0; i < 9; i++) donut.Slices.Add("Kanal " + i, 1);
        Assert.Equal(TwDonutChart.MaxSlices, donut.DisplaySlices.Count);
        var other = donut.DisplaySlices.Last();
        Assert.Equal("Diğer", other.Label);
        Assert.Equal(5, other.Value, 3);                                         // 12 dilim: ilk 7 kalır, kalan 5 birleşir
        Assert.Equal(donut.Total, donut.DisplaySlices.Sum(s => s.Value), 3);
        Paint(donut);

        donut.Slices.Clear();
        Assert.Equal(-1, donut.HoverIndex);
        Paint(donut);                                                            // boş halka
        Assert.Equal(AccessibleRole.Chart, donut.AccessibilityObject.Role);
        form.Close();
    }));

    [Fact]
    public void Sparkline_and_stat_describe_the_trend() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var provider = new TailwindProvider();
        using var form = NewForm();
        var spark = new TwSparkline { Values = new[] { 3.0, 5, double.NaN, 4, 8 } };
        var stat = new TwStat { Title = "Gelir", Value = "₺1,2 Mn", Delta = 0.124, Caption = "geçen aya göre", Trend = new[] { 1.0, 3, 2, 5 }, Size = new Size(240, 150) };
        form.Controls.Add(spark);
        form.Controls.Add(stat);
        provider.SetClasses(stat, "stat");
        form.Show();

        Assert.Equal("8 (3 – 8)", spark.AccessibilityObject.Value);
        Assert.Equal(AccessibleRole.Chart, spark.AccessibilityObject.Role);
        foreach (var kind in new[] { TwSparklineKind.Line, TwSparklineKind.Area, TwSparklineKind.Bar })
        {
            spark.Kind = kind;
            Paint(spark);
        }

        Assert.Equal("+12,4%", stat.DeltaDisplay);
        Assert.True(stat.IsGood);
        Assert.Equal(ColorTranslator.FromHtml("#15803d"), stat.DeltaColor);   // success-700
        Assert.Equal("Gelir", stat.AccessibilityObject.Name);
        Assert.Equal("₺1,2 Mn, +12,4%, geçen aya göre", stat.AccessibilityObject.Value);
        stat.PositiveIsGood = false;                                             // maliyet: artış kötü
        Assert.False(stat.IsGood);
        Assert.Equal(ColorTranslator.FromHtml("#b91c1c"), stat.DeltaColor);
        stat.Delta = 0;
        Assert.Null(stat.IsGood);
        stat.DeltaText = "+12 sipariş";
        Assert.Equal("+12 sipariş", stat.DeltaDisplay);
        Assert.Equal(Color.White.ToArgb(), stat.BackColor.ToArgb());            // stat bileşeni
        provider.DarkMode = true;
        stat.Delta = -0.05;
        Assert.Equal(ColorTranslator.FromHtml("#4ade80"), stat.DeltaColor);   // dark:success-400 (düşüş iyi)
        Paint(stat);
        form.Close();
    }));

    [Fact]
    public void Chart_components_are_in_every_preset()
    {
        Assert.Equal("chart", TwTheme.DefaultAutoStyle["TwChart"]);
        Assert.Equal("stat", TwTheme.DefaultAutoStyle["TwStat"]);
        foreach (var preset in new[] { TwPreset.Tailwind, TwPreset.Fluent, TwPreset.Antd, TwPreset.Material })
        {
            var theme = new TwTheme(preset);
            Assert.True(theme.Components.ContainsKey("chart"));
            Assert.True(theme.Components.ContainsKey("stat"));
        }
        Assert.Equal("Diğer", TwTexts.Turkish.Other);
        Assert.Equal("K", TwTexts.English.ThousandSuffix);
    }
}
