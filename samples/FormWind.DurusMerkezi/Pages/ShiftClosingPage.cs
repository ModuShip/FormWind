using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind;

namespace DurusMerkezi
{
    /// <summary>Vardiya Kapanış Merkezi: vardiya sonu KPI'ları, veri giriş durumu, hat karneleri, Pareto, OEE kayıp ağacı,
    /// ısı haritası, veri kalitesi uyarıları ve vardiya devir teslimi.</summary>
    internal sealed class ShiftClosingPage : Page
    {
        public ShiftClosingPage(Ui ui) : base(ui)
        {
        }

        public override string Title => "Vardiya Kapanışı";
        public override string Subtitle => "Fabrika 1 · vardiya sonu verileri";
        public override string SideTitle => "Hatlar · son vardiya";

        public override SideItem[] SideItems => new[]
        {
            new SideItem("Hat 1  ·  Presleme", Tone.Green, "%78"),
            new SideItem("Hat 2  ·  Kaynak", Tone.Red, "%58"),
            new SideItem("Hat 3  ·  Montaj", Tone.Green, "%81"),
            new SideItem("Hat 4  ·  Boya", Tone.Amber, "%66"),
            new SideItem("Hat 5  ·  Paketleme", Tone.Amber, "%72"),
        };

        public override string Footer => "Son veri: Vardiya B kapanışı · 00:12";
        public override Tone FooterTone => Tone.Amber;

        protected override Control[] BuildToolbar() => new Control[]
        {
            Ui.Segments("Son vardiya", "Dün", "7 gün", "30 gün"),
            Ui.Plain("16 Eki · Vardiya B  ⌄", "calendar"),
            Ui.Search("Makine, duruş no, ERP kod", "w-60"),
            Ui.Primary("Vardiya girişi", "edit"),
        };

        protected override TwPanel BuildContent() => PageBody(
            "Vardiya Kapanış Merkezi",
            "16 Ekim 2026 · Vardiya B (16:00–00:00) · 5 hat, 34 duruş kaydı",
            new Control[] { Ui.Chip("●  Kapanış açık · 2 hat eksik", Tone.Amber), Ui.Chip("Giriş son saati 00:40", Tone.Gray) },
            Ui.Row("items-stretch gap-4 h-[116px]",
                Ui.Kpi("OEE (vardiya)", "71,4", "%", "−2,1 pt", Tone.Red, "önceki vardiyaya göre", captionRight: false),
                Ui.Kpi("Toplam duruş", "286", "dk", "34 kayıt", Tone.Gray, "planlı hariç"),
                Ui.Kpi("MTBF", "46,2", "saat", "+3,8 sa", Tone.Green, "30 gün ort."),
                Ui.Kpi("MTTR", "38", "dk", "+6 dk", Tone.Amber, "hedef 30 dk"),
                Ui.Kpi("Üretim kaybı", "1.840", "adet", "₺ 92 bin", Tone.Gray, "tahmini")),
            Ui.Row("items-stretch gap-4 h-[376px]", EntryStatus(), WaitingRootCause()),
            LineCards(),
            Ui.Row("items-stretch gap-4 h-[352px]", Pareto(), OeeTree()),
            Heatmap(),
            Ui.Row("items-stretch gap-4 h-[476px]", DataQuality(), Handover()));

        // ---- Veri giriş durumu ----

