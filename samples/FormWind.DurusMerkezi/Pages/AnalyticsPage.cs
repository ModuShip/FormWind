using System.Drawing;
using System.Windows.Forms;
using FormWind;

namespace DurusMerkezi
{
    /// <summary>Kestirimci Analitik & Raporlama: dönem KPI'ları, vardiya trendi ve karşılaştırması, kök neden korelasyonu,
    /// beklenen sonraki arızalar, makine karşılaştırması, veri disiplini karnesi ve CAPA karnesi.</summary>
    internal sealed class AnalyticsPage : Page
    {
        private static readonly Color Mechanical = Ui.ColorOf(Tone.Blue), Material = Ui.ColorOf(Tone.Amber),
            Electrical = ColorTranslator.FromHtml("#1d4d8f"), Other = ColorTranslator.FromHtml("#a1a1aa");

        public AnalyticsPage(Ui ui) : base(ui)
        {
        }

        public override string Title => "Analitik & Raporlar";
        public override string Subtitle => "Yönetici görünümü";
        public override string SideTitle => "Kayıtlı raporlar";

        public override SideItem[] SideItems => new[]
        {
            new SideItem("Haftalık yönetim özeti", null, ""),
            new SideItem("CAPA karnesi", null, ""),
            new SideItem("Makine karşılaştırma", null, ""),
            new SideItem("Kök neden korelasyonu", null, ""),
            new SideItem("Veri disiplini", null, ""),
        };

        protected override Control[] BuildToolbar()
        {
            var period = Ui.Segments("Hafta", "Ay", "Çeyrek", "Yıl");
            period.SelectedIndex = 2;
            return new Control[]
            {
                period, Ui.Plain("Önceki dönemle karşılaştır", "compare"), Ui.Primary("Raporu dışa aktar", "download"), Ui.IconButton("search", "Ara"),
            };
        }

        protected override TwPanel BuildContent() => PageBody(
            "Kestirimci Analitik & Raporlama",
            "2026 Ç3–Ç4 · son 12 hafta · tüm hatlar, 3 vardiya",
            new Control[] { Ui.Button("Hat: Tümü", "btn-sm text-sm py-1.5"), Ui.Button("Vardiya: Tümü", "btn-sm text-sm py-1.5"), Ui.Button("Makine grubu: Tümü", "btn-sm text-sm py-1.5"), Ui.Button("Kategori: Plansız", "btn-sm text-sm py-1.5") },
            Ui.Row("items-stretch gap-4 h-[116px]",
                Ui.Kpi("Toplam plansız duruş", "3.912 dk", "", "−%8", Tone.Green, "önceki döneme göre", captionRight: false),
                Ui.Kpi("Plansız / toplam oranı", "%68", "", "+3 pt", Tone.Amber, "hedef %55", captionRight: false),
                Ui.Kpi("Ortalama MTBF", "46,2 sa", "", "+%9", Tone.Green, "38 makine", captionRight: false),
                Ui.Kpi("Önlenen tahmini kayıp", "₺ 412 bin", "", "17 uyarı", Tone.Blue, "erken müdahale", captionRight: false)),
            Ui.Row("items-stretch gap-4 h-[352px]", Trend(), ShiftComparison()),
            Ui.Row("items-stretch gap-4 h-[440px]", Correlation(), NextFailures()),
            Machines(), Discipline(), Capa());

