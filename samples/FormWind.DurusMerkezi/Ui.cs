using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FormWind;

namespace DurusMerkezi
{
    /// <summary>Durum tonları: rozet, nokta ve çubuk renkleri bunlardan seçilir.</summary>
    internal enum Tone
    {
        Gray,
        Blue,
        Green,
        Amber,
        Red,
        Violet,
    }

    /// <summary>
    /// Ekranları kuran küçük yardımcılar: her kontrol FormWind class'larıyla biçimlenir (renkler <see cref="Theme"/>'deki
    /// bileşenlerden), yerleşim flex/grid class'larıyla yapılır.
    /// </summary>
    internal sealed class Ui
    {
        private readonly List<TwHeatmap> _heatmaps = new List<TwHeatmap>();

        public Ui(TailwindProvider tw) => Tw = tw;

        public TailwindProvider Tw { get; }

        public T Style<T>(T control, string classes) where T : Control
        {
            Tw.SetClasses(control, classes);
            return control;
        }

        public static string Palette(Tone tone) => tone switch
        {
            Tone.Blue => "primary",
            Tone.Green => "success",
            Tone.Amber => "warning",
            Tone.Red => "danger",
            Tone.Violet => "violet",
            _ => "surface",
        };

        /// <summary>Tonun çizimde kullanılan rengi (grafikler ve noktalar için; class'lar yerine Color isteyen yerler).</summary>
        public static Color ColorOf(Tone tone) => tone switch
        {
            Tone.Blue => ColorTranslator.FromHtml("#0a84ff"),
            Tone.Green => ColorTranslator.FromHtml("#30d158"),
            Tone.Amber => ColorTranslator.FromHtml("#ff9f0a"),
            Tone.Red => ColorTranslator.FromHtml("#ff453a"),
            Tone.Violet => ColorTranslator.FromHtml("#bf5af2"),
            _ => ColorTranslator.FromHtml("#8e8e93"),
        };

        private static Color HeatLow(bool dark) => ColorTranslator.FromHtml(dark ? "#1b1b1d" : "#f1f1f3");

        /// <summary>Temaya göre değişen, class'larla verilemeyen renkler (ısı haritalarının en düşük tonu).</summary>
        public void ApplyTheme(bool dark)
        {
            foreach (var heat in _heatmaps) heat.LowColor = HeatLow(dark);
        }

        public TwHeatmap Heat(TwHeatmap heat, string classes)
        {
            _heatmaps.Add(heat);
            heat.LowColor = HeatLow(Tw.DarkMode);
            heat.ShowLegend = false;
            return Style(heat, "chart " + classes);
        }

        // ---- Metin ----

        public Label Text(string text, string classes = "body") =>
            Style(new Label { AutoSize = true, Text = text, UseMnemonic = false }, classes);

        /// <summary>Kaydırılan, birkaç satıra sığan metin: genişliği kapsayıcıdan alır, yüksekliği sabittir.</summary>
        public Label Paragraph(string text, int height, string classes = "muted") =>
            Style(new Label { AutoSize = false, Text = text, UseMnemonic = false }, classes + " h-[" + height + "px]");

        /// <summary>Kutu içinde paragraf (not, açıklama): kutu dikey flex olduğundan metin genişliği doldurur.</summary>
        public TwPanel Note(string text, int height, string classes = "body") => Box("tile flex flex-col py-3", Paragraph(text, height, classes));

        public Label Chip(string text, Tone tone) => Text(text, "chip-" + tone.ToString().ToLowerInvariant() + " py-0.5 flex-none");

        /// <summary>Klavye tuşu (Tab, ↑, Ctrl): küçük, gri bir kutu.</summary>
        public Label Kbd(string text) => Text(text, "kbd py-0.5 flex-none");

        public TwPanel Dot(Tone tone, int size = 8) =>
            Box($"w-[{size}px] h-[{size}px] rounded-full flex-none bg-{Palette(tone)}-500");

        /// <summary>Baş harfler (ya da adım numarası) yuvarlak bir zeminde; <paramref name="kind"/> zemin bileşeni.</summary>
        public TwPanel Avatar(string initials, string kind = "avatar", string classes = "") =>
            Box(kind + " flex items-center justify-center w-7 h-7 flex-none " + classes, Text(initials, "text-xs font-bold"));

        public TwIcon Icon(string name, string classes = "w-4 h-4 text-surface-500 dark:text-surface-400") =>
            Style(new TwIcon { IconName = name, AccessibleName = name }, classes + " flex-none");

        // ---- Kutular ----

