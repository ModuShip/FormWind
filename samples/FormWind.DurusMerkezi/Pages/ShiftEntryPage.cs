using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind;

namespace DurusMerkezi
{
    /// <summary>Vardiya Girişi: ERP'den düşen duruşların hat bazında toplu zenginleştirilmesi; vardiya özeti, tamamlanma,
    /// giriş kutulu duruş tablosu, gönderim öncesi kontrol ve onay akışı.</summary>
    internal sealed class ShiftEntryPage : Page
    {
        private static readonly string[] Categories = { "Seçin…", "Mekanik", "Elektrik / sensör", "Kalıp / ayar", "Malzeme", "Operasyonel", "Diğer" };

        public ShiftEntryPage(Ui ui) : base(ui)
        {
        }

        public override string Title => "Vardiya Girişi";
        public override string Subtitle => "Toplu duruş zenginleştirme · 16 Eki · Vardiya B";
        public override string SideTitle => "Bu vardiyanın hatları";

        public override SideItem[] SideItems => new[]
        {
            new SideItem("Hat 1 · Presleme", Tone.Green, "6/6"),
            new SideItem("Hat 2 · Kaynak", Tone.Blue, "9/9"),
            new SideItem("Hat 3 · Montaj", Tone.Green, "4/4"),
            new SideItem("Hat 4 · Boya", Tone.Amber, "5/8"),
            new SideItem("Hat 5 · Paketleme", Tone.Red, "0/7"),
        };

        public override string Footer => "Giriş son saati 00:40";
        public override Tone FooterTone => Tone.Amber;

        protected override Control[] BuildToolbar()
        {
            var lines = Ui.Segments("Hat 1", "Hat 2", "Hat 3", "Hat 4", "Hat 5");
            lines.SelectedIndex = 1;
            return new Control[]
            {
                lines, Ui.Plain("ERP kayıtlarını yenile", "refresh"), Ui.Plain("Taslak kaydet"), Ui.Primary("Amir onayına gönder"), Ui.IconButton("search", "Ara"),
            };
        }

        protected override TwPanel BuildContent() => PageBody(
            "Vardiya Girişi · Hat 2 · Kaynak",
            "ERP'den düşen duruşları tek tabloda tamamlayın. Zorunlu alanlar: kategori ve kök neden.",
            new Control[] { Ui.Chip("6 / 9 kayıt hazır", Tone.Amber), Ui.Chip("Duruş toplamı  170  / 480 dk", Tone.Gray) },
            Ui.Row("items-stretch gap-4 h-[384px]", Summary(), Completion()),
            Records(),
            Ui.Row("items-stretch gap-4 h-[284px]", Checks(), Approval()));

        private TwPanel Summary()
        {
            TwPanel Number(string label, decimal value, bool last)
            {
                var box = new TwNumberBox { Maximum = 100000, Value = value, AccessibleName = label };
                Ui.Style(box, "input px-2 py-1 text-sm h-7 w-32 text-right dark:bg-surface-950 dark:border-surface-950");
                return Ui.Row("h-[46px] px-3.5 flex-none" + (last ? "" : " border-b border-surface-200 dark:border-surface-800"), Ui.Text(label, "body"), Ui.Spacer(), box);
            }
            TwPanel Toggle(string label, bool on, bool last) => Ui.Row("h-[46px] px-3.5 flex-none" + (last ? "" : " border-b border-surface-200 dark:border-surface-800"),
                Ui.Text(label, "body"), Ui.Spacer(), Ui.Switch(on, label));

            var numbers = Ui.Col("tile gap-0 px-0 py-0", Number("Üretim adedi", 1212, false), Number("Hurda adedi", 18, false), Number("Planlı duruş (dk)", 30, false), Number("Operatör sayısı", 6, true));
            var toggles = Ui.Col("tile gap-0 px-0 py-0", Toggle("Gönderimden sonra vardiyayı kilitle", true, false), Toggle("Eksik kayıtlarla gönderime izin ver", false, true));
            return Ui.Card("Vardiya özeti", "Operatörün vardiya sonunda girdiği değerler", null, "basis-3/4 px-5", numbers, toggles);
        }

