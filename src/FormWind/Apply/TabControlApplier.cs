using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Styles the tab headers of a standard TabControl, which ignores its colors: when the classes give colors, FormWind
/// paints the tab strip after Windows does. The draw mode is not changed (changing it recreates the handles of every
/// control on the pages); a TabControl the application owner-draws itself is left alone.
/// <list type="bullet">
/// <item><description>base classes: <c>bg-*</c> and <c>text-*</c> of the tabs</description></item>
/// <item><description><c>selected:</c> the selected tab (<c>selected:bg-*</c>, <c>selected:text-*</c>; <c>selected:border-*</c>
/// draws a 2 px bar on its outer edge)</description></item>
/// </list>
/// The tabs are drawn as flat blocks; the empty part of the strip shows the parent's background and the frame around the
/// pages the selected page's. Tab pages are styled as controls of their own. Removing the colors restores Windows'
/// drawing.
/// </summary>
internal sealed class TabControlApplier : WinFormsApplier
{
    private static readonly ConditionalWeakTable<TabControl, Painter> Painters = new ConditionalWeakTable<TabControl, Painter>();

    public override bool CanApply(Control control) => control is TabControl;

    /// <summary>Whether FormWind draws the tabs (not reported by <see cref="TwContrast.FindOwnerDrawn"/>).</summary>
    internal static bool IsPainted(TabControl tabs) => Painters.TryGetValue(tabs, out var painter) && painter.Active;

    /// <summary>Paints FormWind's tab strip into <paramref name="graphics"/> (tests; the window paints after Windows).</summary>
    internal static void PaintTo(TabControl tabs, Graphics graphics)
    {
        if (Painters.TryGetValue(tabs, out var painter) && painter.Active) painter.PaintChrome(graphics);
    }

    public override void Apply(Control control, TwStyle style, TwApplyContext context)
    {
        var tabs = (TabControl)control;
        // TabControl zemini çizmez; bg-* sekmelerindir. Kontrolün kendi BackColor'ı (sayfaların miras aldığı) değişmez.
        var colors = new TwStyle();
        colors.MergeFrom(style);
        colors.BackColor = null;
        base.Apply(control, colors, context);

        var state = context.State & ~(TwState.Hover | TwState.Active);
        var selected = context.Parsed.ResolvePart("selected", state);
        bool active = !context.DesignMode && tabs.DrawMode == TabDrawMode.Normal
            && (style.BackColor.HasValue || style.ForeColor.HasValue || selected.BackColor.HasValue || selected.ForeColor.HasValue);

        if (!Painters.TryGetValue(tabs, out var painter))
        {
            if (!active) return;
            painter = Painters.GetValue(tabs, t => new Painter(t));
        }
        painter.Back = style.BackColor;
        painter.Fore = style.ForeColor;
        painter.SelectedBack = selected.BackColor;
        painter.SelectedFore = selected.ForeColor;
        painter.Accent = selected.BorderColor;
        painter.SetActive(active);
        if (active) tabs.Invalidate();
    }

    // Pencere alt sınıflanır: Windows'un (temalı) çizimi bittikten sonra şerit ebeveynin, çerçeve seçili sayfanın zeminiyle
    // doldurulur ve sekmeler düz bloklar olarak yeniden çizilir. OwnerDrawFixed kullanılmaz: DrawMode değişimi handle'ı
    // ve sayfalardaki bütün kontrollerin handle'larını yeniden oluşturur (ShareX'in ayarlar formunda ~300 ms), şeridin
    // boş kısmını da zaten DrawItem'a bırakmaz.
    private sealed class Painter : NativeWindow
    {
        private const int WmPaint = 0x000F;
        private readonly TabControl _tabs;

        public Painter(TabControl tabs)
        {
            _tabs = tabs;
            tabs.HandleCreated += (_, _) => { if (Active) Attach(); };
            tabs.HandleDestroyed += (_, _) => ReleaseHandle();
        }

        public bool Active { get; private set; }
        public Color? Back, Fore, SelectedBack, SelectedFore, Accent;

        public void SetActive(bool active)
        {
            if (Active == active) return;
            Active = active;
            if (active)
            {
                Attach();
            }
            else
            {
                ReleaseHandle();
                _tabs.Invalidate();
            }
        }

        private void Attach()
        {
            if (_tabs.IsHandleCreated && Handle != _tabs.Handle)
            {
                if (Handle != System.IntPtr.Zero) ReleaseHandle();
                AssignHandle(_tabs.Handle);
            }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != WmPaint || !Active || _tabs.TabCount == 0) return;
            try
            {
                using var graphics = Graphics.FromHwnd(_tabs.Handle);
                PaintChrome(graphics);
            }
            catch (System.Exception ex) when (!TwErrors.IsCritical(ex))
            {
                TwErrors.Trace("tab strip", ex);
            }
        }

        public void PaintChrome(Graphics graphics)
        {
            var client = _tabs.ClientRectangle;
            var display = _tabs.DisplayRectangle;
            var strip = Strip(client, display);
            using (var region = new Region(client))
            {
                region.Exclude(display);
                region.Exclude(strip);
                using var frame = new SolidBrush(_tabs.SelectedTab?.BackColor ?? Under);
                graphics.FillRegion(frame, region);
            }
            using (var brush = new SolidBrush(Under))
                graphics.FillRectangle(brush, strip);
            for (int i = 0; i < _tabs.TabCount; i++)
                if (i != _tabs.SelectedIndex) DrawTab(graphics, i, _tabs.GetTabRect(i));
            if (_tabs.SelectedIndex >= 0)
            {
                var selected = _tabs.GetTabRect(_tabs.SelectedIndex);
                DrawTab(graphics, _tabs.SelectedIndex, selected);
                // Windows'un odak çerçevesi şeritle birlikte örtülür; klavye ipuçları açıkken yeniden çizilir.
                if (_tabs.Focused && TwFocusCues.Shown(_tabs))
                    ControlPaint.DrawFocusRectangle(graphics, Rectangle.Inflate(selected, -3, -3), SelectedFore ?? Fore ?? _tabs.ForeColor, SelectedBack ?? Back ?? Under);
            }
        }

