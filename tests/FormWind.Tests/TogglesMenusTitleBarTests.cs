using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class TogglesMenusTitleBarTests
{
    private static int Argb(string hex) => ColorTranslator.FromHtml(hex).ToArgb();

    private static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    [Fact]
    public void Parses_accent_and_fill_colors()
    {
        var style = new TwTheme().Parse("accent-green-600 fill-gray-100").Resolve(TwState.None);

        Assert.Equal(Argb("#16a34a"), style.AccentColor!.Value.ToArgb());
        Assert.Equal(Argb("#f3f4f6"), style.FillColor!.Value.ToArgb());
    }

    [Fact]
    public void Checkbox_box_uses_accent_when_checked_and_fill_when_not() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var box = new TwCheckBox { Text = "", Size = new Size(30, 24), BackColor = Color.White };
        provider.SetClasses(box, "accent-green-600 fill-gray-100");

        using (var unchecked_ = Render(box))
            Assert.Equal(Argb("#f3f4f6"), unchecked_.GetPixel(5, 12).ToArgb());

        box.Checked = true;
        using var checkedBitmap = Render(box);
        Assert.Equal(Argb("#16a34a"), checkedBitmap.GetPixel(8, 6).ToArgb()); // kutunun üstü, onay işaretinden uzak
    });

    [Fact]
    public void Toggle_preferred_size_includes_indicator_gap_and_text()
    {
        using var box = new TwCheckBox { Text = "Kabul ediyorum", Font = new Font("Segoe UI", 9f) };
        using var sw = new TwSwitch { Text = "", Font = new Font("Segoe UI", 9f) };

        Assert.True(box.GetPreferredSize(Size.Empty).Width > 16 + 8 + 40);
        Assert.Equal(new Size(44, 24), sw.GetPreferredSize(Size.Empty));
    }

    [Fact]
    public void Switch_track_follows_checked_state() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var sw = new TwSwitch { Size = new Size(44, 24), BackColor = Color.White };
        provider.SetClasses(sw, "accent-blue-600 fill-gray-200");

        using (var off = Render(sw))
            Assert.Equal(Argb("#e5e7eb"), off.GetPixel(38, 12).ToArgb()); // düğme solda, sağ uç ray rengi

        // Handle oluştuğu için düğme kayarak (150 ms) ilerler.
        sw.Checked = true;
        Assert.True(sw.ThumbPosition < 1f);
        var deadline = System.DateTime.UtcNow.AddSeconds(2);
        while (sw.ThumbPosition < 1f && System.DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(10);
        }
        Assert.Equal(1f, sw.ThumbPosition);
        using var on = Render(sw);
        Assert.Equal(Argb("#2563eb"), on.GetPixel(6, 12).ToArgb()); // düğme sağda, sol uç ray rengi
    });

    [Fact]
    public void Radio_draws_accent_circle_when_checked() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var radio = new TwRadioButton { Size = new Size(30, 24), BackColor = Color.White, Checked = true };
        provider.SetClasses(radio, "accent-red-600");

        using var bitmap = Render(radio);
        Assert.Equal(Argb("#dc2626"), bitmap.GetPixel(2, 12).ToArgb());   // halkanın içi
        Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(8, 12).ToArgb()); // ortadaki beyaz nokta
    });

    [Fact]
    public void ToolStrip_applier_builds_a_themed_renderer()
    {
        using var provider = new TailwindProvider();
        using var menu = new MenuStrip();
        provider.SetClasses(menu, "bg-gray-900 text-gray-100 border-b border-gray-700 menu:bg-gray-800 menu:border-gray-600 item:hover:bg-gray-700 item:hover:text-white item:rounded-md divide-gray-600");

        var renderer = Assert.IsType<TwToolStripRenderer>(menu.Renderer);
        var colors = renderer.Colors;
        Assert.Equal(Argb("#111827"), colors.Back.ToArgb());
        Assert.Equal(Argb("#f3f4f6"), colors.Fore.ToArgb());
        Assert.Equal(Argb("#1f2937"), colors.MenuBack.ToArgb());
        Assert.Equal(Argb("#4b5563"), colors.MenuBorder.ToArgb());
        Assert.Equal(Argb("#374151"), colors.ItemHoverBack.ToArgb());
        Assert.Equal(Color.White.ToArgb(), colors.ItemHoverFore.ToArgb());
        Assert.Equal(Argb("#4b5563"), colors.Separator.ToArgb());
        Assert.Equal(6f, colors.ItemRadius);
        Assert.Equal(Argb("#374151"), colors.BottomBorder!.Value.ToArgb());
    }

    [Fact]
    public void Menu_strip_renders_with_theme_background() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var menu = new MenuStrip { Size = new Size(200, 24) };
        menu.Items.Add("Dosya");
        provider.SetClasses(menu, "bg-gray-900 text-white");

        using var bitmap = Render(menu);
        Assert.Equal(Argb("#111827"), bitmap.GetPixel(190, 12).ToArgb());
    });

    [Fact]
    public void Form_title_bar_follows_dark_mode_and_caption_part() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        provider.SetClasses(form, "bg-white caption:bg-white dark:caption:bg-gray-900 dark:caption:text-white");
        form.Show();

        var bar = TwTitleBar.For(form);
        Assert.False(bar.Dark);
        Assert.Equal(Color.White.ToArgb(), bar.Caption!.Value.ToArgb());

        provider.DarkMode = true;
        Assert.True(bar.Dark);
        Assert.Equal(Argb("#111827"), bar.Caption!.Value.ToArgb());
        Assert.Equal(Color.White.ToArgb(), bar.Text!.Value.ToArgb());
        if (System.Environment.OSVersion.Version.Build >= 22000) Assert.True(bar.Applied); // Windows 11
        form.Close();
    });

    private static MdiClient Client(Form form) => form.Controls.OfType<MdiClient>().Single();

    [Fact]
    public void Mdi_workspace_follows_form_background_and_is_restored() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { IsMdiContainer = true };
        provider.SetClasses(form, "bg-gray-50 dark:bg-gray-900");
        Assert.Equal(Argb("#f9fafb"), Client(form).BackColor.ToArgb());

        provider.DarkMode = true;
        Assert.Equal(Argb("#111827"), Client(form).BackColor.ToArgb());

        provider.SetClasses(form, "");
        Assert.Equal(SystemColors.AppWorkspace.ToArgb(), Client(form).BackColor.ToArgb());
    });

    [Fact]
    public void Mdi_workspace_is_styled_when_container_is_enabled_later() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        provider.SetClasses(form, "bg-gray-900");
        form.IsMdiContainer = true;
        Assert.Equal(Argb("#111827"), Client(form).BackColor.ToArgb());
    });

    [Fact]
    public void Mdi_workspace_color_assigned_by_app_is_kept() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var before = new Form { IsMdiContainer = true };
        Client(before).BackColor = Color.Teal;
        provider.SetClasses(before, "bg-gray-900");
        Assert.Equal(Color.Teal.ToArgb(), Client(before).BackColor.ToArgb());

        using var after = new Form { IsMdiContainer = true };
        provider.SetClasses(after, "bg-white dark:bg-gray-900");
        Client(after).BackColor = Color.Teal;
        provider.DarkMode = true;
        Assert.Equal(Color.Teal.ToArgb(), Client(after).BackColor.ToArgb());
    });

    [Fact]
    public void Maximized_mdi_child_control_box_glyphs_use_strip_text_color() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { IsMdiContainer = true, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), Size = new Size(600, 400), ShowInTaskbar = false };
        var menu = new MenuStrip();
        menu.Items.Add("File");
        form.Controls.Add(menu);
        form.MainMenuStrip = menu;
        provider.SetClasses(menu, "bg-gray-900 text-white");
        form.Show();
        var child = new Form { MdiParent = form, WindowState = FormWindowState.Maximized };
        child.Show();
        Application.DoEvents();

        // Küçült, geri yükle ve kapat düğmeleri.
        var boxes = menu.Items.Cast<ToolStripItem>().Where(i => i.GetType().Name == "ControlBoxMenuItem" && i.Alignment == ToolStripItemAlignment.Right).ToList();
        Assert.Equal(3, boxes.Count);
        using var bitmap = Render(menu);
        foreach (var box in boxes)
        {
            var r = box.Bounds;
            int bright = 0, dark = 0;
            for (int x = r.Left; x < r.Right; x++)
                for (int y = r.Top; y < r.Bottom; y++)
                {
                    float b = bitmap.GetPixel(x, y).GetBrightness();
                    if (b > 0.8f) bright++;
                    else if (b < 0.05f) dark++;
                }
            Assert.True(bright > 0, "glif şeridin yazı rengiyle çizilmeli");
            Assert.Equal(0, dark); // siyah sistem glifi kalmamalı (zemin gray-900, parlaklığı ~0.12)
        }
        form.Close();
    });

    [Fact]
    public void Hosted_combo_and_text_boxes_follow_the_strip_colors() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var strip = new ToolStrip();
        var combo = new ToolStripComboBox();
        var text = new ToolStripTextBox();
        var menu = new ToolStripDropDownButton("Ara");
        var search = new ToolStripTextBox();
        menu.DropDownItems.Add(search);
        strip.Items.AddRange(new ToolStripItem[] { combo, menu });
        provider.SetClasses(strip, "bg-white text-gray-800 dark:bg-gray-800 dark:text-gray-100");
        strip.Items.Add(text); // sonradan eklenen

        Assert.Equal(Color.White.ToArgb(), combo.ComboBox.BackColor.ToArgb());
        Assert.Equal(Argb("#1f2937"), combo.ComboBox.ForeColor.ToArgb());

        provider.DarkMode = true;
        foreach (var control in new Control[] { combo.ComboBox, text.TextBox, search.TextBox })
        {
            Assert.True(control.BackColor.GetBrightness() < 0.3f, $"{control.GetType().Name} koyu şeritte açık kaldı: {control.BackColor}");
            Assert.Equal(Argb("#f3f4f6"), control.ForeColor.ToArgb());
        }

        provider.SetClasses(strip, "");
        Assert.Equal(SystemColors.Window.ToArgb(), combo.ComboBox.BackColor.ToArgb());
        Assert.Equal(SystemColors.WindowText.ToArgb(), combo.ComboBox.ForeColor.ToArgb());
    });

    [Fact]
    public void Hosted_box_colors_assigned_by_app_are_kept() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var strip = new ToolStrip();
        var before = new ToolStripTextBox { BackColor = Color.LightYellow };
        var after = new ToolStripComboBox();
        strip.Items.AddRange(new ToolStripItem[] { before, after });
        provider.SetClasses(strip, "bg-white dark:bg-gray-800 dark:text-gray-100");
        Assert.Equal(Color.LightYellow.ToArgb(), before.TextBox.BackColor.ToArgb());

        after.ComboBox.BackColor = Color.Pink;
        provider.DarkMode = true;
        Assert.Equal(Color.Pink.ToArgb(), after.ComboBox.BackColor.ToArgb());
    });

    // WinForms, profesyonel olmayan renderer'lı şeritte açılır kutunun ok düğmesini sistem renkleriyle çizer.
    [Fact]
    public void Hosted_combo_button_and_frame_are_painted_with_the_box_colors() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var strip = new ToolStrip();
        var combo = new ToolStripComboBox();
        strip.Items.Add(combo);
        provider.SetClasses(strip, "dark:bg-gray-800 dark:text-gray-100");
        Assert.True(TwHostedComboPainter.IsAttached(combo.ComboBox));

        var box = combo.ComboBox;
        box.Size = new Size(120, 23);
        var client = box.ClientSize;
        using var bitmap = new Bitmap(client.Width, client.Height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(SystemColors.Control); // WinForms'un çizdiği açık gri düğme
            TwHostedComboPainter.Paint(g, box);
        }
        int light = 0;
        for (int x = client.Width - SystemInformation.HorizontalScrollBarArrowWidth - 1; x < client.Width; x++)
            for (int y = 0; y < client.Height; y++)
                if (bitmap.GetPixel(x, y).ToArgb() == SystemColors.Control.ToArgb()) light++;
        Assert.Equal(0, light);
        Assert.Equal(box.BackColor.ToArgb(), bitmap.GetPixel(client.Width - 4, 3).ToArgb());

        provider.SetClasses(strip, "");
        Assert.False(TwHostedComboPainter.IsAttached(combo.ComboBox));
    });
}
