using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>A focus ring that appears for keyboard focus closes in from outside onto the control.</summary>
[Collection(UiCollection.Name)]
public class FocusRingMotionTests
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

    private static void WithKeyboard(bool keyboard, Action body)
    {
        var previous = TwFocusVisible.LastInputWasKeyboard;
        TwFocusVisible.LastInputWasKeyboard = keyboard;
        try
        {
            body();
        }
        finally
        {
            TwFocusVisible.LastInputWasKeyboard = previous;
        }
    }

    [Fact]
    public void Keyboard_focus_ring_closes_in_from_outside() => Sta.Run(() => WithKeyboard(true, () =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), Size = new Size(300, 200) };
        var other = new TextBox { Location = new Point(20, 100) };
        var box = new TextBox { Location = new Point(20, 20), Width = 160 };
        form.Controls.AddRange(new Control[] { box, other });
        provider.SetClasses(box, "rounded focus-visible:ring-2 focus-visible:ring-blue-500");
        form.Show();
        other.Focus();
        Application.DoEvents();

        TwFocusVisible.LastInputWasKeyboard = true;
        box.Focus();
        Application.DoEvents();
        var outset = TwOutset.For(box);
        Assert.True(outset.Spec!.RingWidth > 0);
        Assert.True(outset.RingInSpread > 0);                                       // dışarıdan başlar
        Assert.True(outset.RingInSpread <= Math.Ceiling(TwOutset.RingInDistance * box.DeviceDpi / 96f));
        using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) form.DrawToBitmap(bitmap, form.ClientRectangle);

        WaitUntil(() => !outset.Animating);
        Assert.Equal(0, outset.RingInSpread);
        Assert.Equal(1f, outset.RingInProgress);
        form.Close();
    }));

    [Fact]
    public void Mouse_focus_and_reduced_motion_show_the_ring_in_place() => Sta.Run(() => WithKeyboard(false, () =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), Size = new Size(300, 200) };
        var other = new TextBox { Location = new Point(20, 100) };
        var box = new TextBox { Location = new Point(20, 20) };
        form.Controls.AddRange(new Control[] { box, other });
        provider.SetClasses(box, "focus:ring-2");
        form.Show();
        other.Focus();
        Application.DoEvents();

        box.Focus();                                                                 // fareyle gelmiş odak
        Application.DoEvents();
        Assert.True(TwOutset.For(box).Spec!.RingWidth > 0);
        Assert.Equal(0, TwOutset.For(box).RingInSpread);

        var previous = TwMotion.Enabled;
        TwMotion.Enabled = false;
        try
        {
            other.Focus();
            TwFocusVisible.LastInputWasKeyboard = true;
            box.Focus();
            Application.DoEvents();
            Assert.Equal(0, TwOutset.For(box).RingInSpread);
        }
        finally
        {
            TwMotion.Enabled = previous;
        }
        form.Close();
    }));
}
