#if NETFRAMEWORK
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using FormWind.Showcase;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests.Visual;

/// <summary>
/// Font çizimi ortama (Windows sürümü, ekran DPI'ı, font yumuşatma) bağlı olduğundan referanslar ortam başına ayrı klasörde
/// tutulur: <c>Baselines/&lt;ortam&gt;/</c>. Bu ortamın klasörü yoksa testler atlanır. Referanslar
/// <c>FORMWIND_UPDATE_BASELINES=1</c> ile üretilir; <c>FORMWIND_VISUAL_EXPORT=klasör</c> çizimleri ayrıca o klasöre yazar
/// (CI bunu yükler; indirilen klasör <c>Baselines</c> altına eklenerek o ortamın referansı olur).
/// </summary>
public sealed class VisualFactAttribute : FactAttribute
{
    public VisualFactAttribute()
    {
        if (VisualBaselines.UpdateMode || VisualBaselines.ExportFolder != null) return;
        if (!Directory.Exists(VisualBaselines.Folder()))
        {
            var available = Directory.Exists(VisualBaselines.Root())
                ? string.Join(", ", Directory.GetDirectories(VisualBaselines.Root()).Select(Path.GetFileName))
                : "";
            Skip = $"Bu ortam ({VisualBaselines.Slug}) için referans yok (mevcut: {(available.Length > 0 ? available : "yok")}). "
                   + "Üretmek için FORMWIND_UPDATE_BASELINES=1 ile çalıştırın.";
        }
    }
}

public static class VisualBaselines
{
    public const string UpdateVariable = "FORMWIND_UPDATE_BASELINES";

    public const string ExportVariable = "FORMWIND_VISUAL_EXPORT";

    public static bool UpdateMode => Environment.GetEnvironmentVariable(UpdateVariable) == "1";

    /// <summary>Çizimlerin ayrıca yazılacağı klasör (CI yapıtları için); ayarlı değilse <c>null</c>.</summary>
    public static string? ExportFolder
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(ExportVariable);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public static string Root([CallerFilePath] string source = "") =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(source)!)!, "Baselines");

    /// <summary>Bu ortamın referans klasörü.</summary>
    public static string Folder() => Path.Combine(Root(), Slug);

    /// <summary>Klasör adı olarak ortam: <c>win26200-96dpi-cleartype</c>.</summary>
    public static string Slug
    {
        get
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            var smoothing = SystemInformation.FontSmoothingType switch { 2 => "cleartype", 1 => "standard", _ => "none" };
            if (!SystemInformation.IsFontSmoothingEnabled) smoothing = "none";
            return $"win{Environment.OSVersion.Version.Build}-{g.DpiX:0}dpi-{smoothing}";
        }
    }

    public static string CurrentSignature
    {
        get
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            return $"Windows {Environment.OSVersion.Version.Build}; {g.DpiX:0} DPI; {SystemInformation.FontSmoothingType} font smoothing";
        }
    }

    public static string? ReadSignature()
    {
        var path = Path.Combine(Folder(), "environment.txt");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    public static void WriteSignature() => File.WriteAllText(Path.Combine(Folder(), "environment.txt"), CurrentSignature);
}

/// <summary>
/// Vitrin formlarını çizip referans görüntülerle karşılaştırır. Bir pikselin RGB farkı toplamı 48'i aşarsa farklı sayılır;
/// farklı piksel oranı %0,2'yi geçerse test başarısız olur ve fark görüntüsü test çıktısına yazılır.
/// </summary>
[Collection(UiCollection.Name)]
[Trait("Category", "Visual")]
public class VisualRegressionTests
{
    private const int ChannelTolerance = 48;
    private const double AllowedRatio = 0.002;

    [Fact]
    public void Compare_counts_only_pixels_beyond_tolerance()
    {
        using var a = new Bitmap(10, 10);
        using var b = new Bitmap(10, 10);
        using (var g = Graphics.FromImage(a)) g.Clear(Color.White);
        using (var g = Graphics.FromImage(b)) g.Clear(Color.White);
        b.SetPixel(1, 1, Color.FromArgb(250, 250, 250)); // tolerans içinde
        b.SetPixel(2, 2, Color.Black);                    // farklı

        var (ratio, diff) = Compare(a, b);
        diff.Dispose();
        Assert.Equal(0.01, ratio, 5);
    }

    [VisualFact]
    public void Showcase_light() => Check("showcase-light", () => new MainForm { DarkMode = false });

    [VisualFact]
    public void Showcase_dark() => Check("showcase-dark", () => new MainForm { DarkMode = true });

    [VisualFact]
    public void Controls_light() => Check("controls-light", () => new ControlsForm { DarkMode = false });

    [VisualFact]
    public void Controls_dark() => Check("controls-dark", () => new ControlsForm { DarkMode = true });

