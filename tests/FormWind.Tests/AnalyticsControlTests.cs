using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// TwChart'ın ikinci ekseni ve kılavuzları; TwRingChart, TwHeatmap, TwTimeline ve TwStepper.
[Collection(UiCollection.Name)]
public class AnalyticsControlTests
{
    private sealed class KeyHeatmap : TwHeatmap
    {
        public void Key(Keys key) => OnKeyDown(new KeyEventArgs(key));
    }

    private sealed class KeyTimeline : TwTimeline
    {
        public void Key(Keys key) => OnKeyDown(new KeyEventArgs(key));
    }

    private static Form NewForm() =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(640, 400) };

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
    public void Pareto_uses_a_secondary_axis_reference_lines_and_markers() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var form = NewForm();
        var chart = new TwChart { Dock = DockStyle.Fill, Kind = TwChartKind.Bar, MaxBarWidth = 56, SecondaryAxisFormat = "'%'0" };
        form.Controls.Add(chart);
        chart.SetData(new[] { "Mekanik", "Malzeme", "Elektrik", "Diğer" },
            new TwChartSeries("Dakika", new[] { 412.0, 286, 198, 41 }) { ShowValues = true },
            new TwChartSeries("Kümülatif", new[] { 44.0, 75, 96, 100 }) { Kind = TwChartKind.Line, Axis = TwChartAxis.Secondary });
        chart.ReferenceLines.Add(new TwChartReferenceLine(80, "%80") { Axis = TwChartAxis.Secondary });
        chart.ReferenceLines.Add(new TwChartReferenceLine(600, "Hedef"));
        chart.Markers.Add(new TwChartMarker(2, "Bugün"));
        form.Show();

        var layout = chart.ComputeLayout();
        Assert.True(layout.Secondary);
        Assert.True(layout.Max >= 600);                                       // referans çizgisi ölçeğe dahil
        Assert.True(layout.Max2 >= 100 && layout.Max2 < 200);                 // sağ eksen kendi ölçeğinde
        Assert.True(layout.AxisWidth2 > 0);
        Assert.True(layout.Plot.Right < chart.Width - chart.Padding.Right - layout.AxisWidth2);
        // Sağ eksendeki %100, soldaki 100 dakikadan çok daha yukarıda.
        Assert.True(layout.Y(100, TwChartAxis.Secondary) < layout.Y(100) - 50);
        var bar = chart.BarBounds(layout, 0, 0);
        Assert.True(bar.Width > 24.5f && bar.Width <= 56.01f);                // MaxBarWidth
        Assert.Equal(layout.Y(412), bar.Top, 1);

        chart.Series[1].Dashed = true;
        chart.Series[1].ShowValues = true;
        chart.HoverIndex = 1;
        Paint(chart);

        // Tek eksen: ikincil seri soldaki ölçeğe döner.
        chart.Series.RemoveAt(0);
        Assert.False(chart.ComputeLayout().Secondary);
        chart.Markers.Clear();
        chart.ReferenceLines[0].Label = "";
        Paint(chart);
        form.Close();
    }));

    [Fact]
    public void Ring_chart_fills_rings_against_their_maximum() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var provider = new TailwindProvider();
        using var form = NewForm();
        var rings = new TwRingChart { Size = new Size(400, 180), ValueFormat = "'%'0.0", CenterText = "71,4", CenterLabel = "OEE %" };
        form.Controls.Add(rings);
        rings.SetData(("Kullanılabilirlik", 82.1), ("Performans", 91.3), ("Kalite", 95.2));
        provider.SetClasses(rings, "chart accent-red-500");
        form.Show();

        Assert.Equal(ColorTranslator.FromHtml("#ef4444"), rings.GetRingColor(0));
        Assert.Equal(0.821, rings.Rings[0].Fraction, 3);
        Assert.Equal(1, new TwChartRing("x", 150).Fraction);                 // taşma kırpılır
        Assert.Equal(0, new TwChartRing("x", 5, 0).Fraction);

        var (square, legend) = rings.ComputeLayout();
        Assert.False(legend.IsEmpty);                                         // üç halka: yanında gösterge
        var radii = rings.RingRadii(square);
        Assert.Equal(3, radii.Count);
        Assert.True(radii[0].Radius > radii[1].Radius && radii[1].Radius > radii[2].Radius);

        // Halkanın üstündeki nokta o halkayı bulur.
        var center = new PointF(square.X + square.Width / 2, square.Y + square.Height / 2);
        Assert.Equal(1, rings.ItemAt(Point.Round(new PointF(center.X, center.Y - radii[1].Radius))));
        Assert.Equal(-1, rings.ItemAt(Point.Round(center)));
        rings.HoverIndex = 2;
        Paint(rings);
        Assert.Equal("Kalite: %95,2 / %100,0", rings.AccessibilityObject.GetChild(2)!.Name);

        // Tek halka: ortada değeri, gösterge yok.
        rings.CenterText = null;
        rings.Rings.Clear();
        rings.Rings.Add(new TwChartRing("Hazır", 6, 9));
        Assert.True(rings.ComputeLayout().Legend.IsEmpty);
        rings.HoverIndex = -1;
        Paint(rings);
        form.Close();
    }));

    [Fact]
    public void Heatmap_shades_cells_and_moves_in_two_dimensions() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var form = NewForm();
        var heatmap = new KeyHeatmap { Dock = DockStyle.Fill, HighColor = Color.FromArgb(255, 200, 0), LowColor = Color.FromArgb(20, 20, 20), LegendMaxText = "120+ dk" };
        form.Controls.Add(heatmap);
        heatmap.SetData(new[] { "Hat 1", "Hat 2" }, new[] { "Pzt", "Sal", "Çar" },
            new double[,] { { 0, 60, double.NaN }, { 30, 120, 240 } });
        heatmap.Maximum = 120;
        form.Show();

        Assert.Equal(2, heatmap.RowCount);
        Assert.Equal(3, heatmap.ColumnCount);
        Assert.Equal((0d, 120d), heatmap.Scale());
        Assert.Equal(heatmap.LowColor.ToArgb(), heatmap.GetCellColor(0).ToArgb());
        Assert.Equal(heatmap.HighColor.ToArgb(), heatmap.GetCellColor(240).ToArgb());   // üst sınırın üstü tam renk
        Assert.Equal(TwCharts.Blend(heatmap.LowColor, heatmap.HighColor, 0.5f), heatmap.GetCellColor(60));
        Assert.Equal(heatmap.LowColor.ToArgb(), heatmap.GetCellColor(double.NaN).ToArgb());

        var layout = heatmap.ComputeLayout();
        var cell = layout.Cell(1, 2);
        Assert.Equal(5, heatmap.ItemAt(Point.Round(new PointF(cell.X + cell.Width / 2, cell.Y + cell.Height / 2))));
        Assert.Equal(-1, heatmap.ItemAt(new Point(1, 1)));

        heatmap.Key(Keys.Down);                                              // ilk tuş ilk hücreyi seçer
        Assert.Equal(0, heatmap.HoverIndex);
        heatmap.Key(Keys.Down);
        heatmap.Key(Keys.Right);
        Assert.Equal(4, heatmap.HoverIndex);
        TwHeatmapCellEventArgs? clicked = null;
        heatmap.CellClick += (_, e) => clicked = e;
        heatmap.Key(Keys.Enter);
        Assert.Equal((1, 1, 120d), (clicked!.Row, clicked.Column, clicked.Value));
        heatmap.ShowValues = true;
        Paint(heatmap);

        Assert.Equal(6, heatmap.AccessibilityObject.GetChildCount());
        Assert.Equal("Hat 1, Çar: —", heatmap.AccessibilityObject.GetChild(2)!.Name);

        // Sıfırı geçen değerler: ıraksak ölçek, negatifler NegativeColor'a gider.
        heatmap.Maximum = double.NaN;
        heatmap.NegativeColor = Color.Blue;
        heatmap.Values = new double[,] { { 1, -1 }, { 0.5, -0.05 } };
        Assert.Equal(Color.Blue.ToArgb(), heatmap.GetCellColor(-1).ToArgb());
        Assert.Equal(heatmap.LowColor.ToArgb(), heatmap.GetCellColor(0).ToArgb());
        Paint(heatmap);
        form.Close();
    }));

    [Fact]
    public void Timeline_places_events_by_time_and_reads_them_in_order() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var form = NewForm();
        var timeline = new KeyTimeline { Size = new Size(600, 90), ValueFormat = "0 'dk'" };
        form.Controls.Add(timeline);
        timeline.Items.Add(new TwTimelineEvent(new DateTime(2026, 10, 16), 47, "DR-24817"));
        timeline.Items.Add(new TwTimelineEvent(new DateTime(2026, 9, 17), 22, "DR-24410"));
        timeline.Items.Add(new TwTimelineEvent(new DateTime(2026, 10, 3), 38));
        form.Show();

        var (left, right, _, centers, radii) = timeline.ComputeLayout();
        Assert.Equal(left, centers[1].X, 1);                                   // en erken solda
        Assert.Equal(right, centers[0].X, 1);                                  // en geç sağda
        Assert.True(centers[2].X > centers[1].X && centers[2].X < centers[0].X);
        Assert.True(radii[0] > radii[2] && radii[2] > radii[1]);               // büyük değer, büyük nokta

        Assert.Equal(2, timeline.ItemAt(Point.Round(centers[2])));
        timeline.Key(Keys.Home);
        Assert.Equal(1, timeline.HoverIndex);
        timeline.Key(Keys.Right);
        Assert.Equal(2, timeline.HoverIndex);
        Paint(timeline);

        var accessible = timeline.AccessibilityObject;
        Assert.Equal("DR-24410, 17 Eyl: 22 dk", accessible.GetChild(0)!.Name);
        Assert.Equal("17 Eyl – 16 Eki", accessible.Value);
        form.Close();
    }));

    [Fact]
    public void Stepper_colors_steps_by_status_and_names_them() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        var previous = TwTexts.Current;
        TwTexts.Current = TwTexts.Turkish;
        try
        {
            using var provider = new TailwindProvider();
            using var form = NewForm();
            var stepper = new TwStepper { Size = new Size(360, 200) };
            form.Controls.Add(stepper);
            stepper.Steps.Add(new TwStep("Operatör girişi", TwStepStatus.Pending, "taslak 00:05") { Badge = "Devam ediyor" });
            stepper.Steps.Add(new TwStep("Amir onayı"));
            stepper.Steps.Add(new TwStep("Bakım şefi kapanışı"));
            provider.SetClasses(stepper, "chart accent-blue-600");
            form.Show();

            stepper.SetCurrent(1);
            Assert.Equal(TwStepStatus.Completed, stepper.Steps[0].Status);
            Assert.Equal(TwStepStatus.Current, stepper.Steps[1].Status);
            Assert.Equal(TwStepStatus.Pending, stepper.Steps[2].Status);
            Assert.Equal(ColorTranslator.FromHtml("#2563eb"), stepper.GetStepColor(1));
            Assert.Equal(ColorTranslator.FromHtml("#15803d"), stepper.GetStepColor(0));   // success-700
            stepper.Steps[2].Status = TwStepStatus.Error;
            Assert.Equal(ColorTranslator.FromHtml("#b91c1c"), stepper.GetStepColor(2));   // danger-700

            Assert.Equal("1. Operatör girişi, tamamlandı, taslak 00:05", stepper.AccessibilityObject.GetChild(0)!.Name);
            Assert.Equal("1 / 3", stepper.AccessibilityObject.Value);

            foreach (var variant in new[] { TwStepperVariant.Vertical, TwStepperVariant.Horizontal, TwStepperVariant.Segments })
            {
                stepper.Variant = variant;
                var bounds = stepper.StepBounds();
                Assert.Equal(3, bounds.Length);
                Assert.True(bounds[1].Left >= bounds[0].Left && bounds[1].Top >= bounds[0].Top);
                Paint(stepper);
            }
            Assert.InRange(Math.Abs(stepper.StepBounds()[0].Width - stepper.StepBounds()[2].Width), 0, 1);   // eşit bölütler
            provider.DarkMode = true;
            Paint(stepper);
            form.Close();
        }
        finally
        {
            TwTexts.Current = previous;
        }
    }));

    [Fact]
    public void Chart_axis_bounds_label_lines_and_point_options() => Sta.Run(() => WithCulture("tr-TR", () =>
    {
        using var form = NewForm();
        var chart = new TwChart { Dock = DockStyle.Fill, Kind = TwChartKind.Bar, AxisMaximum = 450, SecondaryAxisMinimum = 0, SecondaryAxisMaximum = 100, SecondaryAxisInterval = 20 };
        form.Controls.Add(chart);
        var bars = new TwChartSeries("Dakika", new[] { 412.0, 286, 41 }) { PointColors = new[] { Color.Empty, Color.Empty, Color.Navy } };
        chart.SetData(new[] { "Mekanik\narıza", "Malzeme\nbekleme", "Diğer" }, bars,
            new TwChartSeries("Kümülatif", new[] { 55.0, 93, 100 }) { Kind = TwChartKind.Line, Axis = TwChartAxis.Secondary, Points = TwChartPoints.All });
        form.Show();

        var layout = chart.ComputeLayout();
        Assert.Equal((0d, 450d), (layout.Min, layout.Max));
        Assert.Equal(0, Math.IEEERemainder(450, layout.Step), 6);            // otomatik adım aralığı böler
        Assert.Equal((0d, 100d, 20d), (layout.Min2, layout.Max2, layout.Step2));
        int line = TextRenderer.MeasureText("Ag", chart.Font).Height;
        chart.Categories = new[] { "Mekanik", "Malzeme", "Diğer" };
        var single = chart.ComputeLayout();
        Assert.True(single.Plot.Height - layout.Plot.Height >= line - 4);     // iki satırlı etiket bir satır yer alır

        Assert.Equal(Color.Navy, bars.PointColor(2));
        Assert.True(bars.PointColor(0).IsEmpty);
        Assert.True(bars.PointColor(9).IsEmpty);
        chart.AxisInterval = 150;
        Assert.Equal(150, chart.ComputeLayout().Step);
        chart.LegendAlignment = HorizontalAlignment.Right;
        chart.Markers.Add(new TwChartMarker(1, "Not") { ShowLine = false });
        chart.Series[1].Points = TwChartPoints.None;
        chart.HoverIndex = 1;
        Paint(chart);

        // Isı haritası: çok satırlı sütun etiketleri ızgarayı aşağı iter.
        var heat = new TwHeatmap { Size = new Size(400, 200) };
        form.Controls.Add(heat);
        heat.SetData(new[] { "A" }, new[] { "Hafta sonu\nsonrası" }, new double[,] { { 1 } });
        int twoLines = heat.ComputeLayout().Grid.Top;
        heat.ColumnLabels = new[] { "Hafta sonu" };
        Assert.True(twoLines - heat.ComputeLayout().Grid.Top >= line - 4);
        Assert.Equal("A, Hafta sonu: 1", new TwHeatmap { RowLabels = new[] { "A" }, ColumnLabels = new[] { "Hafta\nsonu" }, Values = new double[,] { { 1 } } }.GetItemText(0, 0));

        // Satır bazında sparkline rengi kopyalanır.
        var cell = new TwSparklineCell { LineColor = Color.Red };
        Assert.Equal(Color.Red, ((TwSparklineCell)cell.Clone()).LineColor);
        form.Close();
    }));

    [Fact]
    public void Icon_buttons_size_to_icon_and_text() => Sta.Run(() =>
    {
        using var form = NewForm();
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;
        var icon = new TwButton { Text = "Raporu dışa aktar", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, IconName = "download", IconSpacing = 6, Padding = new Padding(12, 6, 12, 6) };
        var only = new TwButton { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, IconName = "search", Padding = new Padding(6) };
        var growing = new TwButton { Text = "Raporu dışa aktar", AutoSize = true, IconName = "download", IconSpacing = 6 };
        form.Controls.AddRange(new Control[] { icon, only, growing });
        form.Show();
        int em = TwIconRenderer.EmPixels(icon.Font.SizeInPoints, TwDpi.DeviceDpi(icon));
        int text = TextRenderer.MeasureText(icon.Text, icon.Font, Size.Empty, flags).Width;
        Assert.Equal(24 + em + 6 + text + 1, icon.GetPreferredSize(Size.Empty).Width);
        Assert.Equal(12 + em + 1, only.GetPreferredSize(Size.Empty).Width);

        // GrowOnly: önerilen boyut yerleşimden yerleşime büyümez (ikon payı mevcut boyuta tekrar eklenmez).
        int first = growing.GetPreferredSize(Size.Empty).Width;
        growing.PerformLayout();
        form.PerformLayout();
        Assert.Equal(first, growing.GetPreferredSize(Size.Empty).Width);
        Assert.Equal(first, growing.Width);
        form.Close();
    });

    [Fact]
    public void New_controls_take_the_chart_component()
    {
        foreach (var name in new[] { "TwRingChart", "TwHeatmap", "TwTimeline", "TwStepper" })
            Assert.Equal("chart", TwTheme.DefaultAutoStyle[name]);
        Assert.Equal("bekliyor", TwTexts.Turkish.StepPending);
        Assert.Equal("completed", TwTexts.English.StepCompleted);
        Assert.Equal(Color.White, TwCharts.ReadableOn(ColorTranslator.FromHtml("#1f2937")));
        Assert.Equal(ColorTranslator.FromHtml("#111827"), TwCharts.ReadableOn(ColorTranslator.FromHtml("#fde68a")));
    }
}
