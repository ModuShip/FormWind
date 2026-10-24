using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>TwAttention.Pulse and TwChip.Pulse: rings spread out from a control a given number of times.</summary>
[Collection(UiCollection.Name)]
public class AttentionPulseTests
{
    private static Form Host()
    {
        var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), Size = new Size(300, 200), BackColor = Color.White };
        form.Show();
        return form;
    }

    [Fact]
    public void Pulses_run_the_given_number_of_times_then_stop() => Sta.Run(() =>
    {
        using var form = Host();
        var badge = new Label { Text = "3", AutoSize = false, Size = new Size(24, 20), Location = new Point(40, 40), BackColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White };
        form.Controls.Add(badge);
        Application.DoEvents();

        var start = DateTime.UtcNow;
        TwAttention.Pulse(badge, count: 2);
        Assert.True(TwAttention.IsPulsing(badge));
        using (var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) form.DrawToBitmap(bitmap, form.ClientRectangle);

        while (TwAttention.IsPulsing(badge) && DateTime.UtcNow - start < TimeSpan.FromSeconds(3))
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        var took = (DateTime.UtcNow - start).TotalMilliseconds;
        Assert.False(TwAttention.IsPulsing(badge));
        Assert.InRange(took, 2 * TwOutset.PingDuration - 50, 2 * TwOutset.PingDuration + 1000);
        Assert.False(TwOutset.For(badge).Animating);
        form.Close();
    });

    [Fact]
    public void Pulse_color_is_the_part_that_stands_out_from_the_parent() => Sta.Run(() =>
    {
        using var form = Host();
        var filled = new Label { BackColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White };
        var plain = new Label { BackColor = Color.Transparent, ForeColor = Color.FromArgb(185, 28, 28) };
        var soft = new Label { BackColor = Color.FromArgb(254, 226, 226), ForeColor = Color.FromArgb(185, 28, 28) };
        form.Controls.AddRange(new Control[] { filled, plain, soft });
        Assert.Equal(filled.BackColor, TwAttention.PulseColor(filled));
        Assert.Equal(plain.ForeColor, TwAttention.PulseColor(plain));
        Assert.Equal(soft.ForeColor, TwAttention.PulseColor(soft));                 // açık zeminli rozette yazı rengi
        form.Close();
    });

    [Fact]
    public void Chip_pulses_and_nothing_happens_without_motion() => Sta.Run(() =>
    {
        using var form = Host();
        var chip = new TwChip { Text = "Arızalı", Location = new Point(20, 20) };
        form.Controls.Add(chip);
        chip.Pulse(count: 1);
        Assert.True(TwAttention.IsPulsing(chip));

        var previous = TwMotion.Enabled;
        TwMotion.Enabled = false;
        try
        {
            var label = new Label { Location = new Point(20, 80) };
            form.Controls.Add(label);
            TwAttention.Pulse(label);
            Assert.False(TwAttention.IsPulsing(label));
        }
        finally
        {
            TwMotion.Enabled = previous;
        }
        using var orphan = new Label();
        TwAttention.Pulse(orphan);                                                  // ebeveynsiz kontrol: sessizce geçer
        Assert.False(TwAttention.IsPulsing(orphan));
        form.Close();
    });
}
