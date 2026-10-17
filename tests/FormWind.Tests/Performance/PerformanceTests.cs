using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Xunit;
using Xunit.Abstractions;

namespace FormWind.Tests.Performance;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceCollection
{
    public const string Name = "Performance";
}

/// <summary>
/// Rapordaki performans kabul kriterleri ve tahminleri. Varsayılan çalıştırmada atlanır; <c>FORMWIND_PERF=1</c>
/// ortam değişkeniyle çalışır (bkz. README). GDI/USER sayaçları süreç geneli olduğundan diğer testlerle paralel çalışmaz.
/// </summary>
[Collection(PerformanceCollection.Name)]
[Trait("Category", "Performance")]
public class PerformanceTests
{
    private static readonly MethodInfo OnMouseEnter =
        typeof(Control).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo OnMouseLeave =
        typeof(Control).GetMethod("OnMouseLeave", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly ITestOutputHelper _output;

    public PerformanceTests(ITestOutputHelper output) => _output = output;

    // Kabul kriteri: "Koyu tema geçişi 200 kontrollü bir formda 100 ms'nin altında".
    [PerformanceFact]
    public void Dark_mode_toggle_on_200_controls_stays_under_100_ms() => Sta.Run(() =>
    {
        // İlk form JIT ve TypeDescriptor önbelleğini ısıtır (uygulamada yalnızca ilk açılan formda ödenir).
        TimeSpan cold;
        using (var first = new PerfForm(200)) cold = first.InitialStyling;

        using var form = new PerfForm(200);
        form.Show();
        form.Refresh();

        var timings = new List<double>();
        for (int i = 0; i <= 10; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            form.Provider.DarkMode = !form.Provider.DarkMode;
            form.Refresh(); // eşzamanlı yeniden çizim dahil
            stopwatch.Stop();
            if (i > 0) timings.Add(stopwatch.Elapsed.TotalMilliseconds); // ilk geçiş ısınma
        }

        timings.Sort();
        var median = timings[timings.Count / 2];
        var warm = form.InitialStyling.TotalMilliseconds;
        _output.WriteLine($"İlk stillendirme (EndInit, 200 kontrol): soğuk {cold.TotalMilliseconds:F1} ms, ılık {warm:F1} ms");
        _output.WriteLine($"Koyu tema geçişi + yeniden çizim: medyan {median:F1} ms, en kötü {timings.Last():F1} ms ({timings.Count} geçiş)");

        Assert.True(median < 100, $"Koyu tema geçişi medyanı {median:F1} ms (sınır 100 ms).");
        // Rapor tahmini 5–20 ms; ılık ölçüm için 3 kat, soğuk (JIT dahil) için 10 kat pay.
        Assert.True(warm < 60, $"İlk stillendirme (ılık) {warm:F1} ms (sınır 60 ms).");
        Assert.True(cold.TotalMilliseconds < 200, $"İlk stillendirme (soğuk) {cold.TotalMilliseconds:F1} ms (sınır 200 ms).");
    });

    // Kabul kriteri: "1.000 kez açılıp kapatılma sonrasında GDI nesne sayısı artışı %5'in altında".
    [PerformanceFact]
    public void Opening_and_closing_forms_1000_times_does_not_leak_handles() => Sta.Run(() =>
    {
        const int iterations = 1000;
        const int controlsPerForm = 30;

        OpenAndClose(controlsPerForm); // ısınma: font önbelleği, regex derleme, WinForms iç önbellekleri
        var before = GuiResources.Snapshot(collectGarbage: true);

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
            OpenAndClose(controlsPerForm);
        stopwatch.Stop();

        var withoutGc = GuiResources.Snapshot(collectGarbage: false);
        var after = GuiResources.Snapshot(collectGarbage: true);

        _output.WriteLine($"{iterations} form x {controlsPerForm} kontrol: {stopwatch.Elapsed.TotalSeconds:F1} sn ({stopwatch.Elapsed.TotalMilliseconds / iterations:F1} ms/form)");
        _output.WriteLine($"GDI:  önce {before.Gdi}, sonra {after.Gdi} (GC öncesi {withoutGc.Gdi})");
        _output.WriteLine($"USER: önce {before.User}, sonra {after.User} (GC öncesi {withoutGc.User})");

        AssertNoLeak("GDI", before.Gdi, after.Gdi);
        AssertNoLeak("USER", before.User, after.User);
    });

    // Kabul kriteri: "10.000 hover geçişi sonrasında GDI nesne sayısı sabit kalması".
    // Sınıflar her hover'da Region yeniden oluşturmayı (hover:rounded-lg) ve font değişimini (hover:font-normal) zorlar.
    [PerformanceFact]
    public void Ten_thousand_hover_cycles_do_not_leak_handles() => Sta.Run(() =>
    {
        const int cycles = 10_000;

        using var form = new PerfForm(60);
        form.Show();
        form.Refresh();
        var targets = form.Styled;

        foreach (var control in targets) HoverCycle(control); // ısınma
        form.Update();
        var before = GuiResources.Snapshot(collectGarbage: true);

        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < cycles; i++)
        {
            HoverCycle(targets[i % targets.Count]);
            if (i % 100 == 0) form.Update(); // birikmiş çizimleri de ölçüme kat
        }
        form.Update();
        stopwatch.Stop();

        var withoutGc = GuiResources.Snapshot(collectGarbage: false);
        var after = GuiResources.Snapshot(collectGarbage: true);
        var perTransition = stopwatch.Elapsed.TotalMilliseconds * 1000 / (cycles * 2);

        _output.WriteLine($"{cycles} hover döngüsü (giriş + çıkış): {stopwatch.Elapsed.TotalMilliseconds:F0} ms, geçiş başına {perTransition:F1} µs");
        _output.WriteLine($"GDI:  önce {before.Gdi}, sonra {after.Gdi} (GC öncesi {withoutGc.Gdi})");
        _output.WriteLine($"USER: önce {before.User}, sonra {after.User} (GC öncesi {withoutGc.User})");

        AssertNoLeak("GDI", before.Gdi, after.Gdi);
        AssertNoLeak("USER", before.User, after.User);
        // Rapor tahmini: geçiş başına < 0,1 ms; 10 kat pay bırakıldı.
        Assert.True(perTransition < 1000, $"Hover geçişi başına {perTransition:F1} µs (sınır 1000 µs).");
    });

