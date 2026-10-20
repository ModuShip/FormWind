using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Xunit;
using Xunit.Abstractions;

namespace FormWind.Tests.Performance;

/// <summary>
/// Büyük formlar: 1000–5000 kontrollü, gerçekçi karışımlı (sekmeler, paneller içinde iç içe kontroller, grid ve listeler)
/// formlarda ilk stilleme, tema geçişi ve kontrol başına bellek; sonradan eklenen kontrollerin süresinin kontrol sayısıyla
/// büyümesi; saniyede onlarca tema geçişi. Eşikler sabit milisaniye değil, kontrol başına süre ve büyüme oranıdır.
/// </summary>
[Collection(PerformanceCollection.Name)]
[Trait("Category", "Performance")]
public class LargeFormTests
{
    private readonly ITestOutputHelper _output;

    public LargeFormTests(ITestOutputHelper output) => _output = output;

    internal sealed class LargeForm : Form
    {
        public readonly List<(Control Control, string Classes)> Items = new List<(Control, string)>();

        public LargeForm(int count)
        {
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-20000, -20000);
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(1200, 900);
            SuspendLayout();
            var tabs = new TabControl { Dock = DockStyle.Fill };
            var pages = new[] { new TabPage("Genel"), new TabPage("Ayrıntı") };
            tabs.TabPages.AddRange(pages);
            Controls.Add(tabs);
            Items.Add((tabs, "tab-control"));
            var hosts = pages.Select(p =>
            {
                var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
                p.Controls.Add(host);
                return host;
            }).ToArray();

            int made = 1, section = 0;
            while (made < count)
            {
                var host = hosts[section % hosts.Length];
                var panel = new Panel { Bounds = new Rectangle(8, 8 + (section / 2) * 130, 1100, 120) };
                host.Controls.Add(panel);
                Items.Add((panel, "card p-2"));
                made++;
                int x = 8;
                for (int i = 0; i < 8 && made < count; i++, made++)
                {
                    var (control, classes) = Create(made);
                    control.Location = new Point(x, 8 + (i % 2) * 50);
                    x += i % 2 == 1 ? 130 : 0;
                    panel.Controls.Add(control);
                    Items.Add((control, classes));
                }
                if (made < count)
                {
                    var inner = new Panel { Bounds = new Rectangle(560, 8, 240, 100) };
                    panel.Controls.Add(inner);
                    Items.Add((inner, "bg-surface-100 rounded-md dark:bg-surface-800"));
                    made++;
                    for (int i = 0; i < 2 && made < count; i++, made++)
                    {
                        var (control, classes) = Create(made);
                        control.Location = new Point(8, 8 + i * 45);
                        inner.Controls.Add(control);
                        Items.Add((control, classes));
                    }
                }
                if (made < count && section % 12 == 11)
                {
                    Control big = section % 24 == 11
                        ? new DataGridView { Bounds = new Rectangle(820, 8, 260, 100), ColumnCount = 3, RowCount = 5 }
                        : new ListView { Bounds = new Rectangle(820, 8, 260, 100), View = View.Details, Columns = { "A", "B" }, Items = { "1", "2", "3" } };
                    panel.Controls.Add(big);
                    Items.Add((big, big is DataGridView ? "table" : "list"));
                    made++;
                }
                section++;
            }
            ResumeLayout(false);
        }

