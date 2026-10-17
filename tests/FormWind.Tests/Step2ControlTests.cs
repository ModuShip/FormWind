using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class Step2ControlTests
{
    private static int Argb(string hex) => ColorTranslator.FromHtml(hex).ToArgb();

    private static void Invoke(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { args.GetType() }, null)!
            .Invoke(control, new object[] { args });

    private static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    [Fact]
    public void Tab_control_shows_only_the_selected_page_and_syncs_titles() => Sta.Run(() =>
    {
        using var tabs = new TwTabControl { Size = new Size(400, 300) };
        var general = tabs.AddPage("Genel");
        var address = tabs.AddPage("Adres");
        var orders = tabs.AddPage("Siparişler");
        tabs.CreateControl();
        Assert.Equal(new[] { "Genel", "Adres", "Siparişler" }, tabs.Strip.Tabs);
        Assert.True(general.Visible);
        Assert.False(address.Visible);

        int changes = 0;
        tabs.SelectedIndexChanged += (_, _) => changes++;
        tabs.SelectedIndex = 2;
        Assert.Same(orders, tabs.SelectedTab);
        Assert.True(orders.Visible);
        Assert.False(general.Visible);
        Assert.Equal(1, changes);
        Assert.Equal(tabs.PageBounds, orders.Bounds);
        Assert.True(orders.Top >= tabs.Strip.Bottom);

        address.Text = "Adresler";
        Assert.Equal("Adresler", tabs.Strip.Tabs[1]);

        tabs.Controls.Remove(orders);
        orders.Dispose();
        Assert.Equal(2, tabs.Strip.Tabs.Count);
        Assert.Equal(1, tabs.SelectedIndex);
        Assert.True(address.Visible);
    });

    [Fact]
    public void Tab_control_cycles_pages_with_ctrl_tab() => Sta.Run(() =>
    {
        using var tabs = new TwTabControl { Size = new Size(400, 300) };
        tabs.AddPage("A");
        tabs.AddPage("B");
        var process = typeof(TwTabControl).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var message = new Message();
        process.Invoke(tabs, new object[] { message, Keys.Control | Keys.Tab });
        Assert.Equal(1, tabs.SelectedIndex);
        process.Invoke(tabs, new object[] { message, Keys.Control | Keys.Tab });
        Assert.Equal(0, tabs.SelectedIndex);
        process.Invoke(tabs, new object[] { message, Keys.Control | Keys.Shift | Keys.Tab });
        Assert.Equal(1, tabs.SelectedIndex);
    });

    [Fact]
    public void Slider_clamps_and_responds_to_keyboard_and_mouse() => Sta.Run(() =>
    {
        using var slider = new TwSlider { Size = new Size(200, 24), Minimum = 0, Maximum = 50, Value = 10 };
        int changes = 0;
        slider.ValueChanged += (_, _) => changes++;

        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.Right));
        Assert.Equal(11, slider.Value);
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.PageUp));
        Assert.Equal(21, slider.Value);
        Invoke(slider, "OnKeyDown", new KeyEventArgs(Keys.End));
        Assert.Equal(50, slider.Value);
        slider.Value = 999;
        Assert.Equal(50, slider.Value);

        Invoke(slider, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 0, 12, 0));
        Assert.Equal(0, slider.Value);
        Invoke(slider, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 100, 12, 0));
        Assert.InRange(slider.Value, 24, 26);
        Invoke(slider, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 100, 12, 0));
        Assert.Equal(5, changes);

        slider.Maximum = 20;
        Assert.Equal(20, slider.Value);
        Assert.Equal(AccessibleRole.Slider, slider.AccessibilityObject.Role);
        Assert.Equal("20", slider.AccessibilityObject.Value);
    });

    [Fact]
    public void Slider_and_progress_colors_come_from_classes() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var panel = new Panel { BackColor = Color.White, Size = new Size(300, 100) };
        var slider = new TwSlider { Size = new Size(200, 24), Value = 50 };
        var progress = new TwProgress { Size = new Size(200, 8), Top = 40, Value = 25 };
        panel.Controls.AddRange(new Control[] { slider, progress });
        provider.SetClasses(slider, "fill-gray-300 accent-red-600 item:bg-gray-50");
        provider.SetClasses(progress, "fill-gray-100 accent-green-600");
        Assert.Equal(Argb("#d1d5db"), slider.TrackColor.ToArgb());
        Assert.Equal(Argb("#dc2626"), slider.AccentColor.ToArgb());
        Assert.Equal(Argb("#f9fafb"), slider.ThumbColor.ToArgb());

        using var bitmap = Render(progress);
        Assert.Equal(Argb("#16a34a"), bitmap.GetPixel(25, 4).ToArgb());   // dolu kısım (%25 = 50 px)
        Assert.Equal(Argb("#f3f4f6"), bitmap.GetPixel(150, 4).ToArgb());  // ray
        Assert.Equal("%25", progress.AccessibilityObject.Value);
    });

    [Fact]
    public void Indeterminate_progress_slides_a_segment() => Sta.Run(() =>
    {
        using var progress = new TwProgress { Size = new Size(200, 8), Indeterminate = true };
        progress.Phase = 0;
        Assert.True(progress.BarBounds.Width == 0 || progress.BarBounds.Left == 0);
        progress.Phase = 0.5f;
        var middle = progress.BarBounds;
        Assert.True(middle.Left > 0 && middle.Right < 200);
        progress.Phase = 1.25f;
        Assert.Equal(0.25f, progress.Phase, 3);
        Assert.Equal("", progress.AccessibilityObject.Value);
    });

    [Fact]
    public void Select_filters_with_culture_and_selects_by_typing() => Sta.Run(() =>
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            Assert.True(TwSelect.Matches("İzmir", "izm"));
            Assert.True(TwSelect.Matches("Şanlıurfa", "sanli"));      // aksan farkı yok sayılır
            Assert.False(TwSelect.Matches("Ankara", "izm"));
            Assert.True(TwSelect.Matches("Ankara", " "));

            using var select = new TwSelect { Size = new Size(160, 30) };
            select.Items.AddRange(new[] { "Adana", "Ankara", "Antalya", "Bursa", "İzmir" });
            select.SelectedIndex = 3;

            void Type(char c) => Invoke(select, "OnKeyPress", new KeyPressEventArgs(c));
            Type('a');
            Assert.Equal(0, select.SelectedIndex);   // Adana
            Type('a');
            Assert.Equal(1, select.SelectedIndex);   // aynı harf: sonraki A
            Type('n');
            Assert.Equal(1, select.SelectedIndex);   // "an" → Ankara (birikmiş önek)
            Type('t');
            Assert.Equal(2, select.SelectedIndex);   // "ant" → Antalya
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    });

    [Fact]
    public void Searchable_select_filters_menu_items() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) };
        var select = new TwSelect { Size = new Size(160, 30), Searchable = true };
        select.Items.AddRange(new[] { "Adana", "Ankara", "Bursa", "İzmir" });
        form.Controls.Add(select);
        form.Show();
        try
        {
            select.ShowDropDown();
            Assert.NotNull(select.SearchBox);
            select.ApplyFilter("an");
            Assert.Equal(new[] { 0, 1 }, select.VisibleItemIndices().ToArray());
            select.ApplyFilter("");
            Assert.Equal(4, select.VisibleItemIndices().Count());
            select.DropDownMenu!.Close();
        }
        finally
        {
            form.Close();
        }
    });
}
