using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;

namespace FormWind.Benchmarks;

/// <summary>
/// Kullanım:
/// <list type="bullet">
/// <item><c>FormWind.Benchmarks [--quick] [--sizes 50,200,500] [--only a,b] [--out klasör]</c>: tüm senaryoları ayrı süreçlerde ölçer, rapor yazar.</item>
/// <item><c>warm &lt;senaryo&gt; &lt;n&gt;</c>, <c>leak &lt;senaryo&gt;</c>, <c>cold &lt;senaryo&gt; &lt;n&gt;</c>: tek ölçüm (alt süreç), JSON yazar.</item>
/// </list>
/// </summary>
internal static class Program
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length > 0 && args[0] is "warm" or "leak" or "cold")
        {
            if (args.Contains("--other-dpi"))
                Scenario.OffScreen = Scenario.FindOffScreen(otherDpi: true)
                    ?? throw new InvalidOperationException("Formun gösterilirken DPI değişimi aldığı bir monitör yok.");
            var scenario = Scenario.Find(args[1]);
            var result = args[0] switch
            {
                "warm" => Measure.Warm(scenario, int.Parse(args[2]), args.Contains("--quick")),
                "leak" => Measure.Leak(scenario, args.Contains("--quick")),
                _ => Measure.Cold(scenario, int.Parse(args[2])),
            };
            Console.WriteLine("RESULT " + result.ToJsonString());
            return 0;
        }

        // report <results.json>: kayıtlı ölçümlerden Markdown raporu (örn. ayrı koşuların birleştirilmesi).
        if (args.Length == 2 && args[0] == "report")
        {
            Console.WriteLine(Report.Markdown(JsonNode.Parse(File.ReadAllText(args[1]))!.AsObject()));
            return 0;
        }

        return Driver.Run(args);
    }

    private static class Driver
    {
        public static int Run(string[] args)
        {
            bool quick = args.Contains("--quick");
            // --other-dpi: formlar, gösterilirken DPI değişimi aldıkları monitörde açılır (ikinci ekranda açılan form).
            string otherDpi = args.Contains("--other-dpi") ? "--other-dpi" : "";
            var sizes = Option(args, "--sizes")?.Split(',').Select(int.Parse).ToArray() ?? new[] { 50, 200, 500 };
            var only = Option(args, "--only")?.Split(',');
            var output = Option(args, "--out") ?? Path.Combine(FindRepoRoot(), "artifacts", "benchmarks");
            int coldRuns = quick ? 3 : 7;
            var scenarios = Scenario.All.Where(s => only == null || only.Contains(s.Key)).ToArray();

            var report = new JsonObject
            {
                ["machine"] = Machine(),
                ["date"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                ["quick"] = quick,
                ["otherDpi"] = otherDpi.Length > 0,
            };
            var results = new JsonArray();
            report["scenarios"] = results;

            foreach (var scenario in scenarios)
            {
                var entry = new JsonObject { ["key"] = scenario.Key, ["title"] = scenario.Title };
                var warm = new JsonArray();
                foreach (var n in sizes)
                {
                    Console.Error.WriteLine($"[{scenario.Key}] ılık ölçüm, {n} kontrol…");
                    warm.Add(Child("warm", scenario.Key, n.ToString(), quick ? "--quick" : "", otherDpi));
                }
                entry["warm"] = warm;

                Console.Error.WriteLine($"[{scenario.Key}] aç/kapat sızıntı ölçümü…");
                entry["leak"] = Child("leak", scenario.Key, quick ? "--quick" : "", otherDpi);

                var cold = new JsonArray();
                for (int i = 0; i < coldRuns; i++)
                {
                    Console.Error.WriteLine($"[{scenario.Key}] soğuk başlangıç {i + 1}/{coldRuns}…");
                    cold.Add(Child("cold", scenario.Key, "200", otherDpi));
                }
                entry["cold"] = cold;
                results.Add(entry);
            }

            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "results.json"), report.ToJsonString(Json));
            var markdown = Report.Markdown(report);
            File.WriteAllText(Path.Combine(output, "results.md"), markdown);
            Console.WriteLine(markdown);
            Console.Error.WriteLine("Yazıldı: " + output);
            return 0;
        }

        private static JsonNode Child(params string[] args)
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in args.Where(a => a.Length > 0)) info.ArgumentList.Add(arg);
            using var process = Process.Start(info)!;
            var stderr = process.StandardError.ReadToEndAsync();
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            var line = stdout.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith("RESULT "));
            if (process.ExitCode != 0 || line == null)
                throw new InvalidOperationException($"Alt süreç başarısız ({string.Join(' ', args)}):\n{stdout}\n{stderr.Result}");
            return JsonNode.Parse(line.Substring("RESULT ".Length))!;
        }

        private static string? Option(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private static string FindRepoRoot()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "FormWind.sln"))) return dir.FullName;
            return Directory.GetCurrentDirectory();
        }

        private static JsonObject Machine()
        {
            string cpu = "?";
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                cpu = (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? "?";
            }
            catch { }
            using var screen = new Form();
            return new JsonObject
            {
                ["os"] = RuntimeInformation.OSDescription,
                ["runtime"] = RuntimeInformation.FrameworkDescription,
                ["cpu"] = cpu,
                ["cores"] = Environment.ProcessorCount,
                ["dpi"] = screen.DeviceDpi,
            };
        }
    }
}

