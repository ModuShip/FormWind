using System;
using System.Windows.Forms;
using FormWind;

namespace DurusMerkezi
{
    /// <summary>Kronik Arıza Analizörü: tekrarlayan duruş kümeleri, seçili kümenin risk skoru, olay zaman çizelgesi,
    /// vardiya × gün örüntüsü, kayıp projeksiyonu, bileşen ömrü, tespit kuralları ve önleyici aksiyonlar.</summary>
    internal sealed class ChronicFailuresPage : Page
    {
        public ChronicFailuresPage(Ui ui) : base(ui)
        {
        }

        public override string Title => "Kronik Arızalar";
        public override string Subtitle => "Örüntü tanıma · vardiya kapanışı sonrası 00:15";
        public override string SideTitle => "Tespit kaynakları";

        public override SideItem[] SideItems => new[]
        {
            new SideItem("Kural motoru", Tone.Blue, "12"),
            new SideItem("ML kümeleme", Tone.Violet, "3"),
            new SideItem("Metin benzerliği", Tone.Amber, "4"),
        };

        protected override Control[] BuildToolbar() => new Control[]
        {
            Ui.Segments("Aktif 5", "İzlemede 3", "Çözüldü 12"),
            Ui.Search("Bileşen, makine", "w-64"),
            Ui.Plain("Kuralları düzenle"),
        };

        protected override TwPanel BuildContent() => PageBody(
            "Kronik Arıza Analizörü",
            "Tekrarlayan duruşlar, aynı bileşen ve aynı kök neden üzerinden otomatik kümelenir.",
            new Control[] { Ui.Chip("2 yüksek risk", Tone.Red), Ui.Chip("Kronik kayıp · 540 dk / 30 gün", Tone.Gray) },
            Ui.Row("items-start gap-4", Clusters(), Ui.Col("gap-4 flex-1", Hero(), Timeline(), Pattern(),
                Ui.Row("items-stretch gap-4 h-[408px]", Projection(), Lifecycle()), Rules(), Actions())));

        private TwRingChart Score(double score, Tone tone, int size)
        {
            bool big = size > 80;
            var ring = new TwRingChart { CenterText = score.ToString("0"), ShowLegend = false, Thickness = big ? 0.1f : 0.14f, TabStop = false, AccessibleName = "Risk skoru" };
            ring.Rings.Add(new TwChartRing("Risk skoru", score) { Color = Ui.ColorOf(tone) });
            if (big) ring.CenterLabel = "Risk skoru";
            Ui.Style(ring, $"chart w-[{size}px] h-[{size}px] flex-none" + (big ? " text-xs" : " text-[11px]"));
            return ring;
        }

        // ---- Kümeler ----

        private TwPanel Clusters()
        {
            var list = Ui.Col("gap-2 basis-[392px]");
            var items = new (double Score, string Code, string Title, bool Wrap, string Stats, string Trend, Tone Tone)[]
            {
                (87, "KR-204 · Hat 2", "Eksen-3 redüktör yatağı", false, "6 duruş / 30 gün · 214 dk", "Kötüleşiyor", Tone.Red),
                (71, "PR-03 · Hat 1", "Hidrolik pompa basınç düşümü", true, "4 duruş / 30 gün · 132 dk", "Kötüleşiyor", Tone.Amber),
                (54, "PK-05 · Hat 5", "Çıkış fotoseli kirlenmesi", false, "9 duruş / 30 gün · 58 dk", "Sabit", Tone.Amber),
                (48, "BY-102 · Hat 4", "Konveyör zincir gerginliği", false, "3 duruş / 30 gün · 96 dk", "İyileşiyor", Tone.Blue),
                (36, "MN-11 · Hat 3", "Vidalama tork sapması", false, "5 duruş / 30 gün · 40 dk", "Sabit", Tone.Blue),
            };
            for (int i = 0; i < items.Length; i++)
            {
                var c = items[i];
                var trend = c.Trend == "Kötüleşiyor" ? (c.Tone == Tone.Red ? Tone.Red : Tone.Amber) : c.Trend == "İyileşiyor" ? Tone.Green : Tone.Gray;
                var title = c.Wrap ? (Control)Ui.Paragraph(c.Title, 40, "strong") : Ui.Text(c.Title, "strong");
                var text = Ui.Col("gap-0.5 flex-1", Ui.Text(c.Code, "mono"), title, Ui.Text(c.Stats, "small"));
                var card = Ui.Row("card gap-3.5 px-4 py-3 " + (c.Wrap ? "h-[110px]" : "h-[92px]") + (i == 0 ? " border-primary-500 dark:border-primary-500" : ""),
                    Score(c.Score, c.Tone, 54), text, Ui.Chip(c.Trend, trend));
                list.Controls.Add(card);
            }
            return list;
        }

