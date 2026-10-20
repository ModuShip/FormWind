using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// FormWind'in Windows'un yerine çizdiği parçalarda klavye odağı görünmeli: sekme şeridi ve DropDownList kutusu native
/// çizimi örttüğünden Windows'un odak çerçevesi de kayboluyordu. Klavye ipuçları (Windows onları klavye kullanılana kadar
/// gizler) WM_UPDATEUISTATE ile açılır.
/// </summary>
[Collection(UiCollection.Name)]
public class KeyboardFocusTests
{
    private static Form NewForm() =>
        new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, ClientSize = new Size(300, 200) };

    // UIS_CLEAR (2), UISF_HIDEFOCUS (1): odak ipuçları gösterilir (klavye kullanılmış gibi).
    private static void ShowFocusCues(Form form) => SendMessage(form.Handle, 0x0128 /* WM_UPDATEUISTATE */, (IntPtr)0x10002, IntPtr.Zero);

    [Fact]
    public void Tab_strip_draws_the_focus_rectangle_it_covers() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = NewForm();
        var button = new Button { Text = "Başka", Location = new Point(0, 170) };
        var tabs = new TabControl { Size = new Size(300, 160), TabPages = { new TabPage("Genel"), new TabPage("Gelişmiş") } };
        form.Controls.AddRange(new Control[] { tabs, button });
        form.Show();
        provider.SetClasses(tabs, "tab-control");
        Assert.True(TabControlApplier.IsPainted(tabs));
        ShowFocusCues(form);

        button.Focus();
        using var unfocused = Paint(tabs);
        tabs.Focus();
        Assert.True(tabs.Focused);
        using var focused = Paint(tabs);

        var tab = Rectangle.Inflate(tabs.GetTabRect(0), -3, -3);
        Assert.True(Differs(unfocused, focused, tab.Left, tab.Right, tab.Top), "odak çerçevesinin üst kenarı çizilmedi");
        form.Close();
    });

    [Fact]
    public void Drop_down_list_box_painted_by_formwind_shows_keyboard_focus() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = NewForm();
        var button = new Button { Text = "Başka", Location = new Point(0, 100) };
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        combo.Items.Add("Liste seçimi");
        combo.SelectedIndex = 0;
        form.Controls.AddRange(new Control[] { combo, button });
        form.Show();
        provider.SetClasses(combo, "bg-gray-800 text-gray-100");
        Assert.True(TwComboPainter.IsAttached(combo));
        ShowFocusCues(form);

        button.Focus();
        using var unfocused = Snapshot(combo);
        combo.Focus();
        Assert.True(combo.Focused);
        using var focused = Snapshot(combo);

        int x = 3 + 1;                                                                          // kenarlık + 3 px içeride
        bool found = false;
        for (int y = 0; y < combo.Height && !found; y++)
            found = Differs(unfocused, focused, x, x + 1, y);
        Assert.True(found, "odak çerçevesinin sol kenarı çizilmedi");
        form.Close();
    });

    // Devre dışı yazıyı çizen gizli etiket uygulamanın yazısını tutmamalı (erişilebilirlik araçları gizli pencereleri de
    // dolaşabilir).
    [Fact]
    public void Hidden_drawing_label_does_not_keep_the_application_text() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = NewForm();
        var label = new Label { Text = "Gizli tutulmayacak yazı", AutoSize = true, Enabled = false };
        form.Controls.Add(label);
        form.Show();
        provider.SetClasses(label, "text-gray-800 dark:text-gray-200");
        Assert.True(TwDisabledText.IsRepainted(label));
        using var bitmap = Snapshot(label);
        Assert.True(string.IsNullOrEmpty(TwDisabledText.ProbeText), $"gizli etiket: '{TwDisabledText.ProbeText}'");
        form.Close();
    });

    private static Bitmap Paint(TabControl tabs)
    {
        var bitmap = new Bitmap(tabs.Width, tabs.Height);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Magenta);
        TabControlApplier.PaintTo(tabs, g);
        return bitmap;
    }

    private static Bitmap Snapshot(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    private static bool Differs(Bitmap a, Bitmap b, int left, int right, int y)
    {
        for (int x = left; x < right; x++)
            if (a.GetPixel(x, y).ToArgb() != b.GetPixel(x, y).ToArgb()) return true;
        return false;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
}
