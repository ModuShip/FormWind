using System.Windows.Forms;
using FormWind;

namespace DurusMerkezi
{
    /// <summary>Duruş Kayıtları & Kök Neden: ERP'den düşen ham duruşların listesi ve seçili kaydın 5 Neden analizi,
    /// sınıflandırması, parça-maliyeti ve yorumları.</summary>
    internal sealed class DowntimeRecordsPage : Page
    {
        public DowntimeRecordsPage(Ui ui) : base(ui)
        {
        }

        public override string Title => "Duruş Kayıtları";
        public override string Subtitle => "ERP kaynaklı · vardiya sonu girişleri";
        public override string SideTitle => "Akıllı listeler";

        public override SideItem[] SideItems => new[]
        {
            new SideItem("Kök neden eksik", Tone.Red, "5"),
            new SideItem("30 dk üzeri", Tone.Amber, "3"),
            new SideItem("Kronik aday", Tone.Violet, "2"),
            new SideItem("Bana atananlar", Tone.Blue, "4"),
        };

        protected override Control[] BuildToolbar() => new Control[]
        {
            Ui.Segments("Tümü 10", "Zenginleştirilmemiş 5", "İncelemede 2", "Kapatıldı 3"),
            Ui.Search("DR no, makine, ERP kodu", "w-64"),
            Ui.Plain("Toplu giriş"),
        };

        public override Control[] SecondaryToolbar => _secondary ??= new Control[]
        {
            Ui.Plain("ERP'den çek", "refresh"), Ui.IconButton("columns", "Ayrıntı panelini göster", "btn-icon"),
        };

        private Control[] _secondary;

        protected override TwPanel BuildContent() => PageBody(
            "Duruş Kayıtları & Kök Neden",
            "ERP'den düşen ham duruşları zenginleştirin: neden, sınıf, parça, maliyet ve kanıt.",
            new Control[] { Ui.Chip("5  kayıt kök neden bekliyor", Tone.Red), Ui.Chip("Ort. giriş süresi 6 dk", Tone.Gray) },
            Ui.Row("items-start gap-4", RecordList(), Detail()));

        // ---- Kayıt listesi ----

        private TwPanel RecordList()
        {
            var rows = Ui.Col("gap-1.5 px-1.5 pb-1.5");
            var records = new (string Machine, string Record, string Duration, string State, Tone Tone)[]
            {
                ("KR-204 Robot Kaynak Hücresi", "DR-24817 · Hat 2", "47 dk", "Ham", Tone.Red),
                ("BY-102 Fırın Konveyörü", "DR-24815 · Hat 4", "8 dk", "Ham", Tone.Red),
                ("PK-05 Streç Sarma", "DR-24812 · Hat 5", "12 dk", "Taslak", Tone.Amber),
                ("PR-03 Hidrolik Pres", "DR-24806 · Hat 1", "26 dk", "İncelemede", Tone.Blue),
                ("KR-204 Robot Kaynak Hücresi", "DR-24799 · Hat 2", "33 dk", "Ham", Tone.Red),
                ("MN-11 Vidalama İstasyonu", "DR-24790 · Hat 3", "15 dk", "Kapatıldı", Tone.Green),
                ("KR-207 Punta Kaynak", "DR-24781 · Hat 2", "19 dk", "İncelemede", Tone.Blue),
                ("BY-104 Robot Boya", "DR-24770 · Hat 4", "41 dk", "Kapatıldı", Tone.Green),
                ("PR-01 Transfer Pres", "DR-24768 · Hat 1", "22 dk", "Ham", Tone.Red),
                ("PK-02 Koli Kapatma", "DR-24761 · Hat 5", "9 dk", "Kapatıldı", Tone.Green),
            };
            for (int i = 0; i < records.Length; i++)
            {
                var r = records[i];
                bool selected = i == 0;
                // Seçili satır mavi zeminde, yazıları beyaz; rozeti de beyazın üstünde okunur kalır.
                var name = Ui.Text(r.Machine, selected ? "text-sm font-semibold text-white dark:text-white" : "strong");
                var code = Ui.Text(r.Record, selected ? "font-mono text-xs text-primary-100 dark:text-primary-100" : "mono");
                var duration = Ui.Text(r.Duration, (selected ? "text-white dark:text-white" : "text-surface-900 dark:text-surface-50") + " font-mono font-bold text-xs w-12 text-right");
                var chip = selected ? Ui.Text(r.State, "chip py-0.5 flex-none bg-primary-700 text-danger-200 dark:bg-primary-700 dark:text-danger-200") : Ui.Chip(r.State, r.Tone);
                var tile = Ui.Row("gap-3 h-[54px] px-3 rounded-lg " + (selected ? "bg-primary-600 dark:bg-primary-600" : "bg-surface-50 dark:bg-[#2e2e31]"),
                    Ui.Dot(r.Tone), Ui.Col("gap-0.5 flex-1", name, code), duration, Ui.Box("w-[84px] flex justify-end", chip));
                tile.AccessibleName = r.Machine + ", " + r.Record + ", " + r.Duration + ", " + r.State;
                rows.Controls.Add(tile);
            }
            var head = Ui.Row("gap-2 px-4 h-[46px] flex-none border-b border-surface-200 dark:border-surface-700",
                Ui.Text("10 kayıt · ↑↓ ile gezin · sağ tık menüsü", "small"), Ui.Spacer(), Ui.Text("Hatlara göre grupla", "small"), Ui.Switch(false, "Hatlara göre grupla"));
            var columns = Ui.Row("gap-3 px-4 h-[34px] flex-none", Ui.Text("Makine", "small flex-1"), Ui.Text("Süre", "small w-12 text-right"), Ui.Text("Durum", "small w-[84px] text-right"));
            return Ui.Box("card flex flex-col gap-0 p-0 basis-[400px]", head, columns, rows);
        }