        // ---- Seçili küme ----

        private TwPanel Hero()
        {
            var chips = Ui.Row("gap-2", Ui.Chip("Kronik · otomatik tespit", Tone.Violet), Ui.Text("KR-204 · Hat 2", "chip-gray py-0.5 font-mono flex-none"));
            var text = Ui.Col("gap-2 flex-1", chips, Ui.Paragraph("Aynı mil yatağında son 30 günde 6. duruş", 60, "text-xl font-bold text-surface-900 dark:text-white"),
                Ui.Paragraph("Eksen-3 redüktör yatağı · duruşlar arası süre her seferinde kısalıyor; MTBF eğilimine göre beklenen sonraki arıza 19 Eki civarı, Vardiya B ağırlıklı.", 60, "body"));
            var buttons = Ui.Col("gap-2 w-48 pt-8", Ui.Primary("Önleyici aksiyon oluştur"), Ui.Button("Kronik etiketini onayla", "btn-sm text-sm py-1.5"));
            var head = Ui.Row("items-start gap-5", Score(87, Tone.Red, 120), text, buttons);

            TwPanel Metric(string label, string value) => Ui.Col("gap-1.5 flex-1", Ui.Text(label, "small"), Ui.Text(value, "text-xl font-semibold text-surface-900 dark:text-white"));
            var metrics = Ui.Row("items-start gap-4", Metric("Duruş sayısı (30 g)", "6"), Metric("Toplam süre", "214 dk"), Metric("MTBF eğilimi", "9,1 → 2,8 g"), Metric("Kümülatif kayıp", "5.120 adet"));
            var cost = Ui.Row("", Metric("Maliyet (30 g)", "₺ 96.400"));
            return Ui.Box("hero flex flex-col gap-5", head, Ui.Divider(), metrics, cost);
        }

        private TwPanel Timeline()
        {
            var timeline = new TwTimeline { ValueFormat = "0 'dk'", AccessibleName = "Olay zaman çizelgesi" };
            var start = new DateTime(2026, 9, 17);
            foreach (var (day, value) in new[] { (0, 22.0), (9, 31.0), (16, 38.0), (21, 29.0), (25, 41.0), (29, 47.0) })
                timeline.Items.Add(new TwTimelineEvent(start.AddDays(day), value, "KR-204 · Eksen-3") { Color = Ui.ColorOf(Tone.Red) });
            Ui.Style(timeline, "chart h-[86px] divide-surface-200 dark:divide-surface-950");
            var gaps = Ui.Row("gap-2", Ui.Text("Arıza arası süre:", "small"), Ui.Chip("9 gün", Tone.Gray), Ui.Chip("7 gün", Tone.Amber), Ui.Chip("5 gün", Tone.Amber),
                Ui.Chip("4 gün", Tone.Red), Ui.Chip("3 gün", Tone.Red));
            return Ui.Card("Olay zaman çizelgesi", "Son 30 gün · nokta boyutu duruş süresini gösterir", Ui.Chip("Aralıklar kısalıyor", Tone.Red), "px-5", timeline, gaps);
        }

