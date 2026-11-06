using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// 0.1.6 kullanım raporu (BakimRapor): DPI test API'si, başlatma sırası ve sayfa iskeleti bileşenleri.
[Collection(UiCollection.Name)]
public class Report017Tests
{
    private static Form NewForm(int width = 800, int height = 500) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(width, height) };

    // ---- G1 TwDpi.Simulate ----

    [Fact]
    public void Simulate_scales_class_sizes_and_text_fonts_and_restores_nested_scopes() => Sta.Run(() =>
    {
        Assert.Null(TwDpi.SimulatedDpi);
        float Measure(out int height)
        {
            using var form = NewForm();
            using var tw = new TailwindProvider();
            var label = new Label { AutoSize = false, Text = "Ölçü" };
            form.Controls.Add(label);
            tw.SetClasses(label, "text-[13px] h-10");
            height = label.Height;
            return label.Font.SizeInPoints;
        }

        float real = Measure(out int realHeight);
        float scaled;
        int scaledHeight;
        using (TwDpi.Simulate(TwDpi.SystemDpi * 5 / 4))
        {
            Assert.Equal(TwDpi.SystemDpi * 5 / 4, TwDpi.SimulatedDpi);
            using (TwDpi.Simulate(144)) Assert.Equal(144, TwDpi.SimulatedDpi);
            Assert.Equal(TwDpi.SystemDpi * 5 / 4, TwDpi.SimulatedDpi);
            scaled = Measure(out scaledHeight);
        }
        Assert.Null(TwDpi.SimulatedDpi);
        Assert.Equal(real * 1.25f, scaled, 1);
        Assert.Equal((int)Math.Round(40 * TwDpi.SystemDpi * 1.25 / 96), scaledHeight);
        Assert.True(scaledHeight > realHeight);
    });

    [Fact]
    public void Simulate_rejects_unreasonable_dpi_and_double_dispose_is_harmless()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TwDpi.Simulate(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TwDpi.Simulate(10_000));
        var scope = TwDpi.Simulate(120);
        scope.Dispose();
        using (TwDpi.Simulate(144))
        {
            scope.Dispose();                        // eski kapsam yeniden kapatılınca etkin olanı bozmaz
            Assert.Equal(144, TwDpi.SimulatedDpi);
        }
        Assert.Null(TwDpi.SimulatedDpi);
    }

    // ---- G3 TwHeading: iki satırlı başlık, araç çubuğunda Shrink ----

    [Fact]
    public void Heading_measures_two_lines_and_shrinks_in_a_toolbar() => Sta.Run(() =>
    {
        using var form = NewForm(1200);
        using var tw = new TailwindProvider();
        var toolbar = new TwToolbar { Dock = DockStyle.Top, Height = 52 };
        var title = new TwHeading { Text = "Kayıt Arşivi", Subtitle = "Tümü · 740 kayıt" };
        toolbar.Controls.Add(title);
        for (int i = 0; i < 4; i++) toolbar.Controls.Add(new TwButton { Text = "Komut " + i, AutoSize = true });
        toolbar.SetOverflow(title, TwToolbarOverflow.Shrink);
        form.Controls.Add(toolbar);
        form.Show();
        tw.SetClasses(title, "text-[15px] font-semibold hint:text-xs hint:text-red-500");
        Application.DoEvents();

        var one = new TwHeading { Text = "Kayıt Arşivi", Font = title.Font };
        Assert.True(title.GetPreferredSize(Size.Empty).Height > one.GetPreferredSize(Size.Empty).Height);
        Assert.Equal(ColorTranslator.FromHtml("#ef4444").ToArgb(), title.EffectiveSubtitleColor.ToArgb());
        Assert.True(title.EffectiveSubtitleFont.SizeInPoints < title.Font.SizeInPoints);
        Assert.Equal(title.GetPreferredSize(Size.Empty).Width, title.Width);
        Assert.Equal("Tümü · 740 kayıt", title.AccessibilityObject.Description);

        // Uzun alt başlık genişliği büyütür; dar çubukta başlık daralır, düğmeler çubukta kalır.
        title.Subtitle = "Tümü · 740 kayıt · son güncelleme 12:04 · filtre: hat 2, vardiya B";
        Application.DoEvents();
        Assert.Equal(title.GetPreferredSize(Size.Empty).Width, title.Width);
        form.ClientSize = new Size(560, 400);
        Application.DoEvents();
        Assert.True(title.Width < title.GetPreferredSize(Size.Empty).Width);
        using (var picture = new Bitmap(title.Width, title.Height)) title.DrawToBitmap(picture, new Rectangle(Point.Empty, picture.Size));
    });

    // ---- E2 TwPageHeader ----

    [Fact]
    public void Page_header_puts_items_right_of_the_title_and_under_it_when_narrow() => Sta.Run(() =>
    {
        using var form = NewForm(900);
        using var tw = new TailwindProvider { ContainerControl = form, AutoStyle = true };
        var header = new TwPageHeader { Title = "Kayıt Arşivi", Subtitle = "Tümü · 739 kayıt", Dock = DockStyle.Top };
        var chip = new TwChip { Text = "Son 30 gün" };
        var summary = new Label { AutoSize = true, Text = "739 kayıt" };
        header.Controls.Add(chip);
        header.Controls.Add(summary);
        form.Controls.Add(header);
        form.Show();
        Application.DoEvents();

        Assert.Equal(new Control[] { chip, summary }, header.Items);
        Assert.False(header.IsStacked);
        Assert.True(chip.Left > header.Heading.Right);
        Assert.True(summary.Left > chip.Right);
        Assert.Equal(header.Width - header.Padding.Right, summary.Right);
        Assert.True(header.Heading.Font.SizeInPoints > form.Font.SizeInPoints);       // page-header: label:text-2xl
        int wide = header.Height;

        form.ClientSize = new Size(260, 400);
        Application.DoEvents();
        Assert.True(header.IsStacked);
        Assert.True(chip.Top >= header.Heading.Bottom);
        Assert.Equal(header.Heading.Left, chip.Left);
        Assert.True(header.Height > wide);

        // Kaldırılan öğe yerleşimden çıkar.
        header.Controls.Remove(summary);
        Assert.Equal(new Control[] { chip }, header.Items);
    });

    // ---- E1 TwCard ----

    [Fact]
    public void Card_has_a_header_only_when_it_has_content_and_the_body_sits_under_it() => Sta.Run(() =>
    {
        using var form = NewForm();
        using var tw = new TailwindProvider { ContainerControl = form, AutoStyle = true };
        var card = new TwCard { Dock = DockStyle.Fill };
        var body = new Panel { Dock = DockStyle.Fill };
        card.Controls.Add(body);
        form.Controls.Add(card);
        form.Show();
        Application.DoEvents();

        Assert.False(card.Header.Visible);
        Assert.Equal(card.DisplayRectangle.Top, body.Top);
        Assert.Equal("card", tw.GetAutomaticClasses(card));

        card.Title = "Duruş nedenleri";
        card.Description = "Son 7 gün";
        var action = new TwButton { Text = "Tümü", AutoSize = true };
        card.Header.Controls.Add(action);
        Application.DoEvents();
        Assert.True(card.Header.Visible);
        Assert.True(body.Top >= card.Header.Bottom);
        Assert.True(action.Right <= card.Header.Width && action.Left > card.Header.Heading.Left);
        Assert.Null(tw.GetAutomaticClasses(card.Header));                            // sayfa başlığının dolgusu karta gelmez
        Assert.True(card.Header.Heading.Font.Bold || card.Header.Heading.Font.FontFamily.Name.Contains("Semibold"));

        // Kartın label:/hint: parçaları başlığa geçer.
        tw.SetClasses(card, "card label:text-[18px] hint:text-green-600");
        Application.DoEvents();
        Assert.Equal(ColorTranslator.FromHtml("#16a34a").ToArgb(), card.Header.Heading.EffectiveSubtitleColor.ToArgb());
        Assert.Equal(18 * 0.75f * TwDpi.FontScale(card), card.Header.Heading.Font.SizeInPoints / TwTextScale.Factor, 1);

        card.Title = card.Description = "";
        card.Header.Controls.Remove(action);
        Assert.False(card.Header.Visible);
    });

    // ---- G2 TwPopover.ShowOnHover ----

    [Fact]
    public void Popover_opens_after_hover_delay_and_closes_after_the_pointer_leaves() => Sta.Run(() =>
    {
        using var form = NewForm();
        form.Location = new Point(40, 40);
        form.StartPosition = FormStartPosition.Manual;
        var icon = new TwIcon { IconName = "info", Location = new Point(20, 20), Size = new Size(24, 24) };
        form.Controls.Add(icon);
        using var popover = new TwPopover { Content = new Label { Text = "Tanım", Size = new Size(160, 60) } };
        popover.ShowOnHover(icon, 300);
        form.Show();
        Application.DoEvents();

        var t0 = DateTime.UtcNow;
        var inside = icon.PointToScreen(new Point(5, 5));
        popover.BeginHover(icon, t0);
        popover.HoverTick(inside, t0.AddMilliseconds(100));
        Assert.False(popover.IsOpen);
        popover.HoverTick(inside, t0.AddMilliseconds(320));
        Assert.True(popover.IsOpenFromHover);
        Assert.False(popover.Content!.ContainsFocus);                                  // hover odak almaz

        // İmleç karta geçer: açık kalır; ikisinin de dışında HoverCloseDelay sonra kapanır.
        var card = popover.DropDown!.Bounds;
        popover.HoverTick(new Point(card.Left + 4, card.Top + 4), t0.AddMilliseconds(400));
        var outside = new Point(card.Right + 200, card.Bottom + 200);
        popover.HoverTick(outside, t0.AddMilliseconds(500));
        popover.HoverTick(outside, t0.AddMilliseconds(700));
        Assert.True(popover.IsOpen);
        popover.HoverTick(outside, t0.AddMilliseconds(820));
        Assert.False(popover.IsOpen);

        // İmleç gecikme dolmadan çıkarsa açılmaz; StopHover sonrası hiç açılmaz.
        popover.BeginHover(icon, t0);
        popover.HoverTick(outside, t0.AddMilliseconds(100));
        popover.HoverTick(inside, t0.AddMilliseconds(400));
        Assert.False(popover.IsOpen);
        popover.StopHover(icon);
        popover.BeginHover(icon, t0);
        popover.HoverTick(inside, t0.AddMilliseconds(400));
        Assert.False(popover.IsOpen);
    });

    // ---- G4 TwPanel.ScrollToFocused ----

    private sealed class ScrollProbe : TwPanel
    {
        public Point Target(Control control) => ScrollToControl(control);
    }

    [Fact]
    public void Panel_does_not_scroll_to_a_child_focused_with_the_mouse() => Sta.Run(() =>
    {
        using var form = NewForm(400, 300);
        var page = new ScrollProbe { Dock = DockStyle.Fill, AutoScroll = true };
        var top = new Button { Location = new Point(10, 10), Size = new Size(80, 24) };
        var list = new ListBox { Location = new Point(10, 900), Size = new Size(200, 100) };
        page.Controls.Add(top);
        page.Controls.Add(list);
        form.Controls.Add(page);
        form.Show();
        Application.DoEvents();
        page.AutoScrollPosition = Point.Empty;
        Assert.Equal(Point.Empty, page.DisplayRectangle.Location);
        var original = TwPanel.PressedMouseButtons;
        try
        {
            Assert.True(page.ScrollToFocused);
            Assert.NotEqual(page.DisplayRectangle.Location, page.Target(list));

            page.ScrollToFocused = false;
            TwPanel.PressedMouseButtons = () => MouseButtons.Left;
            Assert.Equal(page.DisplayRectangle.Location, page.Target(list));
            TwPanel.PressedMouseButtons = () => MouseButtons.None;                     // klavye (Tab) yine kaydırır
            Assert.NotEqual(page.DisplayRectangle.Location, page.Target(list));
        }
        finally
        {
            TwPanel.PressedMouseButtons = original;
        }
    });

    // ---- G5 TwPeriodPicker.DateStyle ----

    [Fact]
    public void Period_text_uses_month_names_and_the_chip_follows_without_a_filters_changed() => Sta.Run(() =>
    {
        var culture = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
        try
        {
            Assert.Equal("1 – 31 Eki 2026", TwPeriodPicker.AbbreviatedRangeText(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), System.Globalization.CultureInfo.CurrentCulture));
            Assert.Equal("28 Eki – 3 Kas 2026", TwPeriodPicker.AbbreviatedRangeText(new DateTime(2026, 10, 28), new DateTime(2026, 11, 3), System.Globalization.CultureInfo.CurrentCulture));
            Assert.Equal("28 Ara 2025 – 3 Oca 2026", TwPeriodPicker.AbbreviatedRangeText(new DateTime(2025, 12, 28), new DateTime(2026, 1, 3), System.Globalization.CultureInfo.CurrentCulture));
            Assert.Equal("5 Eki 2026", TwPeriodPicker.AbbreviatedRangeText(new DateTime(2026, 10, 5), new DateTime(2026, 10, 5), System.Globalization.CultureInfo.CurrentCulture));

            using var form = NewForm();
            var bar = new TwFilterBar { Dock = DockStyle.Top };
            var filter = new TwFilter("date", "Tarih", TwFilterKind.Period);
            bar.Filters.Add(filter);
            form.Controls.Add(bar);
            form.Show();
            bar.SetPeriod("date", new DateTime(2026, 10, 1), new DateTime(2026, 10, 31));
            int changed = 0, periods = 0;
            bar.FiltersChanged += (_, _) => changed++;
            filter.Period!.PeriodChanged += (_, _) => periods++;

            filter.Period.DateStyle = TwPeriodDateStyle.Abbreviated;
            Assert.Equal("Tarih: 1 – 31 Eki 2026", bar.ChipOf("date")!.Text);
            filter.Period.DateFormat = "dd.MM";
            Assert.Equal("Tarih: 01.10 – 31.10", bar.ChipOf("date")!.Text);
            Assert.Equal("01.10 – 31.10", filter.Period.FormatRange(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)));
            Assert.Equal(0, changed);
            Assert.Equal(0, periods);
            Assert.Equal((new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)), bar.GetPeriod("date")!.Value);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
        }
    });

    // ---- E3 TwFormHost ----

    private sealed class LegacyForm : Form
    {
        public readonly Button Ok = new Button { Text = "Tamam", Location = new Point(10, 10), Size = new Size(100, 30) };
        public readonly Button Cancel = new Button { Text = "Vazgeç", Location = new Point(120, 10), Size = new Size(100, 30) };
        public int Loaded;

        public LegacyForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "Eski kayıtlar";
            ClientSize = new Size(400, 300);
            Controls.Add(Ok);
            Controls.Add(Cancel);
            AcceptButton = Ok;
            CancelButton = Cancel;
            Load += (_, _) => Loaded++;
        }
    }

    [Fact]
    public void Form_host_shows_a_legacy_form_as_a_page_scaled_with_its_title_moved_out() => Sta.Run(() =>
    {
        using var window = NewForm();
        var title = new TwHeading();
        var host = new TwFormHost { Dock = DockStyle.Fill, TitleTarget = title };
        window.Controls.Add(host);
        window.Controls.Add(title);
        window.Show();

        var legacy = new LegacyForm();
        int titles = 0;
        host.TitleChanged += (_, _) => titles++;
        using (TwDpi.Simulate(144)) host.Form = legacy;
        Application.DoEvents();

        Assert.False(legacy.TopLevel);
        Assert.Same(host, legacy.Parent);
        Assert.Equal(FormBorderStyle.None, legacy.FormBorderStyle);
        Assert.Equal(DockStyle.Fill, legacy.Dock);
        Assert.Equal(1, legacy.Loaded);
        Assert.True(host.IsScaled);
        Assert.Equal(150, legacy.Ok.Width);                                            // 100 × 1,5
        Assert.Equal("Eski kayıtlar", host.Title);
        Assert.Equal("Eski kayıtlar", title.Text);

        legacy.Text = "Eski kayıtlar (12)";
        Assert.Equal("Eski kayıtlar (12)", title.Text);
        Assert.True(titles >= 2);

        // Form kendini kapatınca sayfadan çıkar.
        FormClosedEventArgs? closed = null;
        host.FormClosed += (_, e) => closed = e;
        legacy.Close();
        Application.DoEvents();
        Assert.NotNull(closed);
        Assert.Null(host.Form);
        Assert.Equal("", title.Text);
        Assert.DoesNotContain(legacy, host.Controls.Cast<Control>());
    });

    [Fact]
    public void Form_host_keeps_an_auto_scaled_form_as_it_is() => Sta.Run(() =>
    {
        using var window = NewForm();
        var host = new TwFormHost { Dock = DockStyle.Fill };
        window.Controls.Add(host);
        window.Show();
        using var legacy = new LegacyForm { AutoScaleMode = AutoScaleMode.Font };
        using (TwDpi.Simulate(144)) host.Form = legacy;
        Assert.False(host.IsScaled);
        Assert.Equal(100, legacy.Ok.Width);
        host.Form = null;
        Assert.Null(legacy.Parent);
        Assert.False(legacy.IsDisposed);
    });

    // ---- E4 duyarlı gösterge ızgarası (belgedeki örnek) ----

    [Fact]
    public void Stat_grid_example_puts_four_cards_in_a_row_when_wide_and_two_when_narrow() => Sta.Run(() =>
    {
        using var form = NewForm(1100, 500);
        using var tw = new TailwindProvider();
        var grid = new TwPanel { Dock = DockStyle.Top, Height = 260 };
        var stats = Enumerable.Range(0, 4).Select(i => new TwStat { Title = "Gösterge " + i, Value = (i * 10).ToString() }).ToArray();
        foreach (var stat in stats) grid.Controls.Add(stat);
        form.Controls.Add(grid);
        tw.SetClasses(grid, "grid grid-cols-2 lg:grid-cols-4 gap-4");
        foreach (var stat in stats) tw.SetClasses(stat, "stat");
        form.Show();
        Application.DoEvents();
        Assert.Single(stats.Select(s => s.Top).Distinct());

        form.ClientSize = new Size(700, 500);
        Application.DoEvents();
        Application.DoEvents();
        Assert.Equal(2, stats.Select(s => s.Top).Distinct().Count());
        Assert.Equal(stats[0].Top, stats[1].Top);
        Assert.True(stats[2].Top > stats[0].Top);
    });

    // ---- E5 bookmark ve factory simgeleri ----

    [Theory]
    [InlineData("bookmark")]
    [InlineData("bookmark-filled")]
    [InlineData("factory")]
    public void Built_in_svg_icons_are_known_and_draw(string name)
    {
        Assert.True(TwIcons.IsKnown(name));
        Assert.Contains(name, TwIcons.Names);
        using var bitmap = new Bitmap(24, 24);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            Assert.True(TwIconRenderer.DrawIcon(g, name, 20, new Rectangle(0, 0, 24, 24), Color.Black));
        }
        int dark = 0;
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
                if (bitmap.GetPixel(x, y).R < 128) dark++;
        Assert.True(dark > 10, $"{name}: {dark}");
    }

    [Fact]
    public void A_theme_glyph_with_a_built_in_svg_name_wins()
    {
        var theme = new TwTheme();
        theme.SetIcon("factory", '');
        Assert.False(theme.TryGetSvgIcon("factory", out _));
        Assert.True(new TwTheme().TryGetSvgIcon("factory", out _));
    }