        private TwPanel EntryStatus()
        {
            var rows = new (string Line, string Status, Tone Tone, int Done, int Total, string Time, string Action)[]
            {
                ("Hat 1 · Presleme", "Onaylandı", Tone.Green, 6, 6, "23:52", "Görüntüle"),
                ("Hat 2 · Kaynak", "Amir onayında", Tone.Blue, 9, 9, "00:05", "Onayla"),
                ("Hat 3 · Montaj", "Onaylandı", Tone.Green, 4, 4, "23:48", "Görüntüle"),
                ("Hat 4 · Boya", "Eksik alan var", Tone.Amber, 5, 8, "00:09", "Tamamla"),
                ("Hat 5 · Paketleme", "Girilmedi", Tone.Red, 0, 7, "—", "Giriş yap"),
            };
            var list = Ui.Col("gap-0");
            foreach (var r in rows)
            {
                var name = Ui.Col("w-44", Ui.Text(r.Line, "strong"), Ui.Text("[AD SOYAD] · operatör", "small"));
                var badge = Ui.Box("w-32", Ui.Chip(r.Status, r.Tone));
                var barTone = r.Done == r.Total ? (r.Tone == Tone.Blue ? Tone.Blue : Tone.Green) : r.Done == 0 ? Tone.Gray : Tone.Amber;
                var progress = Ui.Col("gap-1.5 w-[160px]", Ui.Bar(r.Done, r.Total, barTone, "h-1.5"), Ui.Text($"{r.Done} / {r.Total} kayıt tamam", "small"));
                list.Controls.Add(Ui.TableRow(58, name, badge, progress, Ui.Text(r.Time, "mono w-12 ml-2"), Ui.Spacer(), Ui.Button(r.Action)));
            }
            var right = Ui.Row("gap-3", Ui.Text("2 onaylı · 1 onayda · 2 eksik", "small"), Ui.Button("Hatırlatma gönder", "btn-sm text-sm py-1.5"));
            return Ui.Card("Veri giriş durumu", "Vardiya B · hat bazında rapor ve onay", right, "basis-3/5 px-5", list);
        }

        private TwPanel WaitingRootCause()
        {
            TwPanel Tile(string label, string value, string tone) =>
                Ui.Col("tile flex-1 gap-0 h-[76px]", Ui.Text(label, "small"), Ui.Text(value, "big " + tone));
            var tiles = Ui.Row("items-stretch gap-2 h-[76px] flex-none",
                Tile("Bugün", "5", ""), Tile("1–3 gün", "7", "text-warning-500 dark:text-warning-400"), Tile("3 günden eski", "4", "text-danger-500 dark:text-danger-400"));
            var items = new (string Machine, string Record, string Age, Tone Tone)[]
            {
                ("PR-01 Transfer Pres", "DR-24410 · 10 Eki · Vardiya C", "6 gün", Tone.Red),
                ("BY-104 Robot Boya", "DR-24533 · 12 Eki · Vardiya A", "4 gün", Tone.Red),
                ("KR-204 Robot Kaynak", "DR-24588 · 13 Eki · Vardiya B", "3 gün", Tone.Amber),
                ("MN-11 Vidalama", "DR-24620 · 13 Eki · Vardiya C", "3 gün", Tone.Amber),
            };
            var list = Ui.Col("gap-0");
            foreach (var i in items)
                list.Controls.Add(Ui.TableRow(44, Ui.Col("", Ui.Text(i.Machine, "strong"), Ui.Text(i.Record, "mono")), Ui.Spacer(), Ui.Chip(i.Age, i.Tone)));
            return Ui.Card("Kök neden bekleyen duruşlar", "Bekleme süresine göre · hedef 48 saat", Ui.Chip("16", Tone.Red), "basis-2/5 px-5",
                tiles, Ui.Text("En eski kayıtlar", "small"), list);
        }

        // ---- Hat karneleri ----