        private static (Control, string) Create(int index) => (index % 6) switch
        {
            0 => (new Label { AutoSize = true, Text = "Etiket " + index }, "muted"),
            1 => (new TextBox { Size = new Size(120, 24), Text = "Metin" }, "input"),
            2 => (new Button { Size = new Size(110, 32), Text = "Kaydet" }, index % 12 == 2 ? "btn-primary" : "btn-ghost"),
            3 => (new CheckBox { AutoSize = true, Text = "Seçenek" }, "toggle"),
            4 => (new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Size = new Size(120, 24), Items = { "a", "b" } }, "select"),
            _ => (new Label { AutoSize = true, Text = "Başlık " + index }, "title text-base"),
        };
    }

    private static int CountControls(Control root) => root.Controls.Cast<Control>().Sum(c => 1 + CountControls(c));

    [PerformanceTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Large_forms_style_and_switch_theme_in_time_proportional_to_their_size(bool autoStyle) => Sta.Run(() =>
    {
        var perControl = new Dictionary<int, (double Style, double Theme, double Bytes)>();
        foreach (var count in new[] { 1000, 2500, 5000 })
        {
            // Aynı form FormWind'siz: bellek farkı FormWind'in kontrol başına payıdır.
            long plainBytes;
            using (var plain = new LargeForm(count))
            {
                plain.Show();
                plain.Refresh();
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                long before = GC.GetTotalMemory(true);
                using var probe = new LargeForm(count);
                probe.Show();
                probe.Refresh();
                plainBytes = GC.GetTotalMemory(true) - before;
                probe.Close();
                plain.Close();
            }

            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long start = GC.GetTotalMemory(true);
            using var form = new LargeForm(count);
            int controls = CountControls(form) + 1;
            using var provider = new TailwindProvider();
            form.Show();
            var watch = Stopwatch.StartNew();
            if (autoStyle)
            {
                provider.ContainerControl = form;
                provider.AutoStyle = true;
            }
            else
            {
                provider.BeginInit();
                provider.SetClasses(form, "page");
                foreach (var (control, classes) in form.Items) provider.SetClasses(control, classes);
                provider.EndInit();
            }
            form.Refresh();
            var style = watch.Elapsed.TotalMilliseconds;
            long styledBytes = GC.GetTotalMemory(true) - start;

            var timings = new List<double>();
            for (int i = 0; i < 5; i++)
            {
                watch.Restart();
                provider.DarkMode = !provider.DarkMode;
                form.Refresh();
                timings.Add(watch.Elapsed.TotalMilliseconds);
            }
            timings.Sort();
            var theme = timings[timings.Count / 2];
            double bytes = (styledBytes - plainBytes) / (double)controls;
            perControl[count] = (style / controls, theme / controls, bytes);
            _output.WriteLine($"{(autoStyle ? "AutoStyle" : "class")} {count} ({controls} kontrol): stilleme {style:F0} ms ({style * 1000 / controls:F0} µs/kontrol), " +
                              $"tema geçişi medyan {theme:F0} ms ({theme * 1000 / controls:F0} µs/kontrol), FormWind belleği {bytes / 1024:F1} KB/kontrol");
            form.Close();
        }

        // Kontrol başına süre 5000'de 1000'dekinin 2 katını geçmez (doğrusal büyüme); mutlak sınırlar makineden bağımsız
        // tutulmak için geniş.
        Assert.True(perControl[5000].Style < perControl[1000].Style * 2, $"stilleme: {perControl[1000].Style * 1000:F0} → {perControl[5000].Style * 1000:F0} µs/kontrol");
        Assert.True(perControl[5000].Theme < perControl[1000].Theme * 2, $"tema: {perControl[1000].Theme * 1000:F0} → {perControl[5000].Theme * 1000:F0} µs/kontrol");
        Assert.True(perControl[5000].Bytes < 16 * 1024, $"bellek {perControl[5000].Bytes / 1024:F1} KB/kontrol");
    });

    // Sonradan eklenen kontroller: AutoStyle açıkken her ekleme stil uygular. Süre kontrol sayısıyla doğrusal büyümeli
    // (250 → 2000 kontrol: 8 kat); karesel büyüme 64 kat olurdu. Düz WinForms'un kendi maliyeti ayrıca ölçülür.
    [PerformanceFact]
    public void Controls_added_later_are_styled_in_linear_time() => Sta.Run(() =>
    {
        var counts = new[] { 250, 500, 1000, 2000 };
        var results = new Dictionary<(string Mode, int Count), double>();
        foreach (var mode in new[] { "WinForms tek tek", "WinForms Suspend", "AutoStyle tek tek", "AutoStyle Suspend" })
            foreach (var count in counts)
            {
                using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000), ShowInTaskbar = false, ClientSize = new Size(1000, 800) };
                var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
                form.Controls.Add(host);
                using var provider = new TailwindProvider();
                if (mode.StartsWith("AutoStyle", StringComparison.Ordinal))
                {
                    provider.ContainerControl = form;
                    provider.AutoStyle = true;
                }
                form.Show();
                var controls = Enumerable.Range(0, count).Select(i => (Control)((i % 4) switch
                {
                    0 => new TextBox { Bounds = new Rectangle((i % 8) * 120, (i / 8) * 30, 110, 24) },
                    1 => new Button { Bounds = new Rectangle((i % 8) * 120, (i / 8) * 30, 110, 24), Text = "b" },
                    2 => new CheckBox { Bounds = new Rectangle((i % 8) * 120, (i / 8) * 30, 110, 24), Text = "c" },
                    _ => new Label { Bounds = new Rectangle((i % 8) * 120, (i / 8) * 30, 110, 24), Text = "l" },
                })).ToArray();

                var watch = Stopwatch.StartNew();
                bool suspend = mode.EndsWith("Suspend", StringComparison.Ordinal);
                if (suspend) host.SuspendLayout();
                foreach (var control in controls) host.Controls.Add(control);
                if (suspend) host.ResumeLayout(true);
                form.Refresh();
                results[(mode, count)] = watch.Elapsed.TotalMilliseconds;
                if (mode.StartsWith("AutoStyle", StringComparison.Ordinal)) Assert.Equal("input", provider.GetAutomaticClasses(controls[0]));
                form.Close();
            }

        foreach (var mode in results.Keys.Select(k => k.Mode).Distinct())
        {
            var line = string.Join(", ", counts.Select(c => $"{c}: {results[(mode, c)]:F0} ms"));
            _output.WriteLine($"{mode}: {line}; 2000/250 = {results[(mode, 2000)] / results[(mode, 250)]:F1}x");
        }
        // WinForms'un kendisi tek tek eklemede her eklemede ebeveynin yerleşimini çalıştırır (karesele yakın); FormWind bu
        // büyümeyi WinForms'unkinden belirgin artırmamalı. Oranlar makineden bağımsızdır.
        double Growth(string mode) => results[(mode, 2000)] / results[(mode, 250)];
        Assert.True(Growth("AutoStyle tek tek") < Math.Max(16, Growth("WinForms tek tek") * 1.5), $"tek tek: AutoStyle {Growth("AutoStyle tek tek"):F1}x, WinForms {Growth("WinForms tek tek"):F1}x");
        Assert.True(Growth("AutoStyle Suspend") < Math.Max(16, Growth("WinForms Suspend") * 1.5), $"Suspend: AutoStyle {Growth("AutoStyle Suspend"):F1}x, WinForms {Growth("WinForms Suspend"):F1}x");
    });

    // Saniyede onlarca tema geçişi: renk geçişleri (transition) üst üste biner; geçişler bitince her kontrol hedef rengindedir,
    // handle sayısı artmaz, UI donmaz.
    [PerformanceFact]
    public void Rapid_theme_switching_ends_on_the_target_colors() => Sta.Run(() =>
    {
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000), ShowInTaskbar = false, ClientSize = new Size(1000, 800) };
        using var provider = new TailwindProvider();
        var controls = new List<Control>();
        for (int i = 0; i < 200; i++)
        {
            var (control, classes) = (i % 4) switch
            {
                0 => ((Control)new Button { Text = "b" }, "btn-primary transition duration-300"),
                1 => (new TextBox(), "input transition duration-300"),
                2 => (new Panel(), "card transition duration-300"),
                _ => (new Label { Text = "l" }, "text-surface-900 dark:text-surface-100 transition duration-300"),
            };
            control.Bounds = new Rectangle((i % 10) * 100, (i / 10) * 40, 90, 32);
            form.Controls.Add(control);
            controls.Add(control);
            provider.SetClasses(control, classes);
        }
        provider.SetClasses(form, "page transition duration-300");
        form.Show();
        form.Refresh();
        var before = GuiResources.Snapshot(collectGarbage: true);

        // 3 saniye boyunca ~16 ms'de bir geçiş (saniyede ~60): animasyonlar bitmeden yenisi başlar.
        var watch = Stopwatch.StartNew();
        double longestGap = 0, last = 0;
        int switches = 0;
        while (watch.ElapsedMilliseconds < 3000)
        {
            provider.DarkMode = !provider.DarkMode;
            switches++;
            var pumpUntil = watch.Elapsed.TotalMilliseconds + 16;
            while (watch.Elapsed.TotalMilliseconds < pumpUntil) Application.DoEvents();
            var now = watch.Elapsed.TotalMilliseconds;
            longestGap = Math.Max(longestGap, now - last);
            last = now;
        }
        int animating = TwAnimator.ActiveCount;
        // Son geçişin animasyonları biter (duration-300).
        var settle = Stopwatch.StartNew();
        while (TwAnimator.ActiveCount > 0 && settle.ElapsedMilliseconds < 3000) Application.DoEvents();
        form.Refresh();
        var after = GuiResources.Snapshot(collectGarbage: true);

        var wrong = controls.Concat(new[] { (Control)form })
            .Where(c => provider.GetCurrentColors(c).Back is Color back && c.BackColor.ToArgb() != back.ToArgb()
                        || provider.GetCurrentColors(c).Fore is Color fore && c.ForeColor.ToArgb() != fore.ToArgb())
            .Select(c => $"{c.GetType().Name} {c.BackColor}/{c.ForeColor}").ToList();
        _output.WriteLine($"{switches} geçiş 3 sn'de; en uzun aralık {longestGap:F0} ms; geçiş sırasında süren animasyon {animating}; " +
                          $"bitince {settle.ElapsedMilliseconds} ms'de duruldu; GDI {before.Gdi}→{after.Gdi}, USER {before.User}→{after.User}; yanlış renk {wrong.Count}");

        Assert.Equal(0, TwAnimator.ActiveCount);
        Assert.Empty(wrong);
        // Renk geçişi sürerken her karede bütün animasyonlu kontroller yeniden çizilir; maliyetin çoğu kartların (Region ve
        // kenar yumuşatmalı kenarlık, kart başına ~0,35 ms) çizimidir. Animasyon zamana göre ilerler: kare hızı düşer
        // (200 animasyonlu kontrolde ~10 kare/sn), UI donmaz ve geçiş zamanında biter.
        Assert.True(longestGap < 1000, $"en uzun aralık {longestGap:F0} ms");
        Assert.True(after.Gdi - before.Gdi <= Math.Max(5, before.Gdi / 20), $"GDI {before.Gdi} → {after.Gdi}");
        Assert.True(after.User - before.User <= Math.Max(5, before.User / 20), $"USER {before.User} → {after.User}");
        form.Close();
    });
}
