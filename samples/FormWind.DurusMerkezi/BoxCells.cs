using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DurusMerkezi
{
    /// <summary>
    /// Tablo içinde giriş kutusu gibi görünen hücrelerin ortak çizimi: hücrenin içinde yuvarlak köşeli, kenarlıklı bir kutu,
    /// boşsa soluk bir yer tutucu; zorunlu ve boş olan kutunun kenarı kırmızıdır. Renkler hücre zemininden türetilir, böylece
    /// açık ve koyu temada tablo ile uyumlu kalır.
    /// </summary>
    internal static class BoxPainter
    {
        public static readonly Color Danger = ColorTranslator.FromHtml("#ff453a");
        public static readonly Color Accent = ColorTranslator.FromHtml("#0a84ff");

        public static bool IsDark(Color back) => back.GetBrightness() < 0.4f;

        public static Rectangle Box(Rectangle cell, float scale) =>
            Rectangle.Inflate(new Rectangle(cell.X + (int)(4 * scale), cell.Y, cell.Width - (int)(10 * scale), cell.Height), 0, -(int)(6 * scale));

        public static void Paint(Graphics g, Rectangle cell, DataGridViewCellStyle style, string text, string placeholder, bool invalid, bool combo, float scale)
        {
            bool dark = IsDark(style.BackColor);
            var box = Box(cell, scale);
            var fill = dark ? ColorTranslator.FromHtml("#151517") : Color.White;
            var border = invalid ? Danger : dark ? ColorTranslator.FromHtml("#38383b") : ColorTranslator.FromHtml("#d4d4d8");
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Rounded(box, 4 * scale))
            {
                using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
                using (var pen = new Pen(border, Math.Max(1f, scale))) g.DrawPath(pen, path);
            }
            int right = box.Right;
            if (combo)
            {
                // macOS açılır menüsü gibi: mavi bir kare içinde yukarı ve aşağı ok.
                int size = Math.Min(box.Height - (int)(8 * scale), (int)(20 * scale));
                var square = new Rectangle(box.Right - size - (int)(4 * scale), box.Y + (box.Height - size) / 2, size, size);
                using (var path = Rounded(square, 3 * scale))
                using (var brush = new SolidBrush(Accent)) g.FillPath(brush, path);
                using var pen = new Pen(Color.White, 1.4f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                float cx = square.X + square.Width / 2f, cy = square.Y + square.Height / 2f, w = size * 0.2f, h = size * 0.12f, gap = size * 0.1f;
                g.DrawLines(pen, new[] { new PointF(cx - w, cy - gap), new PointF(cx, cy - gap - h * 1.6f), new PointF(cx + w, cy - gap) });
                g.DrawLines(pen, new[] { new PointF(cx - w, cy + gap), new PointF(cx, cy + gap + h * 1.6f), new PointF(cx + w, cy + gap) });
                right = square.X - (int)(4 * scale);
            }
            g.Restore(state);

            bool empty = string.IsNullOrEmpty(text);
            var fore = empty ? (dark ? ColorTranslator.FromHtml("#6e6e73") : ColorTranslator.FromHtml("#a1a1aa")) : style.ForeColor;
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;
            var area = Rectangle.FromLTRB(box.X + (int)(8 * scale), box.Y, right - (int)(4 * scale), box.Bottom);
            TextRenderer.DrawText(g, empty ? placeholder : text, style.Font, area, fore, flags);
        }

        private static GraphicsPath Rounded(Rectangle r, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>Metin girişi görünümlü hücre (Alt kategori, Kök neden, Parça).</summary>
    internal sealed class BoxTextCell : DataGridViewTextBoxCell
    {
        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex, DataGridViewElementStates cellState,
            object value, object formattedValue, string errorText, DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle,
            DataGridViewPaintParts paintParts)
        {
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle, advancedBorderStyle,
                paintParts & ~(DataGridViewPaintParts.ContentForeground | DataGridViewPaintParts.Focus | DataGridViewPaintParts.SelectionBackground));
            var column = OwningColumn as BoxTextColumn;
            var text = formattedValue as string ?? "";
            BoxPainter.Paint(graphics, cellBounds, cellStyle, text, column?.Placeholder ?? "", column?.Required == true && text.Length == 0, false,
                DataGridView.DeviceDpi / 96f);
        }
    }

    internal sealed class BoxTextColumn : DataGridViewColumn
    {
        public BoxTextColumn() : base(new BoxTextCell())
        {
        }

        /// <summary>Boş kutuda gösterilen soluk metin.</summary>
        public string Placeholder { get; set; } = "";

        /// <summary>Boşsa kutunun kenarı kırmızı çizilir.</summary>
        public bool Required { get; set; }

        public override object Clone()
        {
            var clone = (BoxTextColumn)base.Clone();
            clone.Placeholder = Placeholder;
            clone.Required = Required;
            return clone;
        }
    }

    /// <summary>Açılır seçim görünümlü hücre (Kategori); düzenlenirken yerel açılır liste kullanılır.</summary>
    internal sealed class BoxComboCell : DataGridViewComboBoxCell
    {
        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex, DataGridViewElementStates cellState,
            object value, object formattedValue, string errorText, DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle,
            DataGridViewPaintParts paintParts)
        {
            // Yalnızca zemin ve kenarlık yerel olarak çizilir; kutu, ok ve metin bizden.
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle, advancedBorderStyle,
                paintParts & (DataGridViewPaintParts.Background | DataGridViewPaintParts.Border));
            var column = OwningColumn as BoxComboColumn;
            var text = formattedValue as string ?? "";
            bool empty = text.Length == 0 || text == column?.Placeholder;
            // Tasarımdaki gibi "Seçin…" normal renkte kalır; seçimsiz zorunlu kutunun yalnızca kenarı kırmızıdır.
            BoxPainter.Paint(graphics, cellBounds, cellStyle, text, column?.Placeholder ?? "", empty && column?.Required == true, true,
                DataGridView.DeviceDpi / 96f);
        }
    }

    internal sealed class BoxComboColumn : DataGridViewComboBoxColumn
    {
        public BoxComboColumn()
        {
            CellTemplate = new BoxComboCell();
            FlatStyle = FlatStyle.Flat;
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing;
        }

        /// <summary>Seçim yapılmamış değer (ilk öğe); kutuda soluk gösterilir.</summary>
        public string Placeholder { get; set; } = "";

        public bool Required { get; set; }

        public override object Clone()
        {
            var clone = (BoxComboColumn)base.Clone();
            clone.Placeholder = Placeholder;
            clone.Required = Required;
            return clone;
        }
    }
}
