using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>TwButton.IsBusy: spinner before the text, clicks ignored, busy state for screen readers.</summary>
[Collection(UiCollection.Name)]
public class ButtonBusyTests
{
    private static Bitmap Render(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    [Fact]
    public void Busy_button_ignores_clicks_and_keeps_its_dialog_result_unset() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
        var button = new TwButton { Text = "Kaydet", DialogResult = DialogResult.OK, Size = new Size(120, 36) };
        form.Controls.Add(button);
        form.AcceptButton = button;
        int clicks = 0;
        button.Click += (_, _) => clicks++;
        form.Show();

        button.IsBusy = true;
        button.PerformClick();
        button.AccessibilityObject.DoDefaultAction();
        Assert.Equal(0, clicks);
        Assert.Equal(DialogResult.None, form.DialogResult);
        Assert.True((button.AccessibilityObject.State & AccessibleStates.Busy) != 0);
        Assert.Equal(AccessibleRole.PushButton, button.AccessibilityObject.Role);

        button.IsBusy = false;
        Assert.True((button.AccessibilityObject.State & AccessibleStates.Busy) == 0);
        button.PerformClick();
        Assert.Equal(1, clicks);
        form.Close();
    });

    [Fact]
    public void Spinner_is_drawn_before_the_text_and_turns() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
        var button = new TwButton { Text = "Kaydet", Size = new Size(160, 40), BackColor = Color.White, ForeColor = Color.Black };
        form.Controls.Add(button);
        form.Show();
        int before = TwAnimations.Count;

        using var idle = Render(button);
        button.IsBusy = true;
        Assert.Equal(before + 1, TwAnimations.Count);                               // her karede yeniden çizilir
        using var busy = Render(button);

        // Gösterge metnin önünde: ilk koyu sütun metnin eski başlangıcından soldadır, metin de sağa kayar.
        int FirstDark(Bitmap b)
        {
            for (int x = 2; x < b.Width - 2; x++)
                for (int y = 4; y < b.Height - 4; y++)
                    if (b.GetPixel(x, y).GetBrightness() < 0.5f) return x;
            return -1;
        }
        int LastDark(Bitmap b)
        {
            for (int x = b.Width - 3; x >= 2; x--)
                for (int y = 4; y < b.Height - 4; y++)
                    if (b.GetPixel(x, y).GetBrightness() < 0.5f) return x;
            return -1;
        }
        Assert.True(FirstDark(busy) < FirstDark(idle));
        Assert.True(LastDark(busy) > LastDark(idle));

        button.IsBusy = false;
        Assert.Equal(before, TwAnimations.Count);
        form.Close();
    });

    [Fact]
    public void Auto_size_button_makes_room_for_the_spinner() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000) };
        var button = new TwButton { Text = "Kaydet", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        form.Controls.Add(button);
        form.Show();
        int width = button.Width;
        button.IsBusy = true;
        Assert.True(button.Width > width);
        button.IsBusy = false;
        Assert.Equal(width, button.Width);
        form.Close();
    });
}
