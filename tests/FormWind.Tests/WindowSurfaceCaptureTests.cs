using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Pictures read from the window's own surface (0.1.8 report: capture without waiting for the screen): the same content
/// the screen shows, also when another window covers the form, and the older paths outside the client area.
/// </summary>
[Collection(UiCollection.Name)]
public class WindowSurfaceCaptureTests
{
    private static Form ShowForm(out Panel left, out Panel right)
    {
        var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(160, 160), ClientSize = new Size(300, 160) };
        left = new Panel { Dock = DockStyle.Left, Width = 120, BackColor = Color.SteelBlue };
        right = new Panel { Dock = DockStyle.Fill, BackColor = Color.Orange };
        right.Controls.Add(new Label { Text = "Kayıtlar", Location = new Point(10, 10), AutoSize = true, ForeColor = Color.Black });
        form.Controls.Add(right);
        form.Controls.Add(left);
        form.Show();
        form.Activate();
        Application.DoEvents();
        return form;
    }

    private static void AssertColor(Color expected, Color actual) => Assert.Equal(expected.ToArgb(), Color.FromArgb(255, actual).ToArgb());

    [Fact]
    public void A_part_of_the_client_area_is_read_from_the_window_surface() => Sta.Run(() =>
    {
        using var form = ShowForm(out var left, out var right);
        var bounds = form.RectangleToScreen(form.ClientRectangle);
        using var picture = TwCapture.Take(form, bounds);
        Assert.NotNull(picture);
        Assert.Equal(TwCapture.Source.Window, TwCapture.LastSource);
        AssertColor(Color.SteelBlue, picture!.GetPixel(60, 140));
        AssertColor(Color.Orange, picture.GetPixel(250, 140));
    });

    [Fact]
    public void Content_painted_just_before_is_in_the_picture_without_waiting_for_the_screen() => Sta.Run(() =>
    {
        using var form = ShowForm(out var left, out _);
        left.BackColor = Color.SeaGreen;
        using var picture = TwCapture.Take(left);
        Assert.Equal(TwCapture.Source.Window, TwCapture.LastSource);
        AssertColor(Color.SeaGreen, picture!.GetPixel(60, 80));
    });

    [Fact]
    public void A_form_covered_by_another_window_is_read_from_its_own_surface() => Sta.Run(() =>
    {
        using var form = ShowForm(out _, out var right);
        using var cover = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, TopMost = true, BackColor = Color.Magenta, FormBorderStyle = FormBorderStyle.None };
        cover.Bounds = form.RectangleToScreen(form.ClientRectangle);
        cover.Show();
        Application.DoEvents();

        using var picture = TwCapture.Take(right);
        Assert.Equal(TwCapture.Source.Window, TwCapture.LastSource);
        AssertColor(Color.Orange, picture!.GetPixel(right.Width - 10, right.Height - 10));
    });

    [Fact]
    public void A_form_with_its_title_bar_takes_the_older_paths() => Sta.Run(() =>
    {
        using var form = ShowForm(out _, out _);
        using var picture = TwCapture.Take(form);
        Assert.NotNull(picture);
        Assert.NotEqual(TwCapture.Source.Window, TwCapture.LastSource);
    });
}
