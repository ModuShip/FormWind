using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

public class ContrastTests
{
    [Fact]
    public void Ratio_matches_wcag_reference_values()
    {
        Assert.Equal(21.0, TwContrast.Ratio(Color.Black, Color.White), 2);
        Assert.Equal(1.0, TwContrast.Ratio(Color.Red, Color.Red), 2);
        // #767676 beyaz üzerinde AA eşiğini tam geçen gri olarak bilinir.
        Assert.Equal(4.54, TwContrast.Ratio(ColorTranslator.FromHtml("#767676"), Color.White), 2);
        Assert.Equal(TwContrast.Ratio(Color.Navy, Color.White), TwContrast.Ratio(Color.White, Color.Navy), 6);
    }

    [Fact]
    public void Audit_reports_unreadable_text_and_skips_disabled_controls()
    {
        using var panel = new Panel { BackColor = Color.White };
        var unreadable = new Label { Name = "unreadable", ForeColor = ColorTranslator.FromHtml("#eeeeee") };
        var disabled = new Label { Name = "disabled", ForeColor = ColorTranslator.FromHtml("#eeeeee"), Enabled = false };
        var readable = new Label { Name = "readable", ForeColor = Color.Black };
        panel.Controls.AddRange(new Control[] { unreadable, disabled, readable });

        var issue = Assert.Single(TwContrast.Audit(panel));

        Assert.Same(unreadable, issue.Control);
        Assert.Equal("text", issue.Part);
        Assert.True(issue.Ratio < TwContrast.TextMinimum);
    }

    [Fact]
    public void Large_text_uses_the_lower_threshold()
    {
        using var panel = new Panel { BackColor = Color.White };
        var gray = ColorTranslator.FromHtml("#949494"); // ~3:1
        panel.Controls.Add(new Label { ForeColor = gray, Font = new Font("Segoe UI", 9f) });
        panel.Controls.Add(new Label { ForeColor = gray, Font = new Font("Segoe UI", 20f) });

        var issue = Assert.Single(TwContrast.Audit(panel));
        Assert.Equal(9f, issue.Control.Font.SizeInPoints, 1);
    }

    [Fact]
    public void Transparent_background_is_composited_over_the_parent()
    {
        using var panel = new Panel { BackColor = Color.Black };
        panel.Controls.Add(new Label { BackColor = Color.Transparent, ForeColor = Color.Black });

        var issue = Assert.Single(TwContrast.Audit(panel));
        Assert.Equal(Color.Black.ToArgb(), issue.Background.ToArgb());
    }

    [Fact]
    public void Audit_checks_toolstrip_hover_colors() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        var derived = new ToolStrip { Name = "derived" };
        var whiteHover = new ToolStrip { Name = "whiteHover" };
        form.Controls.Add(derived);
        form.Controls.Add(whiteHover);
        provider.SetClasses(derived, "bg-gray-800 text-gray-100");
        provider.SetClasses(whiteHover, "bg-gray-800 text-gray-100 item:hover:bg-white");

        var issues = TwContrast.Audit(form);