        // ---- Seçili kayıt ----

        private TwPanel Detail()
        {
            var top = Ui.Row("gap-2", Ui.Text("DR-24817 · ERP sipariş WO-55120", "mono"), Ui.Spacer(), Ui.Chip("Ham", Tone.Red), Ui.Chip("Kronik aday · 30 günde 6.", Tone.Violet));
            var title = Ui.Col("gap-0.5", Ui.Text("KR-204 Robot Kaynak Hücresi", "text-xl font-bold text-surface-900 dark:text-white"), Ui.Text("Hat 2 · İstasyon 4", "muted"));

            TwPanel Fact(string label, string value, string valueClasses = "strong") => Ui.Col("gap-1 flex-1", Ui.Text(label, "small"), Ui.Text(value, valueClasses));
            var facts = Ui.Row("items-start gap-4", Fact("Başlangıç", "16 Eki 18:42"), Fact("Süre", "47 dk"), Fact("ERP sebep kodu", "ERP M-110", "font-mono text-xs font-bold text-surface-900 dark:text-white"), Fact("Vardiya", "B · [AMİR ADI]"));
            var loss = Ui.Row("", Fact("Üretim kaybı", "~1.120 adet"));

            var flow = new TwStepper { Variant = TwStepperVariant.Horizontal, AccessibleName = "Onay akışı" };
            flow.Steps.Add(new TwStep("Operatör girdi", TwStepStatus.Completed, "18:55"));
            flow.Steps.Add(new TwStep("Amir onayı bekliyor", TwStepStatus.Current));
            flow.Steps.Add(new TwStep("Bakım şefi kapanışı"));
            Ui.Style(flow, "chart text-xs h-7 flex-1 accent-primary-500");
            var approval = Ui.Row("gap-3", Ui.Text("Onay akışı", "small"), flow);
            var header = Ui.Col("gap-4 px-5 pt-5 pb-5", top, title, facts, loss, approval);

            var tabs = Ui.Row("gap-2", Ui.Segments("5 Neden", "Balık Kılçığı", "Geçmiş & Benzer", "Kanıtlar"), Ui.Spacer(), Ui.Text("Otomatik kaydedildi · 18:51", "small"));

            var whys = Ui.Col("gap-3");
            var answers = new (string Question, string Answer)[]
            {
                ("Neden durdu?", "Robot torç hareketi Eksen-3 üzerinde kilitlendi"),
                ("Neden kilitlendi?", "Eksen-3 servo sürücüsü aşırı akım alarmı verdi"),
                ("Neden aşırı akım?", "Redüktör çıkış yatağında sürtünme ve ısınma arttı"),
                ("Neden sürtünme arttı?", "Yağlama periyodu aşıldı: plan 500 sa, gerçekleşen 740 sa"),
                ("Neden periyot aşıldı?", "CMMS yağlama iş emrini çalışma saatine göre açmıyor"),
            };
            for (int i = 0; i < answers.Length; i++)
            {
                bool last = i == answers.Length - 1;
                var number = Ui.Avatar((i + 1).ToString(), last ? "avatar" : "avatar-ring", last ? "bg-danger-600 text-white dark:bg-danger-600 dark:text-white" : "");
                whys.Controls.Add(Ui.Row("items-start gap-3", Ui.Box("pt-3", number),
                    Ui.Col("gap-1.5 flex-1", Ui.Text(answers[i].Question, "small"), Ui.Input(answers[i].Question, answers[i].Answer, "h-9"))));
            }
            var root = Ui.Row("alert-root gap-3", Ui.Chip("Kök neden", Tone.Red),
                Ui.Paragraph("Önleyici bakım planı yağlama iş emrini çalışma saatine göre tetiklemiyor (takvim bazlı).", 40, "body flex-1"), Ui.Button("Öneri: Sistemik", "btn-sm text-sm py-1.5"));

            TwPanel Field(string label, Control input, bool last = false) => Ui.Row("h-[46px] px-3.5 flex-none" + (last ? "" : " border-b border-surface-200 dark:border-surface-800"),
                Ui.Text(label, "body"), Ui.Spacer(), input);
            var classify = Ui.Col("tile gap-0 px-0 py-0",
                Field("Arıza kategorisi", Ui.Select(new[] { "Mekanik", "Elektrik / sensör", "Malzeme", "Operasyonel" }, 0, "w-[272px]")),
                Field("Alt kategori", Ui.Select(new[] { "Yatak / rulman", "Redüktör", "Kayış" }, 0, "w-[272px]")),
                Field("Bileşen", Ui.Select(new[] { "Eksen-3 redüktörü", "Eksen-2 redüktörü", "Torç" }, 0, "w-[272px]")),
                Field("Duruş tipi", Ui.Select(new[] { "Plansız · arıza", "Planlı · bakım" }, 0, "w-[272px]")),
                Field("Kronik incelemeye öner", Ui.Switch(true, "Kronik incelemeye öner"), last: true));

            var parts = Ui.Grid("Parça ve maliyet", 42);
            parts.Columns.AddRange(
                new DataGridViewTextBoxColumn { Name = "part", HeaderText = "Parça" }, new DataGridViewTextBoxColumn { Name = "code", HeaderText = "Stok kodu" },
                new DataGridViewTextBoxColumn { Name = "qty", HeaderText = "Adet" }, new DataGridViewTextBoxColumn { Name = "unit", HeaderText = "Birim" },
                new DataGridViewTextBoxColumn { Name = "total", HeaderText = "Toplam" });
            parts.Rows.Add("Rulman 6205-2RS", "STK-10442", "2", "₺ 640", "₺ 1.280");
            parts.Rows.Add("Redüktör yağ keçesi", "STK-20871", "1", "₺ 280", "₺ 280");
            parts.Rows.Add("Gres EP2 (400 g)", "STK-31007", "1", "₺ 400", "₺ 400");
            Ui.Tw.SetColumnClasses(parts.Columns[0], "flex-1");
            Ui.Tw.SetColumnClasses(parts.Columns[1], "w-36 font-mono text-xs font-bold");
            Ui.Tw.SetColumnClasses(parts.Columns[2], "w-20");
            Ui.Tw.SetColumnClasses(parts.Columns[3], "w-28");
            Ui.Tw.SetColumnClasses(parts.Columns[4], "w-28 font-semibold");
            Ui.Style(parts, "table-plain row:h-[42px] h-[162px]");
            var totals = Ui.Row("gap-6", Ui.Spacer(), Ui.Text("İşçilik 1,5 sa · ₺ 1.350", "muted"), Ui.Text("Üretim kaybı · ₺ 18.400", "muted"), Ui.Text("Toplam ₺ 21.710", "strong"));

            TwPanel Comment(string initials, string who, string time, string text) => Ui.Row("items-start gap-3", Ui.Avatar(initials, "avatar-ring"),
                Ui.Col("bubble gap-1 flex-1", Ui.Row("gap-2", Ui.Text(who, "strong text-xs"), Ui.Text(time, "small")), Ui.Text(text, "body")));
            var comments = Ui.Col("gap-2.5",
                Comment("OP", "Operatör · Vardiya B", "18:44", "Torç sağa dönerken takıldı, öncesinde 10 dk kadar tıkırtı sesi vardı."),
                Comment("BK", "Bakım teknisyeni", "19:20", "Eksen-3 redüktöründe radyal boşluk ve ısınma. Rulman ve keçe değiştirildi, gres yenilendi."),
                Ui.Row("gap-2", Ui.Input("Yorum ekle, @bakım ile etiketle", "", "flex-1 h-9"), Ui.Button("Gönder", "btn-sm text-sm py-1.5")));

            var body = Ui.Col("gap-4 px-5 pt-5 pb-5",
                tabs, whys, root, Ui.Divider(),
                Ui.Text("Duruş sınıflandırma", "muted font-semibold"), classify,
                Ui.Row("", Ui.Text("Parça & maliyet", "muted font-semibold"), Ui.Spacer(), Ui.Button("Parça ekle", "btn-sm text-sm py-1.5")), parts, totals,
                Ui.Text("Operatör & bakımcı yorumları", "muted font-semibold"), comments);
            var actions = Ui.Row("gap-2 px-5 h-[60px] flex-none bg-surface-50 dark:bg-[#242426] border-t border-surface-200 dark:border-surface-700",
                Ui.Spacer(), Ui.Button("Taslak kaydet", "btn-sm text-sm py-1.5"), Ui.Button("Kronik incelemeye gönder", "btn-sm text-sm py-1.5"), Ui.Primary("Amir onayına gönder"));

            return Ui.Box("card flex flex-col gap-0 p-0 flex-1", header, Ui.Divider(), body, actions);
        }
    }
}
