using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FormWind.Showcase
{
    /// <summary>
    /// Üretim duruş panosu: Pareto (çubuk + ikinci eksende kümülatif çizgi ve %80 hedefi), OEE kayıp ağacı
    /// (<see cref="TwRingChart"/>), hat × gün ısı haritası ve korelasyon matrisi (<see cref="TwHeatmap"/>), kronik arıza
    /// zaman çizelgesi (<see cref="TwTimeline"/>), üretim kaybı projeksiyonu (kesikli seriler ve "Bugün" işareti) ve onay
    /// akışı (<see cref="TwStepper"/>). Kod ile kurulur (<c>--downtime</c>).
    /// </summary>
    public sealed class DowntimeForm : Form
    {
        private readonly TailwindProvider _tw;
        private readonly TwSwitch _dark;
        private readonly Label _status;

        public DowntimeForm()
        {
            _tw = new TailwindProvider();
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "FormWind Duruş Merkezi";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(1240, 980);
            StartPosition = FormStartPosition.CenterScreen;

            var header = new TwPanel { Dock = DockStyle.Top, Height = 64 };
            var title = new Label { AutoSize = true, Text = "Vardiya Kapanış Merkezi" };
            var spacer = new TwPanel();
            _dark = new TwSwitch { AutoSize = true, Text = "Koyu" };
            header.Controls.AddRange(new Control[] { title, spacer, _dark });

            var statusBar = new TwPanel { Dock = DockStyle.Bottom, Height = 36 };
            _status = new Label { AutoSize = true, Text = "Bir kategoriye, hücreye ya da olaya tıklayın" };
            statusBar.Controls.Add(_status);

            // Pareto: duruş dakikaları çubuk, kümülatif pay sağ eksende çizgi; %80 hedefi kesikli.
            var pareto = new TwChart { Kind = TwChartKind.Bar, MaxBarWidth = 48, SecondaryAxisFormat = "'%'0", AccessibleName = "Duruş süresi dağılımı" };
            string[] causes = { "Mekanik", "Malzeme", "Elektrik", "Kalıp", "Kalite", "Operatör", "Diğer" };
            double[] minutes = { 412, 286, 198, 164, 97, 58, 41 };
            double total = minutes.Sum(), running = 0;
            var cumulative = minutes.Select(m => Math.Round((running += m) / total * 100, 1)).ToArray();
            pareto.SetData(causes,
                new TwChartSeries("Dakika", minutes) { ShowValues = true },
                new TwChartSeries("Kümülatif %", cumulative) { Kind = TwChartKind.Line, Axis = TwChartAxis.Secondary, Color = Color.FromArgb(234, 160, 0) });
            pareto.ReferenceLines.Add(new TwChartReferenceLine(80, "%80") { Axis = TwChartAxis.Secondary, Color = Color.FromArgb(234, 160, 0) });

            // OEE kayıp ağacı: üç bileşen iç içe halkalar, ortada OEE.
            var oee = new TwRingChart { CenterText = "71,4", CenterLabel = "OEE %", ValueFormat = "'%'0.0", AccessibleName = "OEE kayıp ağacı" };
            oee.Rings.Add(new TwChartRing("Kullanılabilirlik", 82.1) { Color = Color.FromArgb(239, 68, 68) });
            oee.Rings.Add(new TwChartRing("Performans", 91.3) { Color = Color.FromArgb(245, 158, 11) });
            oee.Rings.Add(new TwChartRing("Kalite", 95.2) { Color = Color.FromArgb(59, 130, 246) });

            var completion = new TwRingChart { CenterLabel = "hazır", CenterText = "6/9", Thickness = 0.12f, AccessibleName = "Tamamlanma" };
            completion.Rings.Add(new TwChartRing("Tamamlanan kayıt", 6, 9));

            // Hat × gün: vardiya başına duruş dakikası (14 gün × 3 vardiya = 42 sütun).
            string[] lines = { "Hat 1 · Presleme", "Hat 2 · Kaynak", "Hat 3 · Montaj", "Hat 4 · Boya", "Hat 5 · Paketleme" };
            var random = new Random(7);
            var days = new double[lines.Length, 42];
            for (int r = 0; r < lines.Length; r++)
                for (int c = 0; c < 42; c++)
                    days[r, c] = Math.Round(Math.Max(0, random.NextDouble() * (r == 1 ? 130 : 70) - 10));
            var columns = Enumerable.Range(0, 42).Select(i => i % 3 == 0 ? (3 + i / 3).ToString("00") : "").ToArray();
            var heat = new TwHeatmap { Maximum = 120, LegendMinText = "0", LegendMaxText = "120+ dk", ValueFormat = "0 'dk'", AccessibleName = "Duruş ısı haritası" };
            heat.SetData(lines, columns, days);

            // Korelasyon matrisi: ıraksak ölçek, değerler hücrede.
            string[] factors = { "Yağlama", "Kalıp değ.", "Hafta sonu", "Vardiya B", "Malzeme", "Sıklık" };
            var corr = new TwHeatmap { ShowValues = true, ValueFormat = "0.00", Minimum = -1, Maximum = 1, LegendMinText = "−1", LegendMaxText = "+1", AccessibleName = "Kök neden korelasyonu" };
            corr.SetData(factors, factors, new[,]
            {
                { 1, 0.71, 0.64, 0.18, -0.05, 0.82 },
                { 0.71, 1, 0.58, 0.22, 0.02, 0.66 },
                { 0.64, 0.58, 1, 0.31, -0.08, 0.74 },
                { 0.18, 0.22, 0.31, 1, 0.12, 0.39 },
                { -0.05, 0.02, -0.08, 0.12, 1, 0.27 },
                { 0.82, 0.66, 0.74, 0.39, 0.27, 1 },
            });

            // Kronik arıza: aynı yatakta son 30 günde 6 duruş; aralıklar kısalıyor.
            var timeline = new TwTimeline { ValueFormat = "0 'dk'", AccessibleName = "Olay zaman çizelgesi" };
            var start = new DateTime(2026, 9, 17);
            foreach (var (day, value) in new[] { (0, 22.0), (9, 31.0), (16, 38.0), (21, 29.0), (25, 41.0), (29, 47.0) })
                timeline.Items.Add(new TwTimelineEvent(start.AddDays(day), value, "KR-204 · Eksen-3") { Color = Color.FromArgb(239, 68, 68) });

            // Projeksiyon: gerçekleşen çizgi, iki senaryo kesikli; bugün işaretli.
            var projection = new TwChart { Kind = TwChartKind.Line, ValueFormat = "#,0 'adet'", AccessibleName = "Kümülatif üretim kaybı" };
            var labels = new[] { "−30 g", "−20 g", "−10 g", "Bugün", "+10 g", "+20 g", "+30 g" };
            double n = double.NaN;
            projection.SetData(labels,
                new TwChartSeries("Gerçekleşen", new[] { 0, 1200, 2900, 5120, n, n, n }),
                new TwChartSeries("Aksiyonsuz", new[] { n, n, n, 5120, 9800, 15300, 22100 }) { Dashed = true, ShowValues = true, Color = Color.FromArgb(239, 68, 68) },
                new TwChartSeries("Önleyici aksiyonla", new[] { n, n, n, 5120, 6600, 7900, 9100 }) { Dashed = true, ShowValues = true, Color = Color.FromArgb(34, 197, 94) });
            projection.Markers.Add(new TwChartMarker(3, "Bugün"));

            // Onay akışı: dikey, yatay ve CAPA aşama bölütleri.
            var flow = new TwStepper { AccessibleName = "Onay akışı" };
            flow.Steps.Add(new TwStep("Operatör girişi", description: "[AD SOYAD] · taslak 00:05") { Badge = "Devam ediyor" });
            flow.Steps.Add(new TwStep("Vardiya amiri onayı", description: "[AMİR ADI] · B vardiyası") { Badge = "Bekliyor" });
            flow.Steps.Add(new TwStep("Bakım şefi kapanışı", description: "Mekanik ve elektrik kayıtları için") { Badge = "Bekliyor" });
            flow.SetCurrent(0);
            var chips = new TwStepper { Variant = TwStepperVariant.Horizontal, AccessibleName = "Kayıt onay akışı" };
            chips.Steps.Add(new TwStep("Operatör girdi", TwStepStatus.Completed, "18:55"));
            chips.Steps.Add(new TwStep("Amir onayı bekliyor", TwStepStatus.Current));
            chips.Steps.Add(new TwStep("Bakım şefi kapanışı"));
            var phases = new TwStepper { Variant = TwStepperVariant.Segments, AccessibleName = "CAPA aşamaları" };
            foreach (var phase in new[] { "Tanım", "Kök neden", "Aksiyon", "Doğrulama", "Kapanış" }) phases.Steps.Add(new TwStep(phase));
            phases.SetCurrent(2);
            phases.Steps[2].Status = TwStepStatus.Error;
            var flowCard = new TwPanel();
            var flowLabel = new Label { AutoSize = true, Text = "Onay akışı" };
            flowCard.Controls.AddRange(new Control[] { flowLabel, flow, chips, phases });

            var body = new TwPanel { Dock = DockStyle.Fill, AutoScroll = true };
            body.Controls.AddRange(new Control[]
            {
                Card("Duruş süresi dağılımı (Pareto)", pareto, "col-span-4 h-72"),
                Card("OEE kayıp ağacı", oee, "col-span-2 h-72"),
                Card("Duruş ısı haritası · gün × vardiya", heat, "col-span-6 h-56"),
                Card("Olay zaman çizelgesi", timeline, "col-span-4 h-36"),
                Card("Tamamlanma", completion, "col-span-2 h-36"),
                Card("Kümülatif üretim kaybı projeksiyonu", projection, "col-span-3 h-72"),
                Card("Kök neden korelasyon matrisi", corr, "col-span-3 h-72"),
                flowCard,
            });
            Controls.AddRange(new Control[] { body, statusBar, header });

            _tw.SetClasses(this, "page");
            _tw.SetClasses(header, "flex items-center gap-3 px-6 py-3");
            _tw.SetClasses(title, "text-xl font-semibold");
            _tw.SetClasses(spacer, "flex-1");
            _tw.SetClasses(body, "grid grid-cols-6 gap-4 px-6 pb-4");
            _tw.SetClasses(heat, "flex-1 accent-amber-500");
            _tw.SetClasses(flowCard, "card flex flex-col gap-3 col-span-6 h-80");
            _tw.SetClasses(flowLabel, "text-sm font-semibold");
            _tw.SetClasses(flow, "h-44");
            _tw.SetClasses(chips, "h-8");
            _tw.SetClasses(phases, "h-4");
            _tw.SetClasses(corr, "flex-1 accent-amber-500");
            _tw.SetClasses(statusBar, "flex items-center px-6");
            _tw.SetClasses(_status, "muted");
            _tw.ContainerControl = this;
            _tw.AutoStyle = true;

            _dark.CheckedChanged += (_, _) => _tw.DarkMode = _dark.Checked;
            pareto.ItemClick += (_, e) => _status.Text = "Pareto · " + pareto.GetItemText(e.Index);
            oee.ItemClick += (_, e) => _status.Text = "OEE · " + oee.GetItemText(e.Index);
            heat.CellClick += (_, e) => _status.Text = "Isı haritası · " + heat.GetItemText(e.Row, e.Column);
            corr.CellClick += (_, e) => _status.Text = "Korelasyon · " + corr.GetItemText(e.Row, e.Column);
            timeline.ItemClick += (_, e) => _status.Text = "Zaman çizelgesi · " + timeline.GetItemText(e.Index);
            ResumeLayout(true);
        }

        public bool DarkMode
        {
            get => _dark.Checked;
            set => _dark.Checked = value;
        }

        /// <summary>Başlıklı bir kart (<paramref name="classes"/>: ızgaradaki genişliği ve yüksekliği); içerik kalan alanı doldurur.</summary>
        private TwPanel Card(string caption, Control content, string classes)
        {
            var card = new TwPanel();
            var label = new Label { AutoSize = true, Text = caption };
            card.Controls.AddRange(new[] { label, content });
            _tw.SetClasses(card, "card flex flex-col gap-2 " + classes);
            _tw.SetClasses(label, "text-sm font-semibold");
            if (_tw.GetClasses(content).Length == 0) _tw.SetClasses(content, "flex-1");
            return card;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tw.Dispose();
            base.Dispose(disposing);
        }
    }
}