        private TwPanel LineCards()
        {
            var lines = new (string Name, string Area, double Oee, string Delta, Tone DeltaTone, Tone Bar, double[] Trend, int Down, string Cause)[]
            {
                ("Hat 1", "Presleme", 78, "+1,4 pt", Tone.Green, Tone.Green, new[] { 77.0, 77, 78, 76, 74, 74, 76, 77, 77, 76, 74, 73, 75 }, 38, "Kalıp değişimi"),
                ("Hat 2", "Kaynak", 58, "−9,2 pt", Tone.Red, Tone.Red, new[] { 70.0, 71, 68, 66, 68, 70, 70, 71, 70, 67, 66, 68, 70 }, 112, "Mekanik · KR-204"),
                ("Hat 3", "Montaj", 81, "+0,6 pt", Tone.Green, Tone.Green, new[] { 80.0, 78, 76, 78, 80, 80, 80, 81, 79, 77, 79, 80, 80 }, 21, "Malzeme bekleme"),
                ("Hat 4", "Boya", 66, "−3,8 pt", Tone.Amber, Tone.Amber, new[] { 64.0, 63, 65, 67, 67, 66, 67, 68, 66, 63, 65, 66, 68 }, 74, "Malzeme bekleme"),
                ("Hat 5", "Paketleme", 72, "−1,1 pt", Tone.Amber, Tone.Amber, new[] { 70.0, 73, 74, 73, 73, 74, 72, 70, 71, 74, 74, 75, 74 }, 41, "Sensör · PK-05"),
            };
            var grid = Ui.Box("grid grid-cols-4 gap-3 h-[420px]");
            foreach (var l in lines)
            {
                var head = Ui.Row("items-start", Ui.Col("", Ui.Text(l.Name, "strong"), Ui.Text(l.Area, "small")), Ui.Spacer(), Ui.Chip(l.Delta, l.DeltaTone));
                var value = Ui.Row("items-end", Ui.Text($"%{l.Oee:0}", "text-2xl font-semibold text-surface-900 dark:text-white"), Ui.Spacer(), Ui.Text("OEE", "small pb-1"));
                var foot = Ui.Row("gap-1", Ui.Text("Duruş", "small"), Ui.Text($"{l.Down} dk", "strong text-xs"), Ui.Spacer(), Ui.Text(l.Cause, "small"));
                grid.Controls.Add(Ui.Col("tile gap-2.5 px-4 py-3.5 h-[204px]", head, value, Ui.Bar(l.Oee, 100, l.Bar, "h-1.5"), Ui.Spark(l.Trend, l.Bar, "h-9"), Ui.Spacer(), foot));
            }
            return Ui.Card("Hat karneleri", "Son vardiya ile önceki vardiya karşılaştırması · çizgi: son 14 vardiya OEE",
                Ui.Chip("Hedef OEE %85", Tone.Gray), "px-5", grid);
        }

        // ---- Pareto ve OEE ağacı ----

        private TwPanel Pareto()
        {
            string[] causes = { "Mekanik\narıza", "Malzeme\nbekleme", "Elektrik /\nsensör", "Kalıp\ndeğişimi", "Kalite\ndurdurma", "Operatör\nyok", "Diğer" };
            double[] minutes = { 412, 286, 198, 164, 97, 58, 41 };
            double total = minutes.Sum(), running = 0;
            var cumulative = minutes.Select(m => Math.Round((running += m) / total * 100, 1)).ToArray();
            // İlk üç neden ("hayati az") parlak, kalanlar koyu mavi.
            var navy = ColorTranslator.FromHtml("#1d4d8f");
            var colors = minutes.Select((_, i) => i < 3 ? Color.Empty : navy).ToArray();
            var chart = new TwChart
            {
                Kind = TwChartKind.Bar, MaxBarWidth = 64, ShowLegend = false, AccessibleName = "Duruş süresi dağılımı",
                AxisMinimum = 0, AxisMaximum = 450, AxisInterval = 150, AxisFormat = "0",
                SecondaryAxisMinimum = 0, SecondaryAxisMaximum = 100, SecondaryAxisInterval = 100 / 3.0, SecondaryAxisFormat = "'%'0",
            };
            chart.SetData(causes,
                new TwChartSeries("Dakika", minutes) { ShowValues = true, Color = Ui.ColorOf(Tone.Blue), PointColors = colors },
                new TwChartSeries("Kümülatif %", cumulative) { Kind = TwChartKind.Line, Axis = TwChartAxis.Secondary, Color = Ui.ColorOf(Tone.Amber), Points = TwChartPoints.All });
            chart.ReferenceLines.Add(new TwChartReferenceLine(80, "") { Axis = TwChartAxis.Secondary, Color = Ui.ColorOf(Tone.Amber) });
            Ui.Style(chart, "chart flex-1 text-xs");
            var legend = Ui.Legend(("Dakika", Ui.ColorOf(Tone.Blue), 's'), ("Kümülatif %", Ui.ColorOf(Tone.Amber), 'l'));
            return Ui.Card("Duruş süresi dağılımı (Pareto)", "Son 7 gün · toplam 1.256 dk · ilk 3 neden kaybın %71'i", legend, "basis-3/5 px-5", chart);
        }

