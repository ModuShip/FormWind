using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace FormWind.Showcase
{
    /// <summary>
    /// DataGridView özellikleri: <see cref="TwSortableBindingList{T}"/>'e bağlı bir tablo; arama, süzme menüsü (tutar ve tarih
    /// için aralık kutuları), gruplama, alt toplam, satır detayları, yeni hücre tipleri, düzen kaydetme ve CSV dışa aktarma.
    /// Kod ile kurulur (<c>--grid</c>); <see cref="ShowcaseState"/> görüntü için bir grup ve açık bir satır hazırlar.
    /// </summary>
    public sealed class GridForm : Form
    {
        private static readonly (string Text, string Column)[] GroupOptions =
        {
            ("Gruplama yok", null), ("Şehre göre", "city"), ("Duruma göre", "status"),
        };

        private readonly TailwindProvider _tw;
        private readonly DataGridView _grid;
        private readonly TwSortableBindingList<Order> _orders;
        private readonly TwSelect _groupBy;
        private readonly TwSwitch _dark;
        private readonly TwTextBox _search;
        private readonly Label _status;
        private string _layout;

        public GridForm()
        {
            _tw = new TailwindProvider();
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "FormWind DataGridView";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(1160, 680);
            StartPosition = FormStartPosition.CenterScreen;

            // Üst: başlık ve araç çubuğu (flex).
            var header = new TwPanel { Dock = DockStyle.Top, Height = 64 };
            var title = new Label { AutoSize = true, Text = "Siparişler" };
            _search = new TwTextBox { Width = 220, PlaceholderText = "Ara… (müşteri, şehir, etiket)", AccessibleName = "Ara" };
            _groupBy = new TwSelect { Width = 150, AccessibleName = "Gruplama" };
            foreach (var option in GroupOptions) _groupBy.Items.Add(option.Text);
            _groupBy.SelectedIndex = 0;
            var spacer = new TwPanel();
            var save = new TwButton { Text = "Düzeni kaydet", Height = 34, AutoSize = true };
            var load = new TwButton { Text = "Düzeni yükle", Height = 34, AutoSize = true };
            var export = new TwButton { Text = "CSV", Height = 34, AutoSize = true };
            _dark = new TwSwitch { AutoSize = true, Text = "Koyu" };
            header.Controls.AddRange(new Control[] { title, _search, _groupBy, spacer, save, load, export, _dark });

            var statusBar = new TwPanel { Dock = DockStyle.Bottom, Height = 36 };
            _status = new Label { AutoSize = true, Text = "Başlıktaki süzme düğmesi: Tutar ve Tarih için en az / en çok kutuları" };
            statusBar.Controls.Add(_status);

            // Orta: tablo; alt toplam AddFooter ile altına yerleşir.
            var body = new TwPanel { Dock = DockStyle.Fill };
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false, ReadOnly = true,
                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AccessibleName = "Siparişler",
            };
            var expand = new TwExpanderColumn { Name = "expand" };
            var no = new DataGridViewTextBoxColumn { Name = "no", HeaderText = "NO", DataPropertyName = nameof(Order.No) };
            var customer = new TwLinkColumn { Name = "customer", HeaderText = "MÜŞTERİ", DataPropertyName = nameof(Order.Customer) };
            var city = new DataGridViewTextBoxColumn { Name = "city", HeaderText = "ŞEHİR", DataPropertyName = nameof(Order.City) };
            var status = new TwBadgeColumn
            {
                Name = "status", HeaderText = "DURUM", DataPropertyName = nameof(Order.Status),
                Variants = "Teslim edildi: bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-100; "
                           + "Kargoda: bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-100; "
                           + "İptal: bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-100; *: bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-100",
            };
            var tags = new TwTagsColumn
            {
                Name = "tags", HeaderText = "ETİKETLER", DataPropertyName = nameof(Order.Tags),
                Variants = "vip: bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-100; "
                           + "yeni: bg-violet-100 text-violet-800 dark:bg-violet-900 dark:text-violet-100; *: bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-100",
            };
            var trend = new TwSparklineColumn { Name = "trend", HeaderText = "TREND", DataPropertyName = nameof(Order.Trend), Kind = TwSparklineKind.Area };
            var date = new TwDateColumn { Name = "date", HeaderText = "TARİH", DataPropertyName = nameof(Order.Date), Relative = true };
            var amount = new TwNumberColumn { Name = "amount", HeaderText = "TUTAR", DataPropertyName = nameof(Order.Amount), Culture = "tr-TR" };
            amount.DefaultCellStyle.Format = "C2";
            var actions = new TwActionsColumn { Name = "actions", Actions = "Düzenle; Kopyala; -; Sil" };
            _grid.Columns.AddRange(expand, no, customer, city, status, tags, trend, date, amount, actions);
            body.Controls.Add(_grid);

            Controls.AddRange(new Control[] { body, statusBar, header });

            _orders = new TwSortableBindingList<Order>(Order.Sample());
            _grid.DataSource = _orders;

            _tw.SetClasses(header, "flex items-center gap-3 px-6 py-3");
            _tw.SetClasses(title, "text-xl font-semibold mr-4");
            _tw.SetClasses(spacer, "flex-1");
            _tw.SetClasses(save, "btn-ghost");
            _tw.SetClasses(load, "btn-ghost");
            _tw.SetClasses(export, "btn-primary");
            _tw.SetClasses(body, "px-6");
            _tw.SetClasses(statusBar, "flex items-center px-6");
            _tw.SetClasses(_status, "muted");
            _tw.SetClasses(_grid, "table table-striped");
            _tw.SetColumnClasses(expand, "w-10");
            _tw.SetColumnClasses(no, "w-20 text-gray-500 dark:text-gray-400");
            _tw.SetColumnClasses(customer, "flex-1 min-w-36");
            _tw.SetColumnClasses(city, "w-24");
            _tw.SetColumnClasses(status, "w-32");
            _tw.SetColumnClasses(tags, "w-36");
            _tw.SetColumnClasses(trend, "w-28");
            _tw.SetColumnClasses(date, "w-32");
            _tw.SetColumnClasses(amount, "w-32 font-semibold header:text-right");
            _tw.SetColumnClasses(actions, "w-12");
            _tw.ContainerControl = this;
            _tw.AutoStyle = true;

            TwGrid.AttachSearchBox(_grid, _search);
            TwGrid.EnableFilterMenu(_grid);
            TwGrid.EnableColumnChooser(_grid);
            TwGrid.SetEmptyText(_grid, "Eşleşen sipariş yok");
            TwGrid.SetRowDetails(_grid, row => Details((Order)row.DataBoundItem), height: 96);

            var footer = TwGrid.AddFooter(_grid);
            footer.SetText(customer, "Toplam");
            footer.SetAggregate(no, TwAggregate.Count);
            footer.SetAggregate(amount, TwAggregate.Sum, "C2");

            _groupBy.SelectedIndexChanged += (_, _) =>
            {
                string column = GroupOptions[_groupBy.SelectedIndex].Column;
                TwGrid.SetGroupBy(_grid, column == null ? null : _grid.Columns[column]);
            };
            _dark.CheckedChanged += (_, _) => _tw.DarkMode = _dark.Checked;
            _grid.CellContentClick += (_, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == customer.Index)
                    _status.Text = $"Müşteri: {_orders[e.RowIndex].Customer}";
            };
            actions.MenuOpening += (_, e) => e.Menu.Items[3].Enabled = _orders[e.RowIndex].Status != "Teslim edildi";
            actions.ActionClicked += (_, e) =>
            {
                var order = _orders[e.RowIndex];
                if (e.Action == "Sil") _orders.Remove(order);
                _status.Text = $"{e.Action}: {order.No}";
            };
            save.Click += (_, _) =>
            {
                _layout = TwGrid.SaveLayout(_grid);
                _status.Text = "Düzen kaydedildi (kolon sırası, genişlikler, sıralama, gruplama, arama, süzgeçler).";
            };
            load.Click += (_, _) =>
            {
                if (_layout == null) { _status.Text = "Önce düzeni kaydedin."; return; }
                TwGrid.LoadLayout(_grid, _layout);
                string group = TwGrid.GetGroupBy(_grid)?.Name;
                _groupBy.SelectedIndex = Math.Max(0, Array.FindIndex(GroupOptions, o => o.Column == group));
                _search.Text = TwGrid.GetSearchText(_grid) ?? "";
                _status.Text = "Düzen yüklendi.";
            };
            export.Click += (_, _) =>
            {
                using var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "siparisler.csv" };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                TwGrid.ExportCsv(_grid, dialog.FileName);
                _status.Text = $"Dışa aktarıldı: {Path.GetFileName(dialog.FileName)}";
            };
            ResumeLayout(true);
        }

        public bool DarkMode
        {
            get => _dark.Checked;
            set => _dark.Checked = value;
        }

        /// <summary>Görüntü için: şehre göre gruplar ve bir satırın detaylarını açar.</summary>
        public void ShowcaseState()
        {
            _groupBy.SelectedIndex = 1;
            int row = _orders.IndexOf(_orders.First(o => o.No == "#10231"));
            TwGrid.SetRowExpanded(_grid, row, true);
            _grid.ClearSelection();
        }

        private Control Details(Order order)
        {
            var panel = new TwPanel();
            var lines = new Label
            {
                AutoSize = true,
                Text = $"{order.Customer} · {order.City}\n{order.Items} ürün · Kargo: {order.Carrier}\nNot: {order.Note}",
            };
            panel.Controls.Add(lines);
            _tw.SetClasses(panel, "bg-gray-50 dark:bg-gray-800 px-14 py-3");
            _tw.SetClasses(lines, "text-sm text-gray-600 dark:text-gray-300");
            return panel;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tw.Dispose();
            base.Dispose(disposing);
        }

        public sealed class Order
        {
            public string No { get; set; }
            public string Customer { get; set; }
            public string City { get; set; }
            public string Status { get; set; }
            public string Tags { get; set; }
            public double[] Trend { get; set; }
            public DateTime Date { get; set; }
            public decimal Amount { get; set; }
            public int Items { get; set; }
            public string Carrier { get; set; }
            public string Note { get; set; }

            public static Order[] Sample()
            {
                var now = DateTime.Now;
                return new[]
                {
                    New("#10238", "Ayşe Yılmaz", "İstanbul", "Kargoda", "vip, yeni", now.AddMinutes(-12), 1250m, 3, "Yurtiçi", "Kapıya bırakılabilir"),
                    New("#10237", "Mehmet Demir", "Ankara", "Hazırlanıyor", "", now.AddHours(-3), 480.5m, 1, "Aras", "—"),
                    New("#10236", "Zeynep Kaya", "İzmir", "Teslim edildi", "vip", now.AddDays(-1), 2940m, 5, "MNG", "Fatura şirkete"),
                    New("#10235", "Can Öztürk", "İstanbul", "İptal", "iade", now.AddDays(-2), -320m, 1, "—", "Müşteri iptal etti"),
                    New("#10234", "Elif Şahin", "Ankara", "Teslim edildi", "yeni", now.AddDays(-3), 760.25m, 2, "Yurtiçi", "—"),
                    New("#10233", "Burak Aydın", "İzmir", "Kargoda", "toptan, vip, yeni", now.AddDays(-4), 5320m, 12, "Sürat", "Paletli gönderim"),
                    New("#10232", "Selin Arslan", "İstanbul", "Teslim edildi", "", now.AddDays(-6), 189.9m, 1, "PTT", "—"),
                    New("#10231", "Emre Koç", "Bursa", "Kargoda", "vip", now.AddDays(-9), 1675m, 4, "Aras", "Hediye paketi"),
                    New("#10230", "Deniz Yıldız", "Bursa", "Teslim edildi", "toptan", now.AddDays(-14), 3410m, 9, "MNG", "—"),
                    New("#10229", "Ece Çelik", "İzmir", "Teslim edildi", "", now.AddDays(-21), 645m, 2, "Yurtiçi", "—"),
                };
            }

            private static Order New(string no, string customer, string city, string status, string tags, DateTime date, decimal amount,
                                     int items, string carrier, string note)
            {
                // Tutar ve numaradan türeyen sabit bir eğri: görüntüler her çalıştırmada aynı kalsın.
                int seed = int.Parse(no.Substring(1));
                var trend = Enumerable.Range(0, 8).Select(i => (double)((seed * (i + 3) * 7919) % 97) + (double)Math.Abs(amount) / 200).ToArray();
                return new Order
                {
                    No = no, Customer = customer, City = city, Status = status, Tags = tags, Trend = trend, Date = date, Amount = amount,
                    Items = items, Carrier = carrier, Note = note,
                };
            }
        }
    }
}
