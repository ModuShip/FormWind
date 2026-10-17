using System.Drawing;
using System.Windows.Forms;
using FormWind.Krypton;
using FormWind.Tests.Performance;
using Krypton.Toolkit;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class KryptonTests
{
    static KryptonTests() => KryptonApplier.Register();

    private static int Argb(string hex) => ColorTranslator.FromHtml(hex).ToArgb();

    // Krypton başlıkları ve hücreleri kendi paletiyle çizer; DataGridView hücre stillerinin zeminini yok sayardı.
    [Fact]
    public void Krypton_grid_headers_and_cells_are_painted_with_class_colors() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, Size = new Size(400, 300) };
        var grid = new KryptonDataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, RowHeadersVisible = false };
        grid.Columns.Add("name", "Ad");
        grid.Rows.Add("Ayşe");
        grid.ClearSelection();
        form.Controls.Add(grid);
        provider.SetClasses(grid, "bg-white text-gray-700 header:bg-gray-50 header:text-gray-500 "
                                  + "dark:bg-gray-800 dark:text-gray-200 dark:header:bg-gray-900 dark:header:text-gray-400");
        form.Show();
        grid.ClearSelection();

        using var bitmap = new Bitmap(grid.Width, grid.Height);
        grid.DrawToBitmap(bitmap, new Rectangle(Point.Empty, grid.Size));
        var headerCell = grid.GetCellDisplayRectangle(0, -1, false);
        var dataCell = grid.GetCellDisplayRectangle(0, 0, false);
        // Yazıdan uzak, hücrenin sağ kenarına yakın bir piksel.
        Assert.Equal(Argb("#111827"), bitmap.GetPixel(headerCell.Right - 4, headerCell.Top + headerCell.Height / 2).ToArgb());
        Assert.Equal(Argb("#1f2937"), bitmap.GetPixel(dataCell.Right - 4, dataCell.Top + dataCell.Height / 2).ToArgb());

        Assert.Equal(Argb("#9ca3af"), grid.StateCommon.HeaderColumn.Content.Color1.ToArgb());
        provider.DarkMode = false;
        Assert.Equal(Argb("#f9fafb"), grid.StateCommon.HeaderColumn.Back.Color1.ToArgb());
        Assert.Equal(Color.White.ToArgb(), grid.StateCommon.DataCell.Back.Color1.ToArgb());

        provider.SetClasses(grid, "");
        Assert.Equal(PaletteColorStyle.Inherit, grid.StateCommon.HeaderColumn.Back.ColorStyle);
        form.Close();
    });

    private static Form OffscreenForm() =>
        new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, Size = new Size(500, 400) };

    [Fact]
    public void Check_buttons_toggles_and_input_controls_get_their_state_palettes() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = OffscreenForm();
        var check = new KryptonCheckButton();
        var box = new KryptonCheckBox();
        var masked = new KryptonMaskedTextBox();
        var rich = new KryptonRichTextBox();
        var number = new KryptonNumericUpDown();
        var date = new KryptonDateTimePicker();
        form.Controls.AddRange(new Control[] { check, box, masked, rich, number, date });

        provider.SetClasses(check, "bg-white checked:bg-blue-600 checked:text-white checked:hover:bg-blue-700");
        Assert.Equal(Argb("#2563eb"), check.StateCheckedNormal.Back.Color1.ToArgb());
        Assert.Equal(Argb("#1d4ed8"), check.StateCheckedTracking.Back.Color1.ToArgb());
        Assert.Equal(Color.White.ToArgb(), check.StateCheckedNormal.Content.ShortText.Color1.ToArgb());
        Assert.Equal(Color.White.ToArgb(), check.StateCommon.Back.Color1.ToArgb());

        provider.SetClasses(box, "text-gray-700 disabled:text-gray-400");
        Assert.Equal(Argb("#374151"), box.StateCommon.ShortText.Color1.ToArgb());
        Assert.Equal(Argb("#9ca3af"), box.StateDisabled.ShortText.Color1.ToArgb());

        foreach (var (control, common, active) in new (Control, PaletteInputControlTripleRedirect, PaletteInputControlTripleStates)[]
        {
            (masked, masked.StateCommon, masked.StateActive), (rich, rich.StateCommon, rich.StateActive),
            (number, number.StateCommon, number.StateActive), (date, date.StateCommon, date.StateActive),
        })
        {
            provider.SetClasses(control, "input");                                     // bg-white border-gray-300 focus:border-blue-500
            Assert.Equal(Color.White.ToArgb(), common.Back.Color1.ToArgb());
            Assert.Equal(Argb("#d1d5db"), common.Border.Color1.ToArgb());
            Assert.Equal(Argb("#3b82f6"), active.Border.Color1.ToArgb());
            provider.SetClasses(control, "");
            Assert.Equal(Color.Empty, common.Back.Color1);                             // paletten miras
        }
        form.Close();
    });

    // Sabit boyutlu Krypton butonu: büyük padding yazıyı "…" ile kırpardı; rounded-full içerik alanını yok ederdi.
    [Fact]
    public void Fixed_size_buttons_fit_the_padding_and_limit_the_corner_radius() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = OffscreenForm();
        var button = new KryptonButton { Size = new Size(96, 32) };
        button.Values.Text = "Kurumsal";
        form.Controls.Add(button);

        provider.SetClasses(button, "px-8 py-4 rounded-full");
        var padding = button.StateCommon.Content.Padding;
        var text = TextRenderer.MeasureText("Kurumsal", button.Font);
        Assert.True(padding.Left + padding.Right <= button.Width - text.Width, $"{padding} yazıya yer bırakmıyor");
        Assert.True(padding.Left > 0 && padding.Left == padding.Right);                 // orantılı kısılır
        Assert.Equal(16f, button.StateCommon.Border.Rounding);                          // yüksekliğin yarısı

        button.AutoSize = true;                                                          // AutoSize butonda dokunulmaz
        provider.SetClasses(button, "px-8 py-2");
        Assert.Equal(32, button.StateCommon.Content.Padding.Left);
        form.Close();
    });

    [Fact]
    public void Combo_box_group_box_header_group_list_box_and_form_use_their_parts() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new KryptonForm { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var combo = new KryptonComboBox();
        var group = new KryptonGroupBox();
        var headerGroup = new KryptonHeaderGroup();
        var list = new KryptonListBox();
        form.Controls.AddRange(new Control[] { combo, group, headerGroup, list });

        provider.SetClasses(combo, "bg-white item:hover:bg-gray-100 menu:bg-gray-50");
        Assert.Equal(Color.White.ToArgb(), combo.StateCommon.ComboBox.Back.Color1.ToArgb());
        Assert.Equal(Argb("#f3f4f6"), combo.StateTracking.Item.Back.Color1.ToArgb());
        Assert.Equal(Argb("#f9fafb"), combo.StateCommon.DropBack.Color1.ToArgb());

        provider.SetClasses(group, "bg-white border border-gray-200 text-gray-700 caption:text-blue-700");
        Assert.Equal(Color.White.ToArgb(), group.StateCommon.Back.Color1.ToArgb());
        Assert.Equal(Argb("#e5e7eb"), group.StateCommon.Border.Color1.ToArgb());
        Assert.Equal(Argb("#1d4ed8"), group.StateCommon.Content.ShortText.Color1.ToArgb());

        provider.SetClasses(headerGroup, "bg-white header:bg-blue-600 header:text-white");
        Assert.Equal(Argb("#2563eb"), headerGroup.StateCommon.HeaderPrimary.Back.Color1.ToArgb());
        Assert.Equal(Color.White.ToArgb(), headerGroup.StateCommon.HeaderPrimary.Content.ShortText.Color1.ToArgb());

        provider.SetClasses(list, "list");                                             // item:hover:bg-gray-100 selected:bg-blue-50
        Assert.Equal(Argb("#f3f4f6"), list.StateTracking.Item.Back.Color1.ToArgb());
        Assert.Equal(Argb("#eff6ff"), list.StateCheckedNormal.Item.Back.Color1.ToArgb());
        Assert.Equal(Argb("#111827"), list.StateCommon.Item.Content.ShortText.Color1.ToArgb());   // öğe yazısı listenin yazı rengi

        provider.SetClasses(form, "bg-gray-50 caption:bg-gray-50 caption:text-gray-900");
        Assert.Equal(Argb("#f9fafb"), form.StateCommon!.Back.Color1.ToArgb());
        Assert.Equal(Argb("#111827"), form.StateCommon.Header.Content.ShortText.Color1.ToArgb());
        form.Close();
    });

    // Tema köprüsü: class'sız Krypton kontrolleri paketin bileşenlerinin renklerini Krypton paletinden alır.
    [Fact]
    public void Palette_is_built_from_the_theme_components()
    {
        var theme = new TwTheme(TwPreset.Material);
        using var light = TwKryptonTheme.CreatePalette(theme, dark: false);
        Assert.Equal(Argb("#fef7ff"), light.GetBackColor1(PaletteBackStyle.PanelClient, PaletteState.Normal).ToArgb());          // page
        Assert.Equal(Argb("#fef7ff"), light.GetBackColor1(PaletteBackStyle.ButtonStandalone, PaletteState.Normal).ToArgb());     // btn-ghost
        Assert.Equal(Argb("#6750a4"), light.GetContentShortTextColor1(PaletteContentStyle.ButtonStandalone, PaletteState.Normal).ToArgb());
        Assert.Equal(Argb("#6750a4"), light.GetBackColor1(PaletteBackStyle.ButtonStandalone, PaletteState.CheckedNormal).ToArgb()); // btn-primary
        Assert.Equal(Argb("#79747e"), light.GetBorderColor1(PaletteBorderStyle.InputControlStandalone, PaletteState.Normal).ToArgb()); // input
        Assert.Equal(16f, light.GetBorderRounding(PaletteBorderStyle.ButtonStandalone, PaletteState.Normal));   // rounded-full, sınırlı

        using var dark = TwKryptonTheme.CreatePalette(theme, dark: true);
        Assert.Equal(Argb("#141218"), dark.GetBackColor1(PaletteBackStyle.PanelClient, PaletteState.Normal).ToArgb());
        Assert.Equal(Argb("#d0bcff"), dark.GetBackColor1(PaletteBackStyle.ButtonStandalone, PaletteState.CheckedNormal).ToArgb());
    }

    [Fact]
    public void Follow_keeps_the_global_palette_in_sync_with_the_provider() => Sta.Run(() =>
    {
        var previousTheme = TwTheme.Current;
        var previousMode = KryptonManager.CurrentGlobalPaletteMode;
        using var provider = new TailwindProvider();
        try
        {
            TwTheme.Current = new TwTheme(TwPreset.Fluent);
            using var manager = new KryptonManager();
            var previousCustom = manager.GlobalCustomPalette;
            using (TwKryptonTheme.Follow(provider))
            {
                Color PanelBack() => KryptonManager.CurrentGlobalPalette.GetBackColor1(PaletteBackStyle.PanelClient, PaletteState.Normal);
                Assert.Equal(PaletteMode.Custom, KryptonManager.CurrentGlobalPaletteMode);
                Assert.Equal(Argb("#f5f5f5"), PanelBack().ToArgb());                   // fluent page: surface-100

                provider.DarkMode = true;
                Assert.Equal(Argb("#1f1f1f"), PanelBack().ToArgb());                   // surface-900

                TwTheme.Current = new TwTheme(TwPreset.Antd);
                Assert.Equal(Argb("#141414"), PanelBack().ToArgb());

                // Class'sız bir Krypton paneli paletin rengiyle çizilir.
                using var form = OffscreenForm();
                var panel = new KryptonPanel { Dock = DockStyle.Fill };
                form.Controls.Add(panel);
                form.Show();
                using var bitmap = new Bitmap(panel.Width, panel.Height);
                panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, panel.Size));
                Assert.Equal(Argb("#141414"), bitmap.GetPixel(panel.Width / 2, panel.Height / 2).ToArgb());
                form.Close();
            }
            Assert.Equal(previousMode, KryptonManager.CurrentGlobalPaletteMode);         // bırakınca önceki palet
            Assert.Same(previousCustom, manager.GlobalCustomPalette);                   // bırakılan palet asılı kalmaz
        }
        finally
        {
            TwTheme.Current = previousTheme;
        }
    });
}