internal static class Measure
{
    private const int WM_MOUSEMOVE = 0x0200, WM_MOUSELEAVE = 0x02A3;

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);

    private static (int Gdi, int User) Handles(bool collect = true)
    {
        if (collect)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        var handle = Process.GetCurrentProcess().Handle;
        return ((int)GetGuiResources(handle, 0), (int)GetGuiResources(handle, 1));
    }

    /// <summary>Formu kurar, gösterir, çizer ve kuyruktaki mesajları işler; aşama süreleri (ms) <paramref name="phases"/>'a eklenir.</summary>
    private static Form Open(Scenario scenario, int n, List<double[]>? phases = null)
    {
        var watch = Stopwatch.StartNew();
        var form = scenario.Create(n);
        var created = watch.Elapsed.TotalMilliseconds;
        form.Show();
        var shown = watch.Elapsed.TotalMilliseconds;
        form.Refresh();
        var painted = watch.Elapsed.TotalMilliseconds;
        Application.DoEvents();
        phases?.Add(new[] { created, shown - created, painted - shown, watch.Elapsed.TotalMilliseconds - painted });
        return form;
    }

    /// <summary>Kurma + gösterme, yeniden çizim, tema değişimi, hover ve kapatma; ısınmadan sonra.</summary>
    public static JsonObject Warm(Scenario scenario, int n, bool quick)
    {
        int buildRuns = quick ? 3 : 9, paintRuns = quick ? 10 : 30, toggleRuns = quick ? 6 : 20, hoverCycles = quick ? 500 : 3000;

        for (int i = 0; i < 2; i++) Open(scenario, n).Dispose(); // ısınma: JIT, önbellekler

        var build = new List<double>();
        var allocated = new List<double>();
        var gdi = new List<double>();
        var user = new List<double>();
        var dispose = new List<double>();
        var phases = new List<double[]>();
        long managedBefore = 0, managedAfter = 0;
        Form form = null!;
        for (int i = 0; i < buildRuns; i++)
        {
            var before = Handles();
            managedBefore = GC.GetTotalMemory(true);
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            form = Open(scenario, n, phases);
            watch.Stop();
            allocated.Add(GC.GetAllocatedBytesForCurrentThread() - bytes);
            build.Add(watch.Elapsed.TotalMilliseconds);
            var after = Handles();
            managedAfter = GC.GetTotalMemory(true);
            gdi.Add(after.Gdi - before.Gdi);
            user.Add(after.User - before.User);
            if (i < buildRuns - 1)
            {
                watch.Restart();
                form.Dispose();
                Application.DoEvents();
                dispose.Add(watch.Elapsed.TotalMilliseconds);
            }
        }

        // Tam yeniden çizim: Invalidate(true) + Update.
        for (int i = 0; i < 5; i++) form.Refresh();
        var paint = Times(paintRuns, () => form.Refresh());

        // Tema değişimi + eşzamanlı yeniden çizim.
        bool dark = false;
        for (int i = 0; i < 2; i++) Toggle();
        var toggleBefore = Handles();
        long toggleBytes = GC.GetAllocatedBytesForCurrentThread();
        var toggle = Times(toggleRuns, Toggle);
        long toggleAllocated = (GC.GetAllocatedBytesForCurrentThread() - toggleBytes) / toggleRuns;
        if (dark) Toggle();
        var toggleAfter = Handles();

        // Hover: gerçek fare mesajları (WM_MOUSEMOVE → MouseEnter, WM_MOUSELEAVE → MouseLeave); her 50 geçişte bekleyen çizimler.
        var targets = scenario.HoverTargets(form);
        foreach (var target in targets) Hover(target);
        form.Update();
        Application.DoEvents();
        var hoverBefore = Handles();
        long hoverBytes = GC.GetAllocatedBytesForCurrentThread();
        var hoverWatch = Stopwatch.StartNew();
        for (int i = 0; i < hoverCycles; i++)
        {
            Hover(targets[i % targets.Count]);
            if (i % 50 == 49) form.Update();
        }
        form.Update();
        hoverWatch.Stop();
        long hoverAllocated = (GC.GetAllocatedBytesForCurrentThread() - hoverBytes) / (hoverCycles * 2);
        Application.DoEvents();
        var hoverAfter = Handles();

        var closeWatch = Stopwatch.StartNew();
        form.Dispose();
        Application.DoEvents();
        dispose.Add(closeWatch.Elapsed.TotalMilliseconds);

        return new JsonObject
        {
            ["n"] = n,
            ["buildMs"] = Stats(build),
            // Kurma (ctor + class'lar/EndInit), Show (handle'lar), ilk çizim, kuyruktaki mesajlar (BeginInvoke ile ertelenen işler).
            ["phasesMs"] = new JsonArray(Enumerable.Range(0, 4).Select(i => (JsonNode)Median(phases.Select(p => p[i]).ToList())).ToArray()),
            ["allocatedKb"] = Median(allocated) / 1024,
            ["retainedKb"] = (managedAfter - managedBefore) / 1024.0,
            ["gdiPerForm"] = Median(gdi),
            ["userPerForm"] = Median(user),
            ["paintMs"] = Stats(paint),
            ["toggleMs"] = Stats(toggle),
            ["toggleAllocatedKb"] = toggleAllocated / 1024.0,
            ["toggleGdiDelta"] = toggleAfter.Gdi - toggleBefore.Gdi,
            ["hoverUs"] = hoverWatch.Elapsed.TotalMilliseconds * 1000 / (hoverCycles * 2),
            ["hoverAllocatedBytes"] = hoverAllocated,
            ["hoverGdiDelta"] = hoverAfter.Gdi - hoverBefore.Gdi,
            ["disposeMs"] = Median(dispose),
        };

        void Toggle()
        {
            dark = !dark;
            scenario.SetDark(form, dark);
            form.Refresh();
            Application.DoEvents();
        }
    }

    private static void Hover(Control control)
    {
        SendMessage(control.Handle, WM_MOUSEMOVE, IntPtr.Zero, (IntPtr)((10 << 16) | 10));
        SendMessage(control.Handle, WM_MOUSELEAVE, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>30 kontrollü formu tekrar tekrar açıp kapatır; GDI/USER ve yönetilen bellek artışı.</summary>
    public static JsonObject Leak(Scenario scenario, bool quick)
    {
        int iterations = quick ? 100 : 500;
        for (int i = 0; i < 3; i++) Open(scenario, 30).Dispose();
        var before = Handles();
        long managedBefore = GC.GetTotalMemory(true);
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var form = Open(scenario, 30);
            form.Close();
            form.Dispose();
        }
        watch.Stop();
        Application.DoEvents();
        var raw = Handles(collect: false);
        var after = Handles();
        long managedAfter = GC.GetTotalMemory(true);
        return new JsonObject
        {
            ["iterations"] = iterations,
            ["msPerForm"] = watch.Elapsed.TotalMilliseconds / iterations,
            ["gdiBefore"] = before.Gdi,
            ["gdiAfter"] = after.Gdi,
            ["gdiBeforeGc"] = raw.Gdi,
            ["userBefore"] = before.User,
            ["userAfter"] = after.User,
            ["managedGrowthKb"] = (managedAfter - managedBefore) / 1024.0,
        };
    }

    /// <summary>Süreç başlangıcından ilk formun çizilmesine kadar (JIT, derleme yükleme, ilk stillendirme dahil).</summary>
    public static JsonObject Cold(Scenario scenario, int n)
    {
        using var process = Process.GetCurrentProcess();
        var start = process.StartTime;
        var mainEntered = (DateTime.Now - start).TotalMilliseconds;
        var watch = Stopwatch.StartNew();
        var form = Open(scenario, n);
        var firstForm = watch.Elapsed.TotalMilliseconds;
        process.Refresh();
        var handles = Handles(collect: false);
        var result = new JsonObject
        {
            ["toMainMs"] = mainEntered,
            ["firstFormMs"] = firstForm,
            ["totalMs"] = (DateTime.Now - start).TotalMilliseconds,
            ["workingSetMb"] = process.WorkingSet64 / 1048576.0,
            ["privateMb"] = process.PrivateMemorySize64 / 1048576.0,
            ["gdi"] = handles.Gdi,
            ["user"] = handles.User,
            ["assemblies"] = AppDomain.CurrentDomain.GetAssemblies().Length,
        };
        form.Dispose();
        return result;
    }

    private static List<double> Times(int count, Action action)
    {
        var list = new List<double>(count);
        for (int i = 0; i < count; i++)
        {
            var watch = Stopwatch.StartNew();
            action();
            list.Add(watch.Elapsed.TotalMilliseconds);
        }
        return list;
    }

    private static double Median(List<double> values)
    {
        if (values.Count == 0) return double.NaN;
        var sorted = values.OrderBy(v => v).ToArray();
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
    }

    private static JsonObject Stats(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        return new JsonObject
        {
            ["median"] = Median(values),
            ["min"] = sorted[0],
            ["p90"] = sorted[(int)Math.Ceiling(sorted.Length * 0.9) - 1],
            ["count"] = sorted.Length,
        };
    }
}

