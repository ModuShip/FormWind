using System;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>Fare, klavye ve çizim yolları: TwPager, TwNumberBox, TwDatePicker, TwPictureBox.</summary>
[Collection(UiCollection.Name)]
public class ControlInputTests
{
    private static void Invoke(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { args.GetType() }, null)!
            .Invoke(control, new object[] { args });

    private static bool IsInputKey(Control control, Keys keys) =>
        (bool)control.GetType().GetMethod("IsInputKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, new object[] { keys })!;

    private static MouseEventArgs Mouse(Point point, MouseButtons button = MouseButtons.Left) =>
        new MouseEventArgs(button, 1, point.X, point.Y, 0);

    private static Point Center(Rectangle rect) => new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

    private static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    private static Form OffscreenForm() =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) };

    // ---- TwPager ----

    [Fact]
    public void Pager_buttons_respond_to_mouse() => Sta.Run(() =>
    {
        using var pager = new TwPager { Size = new Size(500, 40), TotalItems = 100, PageSize = 10 };
        int changes = 0;
        pager.PageChanged += (_, _) => changes++;

        Invoke(pager, "OnMouseDown", Mouse(Center(pager.GetButtonBounds(2))));
        Assert.Equal(2, pager.Page);
        Invoke(pager, "OnMouseDown", Mouse(Center(pager.GetButtonBounds(TwPager.Next))));
        Assert.Equal(3, pager.Page);
        Invoke(pager, "OnMouseDown", Mouse(Center(pager.GetButtonBounds(TwPager.Previous))));
        Assert.Equal(2, pager.Page);
        Invoke(pager, "OnMouseDown", Mouse(Center(pager.GetButtonBounds(TwPager.Next)), MouseButtons.Right)); // sağ tık sayfa değiştirmez
        Assert.Equal(2, pager.Page);
        Invoke(pager, "OnMouseDown", Mouse(new Point(499, 39)));                                               // boşluk
        Assert.Equal(2, pager.Page);
        Assert.Equal(3, changes);

        Invoke(pager, "OnMouseMove", Mouse(Center(pager.GetButtonBounds(1)), MouseButtons.None));
        Assert.Equal(Cursors.Hand, pager.Cursor);
        Invoke(pager, "OnMouseMove", Mouse(new Point(499, 39), MouseButtons.None));
        Assert.Equal(Cursors.Default, pager.Cursor);
        pager.Page = 1;
        Invoke(pager, "OnMouseMove", Mouse(Center(pager.GetButtonBounds(TwPager.Previous)), MouseButtons.None)); // ilk sayfada devre dışı
        Assert.Equal(Cursors.Default, pager.Cursor);
        Invoke(pager, "OnMouseLeave", EventArgs.Empty);
    });

    [Fact]
    public void Pager_keyboard_moves_between_pages() => Sta.Run(() =>
    {
        using var pager = new TwPager { Size = new Size(500, 40), TotalItems = 50, PageSize = 10 };
        Assert.True(IsInputKey(pager, Keys.Left));
        Assert.True(IsInputKey(pager, Keys.End));

        Invoke(pager, "OnKeyDown", new KeyEventArgs(Keys.End));
        Assert.Equal(5, pager.Page);
        Invoke(pager, "OnKeyDown", new KeyEventArgs(Keys.Left));
        Assert.Equal(4, pager.Page);
        Invoke(pager, "OnKeyDown", new KeyEventArgs(Keys.Home));
        Assert.Equal(1, pager.Page);
        Invoke(pager, "OnKeyDown", new KeyEventArgs(Keys.Right));
        Assert.Equal(2, pager.Page);

        var other = new KeyEventArgs(Keys.A);
        Invoke(pager, "OnKeyDown", other);
        Assert.False(other.Handled);
        Assert.Equal(2, pager.Page);
    });

    [Fact]
    public void Pager_layout_handles_edge_cases() => Sta.Run(() =>
    {
        using var pager = new TwPager { Size = new Size(500, 40) };
        Assert.Equal(1, pager.PageCount);
        Assert.Equal(new[] { TwPager.Previous, 1, TwPager.Next }, pager.VisiblePages());
        Assert.Equal(0, pager.ItemsOnPage);

        pager.TotalItems = 200;
        pager.PageSize = 0;                                                     // en az 1
        Assert.Equal(1, pager.PageSize);
        pager.PageSize = 10;
        pager.Siblings = 0;
        pager.Page = 10;
        Assert.Equal(new[] { TwPager.Previous, 1, TwPager.Gap, 10, TwPager.Gap, 20, TwPager.Next }, pager.VisiblePages());
        Assert.Equal(Rectangle.Empty, pager.GetButtonBounds(7));

        int width = pager.GetPreferredSize(Size.Empty).Width;
        Assert.Equal(pager.GetButtonBounds(TwPager.Next).Right, width);
        pager.Font = new Font(pager.Font.FontFamily, 20f);
        Assert.True(pager.GetPreferredSize(Size.Empty).Width > width);
        Assert.Equal(AccessibleRole.ToolBar, pager.AccessibilityObject.Role);
    });

    [Fact]
    public void Pager_paints_the_current_page_with_the_selected_color() => Sta.Run(() =>
    {
        using var form = OffscreenForm();
        var pager = new TwPager { Location = new Point(0, 0), Size = new Size(400, 40), TotalItems = 100, PageSize = 10, Page = 2 };
        pager.SelectedBackColor = ColorTranslator.FromHtml("#16a34a");
        form.BackColor = Color.White;
        form.Controls.Add(pager);
        form.Show();

        var current = pager.GetButtonBounds(2);
        using var bitmap = Render(pager);
        Assert.Equal(pager.SelectedBackColor.ToArgb(), bitmap.GetPixel(current.X + 3, current.Y + current.Height / 2).ToArgb());
        var other = pager.GetButtonBounds(1);
        Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(other.X + 3, other.Y + other.Height / 2).ToArgb());

        pager.Focus();
        Invoke(pager, "OnKeyDown", new KeyEventArgs(Keys.Right));                 // klavye: focus halkası çizilir
        using var focused = Render(pager);
        Assert.Equal(3, pager.Page);
        form.Close();
    });

    // ---- TwNumberBox ----

    [Fact]
    public void Number_box_responds_to_keys_and_wheel() => Sta.Run(() =>
    {
        using var box = new TwNumberBox { Maximum = 5 };
        Invoke(box.InnerTextBox, "OnKeyDown", new KeyEventArgs(Keys.Up));
        Invoke(box.InnerTextBox, "OnKeyDown", new KeyEventArgs(Keys.Up));
        Assert.Equal(2m, box.Value);
        Invoke(box.InnerTextBox, "OnKeyDown", new KeyEventArgs(Keys.Down));
        Assert.Equal(1m, box.Value);

        box.InnerTextBox.Text = "4";
        var enter = new KeyEventArgs(Keys.Enter);
        Invoke(box.InnerTextBox, "OnKeyDown", enter);
        Assert.Equal(4m, box.Value);
        Assert.True(enter.SuppressKeyPress);

        Invoke(box.InnerTextBox, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 0, 0, 120));
        Assert.Equal(5m, box.Value);
        Invoke(box.InnerTextBox, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 0, 0, 120)); // üst sınır
        Assert.Equal(5m, box.Value);
        box.Step(0);
        Assert.Equal(5m, box.Value);
    });

    [Fact]
    public void Number_box_stepper_buttons_change_the_value() => Sta.Run(() =>
    {
        using var box = new TwNumberBox { Size = new Size(120, 32), Increment = -2 };  // artım mutlak değer
        Assert.Equal(2m, box.Increment);
        var right = new Point(box.Width - 6, 0);

        Invoke(box, "OnMouseDown", Mouse(new Point(right.X, 6)));          // üst yarı: artır
        Assert.Equal(2m, box.Value);
        Invoke(box, "OnMouseUp", Mouse(new Point(right.X, 6)));
        Invoke(box, "OnMouseDown", Mouse(new Point(right.X, box.Height - 6))); // alt yarı: azalt
        Assert.Equal(0m, box.Value);
        Invoke(box, "OnMouseUp", Mouse(new Point(right.X, box.Height - 6)));

        Invoke(box, "OnMouseMove", Mouse(new Point(right.X, 6), MouseButtons.None));
        Assert.Equal(Cursors.Default, box.Cursor);
        Invoke(box, "OnMouseMove", Mouse(new Point(10, 6), MouseButtons.None));
        Assert.Equal(Cursors.IBeam, box.Cursor);
    });

    [Fact]
    public void Number_box_range_stays_consistent() => Sta.Run(() =>
    {
        using var box = new TwNumberBox { Value = 50 };
        box.Minimum = 200;                                                  // en büyük de yükselir, değer sınıra çekilir
        Assert.Equal(200m, box.Maximum);
        Assert.Equal(200m, box.Value);
        box.Maximum = 10;                                                   // en küçük de düşer
        Assert.Equal(10m, box.Minimum);
        Assert.Equal(10m, box.Value);
        box.DecimalPlaces = 99;
        Assert.Equal(10, box.DecimalPlaces);
        box.DecimalPlaces = -1;
        Assert.Equal(0, box.DecimalPlaces);
    });

    [Fact]
    public void Number_box_paints_enabled_and_disabled() => Sta.Run(() =>
    {
        using var box = new TwNumberBox { Size = new Size(120, 32), ForeColor = Color.Black, BackColor = Color.White };
        using (var bitmap = Render(box))
            Assert.True(HasDarkPixel(bitmap, new Rectangle(box.Width - 20, 4, 16, box.Height - 8)), "ok simgeleri çizilmedi");
        box.Enabled = false;
        using var disabled = Render(box);
    });

    private static bool HasDarkPixel(Bitmap bitmap, Rectangle area)
    {
        for (int y = area.Top; y < area.Bottom; y++)
            for (int x = area.Left; x < area.Right; x++)
                if (bitmap.GetPixel(x, y).GetBrightness() < 0.5f) return true;
        return false;
    }

    // ---- TwDatePicker ----

    [Fact]
    public void Date_picker_opens_the_calendar_and_takes_the_selected_day() => Sta.Run(() =>
    {
        using var form = OffscreenForm();
        var picker = new TwDatePicker { Location = new Point(10, 10), Size = new Size(200, 32), Culture = new CultureInfo("tr-TR") };
        form.Controls.Add(picker);
        form.Show();
        Assert.False(picker.DroppedDown);
        Assert.Null(picker.Calendar);

        picker.Value = new DateTime(2026, 5, 10);
        var f4 = new KeyEventArgs(Keys.F4);
        Invoke(picker.InnerTextBox, "OnKeyDown", f4);
        Assert.True(f4.Handled);
        Assert.True(picker.DroppedDown);
        var calendar = picker.Calendar!;
        Assert.Equal(new DateTime(2026, 5, 10), calendar.SelectedDate);

        int changes = 0;
        picker.ValueChanged += (_, _) => changes++;
        var day = calendar.GetDayBounds(new DateTime(2026, 5, 20));
        Invoke(calendar, "OnMouseDown", Mouse(Center(day)));
        Assert.Equal(new DateTime(2026, 5, 20), picker.Value);
        Assert.Equal(1, changes);
        Assert.False(picker.DroppedDown);

        // Simgeye tıklama da açar; Esc kapatır ve değer değişmez.
        Invoke(picker, "OnMouseDown", Mouse(new Point(picker.Width - 10, picker.Height / 2)));
        Assert.True(picker.DroppedDown);
        Invoke(picker.Calendar!, "OnKeyDown", new KeyEventArgs(Keys.Escape));
        Assert.False(picker.DroppedDown);
        Assert.Equal(new DateTime(2026, 5, 20), picker.Value);

        // Alt+↓ değersizken bugünün ayını gösterir.
        picker.Value = null;
        Invoke(picker.InnerTextBox, "OnKeyDown", new KeyEventArgs(Keys.Alt | Keys.Down));
        Assert.Equal(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), picker.Calendar!.DisplayMonth);
        Invoke(picker.Calendar!, "OnKeyDown", new KeyEventArgs(Keys.Escape));
        form.Close();
    });

    [Fact]
    public void Date_picker_commits_on_enter_and_tracks_the_icon_cursor() => Sta.Run(() =>
    {
        using var picker = new TwDatePicker { Size = new Size(200, 32), Culture = new CultureInfo("tr-TR"), Format = "" };
        Assert.Equal("d", picker.Format);

        picker.InnerTextBox.Text = "3 Şubat 2026";                           // biçime uymaz, kültürle çözülür
        var enter = new KeyEventArgs(Keys.Enter);
        Invoke(picker.InnerTextBox, "OnKeyDown", enter);
        Assert.True(enter.SuppressKeyPress);
        Assert.Equal(new DateTime(2026, 2, 3), picker.Value);
        Assert.Equal(new DateTime(2026, 2, 3).ToString("d", picker.Culture), picker.InnerTextBox.Text);

        picker.MaxDate = new DateTime(2026, 12, 31);
        picker.Value = new DateTime(2027, 1, 1);                             // aralık dışı: yok sayılır
        Assert.Equal(new DateTime(2026, 2, 3), picker.Value);
        picker.Culture = null!;                                              // geçerli kültüre döner
        Assert.Equal(CultureInfo.CurrentCulture, picker.Culture);

        Invoke(picker, "OnMouseMove", Mouse(new Point(picker.Width - 10, picker.Height / 2), MouseButtons.None));
        Assert.Equal(Cursors.Hand, picker.Cursor);
        Invoke(picker, "OnMouseMove", Mouse(new Point(10, picker.Height / 2), MouseButtons.None));
        Assert.Equal(Cursors.IBeam, picker.Cursor);

        picker.ForeColor = Color.Black;
        picker.BackColor = Color.White;
        using (var bitmap = Render(picker))
            Assert.True(HasDarkPixel(bitmap, new Rectangle(picker.Width - 30, 4, 26, picker.Height - 8)), "takvim simgesi çizilmedi");
        picker.Enabled = false;
        using var disabled = Render(picker);
    });

    // ---- TwPictureBox ----

    [Theory]
    [InlineData(TwImageFit.Cover, -10, 0, 40, 20)]
    [InlineData(TwImageFit.Contain, 0, 5, 20, 10)]
    [InlineData(TwImageFit.Fill, 0, 0, 20, 20)]
    public void Picture_box_places_the_image_by_fit(TwImageFit fit, int x, int y, int width, int height) =>
        Assert.Equal(new Rectangle(x, y, width, height), TwPictureBox.Destination(new Size(40, 20), new Size(20, 20), fit));

    [Fact]
    public void Picture_box_with_an_empty_image_fills_the_box() =>
        Assert.Equal(new Rectangle(0, 0, 20, 20), TwPictureBox.Destination(Size.Empty, new Size(20, 20), TwImageFit.Contain));

    [Fact]
    public void Picture_box_draws_the_scaled_image_over_the_background() => Sta.Run(() =>
    {
        using var image = new Bitmap(40, 20);
        using (var g = Graphics.FromImage(image))
        {
            g.FillRectangle(Brushes.Red, 0, 0, 20, 20);
            g.FillRectangle(Brushes.Blue, 20, 0, 20, 20);
        }
        using var provider = new TailwindProvider();
        using var picture = new TwPictureBox { Size = new Size(40, 40), Image = image };
        provider.SetClasses(picture, "bg-green-500 object-contain");
        Assert.Equal(TwImageFit.Contain, picture.Fit);
        Assert.Equal(AccessibleRole.Graphic, picture.AccessibilityObject.Role);

        using (var bitmap = Render(picture))
        {
            Assert.Equal(ColorTranslator.FromHtml("#22c55e").ToArgb(), bitmap.GetPixel(20, 3).ToArgb()); // görselin üstü boş
            Assert.Equal(Color.Red.ToArgb(), bitmap.GetPixel(5, 20).ToArgb());
            Assert.Equal(Color.Blue.ToArgb(), bitmap.GetPixel(35, 20).ToArgb());
        }

        picture.Fit = TwImageFit.Cover;
        picture.Size = new Size(20, 20);
        using (var bitmap = Render(picture))
        {
            Assert.Equal(Color.Red.ToArgb(), bitmap.GetPixel(5, 2).ToArgb());
            Assert.Equal(Color.Blue.ToArgb(), bitmap.GetPixel(15, 17).ToArgb());
        }

        picture.Image = null;
        using (var bitmap = Render(picture))
            Assert.Equal(ColorTranslator.FromHtml("#22c55e").ToArgb(), bitmap.GetPixel(10, 10).ToArgb());
    });
}
