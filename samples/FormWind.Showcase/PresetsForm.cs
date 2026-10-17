using System;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind.Showcase
{
    /// <summary>
    /// Hazır görünümler ve AutoStyle: kontrollerin çoğunun class'ı yok, temanın bileşenleriyle stillenir. Üstteki liste paketi
    /// (Tailwind, Fluent, Ant Design, Material) değiştirir; form ve açık diğer formlar yeni temayla yeniden çizilir.
    /// Kod ile kurulur (<c>--presets</c>); görsel regresyon testleri her paket için çizer.
    /// </summary>
    public sealed class PresetsForm : Form
    {
        private static readonly (string Text, TwPreset Preset)[] Presets =
        {
            ("Tailwind", TwPreset.Tailwind), ("Fluent", TwPreset.Fluent), ("Ant Design", TwPreset.Antd), ("Material", TwPreset.Material),
        };

        private readonly TailwindProvider _tw;
        private readonly TwSwitch _dark;

        public PresetsForm()
        {
            _tw = new TailwindProvider();
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "FormWind hazır görünümler";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(820, 560);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var menu = new MenuStrip { Items = { "Dosya", "Düzen", "Görünüm", "Yardım" } };

            var title = new Label { AutoSize = true, Text = "Hazır görünümler", Location = new Point(24, 40) };
            var subtitle = new Label { AutoSize = true, Text = "Class'sız kontroller AutoStyle ile temanın bileşenlerini alır", Location = new Point(26, 78) };

            var preset = new TwSelect { Location = new Point(520, 42), Size = new Size(150, 34), AccessibleName = "Görünüm" };
            foreach (var item in Presets) preset.Items.Add(item.Text);
            preset.SelectedIndex = Math.Max(0, Array.FindIndex(Presets, p => p.Preset == TwTheme.Current.Preset));
            preset.SelectedIndexChanged += (_, _) => TwTheme.Current = new TwTheme(Presets[preset.SelectedIndex].Preset);
            _dark = new TwSwitch { AutoSize = true, Text = "Koyu", Location = new Point(690, 48) };
            _dark.CheckedChanged += (_, _) => _tw.DarkMode = _dark.Checked;

            // Sol: müşteri kartı (flex). Yalnızca kart, başlık, uyarı ve vurgulu butonlar class alır; yerleşim class'ları panellerde.
            var card = new TwPanel { Location = new Point(24, 112), Size = new Size(360, 420) };
            var cardTitle = new Label { AutoSize = true, Text = "Müşteri" };
            var nameField = new TwField { LabelText = "Ad soyad" };
            nameField.Controls.Add(new TwTextBox { Text = "Ayşe Yılmaz" });
            var row = new TwPanel { Height = 70 };
            var cityField = new TwField { LabelText = "Şehir" };
            var city = new TwSelect { AccessibleName = "Şehir" };
            city.Items.AddRange(new[] { "İstanbul", "Ankara", "İzmir" });
            city.SelectedIndex = 0;
            cityField.Controls.Add(city);
            var dateField = new TwField { LabelText = "Kayıt tarihi" };
            dateField.Controls.Add(new TwDatePicker { Culture = new System.Globalization.CultureInfo("tr-TR"), Value = new DateTime(2026, 10, 15) });
            row.Controls.AddRange(new Control[] { cityField, dateField });
            var toggles = new TwPanel { Height = 28 };
            toggles.Controls.Add(new TwCheckBox { AutoSize = true, Text = "Aktif müşteri", Checked = true });
            toggles.Controls.Add(new TwSwitch { AutoSize = true, Text = "Bülten" });
            var alert = new Label { AutoSize = false, Text = "Değişiklikler kaydedildi.", Height = 44 };
            var spacer = new TwPanel();
            var buttons = new TwPanel { Height = 36 };
            var save = new TwButton { Text = "Kaydet", Height = 36 };
            var cancel = new TwButton { Text = "İptal", Height = 36 };
            var delete = new TwButton { Text = "Sil", Height = 36 };
            buttons.Controls.AddRange(new Control[] { save, cancel, delete });
            card.Controls.AddRange(new Control[] { cardTitle, nameField, row, toggles, alert, spacer, buttons });

            // Sağ: sekmeler, tablo ve ilerleme; hepsi otomatik.
            var tabs = new TwTabStrip { AutoSize = true, Location = new Point(408, 112) };
            tabs.Tabs.AddRange(new[] { "Siparişler", "Adresler", "Notlar" });
            var grid = new DataGridView
            {
                Location = new Point(408, 160), Size = new Size(388, 224), AllowUserToAddRows = false, ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            };
            grid.Columns.Add("no", "NO");
            grid.Columns.Add("date", "TARİH");
            grid.Columns.Add("amount", "TUTAR");
            grid.Rows.Add("#10234", "08.10.2026", "1.250,00 ₺");
            grid.Rows.Add("#10233", "07.10.2026", "480,50 ₺");
            grid.Rows.Add("#10232", "05.10.2026", "2.940,00 ₺");
            var progressLabel = new Label { AutoSize = true, Text = "Teslimat", Location = new Point(408, 402) };
            var progress = new TwProgress { Value = 65, Location = new Point(408, 426), Size = new Size(388, 8), AccessibleName = "Teslimat" };
            var badge = new Label { AutoSize = true, Text = "3 açık sipariş", Location = new Point(408, 452) };
            var link = new Label { AutoSize = true, Text = "Tüm siparişler", Location = new Point(408, 490) };

            Controls.AddRange(new Control[] { title, subtitle, preset, _dark, card, tabs, grid, progressLabel, progress, badge, link, menu });
            MainMenuStrip = menu;

            _tw.SetClasses(title, "title");
            _tw.SetClasses(subtitle, "muted");
            _tw.SetClasses(card, "card flex flex-col gap-3");
            _tw.SetClasses(cardTitle, "text-lg font-semibold");
            _tw.SetClasses(row, "flex gap-4");
            _tw.SetClasses(cityField, "flex-1");
            _tw.SetClasses(dateField, "flex-1");
            _tw.SetClasses(toggles, "flex items-center gap-6");
            _tw.SetClasses(spacer, "flex-1");
            _tw.SetClasses(buttons, "flex gap-2");
            _tw.SetClasses(cancel, "flex-1");                       // yerleşim class'ı: otomatik btn-ghost'un üzerine eklenir
            _tw.SetClasses(alert, "alert-success");
            _tw.SetClasses(save, "btn-primary flex-1");
            _tw.SetClasses(delete, "btn-danger flex-1");
            _tw.SetClasses(badge, "badge");
            _tw.SetClasses(link, "link");
            _tw.ContainerControl = this;
            _tw.AutoStyle = true;
            ResumeLayout(true);
        }

        public bool DarkMode
        {
            get => _dark.Checked;
            set => _dark.Checked = value;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tw.Dispose();
            base.Dispose(disposing);
        }
    }
}