        private TwPanel Pattern()
        {
            var heat = Ui.Heat(new TwHeatmap { ShowValues = true, Maximum = 4, ValueFormat = "0", AccessibleName = "Vardiya × gün örüntüsü", HighColor = Ui.ColorOf(Tone.Amber) }, "h-[150px]");
            double n = double.NaN;
            heat.SetData(new[] { "Vardiya A", "Vardiya B", "Vardiya C" }, new[] { "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" },
                new[,] { { 1, n, n, 1, n, n, n }, { 4, 1, 2, 1, 1, 1, 1 }, { 2, 1, n, 1, 1, n, n } });
            return Ui.Card("Vardiya × gün örüntüsü", "Bu kümedeki duruşlar · son 90 gün, 18 olay", Ui.Chip("Vardiya B · Pazartesi yoğun", Tone.Amber), "px-5",
                heat, Ui.Paragraph("Olayların %61'i Vardiya B'de ve hafta sonu duruşundan sonraki ilk vardiyada yoğunlaşıyor. Uzun duruş sonrası ilk çalıştırmada yağlama kontrolü atlanıyor olabilir.", 40, "body"));
        }

        private TwPanel Projection()
        {
            var chart = new TwChart
            {
                Kind = TwChartKind.Line, ValueFormat = "#,0 'adet'", AccessibleName = "Kümülatif üretim kaybı projeksiyonu", LegendAlignment = HorizontalAlignment.Left,
                AxisMinimum = 0, AxisMaximum = 24000, AxisInterval = 8000, AxisFormat = "#,0,' bin';-#,0,' bin';0",
            };
            double n = double.NaN;
            chart.SetData(new[] { "−30 g", "−15 g", "Bugün", "+15 g", "+30 g" },
                new TwChartSeries("Gerçekleşen", new[] { 0, 1900, 5120, n, n }) { Color = Ui.ColorOf(Tone.Blue), Points = TwChartPoints.None },
                new TwChartSeries("Aksiyonsuz", new[] { n, n, 5120, 12400, 22100 }) { Kind = TwChartKind.Area, Dashed = true, ShowValues = true, Color = Ui.ColorOf(Tone.Red), Points = TwChartPoints.None },
                new TwChartSeries("Önleyici aksiyonla", new[] { n, n, 5120, 7300, 9100 }) { Dashed = true, ShowValues = true, Color = Ui.ColorOf(Tone.Green), Points = TwChartPoints.None });
            chart.Markers.Add(new TwChartMarker(2, "Bugün"));
            Ui.Style(chart, "chart flex-1 text-xs");
            return Ui.Card("Kümülatif üretim kaybı projeksiyonu", "Adet · 60 günlük ufuk", null, "basis-7/12 px-5", chart);
        }

        private TwPanel Lifecycle()
        {
            var current = Ui.Col("gap-2",
                Ui.Row("gap-2", Ui.Paragraph("Mevcut parça · 4 Eki'de takıldı", 20, "body text-[13px] flex-1"), Ui.Text("%38 sağlık", "strong flex-none text-danger-500 dark:text-danger-400")),
                Ui.Bar(38, 100, Tone.Red, "h-2.5"),
                Ui.Row("", Ui.Text("180 sa çalıştı", "small"), Ui.Spacer(), Ui.Text("Nominal L10: 2.000 sa", "small")));
            var history = Ui.Col("gap-2.5", Ui.Text("Son 90 gün değişim geçmişi · ömür kısalıyor", "strong text-xs"));
            foreach (var (when, hours, tone) in new[] { ("Tem 28", 410.0, Tone.Blue), ("Ağu 24", 290.0, Tone.Amber), ("Eyl 18", 220.0, Tone.Amber), ("Eki 04 →", 180.0, Tone.Red) })
                history.Controls.Add(Ui.Row("gap-3", Ui.Text(when, "muted w-20"), Ui.Bar(hours, 2000 / 4.0, tone, "h-1.5 flex-1"), Ui.Text($"{hours:0} sa", "font-mono text-xs font-bold text-surface-900 dark:text-white w-14 text-right")));
            var note = Ui.Note("Ömür, nominal değerin %9'una kadar düştü. Hizalama veya yük kaynaklı sistemik neden olası.", 60);
            return Ui.Card("Bileşen yaşam döngüsü", "Rulman 6205-2RS · Eksen-3 redüktörü", null, "basis-5/12 px-5", current, history, note);
        }