        private TwPanel Trend()
        {
            var chart = new TwChart
            {
                Kind = TwChartKind.Line, ShowLegend = false, ValueFormat = "#,0 'dk'", AccessibleName = "Vardiya bazlı duruş trendi",
                AxisMinimum = 0, AxisMaximum = 600, AxisInterval = 200, AxisFormat = "0",
            };
            var weeks = new[] { "H31", "H32", "H33", "H34", "H35", "H36", "H37", "H38", "H39", "H40", "H41", "H42" };
            chart.SetData(weeks,
                new TwChartSeries("Vardiya A", new[] { 205.0, 228, 195, 214, 190, 196, 182, 178, 205, 186, 172, 166 }) { Color = Ui.ColorOf(Tone.Blue), Points = TwChartPoints.None },
                new TwChartSeries("Vardiya B", new[] { 318.0, 300, 340, 312, 352, 336, 360, 400, 455, 390, 350, 325 }) { Color = Ui.ColorOf(Tone.Amber), Points = TwChartPoints.None },
                new TwChartSeries("Vardiya C", new[] { 262.0, 250, 266, 240, 252, 230, 242, 236, 258, 226, 218, 210 }) { Color = Ui.ColorOf(Tone.Violet), Dashed = true, Points = TwChartPoints.None });
            chart.Markers.Add(new TwChartMarker(8, "H41 · KR-204 arızaları") { Color = Ui.ColorOf(Tone.Amber), ShowLine = false });
            Ui.Style(chart, "chart flex-1 text-xs");
            var legend = Ui.Legend(("Vardiya A", Ui.ColorOf(Tone.Blue), 'l'), ("Vardiya B", Ui.ColorOf(Tone.Amber), 'l'), ("Vardiya C", Ui.ColorOf(Tone.Violet), 'd'));
            return Ui.Card("Vardiya bazlı duruş trendi", "Haftalık plansız duruş dakikası · son 12 hafta", legend, "basis-3/5 px-5", chart);
        }

        private TwPanel ShiftComparison()
        {
            var body = Ui.Col("gap-4");
            foreach (var (name, info, parts, total) in new[]
                     {
                         ("Vardiya A · 08–16", "2.448 dk · MTTR 31 dk", new[] { 960.0, 690, 600, 290 }, 4250.0),
                         ("Vardiya B · 16–24", "4.250 dk · MTTR 44 dk", new[] { 1820.0, 760, 920, 300 }, 4250.0),
                         ("Vardiya C · 00–08", "2.913 dk · MTTR 36 dk", new[] { 1190.0, 870, 520, 270 }, 4250.0),
                     })
            {
                var bar = new SegmentBar { Total = total, Parts = new[] { (parts[0], Mechanical), (parts[1], Material), (parts[2], Electrical), (parts[3], Other) }, AccessibleName = name };
                Ui.Style(bar, "h-[12px]");
                body.Controls.Add(Ui.Col("gap-2", Ui.Row("", Ui.Text(name, "strong"), Ui.Spacer(), Ui.Text(info, "muted")), bar));
            }
            TwPanel Key(string text, Color color)
            {
                var swatch = new LegendKey { Color = color, Kind = 's' };
                return Ui.Row("gap-1.5", Ui.Style(swatch, "w-[10px] h-[10px] flex-none"), Ui.Text(text, "small"));
            }
            var legend = Ui.Row("gap-4", Key("Mekanik", Mechanical), Key("Malzeme", Material), Key("Elektrik", Electrical), Key("Diğer", Other));
            var note = Ui.Note("Vardiya B, mekanik arızalarda A'nın 1,7 katı. Gece geçişindeki görsel kontrol atlanması olası neden.", 40);
            return Ui.Card("Vardiya karşılaştırma", "Duruş dakikasının kategori kırılımı", null, "basis-2/5 px-5", body, legend, note);
        }

        private TwPanel Correlation()
        {
            string[] rows = { "Yağlama gecikmesi", "Kalıp değişimi sonrası", "Hafta sonu sonrası ilk vardiya", "Vardiya B", "Malzeme partisi", "Duruş sıklığı" };
            string[] columns = { "Yağlama\ngecikmesi", "Kalıp\ndeğişimi\nsonrası", "Hafta sonu\nsonrası ilk\nvardiya", "Vardiya B", "Malzeme\npartisi", "Duruş\nsıklığı" };
            var heat = Ui.Heat(new TwHeatmap
            {
                ShowValues = true, ValueFormat = "0.##", Minimum = -1, Maximum = 1, AccessibleName = "Kök neden korelasyon matrisi",
                HighColor = Ui.ColorOf(Tone.Amber), NegativeColor = ColorTranslator.FromHtml("#1d4d8f"),
            }, "flex-1 text-xs");
            heat.SetData(rows, columns, new[,]
            {
                { 1, 0.71, 0.64, 0.18, -0.05, 0.82 },
                { 0.71, 1, 0.58, 0.22, 0.02, 0.66 },
                { 0.64, 0.58, 1, 0.31, -0.08, 0.74 },
                { 0.18, 0.22, 0.31, 1, 0.12, 0.39 },
                { -0.05, 0.02, -0.08, 0.12, 1, 0.27 },
                { 0.82, 0.66, 0.74, 0.39, 0.27, 1 },
            });
            var legend = Ui.Gradient("−1", "+1", "bg-gradient-to-r from-primary-500 via-surface-950 to-warning-500");
            return Ui.Card("Kök neden korelasyon matrisi", "Pearson r · 90 gün, 1.482 duruş", legend, "basis-3/5 px-5", heat);
        }

