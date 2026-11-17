using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// <see cref="TwPlaceholder"/>: a plain TextBox (one window) draws its hint in the <c>placeholder:text-*</c> color, as a
/// <see cref="TwTextBox"/> (a panel with a text box inside) does.
/// </summary>
[Collection(UiCollection.Name)]
public class PlaceholderTests
{
    private static readonly Color Red = ColorTranslator.FromHtml("#ef4444");

    // ClearType draws black text with colored fringes: a hint is a run of pixels close to the class color.
    private static int RedPixels(Control control)
    {
        using var picture = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(picture, new Rectangle(Point.Empty, control.Size));
        int count = 0;
        for (int y = 0; y < picture.Height; y++)
            for (int x = 0; x < picture.Width; x++)
            {
                var c = picture.GetPixel(x, y);
                if (Math.Abs(c.R - Red.R) < 40 && Math.Abs(c.G - Red.G) < 40 && Math.Abs(c.B - Red.B) < 40) count++;
            }
        return count;
    }

    private static bool HasReddish(Control control) => RedPixels(control) >= 8;

    private static (Form Form, TextBox Box) Show(TailwindProvider provider, bool classesFirst)
    {
        var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(300, 80) };
        var box = new TextBox { Location = new Point(10, 10), Width = 220 };
        form.Controls.Add(box);
        if (classesFirst) provider.SetClasses(box, "input placeholder:text-red-500");
        TwPlaceholder.Set(box, "Kayıt ara…");
        if (!classesFirst) provider.SetClasses(box, "input placeholder:text-red-500");
        form.Show();
        Application.DoEvents();
        return (form, box);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_hint_of_an_empty_box_is_drawn_in_the_placeholder_color(bool classesFirst) => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var (form, box) = Show(provider, classesFirst);
        using (form)
        {
            Assert.Equal("Kayıt ara…", TwPlaceholder.Get(box));
            Assert.Equal(Red.ToArgb(), TwPlaceholder.ColorOf(box)!.Value.ToArgb());
            Assert.True(HasReddish(box), RedPixels(box) + " red pixels");
            Assert.Empty(box.Controls);                                         // still a single window

            box.Text = "Hat 3";
            Application.DoEvents();
            Assert.False(HasReddish(box), RedPixels(box) + " red pixels");
        }
    });

    [Fact]
    public void Removing_the_hint_stops_drawing_it() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        var (form, box) = Show(provider, classesFirst: true);
        using (form)
        {
            TwPlaceholder.Set(box, null);
            Application.DoEvents();
            Assert.Equal("", TwPlaceholder.Get(box));
            Assert.False(HasReddish(box));
            Assert.Throws<ArgumentNullException>(() => TwPlaceholder.Set(null!, "x"));
        }
    });
}