        private TwPanel Rules()
        {
            var grid = Ui.Box("grid grid-cols-2 gap-3 h-[220px]");
            foreach (var (title, detail, kind, tone) in new[]
                     {
                         ("Aynı bileşen ≥ 3 duruş / 30 gün", "6 duruş, eşik 3", "Kural", Tone.Blue),
                         ("Arıza arası süre azalıyor", "Eğim −1,5 gün / olay", "Kural", Tone.Blue),
                         ("Aynı ERP kodu tekrarı", "M-110 · 30 günde 6 kez", "Kural", Tone.Blue),
                         ("Vardiya yoğunlaşması", "18 olayın 11'i Vardiya B'de", "Örüntü", Tone.Amber),
                         ("Benzer kök neden metinleri", "5 kayıtta \"yatak\", \"ısınma\"", "ML", Tone.Violet),
                     })
            {
                var check = Ui.Icon("check", "w-4 h-4 text-success-500 dark:text-success-400");
                grid.Controls.Add(Ui.Row("tile items-start gap-3 py-3", Ui.Box("pt-0.5", check), Ui.Col("gap-0.5", Ui.Text(title, "strong"), Ui.Text(detail, "small")), Ui.Spacer(), Ui.Chip(kind, tone)));
            }
            return Ui.Card("Eşleşen tespit kuralları", "Bu kümeyi kronik olarak işaretleyen sinyaller", null, "px-5", grid);
        }

        private TwPanel Actions()
        {
            var header = Ui.TableRow(36, Ui.Text("Aksiyon", "small flex-1"), Ui.Text("Sorumlu", "small w-28"), Ui.Text("Termin", "small w-16"), Ui.Text("İlerleme", "small w-28"), Ui.Text("Durum", "small w-32"));
            var table = Ui.Col("gap-0", header);
            foreach (var (action, owner, unit, due, progress, tone, state) in new[]
                     {
                         ("Yağlamayı çalışma saatine bağla (CMMS)", "[AD SOYAD] ·", "Bakım", "20 Eki", 60.0, Tone.Blue, "Devam ediyor"),
                         ("Eksen-3 redüktör hizalama kontrolü", "[AD SOYAD] ·", "Mekanik", "14 Eki", 20.0, Tone.Red, "Gecikmiş"),
                         ("Uzun duruş sonrası ilk çalıştırma kontrol listesi", "[AD SOYAD] ·", "Üretim", "24 Eki", 0.0, Tone.Gray, "Planlandı"),
                     })
            {
                var chip = Ui.Text(state, $"chip-{tone.ToString().ToLowerInvariant()} py-1 text-left px-2.5 w-32 flex-none");
                table.Controls.Add(Ui.TableRow(64, Ui.Paragraph(action, 20, "strong flex-1"), Ui.Col("w-28", Ui.Text(owner, "body"), Ui.Text(unit, "body")),
                    Ui.Text(due, "font-mono text-xs font-bold text-surface-900 dark:text-white w-16"), Ui.Box("w-28 flex items-center", Ui.Bar(progress, 100, tone, "h-1.5 w-[104px]")), chip));
            }
            return Ui.Card("Önleyici aksiyon takibi", "CAPA bağlantılı · 4 aksiyon, 1 gecikmiş", Ui.Button("Aksiyon ekle", "btn-sm text-sm py-1.5"), "px-5", table);
        }
    }
}