        private TwPanel NextFailures()
        {
            var list = Ui.Col("gap-3.5");
            foreach (var (code, name, when, note, share, tone) in new[]
                     {
                         ("KR-204", "Eksen-3 redüktör", "19 Eki · ~3 gün", "MTBF 9,1 → 2,8 gün · son arıza 16 Eki", 0.88, Tone.Red),
                         ("PR-03", "Hidrolik pompa", "23 Eki · ~7 gün", "MTBF 12 → 7 gün · 4 tekrar", 0.62, Tone.Amber),
                         ("PK-05", "Çıkış fotoseli", "25 Eki · ~9 gün", "Sık ama kısa duruşlar, eğilim yatay", 0.48, Tone.Amber),
                         ("BY-104", "Robot boya nozulu", "2 Kas · ~17 gün", "Son 2 haftada 3 kayıt", 0.3, Tone.Blue),
                         ("MN-11", "Vidalama ünitesi", "12 Kas · ~27 gün", "Seyrek, eğilim iyileşiyor", 0.14, Tone.Blue),
                     })
            {
                string color = tone == Tone.Blue ? "text-surface-900 dark:text-white" : $"text-{Ui.Palette(tone)}-500 dark:text-{Ui.Palette(tone)}-400";
                list.Controls.Add(Ui.Col("gap-1.5",
                    Ui.Row("gap-1.5", Ui.Text(code, "strong"), Ui.Text("· " + name, "muted"), Ui.Spacer(), Ui.Text(when, "strong " + color)),
                    Ui.Bar(share * 100, 100, tone, "h-2"), Ui.Text(note, "small")));
            }
            return Ui.Card("Beklenen sonraki arıza", "MTBF eğilimi ve son arıza tarihinden · vardiya kayıtlarıyla", Ui.Chip("Kayıt tabanlı", Tone.Gray), "basis-2/5 px-5", list);
        }