        public TwPanel Box(string classes, params Control[] children)
        {
            // İçerikten boyutlanır; w-/h-/flex-1 class'ları bunu geçersiz kılar.
            var panel = new TwPanel { AutoSize = true };
            panel.Controls.AddRange(children);
            Tw.SetClasses(panel, classes);
            return panel;
        }

        public TwPanel Row(string classes, params Control[] children) => Box("flex items-center gap-2 " + classes, children);

        public TwPanel Col(string classes, params Control[] children) => Box("flex flex-col " + classes, children);

        public TwPanel Spacer()
        {
            var spacer = new TwPanel { Size = new Size(1, 1) };
            Tw.SetClasses(spacer, "flex-1");
            return spacer;
        }

        /// <summary>Yatay ayraç çizgisi.</summary>
        public TwPanel Divider() => Box("h-px flex-none bg-surface-200 dark:bg-surface-700");

        /// <summary>Başlıklı kart: başlık, alt başlık ve sağda isteğe bağlı bir öğe; altında içerik.</summary>
        public TwPanel Card(string title, string subtitle, Control right, string classes, params Control[] body)
        {
            var heading = Col("gap-0.5", Text(title, "h2"));
            if (!string.IsNullOrEmpty(subtitle)) heading.Controls.Add(Text(subtitle, "small"));
            var header = right == null ? Row("items-start", heading) : Row("items-start", heading, Spacer(), right);
            var card = Box("card flex flex-col gap-3 " + classes, header);
            card.Controls.AddRange(body);
            return card;
        }

        /// <summary>KPI kartı: başlık, büyük değer ve yanında küçük birim; altta tonlu bir rozet ve açıklama.</summary>
        public TwPanel Kpi(string title, string value, string unit, string chip, Tone tone, string caption, bool captionRight = true)
        {
            var number = Row("items-end gap-1.5", Text(value, "kpi"));
            if (unit.Length > 0) number.Controls.Add(Text(unit, "muted pb-0.5"));
            var bottom = captionRight
                ? Row("", Chip(chip, tone), Spacer(), Text(caption, "small text-[11px]"))
                : Row("gap-2", Chip(chip, tone), Text(caption, "small text-[11px]"));
            return Col("card flex-1 gap-1 px-4 py-3.5 h-[116px]", Text(title, "body text-xs font-medium"), number, Spacer(), bottom);
        }

        // ---- Girişler ----

        public TwButton Button(string text, string classes = "btn-sm", string icon = "")
        {
            var button = new TwButton { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, UseMnemonic = false, IconName = icon, IconSpacing = 6 };
            Tw.SetClasses(button, classes + " flex-none");
            return button;
        }

        public TwButton Primary(string text, string icon = "") => Button(text, "btn-sm-primary text-sm px-3.5 py-1.5", icon);

        /// <summary>Araç çubuğu düğmesi: zeminsiz, isteğe bağlı ikonlu.</summary>
        public TwButton Plain(string text, string icon = "") => Button(text, "btn-plain", icon);

        /// <summary>Yalnızca ikon: araç çubuğundaki arama gibi.</summary>
        public TwButton IconButton(string icon, string name, string classes = "btn-plain px-1.5")
        {
            var button = Button("", classes, icon);
            button.AccessibleName = name;
            return button;
        }

        public TwTextBox Input(string placeholder, string text = "", string classes = "")
        {
            var box = new TwTextBox { PlaceholderText = placeholder, Text = text, AccessibleName = placeholder };
            Tw.SetClasses(box, "input px-2 py-1 text-sm h-8 dark:bg-surface-950 dark:border-surface-700 " + classes);
            return box;
        }

        /// <summary>Arama kutusu: büyüteç, metin ve sağda kısayol tuşu (tasarımdaki ⌘K'nin Windows karşılığı).</summary>
        public TwPanel Search(string placeholder, string width)
        {
            var box = new TwTextBox { PlaceholderText = placeholder, AccessibleName = placeholder };
            // Yerel metin kutusu saydam olamaz: zemini arama kutusununkiyle aynı.
            Tw.SetClasses(box, "flex-1 text-sm bg-white border-0 px-0 py-0 dark:bg-surface-800 placeholder:text-surface-400 dark:placeholder:text-surface-500");
            return Box("search flex-none " + width, Icon("search", "w-3.5 h-3.5 text-surface-400 dark:text-surface-500"), box, Text("Ctrl K", "kbd py-0 flex-none"));
        }

