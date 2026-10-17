using System;
using System.Windows.Forms;

namespace FormWind.Showcase
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            LoadOrders();
        }

        public bool DarkMode
        {
            get => chkDark.Checked;
            set => chkDark.Checked = value;
        }

        public void FocusFirstInput() => txtName.Focus();

        private void chkDark_CheckedChanged(object sender, EventArgs e)
        {
            tailwindProvider1.DarkMode = chkDark.Checked;
        }

        private void LoadOrders()
        {
            gridOrders.Columns.Add("no", "SİPARİŞ NO");
            gridOrders.Columns.Add("customer", "MÜŞTERİ");
            gridOrders.Columns.Add("date", "TARİH");
            gridOrders.Columns.Add("amount", "TUTAR");
            gridOrders.Columns.Add(new TwBadgeColumn
            {
                Name = "status",
                HeaderText = "DURUM",
                Variants = "Hazırlanıyor: bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-100; " +
                           "Kargoda: bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-100; " +
                           "Teslim edildi: bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-100; " +
                           "*: bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-300",
            });
            gridOrders.Columns.Add(new TwProgressColumn { Name = "progress", HeaderText = "İLERLEME" });
            gridOrders.Columns.Add(new TwCheckColumn { Name = "invoiced", HeaderText = "" });

            // Kolon class'ları: hizalama, kalınlık ve genişlik.
            tailwindProvider1.SetColumnClasses(gridOrders.Columns["no"], "text-gray-500 font-medium dark:text-gray-400");
            tailwindProvider1.SetColumnClasses(gridOrders.Columns["amount"], "text-right font-semibold header:text-right");
            tailwindProvider1.SetColumnClasses(gridOrders.Columns["progress"], "flex-1 min-w-24");
            tailwindProvider1.SetColumnClasses(gridOrders.Columns["invoiced"], "w-12 text-center");

            gridOrders.Rows.Add("#10234", "Ayşe Yılmaz", "08.10.2026", "1.250,00 ₺", "Hazırlanıyor", 35, false);
            gridOrders.Rows.Add("#10233", "Mehmet Demir", "08.10.2026", "480,50 ₺", "Kargoda", 70, true);
            gridOrders.Rows.Add("#10232", "Zeynep Kaya", "07.10.2026", "2.940,00 ₺", "Teslim edildi", 100, true);
            gridOrders.Rows.Add("#10231", "Can Öztürk", "07.10.2026", "175,00 ₺", "İptal", 0, false);
            gridOrders.Rows.Add("#10230", "Elif Şahin", "06.10.2026", "860,25 ₺", "Teslim edildi", 100, true);

            // Sıralama oku (bağlı listede başlığa tıklamak bunu kendisi ayarlar; bkz. TwSortableBindingList).
            gridOrders.Columns["date"].HeaderCell.SortGlyphDirection = SortOrder.Descending;
            TwGrid.SetEmptyText(gridOrders, "Henüz sipariş yok");

            // Satır kuralı: iptal edilen siparişler soluk.
            tailwindProvider1.SetRowClasses(gridOrders, row =>
                (string)row.Cells["status"].Value == "İptal" ? "text-gray-400 dark:text-gray-500" : null);
        }
    }
}