        private TwPanel Machines()
        {
            var grid = Ui.Grid("Makine bazlı karşılaştırma", 46);
            var trend = new TwSparklineColumn { Name = "trend", HeaderText = "12 hafta", LineColor = Ui.ColorOf(Tone.Red) };
            var delta = new TwBadgeColumn
            {
                Name = "delta", HeaderText = "Δ",
                Variants = Ui.BadgeVariants(("+%41", Tone.Red), ("+%18", Tone.Red), ("+%4", Tone.Red), ("+%2", Tone.Red),
                    ("−%6", Tone.Green), ("−%22", Tone.Green), ("−%9", Tone.Green), ("−%15", Tone.Green)),
            };
            grid.Columns.AddRange(
                new DataGridViewTextBoxColumn { Name = "machine", HeaderText = "Makine" }, new DataGridViewTextBoxColumn { Name = "line", HeaderText = "Hat" },
                new DataGridViewTextBoxColumn { Name = "count", HeaderText = "Duruş" }, new DataGridViewTextBoxColumn { Name = "total", HeaderText = "Toplam dk" },
                new DataGridViewTextBoxColumn { Name = "mtbf", HeaderText = "MTBF sa" }, new DataGridViewTextBoxColumn { Name = "mttr", HeaderText = "MTTR dk" }, trend, delta);
            var data = new (string Machine, string Line, string Count, string Total, string Mtbf, string Mttr, double[] Trend, string Delta)[]
            {
                ("KR-204 Robot Kaynak Hücresi", "Hat 2", "14", "486", "22,4", "35", new[] { 5.0, 5.5, 6, 5.5, 5, 4.5, 4.8, 5.2, 6, 6.5, 6.3, 6 }, "+%41"),
                ("PR-03 Hidrolik Pres", "Hat 1", "11", "352", "31,0", "32", new[] { 4.0, 4.5, 5, 5.5, 5.2, 4.8, 4.6, 5, 5.8, 6.2, 6, 5.8 }, "+%18"),
                ("BY-104 Robot Boya", "Hat 4", "9", "318", "36,8", "35", new[] { 6.0, 5.6, 5.2, 5.8, 6, 5.6, 5.4, 5.8, 6, 5.6, 5, 4.8 }, "−%6"),
                ("PK-05 Streç Sarma", "Hat 5", "23", "214", "14,9", "9", new[] { 5.0, 4.8, 4.8, 5.2, 5.8, 6, 5.8, 5.4, 5.6, 5.8, 5.6, 6.2 }, "+%4"),
                ("BY-102 Fırın Konveyörü", "Hat 4", "8", "205", "41,2", "26", new[] { 5.0, 5.2, 5.6, 5.8, 5.4, 5, 4.8, 5, 5.2, 5, 4.8, 5 }, "−%22"),
                ("KR-207 Punta Kaynak", "Hat 2", "7", "176", "47,0", "25", new[] { 5.0, 5.4, 5.8, 5.6, 5.2, 4.8, 4.8, 5, 5.2, 5, 5, 5.2 }, "−%9"),
                ("MN-11 Vidalama İstasyonu", "Hat 3", "12", "148", "28,5", "12", new[] { 5.0, 5.4, 5.6, 5.4, 5.2, 5.2, 5.4, 5.8, 6.2, 6.4, 6.2, 6 }, "+%2"),
                ("PR-01 Transfer Pres", "Hat 1", "5", "131", "66,3", "26", new[] { 6.0, 5.8, 5.4, 5, 4.8, 5, 5.2, 5.4, 5.2, 5, 4.8, 4.6 }, "−%15"),
            };
            foreach (var d in data)
            {
                int row = grid.Rows.Add(d.Machine, d.Line, d.Count, d.Total, d.Mtbf, d.Mttr, d.Trend, d.Delta);
                // Kötüleşen makinenin trendi kırmızı, iyileşenin yeşil.
                ((TwSparklineCell)grid.Rows[row].Cells[trend.Index]).LineColor = d.Delta.StartsWith("+") ? Ui.ColorOf(Tone.Red) : Ui.ColorOf(Tone.Green);
            }
            Ui.Tw.SetColumnClasses(grid.Columns[0], "flex-1 font-semibold");
            Ui.Tw.SetColumnClasses(grid.Columns[1], "w-24 text-surface-500 dark:text-surface-400");
            foreach (int i in new[] { 2, 4, 5 }) Ui.Tw.SetColumnClasses(grid.Columns[i], "w-24");
            Ui.Tw.SetColumnClasses(grid.Columns[3], "w-24 font-bold");
            Ui.Tw.SetColumnClasses(trend, "w-36");
            Ui.Tw.SetColumnClasses(delta, "w-24");
            Ui.Style(grid, "table-plain table-striped row:h-[46px] h-[408px]");
            return Ui.Card("Makine bazlı karşılaştırma", "En çok duruş üreten 8 makine · 2026 Ç3–Ç4 · son 12 hafta", Ui.Button("Sütunlar", "btn-sm text-sm py-1.5"), "px-5", grid);
        }

        private TwPanel Discipline()
        {
            var header = Ui.TableRow(40, Ui.Text("Vardiya", "small w-36"), Ui.Text("Zamanında giriş (hedef %90)", "small flex-1"), Ui.Text("Ort. gecikme", "small w-40"),
                Ui.Text("Eksik alan", "small w-40"), Ui.Text("\"Diğer\" oranı", "small w-40"), Ui.Text("Amir onay süresi", "small w-40"));
            var table = Ui.Col("gap-0", header);
            foreach (var (shift, onTime, tone, delay, missing, missingTone, other, otherTone, approve) in new[]
                     {
                         ("Vardiya A", 94.0, Tone.Green, "6 dk", "% 2", Tone.Green, "% 7", Tone.Green, "18 dk"),
                         ("Vardiya B", 71.0, Tone.Red, "27 dk", "% 9", Tone.Red, "% 18", Tone.Amber, "52 dk"),
                         ("Vardiya C", 86.0, Tone.Amber, "11 dk", "% 4", Tone.Green, "% 11", Tone.Green, "31 dk"),
                     })
            {
                var bar = Ui.Row("gap-3 flex-1", Ui.Bar(onTime, 100, tone, "h-1.5 w-[264px]"), Ui.Text($"%{onTime:0}", "strong flex-none"));
                table.Controls.Add(Ui.TableRow(48, Ui.Text(shift, "strong w-36"), bar, Ui.Text(delay, "body w-40"),
                    Ui.Box("w-40", Ui.Chip(missing, missingTone)), Ui.Box("w-40", Ui.Chip(other, otherTone)), Ui.Text(approve, "body w-40")));
            }
            var note = Ui.Paragraph("Vardiya B'de girişler ortalama 27 dk gecikiyor ve kayıtların %18'i \"Diğer\" olarak sınıflanıyor. Bu, kök neden analizinin güvenilirliğini düşürüyor.", 20, "muted");
            return Ui.Card("Vardiya veri disiplini karnesi", "Girişlerin zamanında, eksiksiz ve doğru sınıflandırılmış olması · 2026 Ç3–Ç4 · son 12 hafta",
                Ui.Chip("Vardiya B hedef altında", Tone.Amber), "px-5", table, note);
        }

