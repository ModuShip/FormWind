using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// TwOffscreen: ekrana çıkmadan pencere resmi; sahip olunan pencereler, saydamlık ve Region dahil.
[Collection(UiCollection.Name)]
public class OffscreenTests
{
    private static Form NewForm(Color back) => new Form
    {
        FormBorderStyle = FormBorderStyle.None,
        ClientSize = new Size(300, 200),
        BackColor = back,
        ShowInTaskbar = false,
    };

    private static Point Mid(Bitmap bitmap) => new Point(bitmap.Width / 2, bitmap.Height / 2);

    [Fact]
    public void Renders_a_form_that_was_never_shown_and_leaves_it_hidden() => Sta.Run(() =>
    {
        using var form = NewForm(Color.FromArgb(200, 30, 40));
        form.StartPosition = FormStartPosition.CenterScreen;
        var before = form.StartPosition;

        using var picture = TwOffscreen.Render(form);

        Assert.False(form.Visible);
        Assert.Equal(before, form.StartPosition);
        Assert.Equal(form.ClientSize.Width, picture.Width);
        Assert.Equal(form.ClientSize.Height, picture.Height);
        var pixel = picture.GetPixel(Mid(picture).X, Mid(picture).Y);
        Assert.Equal(255, pixel.A);
        Assert.Equal(Color.FromArgb(200, 30, 40).ToArgb(), pixel.ToArgb());
    });

    [Fact]
    public void Lays_out_children_and_draws_styled_controls() => Sta.Run(() =>
    {
        using var form = NewForm(Color.White);
        using var tw = new TailwindProvider();
        var panel = new Panel { Dock = DockStyle.Top, Height = 80 };
        form.Controls.Add(panel);
        tw.SetClasses(panel, "bg-[#0000ff]");

        using var picture = TwOffscreen.Render(form);

        Assert.Equal(Color.FromArgb(0, 0, 255).ToArgb(), picture.GetPixel(150, 40).ToArgb());
        Assert.Equal(Color.White.ToArgb(), picture.GetPixel(150, 150).ToArgb());
    });

    [Fact]
    public void Includes_owned_windows_over_the_form() => Sta.Run(() =>
    {
        using var owner = NewForm(Color.White);
        using var sheet = NewForm(Color.Lime);
        sheet.ClientSize = new Size(100, 60);
        sheet.StartPosition = FormStartPosition.Manual;
        owner.StartPosition = FormStartPosition.Manual;
        owner.Location = new Point(-31000, -31000);
        owner.Show();
        sheet.Owner = owner;
        sheet.Location = new Point(owner.Left + 20, owner.Top + 30);
        sheet.Show();

        using var with = TwOffscreen.Render(owner);
        Assert.Equal(Color.Lime.ToArgb(), with.GetPixel(60, 60).ToArgb());
        Assert.Equal(Color.White.ToArgb(), with.GetPixel(250, 150).ToArgb());

        using var without = TwOffscreen.Render(owner, new TwOffscreenOptions { IncludeOwned = false });
        Assert.Equal(Color.White.ToArgb(), without.GetPixel(60, 60).ToArgb());

        // Resmin dışına taşan sahip pencere: varsayılan kırpar, ExpandToOwned büyütür.
        sheet.Location = new Point(owner.Left + 250, owner.Top + 150);
        using var clipped = TwOffscreen.Render(owner);
        using var expanded = TwOffscreen.Render(owner, new TwOffscreenOptions { ExpandToOwned = true });
        Assert.Equal(owner.ClientSize, clipped.Size);
        Assert.Equal(sheet.Bounds.Right - owner.Left, expanded.Width);
        Assert.Equal(sheet.Bounds.Bottom - owner.Top, expanded.Height);
        Assert.Equal(Color.Lime.ToArgb(), expanded.GetPixel(expanded.Width - 10, expanded.Height - 10).ToArgb());
        Assert.Equal(0, expanded.GetPixel(expanded.Width - 10, 5).A);   // hiçbir pencerenin örtmediği yer saydam
    });

    [Fact]
    public void Applies_opacity_transparency_key_and_region() => Sta.Run(() =>
    {
        using var faded = NewForm(Color.Black);
        faded.Opacity = 0.5;
        using (var picture = TwOffscreen.Render(faded))
            Assert.InRange(picture.GetPixel(10, 10).A, 120, 135);

        using var keyed = NewForm(Color.White);
        keyed.TransparencyKey = Color.Magenta;
        var strip = new Panel { Dock = DockStyle.Left, Width = 100, BackColor = Color.Magenta };
        keyed.Controls.Add(strip);
        using (var picture = TwOffscreen.Render(keyed))
        {
            Assert.Equal(0, picture.GetPixel(10, 100).A);
            Assert.Equal(255, picture.GetPixel(200, 100).A);
        }

        using var round = NewForm(Color.Black);
        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            path.AddEllipse(0, 0, round.ClientSize.Width, round.ClientSize.Height);
            round.Region = new Region(path);
        }
        using (var picture = TwOffscreen.Render(round))
        {
            Assert.Equal(0, picture.GetPixel(2, 2).A);
            Assert.Equal(255, picture.GetPixel(150, 100).A);
        }
    });

    [Fact]
    public void Restores_motion_and_can_close_the_form() => Sta.Run(() =>
    {
        bool motion = TwMotion.Enabled;
        TwMotion.Enabled = true;
        try
        {
            var form = NewForm(Color.White);
            using (TwOffscreen.Render(form, new TwOffscreenOptions { CloseWhenDone = true })) { }
            Assert.True(TwMotion.Enabled);
            Assert.True(form.IsDisposed);
            Assert.Throws<ArgumentNullException>(() => TwOffscreen.Render(null!));
        }
        finally
        {
            TwMotion.Enabled = motion;
        }
    });
}
