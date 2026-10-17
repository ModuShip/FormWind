#if NET9_0_OR_GREATER
using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

// Application.SetColorMode .NET 9'da deneysel (WFO5001).
#pragma warning disable WFO5001

namespace FormWind.Tests;

/// <summary>
/// <c>FORMWIND_COLORMODE=1</c> ile ayrı bir süreçte çalışır: <see cref="Application.SetColorMode"/> süreç geneline etki eder ve
/// ilk pencereden önce çağrılmalıdır. .NET 9+'ın koyu sistem renkleriyle FormWind class'larının birlikte çalıştığını doğrular.
/// </summary>
public sealed class ColorModeFactAttribute : FactAttribute
{
    public ColorModeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FORMWIND_COLORMODE") != "1")
            Skip = "Renk modu testleri FORMWIND_COLORMODE=1 ile ayrı çalıştırılır.";
    }
}

[Collection(UiCollection.Name)]
[Trait("Category", "ColorMode")]
public class ColorModeTests
{
    [ColorModeFact]
    public void Classes_work_on_top_of_the_dark_system_color_mode() => Sta.Run(() =>
    {
        Application.SetColorMode(SystemColorMode.Dark);
        if (!Application.IsDarkModeEnabled) return;   // Yüksek Karşıtlık gibi koyu modun kapalı olduğu sistemler

        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var plain = new TextBox();
        var styled = new TextBox();
        var themed = new TextBox();
        var button = new TwButton { Text = "Kaydet", Size = new Size(90, 30) };
        form.Controls.AddRange(new Control[] { plain, styled, themed, button });
        form.Show();
        var systemBack = plain.BackColor;
        Assert.True(systemBack.GetBrightness() < 0.5f, $"koyu sistem zemini bekleniyordu: {systemBack}");

        // DarkMode uygulamanın renk moduna bağlanır; dark: varyantları seçilir.
        using var provider = new TailwindProvider { DarkMode = Application.IsDarkModeEnabled };
        provider.SetClasses(styled, "bg-white text-gray-900");
        provider.SetClasses(themed, "bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100");
        provider.SetClasses(button, "btn-primary");

        Assert.Equal(Color.White.ToArgb(), styled.BackColor.ToArgb());                          // class rengi kazanır
        Assert.Equal(ColorTranslator.FromHtml("#1f2937").ToArgb(), themed.BackColor.ToArgb());   // dark:bg-gray-800
        using (var bitmap = new Bitmap(button.Width, button.Height))
            button.DrawToBitmap(bitmap, new Rectangle(Point.Empty, button.Size));

        provider.SetClasses(styled, "");
        Assert.Equal(systemBack.ToArgb(), styled.BackColor.ToArgb());                           // koyu sistem rengine döner
        form.Close();
    });
}
#endif
