using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class SvgIconTests
{
    // Lucide tarzı çizgi ikonlar (24×24, fill none, stroke 2).
    private const string Check =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" " +
        "stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M20 6 9 17l-5-5\"/></svg>";

    private const string User =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\">" +
        "<path d=\"M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2\"/><circle cx=\"12\" cy=\"7\" r=\"4\"/></svg>";

    private const string Filled =
        "<svg viewBox=\"0 0 20 20\"><g><rect x=\"2\" y=\"2\" width=\"16\" height=\"16\" rx=\"3\"/></g><title>kare</title></svg>";

    private static int Ink(Bitmap bitmap, Color color)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var p = bitmap.GetPixel(x, y);
                if (Math.Abs(p.R - color.R) + Math.Abs(p.G - color.G) + Math.Abs(p.B - color.B) < 120 && p.A > 0) count++;
            }
        return count;
    }

    [Fact]
    public void Path_reader_handles_compact_numbers()
    {
        Assert.Equal(new[] { 10f, -5.5f, 0.5f, 1e2f }, TwSvgIcon.Numbers("10-5.5.5 1e2").ToArray());
    }

    [Fact]
    public void Arcs_end_at_their_target_point()
    {
        using var path = new GraphicsPath();
        TwSvgIcon.PathData.Append(path, "M19 21v-2a4 4 0 0 0-4-4H9");
        var points = path.PathPoints;
        Assert.Equal(9f, points[points.Length - 1].X, 2);
        Assert.Equal(15f, points[points.Length - 1].Y, 2);
        Assert.Contains(points, p => Math.Abs(p.X - 15) < 0.01 && Math.Abs(p.Y - 15) < 0.01);   // yay sonu (19,19) → (15,15)
    }

    [Fact]
    public void Parses_shapes_groups_and_ignores_metadata()
    {
        Assert.Equal(1, TwSvgIcon.Parse(Check).ShapeCount);
        Assert.Equal(2, TwSvgIcon.Parse(User).ShapeCount);
        var filled = TwSvgIcon.Parse(Filled);
        Assert.Equal(1, filled.ShapeCount);
        Assert.Equal(new RectangleF(0, 0, 20, 20), filled.ViewBox);
        Assert.Throws<FormatException>(() => TwSvgIcon.Parse("<div/>"));
        Assert.Throws<FormatException>(() => TwSvgIcon.Parse("<svg"));
    }

    [Fact]
    public void Draws_stroke_and_fill_icons_in_the_given_color()
    {
        using var bitmap = new Bitmap(48, 48);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.White);
            TwSvgIcon.Parse(Check).Draw(g, 24, new Rectangle(0, 0, 24, 24), Color.Red);
            TwSvgIcon.Parse(Filled).Draw(g, 24, new Rectangle(24, 24, 24, 24), Color.Blue);
        }
        Assert.InRange(Ink(bitmap, Color.Red), 20, 200);                 // ince çizgi
        Assert.True(Ink(bitmap, Color.Blue) > 300);                      // dolu kare (~19×19)
        Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(2, 40).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), bitmap.GetPixel(36, 36).ToArgb());
    }

    [Fact]
    public void Theme_icons_accept_inline_svg_files_and_code_points()
    {
        var folder = Path.Combine(Path.GetTempPath(), "formwind-svg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "user.svg"), User);
            var theme = new TwTheme();
            theme.ApplyJson("{\"icons\": {\"ok\": " + System.Text.Json.JsonSerializer.Serialize(Check) + ", \"person\": \"user.svg\", \"invoice\": \"E8A5\"}}", folder);
            Assert.True(theme.TryGetSvgIcon("ok", out _));
            Assert.True(theme.TryGetSvgIcon("PERSON", out _));
            Assert.True(theme.TryGetIcon("invoice", out var glyph) && glyph == '');
            Assert.True(theme.HasIcon("person"));
            Assert.False(theme.HasIcon("yok"));
            Assert.Throws<FormatException>(() => theme.ApplyJson("{\"icons\": {\"x\": \"missing.svg\"}}", folder));
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Svg_icon_overrides_the_glyph_with_the_same_name() => Sta.Run(() =>
    {
        var previous = TwTheme.Current;
        try
        {
            var theme = new TwTheme();
            theme.SetSvgIcon("check", Filled);   // yerleşik "check" glifini ezer
            TwTheme.Current = theme;
            using var bitmap = new Bitmap(24, 24);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.White);
                Assert.True(TwIconRenderer.DrawIcon(g, "check", 20, new Rectangle(0, 0, 24, 24), Color.Black));
                Assert.False(TwIconRenderer.DrawIcon(g, "yok-boyle-ikon", 20, new Rectangle(0, 0, 24, 24), Color.Black));
            }
            Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(12, 12).ToArgb());   // dolu kare = SVG çizildi

            using var icon = new TwIcon { IconName = "check", Size = new Size(24, 24), ForeColor = Color.Black, BackColor = Color.White };
            using var rendered = new Bitmap(24, 24);
            icon.DrawToBitmap(rendered, new Rectangle(0, 0, 24, 24));
            Assert.Equal(Color.Black.ToArgb(), rendered.GetPixel(12, 12).ToArgb());
        }
        finally
        {
            TwTheme.Current = previous;
        }
    });

    [Fact]
    public void Backdrop_classes_reach_the_title_bar() => Sta.Run(() =>
    {
        Assert.Equal(TwBackdrop.Mica, TwParser.Parse("backdrop-mica", TwTheme.Current).Resolve(TwState.None).Backdrop);
        Assert.Equal(TwBackdrop.Tabbed, TwParser.Parse("backdrop-tabbed", TwTheme.Current).Resolve(TwState.None).Backdrop);

        using var provider = new TailwindProvider();
        using var form = new Form();
        provider.SetClasses(form, "bg-white backdrop-acrylic");
        Assert.Equal(TwBackdrop.Acrylic, TwTitleBar.For(form).Backdrop);
        provider.SetClasses(form, "bg-white");
        Assert.Null(TwTitleBar.For(form).Backdrop);
    });
}
