using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>Regressions found when reviewing the 0.1.10 feedback changes.</summary>
[Collection(UiCollection.Name)]
public class ReviewFixTests
{
    private static Form HostForm(int width = 420, int height = 260) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(140, 140), ClientSize = new Size(width, height) };

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // A cover window that fails before it is shown is disposed; the count of fading pictures must not go below where it was.
    [Fact]
    public void A_cover_window_disposed_before_its_fade_leaves_the_fade_count_balanced() => Sta.Run(() =>
    {
        using var form = HostForm();
        form.Show();
        Application.DoEvents();
        int before = TwFadeOut.ActiveCount;
        var ghost = new TwFadeOut.Ghost(new Bitmap(10, 10), new Rectangle(0, 0, 10, 10), roundCorners: false, poolOwner: form);
        Assert.Equal(before + 1, TwFadeOut.ActiveCount);
        ghost.Dispose();
        Assert.Equal(before, TwFadeOut.ActiveCount);
    });

    // Quick navigation: the picture of the second cross-fade must contain the first one still fading on screen.
    [Fact]
    public void A_picture_taken_while_a_fade_runs_is_read_from_the_screen() => Sta.Run(() =>
    {
        using var form = HostForm();
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        form.Controls.Add(page);
        form.Show();
        form.Activate();
        Application.DoEvents();
        if (!TwMotion.CanAnimate(page)) return;

        TwTransition.CrossFade(page, () => page.BackColor = Color.Black, 2000);
        try
        {
            using var picture = TwCapture.Take(page, page.RectangleToScreen(page.ClientRectangle));
            Assert.NotEqual(TwCapture.Source.Window, TwCapture.LastSource);
        }
        finally
        {
            form.Close();
        }
    });

    [Fact]
    public void A_combo_box_with_its_native_edit_window_is_read_from_the_window_surface() => Sta.Run(() =>
    {
        using var form = HostForm();
        form.BackColor = Color.White;
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Location = new Point(20, 20), Width = 200 };
        combo.Items.Add("Hat 1");
        form.Controls.Add(combo);
        form.Show();
        Application.DoEvents();
        Assert.True(TwCapture.IsGdiWindowClass("Edit"));
        Assert.False(TwCapture.IsGdiWindowClass("Chrome_WidgetWin_1"));

        using var picture = TwCapture.Take(form, form.RectangleToScreen(form.ClientRectangle));
        Assert.Equal(TwCapture.Source.Window, TwCapture.LastSource);
    });

    [Fact]
    public void An_unstyled_select_on_a_transparent_parent_takes_the_first_opaque_background() => Sta.Run(() =>
    {
        using var form = HostForm();
        form.BackColor = ColorTranslator.FromHtml("#111827");
        var holder = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var select = new TwSelect { Width = 160, BackColor = Color.Transparent };
        holder.Controls.Add(select);
        form.Controls.Add(holder);
        Assert.Equal(form.BackColor.ToArgb(), select.MenuStyle.Back.ToArgb());
    });

    // A wheel step over a chip scrolls the strip once: the chip's unhandled wheel message also goes to the strip.
    [Fact]
    public void One_wheel_step_over_a_chip_scrolls_the_strip_once() => Sta.Run(() =>
    {
        using var form = HostForm(300, 100);
        var strip = new TwChipStrip { Dock = DockStyle.Top, Height = 40 };
        for (int i = 0; i < 10; i++) strip.Controls.Add(new Button { Text = "Hat " + i, Size = new Size(80, 28) });
        form.Controls.Add(strip);
        form.Show();
        Application.DoEvents();

        var chip = strip.Chips[1];
        var at = chip.PointToScreen(new Point(5, 5));
        const int WmMouseWheel = 0x020A;
        SendMessage(chip.Handle, WmMouseWheel, new IntPtr(-120 << 16), new IntPtr((at.Y << 16) | (at.X & 0xFFFF)));
        Application.DoEvents();
        Assert.Equal((int)Math.Round(48 * TwDpi.DeviceDpi(strip) / 96f), strip.ScrollOffset);
    });

    [Fact]
    public void Enter_opens_an_info_tip_even_on_a_form_with_an_accept_button() => Sta.Run(() =>
    {
        using var form = HostForm();
        var ok = new Button { Text = "Tamam", Location = new Point(10, 200) };
        int accepted = 0;
        ok.Click += (_, _) => accepted++;
        var tip = new InputTip { Title = "OEE", Description = "Kullanılabilirlik × performans × kalite.", Location = new Point(10, 10) };
        form.Controls.AddRange(new Control[] { ok, tip });
        form.AcceptButton = ok;
        Assert.True(tip.Accepts(Keys.Enter));
        Assert.True(tip.Accepts(Keys.Space));
        Assert.False(tip.Accepts(Keys.Escape));                                 // closed: Esc stays the form's
        Assert.Equal(0, accepted);
    });

    [Fact]
    public void Colors_given_to_the_card_before_it_first_opens_are_kept() => Sta.Run(() =>
    {
        using var form = HostForm();
        form.BackColor = ColorTranslator.FromHtml("#111827");
        var tip = new TwInfoTip { Title = "MTBF", Location = new Point(10, 10) };
        tip.Card.BackColor = Color.LemonChiffon;
        form.Controls.Add(tip);
        form.Show();
        Application.DoEvents();
        tip.ShowCard();
        Application.DoEvents();
        try
        {
            Assert.Equal(Color.LemonChiffon.ToArgb(), tip.Card.BackColor.ToArgb());
            Assert.Equal(tip.ForeColor.ToArgb(), tip.Card.ForeColor.ToArgb());   // the one not given still follows
        }
        finally
        {
            tip.HideCard();
        }
    });

    private sealed class InputTip : TwInfoTip
    {
        public bool Accepts(Keys key) => IsInputKey(key);
    }
}
