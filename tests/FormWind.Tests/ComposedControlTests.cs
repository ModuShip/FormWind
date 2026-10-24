using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// Uygulamaların elle çizdiği parçaların hazır karşılıkları: sekme taşması, çip, liste satırı şablonu, araç çubuğu,
// ayar satırı ve metin girişli diyalog.
[Collection(UiCollection.Name)]
public class ComposedControlTests
{
    private static Form NewForm(int width = 640, int height = 400) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(width, height) };

    private static void Invoke(Control control, string method, EventArgs args) =>
        control.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { args.GetType() }, null)!
            .Invoke(control, new object[] { args });

    private static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(Math.Max(1, control.Width), Math.Max(1, control.Height));
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    [Fact]
    public void Tab_strip_can_draw_ampersands_as_text_and_reads_them_without_the_marker() => Sta.Run(() =>
    {
        using var tabs = new TwTabStrip { Size = new Size(600, 40) };
        tabs.Tabs.AddRange(new[] { "Özet", "Parça & maliyet", "&Notlar" });
        int withMnemonic = tabs.GetTabBounds(1).Width;
        Assert.Equal("Notlar", tabs.AccessibilityObject.GetChild(2)!.Name);           // kısayol işareti okunmaz

        tabs.UseMnemonic = false;
        Assert.True(tabs.GetTabBounds(1).Width > withMnemonic);                        // & artık metin: ölçüye girer
        Assert.Equal("&Notlar", tabs.AccessibilityObject.GetChild(2)!.Name);
        using (Render(tabs)) { }
    });

    [Fact]
    public void Tab_strip_selects_a_tab_with_its_alt_shortcut() => Sta.Run(() =>
    {
        using var form = NewForm();
        var tabs = new TwTabStrip { Size = new Size(400, 40) };
        tabs.Tabs.AddRange(new[] { "&Genel", "&Ayrıntı" });
        form.Controls.Add(tabs);
        form.Show();
        try
        {
            var process = typeof(Control).GetMethod("ProcessMnemonic", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.True((bool)process.Invoke(tabs, new object[] { 'a' })!);
            Assert.Equal(1, tabs.SelectedIndex);
            tabs.UseMnemonic = false;
            Assert.False((bool)process.Invoke(tabs, new object[] { 'g' })!);
            Assert.Equal(1, tabs.SelectedIndex);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Tab_strip_moves_tabs_that_do_not_fit_to_a_more_menu() => Sta.Run(() =>
    {
        using var form = NewForm();
        var tabs = new TwTabStrip { Size = new Size(260, 40) };
        tabs.Tabs.AddRange(new[] { "Genel bakış", "Duruşlar", "Parça & maliyet", "Kök neden", "Ekip", "Geçmiş" });
        form.Controls.Add(tabs);
        form.Show();
        try
        {
            var hidden = tabs.OverflowTabs;
            Assert.NotEmpty(hidden);
            var button = tabs.OverflowButtonBounds;
            Assert.False(button.IsEmpty);
            Assert.True(button.Right <= tabs.Width);
            for (int i = 0; i < tabs.Tabs.Count; i++)
            {
                var bounds = tabs.GetTabBounds(i);
                Assert.Equal(hidden.Contains(i), bounds.IsEmpty);
                if (!bounds.IsEmpty) Assert.True(bounds.Right <= button.Left);
            }

            // Menüden seçilen sekme görünür sekmelerin arasına alınır.
            tabs.ShowOverflow();
            var menu = tabs.OverflowDropDown!;
            Assert.Equal(hidden.Count, menu.Items.Count);
            Assert.Equal("Geçmiş", menu.Items[menu.Items.Count - 1].Text);
            menu.Items[menu.Items.Count - 1].PerformClick();
            menu.Close();
            Assert.Equal(5, tabs.SelectedIndex);
            Assert.False(tabs.GetTabBounds(5).IsEmpty);
            Assert.DoesNotContain(5, tabs.OverflowTabs);

            // Gizli sekmeler ekran okuyucuda görünmez durumdadır; taşma kapatılınca hepsi yerleşir.
            Assert.True((tabs.AccessibilityObject.GetChild(tabs.OverflowTabs[0])!.State & AccessibleStates.Invisible) != 0);
            tabs.OverflowMenu = false;
            Assert.Empty(tabs.OverflowTabs);
            Assert.True(tabs.OverflowButtonBounds.IsEmpty);
            tabs.Width = 1200;
            tabs.OverflowMenu = true;
            Assert.Empty(tabs.OverflowTabs);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Segmented_tab_strip_with_overflow_paints() => Sta.Run(() =>
    {
        using var tabs = new TwTabStrip { Size = new Size(180, 40), Variant = TwTabVariant.Segmented };
        tabs.Tabs.AddRange(new[] { "Gün", "Hafta", "Ay", "Çeyrek", "Yıl" });
        tabs.SelectedIndex = 4;
        Assert.NotEmpty(tabs.OverflowTabs);
        Assert.False(tabs.GetTabBounds(4).IsEmpty);
        using (Render(tabs)) { }
    });

    [Fact]
    public void Chip_raises_remove_without_click_and_toggles_when_checkable() => Sta.Run(() =>
    {
        using var form = NewForm();
        var chip = new TwChip { Text = "Hat 2", Removable = true };
        form.Controls.Add(chip);
        form.Show();
        try
        {
            Assert.True(chip.TabStop);
            chip.Size = chip.GetPreferredSize(Size.Empty);
            int removed = 0, clicked = 0;
            chip.RemoveClick += (_, _) => removed++;
            chip.Click += (_, _) => clicked++;

            var x = chip.RemoveButtonBounds;
            Assert.False(x.IsEmpty);
            Assert.True(x.Right <= chip.Width);
            var center = new Point(x.X + x.Width / 2, x.Y + x.Height / 2);
            Invoke(chip, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));
            Invoke(chip, "OnClick", new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));
            Assert.Equal((1, 0), (removed, clicked));                                  // ✕ çipin Click'ini tetiklemez

            Invoke(chip, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 4, 4, 0));
            Invoke(chip, "OnClick", new MouseEventArgs(MouseButtons.Left, 1, 4, 4, 0));
            Assert.Equal((1, 1), (removed, clicked));

            Invoke(chip, "OnKeyDown", new KeyEventArgs(Keys.Delete));
            Assert.Equal(2, removed);

            var acc = chip.AccessibilityObject;
            Assert.Equal(1, acc.GetChildCount());
            Assert.Equal(TwTexts.Current.Remove + " Hat 2", acc.GetChild(0)!.Name);
            acc.GetChild(0)!.DoDefaultAction();
            Assert.Equal(3, removed);

            chip.Removable = false;
            chip.Checkable = true;
            Assert.Equal(AccessibleRole.CheckButton, chip.AccessibilityObject.Role);
            int changed = 0;
            chip.CheckedChanged += (_, _) => changed++;
            Invoke(chip, "OnKeyDown", new KeyEventArgs(Keys.Space));
            Assert.True(chip.Checked);
            Assert.True((chip.AccessibilityObject.State & AccessibleStates.Checked) != 0);
            chip.PerformClick();
            Assert.False(chip.Checked);
            Assert.Equal(2, changed);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Static_chip_is_not_a_tab_stop_and_sizes_to_its_content() => Sta.Run(() =>
    {
        using var chip = new TwChip { Text = "Kronik" };
        Assert.False(chip.TabStop);
        Assert.Equal(AccessibleRole.StaticText, chip.AccessibilityObject.Role);
        var plain = chip.GetPreferredSize(Size.Empty);
        chip.IconName = "warning";
        var withIcon = chip.GetPreferredSize(Size.Empty);
        Assert.True(withIcon.Width > plain.Width);
        chip.IconName = null;
        chip.DotColor = Color.Red;
        Assert.True(chip.GetPreferredSize(Size.Empty).Width > plain.Width);
        chip.Removable = true;
        Assert.True(chip.TabStop);
        chip.Size = chip.GetPreferredSize(Size.Empty);
        using (Render(chip)) { }
    });

    [Fact]
    public void Chip_classes_follow_checked_state() => Sta.Run(() =>
    {
        using var tw = new TailwindProvider();
        using var chip = new TwChip { Text = "Vardiya B", Checkable = true };
        tw.SetClasses(chip, "chip");
        var off = chip.BackColor;
        chip.Checked = true;
        Assert.NotEqual(off, chip.BackColor);
        Assert.Equal(TwTheme.Current.Parse("bg-primary-600").Resolve(TwState.None).BackColor!.Value.ToArgb(), chip.BackColor.ToArgb());
        Assert.Equal("chip", TwTheme.DefaultAutoStyle["TwChip"]);
    });

    [Fact]
    public void List_box_draws_list_items_with_the_template_and_reads_all_parts() => Sta.Run(() =>
    {
        using var form = NewForm();
        var list = new TwListBox { Dock = DockStyle.Fill, SmoothScrolling = false };
        form.Controls.Add(list);
        list.Items.Add(new TwListItem("Pres 3 · mil yatağı", "Hat 2 · 30 günde 6 duruş")
        {
            DotColor = Color.Red, Value = "540 dk", ValueCaption = "30 gün", Badge = "Kronik", BadgeClasses = "chip-danger",
        });
        list.Items.Add(new TwListItem("Testere") { Value = "90 dk" });
        list.Items.Add(new TwListItem("Risk") { Score = 0.72, Subtitle = "Pres 3" });
        list.Items.Add("düz metin");
        form.Show();

        Assert.Equal("Pres 3 · mil yatağı", list.GetItemText(0));
        Assert.True(list.GetItemBounds(0).Height > list.GetItemBounds(1).Height);   // iki satırlı satır uzar
        Assert.Equal(list.GetItemBounds(1).Height, list.GetItemBounds(3).Height);
        Assert.Equal("Pres 3 · mil yatağı, Hat 2 · 30 günde 6 duruş, 540 dk, 30 gün, Kronik", list.AccessibilityObject.GetChild(0)!.Name);
        Assert.Equal("Risk, Pres 3, 72", list.AccessibilityObject.GetChild(2)!.Name);
        Assert.Equal("düz metin", list.AccessibilityObject.GetChild(3)!.Name);

        // Rozet BadgeClasses'ın zemin rengiyle çizilir.
        var badge = TwTheme.Current.Parse("chip-danger").Resolve(TwState.None).BackColor!.Value;
        using (var bitmap = Render(list))
        {
            var row = list.GetItemBounds(0);
            bool found = false;
            for (int x = row.Right - 120; x < row.Right && !found; x++)
                for (int y = row.Top; y < row.Bottom && !found; y++)
                    found = bitmap.GetPixel(x, y).ToArgb() == badge.ToArgb();
            Assert.True(found);
        }

        // Kendi DrawItem'ı olan liste şablonu kullanmaz.
        int drawn = 0;
        list.DrawItem += (_, e) => drawn++;
        using (Render(list)) { }
        Assert.True(drawn >= 4);
    });

    [Fact]
    public void Toolbar_moves_items_that_do_not_fit_to_the_more_menu() => Sta.Run(() =>
    {
        using var form = NewForm(800);
        var toolbar = new TwToolbar { Dock = DockStyle.Top, Height = 44 };
        var title = new Label { Text = "Duruş kayıtları · Hat 2 · Ekim", AutoSize = true, AutoEllipsis = true, MinimumSize = new Size(80, 0) };
        var segments = new TwTabStrip { Variant = TwTabVariant.Segmented, AutoSize = true };
        segments.Tabs.AddRange(new[] { "Gün", "Hafta", "Ay" });
        var refresh = new TwButton { Text = "Yenile", AutoSize = true };
        var export = new TwButton { Text = "Dışa aktar", AutoSize = true };
        var compact = new CheckBox { Text = "Sıkı görünüm", AutoSize = true };
        var settings = new TwButton { Text = "Ayarlar", AutoSize = true };
        toolbar.Controls.AddRange(new Control[] { title, segments, refresh, export, compact, settings });
        toolbar.SetOverflow(title, TwToolbarOverflow.Shrink);
        toolbar.SetAlignment(settings, TwToolbarAlignment.End);
        toolbar.SetPriority(refresh, 1);
        form.Controls.Add(toolbar);
        form.Show();
        try
        {
            Assert.Empty(toolbar.OverflowItems);
            Assert.False(toolbar.OverflowButton.Visible);
            Assert.True(settings.Right <= toolbar.Width - toolbar.Padding.Right);
            Assert.True(title.Right <= segments.Left);

            // Daralınca önceliği düşük ve sondaki öğeler menüye geçer; açılan yer başlığa geri verilir.
            form.ClientSize = new Size(360, 400);
            Application.DoEvents();
            Assert.True(toolbar.OverflowButton.Visible);
            Assert.True(title.Width >= title.MinimumSize.Width);
            var hidden = toolbar.OverflowItems;
            Assert.Contains(compact, hidden);
            Assert.DoesNotContain(title, hidden);
            Assert.False(compact.Visible);
            Assert.True(toolbar.OverflowButton.Right <= toolbar.Width);
            foreach (var item in new Control[] { title, segments, refresh, export, settings }.Where(c => c.Visible))
                Assert.True(item.Right <= toolbar.OverflowButton.Left);
            if (hidden.Contains(refresh)) Assert.Contains(export, hidden);              // öncelikli öğe en son gizlenir

            // Taşınacak öğe kalmayınca başlık en küçük genişliğine kadar kısalır.
            form.ClientSize = new Size(200, 400);
            Application.DoEvents();
            Assert.True(title.Width < title.GetPreferredSize(Size.Empty).Width);
            Assert.True(title.Width >= title.MinimumSize.Width);
            Assert.True(title.Right <= toolbar.OverflowButton.Left);
            Assert.False(title.AutoSize);                                              // genişliği araç çubuğu yönetir
            form.ClientSize = new Size(360, 400);
            Application.DoEvents();

            // Menü öğeleri kontrolleri çalıştırır.
            int exported = 0;
            export.Click += (_, _) => exported++;
            toolbar.SetOverflow(export, TwToolbarOverflow.Always);
            toolbar.SetOverflow(segments, TwToolbarOverflow.Always);
            toolbar.SetOverflow(compact, TwToolbarOverflow.Always);
            toolbar.ShowOverflowMenu();
            var menu = toolbar.OverflowMenu!;
            var exportItem = menu.Items.Cast<ToolStripItem>().Single(i => i.Text == "Dışa aktar");
            exportItem.PerformClick();
            Assert.Equal(1, exported);
            var check = (ToolStripMenuItem)menu.Items.Cast<ToolStripItem>().Single(i => i.Text == "Sıkı görünüm");
            check.PerformClick();
            Assert.True(compact.Checked);
            var tabsItem = (ToolStripMenuItem)menu.Items.Cast<ToolStripItem>().Single(i => i.Text == "Gün");
            Assert.Equal(3, tabsItem.DropDownItems.Count);
            tabsItem.DropDownItems[2].PerformClick();
            Assert.Equal(2, segments.SelectedIndex);
            menu.Close();
            toolbar.SetOverflow(compact, TwToolbarOverflow.AsNeeded);

            // Genişleyince öğeler geri gelir; Always olanlar menüde kalır.
            form.ClientSize = new Size(1200, 400);
            Application.DoEvents();
            Assert.Equal(new Control[] { segments, export }, toolbar.OverflowItems);
            Assert.True(compact.Visible);
            Assert.Equal(title.GetPreferredSize(Size.Empty).Width, title.Width);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Setting_rows_lay_out_label_description_editor_and_unit_and_divide_a_group() => Sta.Run(() =>
    {
        using var form = NewForm(560);
        using var tw = new TailwindProvider();
        var group = new TwPanel { Dock = DockStyle.Top, Height = 200 };
        var first = new TwSettingRow { LabelText = "Mikro duruş sınırı", Description = "Daha kısa duruşlar hız kaybı sayılır", UnitText = "dk", Dock = DockStyle.Top };
        var box = new TwNumberBox();
        first.Controls.Add(box);
        var second = new TwSettingRow { LabelText = "Vardiya başlangıcı", Dock = DockStyle.Top };
        second.Controls.Add(new TwSelect { Items = { "06:00", "07:00" } });
        group.Controls.Add(second);
        group.Controls.Add(first);
        form.Controls.Add(group);
        tw.SetClasses(first, "setting");
        tw.SetClasses(second, "setting");
        form.Show();

        Assert.Same(box, first.Editor);
        Assert.True(first.CaptionLabel.Right <= box.Left);
        Assert.True(first.UnitLabel.Left >= box.Right);
        Assert.True(first.UnitLabel.Right <= first.Width - first.Padding.Right);
        Assert.Equal(TwDpiScaled(first, 160), box.Width);
        Assert.True(first.DescriptionLabel.Top >= first.CaptionLabel.Bottom);
        Assert.True(first.Height > second.Height);                                   // açıklama satırı uzatır
        Assert.False(first.HasDividerAbove);
        Assert.True(second.HasDividerAbove);
        Assert.NotEqual(first.CaptionLabel.ForeColor, first.DescriptionLabel.ForeColor);

        // Dar satırda düzenleyici metnin altına iner.
        form.ClientSize = new Size(220, 400);
        first.PerformLayout();
        Assert.True(box.Top >= first.DescriptionLabel.Bottom);
        using (Render(group)) { }
        Assert.Equal("setting", TwTheme.DefaultAutoStyle["TwSettingRow"]);
    });

    private static int TwDpiScaled(Control control, int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(control) / 96f);

    [Fact]
    public void Dialog_with_input_validates_before_closing_and_returns_the_text() => Sta.Run(() =>
    {
        using var dialog = TwDialog.Create(new TwDialogOptions
        {
            Message = "Kayıtlı arama adı",
            ShowInput = true,
            InputText = "  Hat 2  ",
            InputRequired = true,
            InputValidator = text => text == "Hat 2" ? "Bu ad kullanılıyor." : null,
        });
        Assert.NotNull(dialog.InputBox);
        Assert.Equal(new[] { DialogResult.Cancel, DialogResult.OK }, dialog.Buttons.Select(b => b.DialogResult).ToArray());
        string? shownError = null;
        int attempts = 0;
        dialog.Shown += (_, _) =>
        {
            var ok = dialog.Buttons.First(b => b.DialogResult == DialogResult.OK);
            ok.PerformClick();                                                       // geçersiz: açık kalır
            attempts++;
            shownError = dialog.InputField!.ErrorText;
            dialog.InputBox!.Text = "";
            ok.PerformClick();                                                       // boş: zorunlu
            attempts++;
            Assert.Equal(TwTexts.Current.Required, dialog.InputField.ErrorText);
            dialog.InputBox.Text = " Gece vardiyası ";
            ok.PerformClick();
        };
        Assert.Equal(DialogResult.OK, dialog.ShowDialog());
        Assert.Equal(2, attempts);
        Assert.Equal("Bu ad kullanılıyor.", shownError);
        Assert.Equal("Gece vardiyası", dialog.InputText);
    });

    [Fact]
    public void Dialog_input_cancel_closes_without_validation() => Sta.Run(() =>
    {
        using var dialog = TwDialog.Create(new TwDialogOptions { Message = "Ad", ShowInput = true, InputRequired = true });
        dialog.Shown += (_, _) => dialog.Buttons.First(b => b.DialogResult == DialogResult.Cancel).PerformClick();
        Assert.Equal(DialogResult.Cancel, dialog.ShowDialog());
        Assert.Null(dialog.InputField!.ErrorText);
    });
}
