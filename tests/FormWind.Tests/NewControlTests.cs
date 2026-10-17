using System;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class NewControlTests
{
    private static int Argb(string hex) => ColorTranslator.FromHtml(hex).ToArgb();

    private static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    [Fact]
    public void Dark_scrollbars_follow_the_provider() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var panel = new Panel { AutoScroll = true };
        _ = panel.Handle;
        provider.SetClasses(panel, "bg-white dark:bg-gray-900");

        provider.DarkMode = true;
        Assert.True(TwNativeTheme.IsDark(panel));
        provider.DarkMode = false;
        Assert.False(TwNativeTheme.IsDark(panel));

        provider.DarkScrollbars = false;
        provider.DarkMode = true;
        Assert.False(TwNativeTheme.IsDark(panel));
    });

    [Fact]
    public void Tab_strip_selects_with_mouse_and_keyboard_and_measures_itself() => Sta.Run(() =>
    {
        using var tabs = new TwTabStrip { Size = new Size(300, 40), Font = new Font("Segoe UI", 9f) };
        tabs.Tabs.AddRange(new[] { "Genel", "Adres", "Siparişler" });
        int changes = 0;
        tabs.SelectedIndexChanged += (_, _) => changes++;

        var third = tabs.GetTabBounds(2);
        typeof(Control).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(tabs, new object[] { new MouseEventArgs(MouseButtons.Left, 1, third.X + 3, third.Y + 3, 0) });
        Assert.Equal(2, tabs.SelectedIndex);

        typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(tabs, new object[] { new KeyEventArgs(Keys.Home) });
        Assert.Equal(0, tabs.SelectedIndex);
        Assert.Equal(2, changes);

        var preferred = tabs.GetPreferredSize(Size.Empty);
        Assert.True(preferred.Width > TextRenderer.MeasureText("GenelAdresSiparişler", tabs.Font).Width);
    });

    [Fact]
    public void Tab_strip_colors_come_from_parts_and_underline_is_drawn() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var tabs = new TwTabStrip { Size = new Size(300, 40), BackColor = Color.White };
        tabs.Tabs.AddRange(new[] { "Genel", "Adres" });
        provider.SetClasses(tabs, "text-gray-500 selected:text-gray-900 selected:border-red-600 item:hover:text-gray-700 gap-8");

        Assert.Equal(Argb("#dc2626"), tabs.Colors.Accent.ToArgb());
        Assert.Equal(32, tabs.Gap);

        using var bitmap = Render(tabs);
        var first = tabs.GetTabBounds(0);
        Assert.Equal(Argb("#dc2626"), bitmap.GetPixel(first.X + first.Width / 2, first.Bottom - 1).ToArgb());
    });

    [Fact]
    public void Number_box_clamps_steps_and_parses_with_culture() => Sta.Run(() =>
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            using var box = new TwNumberBox { Minimum = 0, Maximum = 10, DecimalPlaces = 1, Increment = 0.5m };
            int changes = 0;
            box.ValueChanged += (_, _) => changes++;

            box.Step(1);
            Assert.Equal(0.5m, box.Value);
            Assert.Equal("0,5", box.InnerTextBox.Text);

            box.InnerTextBox.Text = "7,25";
            box.Commit();
            Assert.Equal(7.2m, box.Value); // 1 ondalık basamağa yuvarlanır (banker's rounding)

            box.InnerTextBox.Text = "99";
            box.Commit();
            Assert.Equal(10m, box.Value);

            box.InnerTextBox.Text = "abc";
            box.Commit();
            Assert.Equal("10,0", box.InnerTextBox.Text);
            Assert.Equal(3, changes);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    });

    [Fact]
    public void Number_box_text_area_leaves_room_for_the_stepper()
    {
        using var box = new TwNumberBox { Size = new Size(120, 32) };
        box.PerformLayout();

        Assert.True(box.InnerTextBox.Right <= 120 - 20, $"metin kutusu sağ kenarı {box.InnerTextBox.Right}");
    }

    [Fact]
    public void Tooltip_resolves_classes_with_provider_dark_mode()
    {
        using var provider = new TailwindProvider();
        using var tip = new TwToolTip { Provider = provider };

        Assert.Equal(Argb("#111827"), tip.ResolveStyle().BackColor!.Value.ToArgb());
        provider.DarkMode = true;
        Assert.Equal(Argb("#f3f4f6"), tip.ResolveStyle().BackColor!.Value.ToArgb());

        var size = tip.MeasureTip("Kaydet (Ctrl+S)");
        Assert.True(size.Width > TextRenderer.MeasureText("Kaydet (Ctrl+S)", SystemFonts.StatusFont!).Width / 2);
        Assert.True(size.Height >= 12);
    }

    private static void Invoke(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { args.GetType() }, null)!
            .Invoke(control, new object[] { args });

    [Fact]
    public void Calendar_starts_weeks_on_the_culture_first_day() => Sta.Run(() =>
    {
        using var calendar = new TwCalendar { DisplayMonth = new DateTime(2026, 10, 1) }; // 1 Ekim 2026 perşembe
        calendar.Culture = new CultureInfo("tr-TR");
        Assert.Equal(new DateTime(2026, 9, 28), calendar.FirstVisibleDate);   // pazartesi
        calendar.Culture = new CultureInfo("en-US");
        Assert.Equal(new DateTime(2026, 9, 27), calendar.FirstVisibleDate);   // pazar
        Assert.Equal(calendar.GetDayBounds(new DateTime(2026, 9, 27)).Y, calendar.GetDayBounds(new DateTime(2026, 10, 3)).Y);
        Assert.True(calendar.GetDayBounds(new DateTime(2026, 10, 4)).Y > calendar.GetDayBounds(new DateTime(2026, 10, 3)).Y);
    });

    [Fact]
    public void Calendar_keyboard_moves_focus_across_months_and_selects() => Sta.Run(() =>
    {
        using var calendar = new TwCalendar { Culture = new CultureInfo("tr-TR"), SelectedDate = new DateTime(2026, 10, 31) };
        int selected = 0, cancelled = 0;
        calendar.DateSelected += (_, _) => selected++;
        calendar.Cancelled += (_, _) => cancelled++;

        Invoke(calendar, "OnKeyDown", new KeyEventArgs(Keys.Right));    // 1 Kasım
        Assert.Equal(new DateTime(2026, 11, 1), calendar.DisplayMonth);
        Invoke(calendar, "OnKeyDown", new KeyEventArgs(Keys.End));      // 30 Kasım
        Invoke(calendar, "OnKeyDown", new KeyEventArgs(Keys.Enter));
        Assert.Equal(new DateTime(2026, 11, 30), calendar.SelectedDate);
        Assert.Equal(1, selected);

        Invoke(calendar, "OnKeyDown", new KeyEventArgs(Keys.PageUp));   // 30 Ekim
        Assert.Equal(new DateTime(2026, 10, 1), calendar.DisplayMonth);
        Invoke(calendar, "OnKeyDown", new KeyEventArgs(Keys.Escape));
        Assert.Equal(1, cancelled);
    });

    [Fact]
    public void Calendar_click_selects_a_day_and_respects_the_range() => Sta.Run(() =>
    {
        using var calendar = new TwCalendar { Culture = new CultureInfo("tr-TR"), DisplayMonth = new DateTime(2026, 10, 1), MinDate = new DateTime(2026, 10, 10) };
        var early = calendar.GetDayBounds(new DateTime(2026, 10, 5));
        Invoke(calendar, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, early.X + 5, early.Y + 5, 0));
        Assert.Null(calendar.SelectedDate);

        var day = calendar.GetDayBounds(new DateTime(2026, 10, 15));
        Invoke(calendar, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, day.X + 5, day.Y + 5, 0));
        Assert.Equal(new DateTime(2026, 10, 15), calendar.SelectedDate);

        var next = calendar.NextButtonBounds;
        Invoke(calendar, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, next.X + 3, next.Y + 3, 0));
        Assert.Equal(new DateTime(2026, 11, 1), calendar.DisplayMonth);
    });

    [Fact]
    public void Calendar_colors_come_from_parts_and_selected_day_is_filled() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var calendar = new TwCalendar { Culture = new CultureInfo("tr-TR"), SelectedDate = new DateTime(2026, 10, 15) };
        provider.SetClasses(calendar, "menu:bg-white selected:bg-red-600 selected:text-white item:hover:bg-gray-100 dark:menu:bg-gray-800");
        Assert.Equal(Argb("#dc2626"), calendar.Colors.Accent.ToArgb());
        Assert.Equal(Argb("#f3f4f6"), calendar.Colors.Hover.ToArgb());
        provider.DarkMode = true;
        Assert.Equal(Argb("#1f2937"), calendar.Colors.Back.ToArgb());

        using var bitmap = Render(calendar);
        var cell = calendar.GetDayBounds(new DateTime(2026, 10, 15));
        Assert.Equal(Argb("#dc2626"), bitmap.GetPixel(cell.X + cell.Width / 2, cell.Y + 6).ToArgb());
        Assert.Equal(Argb("#1f2937"), bitmap.GetPixel(3, 3).ToArgb());
    });

    [Fact]
    public void Date_picker_parses_typed_text_with_culture_and_keeps_range() => Sta.Run(() =>
    {
        using var picker = new TwDatePicker { Culture = new CultureInfo("tr-TR"), MinDate = new DateTime(2026, 1, 1) };
        int changes = 0;
        picker.ValueChanged += (_, _) => changes++;

        picker.InnerTextBox.Text = "15.10.2026";
        picker.Commit();
        Assert.Equal(new DateTime(2026, 10, 15), picker.Value);
        Assert.Equal("15.10.2026", picker.InnerTextBox.Text);

        picker.InnerTextBox.Text = "yarın";             // geçersiz: önceki değer geri yazılır
        picker.Commit();
        Assert.Equal("15.10.2026", picker.InnerTextBox.Text);

        picker.InnerTextBox.Text = "01.06.2020";        // aralık dışı
        picker.Commit();
        Assert.Equal(new DateTime(2026, 10, 15), picker.Value);

        picker.InnerTextBox.Text = "";
        picker.Commit();
        Assert.Null(picker.Value);
        Assert.Equal(2, changes);

        picker.Format = "d MMMM yyyy";
        picker.Value = new DateTime(2026, 3, 5);
        Assert.Equal("5 Mart 2026", picker.InnerTextBox.Text);
    });

    [Fact]
    public void Date_picker_text_area_leaves_room_for_the_icon() => Sta.Run(() =>
    {
        using var picker = new TwDatePicker { Size = new Size(200, 36) };
        using var box = new TwTextBox { Size = new Size(200, 36) };
        Assert.True(picker.DisplayRectangle.Width <= box.DisplayRectangle.Width - 24);
    });

    [Fact]
    public void Data_list_draws_only_visible_rows() => Sta.Run(() =>
    {
        using var list = new TwDataList { Size = new Size(300, 208), Padding = new Padding(4), ItemHeight = 40, ItemCount = 1_000_000 };
        int drawn = 0, maxIndex = -1;
        list.DrawItem += (_, e) =>
        {
            drawn++;
            maxIndex = Math.Max(maxIndex, e.Index);
        };
        using (Render(list)) { }
        Assert.Equal(5, drawn);   // (208 - 8) / 40
        Assert.Equal(4, maxIndex);

        list.ScrollTo(40_000_000 - 10, animate: false);
        Assert.Equal(list.MaxScrollOffset, list.ScrollOffset);
        drawn = 0;
        using (Render(list)) { }
        Assert.InRange(drawn, 5, 6);
        Assert.Equal(999_999, maxIndex);
    });

    [Fact]
    public void Data_list_keyboard_selects_and_keeps_selection_visible() => Sta.Run(() =>
    {
        using var list = new TwDataList { Size = new Size(300, 208), Padding = new Padding(4), ItemHeight = 40, ItemCount = 100, SmoothScrolling = false };
        int changes = 0, activations = 0;
        list.SelectedIndexChanged += (_, _) => changes++;
        list.ItemActivate += (_, _) => activations++;

        Invoke(list, "OnKeyDown", new KeyEventArgs(Keys.Down));
        Assert.Equal(0, list.SelectedIndex);
        Invoke(list, "OnKeyDown", new KeyEventArgs(Keys.PageDown));
        Assert.Equal(4, list.SelectedIndex);
        Invoke(list, "OnKeyDown", new KeyEventArgs(Keys.End));
        Assert.Equal(99, list.SelectedIndex);
        var bounds = list.GetItemBounds(99);
        Assert.True(bounds.Bottom <= list.ClientSize.Height - list.Padding.Bottom && bounds.Top >= 0);
        Invoke(list, "OnKeyDown", new KeyEventArgs(Keys.Home));
        Assert.Equal(0, list.ScrollOffset);
        Invoke(list, "OnKeyDown", new KeyEventArgs(Keys.Enter));
        Assert.Equal(4, changes);
        Assert.Equal(1, activations);

        list.ItemCount = 10;
        Assert.Equal(0, list.SelectedIndex);
        list.SelectedIndex = 9;
        list.ItemCount = 3;
        Assert.Equal(2, list.SelectedIndex);
    });

    [Fact]
    public void Data_list_maps_points_and_scrolls_with_the_wheel() => Sta.Run(() =>
    {
        using var list = new TwDataList { Size = new Size(300, 208), Padding = new Padding(4), ItemHeight = 40, ItemCount = 50, SmoothScrolling = false };
        Assert.Equal(-1, list.IndexFromPoint(new Point(50, 2)));
        Assert.Equal(0, list.IndexFromPoint(new Point(50, 10)));
        Assert.Equal(2, list.IndexFromPoint(new Point(50, 4 + 80 + 5)));

        Invoke(list, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 50, 50, -120));
        Assert.Equal(Math.Max(1, SystemInformation.MouseWheelScrollLines) * 40, list.ScrollOffset);
        Assert.True(list.TopIndex > 0);

        var row = list.GetItemBounds(list.TopIndex + 1);
        Invoke(list, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, row.X + 10, row.Y + 10, 0));
        Assert.Equal(list.IndexFromPoint(new Point(row.X + 10, row.Y + 10)), list.SelectedIndex);

        Invoke(list, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 50, 50, 120 * 100));
        Assert.Equal(0, list.ScrollOffset);
    });

    [Fact]
    public void Data_list_colors_come_from_parts_and_selection_is_painted() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var list = new TwDataList { Size = new Size(300, 208), ItemCount = 20, ItemText = i => "Satır " + i };
        provider.SetClasses(list, "p-1 bg-white text-gray-900 item:hover:bg-gray-100 item:rounded-lg selected:bg-blue-600 selected:text-white row:border-gray-200 fill-gray-400 "
                                  + "dark:bg-gray-900 dark:text-gray-100");
        Assert.Equal(8, list.ItemRadius);
        Assert.Equal(Argb("#e5e7eb"), list.Colors.Divider.ToArgb());
        Assert.Equal(Argb("#9ca3af"), list.Colors.Thumb.ToArgb());
        Assert.Equal(new Padding(4), list.Padding);

        list.SelectedIndex = 1;
        using (var bitmap = Render(list))
        {
            var row = list.GetItemBounds(1);
            Assert.Equal(Argb("#2563eb"), bitmap.GetPixel(row.X + row.Width / 2, row.Y + 2).ToArgb());
            Assert.Equal(Argb("#ffffff"), bitmap.GetPixel(row.X + row.Width / 2, list.GetItemBounds(3).Y + 2).ToArgb());
        }

        provider.DarkMode = true;
        Assert.Equal(Argb("#111827"), list.Colors.Back.ToArgb());
        Assert.Equal(Argb("#f3f4f6"), list.Colors.Fore.ToArgb());
    });
}