        public TwSelect Select(string[] items, int selected, string classes = "")
        {
            var select = new TwSelect();
            foreach (var item in items) select.Items.Add(item);
            select.SelectedIndex = selected;
            Tw.SetClasses(select, "select px-2 py-1 text-sm h-7 accent-primary-600 dark:bg-surface-950 dark:border-surface-700 dark:accent-primary-500 " + classes);
            return select;
        }

        public TwSwitch Switch(bool on, string name)
        {
            var toggle = new TwSwitch { Checked = on, AccessibleName = name, AutoSize = true };
            Tw.SetClasses(toggle, "toggle fill-surface-300 dark:fill-surface-600");
            return toggle;
        }

        /// <summary>Segment seçici (Son vardiya / Dün / 7 gün …).</summary>
        public TwTabStrip Segments(params string[] items)
        {
            // Genişliği sekmelerden hesaplanır (AutoSize).
            var strip = new TwTabStrip { Variant = TwTabVariant.Segmented, AccessibleName = string.Join(", ", items), AutoSize = true };
            foreach (var item in items) strip.Tabs.Add(item.Replace("&", "&&"));
            strip.SelectedIndex = 0;
            Tw.SetClasses(strip, $"tabs border-0 text-xs font-semibold h-8 flex-none text-surface-700 fill-surface-200 selected:bg-white selected:text-surface-900 "
                                 + "dark:text-surface-100 dark:fill-surface-800 dark:selected:bg-surface-600 dark:selected:text-white");
            return strip;
        }

        // ---- Göstergeler ----

        public TwProgress Bar(double value, double maximum, Tone tone, string classes = "h-1.5")
        {
            var bar = new TwProgress { Maximum = 1000, Value = maximum <= 0 ? 0 : (int)Math.Round(Math.Max(0, Math.Min(1, value / maximum)) * 1000) };
            string accent = tone == Tone.Gray ? "surface-400" : Palette(tone) + "-500";
            Tw.SetClasses(bar, $"range fill-surface-200 dark:fill-surface-950 accent-{accent} dark:accent-{accent} {classes}");
            return bar;
        }

        public TwSparkline Spark(double[] values, Tone tone, string classes = "h-8")
        {
            var spark = new TwSparkline { Values = values, LineColor = ColorOf(tone), ShowMarker = false };
            Tw.SetClasses(spark, "chart " + classes);
            return spark;
        }

        /// <summary>Grafik göstergesi kartın başlık satırında: kare ('s'), çizgi ('l') ya da kesikli çizgi ('d').</summary>
        public TwPanel Legend(params (string Text, Color Color, char Kind)[] items)
        {
            var row = Row("gap-4");
            foreach (var (text, color, kind) in items)
            {
                var key = new LegendKey { Color = color, Kind = kind };
                row.Controls.Add(Row("gap-1.5", Style(key, kind == 's' ? "w-[10px] h-[10px] flex-none" : "w-[16px] h-[10px] flex-none"), Text(text, "small")));
            }
            return row;
        }

        /// <summary>Renk geçişli ölçek göstergesi: "0 ▬▬▬ 120+ dk".</summary>
        public TwPanel Gradient(string low, string high, string gradient) =>
            Row("gap-2", Text(low, "small"), Box("w-[140px] h-[6px] rounded-full flex-none " + gradient), Text(high, "small"));

        // ---- Tablolar ----

        /// <summary>Salt okunur, satır başlıksız, tam satır seçimli bir tablo; başlık kartla aynı zeminde.</summary>
        public DataGridView Grid(string name, int rowHeight = 40)
        {
            var grid = new DataGridView
            {
                AutoGenerateColumns = false, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                RowHeadersVisible = false, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AccessibleName = name,
                MultiSelect = false,
            };
            grid.RowTemplate.Height = rowHeight;
            // Açılışta seçili satır gösterilmez (tasarımdaki gibi).
            grid.VisibleChanged += (_, _) => grid.ClearSelection();
            Tw.SetClasses(grid, $"table-plain row:h-[{rowHeight}px]");
            return grid;
        }

        /// <summary>Rozet sütunlarının ortak varyantları: metin → ton.</summary>
        public static string BadgeVariants(params (string Text, Tone Tone)[] items)
        {
            var parts = new List<string>();
            foreach (var (text, tone) in items)
            {
                string p = Palette(tone);
                parts.Add($"{text}: bg-{p}-100 text-{p}-800 dark:bg-{p}-950 dark:text-{p}-400");
            }
            parts.Add("*: bg-surface-200 text-surface-700 dark:bg-surface-700 dark:text-surface-200");
            return string.Join("; ", parts);
        }

