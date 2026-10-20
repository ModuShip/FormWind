using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Xunit;
using Xunit.Abstractions;

namespace FormWind.Tests.Performance;

/// <summary>
/// Uzun süreli kullanım: FormWind'in pencere alt sınıflamaları ve kancaları olan kontrollerle (koyu ListView/TreeView, RTF,
/// DropDownList, DateTimePicker, devre dışı ListView/ComboBox, ipuçları, PropertyGrid …) dolu bir form tekrar tekrar açılır,
/// tema değiştirilir, uygulama kendi temasını verir, handle'lar yeniden oluşturulur ve form kapatılır. GDI/USER sayısı ve
/// bellek artmamalı, kapanan formlar ve sağlayıcılar toplanmalı. Elle bağlanan sağlayıcı ve TwAutoTheme ayrı denenir.
/// </summary>
[Collection(PerformanceCollection.Name)]
public class SoakTests
{
    private readonly ITestOutputHelper _output;

    public SoakTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Forms_with_subclassed_controls_do_not_leak() => Sta.Run(() => Soak(8));

    // ~2,5 dk/yol: FORMWIND_PERF=1 ile. 300 turda (net9): GDI 62→62, USER 37→37, bellek ~4,3 MB, tur başına ~0,5 sn.
    [PerformanceFact]
    public void Three_hundred_rounds_do_not_leak_or_slow_down() => Sta.Run(() => Soak(300));

    private void Soak(int rounds)
    {
        foreach (bool auto in new[] { false, true })
        {
            if (auto) TwAutoTheme.Install(Path.Combine(Path.GetTempPath(), "formwind-no-theme.json"));
            try
            {
                for (int i = 0; i < 3; i++) Round(auto, null); // ısınma: font ve tema önbellekleri, WinForms iç önbellekleri
                var before = GuiResources.Snapshot(collectGarbage: true);
                long memoryBefore = GC.GetTotalMemory(forceFullCollection: true);
                var weak = new List<WeakReference>();
                var watch = Stopwatch.StartNew();
                var firstHalf = TimeSpan.Zero;
                for (int i = 0; i < rounds; i++)
                {
                    Round(auto, i < rounds - 1 ? weak : null);                                     // son turun formu yerelde kalabilir
                    if (i == rounds / 2 - 1) firstHalf = watch.Elapsed;
                }
                var secondHalf = watch.Elapsed - firstHalf;
                var after = GuiResources.Snapshot(collectGarbage: true);
                long memoryAfter = GC.GetTotalMemory(forceFullCollection: true);
                int alive = weak.Count(w => w.IsAlive);
                string name = auto ? "TwAutoTheme" : "TailwindProvider";
                _output.WriteLine($"{name}: {rounds} tur {watch.ElapsedMilliseconds} ms (ilk yarı {firstHalf.TotalMilliseconds:F0}, ikinci yarı {secondHalf.TotalMilliseconds:F0}); " +
                                  $"GDI {before.Gdi}→{after.Gdi}, USER {before.User}→{after.User}, bellek {memoryBefore / 1024}→{memoryAfter / 1024} KB, canlı {alive}/{weak.Count}");

                Assert.Equal(0, alive);
                AssertNoLeak($"{name} GDI", before.Gdi, after.Gdi);
                AssertNoLeak($"{name} USER", before.User, after.User);
                Assert.True(memoryAfter - memoryBefore < 2 * 1024 * 1024, $"{name}: bellek {memoryBefore / 1024} → {memoryAfter / 1024} KB");
                // Birikme (tur başına büyüyen bir liste) ikinci yarıyı belirgin yavaşlatırdı.
                if (rounds >= 100) Assert.True(secondHalf < firstHalf + firstHalf, $"{name}: ilk yarı {firstHalf}, ikinci yarı {secondHalf}");
            }
            finally
            {
                if (auto)
                {
                    TwAutoTheme.Uninstall();
                    TwAutoTheme.DarkMode = false;
                }
            }
        }
    }

    private static void Round(bool auto, List<WeakReference>? weak)
    {
        var provider = auto ? null : new TailwindProvider();
        var form = Build();
        if (provider != null)
        {
            provider.ContainerControl = form;
            provider.AutoStyle = true;
        }
        weak?.Add(new WeakReference(form));
        if (provider != null) weak?.Add(new WeakReference(provider));
        Exercise(form, dark =>
        {
            if (provider != null) provider.DarkMode = dark;
            else TwAutoTheme.DarkMode = dark;
        });
        provider?.Dispose();
    }

