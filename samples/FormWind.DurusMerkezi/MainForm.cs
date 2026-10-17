using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FormWind;

namespace DurusMerkezi
{
    /// <summary>
    /// Ana pencere: Windows başlık çubuğu ve kenar çizgisi olmadan (<see cref="WindowFrame"/>), solda trafik lambaları, gezinme
    /// ve ekrana özgü listeler, üstte ekranın başlığı ve araçları, ortada kaydırılan içerik. Menü yerine kısayollar vardır.
    /// </summary>
    public sealed class MainForm : Form
    {
        private static readonly (string Text, string Icon, string Count)[] NavItems =
        {
            ("Vardiya Kapanışı", "grid", ""),
            ("Vardiya Girişi", "table", "7"),
            ("Duruş Kayıtları", "list", "5"),
            ("Kronik Arızalar", "sync", "5"),
            ("Analitik & Raporlar", "chart", ""),
        };

        private readonly TailwindProvider _tw;
        private readonly Ui _ui;
        private readonly Page[] _pages;
        private readonly List<TwPanel> _nav = new List<TwPanel>();
        private readonly TwPanel _sideSection;
        private readonly TwPanel _sideFooter;
        private readonly TwPanel _toolbarHost;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly TwPanel _host;
        private readonly TwPanel _sidebar;
        private readonly TwPanel _toolbar;
        private readonly TwPanel _toolbar2;
        private readonly TwIcon _sidebarIcon;
        private readonly Control _user;
        private readonly Control _navTitle;
        private readonly TrafficLights _lights;
        private readonly WindowFrame _frame;
        private int _current = -1;

        public MainForm()
        {
            _tw = new TailwindProvider();
            _ui = new Ui(_tw);
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Duruş Merkezi";
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(1440, 900);
            MinimumSize = new Size(1100, 640);
            StartPosition = FormStartPosition.CenterScreen;

            // Kenar çubuğu: sabit gezinme, ekranın listesi, altta son veri ve kullanıcı.
            var navList = _ui.Col("gap-0.5", _ui.Text("İzleme", "section px-2 pb-1"));
            for (int i = 0; i < NavItems.Length; i++) navList.Controls.Add(NavItem(i));
            _sideSection = _ui.Col("gap-0.5");
            _sideFooter = _ui.Col("gap-3");
            var user = _user = _ui.Row("gap-2", _ui.Avatar("MK"), _ui.Col("", _ui.Text("[KULLANICI ADI]", "strong text-xs"), _ui.Text("Vardiya Amiri", "small")));
            _navTitle = navList.Controls[0];

            // Pencere düğmeleri kenar çubuğunun tepesinde (macOS'taki gibi); Windows başlık çubuğu yok. Sağdaki simge
            // kenar çubuğunu daraltır: gezinme yalnızca ikonlarla kalır.
            _lights = _ui.Style(new TrafficLights(), "w-[58px] h-4 flex-none");
            _sidebarIcon = _ui.Icon("sidebar", "w-4 h-4 text-surface-500 hover:text-surface-900 dark:text-surface-400 dark:hover:text-white");
            _sidebarIcon.AccessibleName = "Kenar çubuğunu daralt";
            _sidebarIcon.Cursor = Cursors.Hand;
            _sidebarIcon.Click += (_, _) => ToggleSidebar();
            var lightsRow = _ui.Row("h-6 px-1", _lights, _ui.Spacer(), _sidebarIcon);
            _sidebar = _ui.Box("sidebar flex flex-col gap-5 px-3 pt-3 pb-4", lightsRow, navList, _sideSection, _ui.Spacer(), _sideFooter, user);
            _sidebar.AutoSize = false;
            _sidebar.Dock = DockStyle.Left;
            _sidebar.Width = 232;

            // Araç çubuğu: başlık ve ekranın kontrolleri.
            _title = _ui.Text("", "strong");
            _subtitle = _ui.Text("", "small");
            var back = _ui.Style(new TwIcon { IconName = "chevron-left", AccessibleName = "Geri" }, "w-4 h-4 text-surface-400");
            var forward = _ui.Style(new TwIcon { IconName = "chevron-right", AccessibleName = "İleri" }, "w-4 h-4 text-surface-500 dark:text-surface-600");
            _toolbarHost = _ui.Row("gap-2");
            _toolbar = _ui.Row("toolbar gap-3 px-5", back, forward, _ui.Col("gap-0 ml-2", _title, _subtitle), _ui.Spacer(), _toolbarHost);
            _toolbar.AutoSize = false;
            _toolbar.Dock = DockStyle.Top;
            _toolbar.Height = 56;
            // İkinci satır (ekran isterse): ERP'den çek gibi ikincil araçlar; araç çubuğunun alt çizgisi onun altına iner.
            _toolbar2 = _ui.Row("toolbar gap-2 px-5 pb-2");
            _toolbar2.AutoSize = false;
            _toolbar2.Dock = DockStyle.Top;
            _toolbar2.Height = 40;

            _host = new TwPanel { Dock = DockStyle.Fill, AutoScroll = true };
            var main = new TwPanel { Dock = DockStyle.Fill };
            main.Controls.Add(_host);
            main.Controls.Add(_toolbar2);
            main.Controls.Add(_toolbar);

            Controls.Add(main);
            Controls.Add(_sidebar);

            _pages = new Page[]
            {
                new ShiftClosingPage(_ui), new ShiftEntryPage(_ui), new DowntimeRecordsPage(_ui), new ChronicFailuresPage(_ui), new AnalyticsPage(_ui),
            };

            _tw.SetClasses(this, "page");
            _tw.SetClasses(_host, "page");
            _tw.ContainerControl = this;
            _tw.AutoStyle = true;
            _frame = new WindowFrame(this, IsDragSurface);
            _tw.DarkModeChanged += (_, _) => UpdateTheme();
            HandleCreated += (_, _) => UpdateTheme();
            ResumeLayout(true);
        }

