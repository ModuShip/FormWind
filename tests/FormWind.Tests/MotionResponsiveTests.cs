using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class MotionResponsiveTests
{
    [Theory]
    [InlineData(500, TwState.None)]
    [InlineData(640, TwState.Sm)]
    [InlineData(800, TwState.Sm | TwState.Md)]
    [InlineData(1300, TwState.Sm | TwState.Md | TwState.Lg | TwState.Xl)]
    public void Breakpoints_follow_tailwind_widths(int width, TwState expected) => Assert.Equal(expected, TwBreakpoints.ForWidth(width));

    [Fact]
    public void Responsive_variants_apply_when_the_form_widens() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0), ClientSize = new Size(600, 400) };
        var panel = new TwPanel { Dock = DockStyle.Fill };
        var a = new Panel { Size = new Size(100, 50) };
        var b = new Panel { Size = new Size(100, 50) };
        panel.Controls.AddRange(new Control[] { a, b });
        form.Controls.Add(panel);
        provider.SetClasses(panel, "flex flex-col gap-2 bg-white md:flex-row md:bg-gray-100 lg:bg-gray-200");
        form.Show();
        try
        {
            Assert.Equal(Color.White.ToArgb(), panel.BackColor.ToArgb());
            Assert.True(b.Top > a.Top && b.Left == a.Left);                 // dikey

            int changes = 0;
            provider.BreakpointChanged += (_, _) => changes++;
            form.ClientSize = new Size(900, 400);
            Application.DoEvents();
            Assert.Equal(ColorTranslator.FromHtml("#f3f4f6").ToArgb(), panel.BackColor.ToArgb());
            Assert.True(b.Left > a.Left && b.Top == a.Top);                 // yatay

            form.ClientSize = new Size(1100, 400);
            Assert.Equal(ColorTranslator.FromHtml("#e5e7eb").ToArgb(), panel.BackColor.ToArgb());
            form.ClientSize = new Size(1150, 400);                          // aynı eşik: yeniden uygulanmaz
            Assert.Equal(2, changes);

            form.ClientSize = new Size(500, 400);
            Assert.Equal(Color.White.ToArgb(), panel.BackColor.ToArgb());
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void Tab_indicator_slides_only_when_motion_is_enabled() => Sta.Run(() =>
    {
        var previous = TwMotion.Enabled;
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
        var tabs = new TwTabStrip { Size = new Size(300, 40), BackColor = Color.White };
        tabs.Tabs.AddRange(new[] { "Genel", "Adres", "Siparişler" });
        form.Controls.Add(tabs);
        form.Show();
        try
        {
            int AccentLeft()
            {
                using var bitmap = new Bitmap(tabs.Width, tabs.Height);
                tabs.DrawToBitmap(bitmap, new Rectangle(Point.Empty, tabs.Size));
                for (int x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel(x, tabs.Height - 1).ToArgb() == ColorTranslator.FromHtml("#2563eb").ToArgb()) return x;
                return -1;
            }

            TwMotion.Enabled = false;
            tabs.SelectedIndex = 2;
            Assert.Equal(tabs.GetTabBounds(2).Left, AccentLeft());          // anında

            TwMotion.Enabled = true;
            tabs.SelectedIndex = 0;
            int during = AccentLeft();
            Assert.True(during > tabs.GetTabBounds(0).Left, "Gösterge kaymadan yerine geçti.");
            Thread.Sleep(250);
            Assert.Equal(tabs.GetTabBounds(0).Left, AccentLeft());          // sonunda yerinde
        }
        finally
        {
            TwMotion.Enabled = previous;
            form.Close();
        }
    });

    [Fact]
    public void Switch_knob_slides_only_when_motion_is_enabled() => Sta.Run(() =>
    {
        var previous = TwMotion.Enabled;
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
        var sw = new TwSwitch { Size = new Size(44, 24) };
        form.Controls.Add(sw);
        form.Show();
        try
        {
            TwMotion.Enabled = false;
            sw.Checked = true;
            Assert.Equal(1f, sw.ThumbPosition);                             // anında

            TwMotion.Enabled = true;
            sw.Checked = false;
            Assert.True(sw.ThumbPosition > 0f, "Düğme kaymadan yerine geçti.");
        }
        finally
        {
            TwMotion.Enabled = previous;
            form.Close();
        }
    });

    [Fact]
    public void Toast_appears_and_closes_immediately_when_motion_is_disabled() => Sta.Run(() =>
    {
        var previous = TwMotion.Enabled;
        using var owner = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0), Size = new Size(600, 400) };
        owner.Show();
        try
        {
            TwMotion.Enabled = false;
            var toast = TwToast.Show(owner, "Kaydedildi", durationMs: 0);
            Assert.Equal(1.0, toast.Opacity);                               // solarak belirmez

            toast.Dismiss();
            Assert.True(toast.IsDisposed);                                  // solmadan kapanır
            Assert.Empty(TwToast.OpenToasts(owner));

            TwMotion.Enabled = true;
            var animated = TwToast.Show(owner, "Kaydedildi", durationMs: 0);
            Assert.True(animated.Opacity < 1.0);
            animated.Close();
        }
        finally
        {
            TwMotion.Enabled = previous;
            owner.Close();
        }
    });

    [Fact]
    public void Indeterminate_progress_stands_still_when_motion_is_disabled() => Sta.Run(() =>
    {
        var previous = TwMotion.Enabled;
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
        var progress = new TwProgress { Size = new Size(200, 8) };
        form.Controls.Add(progress);
        form.Show();
        try
        {
            TwMotion.Enabled = false;
            progress.Indeterminate = true;
            Assert.False(progress.Animating);
            var bar = progress.BarBounds;
            Assert.Equal(200 - bar.Right, bar.Left);                        // ortada
            Assert.True(bar.Width > 0);
            progress.Phase = 0.9f;
            Assert.Equal(bar, progress.BarBounds);                          // faz yok sayılır

            progress.Indeterminate = false;
            TwMotion.Enabled = true;
            progress.Indeterminate = true;
            Assert.True(progress.Animating);
        }
        finally
        {
            TwMotion.Enabled = previous;
            form.Close();
        }
    });
}