    private static Form Build()
    {
        var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, ClientSize = new Size(600, 500) };
        var list = new ListView { View = View.Details, Columns = { "A", "B" }, Bounds = new Rectangle(0, 0, 200, 100) };
        list.Groups.Add(new ListViewGroup("Grup"));
        list.Items.Add(new ListViewItem("x", list.Groups[0]));
        var button = new Button { Text = "Tools", Bounds = new Rectangle(300, 250, 80, 23), Image = new Bitmap(16, 16), TextImageRelation = TextImageRelation.ImageBeforeText };
        var tip = new ToolTip();
        tip.SetToolTip(button, "ipucu");
        form.Controls.AddRange(new Control[]
        {
            list,
            new TreeView { Bounds = new Rectangle(200, 0, 150, 100), Nodes = { "düğüm" } },
            new RichTextBox { Bounds = new Rectangle(350, 0, 150, 100) },
            new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Items = { "a", "b" }, Bounds = new Rectangle(0, 110, 120, 24) },
            new ComboBox { Bounds = new Rectangle(130, 110, 120, 24), Enabled = false },
            new DateTimePicker { Bounds = new Rectangle(260, 110, 150, 24) },
            new NumericUpDown { Bounds = new Rectangle(420, 110, 80, 24) },
            new ListView { Bounds = new Rectangle(0, 140, 150, 80), Enabled = false },
            new DataGridView { Bounds = new Rectangle(160, 140, 200, 100), ColumnCount = 2, RowCount = 3 },
            new TabControl { Bounds = new Rectangle(370, 140, 200, 100), TabPages = { "a", "b" } },
            new ProgressBar { Bounds = new Rectangle(0, 250, 200, 20), Value = 40 },
            new LinkLabel { Text = "bağlantı", Bounds = new Rectangle(210, 250, 80, 20) },
            button,
            new CheckBox { Text = "c", Bounds = new Rectangle(390, 250, 80, 20), Enabled = false },
            new TextBox { Bounds = new Rectangle(0, 280, 200, 24) },
            new PropertyGrid { Bounds = new Rectangle(0, 310, 250, 150), SelectedObject = new Size(1, 2) },
            new MenuStrip { Items = { "Dosya" } },
        });
        form.Disposed += (_, _) =>
        {
            tip.Dispose();
            button.Image?.Dispose();
        };
        return form;
    }

    private static void Exercise(Form form, Action<bool> setDark)
    {
        form.Show();
        var rich = form.Controls.OfType<RichTextBox>().First();
        using (var source = new RichTextBox { Text = "satır" }) rich.Rtf = source.Rtf;     // TwRichTextColor
        foreach (var dark in new[] { true, false, true })
        {
            setDark(dark);
            form.Refresh();
        }
        // Uygulamanın kendi teması (TwNativeTheme'in WM_THEMECHANGED'i), handle'ı yeniden oluşturan değişiklikler,
        // uygulamanın rengi ezip geri vermesi.
        SetWindowTheme(form.Controls.OfType<ListView>().First().Handle, "explorer", null);
        SetWindowTheme(form.Controls.OfType<TreeView>().First().Handle, "explorer", null);
        form.RightToLeft = RightToLeft.Yes;
        form.RightToLeft = RightToLeft.No;
        var text = form.Controls.OfType<TextBox>().First();
        text.Multiline = !text.Multiline;
        var combo = form.Controls.OfType<ComboBox>().First();
        combo.Enabled = false;
        combo.Enabled = true;
        var number = form.Controls.OfType<NumericUpDown>().First();
        number.BackColor = Color.Red;
        number.ResetBackColor();
        form.Refresh();
        Application.DoEvents();
        form.Close();
        form.Dispose();
    }

    private static void AssertNoLeak(string kind, int before, int after)
    {
        var allowed = Math.Max(5, (int)Math.Ceiling(before * 0.05));
        Assert.True(after - before <= allowed, $"{kind} nesne sayısı {before} → {after} (izin verilen artış {allowed}).");
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);
}