        public bool DarkMode
        {
            get => _tw.DarkMode;
            set => _tw.DarkMode = value;
        }

        private void UpdateTheme()
        {
            _ui.ApplyTheme(_tw.DarkMode);
            // Windows 11'in pencere çevresindeki ince çizgisi kenar çubuğunun kenarlığıyla aynı tonda.
            _frame.SetBorderColor(_tw.DarkMode ? ColorTranslator.FromHtml("#2a2a2d") : ColorTranslator.FromHtml("#d4d4d8"));
        }

        /// <summary>Pencereyi taşıyan yüzeyler: kenar çubuğu ve araç çubuğunun etkileşimsiz kısımları (başlık, boşluklar).</summary>
        private bool IsDragSurface(Control control)
        {
            for (var c = control; c != null; c = c.Parent)
            {
                if (c is ButtonBase || c is TextBoxBase || c is TwTextBox || c is TwSelect || c is TwTabStrip || c is TrafficLights || c == _sidebarIcon) return false;
                if (c is TwPanel panel && _nav.Contains(panel)) return false;
                if (c == _sidebar || c == _toolbar || c == _toolbar2) return true;
            }
            return false;
        }

        // Menü yerine klavye kısayolları: Ctrl+1…5 ekranlar, Ctrl+Shift+L tema.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData >= (Keys.Control | Keys.D1) && keyData <= (Keys.Control | Keys.D5))
            {
                ShowPage((int)(keyData & ~Keys.Control) - (int)Keys.D1);
                return true;
            }
            if (keyData == (Keys.Control | Keys.Shift | Keys.L))
            {
                DarkMode = !DarkMode;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private TwPanel NavItem(int index)
        {
            var (text, icon, count) = NavItems[index];
            var item = _ui.Row("nav h-8 gap-2.5",
                _ui.Style(new TwIcon { IconName = icon }, "nav-icon flex-none"), _ui.Text(text, "text-sm"), _ui.Spacer(), _ui.Text(count, "small"));
            item.AccessibleName = text;
            item.AccessibleRole = AccessibleRole.Link;
            item.Cursor = Cursors.Hand;
            void Go(object sender, EventArgs e) => ShowPage(index);
            item.Click += Go;
            foreach (Control child in item.Controls) child.Click += Go;
            _nav.Add(item);
            return item;
        }

        // Daraltılmış kenar çubuğu: trafik lambaları ve gezinme ikonları kalır; metinler ve listeler gizlenir.
        private bool _collapsed;

        private void ToggleSidebar()
        {
            _collapsed = !_collapsed;
            SuspendLayout();
            foreach (var item in _nav)
                for (int i = 1; i < item.Controls.Count; i++) item.Controls[i].Visible = !_collapsed;
            _sideSection.Visible = _sideFooter.Visible = _user.Visible = _navTitle.Visible = !_collapsed;
            _sidebar.Width = _collapsed ? 112 : 232;
            _sidebarIcon.AccessibleName = _collapsed ? "Kenar çubuğunu genişlet" : "Kenar çubuğunu daralt";
            ResumeLayout(true);
        }

        /// <summary>Bir ekranı gösterir (0: kapanış, 1: giriş, 2: kayıtlar, 3: kronik, 4: analitik).</summary>
        public void ShowPage(int index)
        {
            index = Math.Max(0, Math.Min(_pages.Length - 1, index));
            if (index == _current) return;
            _current = index;
            var page = _pages[index];
            SuspendLayout();
            for (int i = 0; i < _nav.Count; i++)
                _tw.SetClasses(_nav[i], (i == index ? "nav-on" : "nav") + " flex items-center h-8 gap-2.5");

            _title.Text = page.Title;
            _subtitle.Text = page.Subtitle;
            _toolbarHost.Controls.Clear();
            _toolbarHost.Controls.AddRange(page.Toolbar);
            var second = page.SecondaryToolbar;
            _toolbar2.Controls.Clear();
            if (second != null) _toolbar2.Controls.AddRange(second);
            _toolbar2.Visible = second != null;
            _tw.SetClasses(_toolbar, (second != null ? "toolbar-top" : "toolbar") + " flex items-center gap-3 px-5");

            _sideSection.Controls.Clear();
            _sideSection.Controls.Add(_ui.Text(page.SideTitle, "section px-2 pb-1"));
            foreach (var item in page.SideItems)
            {
                var row = _ui.Row("nav h-8 gap-2.5");
                if (item.Dot is Tone tone) row.Controls.Add(_ui.Dot(tone));
                row.Controls.AddRange(new Control[] { _ui.Text(item.Text, "text-sm"), _ui.Spacer(), _ui.Text(item.Value, "small") });
                _sideSection.Controls.Add(row);
            }
            _sideFooter.Controls.Clear();
            _sideFooter.Controls.Add(_ui.Row("gap-2 px-2", _ui.Dot(page.FooterTone), _ui.Text(page.Footer, "small")));

            _host.Controls.Clear();
            var content = page.Content;
            content.Dock = DockStyle.Top;
            _host.Controls.Add(content);
            _host.AutoScrollPosition = Point.Empty;
            ResumeLayout(true);
        }

        /// <summary>
        /// Ekranın tamamının görüntüsü: pencere ekran dışında açılır, kaydırılan içerik tam yüksekliğiyle pencerenin
        /// görüntüsünün altına eklenir (tasarım dokümanının uzun sayfaları gibi).
        /// </summary>
        public Bitmap RenderFullPage()
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-10000, -10000);
            ShowInTaskbar = false;
            Show();
            ActiveControl = null;
            Application.DoEvents();