        /// <summary>Panellerle kurulan tablo satırı (renkli çubuklu, az satırlı tablolar için): alt çizgili bir satır.</summary>
        public TwPanel TableRow(int height, params Control[] cells) =>
            Row($"gap-3 h-[{height}px] flex-none border-b border-surface-200 dark:border-surface-700", cells);
    }

    /// <summary>Gösterge anahtarı: kare ('s'), çizgi ('l') ya da kesikli çizgi ('d').</summary>
    internal sealed class LegendKey : Control
    {
        public LegendKey()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public Color Color { get; set; }

        public char Kind { get; set; } = 's';

        protected override void OnPaint(PaintEventArgs e)
        {
            float s = DeviceDpi / 96f;
            if (Kind == 's')
            {
                using var brush = new SolidBrush(Color);
                e.Graphics.FillRectangle(brush, 0, 0, Width, Height);
                return;
            }
            using var pen = new Pen(Color, 2 * s);
            if (Kind == 'd') pen.DashPattern = new[] { 2f, 1.5f };
            e.Graphics.DrawLine(pen, 0, Height / 2f, Width, Height / 2f);
        }
    }

    /// <summary>
    /// İki satırlı hücre: değer "Başlık\nAlt satır" biçimindedir; ilk satır kalın, ikincisi soluk çizilir (makine ve istasyon,
    /// kişi ve birim gibi).
    /// </summary>
    internal sealed class TwoLineCell : DataGridViewTextBoxCell
    {
        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex, DataGridViewElementStates cellState,
            object value, object formattedValue, string errorText, DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle,
            DataGridViewPaintParts paintParts)
        {
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle, advancedBorderStyle,
                paintParts & ~DataGridViewPaintParts.ContentForeground);
            if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0) return;
            var text = formattedValue as string ?? "";
            int split = text.IndexOf('\n');
            string first = split < 0 ? text : text.Substring(0, split), second = split < 0 ? "" : text.Substring(split + 1);
            bool selected = (cellState & DataGridViewElementStates.Selected) != 0;
            var fore = selected ? cellStyle.SelectionForeColor : cellStyle.ForeColor;
            var back = selected ? cellStyle.SelectionBackColor : cellStyle.BackColor;
            var muted = Color.FromArgb((fore.R * 3 + back.R * 2) / 5, (fore.G * 3 + back.G * 2) / 5, (fore.B * 3 + back.B * 2) / 5);
            var font = cellStyle.Font ?? DataGridView.Font;
            using var bold = new Font(font, FontStyle.Bold);
            using var small = new Font(font.FontFamily, font.SizeInPoints * 0.88f, GraphicsUnit.Point);
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            int x = cellBounds.X + Math.Max(8, cellStyle.Padding.Left);
            int width = cellBounds.Right - x - 4;
            int h1 = TextRenderer.MeasureText(graphics, "Ag", bold, Size.Empty, flags).Height;
            int h2 = second.Length == 0 ? 0 : TextRenderer.MeasureText(graphics, "Ag", small, Size.Empty, flags).Height;
            int y = cellBounds.Y + (cellBounds.Height - h1 - h2) / 2;
            TextRenderer.DrawText(graphics, first, bold, new Rectangle(x, y, width, h1), fore, flags);
            if (h2 > 0) TextRenderer.DrawText(graphics, second, small, new Rectangle(x, y + h1, width, h2), muted, flags);
        }
    }

    internal sealed class TwoLineColumn : DataGridViewColumn
    {
        public TwoLineColumn() : base(new TwoLineCell())
        {
        }
    }

    /// <summary>Yatay, bölümlü bir çubuk: değerler orantılı genişlikte yan yana (vardiyanın duruş kırılımı).</summary>
    internal sealed class SegmentBar : Control
    {
        public SegmentBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public (double Value, Color Color)[] Parts { get; set; } = Array.Empty<(double, Color)>();

        /// <summary>Tam genişliğin karşılık geldiği toplam (en büyük vardiya); kısa çubuklar sağda boş kalır.</summary>
        public double Total { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            double total = 0;
            foreach (var part in Parts) total += part.Value;
            double scale = Total > 0 ? Total : total;
            if (scale <= 0) return;
            float gap = 2 * DeviceDpi / 96f, x = 0;
            foreach (var part in Parts)
            {
                float w = (float)(part.Value / scale * Width) - gap;
                if (w <= 0) continue;
                using var brush = new SolidBrush(part.Color);
                e.Graphics.FillRectangle(brush, x, 0, w, Height);
                x += w + gap;
            }
        }
    }
}