    // Hazır görünümler: AutoStyle'lı vitrin formu her paketle, açık ve koyu.
    [VisualFact] public void Preset_tailwind_light() => CheckPreset(TwPreset.Tailwind, dark: false);
    [VisualFact] public void Preset_tailwind_dark() => CheckPreset(TwPreset.Tailwind, dark: true);
    [VisualFact] public void Preset_fluent_light() => CheckPreset(TwPreset.Fluent, dark: false);
    [VisualFact] public void Preset_fluent_dark() => CheckPreset(TwPreset.Fluent, dark: true);
    [VisualFact] public void Preset_antd_light() => CheckPreset(TwPreset.Antd, dark: false);
    [VisualFact] public void Preset_antd_dark() => CheckPreset(TwPreset.Antd, dark: true);
    [VisualFact] public void Preset_material_light() => CheckPreset(TwPreset.Material, dark: false);
    [VisualFact] public void Preset_material_dark() => CheckPreset(TwPreset.Material, dark: true);

    private static void CheckPreset(TwPreset preset, bool dark) =>
        Check($"preset-{preset.ToString().ToLowerInvariant()}-{(dark ? "dark" : "light")}", () => new PresetsForm { DarkMode = dark }, new TwTheme(preset));

    [VisualFact]
    public void Calendar_light() => Check("calendar-light", () => CalendarForm(dark: false));

    [VisualFact]
    public void Calendar_dark() => Check("calendar-dark", () => CalendarForm(dark: true));

