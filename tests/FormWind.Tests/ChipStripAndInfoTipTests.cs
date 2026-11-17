using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// <see cref="TwChipStrip"/> and <see cref="TwInfoTip"/> (0.1.8 report: a chip strip without scroll bar and a concept info
/// icon kept in the application).
/// </summary>
[Collection(UiCollection.Name)]
public class ChipStripAndInfoTipTests
{
    private static Form HostForm(int width = 300, int height = 120) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(width, height) };

    private static TwChipStrip StripWith(Form form, int chips)
    {
        var strip = new TwChipStrip { Dock = DockStyle.Top, Height = 40 };
        for (int i = 0; i < chips; i++) strip.Controls.Add(new Button { Text = "Hat " + i, AutoSize = false, Size = new Size(80, 28) });
        form.Controls.Add(strip);
        form.Show();
        Application.DoEvents();
        return strip;
    }

    [Fact]
    public void Chips_that_fit_are_laid_out_in_a_row_without_buttons() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = StripWith(form, 2);
        var chips = strip.Chips;
        Assert.Equal(2, chips.Length);
        Assert.True(chips[1].Left > chips[0].Right);
        Assert.Equal(chips[0].Top, chips[1].Top);
        Assert.Equal(0, strip.MaxScrollOffset);
        Assert.False(strip.PreviousButton.Visible);
        Assert.False(strip.NextButton.Visible);
    });

    [Fact]
    public void Chips_that_overflow_scroll_and_show_the_button_of_the_hidden_side() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = StripWith(form, 8);
        Assert.True(strip.MaxScrollOffset > 0);
        Assert.False(strip.PreviousButton.Visible);
        Assert.True(strip.NextButton.Visible);

        int left = strip.Chips[0].Left;
        strip.ScrollBy(50);
        Assert.Equal(50, strip.ScrollOffset);
        Assert.Equal(left - 50, strip.Chips[0].Left);
        Assert.True(strip.PreviousButton.Visible);

        strip.ScrollOffset = int.MaxValue;
        Assert.Equal(strip.MaxScrollOffset, strip.ScrollOffset);
        Assert.False(strip.NextButton.Visible);
        strip.ScrollOffset = -10;
        Assert.Equal(0, strip.ScrollOffset);
    });

    [Fact]
    public void EnsureVisible_scrolls_a_hidden_chip_into_view() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = StripWith(form, 8);
        int changes = 0;
        strip.ScrollChanged += (_, _) => changes++;
        var last = strip.Chips.Last();
        strip.EnsureVisible(last);
        Assert.Equal(1, changes);
        Assert.True(last.Right <= strip.NextButton.Left || !strip.NextButton.Visible);
        Assert.True(last.Left >= 0);
        strip.EnsureVisible(last);                                                 // already shown: no scroll
        Assert.Equal(1, changes);
        Assert.Throws<ArgumentNullException>(() => strip.EnsureVisible(null!));
    });

    [Fact]
    public void Info_tip_opens_a_card_with_its_title_and_description_in_the_colors_around_it() => Sta.Run(() =>
    {
        using var form = HostForm();
        form.BackColor = ColorTranslator.FromHtml("#1f2937");
        form.ForeColor = Color.White;
        var tip = new TwInfoTip { Title = "OEE", Description = "Kullanılabilirlik × performans × kalite.", Location = new Point(10, 10) };
        form.Controls.Add(tip);
        form.Show();
        Application.DoEvents();

        Assert.Equal("OEE", tip.AccessibilityObject.Name);
        tip.ShowCard();
        Application.DoEvents();
        try
        {
            Assert.True(tip.IsOpen);
            Assert.Equal(form.BackColor.ToArgb(), tip.Card.BackColor.ToArgb());
            Assert.Equal(Color.White.ToArgb(), tip.Card.ForeColor.ToArgb());
            var texts = tip.Card.Controls.OfType<Label>().Select(l => l.Text).ToArray();
            Assert.Contains("OEE", texts);
            Assert.Contains("Kullanılabilirlik × performans × kalite.", texts);
            Assert.True(tip.Card.Width <= Math.Round(tip.MaxCardWidth * TwDpi.DeviceDpi(tip) / 96f));
        }
        finally
        {
            tip.HideCard();
            Application.DoEvents();
        }
        Assert.False(tip.IsOpen);
    });

    [Fact]
    public void Info_tip_keeps_colors_the_application_gave_its_card() => Sta.Run(() =>
    {
        using var form = HostForm();
        var tip = new TwInfoTip { Title = "MTBF", Location = new Point(10, 10) };
        form.Controls.Add(tip);
        form.Show();
        Application.DoEvents();
        tip.ShowCard();
        tip.HideCard();
        tip.Card.BackColor = Color.LemonChiffon;
        tip.ShowCard();
        Application.DoEvents();
        try
        {
            Assert.Equal(Color.LemonChiffon.ToArgb(), tip.Card.BackColor.ToArgb());
        }
        finally
        {
            tip.HideCard();
        }
    });

    [Fact]
    public void Info_tip_opens_and_closes_with_the_keyboard() => Sta.Run(() =>
    {
        using var form = HostForm();
        var tip = new KeyTip { Title = "Takt", Description = "Talep hızına göre bir parçanın süresi.", Location = new Point(10, 10) };
        form.Controls.Add(tip);
        form.Show();
        Application.DoEvents();
        tip.Press(Keys.Enter);
        Application.DoEvents();
        Assert.True(tip.IsOpen);
        tip.Press(Keys.Escape);
        Application.DoEvents();
        Assert.False(tip.IsOpen);
    });

    private sealed class KeyTip : TwInfoTip
    {
        public void Press(Keys key) => OnKeyDown(new KeyEventArgs(key));
    }
}