internal static class Report
{
    public static string Markdown(JsonObject report)
    {
        var scenarios = report["scenarios"]!.AsArray().Select(s => s!.AsObject()).ToArray();
        var sizes = scenarios[0]["warm"]!.AsArray().Select(w => (int)w!["n"]!).ToArray();
        var machine = report["machine"]!;
        var md = new StringBuilder();
        md.AppendLine($"Ölçüm: {report["date"]} · {machine["cpu"]} ({machine["cores"]} çekirdek) · {machine["os"]} · {machine["runtime"]} · {machine["dpi"]} DPI{((bool)report["quick"]! ? " · hızlı mod" : "")}{(report["otherDpi"]?.GetValue<bool>() == true ? " · DPI değişimli monitörde açılış" : "")}");
        md.AppendLine();

        string Title(JsonObject s) => (string)s["title"]!;
        JsonNode Warm(JsonObject s, int n) => s["warm"]!.AsArray().First(w => (int)w!["n"]! == n)!;
        string F(double v, int digits = 1) => double.IsNaN(v) ? "—" : v.ToString("F" + digits, CultureInfo.InvariantCulture);

        void Table(string heading, string unit, Func<JsonNode, string> cell)
        {
            md.AppendLine($"### {heading}");
            md.AppendLine();
            md.AppendLine("| Senaryo | " + string.Join(" | ", sizes.Select(n => $"{n} kontrol")) + " |");
            md.AppendLine("| --- | " + string.Join(" | ", sizes.Select(_ => "---:")) + " |");
            foreach (var s in scenarios)
                md.AppendLine($"| {Title(s)} | " + string.Join(" | ", sizes.Select(n => cell(Warm(s, n)))) + " |");
            md.AppendLine();
            if (unit.Length > 0) md.AppendLine(unit).AppendLine();
        }

        string Med(JsonNode w, string key) => $"{F((double)w[key]!["median"]!)} <sub>p90 {F((double)w[key]!["p90"]!)}</sub>";

        Table("Formu kurma + stillendirme + ilk çizim (ms, medyan)", "Isınmış süreçte; `new Form` → `Show` → `Refresh`.", w => Med(w, "buildMs"));
        Table("Kurma aşamaları (ms, medyan): kurma · Show · ilk çizim · ertelenen mesajlar", "", w =>
            string.Join(" · ", w["phasesMs"]!.AsArray().Select(v => F((double)v!, 0))));
        Table("Tam yeniden çizim (ms, medyan)", "`Form.Refresh()`: tüm kontroller yeniden çizilir.", w => Med(w, "paintMs"));
        Table("Açık ↔ koyu tema geçişi + yeniden çizim (ms, medyan)", "", w => Med(w, "toggleMs"));
        Table("Hover geçişi (µs / giriş veya çıkış)", "`WM_MOUSEMOVE` + `WM_MOUSELEAVE` mesajları; bekleyen çizimler dahil.", w => F((double)w["hoverUs"]!));
        Table("Form başına ayrılan bellek (KB)", "Kurma sırasında UI iş parçacığında ayrılan toplam yönetilen bellek.", w => F((double)w["allocatedKb"]!, 0));
        Table("Form açıkken tutulan yönetilen bellek (KB)", "", w => F((double)w["retainedKb"]!, 0));
        Table("Form başına GDI / USER nesnesi", "", w => $"{F((double)w["gdiPerForm"]!, 0)} / {F((double)w["userPerForm"]!, 0)}");
        Table("Tema geçişi başına ayrılan bellek (KB)", "", w => F((double)w["toggleAllocatedKb"]!, 0));

        md.AppendLine("### Soğuk başlangıç (200 kontrol, medyan)");
        md.AppendLine();
        md.AppendLine("| Senaryo | Main'e kadar (ms) | İlk form (ms) | Toplam (ms) | Çalışma kümesi (MB) | Özel bellek (MB) | GDI | USER | Derleme |");
        md.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (var s in scenarios)
        {
            var cold = s["cold"]!.AsArray();
            double M(string key) => cold.Select(c => (double)c![key]!).OrderBy(v => v).ElementAt(cold.Count / 2);
            md.AppendLine($"| {Title(s)} | {F(M("toMainMs"), 0)} | {F(M("firstFormMs"), 0)} | {F(M("totalMs"), 0)} | {F(M("workingSetMb"))} | {F(M("privateMb"))} | {F(M("gdi"), 0)} | {F(M("user"), 0)} | {F(M("assemblies"), 0)} |");
        }
        md.AppendLine();

        md.AppendLine("### Aç / kapat (30 kontrollü form)");
        md.AppendLine();
        md.AppendLine("| Senaryo | Tekrar | ms / form | GDI önce → sonra | USER önce → sonra | Yönetilen bellek artışı (KB) |");
        md.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (var s in scenarios)
        {
            var l = s["leak"]!;
            md.AppendLine($"| {Title(s)} | {l["iterations"]} | {F((double)l["msPerForm"]!)} | {l["gdiBefore"]} → {l["gdiAfter"]} | {l["userBefore"]} → {l["userAfter"]} | {F((double)l["managedGrowthKb"]!, 0)} |");
        }
        md.AppendLine();
        return md.ToString();
    }
}