        Assert.DoesNotContain(issues, i => i.Control == derived);
        Assert.Contains(issues, i => i.Control == whiteHover && i.Part == "item:hover");
    });

    [Fact]
    public void Audit_with_provider_checks_hover_variants() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { BackColor = Color.White };
        var button = new Button { Text = "Kaydet" };
        form.Controls.Add(button);
        provider.SetClasses(button, "bg-white text-gray-900 hover:bg-gray-900");

        Assert.Empty(TwContrast.Audit(form));
        var issue = Assert.Single(TwContrast.Audit(form, provider));
        Assert.Equal("hover", issue.Part);
    });

    [Fact]
    public void Audit_checks_the_final_color_of_a_running_transition() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var button = new Button { Text = "Kaydet" };
        form.Controls.Add(button);
        form.Show();
        provider.SetClasses(button, "transition bg-white text-gray-900 dark:text-gray-100");
        TwAnimator.Step(DateTime.UtcNow.AddSeconds(10));

        provider.DarkMode = true; // yazı rengi geçişte; henüz koyu
        Assert.True(TwAnimator.IsAnimating(button, TwColorChannel.Fore));

        var issue = Assert.Single(TwContrast.Audit(form));
        Assert.Equal(ColorTranslator.FromHtml("#f3f4f6").ToArgb(), issue.Foreground.ToArgb());
        form.Close();
    });

    // Yalnızca yazı rengi veren class'lar uygulamanın kendi zeminini bırakır; denetim nedenini söylemeli.
    [Fact]
    public void Audit_reports_a_kept_application_background_as_the_cause()
    {
        using var provider = new TailwindProvider();
        using var form = new Form { BackColor = ColorTranslator.FromHtml("#272727") };
        var copied = new Label { Name = "copied", Text = "Ad", BackColor = ColorTranslator.FromHtml("#272727") };
        var plain = new Label { Name = "plain", Text = "Soyad", BackColor = ColorTranslator.FromHtml("#272727") };
        form.Controls.Add(copied);
        form.Controls.Add(plain);
        provider.SetClasses(form, "bg-gray-50");
        provider.SetClasses(copied, "text-gray-800");
        provider.SetClasses(plain, "text-gray-800 bg-gray-800");

        var issues = TwContrast.Audit(form, provider);

        var kept = Assert.Single(issues, i => i.Control == copied);
        Assert.Equal(TwContrastCause.KeptBackground, kept.Cause);
        Assert.Contains("add bg-*", kept.ToString());
        Assert.Equal(TwContrastCause.None, Assert.Single(issues, i => i.Control == plain).Cause);
    }

    // İlk uygulamada transition çalışmaz: uygulamanın atadığı renkten başlayan geçiş açılışta renk kaydırırdı. Sonraki
    // değişimler (tema, durum, class) geçişlidir.
    [Fact]
    public void Transition_does_not_run_on_the_first_application() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var button = new Button { Text = "Kaydet", BackColor = ColorTranslator.FromHtml("#2e2e2e") };
        form.Controls.Add(button);
        form.Show();

        provider.SetClasses(button, "transition bg-white dark:bg-gray-900");
        Assert.False(TwAnimator.IsAnimating(button, TwColorChannel.Back));
        Assert.Equal(Color.White.ToArgb(), button.BackColor.ToArgb());

        provider.DarkMode = true;
        Assert.True(TwAnimator.IsAnimating(button, TwColorChannel.Back));
        TwAnimator.Step(DateTime.UtcNow.AddSeconds(10));
        form.Close();
    });

    [Fact]
    public void Include_disabled_checks_disabled_text_boxes_against_the_system_background()
    {
        using var panel = new Panel { BackColor = ColorTranslator.FromHtml("#111827") };
        // İçerik göstermek için devre dışı bırakılmış kutu; yazı rengi uygulama tarafından açık atanmış.
        var display = new RichTextBox { Text = "Açıklama", ForeColor = ColorTranslator.FromHtml("#f3f4f6"), Enabled = false };
        var plain = new TextBox { Text = "Varsayılan", Enabled = false };
        panel.Controls.Add(display);
        panel.Controls.Add(plain);

        Assert.Empty(TwContrast.Audit(panel));

        var issue = Assert.Single(TwContrast.Audit(panel, includeDisabled: true));
        Assert.Same(display, issue.Control);
        Assert.Equal("disabled text", issue.Part);
        Assert.Equal(SystemColors.Control.ToArgb(), issue.Background.ToArgb());
    }

    // Kütüphanenin varsayılan bileşen class'ları açık ve koyu temada, iki palet sürümüyle de okunabilir olmalı.
    [Theory]
    [InlineData(false, TwPaletteVersion.V3)]
    [InlineData(true, TwPaletteVersion.V3)]
    [InlineData(false, TwPaletteVersion.V4)]
    [InlineData(true, TwPaletteVersion.V4)]
    public void Default_component_classes_pass_the_audit(bool dark, TwPaletteVersion palette) => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { Theme = new TwTheme(palette) };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var card = new TwPanel { Name = "card", Size = new Size(400, 300) };
        var title = new Label { Name = "title", Text = "Müşteri", AutoSize = true };
        var input = new TwTextBox { Name = "input", Size = new Size(200, 36) };
        var primary = new TwButton { Name = "primary", Text = "Kaydet", Size = new Size(100, 36) };
        var ghost = new TwButton { Name = "ghost", Text = "İptal", Size = new Size(100, 36) };
        var grid = new DataGridView { Name = "grid", ColumnCount = 2, RowCount = 2 };
        var strip = new MenuStrip { Name = "menu", Items = { "Dosya" } };
        card.Controls.AddRange(new Control[] { title, input, primary, ghost, grid });
        form.Controls.Add(card);
        form.Controls.Add(strip);
        form.Show();

        provider.SetClasses(form, "bg-gray-50 text-gray-900 dark:bg-gray-900 dark:text-gray-100");
        provider.SetClasses(card, "card");
        provider.SetClasses(title, "text-gray-900 font-semibold dark:text-gray-100");
        provider.SetClasses(input, "input");
        provider.SetClasses(primary, "btn-primary");
        provider.SetClasses(ghost, "btn-ghost");
        provider.SetClasses(grid, "table");
        provider.SetClasses(strip, "bg-white text-gray-700 dark:bg-gray-800 dark:text-gray-100");
        provider.DarkMode = dark;
        TwAnimator.Step(DateTime.UtcNow.AddSeconds(10)); // btn'deki transition geçişleri tamamlanır

        var issues = TwContrast.Audit(form, provider);
        form.Close();
        Assert.True(issues.Count == 0, string.Join(Environment.NewLine, issues.Select(i => i.ToString())));
    });

    // Devre dışı butonlar: yazı okunabilir kalmalı (3:1), varsayılan bileşenlerin koyu temada açık blok olmamalı.
    [Theory]
    [InlineData(false, TwPaletteVersion.V3)]
    [InlineData(true, TwPaletteVersion.V3)]
    [InlineData(false, TwPaletteVersion.V4)]
    [InlineData(true, TwPaletteVersion.V4)]
    public void Disabled_default_buttons_stay_readable(bool dark, TwPaletteVersion palette) => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { Theme = new TwTheme(palette), DarkMode = dark };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        provider.SetClasses(form, "bg-white dark:bg-gray-900");
        var buttons = new Control[]
        {
            new TwButton { Name = "tw-primary", Text = "Kaydet", Enabled = false },
            new TwButton { Name = "tw-ghost", Text = "İptal", Enabled = false },
            new TwButton { Name = "tw-danger", Text = "Sil", Enabled = false },
            new Button { Name = "native-primary", Text = "Kaydet", Enabled = false },
            new Button { Name = "native-ghost", Text = "İptal", Enabled = false },
            new Button { Name = "native-danger", Text = "Sil", Enabled = false },
        };
        form.Controls.AddRange(buttons);
        foreach (var button in buttons) provider.SetClasses(button, "btn-" + button.Name.Split('-')[1]);
        form.Show();

        var issues = TwContrast.Audit(form, provider, includeDisabled: true);
        Assert.True(issues.Count == 0, string.Join(Environment.NewLine, issues.Select(i => i.ToString())));
        if (dark)
            foreach (var button in buttons)
                Assert.True(button.BackColor.GetBrightness() < 0.5f, $"{button.Name} koyu temada açık zeminli: {button.BackColor}");
        form.Close();
    });

    [Fact]
    public void Disabled_button_without_disabled_variant_is_dimmed_and_audited_with_drawn_color() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { BackColor = ColorTranslator.FromHtml("#111827") };
        var button = new TwButton { Text = "OK", Enabled = false };
        form.Controls.Add(button);
        provider.SetClasses(button, "bg-gray-800 text-gray-100");

        var drawn = TwDisabledText.TextColor(button);
        Assert.NotEqual(button.ForeColor.ToArgb(), drawn.ToArgb());
        Assert.True(TwContrast.Ratio(drawn, button.BackColor) < TwContrast.Ratio(button.ForeColor, button.BackColor));

        provider.SetClasses(button, "bg-gray-800 text-gray-100 disabled:text-gray-400");
        Assert.Equal(ColorTranslator.FromHtml("#9ca3af").ToArgb(), TwDisabledText.TextColor(button).ToArgb());
    });

    private sealed class PaintedGrid : DataGridView
    {
        protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e) => base.OnCellPainting(e);
    }

    [Fact]
    public void Finds_controls_that_paint_their_own_colors()
    {
        using var panel = new Panel();
        var handled = new DataGridView { Name = "handled" };
        handled.CellPainting += (_, _) => { };
        var subclassed = new PaintedGrid { Name = "subclassed" };
        var plain = new DataGridView { Name = "plain" };
        var list = new ListView { Name = "list", OwnerDraw = true };
        var box = new ListBox { Name = "box", DrawMode = DrawMode.OwnerDrawFixed };
        var tree = new TreeView { Name = "tree" };
        var own = new TwDataList { Name = "own" };
        panel.Controls.AddRange(new Control[] { handled, subclassed, plain, list, box, tree, own });

        var found = TwContrast.FindOwnerDrawn(panel).ToDictionary(f => f.Control.Name, f => f.Reason);

        Assert.Equal(new[] { "box", "handled", "list", "subclassed" }, found.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("CellPainting event", found["handled"]);
        Assert.Equal("OnCellPainting override", found["subclassed"]);
        Assert.Equal("DrawMode.OwnerDrawFixed", found["box"]);
    }
}
