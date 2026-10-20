using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// FormWind başka bir uygulamanın içinde (ör. KeePass eklentisi): barındıran uygulamanın formlarına sızmamalı, bağımlılıkları
/// binding redirect olmadan yüklenmeli; aynı süreçteki iki bileşenin sağlayıcıları birbirini bozmamalı.
/// </summary>
[Collection(UiCollection.Name)]
public class PluginHostTests
{
    // KeePass'in exe.config'inde redirect yokken System.Memory, Unsafe 4.0.4.1'i ister (dosya 6.0.0.0); çözücü daha yeni
    // sürümü verir. Önceden çözücü kendi içinde System.Memory'ye dokunduğu için olay kendini çağırıp yığını taşırıyordu.
    [Fact]
    public void Dependency_resolver_binds_older_references_without_recursing()
    {
        // Çözücü yalnızca .NET Framework'te kurulur; .NET'te bu derlemeler framework'ün içindedir.
#if NETFRAMEWORK
        var unsafeRef = "System.Runtime.CompilerServices.Unsafe, Version=4.0.4.1, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
        var resolved = TwDependencyResolver.Resolve(unsafeRef);
        Assert.NotNull(resolved);
        Assert.Equal("System.Runtime.CompilerServices.Unsafe", resolved!.GetName().Name);
        Assert.True(resolved.GetName().Version >= new Version(4, 0, 4, 1));
#endif

        Assert.Null(TwDependencyResolver.Resolve("System.Text.Json, Version=999.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"));
        Assert.Null(TwDependencyResolver.Resolve("System.Runtime.CompilerServices.Unsafe, Version=4.0.4.1, Culture=neutral, PublicKeyToken=0000000000000000"));
        Assert.Null(TwDependencyResolver.Resolve("KeePass, Version=2.61.1.0, Culture=neutral, PublicKeyToken=null"));
        Assert.Null(TwDependencyResolver.Resolve("not an assembly name ,,,"));
    }

    // Eklentinin koyu diyaloğu barındıran uygulamanın ipuçlarını koyulaştırmamalı (önceden iş parçacığındaki tüm ipuçları
    // koyulaşıyordu: KeePass'in ana penceresinin ipuçları da).
    [Fact]
    public void Dark_provider_themes_only_tooltips_of_its_own_forms() => Sta.Run(() =>
    {
        using var host = NewForm(out var hostButton);
        using var plugin = NewForm(out var pluginButton);
        using var hostTip = new ToolTip();
        hostTip.SetToolTip(hostButton, "barındıran");
        hostTip.Show("barındıran", hostButton);
        Application.DoEvents();

        using var provider = new TailwindProvider { ContainerControl = plugin, DarkMode = true };
        using var pluginTip = new ToolTip();
        pluginTip.SetToolTip(pluginButton, "eklenti");
        pluginTip.Show("eklenti", pluginButton);
        using var hostLater = new ToolTip();
        hostLater.SetToolTip(hostButton, "sonra");
        hostLater.Show("sonra", hostButton);
        Application.DoEvents();

        Assert.Contains(HandleOf(pluginTip), TwNativeToolTips.Themed);
        Assert.DoesNotContain(HandleOf(hostTip), TwNativeToolTips.Themed);
        Assert.DoesNotContain(HandleOf(hostLater), TwNativeToolTips.Themed);
        Assert.False(TwNativeToolTips.ShouldBeDark(HandleOf(hostLater)));
    });

    // İki bileşenin sağlayıcıları: biri kapanınca (Dispose) diğerinin koyu ipuçları ve kancası kalır.
    [Fact]
    public void Disposing_one_provider_keeps_the_other_providers_tooltips() => Sta.Run(() =>
    {
        using var first = NewForm(out var firstButton);
        using var second = NewForm(out var secondButton);
        var firstProvider = new TailwindProvider { ContainerControl = first, DarkMode = true };
        using var secondProvider = new TailwindProvider { ContainerControl = second, DarkMode = true };
        using var firstTip = new ToolTip();
        firstTip.SetToolTip(firstButton, "bir");
        firstTip.Show("bir", firstButton);
        using var secondTip = new ToolTip();
        secondTip.SetToolTip(secondButton, "iki");
        secondTip.Show("iki", secondButton);
        Application.DoEvents();
        Assert.Contains(HandleOf(firstTip), TwNativeToolTips.Themed);
        Assert.Contains(HandleOf(secondTip), TwNativeToolTips.Themed);

        firstProvider.Dispose();
        Assert.DoesNotContain(HandleOf(firstTip), TwNativeToolTips.Themed);
        Assert.Contains(HandleOf(secondTip), TwNativeToolTips.Themed);
        Assert.True(TwNativeToolTips.HookInstalled);

        using var secondLater = new ToolTip();
        secondLater.SetToolTip(secondButton, "sonra");
        secondLater.Show("sonra", secondButton);
        Application.DoEvents();
        Assert.Contains(HandleOf(secondLater), TwNativeToolTips.Themed);
    });

    // Eklentinin kendi teması sağlayıcıya verilir; süreç geneli TwTheme.Current'a (barındıran uygulamanın ve diğer
    // eklentilerin) dokunmaz.
    [Fact]
    public void Provider_theme_leaves_the_process_wide_theme_alone() => Sta.Run(() =>
    {
        var current = TwTheme.Current;
        using var form = NewForm(out var button);
        using var provider = new TailwindProvider
        {
            Theme = TwTheme.FromJson("{ \"colors\": { \"primary\": { \"600\": \"#7c3aed\" } } }"),
            ContainerControl = form,
        };
        provider.SetClasses(button, "bg-primary-600");
        var brand = Color.FromArgb(0x7c, 0x3a, 0xed).ToArgb();
        Assert.Equal(brand, button.BackColor.ToArgb());
        Assert.Same(current, TwTheme.Current);
        Assert.False(TwTheme.Current.TryGetColor("primary", "600", out var shared) && shared.ToArgb() == brand);
    });

    private static Form NewForm(out Button button)
    {
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        button = new Button();
        form.Controls.Add(button);
        form.Show();
        return form;
    }

    private static IntPtr HandleOf(ToolTip tip) =>
        (IntPtr)typeof(ToolTip).GetProperty("Handle", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tip)!;
}
