using System;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind.Showcase
{
    /// <summary>
    /// Yeni kontrollerin vitrini: sekmeler, sayı kutuları, tarih seçici, toggle'lar, açılır liste ve ipucu.
    /// Kod ile kurulur; görsel regresyon testleri de bu formu çizer.
    /// </summary>
    public sealed class ControlsForm : Form
    {
        private readonly TailwindProvider _tw;

        public ControlsForm()
        {
            _tw = new TailwindProvider();
            SuspendLayout();
            // Boyutlar 96 DPI'a göre yazılır; DPI-aware (PerMonitorV2) çalışmada form da ölçeklenir.
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "FormWind kontroller";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(760, 680);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var root = new TwPanel { Dock = DockStyle.Fill };
            Controls.Add(root);

            var title = new Label { AutoSize = true, Text = "Kontroller" };
            root.Controls.Add(title);

            var tabRow = Row(root, "gap-6");
            var underline = Tabs(tabRow, TwTabVariant.Underline, "Genel", "Adres", "Siparişler");
            var pills = Tabs(tabRow, TwTabVariant.Pills, "Gün", "Hafta", "Ay");
            var segmented = Tabs(tabRow, TwTabVariant.Segmented, "Liste", "Kart", "Takvim");

            var card = new TwPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            root.Controls.Add(card);
            var grid = new TwPanel();
            card.Controls.Add(grid);

            var quantityLabel = Caption(grid, "Adet");
            var quantity = new TwNumberBox { Minimum = 1, Maximum = 99, Value = 3 };
            grid.Controls.Add(quantity);
            var discountLabel = Caption(grid, "İndirim (%)");
            var discount = new TwNumberBox { Minimum = 0, Maximum = 50, DecimalPlaces = 1, Increment = 0.5m, Value = 12.5m };
            grid.Controls.Add(discount);
            DateLabel = Caption(grid, "Teslim tarihi");
            DatePicker = new TwDatePicker { Culture = new System.Globalization.CultureInfo("tr-TR"), Value = new DateTime(2026, 10, 15) };
            grid.Controls.Add(DatePicker);
            var cityLabel = Caption(grid, "Şehir");
            var city = new TwSelect { AutoSize = true, SizeToSelection = false, Searchable = true };
            city.Items.AddRange(new[] { "İstanbul", "Ankara", "İzmir", "Bursa" });
            city.SelectedIndex = 0;
            grid.Controls.Add(city);

            var toggles = Row(card, "gap-6 mt-2");
            var check = new TwCheckBox { AutoSize = true, Text = "Hediye paketi", Checked = true };
            var radioA = new TwRadioButton { AutoSize = true, Text = "Standart", Checked = true };
            var radioB = new TwRadioButton { AutoSize = true, Text = "Hızlı" };
            var notify = new TwSwitch { AutoSize = true, Text = "Bildirim gönder", Checked = true };
            toggles.Controls.AddRange(new Control[] { check, radioA, radioB, notify });

            var levels = Row(card, "gap-4");
            var volumeLabel = Caption(levels, "Ses");
            var volume = new TwSlider { Size = new Size(180, 24), Value = 60, AccessibleName = "Ses" };
            levels.Controls.Add(volume);
            var uploadLabel = Caption(levels, "Yükleme");
            var upload = new TwProgress { Size = new Size(180, 8), Value = 65, AccessibleName = "Yükleme" };
            levels.Controls.Add(upload);

            // Sanal liste: 10.000 sipariş, yalnızca görünen satırlar çizilir.
            var listCard = new TwPanel();
            root.Controls.Add(listCard);
            Orders = new TwDataList { ItemHeight = 52, ItemCount = 10_000, SelectedIndex = 1 };
            Orders.DrawItem += DrawOrder;
            Orders.AccessibleName = "Siparişler";
            Orders.AccessibleItemText = i => $"{Customers[i % Customers.Length]}, sipariş {100000 + i}, {Statuses[i * 7 % Statuses.Length].Text}";
            listCard.Controls.Add(Orders);

            // Ekran okuyucu adları: görünen etiketlerle aynı.
            quantity.AccessibleName = "Adet";
            discount.AccessibleName = "İndirim (%)";
            DatePicker.AccessibleName = "Teslim tarihi";
            city.AccessibleName = "Şehir";

            var tip = new TwToolTip { Provider = _tw };
            tip.SetToolTip(quantity, "1–99 arası");

            _tw.SetClasses(this, "font-sans backdrop-mica bg-gray-50 dark:bg-gray-900 dark:caption:bg-gray-900 dark:caption:text-gray-100");
            _tw.SetClasses(root, "flex flex-col gap-4 p-6 bg-gray-50 dark:bg-gray-900");
            _tw.SetClasses(title, "text-2xl font-semibold text-gray-900 dark:text-white");
            _tw.SetClasses(underline, "text-gray-500 border-b border-gray-200 selected:text-gray-900 selected:border-blue-600 item:hover:text-gray-700 dark:text-gray-400 dark:border-gray-700 dark:selected:text-white dark:selected:border-blue-400");
            _tw.SetClasses(pills, "text-gray-600 selected:bg-blue-600 selected:text-white item:hover:bg-gray-200 dark:text-gray-300 dark:item:hover:bg-gray-700");
            _tw.SetClasses(segmented, "text-gray-600 fill-gray-200 selected:bg-white selected:text-gray-900 dark:text-gray-300 dark:fill-gray-800 dark:selected:bg-gray-600 dark:selected:text-white");
            _tw.SetClasses(card, "card shadow-sm flex flex-col gap-4");
            _tw.SetClasses(listCard, "card p-1 flex-1 flex flex-col");
            _tw.SetClasses(Orders, "flex-1 p-1 bg-white text-gray-900 item:hover:bg-gray-100 selected:bg-blue-50 selected:text-blue-900 row:border-gray-100 fill-gray-300 "
                                   + "dark:bg-gray-800 dark:text-gray-100 dark:item:hover:bg-gray-700 dark:selected:bg-blue-950 dark:selected:text-blue-100 dark:row:border-gray-700 dark:fill-gray-600");
            _tw.SetClasses(grid, "grid grid-cols-4 gap-x-4 gap-y-3 items-center");
            _tw.SetClasses(quantity, "input");
            _tw.SetClasses(discount, "input");
            _tw.SetClasses(DatePicker, "input menu:bg-white menu:border-gray-200 item:hover:bg-gray-100 selected:bg-blue-600 selected:text-white dark:menu:bg-gray-800 dark:menu:border-gray-700 dark:item:hover:bg-gray-700");
            _tw.SetClasses(city, "input h-9 menu:bg-white item:hover:bg-gray-100 dark:menu:bg-gray-800 dark:item:hover:bg-gray-700");
            foreach (var toggle in new Control[] { check, radioA, radioB, notify })
                _tw.SetClasses(toggle, "text-sm text-gray-700 dark:text-gray-200 accent-blue-600 dark:fill-gray-700 dark:border-gray-600");
            _tw.SetClasses(volume, "fill-gray-200 accent-blue-600 dark:fill-gray-700 dark:accent-blue-500 dark:item:bg-gray-100");
            _tw.SetClasses(upload, "fill-gray-200 accent-green-600 dark:fill-gray-700 dark:accent-green-500");
            foreach (var label in new[] { quantityLabel, discountLabel, DateLabel, cityLabel, volumeLabel, uploadLabel })
                _tw.SetClasses(label, "text-xs font-medium text-gray-500 dark:text-gray-400");
            ResumeLayout(true);
        }

        internal TwDatePicker DatePicker { get; }

        internal TwDataList Orders { get; }

        private static readonly string[] Customers = { "Ayşe Yılmaz", "Mehmet Demir", "Zeynep Kaya", "Can Öztürk", "Elif Şahin", "Burak Arslan", "Selin Aydın" };

        private static readonly (string Text, string Back, string Fore, string DarkBack, string DarkFore)[] Statuses =
        {
            ("Hazırlanıyor", "#fef3c7", "#92400e", "#451a03", "#fcd34d"),
            ("Kargoda", "#dbeafe", "#1e40af", "#172554", "#93c5fd"),
            ("Teslim edildi", "#dcfce7", "#166534", "#052e16", "#86efac"),
            ("İptal", "#fee2e2", "#991b1b", "#450a0a", "#fca5a5"),
        };

        // Satır: ikon, müşteri + sipariş no/tutar, sağda durum rozeti.
        private void DrawOrder(object sender, TwDataListDrawEventArgs e)
        {
            var content = e.ContentBounds;
            int icon = content.Height;
            e.DrawIcon(TwTheme.Current.HasIcon("package") ? "package" : "cart", new Rectangle(content.X, content.Y, icon, content.Height));

            var status = Statuses[e.Index * 7 % Statuses.Length];
            bool dark = _tw.DarkMode;
            int badgeLeft = e.DrawBadge(status.Text, content.Right,
                ColorTranslator.FromHtml(dark ? status.DarkBack : status.Back), ColorTranslator.FromHtml(dark ? status.DarkFore : status.Fore));

            int textX = content.X + icon + 4, half = content.Height / 2;
            var textWidth = Math.Max(0, badgeLeft - textX - 12);
            e.DrawText(Customers[e.Index % Customers.Length], new Rectangle(textX, content.Y + 4, textWidth, half - 4), font: e.FontWithWeight(600),
                       alignment: ContentAlignment.BottomLeft);
            var amount = (149.90m + e.Index * 37 % 900).ToString("C", new System.Globalization.CultureInfo("tr-TR"));
            e.DrawText($"#{100000 + e.Index} · {amount}", new Rectangle(textX, content.Y + half + 1, textWidth, half - 4), e.MutedColor,
                       alignment: ContentAlignment.TopLeft);
        }

        internal Label DateLabel { get; }

        internal TailwindProvider Provider => _tw;

        public bool DarkMode
        {
            get => _tw.DarkMode;
            set => _tw.DarkMode = value;
        }

        private TwTabStrip Tabs(Control parent, TwTabVariant variant, params string[] items)
        {
            var tabs = new TwTabStrip { Variant = variant, AutoSize = true };
            tabs.Tabs.AddRange(items);
            parent.Controls.Add(tabs);
            return tabs;
        }

        private TwPanel Row(Control parent, string classes)
        {
            var row = new TwPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            parent.Controls.Add(row);
            _tw.SetClasses(row, "flex items-center " + classes);
            return row;
        }

        private static Label Caption(Control parent, string text)
        {
            var label = new Label { AutoSize = true, Text = text };
            parent.Controls.Add(label);
            return label;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tw.Dispose();
            base.Dispose(disposing);
        }
    }
}