        private TwPanel Capa()
        {
            TwPanel Tile(string label, string value, string tone = "") => Ui.Col("tile flex-1 gap-0.5 px-4 py-3", Ui.Text(label, "small"), Ui.Text(value, "text-2xl font-semibold text-surface-900 dark:text-white " + tone));
            var tiles = Ui.Row("items-stretch gap-3", Tile("Açık", "14"), Tile("Gecikmiş", "3", "text-danger-500 dark:text-danger-400"), Tile("Zamanında kapanma", "%78"), Tile("Etkinliği doğrulanan", "9 / 12"));
            var header = Ui.TableRow(36, Ui.Text("Faaliyet", "small flex-1"), Ui.Text("Aşama · Tanım → Kök neden → Aksiyon → Doğrulama → Kapanış", "small w-[340px]"), Ui.Text("Durum", "small w-[92px]"));
            var rows = Ui.Col("gap-0", header);
            foreach (var (title, meta, phase, error, state, tone) in new[]
                     {
                         ("Yağlama periyodunu çalışma saatine bağla", "CAPA-0912 · Bakım · termin 20 Eki", 3, false, "Aksiyonda", Tone.Blue),
                         ("Eksen-3 redüktör hizalama kontrolü", "CAPA-0909 · Mekanik · termin 14 Eki", 3, true, "Gecikmiş", Tone.Red),
                         ("Hidrolik pompa filtre değişim standardı", "CAPA-0897 · Bakım · termin 28 Eki", 2, false, "Kök neden", Tone.Amber),
                         ("Fotosel temizlik adımını otonom bakıma ekle", "CAPA-0871 · Üretim · termin 02 Eki", 5, false, "Doğrulandı", Tone.Green),
                     })
            {
                var phases = new TwStepper { Variant = TwStepperVariant.Segments, AccessibleName = title };
                foreach (var name in new[] { "Tanım", "Kök neden", "Aksiyon", "Doğrulama", "Kapanış" })
                    phases.Steps.Add(new TwStep(name));
                for (int i = 0; i < phases.Steps.Count; i++)
                {
                    var step = phases.Steps[i];
                    step.Status = i < phase ? TwStepStatus.Completed : TwStepStatus.Pending;
                    step.Color = i < phase ? Ui.ColorOf(Tone.Blue) : Color.Empty;
                    if (error && i == phase - 1)
                    {
                        step.Status = TwStepStatus.Error;
                        step.Color = Ui.ColorOf(Tone.Red);
                    }
                }
                Ui.Style(phases, "chart w-[340px] h-[6px] divide-surface-200 dark:divide-surface-950");
                var chip = Ui.Text(state, $"chip-{tone.ToString().ToLowerInvariant()} py-1 text-left px-2.5 w-[92px] flex-none");
                rows.Controls.Add(Ui.TableRow(52, Ui.Col("gap-0.5 flex-1", Ui.Text(title, "strong"), Ui.Text(meta, "mono")), phases, chip));
            }
            return Ui.Card("Aksiyon takip karnesi (CAPA)", "Düzeltici ve önleyici faaliyetler · etkinlik doğrulaması dahil", null, "px-5", tiles, rows);
        }
    }
}