        private TwPanel OeeTree()
        {
            var rings = new TwRingChart { CenterText = "71,4", CenterLabel = "OEE %", ShowLegend = false, Thickness = 0.12f, AccessibleName = "OEE kayıp ağacı" };
            rings.Rings.Add(new TwChartRing("Kullanılabilirlik", 82.1) { Color = Ui.ColorOf(Tone.Red) });
            rings.Rings.Add(new TwChartRing("Performans", 91.3) { Color = Ui.ColorOf(Tone.Amber) });
            rings.Rings.Add(new TwChartRing("Kalite", 95.2) { Color = Ui.ColorOf(Tone.Blue) });
            Ui.Style(rings, "chart w-[140px] h-[140px] flex-none");
            var top = Ui.Row("gap-5 h-[144px] flex-none", rings,
                Ui.Paragraph("Kayıpların %64'ü kullanılabilirlik kaynaklı. En büyük katkı Hat 2 kaynak hücresindeki plansız duruşlar.", 64, "body flex-1"));

            TwPanel Factor(string name, string value, string note, double percent, Tone tone) => Ui.Col("gap-2 flex-none",
                Ui.Row("gap-1", Ui.Text(name, "strong"), Ui.Spacer(), Ui.Text(value, "strong"), Ui.Text(note, "muted")),
                Ui.Bar(percent, 100, tone, "h-2"));
            return Ui.Card("OEE kayıp ağacı", "Vardiya B · hedef %85", Ui.Chip("−13,6 pt", Tone.Amber), "basis-2/5 px-5", top,
                Factor("Kullanılabilirlik", "%82,1", "· kayıp 86 dk duruş", 82.1, Tone.Red),
                Factor("Performans", "%91,3", "· kayıp yavaş çalışma", 91.3, Tone.Amber),
                Factor("Kalite", "%95,2", "· kayıp 64 adet hurda", 95.2, Tone.Blue));
        }

        // ---- Isı haritası ----

        private TwPanel Heatmap()
        {
            string[] lines = { "Hat 1 · Presleme", "Hat 2 · Kaynak", "Hat 3 · Montaj", "Hat 4 · Boya", "Hat 5 · Paketleme" };
            var random = new Random(16);
            var values = new double[lines.Length, 42];
            for (int r = 0; r < lines.Length; r++)
                for (int c = 0; c < 42; c++)
                {
                    double v = random.NextDouble();
                    values[r, c] = Math.Round(v < 0.3 ? 0 : v * 70);
                }
            // Tasarımdaki parlak hücreler: Hat 2'nin son günleri ve Hat 4'ün iki yoğun vardiyası.
            foreach (var c in new[] { 28, 31, 34, 37, 40 }) values[1, c] = 130;
            values[3, 14] = 118;
            values[3, 17] = 110;
            var columns = Enumerable.Range(0, 42).Select(i => i % 3 == 1 ? (3 + i / 3).ToString("00") : "").ToArray();
            var heat = Ui.Heat(new TwHeatmap { Maximum = 120, ValueFormat = "0 'dk'", AccessibleName = "Duruş ısı haritası", HighColor = Ui.ColorOf(Tone.Amber) }, "flex-1");
            heat.SetData(lines, columns, values);
            var legend = Ui.Gradient("0", "120+ dk", "bg-gradient-to-r from-surface-200 to-warning-500 dark:from-surface-950 dark:to-warning-500");
            return Ui.Card("Duruş ısı haritası · gün × vardiya", "Son 14 gün · her gün A · B · C vardiyası · vardiya başına duruş dakikası", legend, "h-[262px] px-5", heat);
        }

