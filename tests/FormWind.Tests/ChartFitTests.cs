using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// Yatay çubuk grafik, ısı haritasının boş hücreleri, KPI değerinin sığdırılması, küçük halka ve buzlu cam yüzey.
[Collection(UiCollection.Name)]
public class ChartFitTests
{
    private static Form NewForm(int width = 640, int height = 400) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(width, height) };

    private static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(Math.Max(1, control.Width), Math.Max(1, control.Height));
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

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

    private sealed class KeyChart : TwChart
    {
        public void Key(Keys key) => OnKeyDown(new KeyEventArgs(key));
    }

    [Fact]
    public void Horizontal_bars_put_categories_in_rows_with_full_labels() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var form = NewForm(520, 260);
        var chart = new KeyChart { Dock = DockStyle.Fill, Kind = TwChartKind.HorizontalBar };
        form.Controls.Add(chart);
        chart.SetData(new[] { "Kalıp değişimi", "Malzeme bekleme", "Arıza", "Ayar" },
            new TwChartSeries("Dakika", new[] { 412.0, 286, 198, 41 }) { ShowValues = true });
        chart.ReferenceLines.Add(new TwChartReferenceLine(300, "Hedef"));
        form.Show();

        var layout = chart.ComputeHorizontalLayout();
        int widest = TextRenderer.MeasureText("Malzeme bekleme", chart.Font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width;
        Assert.Equal(widest, layout.LabelWidth);                                   // etiketler kesilmez
        Assert.False(layout.Axis);                                                 // değerler çubuk ucunda: alt eksen yok
        var bars = Enumerable.Range(0, 4).Select(c => chart.HorizontalBarBounds(layout, 0, c)).ToList();
        Assert.True(bars[0].Width > bars[1].Width && bars[1].Width > bars[3].Width);
        Assert.All(bars, b => Assert.Equal(layout.Plot.Left, b.Left, 1));
        Assert.True(bars[1].Top > bars[0].Bottom);                                 // satırlar alt alta
        Assert.True(bars.Max(b => b.Right) <= layout.Plot.Right + 1);

        // Satırın herhangi bir yeri (etiket dahil) o kategoridir; ↑/↓ satırlar arasında gezer.
        Assert.Equal(1, chart.ItemAt(new Point(chart.Padding.Left + 2, (int)layout.Y(1))));
        chart.Key(Keys.Down);
        chart.Key(Keys.Down);
        Assert.Equal(1, chart.HoverIndex);
        chart.Key(Keys.Up);
        Assert.Equal(0, chart.HoverIndex);
        Assert.Equal("Kalıp değişimi: 412", chart.GetItemText(0));
        using (Render(chart)) { }

        // Yığılmış ve eksenli: iki seri bir satırda art arda; alt eksen yazılır.
        chart.Stacked = true;
        chart.SetData(new[] { "Hat 1", "Hat 2" },
            new TwChartSeries("Planlı", new[] { 100.0, 50 }),
            new TwChartSeries("Plansız", new[] { 60.0, 80 }));
        layout = chart.ComputeHorizontalLayout();
        Assert.True(layout.Axis);
        var first = chart.HorizontalBarBounds(layout, 0, 0);
        var second = chart.HorizontalBarBounds(layout, 1, 0);
        Assert.True(second.Left >= first.Right);
        Assert.Equal(first.Top, second.Top, 1);
        using (Render(chart)) { }
    }));

    [Fact]
    public void Heatmap_draws_missing_values_hatched_and_names_them() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var form = NewForm();
        var heatmap = new TwHeatmap { Dock = DockStyle.Fill, HighColor = Color.FromArgb(220, 60, 20), LowColor = Color.FromArgb(250, 240, 230) };
        form.Controls.Add(heatmap);
        heatmap.SetData(new[] { "Hat 1" }, new[] { "Pzt", "Sal", "Çar" }, new double[,] { { 0, double.NaN, 120 } });
        form.Show();

        Color Center(int column)
        {
            using var bitmap = Render(heatmap);
            var cell = heatmap.ComputeLayout().Cell(0, column);
            return bitmap.GetPixel((int)(cell.X + cell.Width / 2), (int)(cell.Y + cell.Height / 2));
        }
        int Colors(int column)
        {
            using var bitmap = Render(heatmap);
            var cell = Rectangle.Round(heatmap.ComputeLayout().Cell(0, column));
            cell.Inflate(-8, -8);
            return Enumerable.Range(cell.Left, cell.Width).SelectMany(x => Enumerable.Range(cell.Top, cell.Height).Select(y => bitmap.GetPixel(x, y).ToArgb())).Distinct().Count();
        }

        Assert.Equal(heatmap.LowColor.ToArgb(), Center(1).ToArgb());                // varsayılan: en düşük ton (eski davranış)

        heatmap.EmptyCells = TwHeatmapEmptyCell.Solid;
        heatmap.EmptyCellColor = Color.FromArgb(200, 200, 200);
        Assert.Equal(heatmap.EmptyCellColor.ToArgb(), Center(1).ToArgb());
        Assert.Equal(1, Colors(1));

        heatmap.EmptyCells = TwHeatmapEmptyCell.Hatched;
        Assert.True(Colors(1) > 2);                                                 // taralı: zemin ve çizgiler
        Assert.Equal(1, Colors(0));                                                 // değerli hücre düz kalır

        heatmap.EmptyCells = TwHeatmapEmptyCell.None;
        Assert.NotEqual(heatmap.EmptyCellColor.ToArgb(), Center(1).ToArgb());

        Assert.Equal("Hat 1, Sal: —", heatmap.GetItemText(0, 1));
        heatmap.EmptyCellText = "Üretim yok";
        Assert.Equal("Hat 1, Sal: Üretim yok", heatmap.GetItemText(0, 1));
        Assert.Equal("Hat 1, Pzt: 0", heatmap.GetItemText(0, 0));
        heatmap.EmptyCells = TwHeatmapEmptyCell.Hatched;
        heatmap.HoverIndex = 1;                                                     // ipucu ve gösterge çizilir
        using (Render(heatmap)) { }
    }));

    [Fact]
    public void Stat_switches_to_the_short_value_and_then_shrinks_instead_of_cutting() => Sta.Run(() =>
    {
        using var stat = new TwStat { Size = new Size(150, 132), Title = "Toplam duruş", Value = "278 sa 52 dk" };
        int width = stat.Width - stat.Padding.Horizontal;
        var full = stat.FitValue(1000);
        Assert.Equal("278 sa 52 dk", full.Text);

        var fitted = stat.FitValue(width);
        Assert.Equal("278 sa 52 dk", fitted.Text);
        Assert.True(fitted.Points < full.Points);                                   // küçülür
        using (var font = new Font(stat.Font.FontFamily, fitted.Points, FontStyle.Bold))
            Assert.True(TextRenderer.MeasureText(fitted.Text, font, Size.Empty, TextFormatFlags.NoPadding).Width <= width);

        stat.ShortValue = "278 sa";
        Assert.Equal(("278 sa", full.Points), stat.FitValue(width));                // kısa biçim tam boyda sığar

        stat.ShortValue = "";
        stat.ValueFit = TwStatFit.Ellipsis;
        Assert.Equal(full.Points, stat.FitValue(width).Points);                    // eski davranış: kesilir
        Assert.Equal("Toplam duruş", stat.AccessibilityObject.Name);
        Assert.StartsWith("278 sa 52 dk", stat.AccessibilityObject.Value);          // ekran okuyucu tam değeri okur
        using var bitmap = Render(stat);
    });

    [Fact]
    public void Small_ring_chart_keeps_a_readable_ring_and_center_text() => Sta.Run(() =>
    {
        using var ring = new TwRingChart { Size = new Size(36, 36), Padding = new Padding(2) };
        ring.SetData(("Risk", 72));
        var radii = ring.RingRadii(ring.ComputeLayout().Ring);
        Assert.Single(radii);
        Assert.True(radii[0].Width >= 3 * TwDpi.DeviceDpi(ring) / 96f - 0.01f);
        using (var bitmap = Render(ring)) { }

        // Liste satırındaki skor halkası: yay ve iz çizilir.
        using var canvas = new Bitmap(40, 40);
        using (var g = Graphics.FromImage(canvas))
        {
            g.Clear(Color.White);
            TwRingChart.DrawScore(g, new Rectangle(4, 4, 32, 32), 0.25, Color.Red, Color.LightGray, "25", SystemFonts.DefaultFont, Color.Black, 1f);
        }
        var pixels = Enumerable.Range(0, 40).SelectMany(x => Enumerable.Range(0, 40).Select(y => canvas.GetPixel(x, y))).ToList();
        int arc = pixels.Count(p => p.R > 200 && p.G < 80 && p.B < 80), track = pixels.Count(p => p.ToArgb() == Color.LightGray.ToArgb());
        Assert.True(arc > 20);
        Assert.True(track > arc);                                                  // %25: izin çoğu boş kalır
        Assert.True(canvas.GetPixel(30, 8).R > 200 && canvas.GetPixel(30, 8).G < 80);   // yay saat 12'den sağa başlar
    });

    [Fact]
    public void Translucent_and_blurred_panels_show_the_parent_painting_through() => Sta.Run(() =>
    {
        using var form = NewForm(400, 200);
        using var tw = new TailwindProvider();
        var page = new TwPanel { Dock = DockStyle.Fill };
        tw.SetClasses(page, "bg-gradient-to-r from-blue-600 to-red-600");
        var card = new TwPanel { Bounds = new Rectangle(40, 40, 320, 120) };
        page.Controls.Add(card);
        form.Controls.Add(page);
        tw.SetClasses(card, "bg-white/50");
        form.Show();

        using (var bitmap = Render(page))
        {
            // Yarı saydam kart geçişi gösterir: sol mavimsi, sağ kırmızımsı.
            var left = bitmap.GetPixel(card.Left + 10, card.Top + 60);
            var right = bitmap.GetPixel(card.Right - 10, card.Top + 60);
            Assert.True(left.B > left.R);
            Assert.True(right.R > right.B);
        }

        tw.SetClasses(card, "bg-white/40 backdrop-blur-xl");
        Assert.Equal(24, card.BackdropBlur);
        Assert.Equal(24, TwTheme.Current.Parse("backdrop-blur-xl").Resolve(TwState.None).BackdropBlur);
        Assert.Equal(0, TwTheme.Current.Parse("backdrop-blur-none").Resolve(TwState.None).BackdropBlur);
        Assert.Empty(TwTheme.Current.Parse("backdrop-blur backdrop-blur-sm backdrop-blur-2xl").Unknown);
        using (var bitmap = Render(page))
        {
            var left = bitmap.GetPixel(card.Left + 10, card.Top + 60);
            var right = bitmap.GetPixel(card.Right - 10, card.Top + 60);
            Assert.True(left.B > left.R && right.R > right.B);
        }
        tw.SetClasses(card, "bg-white");
        Assert.Equal(0, card.BackdropBlur);
        form.Close();
    });

    [Fact]
    public void Blur_spreads_a_hard_edge_and_keeps_flat_areas() => Sta.Run(() =>
    {
        using var bitmap = new Bitmap(40, 10, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Black);
            g.FillRectangle(Brushes.White, 20, 0, 20, 10);
        }
        TwBlur.Apply(bitmap, 8);
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(0, 5).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(39, 5).ToArgb());
        var edge = bitmap.GetPixel(20, 5);
        Assert.InRange(edge.R, 60, 200);                                           // sert kenar yumuşar
        Assert.True(bitmap.GetPixel(17, 5).R > 0 && bitmap.GetPixel(23, 5).R < 255);
    });
}