    // Rapor tahmini: önbelleksiz parse 10–50 µs.
    [PerformanceFact]
    public void Uncached_parse_averages_under_200_microseconds()
    {
        const int count = 2000;
        var theme = new TwTheme();
        theme.Parse("bg-blue-600 px-4 hover:bg-blue-700"); // regex ısınması

        string Shade(int i) => TwPalette.Shades[i % TwPalette.Shades.Length];
        var inputs = Enumerable.Range(0, count)
            .Select(i => $"btn-primary bg-blue-{Shade(i)} px-{i % 12} py-2 rounded-md text-sm font-bold " +
                         $"hover:bg-red-{Shade(i + 3)} dark:bg-gray-{Shade(i + 5)} disabled:bg-gray-300 p-[{i}px]")
            .ToArray();

        var stopwatch = Stopwatch.StartNew();
        foreach (var input in inputs) theme.Parse(input);
        var uncached = stopwatch.Elapsed.TotalMilliseconds * 1000 / count;

        stopwatch.Restart();
        foreach (var input in inputs) theme.Parse(input);
        var cached = stopwatch.Elapsed.TotalMilliseconds * 1000 / count;

        _output.WriteLine($"Parse ({count} farklı dizi, ~17 token): önbelleksiz {uncached:F1} µs, önbellekten {cached:F2} µs");
        Assert.True(uncached < 200, $"Önbelleksiz parse ortalaması {uncached:F1} µs (sınır 200 µs).");
    }

    private static void OpenAndClose(int controls)
    {
        using var form = new PerfForm(controls);
        form.Show();
        form.Refresh();
        form.Close();
    }

    private static void HoverCycle(Control control)
    {
        OnMouseEnter.Invoke(control, new object[] { EventArgs.Empty });
        // Form ekran dışında olduğundan imleç kontrolün içinde sayılmaz ve hover kapanır.
        OnMouseLeave.Invoke(control, new object[] { EventArgs.Empty });
    }

    // Kriter %5. Küçük sayaçlarda (örn. 40 nesne) %5 = 2 nesne ölçüm gürültüsünün altında kalır; en az 5 nesne pay.
    private static void AssertNoLeak(string kind, int before, int after)
    {
        var allowed = Math.Max(5, (int)Math.Ceiling(before * 0.05));
        Assert.True(after - before <= allowed, $"{kind} nesne sayısı {before} → {after} (izin verilen artış {allowed}).");
    }
}