            // Görüntü yalnızca istemci alanıdır; pencerenin görünmez boyutlandırma kenarları alınmaz.
            var content = _pages[_current].Content;
            var hostOrigin = PointToClient(_host.PointToScreen(Point.Empty));
            int height = Math.Max(ClientSize.Height, hostOrigin.Y + content.Height + 8);
            var bitmap = new Bitmap(ClientSize.Width, height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(_host.BackColor);
                using (var brush = new SolidBrush(_sidebar.BackColor)) g.FillRectangle(brush, 0, 0, _sidebar.Width, height);
                // Form değil üst düzey kontroller çizilir: Form.DrawToBitmap çerçeveyi pencere stilinden çizerdi.
                foreach (Control child in Controls)
                {
                    if (!child.Visible || child.Width <= 0 || child.Height <= 0) continue;
                    using var part = new Bitmap(child.Width, child.Height);
                    child.DrawToBitmap(part, new Rectangle(Point.Empty, child.Size));
                    g.DrawImage(part, child.Location);
                }
            }
            using (var page = new Bitmap(content.Width, content.Height))
            {
                content.DrawToBitmap(page, new Rectangle(Point.Empty, content.Size));
                using var g = Graphics.FromImage(bitmap);
                g.DrawImage(page, hostOrigin.X, hostOrigin.Y);
            }
            return bitmap;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _frame.Dispose();
                _tw.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