#if NETFRAMEWORK
    // ---- H2 Main'den önce (statik alanda) font kaydı ----

    // WinForms'un "ilk pencere" durumu AppDomain'e özeldir: yeni bir alanda statik alan başlatıcısı FormWind'i Main'den önce
    // başlatır, sonra uygulamanın Main'i SetCompatibleTextRenderingDefault çağırır.
    [Fact]
    public void Registering_fonts_from_a_static_field_before_main_does_not_break_text_rendering_default()
    {
        var setup = new AppDomainSetup { ApplicationBase = AppDomain.CurrentDomain.BaseDirectory };
        var domain = AppDomain.CreateDomain("FormWind.StartupProbe", null, setup);
        try
        {
            var probe = (StartupProbe)domain.CreateInstanceAndUnwrap(typeof(StartupProbe).Assembly.FullName, typeof(StartupProbe).FullName);
            Assert.Equal("ok", probe.Run(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", "Outfit-Regular.ttf")));
        }
        finally
        {
            AppDomain.Unload(domain);
        }
    }

    public sealed class StartupProbe : MarshalByRefObject
    {
        private static string? _path;

        private static class EarlyFonts
        {
            // Uygulamalarda: static readonly ... = TwFonts.Register(...) ve TwTheme.Load(...) Program sınıfının alanlarında.
            public static readonly object Names = TwFonts.Register(_path!);
            public static readonly TwTheme Theme = TwTheme.Current;

            // beforefieldinit olmasın: alanlar Run JIT'lenirken değil, ilk erişimde (_path atandıktan sonra) başlatılır.
            static EarlyFonts() { }
        }

        public string Run(string fontPath)
        {
            _path = fontPath;
            try
            {
                _ = EarlyFonts.Names;
                _ = EarlyFonts.Theme;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                return "ok";
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }
    }
#endif
}
