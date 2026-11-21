using System;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// The 0.1.10-preview.3 user feedback report: square scroll buttons when stretched, the chip's class height in a strip,
/// the strip's scroll fade, the grid row lock and info tip hover from icon to icon driven by a stand-in pointer.
/// </summary>
[Collection(UiCollection.Name)]
public class Feedback0110Preview3Tests
{
    private static Form HostForm(int width = 640, int height = 200) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(width, height) };

    private static Button Chip(Color? back = null)
    {
        var chip = new Button { AutoSize = false, Size = new Size(80, 26), FlatStyle = FlatStyle.Flat, BackColor = back ?? SystemColors.Control };
        chip.FlatAppearance.BorderSize = 0;
        return chip;
    }

    private static TwChipStrip FixedStrip(Form form, int width, int chips, int height = 40, Color? chipBack = null)
    {
        var strip = new TwChipStrip { Size = new Size(width, height), Gap = 6 };
        for (int i = 0; i < chips; i++) strip.Controls.Add(Chip(chipBack));
        form.Controls.Add(strip);
        form.Show();
        Application.DoEvents();
        return strip;
    }

    private static bool PumpUntil(Func<bool> done, int milliseconds)
    {
        var clock = Stopwatch.StartNew();
        while (!done() && clock.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        return done();
    }

    // ---- F: the ‹ › buttons are square in every alignment ----

    [Fact]
    public void Stretched_scroll_buttons_are_square() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = FixedStrip(form, 200, 3, height: 44);
        strip.Padding = new Padding(0, 6, 0, 0);
        strip.ChipAlignment = TwAlign.Stretch;

        Assert.True(strip.NextButton.Visible);
        Assert.Equal(38, strip.NextButton.Height);
        Assert.Equal(strip.NextButton.Height, strip.NextButton.Width);
        Assert.Equal(strip.Chips[0].Top, strip.NextButton.Top);
    });

    [Fact]
    public void Very_tall_stretched_rows_keep_a_capped_square_button_in_the_middle() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = FixedStrip(form, 200, 3, height: 120);
        strip.ChipAlignment = TwAlign.Stretch;

        int side = (int)Math.Round(48 * TwDpi.DeviceDpi(strip) / 96f);
        Assert.Equal(new Size(side, side), strip.NextButton.Size);
        Assert.Equal((strip.ClientSize.Height - side) / 2, strip.NextButton.Top);
    });

    // ---- G: the chip's class height inside a strip ----

    [Fact]
    public void Chip_in_a_strip_keeps_and_reports_its_class_height() => Sta.Run(() =>
    {
        using var form = HostForm();
        using var tw = new TailwindProvider();
        var strip = new TwChipStrip { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var chip = new TwChip { Text = "Durum: Açık", AutoSize = true };
        strip.Controls.Add(chip);
        form.Controls.Add(strip);
        tw.SetClasses(chip, "chip px-3 h-[26px]");
        form.Show();
        Application.DoEvents();

        int expected = (int)Math.Round(26 * TwDpi.DeviceDpi(chip) / 96f);
        Assert.Equal(expected, chip.ClassHeight);
        Assert.Equal(expected, chip.Height);
    });

    // ---- Request 3: scroll fade ----

    [Fact]
    public void Scroll_fade_lies_next_to_the_shown_button_and_is_not_a_chip() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = FixedStrip(form, 200, 3);
        strip.ScrollFade = true;
        Application.DoEvents();

        Assert.Equal(3, strip.Chips.Length);
        var fades = strip.Controls.OfType<TwScrollFade>().ToArray();
        Assert.Equal(2, fades.Length);
        var shown = Assert.Single(fades, f => f.Visible);
        Assert.Equal(strip.NextButton.Left, shown.Right);
        Assert.Equal(strip.Chips[0].Top, shown.Top);
        Assert.Equal(strip.Chips[0].Height, shown.Height);
        Assert.True(strip.Controls.GetChildIndex(shown) < strip.Controls.GetChildIndex(strip.Chips[1]));   // over the chips

        strip.ScrollOffset = strip.MaxScrollOffset;
        shown = Assert.Single(strip.Controls.OfType<TwScrollFade>(), f => f.Visible);
        Assert.Equal(strip.PreviousButton.Right, shown.Left);

        strip.ScrollFade = false;
        Assert.Empty(strip.Controls.OfType<TwScrollFade>());
    });

    [Fact]
    public void Scroll_fade_comes_back_after_Controls_is_cleared() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = FixedStrip(form, 200, 3);
        strip.ScrollFade = true;
        strip.Controls.Clear();
        Application.DoEvents();

        Assert.Equal(2, strip.Controls.OfType<TwScrollFade>().Count());
        Assert.Empty(strip.Chips);
    });

    [Fact]
    public void Scroll_fade_blends_the_chips_into_the_strip_background() => Sta.Run(() =>
    {
        using var form = HostForm();
        var strip = FixedStrip(form, 200, 3, chipBack: Color.Red);
        strip.BackColor = Color.White;
        strip.ScrollFade = true;
        Application.DoEvents();

        var fade = strip.Controls.OfType<TwScrollFade>().Single(f => f.Visible);
        using var picture = TwOffscreen.Render(form);
        // The picture starts at the window's visible frame, not the client area.
        var frame = TwCapture.VisibleFormBounds(form).Location;
        var origin = strip.PointToScreen(Point.Empty) - new Size(frame);
        int y = origin.Y + fade.Top + fade.Height / 2;
        var far = picture.GetPixel(origin.X + fade.Left, y);              // chip side: the chip shows
        var near = picture.GetPixel(origin.X + fade.Right - 1, y);        // button side: the strip's background
        Assert.True(far.R > 200 && far.G < 80, far.ToString());
        Assert.True(near.G > 200 && near.B > 200, near.ToString());
    });

    // ---- Request 1 (H): grid row lock ----

    [Fact]
    public void Row_lock_cancels_editing_and_keeps_the_list_closed_in_locked_rows() => Sta.Run(() =>
    {
        using var form = HostForm(500, 300);
        var table = new DataTable();
        table.Columns.Add("Line", typeof(string));
        table.Columns.Add("Note", typeof(string));
        table.Columns.Add("Plain", typeof(string));
        table.Rows.Add("P1", "a", "x");
        table.Rows.Add("P2", "b", "y");
        var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false };
        var line = new TwSelectColumn { DataPropertyName = "Line", Width = 120 };
        line.Items.AddRange(new object[] { "P1", "P2" });
        grid.Columns.Add(line);
        grid.Columns.Add(new TwTextColumn { DataPropertyName = "Note", Width = 120, EditOnFocus = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Plain", Width = 120 });
        grid.DataSource = table;
        form.Controls.Add(grid);
        form.Show();
        form.Activate();
        Application.DoEvents();

        Func<int, bool> locked = row => row == 1;
        TwGrid.SetRowLock(grid, locked);
        Assert.Same(locked, TwGrid.GetRowLock(grid));

        foreach (int column in new[] { 1, 2 })
        {
            grid.CurrentCell = grid[column, 1];
            Assert.False(grid.BeginEdit(true));
            grid.CurrentCell = grid[column, 0];
            Assert.True(grid.BeginEdit(true));
            grid.EndEdit();
        }

        ((TwSelectCell)grid[0, 1]).ShowDropDown(1);
        Assert.Null(TwSelectCell.OpenMenu);
        Assert.True(line.IsLockedAt(1));
        Assert.False(line.IsLockedAt(0));

        TwGrid.SetRowLock(grid, null);
        Assert.Null(TwGrid.GetRowLock(grid));
        grid.CurrentCell = grid[1, 1];
        Assert.True(grid.BeginEdit(true));
        grid.EndEdit();
    });

    // ---- Request 4 / R6: hover from icon to icon, with a stand-in pointer ----

    [Fact]
    public void Info_tip_under_the_pointer_opens_after_the_other_card_closes() => Sta.Run(() =>
    {
        var original = TwPopover.PointerSource;
        try
        {
            using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(40, 40), ClientSize = new Size(300, 120) };
            var a = new TwInfoTip { Title = "OEE", Description = "A", Location = new Point(20, 20) };
            var b = new TwInfoTip { Title = "MTBF", Description = "B", Location = new Point(80, 20) };
            form.Controls.AddRange(new Control[] { a, b });
            form.Show();
            Application.DoEvents();

            var pointer = a.PointToScreen(new Point(a.Width / 2, a.Height / 2));
            TwPopover.PointerSource = () => pointer;
            var onMouseEnter = typeof(Control).GetMethod("OnMouseEnter", BindingFlags.NonPublic | BindingFlags.Instance)!;
            onMouseEnter.Invoke(a, new object[] { EventArgs.Empty });
            Assert.True(PumpUntil(() => a.IsOpen, 2000), "A's card did not open on hover");

            // The pointer moves onto B while A's card is open; B gets no MouseEnter and the pointer then stays still.
            pointer = b.PointToScreen(new Point(b.Width / 2, b.Height / 2));
            Assert.True(PumpUntil(() => b.IsOpen, 3000), "B's card did not open after A's closed");
            Assert.False(a.IsOpen);
            b.HideCard();
        }
        finally
        {
            TwPopover.PointerSource = original;
        }
    });
}