        // Sekmelerin bulunduğu şerit: hizalamaya göre sayfa alanının dışındaki bant.
        private Rectangle Strip(Rectangle client, Rectangle display) => _tabs.Alignment switch
        {
            TabAlignment.Bottom => Rectangle.FromLTRB(client.Left, display.Bottom + 2, client.Right, client.Bottom),
            TabAlignment.Left => Rectangle.FromLTRB(client.Left, client.Top, display.Left - 2, client.Bottom),
            TabAlignment.Right => Rectangle.FromLTRB(display.Right + 2, client.Top, client.Right, client.Bottom),
            _ => Rectangle.FromLTRB(client.Left, client.Top, client.Right, display.Top - 2),
        };

        private Color Under => _tabs.Parent?.BackColor ?? SystemColors.Control;

        private void DrawTab(Graphics graphics, int index, Rectangle tab)
        {
            var page = _tabs.TabPages[index];
            bool selected = index == _tabs.SelectedIndex;
            var under = Under;
            var back = (selected ? SelectedBack ?? Back : Back) ?? under;
            var fore = (selected ? SelectedFore ?? Fore : Fore) ?? _tabs.ForeColor;
            if (back.A < 255) back = Composite(back, under);

            // Seçili sekme sayfayla birleşir; diğerleri arasında bir piksellik şerit kalır.
            var bounds = selected ? Join(tab) : Rectangle.Inflate(tab, -1, -1);
            using (var brush = new SolidBrush(back))
                graphics.FillRectangle(brush, bounds);

            // Windows'un yerleşimi: ikon, Padding.X kadar boşluk ve yazı birlikte sekmeye ortalanır; yazı iç boşluksuz
            // çizilir. TextRenderer'ın varsayılan boşluğu Windows'un sığdırdığı yazıyı yüksek DPI'da "…" ile kısaltıyordu.
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
                | TextFormatFlags.EndEllipsis | TextFormatFlags.HidePrefix;
            // RTL'de de (aynalanmamış pencerede Windows sırayı ve ikonun yerini değiştirmez) ikon yazının solundadır.
            if (_tabs.RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
            var picture = _tabs.ImageList is ImageList images && ImageIndex(page, images) is int image ? images.Images[image] : null;
            int gap = picture is null ? 0 : picture.Width + _tabs.Padding.X;
            int content = gap + TextRenderer.MeasureText(graphics, page.Text, _tabs.Font, new Size(int.MaxValue, int.MaxValue), flags & ~TextFormatFlags.EndEllipsis).Width;
            int start = tab.Left + System.Math.Max(0, (tab.Width - content) / 2);
            if (picture != null)
                graphics.DrawImage(picture, start, tab.Top + (tab.Height - picture.Height) / 2, picture.Width, picture.Height);
            var text = Rectangle.FromLTRB(start + gap, tab.Top, tab.Right, tab.Bottom);
            TextRenderer.DrawText(graphics, page.Text, _tabs.Font, text, fore, flags);

            if (selected && Accent is Color accent)
            {
                int bar = _tabs.LogicalToDeviceUnits(2);
                var line = _tabs.Alignment switch
                {
                    TabAlignment.Bottom => new Rectangle(tab.Left, tab.Bottom - bar, tab.Width, bar),
                    TabAlignment.Left => new Rectangle(tab.Left, tab.Top, bar, tab.Height),
                    TabAlignment.Right => new Rectangle(tab.Right - bar, tab.Top, bar, tab.Height),
                    _ => new Rectangle(tab.Left, tab.Top, tab.Width, bar),
                };
                using var brush = new SolidBrush(accent);
                graphics.FillRectangle(brush, line);
            }
        }

        // Seçili sekme sayfa tarafına uzatılır.
        private Rectangle Join(Rectangle tab)
        {
            var display = _tabs.DisplayRectangle;
            return _tabs.Alignment switch
            {
                TabAlignment.Bottom => Rectangle.FromLTRB(tab.Left, display.Bottom, tab.Right, tab.Bottom),
                TabAlignment.Left => Rectangle.FromLTRB(tab.Left, tab.Top, display.Left, tab.Bottom),
                TabAlignment.Right => Rectangle.FromLTRB(display.Right, tab.Top, tab.Right, tab.Bottom),
                _ => Rectangle.FromLTRB(tab.Left, tab.Top, tab.Right, display.Top),
            };
        }

        private static int? ImageIndex(TabPage page, ImageList images)
        {
            if (page.ImageIndex >= 0 && page.ImageIndex < images.Images.Count) return page.ImageIndex;
            if (page.ImageKey.Length > 0 && images.Images.IndexOfKey(page.ImageKey) is int key and >= 0) return key;
            return null;
        }

        private static Color Composite(Color color, Color under)
        {
            double a = color.A / 255.0;
            return Color.FromArgb(
                (int)(color.R * a + under.R * (1 - a)),
                (int)(color.G * a + under.G * (1 - a)),
                (int)(color.B * a + under.B * (1 - a)));
        }
    }
}
