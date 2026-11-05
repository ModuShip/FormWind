using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// 0.1.6 adım 5: TwHeatmap renkleri, TwDialog simgesiz tür ve yüzey.
[Collection(UiCollection.Name)]
public class LowPriorityTests
{
    private static Form NewForm() =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(640, 400) };

    private static int Count(Bitmap bitmap, Rectangle area, Color color)
    {
        int count = 0;
        area.Intersect(new Rectangle(Point.Empty, bitmap.Size));
        for (int y = area.Top; y < area.Bottom; y++)
            for (int x = area.Left; x < area.Right; x++)
                if (bitmap.GetPixel(x, y).ToArgb() == color.ToArgb()) count++;
        return count;
    }

    private static Bitmap Draw(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    private static TwHeatmap NewHeatmap(Form form)
    {
        var heatmap = new TwHeatmap
        {
            Dock = DockStyle.Fill,
            AnimateOnShow = false,
            AnimateChanges = false,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(10, 200, 30),
            HighColor = Color.FromArgb(40, 60, 220),
            LowColor = Color.FromArgb(40, 60, 220),
        };
        form.Controls.Add(heatmap);
        heatmap.SetData(new[] { "A", "B" }, new[] { "Pzt", "Paz" }, new double[,] { { 5, 5 }, { 5, 5 } });
        form.Show();
        return heatmap;
    }

    [Fact]
    public void Heatmap_selection_outline_defaults_to_the_text_color() => Sta.Run(() =>
    {
        using var form = NewForm();
        var heatmap = NewHeatmap(form);
        var box = heatmap.GetCellBounds(0, 0);
        var area = Rectangle.Inflate(box, 6, 6);

        using (var none = Draw(heatmap)) Assert.Equal(0, Count(none, area, heatmap.ForeColor));

        heatmap.SelectedIndex = 0;
        using (var selected = Draw(heatmap)) Assert.True(Count(selected, area, heatmap.ForeColor) > 20);

        heatmap.SelectionColor = Color.FromArgb(255, 0, 0);
        using (var custom = Draw(heatmap))
        {
            Assert.True(Count(custom, area, Color.FromArgb(255, 0, 0)) > 20);
            Assert.Equal(0, Count(custom, area, heatmap.ForeColor));
        }
        form.Close();
    });

    [Fact]
    public void Heatmap_highlight_header_text_has_its_own_color() => Sta.Run(() =>
    {
        using var form = NewForm();
        var heatmap = NewHeatmap(form);
        heatmap.HighlightColumns = new[] { 1 };
        var header = new Rectangle(heatmap.GetCellBounds(0, 1).Left - 10, 0, heatmap.GetCellBounds(0, 1).Width + 20, heatmap.GetCellBounds(0, 1).Top);
        var red = Color.FromArgb(255, 0, 0);

        using (var plain = Draw(heatmap)) Assert.Equal(0, Count(plain, header, red));
        heatmap.HighlightTextColor = red;
        using (var colored = Draw(heatmap))
        {
            Assert.True(Count(colored, header, red) > 0);
            // Vurgulu olmayan sütunun başlığı etkilenmez.
            var other = new Rectangle(heatmap.GetCellBounds(0, 0).Left - 10, 0, heatmap.GetCellBounds(0, 0).Width + 20, heatmap.GetCellBounds(0, 0).Top);
            Assert.Equal(0, Count(colored, other, red));
        }
        form.Close();
    });

    private static void ShowOffScreen(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-3000, -3000);
        form.Show();
    }

    [Fact]
    public void Dialog_without_an_icon_hides_the_badge_and_prompt_uses_the_kind() => Sta.Run(() =>
    {
        using var none = TwDialog.Create(new TwDialogOptions { Message = "Ad", Kind = TwDialogKind.None });
        using var info = TwDialog.Create(new TwDialogOptions { Message = "Ad" });
        using var prompt = TwDialog.Create(new TwDialogOptions { Message = "Ad", Kind = TwDialogKind.None, ShowInput = true });
        foreach (var dialog in new[] { none, info, prompt }) ShowOffScreen(dialog);
        Assert.False(none.IconBadge.Parent!.Visible);
        Assert.True(info.IconBadge.Parent!.Visible);
        Assert.Equal("info", info.IconBadge.IconName);
        Assert.False(prompt.IconBadge.Parent!.Visible);
        Assert.Equal("None", prompt.IconBadge.AccessibleName);
        // Simgesiz diyalog simgeli olandan dardır: ileti sol kenardan başlar.
        int X(TwDialogForm d) => d.PointToClient(d.MessageLabel.PointToScreen(Point.Empty)).X;
        Assert.True(X(none) < X(info), X(none) + " vs " + X(info));
    });

    [Fact]
    public void Dialog_surface_defaults_to_the_theme_card() => Sta.Run(() =>
    {
        var theme = TwTheme.Current;
        var card = theme.Parse("card");
        using var light = TwDialog.Create(new TwDialogOptions { Message = "Ad" });
        using var dark = TwDialog.Create(new TwDialogOptions { Message = "Ad", Dark = true });
        Assert.Equal(card.Resolve(TwState.None).BackColor!.Value.ToArgb(), light.BackColor.ToArgb());
        Assert.Equal(card.Resolve(TwState.Dark).BackColor!.Value.ToArgb(), dark.BackColor.ToArgb());
    });

    private sealed class HoverCaptionButtons : TwCaptionButtons
    {
        public void MoveMouse(Point point) => OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0));
    }

    [Fact]
    public void Caption_dots_lay_out_left_to_right_and_hit_test() => Sta.Run(() =>
    {
        using var form = NewForm();
        var buttons = new TwCaptionButtons { Dock = DockStyle.Right };
        form.Controls.Add(buttons);
        form.Show();
        int windowsWidth = buttons.Width;
        Assert.Equal(TwCaptionButtonStyle.Windows, buttons.ButtonStyle);
        Assert.Equal(2, buttons.KindAt(new Point(buttons.Width - 5, 10)));   // Windows: kapat sağda
        Assert.Equal(0, buttons.KindAt(new Point(5, 10)));                   // küçült solda

        buttons.ButtonStyle = TwCaptionButtonStyle.Dots;
        Assert.True(buttons.Width < windowsWidth);                           // dockedren genişlik tercih edilene uydu
        Assert.Equal(buttons.GetPreferredSize(Size.Empty).Width, buttons.Width);
        var bounds = buttons.ButtonBounds();
        Assert.Equal(3, bounds.Count);
        Assert.True(bounds[0].Left <= 0 && bounds[0].Right <= bounds[1].Left + 1);
        Assert.True(bounds[1].Right <= bounds[2].Left + 1 && bounds[2].Right >= buttons.Width);
        // Soldan sağa: kapat (2), küçült (0), büyüt (1).
        Assert.Equal(2, buttons.KindAt(new Point(bounds[0].Left + bounds[0].Width / 2, 10)));
        Assert.Equal(0, buttons.KindAt(new Point(bounds[1].Left + bounds[1].Width / 2, 10)));
        Assert.Equal(1, buttons.KindAt(new Point(bounds[2].Left + bounds[2].Width / 2, 10)));
        Assert.Equal(-1, buttons.KindAt(new Point(buttons.Width + 5, 10)));

        buttons.ShowMaximize = false;
        buttons.ButtonStyle = TwCaptionButtonStyle.Windows;
        buttons.ButtonStyle = TwCaptionButtonStyle.Dots;
        Assert.Equal(2, buttons.ButtonBounds().Count);
        form.Close();
    });

    [Fact]
    public void Caption_dots_show_glyphs_only_while_hovered() => Sta.Run(() =>
    {
        using var form = NewForm();
        var buttons = new HoverCaptionButtons { ButtonStyle = TwCaptionButtonStyle.Dots, Location = new Point(10, 10) };
        form.Controls.Add(buttons);
        form.Show();
        Bitmap Draw()
        {
            var bitmap = new Bitmap(buttons.Width, buttons.Height);
            buttons.DrawToBitmap(bitmap, new Rectangle(Point.Empty, buttons.Size));
            return bitmap;
        }
        // Kapat noktasının merkezi: durağanken düz kırmızı, üstünde gezilirken simge çizilir.
        var center = new Point(buttons.ButtonBounds()[0].Left + 12 + 6, buttons.Height / 2);
        using (var rest = Draw()) Assert.Equal(buttons.CloseDotColor.ToArgb(), rest.GetPixel(center.X, center.Y).ToArgb());
        buttons.MoveMouse(center);
        using (var hover = Draw()) Assert.NotEqual(buttons.CloseDotColor.ToArgb(), hover.GetPixel(center.X, center.Y).ToArgb());
        form.Close();
    });
}