    /// <summary>Tarih seçicinin açılır takvimi, sabit ay ve seçimle (bugün halkası görünmesin diye geçmiş bir ay).</summary>
    private static Form CalendarForm(bool dark)
    {
        var form = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Font = new Font("Segoe UI", 9f) };
        var calendar = new TwCalendar { Culture = new System.Globalization.CultureInfo("tr-TR"), SelectedDate = new DateTime(2025, 3, 12), Location = Point.Empty };
        form.ClientSize = calendar.Size;
        form.Controls.Add(calendar);
        var provider = new TailwindProvider();
        form.Disposed += (_, _) => provider.Dispose();
        provider.SetClasses(calendar, "text-gray-900 dark:text-gray-100 menu:bg-white menu:border-gray-200 item:hover:bg-gray-100 selected:bg-blue-600 selected:text-white "
                                      + "dark:menu:bg-gray-800 dark:menu:border-gray-700 dark:item:hover:bg-gray-700");
        provider.DarkMode = dark;
        return form;
    }

    // PerMonitorV2: vitrin ayrı bir süreçte (DPI-aware) açılır ve başka DPI'lı bir ekrana taşınmış gibi ölçeklenir.
    [VisualFact]
    public void Controls_light_at_150_percent() => CheckDpi("controls-light", 144, "--controls");

    [VisualFact]
    public void Controls_dark_at_200_percent() => CheckDpi("controls-dark", 192, "--controls", "--dark");

    // .NET Framework ekran değişiminde fontları olaysız değiştirir; TwTextBox'ın iç kutusu silinmiş font handle'ını tutup
    // yazıyı sistem fontuyla küçük çiziyordu. Ana form AutoScaleMode.Font ile kurulu (Designer biçimi).
    [Fact]
    public void Text_boxes_keep_a_valid_native_font_after_moving_to_200_percent()
    {
        var folder = PrepareDpiShowcase();
        try
        {
            var output = Path.Combine(folder, "snapshot.png");
            int exit = RunShowcase(folder, $"--snapshot \"{output}\" --dpi 192 --check-native-fonts");
            Assert.True(exit == 0, "Native fontu geçersiz metin kutuları: " + (File.Exists(output + ".fonts.txt") ? File.ReadAllText(output + ".fonts.txt") : "?"));
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    // Vitrinin PerMonitorV2 yapılandırmayla çalışan bir kopyası.
    private static string PrepareDpiShowcase()
    {
        var folder = Path.Combine(Path.GetTempPath(), "formwind-dpi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var source = AppDomain.CurrentDomain.BaseDirectory;
        foreach (var file in Directory.GetFiles(source, "*.dll").Concat(new[] { Path.Combine(source, "FormWind.Showcase.exe"), Path.Combine(source, "tw.config.json") }))
            if (File.Exists(file)) File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));

        // Aynı yapılandırma, PerMonitorV2 açık (manifest Windows 10 uyumluluğunu zaten bildiriyor).
        var config = System.Xml.Linq.XDocument.Load(Path.Combine(source, "FormWind.Showcase.exe.config"));
        config.Root!.AddFirst(new System.Xml.Linq.XElement("System.Windows.Forms.ApplicationConfigurationSection",
            new System.Xml.Linq.XElement("add", new System.Xml.Linq.XAttribute("key", "DpiAwareness"), new System.Xml.Linq.XAttribute("value", "PerMonitorV2")),
            new System.Xml.Linq.XElement("add", new System.Xml.Linq.XAttribute("key", "EnableWindowsFormsHighDpiAutoResizing"), new System.Xml.Linq.XAttribute("value", "true"))));
        config.Save(Path.Combine(folder, "FormWind.Showcase.exe.config"));
        return folder;
    }

    private static int RunShowcase(string folder, string arguments)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(folder, "FormWind.Showcase.exe"), arguments)
            { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = folder })!;
        Assert.True(process.WaitForExit(60_000), "Vitrin süreci 60 sn içinde bitmedi.");
        return process.ExitCode;
    }

    private static void CheckDpi(string name, int dpi, params string[] options)
    {
        var folder = PrepareDpiShowcase();
        try
        {
            var output = Path.Combine(folder, "snapshot.png");
            Assert.Equal(0, RunShowcase(folder, $"--snapshot \"{output}\" --dpi {dpi} " + string.Join(" ", options)));

            // system=120;form=192 → referans adı sistem DPI'ını da içerir (sistem DPI'ı farklı makinede çizim farklıdır).
            var info = File.ReadAllText(output + ".txt").Split(';').Select(p => p.Split('=')).ToDictionary(p => p[0], p => p[1]);
            Assert.Equal(dpi.ToString(), info["form"]);
            var baselineName = $"{name}@{dpi}dpi-system{info["system"]}";
            using var actual = new Bitmap(output);
            CompareOrUpdate(baselineName, actual, optional: true);
        }
        finally
        {
            try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <param name="name">Referans adı.</param>
    /// <param name="create">Çizilecek form.</param>
    /// <param name="theme">Tema; verilmezse vitrinin kendi tema dosyası (marka renkleri).</param>
    private static void Check(string name, Func<Form> create, TwTheme? theme = null) => Sta.Run(() =>
    {
        var previousTheme = TwTheme.Current;
        try
        {
            TwTheme.Current = theme ?? TwTheme.LoadIfExists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tw.config.json"));
            using var form = create();
            using var actual = Snapshots.Render(form);
            CompareOrUpdate(name, actual);
        }
        finally
        {
            TwTheme.Current = previousTheme;
        }
    });

    /// <param name="name">Referans adı.</param>
    /// <param name="actual">Çizim.</param>
    /// <param name="optional">Referans yoksa geç (sistem DPI'ı farklı bir makinedeki DPI testleri).</param>
    private static void CompareOrUpdate(string name, Bitmap actual, bool optional = false)
    {
        var baselinePath = Path.Combine(VisualBaselines.Folder(), name + ".png");

        if (VisualBaselines.ExportFolder is string export)
        {
            var folder = Path.Combine(export, VisualBaselines.Slug);
            Directory.CreateDirectory(folder);
            actual.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
            File.WriteAllText(Path.Combine(folder, "environment.txt"), VisualBaselines.CurrentSignature);
            if (!VisualBaselines.UpdateMode && !File.Exists(baselinePath)) return;   // yalnızca dışa aktarım
        }

        if (VisualBaselines.UpdateMode)
        {
            Directory.CreateDirectory(VisualBaselines.Folder());
            actual.Save(baselinePath, ImageFormat.Png);
            VisualBaselines.WriteSignature();
            return;
        }

        if (!File.Exists(baselinePath))
        {
            Assert.True(optional, $"{name}: referans yok ({baselinePath}).");
            return;
        }

        using var expected = new Bitmap(baselinePath);
        Assert.True(expected.Size == actual.Size, $"{name}: boyut {actual.Size}, referans {expected.Size}");

        var (ratio, diff) = Compare(expected, actual);
        if (ratio > AllowedRatio)
        {
            var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VisualDiffs");
            Directory.CreateDirectory(output);
            actual.Save(Path.Combine(output, name + ".actual.png"), ImageFormat.Png);
            diff.Save(Path.Combine(output, name + ".diff.png"), ImageFormat.Png);
        }
        diff.Dispose();
        Assert.True(ratio <= AllowedRatio, $"{name}: piksellerin %{ratio * 100:0.00}'ı farklı (sınır %{AllowedRatio * 100:0.0}). Fark görüntüsü: VisualDiffs/{name}.diff.png");
    }

    /// <summary>Farklı piksel oranı ve farkları kırmızıyla işaretleyen görüntü.</summary>
    internal static (double Ratio, Bitmap Diff) Compare(Bitmap expected, Bitmap actual)
    {
        var diff = new Bitmap(actual.Width, actual.Height);
        int different = 0;
        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                var a = expected.GetPixel(x, y);
                var b = actual.GetPixel(x, y);
                int delta = Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
                if (delta > ChannelTolerance)
                {
                    different++;
                    diff.SetPixel(x, y, Color.Red);
                }
                else
                {
                    int gray = (b.R + b.G + b.B) / 3;
                    diff.SetPixel(x, y, Color.FromArgb(40, gray, gray, gray));
                }
            }
        }
        return (different / (double)(actual.Width * actual.Height), diff);
    }
}
#endif
