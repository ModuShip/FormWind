using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>TwTransition.CrossFade: the old look fades away over the change.</summary>
[Collection(UiCollection.Name)]
public class CrossFadeTests
{
    private static void WaitUntil(Func<bool> condition, int milliseconds = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }

    [Fact]
    public void Change_is_applied_under_a_fading_picture_of_the_old_look() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(100, 100), Size = new Size(400, 300) };
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        form.Controls.Add(page);
        form.Show();
        Application.DoEvents();
        int before = TwFadeOut.ActiveCount;
        bool applied = false;

        TwTransition.CrossFade(page, () =>
        {
            Assert.Equal(before + 1, TwFadeOut.ActiveCount);                         // eski görüntü değişiklikten önce ekranda
            page.BackColor = Color.Black;
            applied = true;
        });
        Assert.True(applied);
        Assert.True(TwTransition.LastCovered);
        Assert.Equal(Color.Black, page.BackColor);
        WaitUntil(() => TwFadeOut.ActiveCount == before);
        Assert.Equal(before, TwFadeOut.ActiveCount);
        form.Close();
    });

    [Fact]
    public void Without_motion_or_when_hidden_the_change_is_simply_applied() => Sta.Run(() =>
    {
        using var hidden = new Panel { Size = new Size(100, 100) };
        int calls = 0;
        TwTransition.CrossFade(hidden, () => calls++);
        Assert.Equal(1, calls);
        Assert.False(TwTransition.LastCovered);

        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(100, 100) };
        form.Show();
        var previous = TwMotion.Enabled;
        TwMotion.Enabled = false;
        try
        {
            TwTransition.CrossFade(form, () => calls++);
            Assert.Equal(2, calls);
        }
        finally
        {
            TwMotion.Enabled = previous;
        }
        Assert.Throws<ArgumentNullException>(() => TwTransition.CrossFade(form, null!));
        form.Close();
    });

    [Fact]
    public void The_picture_covers_only_the_visible_part_of_the_host() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(100, 100), ClientSize = new Size(300, 200) };
        var clip = new Panel { Location = new Point(10, 10), Size = new Size(100, 100) };
        var child = new Panel { Location = new Point(50, 50), Size = new Size(200, 200) };
        clip.Controls.Add(child);
        form.Controls.Add(clip);
        form.Show();
        var bounds = clip.RectangleToScreen(child.Bounds);
        var visible = TwTransition.VisibleScreenBounds(child, bounds);
        Assert.Equal(new Size(50, 50), visible.Size);                               // ebeveynin dışında kalan kesilir
        Assert.Equal(bounds.Location, visible.Location);
        form.Close();
    });
}