        // ---- Veri kalitesi ve devir teslim ----

        private TwPanel DataQuality()
        {
            var items = new (string Title, string Detail, Tone Tone)[]
            {
                ("Hat 4 · 3 kayıtta kök neden boş", "DR-24815, DR-24813, DR-24811", Tone.Red),
                ("Hat 2 · \"Diğer\" kategorisi %18", "Eşik %15 · 2 kayıt yeniden sınıflandırılmalı", Tone.Amber),
                ("Hat 5 · zaman aralığı çakışması", "DR-24812 ile DR-24809 aynı dakikalarda", Tone.Amber),
                ("Hat 3 · kısa açıklama", "2 kayıtta açıklama 10 karakterden kısa", Tone.Gray),
            };
            var list = Ui.Col("gap-2");
            foreach (var i in items)
                list.Controls.Add(Ui.Row("tile items-start gap-3 h-[60px] py-3", Ui.Box("pt-1.5", Ui.Dot(i.Tone)), Ui.Col("", Ui.Text(i.Title, "strong"), Ui.Text(i.Detail, "small")), Ui.Spacer(), Ui.Button("Düzelt")));
            return Ui.Card("Veri kalitesi uyarıları", "Kapanıştan önce düzeltilmesi önerilir", Ui.Chip("4", Tone.Amber), "basis-2/5 px-5", list);
        }

        private TwPanel Handover()
        {
            var from = Ui.Col("tile gap-0 py-0",
                Ui.Row("h-[44px]", Ui.Paragraph("PR-03 hidrolik yağ seviyesi düşük, Hat 1 başlangıcında kontrol edilecek.", 20, "body flex-1"), Ui.Chip("Kapatıldı", Tone.Green)));
            var notes = Ui.Col("gap-0");
            foreach (var (text, owner) in new[]
                     {
                         ("KR-204 rulman değişti; ilk 2 saat redüktör sıcaklığı elle kontrol edilsin.", "Bakım · Hat 2"),
                         ("BY-102 boya partisi P-2291 bitti, yeni parti 02:00'de gelecek.", "Lojistik · Hat 4"),
                         ("PK-05 fotosel geçici temizlendi; kalıcı çözüm CAPA-0871 kapsamında.", "Bakım · Hat 5"),
                     })
            {
                var check = Ui.Style(new TwCheckBox { AutoSize = true, Text = "", AccessibleName = text }, "toggle");
                notes.Controls.Add(Ui.Row("items-start gap-3 h-[54px] pt-2.5 flex-none border-b border-surface-200 dark:border-surface-700",
                    check, Ui.Col("", Ui.Text(text, "body"), Ui.Text(owner, "small"))));
            }
            var add = Ui.Row("gap-2", Ui.Input("Not ekle: makine, durum, sonraki vardiyadan beklenen", "", "flex-1"), Ui.Button("Not ekle", "btn-sm text-sm py-1.5"));
            var sign = Ui.Row("gap-1 pt-1 border-t border-surface-200 dark:border-surface-700 h-[48px] flex-none",
                Ui.Text("Devreden:", "small"), Ui.Text("[AMİR ADI] · B", "strong text-xs"), Ui.Text("  ·  Devralan:", "small"), Ui.Text("[AMİR ADI] · C", "strong text-xs"),
                Ui.Spacer(), Ui.Primary("Devret ve imzala"));
            return Ui.Card("Vardiya devir teslim", "Vardiya B → Vardiya C · 00:00", Ui.Chip("Devralan imzası bekliyor", Tone.Blue), "basis-3/5 px-5",
                Ui.Text("Vardiya A'dan devralınan", "small"), from, Ui.Text("Sonraki vardiyaya notlar", "small"), notes, add, sign);
        }
    }
}
