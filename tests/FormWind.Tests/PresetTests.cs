using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class PresetTests
{
    public static IEnumerable<object[]> Presets() => Enum.GetValues(typeof(TwPreset)).Cast<TwPreset>().Select(p => new object[] { p });

    public static IEnumerable<object[]> PresetsAndModes() =>
        Enum.GetValues(typeof(TwPreset)).Cast<TwPreset>().SelectMany(p => new[] { new object[] { p, false }, new object[] { p, true } });

    // Paketler aynı bileşen adlarını tanımlar; hepsinin class'ları (iç içe bileşenler dahil) tanınmalı.
    [Theory]
    [MemberData(nameof(Presets))]
    public void Preset_defines_the_standard_components_with_known_classes(TwPreset preset)
    {
        var theme = new TwTheme(preset);
        Assert.Equal(preset, theme.Preset);
        foreach (var name in TwTheme.DefaultComponents.Keys)
        {
            var parsed = theme.Parse(name);
            Assert.True(parsed.Unknown.Count == 0, $"{preset}/{name}: {string.Join(", ", parsed.Unknown)}");
        }
        foreach (var classes in theme.AutoStyle.Values.Where(v => v.Length > 0))
            Assert.Empty(theme.Parse(classes).Unknown);
    }

    [Fact]
    public void Presets_change_the_semantic_colors_and_the_component_shapes()
    {
        Color Primary(TwTheme theme) => theme.Parse("bg-primary-600").Resolve(TwState.None).BackColor!.Value;
        Assert.Equal(ColorTranslator.FromHtml("#2563eb").ToArgb(), Primary(new TwTheme()).ToArgb());             // blue-600
        Assert.Equal(ColorTranslator.FromHtml("#0f6cbd").ToArgb(), Primary(new TwTheme(TwPreset.Fluent)).ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#0958d9").ToArgb(), Primary(new TwTheme(TwPreset.Antd)).ToArgb());
        Assert.Equal(ColorTranslator.FromHtml("#6750a4").ToArgb(), Primary(new TwTheme(TwPreset.Material)).ToArgb());

        int? Radius(TwPreset preset) => new TwTheme(preset).Parse("btn-primary").Resolve(TwState.None).Radius;
        Assert.Equal(6, Radius(TwPreset.Tailwind));
        Assert.Equal(4, Radius(TwPreset.Fluent));
        Assert.Equal(9999, Radius(TwPreset.Material));
        Assert.Equal("Segoe UI Variable Text, Segoe UI", new TwTheme(TwPreset.Fluent).FontFamilySans);
    }

    [Fact]
    public void Theme_file_applies_the_preset_first_and_overrides_it()
    {
        var theme = TwTheme.FromJson(@"{
            ""preset"": ""Material"",
            ""palette"": ""v4"",
            ""colors"": { ""primary"": { ""600"": ""#123456"" } },
            ""components"": { ""card"": ""bg-white p-2"" },
            ""autoStyle"": { ""Label"": ""muted"", ""ListView"": """" }
        }");
        Assert.Equal(TwPreset.Material, theme.Preset);
        Assert.Equal(TwPaletteVersion.V4, theme.Palette);
        Assert.Equal(9999, theme.Parse("btn").Resolve(TwState.None).Radius);                                  // paketten
        Assert.True(theme.TryGetColor("primary", "600", out var primary));
        Assert.Equal(ColorTranslator.FromHtml("#123456").ToArgb(), primary.ToArgb());                        // dosyadan
        Assert.True(theme.TryGetColor("primary", "200", out var tone));                                      // paketin tonu, v4'e dönmez
        Assert.Equal(ColorTranslator.FromHtml("#d0bcff").ToArgb(), tone.ToArgb());
        Assert.Equal("bg-white p-2", theme.GetComponent("card"));
        Assert.Equal("muted", theme.GetAutoStyle(typeof(Label)));
        Assert.Null(theme.GetAutoStyle(typeof(ListView)));                                                   // boş: stillenmez

        var error = Assert.Throws<FormatException>(() => TwTheme.FromJson(@"{ ""preset"": ""bootstrap"" }"));
        Assert.Contains("fluent", error.Message);
    }

    [Fact]
    public void Semantic_colors_follow_the_palette_until_the_application_defines_them()
    {
        var theme = new TwTheme();
        Assert.True(theme.TryGetColor("surface", "500", out var gray));
        Assert.Equal(ColorTranslator.FromHtml("#6b7280").ToArgb(), gray.ToArgb());                          // gray-500 (v3)

        theme.UsePalette(TwPaletteVersion.V4);
        theme.TryGetColor("blue", "600", out var blue4);
        theme.TryGetColor("primary", "600", out var primary4);
        Assert.Equal(blue4.ToArgb(), primary4.ToArgb());

        theme.SetColor("primary", "600", Color.Purple);                                                      // uygulamanın rengi
        theme.UsePalette(TwPaletteVersion.V3);
        theme.TryGetColor("primary", "600", out var own);
        theme.TryGetColor("primary", "500", out var kept);
        Assert.Equal(Color.Purple.ToArgb(), own.ToArgb());
        new TwTheme(TwPaletteVersion.V4).TryGetColor("blue", "500", out var blue500);
        Assert.Equal(blue500.ToArgb(), kept.ToArgb());                                                       // tanımlanmayan ton da sabit kalır
        Assert.Contains("primary", TwTheme.SemanticColors.Keys);
    }

    [Fact]
    public void Auto_style_is_found_through_the_base_types()
    {
        var theme = new TwTheme();
        Assert.Equal("input", theme.GetAutoStyle(typeof(TwNumberBox)));        // TwNumberBox → TwTextBox
        Assert.Equal("input", theme.GetAutoStyle(typeof(RichTextBox)));        // → TextBoxBase
        Assert.Equal("select", theme.GetAutoStyle(typeof(TwDatePicker)));     // kendi kaydı TwTextBox'tan önce
        Assert.Equal("toggle", theme.GetAutoStyle(typeof(TwSwitch)));         // → CheckBox
        Assert.Equal("btn-ghost", theme.GetAutoStyle(typeof(TwButton)));      // → Button
        Assert.Equal("list", theme.GetAutoStyle(typeof(TwTreeView)));         // → TwDataList
        Assert.Equal("menu", theme.GetAutoStyle(typeof(StatusStrip)));        // → ToolStrip
        Assert.Null(theme.GetAutoStyle(typeof(Label)));
        Assert.Null(theme.GetAutoStyle(typeof(Panel)));

        theme.SetAutoStyle("Button", "");
        Assert.Null(theme.GetAutoStyle(typeof(TwButton)));
        Assert.True(theme.RemoveAutoStyle("Button"));
        Assert.Null(theme.GetAutoStyle(typeof(Button)));
    }

    // Her paket açık ve koyu temada okunabilir olmalı: AutoStyle'lı bir form ve bileşenler kontrast denetiminden geçer.
    [Theory]
    [MemberData(nameof(PresetsAndModes))]
    public void Preset_passes_the_contrast_audit(TwPreset preset, bool dark) => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { Theme = new TwTheme(preset) };
        using var form = PresetForm(provider);
        provider.DarkMode = dark;
        TwAnimator.Step(DateTime.UtcNow.AddSeconds(10));

        var issues = TwContrast.Audit(form, provider);
        form.Close();
        Assert.True(issues.Count == 0, $"{preset} ({(dark ? "dark" : "light")}):{Environment.NewLine}" + string.Join(Environment.NewLine, issues.Select(i => i.ToString())));
    });

    [Theory]
    [MemberData(nameof(PresetsAndModes))]
    public void Disabled_preset_buttons_stay_readable(TwPreset preset, bool dark) => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { Theme = new TwTheme(preset), DarkMode = dark };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        provider.SetClasses(form, "page");
        var buttons = new[] { "btn-primary", "btn-secondary", "btn-ghost", "btn-danger" }
            .SelectMany(c => new Control[] { new TwButton { Name = "tw:" + c, Text = "Kaydet", Enabled = false }, new Button { Name = "native:" + c, Text = "Kaydet", Enabled = false } })
            .ToArray();
        form.Controls.AddRange(buttons);
        foreach (var button in buttons) provider.SetClasses(button, button.Name.Split(':')[1]);
        form.Show();

        var issues = TwContrast.Audit(form, provider, includeDisabled: true);
        form.Close();
        Assert.True(issues.Count == 0, string.Join(Environment.NewLine, issues.Select(i => i.ToString())));
        if (dark)
            foreach (var button in buttons)
                Assert.True(button.BackColor.GetBrightness() < 0.5f, $"{preset}: {button.Name} koyu temada açık zeminli: {button.BackColor}");
    });

    /// <summary>Paketin bileşenlerini kullanan form: çoğu kontrol AutoStyle'la, vurgulular kendi class'larıyla.</summary>
    internal static Form PresetForm(TailwindProvider provider)
    {
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, ClientSize = new Size(900, 640) };
        var menu = new MenuStrip { Items = { "Dosya", "Düzen" } };
        var title = new Label { Name = "title", Text = "Müşteri", AutoSize = true, Location = new Point(16, 32) };
        var muted = new Label { Name = "muted", Text = "Son güncelleme dün", AutoSize = true, Location = new Point(16, 64) };
        var card = new TwPanel { Name = "card", Location = new Point(16, 90), Size = new Size(420, 300) };
        var name = new Label { Name = "name", Text = "Ad soyad", AutoSize = true, Location = new Point(16, 16) };
        var input = new TwTextBox { Name = "input", Location = new Point(16, 40), Size = new Size(200, 36), Text = "Ayşe" };
        var select = new TwSelect { Name = "select", Location = new Point(16, 84), Size = new Size(200, 36) };
        select.Items.AddRange(new[] { "İstanbul", "Ankara" });
        select.SelectedIndex = 0;
        var check = new CheckBox { Name = "check", Text = "Aktif", AutoSize = true, Location = new Point(16, 128), Checked = true };
        var primary = new TwButton { Name = "primary", Text = "Kaydet", Location = new Point(16, 170), Size = new Size(100, 36) };
        var ghost = new TwButton { Name = "ghost", Text = "İptal", Location = new Point(124, 170), Size = new Size(100, 36) };
        var secondary = new Button { Name = "secondary", Text = "Taslak", Location = new Point(232, 170), Size = new Size(100, 36) };
        var danger = new Button { Name = "danger", Text = "Sil", Location = new Point(16, 220), Size = new Size(100, 36) };
        var link = new Label { Name = "link", Text = "Ayrıntılar", AutoSize = true, Location = new Point(130, 230) };
        card.Controls.AddRange(new Control[] { name, input, select, check, primary, ghost, secondary, danger, link });

        var tabs = new TwTabStrip { Name = "tabs", AutoSize = true, Location = new Point(460, 32) };
        tabs.Tabs.AddRange(new[] { "Genel", "Adres" });
        var grid = new DataGridView { Name = "grid", ColumnCount = 2, RowCount = 2, Location = new Point(460, 80), Size = new Size(400, 140) };
        var list = new TwListBox { Name = "list", Location = new Point(460, 230), Size = new Size(200, 120) };
        list.Items.Add("Sipariş 1");
        list.Items.Add("Sipariş 2");
        list.SelectedIndex = 1;
        var badge = new Label { Name = "badge", Text = "Yeni", AutoSize = true, Location = new Point(460, 370) };
        var nav = new RadioButton { Name = "nav", Text = "Siparişler", Appearance = Appearance.Button, Checked = true, Location = new Point(680, 230), Size = new Size(160, 36) };
        form.Controls.AddRange(new Control[] { menu, title, muted, card, tabs, grid, list, badge, nav });
        var alerts = new[] { "alert-info", "alert-success", "alert-warning", "alert-danger" }.Select((c, i) =>
            new Label { Name = c, Text = "Kayıt güncellendi", AutoSize = false, Size = new Size(200, 44), Location = new Point(16 + i * 210, 410) }).ToArray();
        form.Controls.AddRange(alerts);

        provider.ContainerControl = form;
        provider.AutoStyle = true;
        provider.SetClasses(title, "title");
        provider.SetClasses(muted, "muted");
        provider.SetClasses(card, "card");
        provider.SetClasses(primary, "btn-primary");
        provider.SetClasses(secondary, "btn-secondary");
        provider.SetClasses(danger, "btn-danger");
        provider.SetClasses(link, "link");
        provider.SetClasses(badge, "badge");
        provider.SetClasses(nav, "nav-item");
        foreach (var alert in alerts) provider.SetClasses(alert, alert.Name);
        form.Show();
        return form;
    }
}
