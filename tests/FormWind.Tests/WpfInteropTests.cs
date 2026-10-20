using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using FormWind.Tests.Performance;
using Xunit;
using Wpf = System.Windows;
using WpfControls = System.Windows.Controls;

namespace FormWind.Tests;

/// <summary>
/// <c>FORMWIND_WPF=1</c> ile, ayrı bir süreçte çalışır: .NET Framework'te ilk WPF penceresi süreci DPI farkında yapar (sonraki
/// görsel testler ekranın DPI'ıyla çizilir) ve SystemEvents penceresi onu ilk kullanan test iş parçacığıyla kapanır.
/// </summary>
public sealed class WpfFactAttribute : FactAttribute
{
    public WpfFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FORMWIND_WPF") != "1")
            Skip = "WPF testleri FORMWIND_WPF=1 ile ayrı çalıştırılır.";
    }
}

/// <summary>
/// WPF ile birlikte çalışma: WPF penceresine gömülü (WindowsFormsHost) FormWind'li WinForms kontrolleri, WinForms formuna
/// gömülü WPF içeriği (ElementHost) ve mesaj döngüsü WPF'in Dispatcher'ı iken açılan WinForms formları.
/// </summary>
[Collection(UiCollection.Name)]
[Trait("Category", "Wpf")]
public class WpfInteropTests
{
    private static Color Color(string hex) => ColorTranslator.FromHtml(hex);

