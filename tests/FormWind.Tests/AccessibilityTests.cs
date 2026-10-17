using System;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class AccessibilityTests
{
    private static void Invoke(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { args.GetType() }, null)!
            .Invoke(control, new object[] { args });

    [Fact]
    public void Data_list_exposes_virtual_rows_as_list_items() => Sta.Run(() =>
    {
        using var list = new TwDataList { Size = new Size(300, 208), ItemCount = 1_000_000, SmoothScrolling = false };
        var accessible = list.AccessibilityObject;
        Assert.Equal(AccessibleRole.List, accessible.Role);
        Assert.Equal(1_000_000, accessible.GetChildCount());

        var row = accessible.GetChild(41)!;
        Assert.Equal(AccessibleRole.ListItem, row.Role);
        Assert.Equal("Öğe 42", row.Name);
        Assert.True(row.State.HasFlag(AccessibleStates.Offscreen));

        list.AccessibleItemText = i => "Sipariş " + i;
        row.Select(AccessibleSelection.TakeSelection);
        Assert.Equal(41, list.SelectedIndex);
        Assert.Equal("Sipariş 41", accessible.GetSelected()!.Name);
        Assert.True(accessible.GetChild(41)!.State.HasFlag(AccessibleStates.Selected));
        Assert.False(accessible.GetChild(41)!.State.HasFlag(AccessibleStates.Offscreen));   // seçince görünür kaydırıldı

        int activations = 0;
        list.ItemActivate += (_, _) => activations++;
        accessible.GetChild(3)!.DoDefaultAction();
        Assert.Equal(3, list.SelectedIndex);
        Assert.Equal(1, activations);
    });

    [Fact]
    public void Tab_strip_exposes_page_tabs() => Sta.Run(() =>
    {
        using var tabs = new TwTabStrip { Size = new Size(300, 40) };
        tabs.Tabs.AddRange(new[] { "Genel", "Adres", "Siparişler" });
        var accessible = tabs.AccessibilityObject;
        Assert.Equal(AccessibleRole.PageTabList, accessible.Role);
        Assert.Equal(3, accessible.GetChildCount());
        Assert.Equal("Adres", accessible.GetChild(1)!.Name);
        Assert.Equal(AccessibleRole.PageTab, accessible.GetChild(1)!.Role);

        accessible.GetChild(2)!.DoDefaultAction();
        Assert.Equal(2, tabs.SelectedIndex);
        Assert.True(accessible.GetChild(2)!.State.HasFlag(AccessibleStates.Selected));
        Assert.False(accessible.GetChild(0)!.State.HasFlag(AccessibleStates.Selected));

        tabs.AccessibleRole = AccessibleRole.ButtonDropDownGrid;    // kullanıcının verdiği rol önceliklidir
        Assert.Equal(AccessibleRole.ButtonDropDownGrid, tabs.AccessibilityObject.Role);
    });

    [Fact]
    public void Calendar_exposes_navigation_buttons_and_named_days() => Sta.Run(() =>
    {
        using var calendar = new TwCalendar { Culture = new CultureInfo("tr-TR"), DisplayMonth = new DateTime(2026, 10, 1), MinDate = new DateTime(2026, 10, 5) };
        var accessible = calendar.AccessibilityObject;
        Assert.Equal("Ekim 2026", accessible.Name);
        Assert.Equal(44, accessible.GetChildCount());
        Assert.Equal("Önceki ay", accessible.GetChild(0)!.Name);
        Assert.Equal(AccessibleRole.PushButton, accessible.GetChild(1)!.Role);

        // 28 Eylül pazartesi ilk hücre; 15 Ekim = 17. gün → çocuk 2 + 17
        var day = accessible.GetChild(2 + 17)!;
        Assert.Equal("15 Ekim 2026 Perşembe", day.Name);
        Assert.Equal(AccessibleRole.Cell, day.Role);
        Assert.True(accessible.GetChild(2 + 3)!.State.HasFlag(AccessibleStates.Unavailable));   // 1 Ekim < MinDate

        day.DoDefaultAction();
        Assert.Equal(new DateTime(2026, 10, 15), calendar.SelectedDate);
        Assert.Equal("15 Ekim 2026 Perşembe", accessible.Value);

        accessible.GetChild(1)!.DoDefaultAction();
        Assert.Equal(new DateTime(2026, 11, 1), calendar.DisplayMonth);
        Assert.Equal("Kasım 2026", accessible.Name);
    });

    [Fact]
    public void Select_is_a_combo_box_with_the_selected_value() => Sta.Run(() =>
    {
        using var select = new TwSelect();
        select.Items.AddRange(new[] { "İstanbul", "Ankara" });
        select.SelectedIndex = 0;
        var accessible = select.AccessibilityObject;
        Assert.Equal(AccessibleRole.ComboBox, accessible.Role);
        Assert.Equal("İstanbul", accessible.Value);
        Assert.True(accessible.State.HasFlag(AccessibleStates.Collapsed));

        accessible.Value = "Ankara";
        Assert.Equal(1, select.SelectedIndex);
    });

    [Fact]
    public void Text_box_inner_editor_reads_the_wrapper_name() => Sta.Run(() =>
    {
        using var box = new TwNumberBox { AccessibleName = "Adet", AccessibleDescription = "1–99 arası" };
        var inner = box.InnerTextBox.AccessibilityObject;
        Assert.Equal("Adet", inner.Name);
        Assert.Equal("1–99 arası", inner.Description);

        using var picker = new TwDatePicker { PlaceholderText = "Teslim tarihi" };
        Assert.Equal("Teslim tarihi", picker.InnerTextBox.AccessibilityObject.Name);

        using var picture = new TwPictureBox();
        Assert.Equal(AccessibleRole.Graphic, picture.AccessibilityObject.Role);
    });

    [Fact]
    public void Focus_visible_variant_needs_keyboard_focus()
    {
        var parsed = TwParser.Parse("border-gray-300 focus-visible:border-blue-600", TwTheme.Current);
        Assert.Equal(ColorTranslator.FromHtml("#d1d5db"), parsed.Resolve(TwState.Focus).BorderColor);
        Assert.Equal(ColorTranslator.FromHtml("#2563eb"), parsed.Resolve(TwState.Focus | TwState.FocusVisible).BorderColor);
    }

    [Fact]
    public void Focus_visible_follows_the_last_input() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) };
        using var provider = new TailwindProvider();
        var first = new TwButton();
        var box = new Button { Top = 40 };
        form.Controls.AddRange(new Control[] { first, box });
        provider.SetClasses(box, "bg-white focus-visible:bg-blue-600");
        form.Show();
        try
        {
            TwFocusVisible.LastInputWasKeyboard = false;   // fareyle tıklandı
            box.Focus();
            Assert.Equal(Color.White.ToArgb(), box.BackColor.ToArgb());

            first.Focus();
            TwFocusVisible.LastInputWasKeyboard = true;    // Tab ile gelindi
            box.Focus();
            Assert.Equal(ColorTranslator.FromHtml("#2563eb").ToArgb(), box.BackColor.ToArgb());
        }
        finally
        {
            TwFocusVisible.LastInputWasKeyboard = false;
            form.Close();
        }
    });
}