        private TwPanel Completion()
        {
            var ring = new TwRingChart { CenterText = "6/9", CenterLabel = "hazır", Thickness = 0.1f, AccessibleName = "Tamamlanma" };
            ring.Rings.Add(new TwChartRing("Hazır kayıt", 6, 9) { Color = Ui.ColorOf(Tone.Blue) });
            Ui.Style(ring, "chart h-[160px] mt-4");
            return Ui.Card("Tamamlanma", "Zorunlu alanlar", null, "basis-1/4 px-5", ring, Ui.Text("Duruş toplamı 170 / 480 dk", "muted text-center"));
        }

        // ---- Duruş tablosu ----

        private TwPanel Records()
        {
            var grid = Ui.Grid("Vardiya duruşları", 57);
            grid.ReadOnly = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.EditMode = DataGridViewEditMode.EditOnEnter;
            var no = new DataGridViewTextBoxColumn { Name = "no", HeaderText = "DR no", ReadOnly = true };
            var machine = new TwoLineColumn { Name = "machine", HeaderText = "Makine", ReadOnly = true };
            var time = new DataGridViewTextBoxColumn { Name = "time", HeaderText = "Başlangıç · süre", ReadOnly = true };
            var erp = new DataGridViewTextBoxColumn { Name = "erp", HeaderText = "ERP kodu", ReadOnly = true };
            var category = new BoxComboColumn { Name = "category", HeaderText = "Kategori *", Placeholder = Categories[0], Required = true };
            category.Items.AddRange(Categories);
            var sub = new BoxTextColumn { Name = "sub", HeaderText = "Alt kategori", Placeholder = "—" };
            var cause = new BoxTextColumn { Name = "cause", HeaderText = "Kök neden *", Placeholder = "Kısa kök neden", Required = true };
            var part = new BoxTextColumn { Name = "part", HeaderText = "Parça", Placeholder = "Stok kodu" };
            grid.Columns.AddRange(no, machine, time, erp, category, sub, cause, part);
            foreach (DataGridViewColumn c in grid.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;

            var rows = new[]
            {
                ("DR-24817", "KR-204 Robot Kaynak\nİstasyon 4", "18:42 · 47 dk", "ERP M-110", "Mekanik", "Yatak / rulman", "Yağlama periyodu aşıldı, rulman", "STK-10442"),
                ("DR-24814", "KR-204 Robot Kaynak\nİstasyon 4", "17:20 · 12 dk", "ERP M-110", "Seçin…", "", "", ""),
                ("DR-24810", "KR-207 Punta Kaynak\nİstasyon 7", "16:58 · 19 dk", "ERP M-140", "Mekanik", "Elektrot", "Elektrot ucu aşınması", "STK-40021"),
                ("DR-24808", "KR-201 Robot Kaynak\nİstasyon 1", "16:41 · 8 dk", "ERP E-031", "Elektrik / sensör", "Fotosel", "", ""),
                ("DR-24805", "FK-02 Fikstür Ünitesi\nİstasyon 2", "16:30 · 22 dk", "ERP K-012", "Kalıp / ayar", "Fikstür ayarı", "Model değişiminde pim ayarı", ""),
                ("DR-24803", "KR-204 Robot Kaynak\nİstasyon 4", "16:22 · 9 dk", "ERP M-110", "Diğer", "", "Sebep belirsiz", ""),
                ("DR-24801", "TK-01 Tel Besleme\nİstasyon 3", "16:15 · 14 dk", "ERP L-020", "Malzeme", "Tel makarası", "Makara bitti, yedek hazır değildi", ""),
                ("DR-24799", "KR-204 Robot Kaynak\nİstasyon 4", "16:05 · 33 dk", "ERP M-110", "Seçin…", "", "", ""),
                ("DR-24796", "KR-209 Robot Kaynak\nİstasyon 9", "16:02 · 6 dk", "ERP O-004", "Operasyonel", "Vardiya devri", "Devir sırasında hücre boş", ""),
            };
            foreach (var r in rows) grid.Rows.Add(r.Item1, r.Item2, r.Item3, r.Item4, r.Item5, r.Item6, r.Item7, r.Item8);

            // Zorunlu alanı eksik satırlar hafif kırmızı tonla işaretlenir (kutunun kenarı da kırmızıdır).
            grid.CellFormatting += (_, e) =>
            {
                if (e.RowIndex < 0) return;
                var row = grid.Rows[e.RowIndex];
                bool missing = row.Cells[category.Index].Value as string == Categories[0] || string.IsNullOrEmpty(row.Cells[cause.Index].Value as string);
                if (missing) e.CellStyle.BackColor = Ui.Tw.DarkMode ? Color.FromArgb(48, 34, 35) : Color.FromArgb(254, 242, 242);
            };

            Ui.Tw.SetColumnClasses(no, "w-28 font-mono font-bold text-xs");
            Ui.Tw.SetColumnClasses(machine, "w-44");
            Ui.Tw.SetColumnClasses(time, "w-36 font-mono font-bold text-xs");
            Ui.Tw.SetColumnClasses(erp, "w-24 font-mono text-xs text-surface-500 dark:text-surface-400");
            Ui.Tw.SetColumnClasses(category, "w-44");
            Ui.Tw.SetColumnClasses(sub, "w-32");
            Ui.Tw.SetColumnClasses(cause, "flex-1 min-w-48");
            Ui.Tw.SetColumnClasses(part, "w-32");
            Ui.Style(grid, "table-plain row:h-[57px] h-[548px]");

            var filter = Ui.Segments("Tümü 9", "Eksik 3", "Hazır 6");
            var head = Ui.Row("gap-2 px-5 pt-4", filter, Ui.Spacer(), Ui.Button("Seçilenlere kategori uygula", "btn-sm text-sm py-1.5"), Ui.Button("Üst satırı kopyala", "btn-sm text-sm py-1.5"));
            var keys = Ui.Row("gap-1.5 px-5 h-[44px] flex-none bg-surface-50 dark:bg-surface-800 border-t border-surface-200 dark:border-surface-700",
                Ui.Kbd("Tab"), Ui.Text("sonraki alan", "small mr-3"), Ui.Kbd("↑"), Ui.Kbd("↓"), Ui.Text("satır", "small mr-3"),
                Ui.Kbd("Ctrl"), Ui.Kbd("D"), Ui.Text("üst satırı kopyala", "small mr-3"), Ui.Kbd("Ctrl"), Ui.Kbd("↵"), Ui.Text("kaydı hazır işaretle", "small"),
                Ui.Spacer(), Ui.Text("Ayrıntılı RCA için kayıt ekranına git", "link text-xs"));
            return Ui.Box("card flex flex-col gap-3 p-0", head, grid, keys);
        }

        // ---- Kontrol ve onay ----

        private TwPanel Checks()
        {
            var items = new (string Text, string Value, Tone Tone)[]
            {
                ("Tüm kayıtlarda kategori ve kök neden", "3 kayıt eksik", Tone.Red),
                ("Duruş toplamı vardiya süresini aşmıyor", "170 / 480 dk", Tone.Green),
                ("\"Diğer\" kategorisi %15 altında", "1 kayıt", Tone.Green),
                ("Zaman aralıklarında çakışma yok", "Tamam", Tone.Green),
            };
            var list = Ui.Col("gap-0");
            foreach (var i in items)
                list.Controls.Add(Ui.TableRow(50, Ui.Dot(i.Tone), Ui.Text(i.Text, "body"), Ui.Spacer(), Ui.Text(i.Value, "small")));
            return Ui.Card("Gönderim öncesi kontrol", "Onaya göndermeden önce", null, "basis-1/2 px-5", list);
        }

        private TwPanel Approval()
        {
            var stepper = new TwStepper { AccessibleName = "Onay akışı" };
            stepper.Steps.Add(new TwStep("Operatör girişi", description: "[AD SOYAD] · taslak 00:05") { Badge = "Devam ediyor" });
            stepper.Steps.Add(new TwStep("Vardiya amiri onayı", description: "[AMİR ADI] · B vardiyası") { Badge = "Bekliyor" });
            stepper.Steps.Add(new TwStep("Bakım şefi kapanışı", description: "Mekanik ve elektrik kayıtları için") { Badge = "Bekliyor" });
            stepper.SetCurrent(0);
            Ui.Style(stepper, "chart flex-1 accent-primary-500");
            return Ui.Card("Onay akışı", "Gönderildikten sonra vardiya kilitlenir", null, "basis-1/2 px-5", stepper);
        }
    }
}