    // WPF'in mesaj döngüsü: Application.DoEvents yerine Dispatcher'ın bekleyen işleri ve mesajları işlenir.
    private static void PumpWpf(int milliseconds = 0)
    {
        // Dispatcher bu süre boyunca mesaj döngüsünü çalıştırır (WM_TIMER dahil); süre bitince çerçeve kapanır.
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(Math.Max(1, milliseconds)) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private sealed class Panel : UserControl
    {
        public new readonly TextBox Text = new TextBox { Location = new Point(10, 10), Width = 150 };
        public readonly Button Save = new Button { Location = new Point(10, 40), Text = "Kaydet" };
        public readonly CheckBox Check = new CheckBox { Location = new Point(10, 70), Text = "Seçenek" };
        public readonly CheckBox Disabled = new CheckBox { Location = new Point(10, 95), Text = "Kapalı", Enabled = false };
        public readonly Label Caption = new Label { Location = new Point(200, 10), Text = "Başlık", AutoSize = true };
        public readonly ComboBox Kind = new ComboBox { Location = new Point(200, 40), DropDownStyle = ComboBoxStyle.DropDownList, Items = { "a", "b" } };
        public readonly ListView List = new ListView { Location = new Point(10, 130), Size = new Size(200, 90), View = View.Details, Columns = { "Ad" } };
        public readonly TabControl Tabs = new TabControl { Location = new Point(220, 130), Size = new Size(200, 90), TabPages = { "Bir", "İki" } };
        public readonly ProgressBar Progress = new ProgressBar { Location = new Point(10, 230), Width = 200, Value = 40 };
        public readonly PropertyGrid Properties = new PropertyGrid { Location = new Point(220, 230), Size = new Size(200, 120), SelectedObject = new Size(1, 2) };

        public Panel()
        {
            Size = new Size(440, 360);
            List.Groups.Add(new ListViewGroup("Grup"));
            List.Items.Add(new ListViewItem("x", List.Groups[0]));
            Controls.AddRange(new Control[] { Text, Save, Check, Disabled, Caption, Kind, List, Tabs, Progress, Properties });
        }
    }

    private static (Wpf.Window Window, WindowsFormsHost Host) ShowInWpf(Control child)
    {
        var host = new WindowsFormsHost { Child = child };
        var window = new Wpf.Window
        {
            Content = new WpfControls.DockPanel { Children = { host } },
            Width = 500, Height = 450, Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false,
        };
        window.Show();
        PumpWpf();
        return (window, host);
    }

    // Formsuz kullanım: ContainerControl bir UserControl; AutoStyle, ebeveynin zeminini kopyalayan kontrolleri bırakma
    // (FollowParentBackColor) ve koyu tema WPF penceresinde çalışır.
    [WpfFact]
    public void User_control_in_windows_forms_host_is_styled_without_a_form() => Sta.Run(() =>
    {
        var panel = new Panel { BackColor = Color("#272727") };
        panel.Caption.BackColor = panel.BackColor;                                         // uygulamanın kopyaladığı zemin
        var (window, _) = ShowInWpf(panel);
        using var provider = new TailwindProvider();
        try
        {
            provider.SetClasses(panel, "page");                                              // AutoStyle'dan önce
            var afterClasses = panel.BackColor;
            provider.ContainerControl = panel;
            provider.AutoStyle = true;
            PumpWpf();

            Assert.Null(panel.FindForm());
            Assert.True(afterClasses.ToArgb() == Color("#f9fafb").ToArgb(), $"SetClasses sonrası {afterClasses}");
            Assert.Equal("input", provider.GetAutomaticClasses(panel.Text));
            Assert.Equal("btn-ghost", provider.GetAutomaticClasses(panel.Save));
            Assert.Equal(Color("#f9fafb").ToArgb(), panel.BackColor.ToArgb());             // page
            Assert.Equal(panel.BackColor.ToArgb(), panel.Caption.BackColor.ToArgb());      // ebeveyni izler

            provider.DarkMode = true;
            PumpWpf();
            Assert.Equal(Color("#111827").ToArgb(), panel.BackColor.ToArgb());
            Assert.Equal(panel.BackColor.ToArgb(), panel.Caption.BackColor.ToArgb());
            Assert.Equal(Color("#111827").ToArgb(), panel.Text.BackColor.ToArgb());        // dark:bg-surface-900
            Assert.Empty(TwContrast.Audit(panel, provider));
        }
        finally
        {
            window.Close();
        }
    });

    // Klavye ile gelinen odakta halka (focus-visible): WinForms ileti filtreleri WPF'in Dispatcher'ında çağrılmaz;
    // izleme döngüden bağımsız olmalı.
    [WpfFact]
    public void Keyboard_input_is_seen_under_the_wpf_dispatcher() => Sta.Run(() =>
    {
        var panel = new Panel();
        var (window, _) = ShowInWpf(panel);
        using var provider = new TailwindProvider();
        try
        {
            provider.ContainerControl = panel;
            provider.AutoStyle = true;
            TwFocusVisible.LastInputWasKeyboard = false;
            PostMessage(panel.Text.Handle, 0x0100 /* WM_KEYDOWN */, (IntPtr)Keys.Tab, IntPtr.Zero);
            PumpWpf();
            Assert.True(TwFocusVisible.LastInputWasKeyboard, "WM_KEYDOWN WPF döngüsünde görülmedi");

            PostMessage(panel.Text.Handle, 0x0201 /* WM_LBUTTONDOWN */, IntPtr.Zero, IntPtr.Zero);
            PumpWpf();
            Assert.False(TwFocusVisible.LastInputWasKeyboard);
        }
        finally
        {
            window.Close();
        }
    });

#if !NETFRAMEWORK
    // WPF penceresi DPI'ı farklı bir monitöre taşınınca gömülü kontroller WM_DPICHANGED_AFTERPARENT alır; FormWind'in
    // yeniden uygulaması (BeginInvoke ile ertelenir) WPF döngüsünde çalışır ve değerleri yeni ölçekle verir. .NET Framework
    // DPI olaylarını yalnızca app.config'te PerMonitorV2 açıkken üretir; test süreci öyle olmadığından .NET'te denenir.
    [WpfFact]
    public void Dpi_change_restyles_hosted_controls() => Sta.Run(() =>
    {
        var panel = new Panel();
        var (window, _) = ShowInWpf(panel);
        using var provider = new TailwindProvider();
        try
        {
            panel.Save.Size = new Size(200, 60);                                             // yazı dolguyla sığar
            provider.SetClasses(panel.Save, "px-4 py-2");
            Assert.Equal(16, panel.Save.Padding.Left);
            TwDpi.DeviceDpiOverride = 144;
            SendMessage(panel.Save.Handle, 0x02E3 /* WM_DPICHANGED_AFTERPARENT */, IntPtr.Zero, IntPtr.Zero);
            PumpWpf(50);
            Assert.Equal(24, panel.Save.Padding.Left);
        }
        finally
        {
            TwDpi.DeviceDpiOverride = null;
            window.Close();
        }
    });
#endif

    // Klavyeyle gezinmede odak işaretleri: Tab, WinForms kontrolleri arasında odağı taşır ve Windows'un odak işaretleri
    // (UISF_HIDEFOCUS) açılır; FormWind'in kendi çizdiği halkalar (sekme şeridi, DropDownList) TwFocusCues'a uyar.
    [WpfFact]
    public void Tab_navigation_inside_the_host_shows_focus_cues() => Sta.Run(() =>
    {
        var panel = new Panel();
        var (window, _) = ShowInWpf(panel);
        using var provider = new TailwindProvider();
        try
        {
            window.Activate();
            provider.ContainerControl = panel;
            provider.AutoStyle = true;
            panel.Text.Focus();
            PumpWpf(50);
            var log = $"başta odak: {Describe(panel)} ipucu: {TwFocusCues.Shown(panel.Text)}";
            PostMessage(panel.Text.Handle, 0x0100 /* WM_KEYDOWN */, (IntPtr)Keys.Tab, IntPtr.Zero);
            PostMessage(panel.Text.Handle, 0x0101 /* WM_KEYUP */, (IntPtr)Keys.Tab, (IntPtr)unchecked((int)0xC0000001));
            PumpWpf(50);
            var focused = Focused(panel);
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "fw-wpf-focus.txt"),
                log + Environment.NewLine + $"Tab sonrası odak: {focused?.GetType().Name} {focused?.Text}; ipucu: {(focused != null && TwFocusCues.Shown(focused))}; klavye: {TwFocusVisible.LastInputWasKeyboard}");
            Assert.NotNull(focused);
            Assert.NotSame(panel.Text, focused);
            Assert.True(TwFocusVisible.LastInputWasKeyboard);
            Assert.True(TwFocusCues.Shown(focused!), "odak işaretleri gizli");
        }
        finally
        {
            window.Close();
        }
    });

    // Sistem teması, yüksek karşıtlık ve metin boyutu değişimi FormWind'e SystemEvents.UserPreferenceChanged ile gelir;
    // SystemEvents olayı abonenin SynchronizationContext'ine (burada WPF'in Dispatcher'ı) gönderir. Sistem ayarı
    // değiştirilmeden olay SystemEvents'in kendi dağıtım yöntemiyle tetiklenir (gizli penceresi testlerde onu ilk kullanan,
    // çoktan bitmiş iş parçacığında kalabildiğinden pencereye ileti gönderilmez).
    [WpfFact]
    public void System_setting_changes_reach_the_wpf_thread() => Sta.Run(() =>
    {
        var (window, _) = ShowInWpf(new Panel());
        int thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
        var calls = new List<(int Thread, Microsoft.Win32.UserPreferenceCategory Category)>();
        Microsoft.Win32.UserPreferenceChangedEventHandler handler = (_, e) => calls.Add((System.Threading.Thread.CurrentThread.ManagedThreadId, e.Category));
        using var provider = new TailwindProvider();                                       // TwHighContrast/TwTextScale abone olur
        using var button = new Button();
        provider.SetClasses(button, "btn");
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += handler;
        try
        {
            // Olay başka bir iş parçacığından tetiklenir (SystemEvents'in kendi iş parçacığı gibi). SystemEvents abonelere
            // eşzamanlı (Send) iletir: tetikleyen beklenmez, bu sırada WPF döngüsü çalışır. Önceki testlerin bitmiş iş
            // parçacıklarındaki aboneler tetikleyeni bekletebilir; arka plan iş parçacığı süreci tutmaz.
            var raise = new System.Threading.Thread(() => RaiseUserPreferenceChanged(Microsoft.Win32.UserPreferenceCategory.Color)) { IsBackground = true };
            raise.Start();
            var watch = Stopwatch.StartNew();
            while (!calls.Any(c => c.Category == Microsoft.Win32.UserPreferenceCategory.Color) && watch.ElapsedMilliseconds < 3000) PumpWpf(20);
            Assert.Contains(calls, c => c.Thread == thread && c.Category == Microsoft.Win32.UserPreferenceCategory.Color);
        }
        finally
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= handler;
            window.Close();
        }
    });

    // SystemEvents.RaiseEvent: abonelere kendi SynchronizationContext'lerinde iletir (.NET Framework ve .NET'te imzalar farklı).
    private static void RaiseUserPreferenceChanged(Microsoft.Win32.UserPreferenceCategory category)
    {
        const System.Reflection.BindingFlags Static = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var type = typeof(Microsoft.Win32.SystemEvents);
        var key = (type.GetField("s_onUserPreferenceChangedEvent", Static) ?? type.GetField("OnUserPreferenceChangedEvent", Static))!.GetValue(null)!;
        var args = new object[] { null!, new Microsoft.Win32.UserPreferenceChangedEventArgs(category) };
        foreach (var method in type.GetMethods(Static).Where(m => m.Name == "RaiseEvent"))
        {
            var parameters = method.GetParameters();
            if (parameters.Length == 2 && parameters[0].ParameterType == typeof(object))
            {
                method.Invoke(null, new object[] { key, args });
                return;
            }
            if (parameters.Length == 3 && parameters[0].ParameterType == typeof(bool))
            {
                method.Invoke(null, new object[] { false, key, args });
                return;
            }
        }
        throw new MissingMethodException(type.FullName, "RaiseEvent");
    }

    private static Control? Focused(Control root)
    {
        if (root.Focused) return root;
        foreach (Control child in root.Controls)
            if (Focused(child) is Control found) return found;
        return null;
    }

    private static string Describe(Control root) => Focused(root) is Control c ? c.GetType().Name : "yok";

    // WinForms zamanlayıcıları (renk geçişleri, hover takibi) WPF'in döngüsünde de çalışır (WM_TIMER).
    [WpfFact]
    public void Transitions_finish_under_the_wpf_dispatcher() => Sta.Run(() =>
    {
        var panel = new Panel();
        var (window, _) = ShowInWpf(panel);
        using var provider = new TailwindProvider();
        try
        {
            provider.SetClasses(panel.Save, "bg-blue-600 dark:bg-red-600 transition duration-150");
            PumpWpf();
            provider.DarkMode = true;
            Assert.True(TwAnimator.IsAnimating(panel.Save, TwColorChannel.Back));
            PumpWpf(400);
            Assert.False(TwAnimator.IsAnimating(panel.Save, TwColorChannel.Back));
            Assert.Equal(Color("#dc2626").ToArgb(), panel.Save.BackColor.ToArgb());
        }
        finally
        {
            window.Close();
        }
    });

    // WinForms formuna gömülü WPF içeriği: AutoStyle ElementHost'a bileşen vermez, kontrast denetimi onu metin taşıyan bir
    // kontrol saymaz (içindeki WPF yazısını göremez; yanlış bulgu üretmemeli); formun zeminini kopyalamışsa onu izler.
    [WpfFact]
    public void Element_host_is_left_to_wpf() => Sta.Run(() =>
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, BackColor = Color("#272727") };
        var host = new ElementHost
        {
            Bounds = new Rectangle(10, 10, 200, 100),
            BackColor = form.BackColor,
            Child = new WpfControls.TextBlock { Text = "WPF yazısı", Foreground = System.Windows.Media.Brushes.Black },
        };
        var plain = new ElementHost { Bounds = new Rectangle(10, 120, 200, 60), Child = new WpfControls.Button { Content = "WPF" } };
        form.Controls.Add(host);
        form.Controls.Add(plain);
        using var provider = new TailwindProvider { DarkMode = true };
        form.Show();
        provider.ContainerControl = form;
        provider.AutoStyle = true;
        Application.DoEvents();

        Assert.Null(provider.GetAutomaticClasses(host));
        Assert.Null(provider.GetAutomaticClasses(plain));
        Assert.Equal(form.BackColor.ToArgb(), host.BackColor.ToArgb());                   // kopyalanan zemin sayfayı izler
        Assert.Equal(Color("#111827").ToArgb(), form.BackColor.ToArgb());
        Assert.DoesNotContain(TwContrast.Audit(form, provider), i => i.Control is ElementHost);
        form.Close();
    });

    // Mesaj döngüsü WPF'in Dispatcher'ı iken açılan WinForms formları: TwAutoTheme (döngüden bağımsız kanca) modeless ve modal
    // formları görünmeden önce stiller.
    [WpfFact]
    public void Forms_opened_from_wpf_are_styled_by_auto_theme() => Sta.Run(() =>
    {
        var (window, _) = ShowInWpf(new Panel());
        TwAutoTheme.Install(Path.Combine(Path.GetTempPath(), "formwind-no-theme.json"));
        try
        {
            using var modeless = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
            var text = new TextBox();
            modeless.Controls.Add(text);
            bool styledWhenShown = false;
            modeless.VisibleChanged += (_, _) => styledWhenShown |= modeless.Visible && TwAutoTheme.GetProvider(modeless) != null;
            modeless.Show();
            PumpWpf();
            Assert.True(styledWhenShown);
            Assert.Equal("input", TwAutoTheme.GetProvider(modeless)!.GetAutomaticClasses(text));

            using var modal = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
            TailwindProvider? modalProvider = null;
            modal.Shown += (_, _) =>
            {
                modalProvider = TwAutoTheme.GetProvider(modal);
                modal.BeginInvoke(new Action(modal.Close));
            };
            modal.ShowDialog();
            Assert.NotNull(modalProvider);
            modeless.Close();
        }
        finally
        {
            TwAutoTheme.Uninstall();
            window.Close();
        }
    });

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
}
