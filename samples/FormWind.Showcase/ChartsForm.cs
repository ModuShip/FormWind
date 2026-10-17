using System;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind.Showcase
{
    /// <summary>
    /// Grafikler: üç <see cref="TwStat"/> KPI kartı, eksik değerli iki serili bir çizgi grafik, üstünde hedef çizgisi olan
    /// gruplu çubuklar, yığılmış çubuklar ve bir <see cref="TwDonutChart"/>. Kod ile kurulur (<c>--charts</c>); bir
    /// kategoriye ya da dilime tıklamak alttaki durum satırını günceller.
    /// </summary>
    public sealed class ChartsForm : Form
    {
        private static readonly string[] Months = { "Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara" };
        private static readonly string[] Quarters = { "Ç1", "Ç2", "Ç3", "Ç4" };

        private readonly TailwindProvider _tw;
        private readonly TwSwitch _dark;
        private readonly Label _status;

        public ChartsForm()
        {
            _tw = new TailwindProvider();
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "FormWind Grafikler";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(1160, 840);
            StartPosition = FormStartPosition.CenterScreen;

            // Üst: başlık ve koyu tema anahtarı (flex).
            var header = new TwPanel { Dock = DockStyle.Top, Height = 64 };
            var title = new Label { AutoSize = true, Text = "Satış panosu" };
            var spacer = new TwPanel();
            _dark = new TwSwitch { AutoSize = true, Text = "Koyu" };
            header.Controls.AddRange(new Control[] { title, spacer, _dark });

            var statusBar = new TwPanel { Dock = DockStyle.Bottom, Height = 36 };
            _status = new Label { AutoSize = true, Text = "Bir aya, çeyreğe ya da dilime tıklayın" };
            statusBar.Controls.Add(_status);

            // KPI kartları: biri maliyet gibi artışı kötü sayar, biri çubuk eğilimli.
            var revenue = new TwStat
            {
                Title = "Gelir", Value = "₺1,24 Mn", Delta = 0.124, Caption = "geçen aya göre",
                Trend = new[] { 820.0, 940, 910, 1040, 980, 1120, 1240 },
            };
            var returns = new TwStat
            {
                Title = "İade oranı", Value = "%3,8", Delta = 0.021, Caption = "geçen aya göre", PositiveIsGood = false,
                Trend = new[] { 2.9, 3.1, 3.0, 3.4, 3.5, 3.7, 3.8 },
            };
            var orders = new TwStat
            {
                Title = "Sipariş", Value = "1.482", Delta = -0.046, DeltaText = "-72 sipariş", Caption = "geçen haftaya göre",
                Trend = new[] { 210.0, 198, 225, 240, 205, 190, 214 }, TrendKind = TwSparklineKind.Bar,
            };

            // Çizgi: iki seri; Haziran'da bu yılın değeri eksik (NaN), çizgide boşluk kalır.
            var line = new TwChart { Kind = TwChartKind.Line, ValueFormat = "#,0 'B₺'", AccessibleName = "Aylık gelir" };
            line.SetData(Months,
                new TwChartSeries("Bu yıl", new[] { 82.0, 94, 91, 104, 98, double.NaN, 112, 118, 109, 124, 131, 142 }),
                new TwChartSeries("Geçen yıl", new[] { 70.0, 76, 80, 85, 88, 92, 90, 97, 101, 99, 108, 115 }));

            // Gruplu çubuklar ve üstlerinde hedef çizgisi.
            var grouped = new TwChart { Kind = TwChartKind.Bar, ValueFormat = "#,0 'B₺'", AccessibleName = "Çeyreklik satış" };
            grouped.SetData(Quarters,
                new TwChartSeries("Web", new[] { 120.0, 150, 170, 190 }),
                new TwChartSeries("Mağaza", new[] { 95.0, 110, 105, 130 }),
                new TwChartSeries("Hedef", new[] { 200.0, 240, 270, 300 }) { Kind = TwChartKind.Line });

            // Yığılmış çubuklar: kanalların çeyrek toplamı.
            var stacked = new TwChart { Kind = TwChartKind.Bar, Stacked = true, ValueFormat = "#,0", AccessibleName = "Kanallara göre sipariş" };
            stacked.SetData(Quarters,
                new TwChartSeries("Web", new[] { 620.0, 710, 760, 880 }),
                new TwChartSeries("Mağaza", new[] { 410.0, 380, 430, 470 }),
                new TwChartSeries("Telefon", new[] { 150.0, 170, 140, 160 }));

            var donut = new TwDonutChart { CenterLabel = "sipariş", CenterText = "1.482", AccessibleName = "Kanal payı" };
            donut.SetData(new[] { "Web", "Mağaza", "Telefon", "Bayi" }, new[] { 52.0, 28, 12, 8 });

            var body = new TwPanel { Dock = DockStyle.Fill };
            var lineCard = Card("Aylık gelir", line, "col-span-4");
            var donutCard = Card("Kanal payı", donut, "col-span-2");
            var groupedCard = Card("Çeyreklik satış ve hedef", grouped, "col-span-3");
            var stackedCard = Card("Kanallara göre sipariş", stacked, "col-span-3");
            body.Controls.AddRange(new Control[] { revenue, returns, orders, lineCard, donutCard, groupedCard, stackedCard });
            Controls.AddRange(new Control[] { body, statusBar, header });

            _tw.SetClasses(this, "page");
            _tw.SetClasses(header, "flex items-center gap-3 px-6 py-3");
            _tw.SetClasses(title, "text-xl font-semibold");
            _tw.SetClasses(spacer, "flex-1");
            _tw.SetClasses(body, "grid grid-cols-6 gap-4 px-6 pb-2");
            foreach (var stat in new[] { revenue, returns, orders }) _tw.SetClasses(stat, "col-span-2 h-40");
            _tw.SetClasses(statusBar, "flex items-center px-6");
            _tw.SetClasses(_status, "muted");
            _tw.ContainerControl = this;
            _tw.AutoStyle = true;

            _dark.CheckedChanged += (_, _) => _tw.DarkMode = _dark.Checked;
            line.ItemClick += (_, e) => _status.Text = $"Aylık gelir · {line.GetItemText(e.Index)}";
            grouped.ItemClick += (_, e) => _status.Text = $"Çeyreklik satış · {grouped.GetItemText(e.Index)}";
            stacked.ItemClick += (_, e) => _status.Text = $"Kanallara göre sipariş · {stacked.GetItemText(e.Index)}";
            donut.ItemClick += (_, e) => _status.Text = $"Kanal payı · {donut.GetItemText(e.Index)}";
            ResumeLayout(true);
        }

        public bool DarkMode
        {
            get => _dark.Checked;
            set => _dark.Checked = value;
        }

        /// <summary>Başlıklı bir kart (<paramref name="span"/>: ızgaradaki genişliği); grafik kalan yüksekliği doldurur.</summary>
        private TwPanel Card(string caption, Control chart, string span)
        {
            var card = new TwPanel();
            var label = new Label { AutoSize = true, Text = caption };
            card.Controls.AddRange(new[] { label, chart });
            _tw.SetClasses(card, "card flex flex-col gap-2 h-64 " + span);
            _tw.SetClasses(label, "text-sm font-semibold");
            _tw.SetClasses(chart, "flex-1");
            return card;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tw.Dispose();
            base.Dispose(disposing);
        }
    }
}
