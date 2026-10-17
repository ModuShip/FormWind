#nullable enable
// Uygulamalara kod olarak da eklenebilir (.NET Framework'te startup hook yoktur); nullable bağlamı açık olmayan projelerde de derlenir.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace FormWind.Harness;

/// <summary>
/// Açılan her WinForms formuna kontrol tipine göre FormWind class'ları uygular ve sonuçları kaydeder. Formlar bir
/// WinForms zamanlayıcısıyla bulunur; böylece uygulama WinForms ya da WPF mesaj döngüsüyle çalışsa da olur.
/// <list type="bullet">
/// <item><description><c>FORMWIND_DARK=1</c> koyu temayla başlar; Ctrl+Alt+Shift+D çalışırken açar/kapatır.</description></item>
/// <item><description><c>FORMWIND_SKIP=DataGridView,ToolStrip</c> bu tipleri stillemez.</description></item>
/// <item><description><c>FORMWIND_SELFTEST=1</c> her yeni formun açık ve koyu temada ekran görüntüsünü alır (PrintWindow), kontrastını
/// class renklerinden ve görüntüden (<see cref="ScreenContrast"/>) denetler.</description></item>
/// <item><description><c>FORMWIND_EXIT_AFTER=60</c> verilen saniye sonra süreci sonlandırır (gözetimsiz çalıştırma).</description></item>
/// <item><description><c>FORMWIND_DUMP=1</c> hiç stil uygulamaz; her formun kontrollerinin renk, font ve padding değerlerini
/// <c>dump-&lt;form&gt;.txt</c> dosyasına yazar (örn. dönüştürme aracından önce ve sonra karşılaştırmak için).</description></item>
/// <item><description><c>FORMWIND_CLOSE=FormSettings</c> adı verilen formları işlendikten sonra kapatır (açılıştaki modal
/// pencerelerin arkasındaki formlara gözetimsiz ulaşmak için).</description></item>
/// <item><description><c>FORMWIND_WALK=Launch</c> ilk formdaki bu yazıya sahip butonlara sırayla basar; açılan her formu işler,
/// <c>FORMWIND_WALK_WAIT</c> ms (varsayılan 4000) sonra kapatır ve sonrakine geçer; bitince süreci sonlandırır.
/// <c>FORMWIND_WALK_SKIP=33,40</c> bu sıradaki (1'den başlayan) butonları atlar.</description></item>
/// <item><description><c>FORMWIND_SKIP=*</c> hiçbir kontrolü stillemez ama ölçer (karşılaştırma çalıştırması).</description></item>
/// <item><description><c>FORMWIND_OPEN=SettingsForm,AboutForm</c> bu form tiplerini (kısa ya da tam ad) yüklü derlemelerde bulur,
/// parametresiz constructor'la sırayla açar, işler ve <c>FORMWIND_WALK_WAIT</c> ms sonra kapatır; bitince süreci sonlandırır.
/// Menüden ya da tepsiden açılan formlara gözetimsiz ulaşmak için (test amaçlı yansıma).</description></item>
/// </list>
/// Log ve görüntüler: <c>%TEMP%\FormWind-Harness\&lt;süreç&gt;-&lt;zaman&gt;</c>.
/// </summary>
public static class FormWindHarness
{
    // Sıra önemli: ilk eşleşen kazanır, türetilmiş tipler tabanlarından önce gelir. DataGridView'a "table" bileşeninin
    // divide-y ve row:h-* içermeyen hâli verilir: kendi çizen grid'ler (örn. Git Extensions) CellBorderStyle.None ve
    // kendi satır yüksekliklerini varsayabilir.
    private static readonly (Type Type, string Classes)[] Map =
    {
        (typeof(DataGridView), "bg-white text-sm text-gray-700 border border-gray-200 header:bg-gray-50 header:text-gray-500 cell:px-3 "
            + "row:hover:bg-gray-50 selected:bg-blue-50 selected:text-blue-900 row-header:hidden "
            + "dark:bg-gray-800 dark:text-gray-200 dark:border-gray-700 dark:header:bg-gray-900 dark:header:text-gray-400 "
            + "dark:row:hover:bg-gray-700 dark:selected:bg-blue-900 dark:selected:text-blue-100"),
        (typeof(Button), "btn-ghost"),
        (typeof(TextBoxBase), "bg-white text-gray-900 dark:bg-gray-900 dark:text-gray-100"),
        (typeof(ComboBox), "bg-white text-gray-900 dark:bg-gray-800 dark:text-gray-100"),
        (typeof(UpDownBase), "bg-white text-gray-900 dark:bg-gray-900 dark:text-gray-100"),
        (typeof(CheckBox), "text-gray-800 dark:text-gray-200"),
        (typeof(RadioButton), "text-gray-800 dark:text-gray-200"),
        (typeof(Label), "text-gray-800 dark:text-gray-200"),
        (typeof(GroupBox), "text-gray-700 dark:text-gray-300"),
        (typeof(TreeView), "bg-white text-gray-800 dark:bg-gray-800 dark:text-gray-100"),
        (typeof(ListView), "bg-white text-gray-800 dark:bg-gray-800 dark:text-gray-100"),
        (typeof(ListBox), "bg-white text-gray-800 dark:bg-gray-800 dark:text-gray-100"),
        (typeof(ToolStrip), "bg-gray-50 text-gray-800 dark:bg-gray-800 dark:text-gray-100"),
        (typeof(TabPage), "bg-white dark:bg-gray-900 dark:text-gray-100"),
        (typeof(Form), "bg-gray-50 text-gray-900 dark:bg-gray-900 dark:text-gray-100"),
        (typeof(UserControl), "dark:bg-gray-900 dark:text-gray-100"),
        (typeof(SplitContainer), "dark:bg-gray-900"),
        (typeof(Panel), "dark:bg-gray-900"),
    };

    private static readonly Dictionary<Form, TailwindProvider> Providers = new();
    private static readonly HashSet<Control> Hooked = new();
    private static readonly HashSet<string> Reported = new();
    private static readonly object LogLock = new();
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static string _directory = "";
    private static string _logPath = "";
    private static bool _dark;
    private static bool _selfTest;
    private static bool _dump;
    private static readonly HashSet<Form> Dumped = new();
    private static HashSet<string> _close = new(StringComparer.OrdinalIgnoreCase);
    private static string? _walkText;
    private static int _walkWait = 4000;
    private static Form? _walkRoot;
    private static HashSet<int> _walkSkip = new();
    private static Form? _opened;
    private static HashSet<string> _skip = new(StringComparer.OrdinalIgnoreCase);
    private static int _paints;
    private static int _busySeconds;
    private static int _forms;
    private static bool _installed;

    /// <summary>UI iş parçacığında çağrılır (startup hook ya da uygulamanın kendisi).</summary>
    public static void Install()
    {
        if (_installed) return;
        _installed = true;

        _dark = Environment.GetEnvironmentVariable("FORMWIND_DARK") == "1";
        _selfTest = Environment.GetEnvironmentVariable("FORMWIND_SELFTEST") == "1";
        _dump = Environment.GetEnvironmentVariable("FORMWIND_DUMP") == "1";
        _walkText = Environment.GetEnvironmentVariable("FORMWIND_WALK") is { Length: > 0 } walk ? walk : null;
        if (int.TryParse(Environment.GetEnvironmentVariable("FORMWIND_WALK_WAIT"), out int wait) && wait > 0) _walkWait = wait;
        _walkSkip = new HashSet<int>((Environment.GetEnvironmentVariable("FORMWIND_WALK_SKIP") ?? "")
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => int.TryParse(x.Trim(), out int n) ? n : -1));
        _close = new HashSet<string>(
            (Environment.GetEnvironmentVariable("FORMWIND_CLOSE") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);
        // .NET Framework'te de derlenir (projeye kopyalanarak kullanılabilir): Split(char, TrimEntries) yok.
        _skip = new HashSet<string>(
            (Environment.GetEnvironmentVariable("FORMWIND_SKIP") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()),
            StringComparer.OrdinalIgnoreCase);

        string process = Process.GetCurrentProcess().ProcessName;
        _directory = Path.Combine(Path.GetTempPath(), "FormWind-Harness", $"{process}-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(_directory);
        _logPath = Path.Combine(_directory, "log.txt");
        Log($"STARTED {process} FormWind {typeof(TailwindProvider).Assembly.GetName().Version} dark={_dark} selftest={_selfTest} dump={_dump} skip=[{string.Join(",", _skip)}]");

        Application.AddMessageFilter(new DarkToggleFilter());
        if (int.TryParse(Environment.GetEnvironmentVariable("FORMWIND_EXIT_AFTER"), out int seconds) && seconds > 0)
        {
            var exit = new Timer { Interval = seconds * 1000 };
            exit.Tick += (_, _) =>
            {
                Log($"EXIT after {seconds} s: {_forms} forms styled");
                Environment.Exit(0);
            };
            exit.Start();
        }

        if (Environment.GetEnvironmentVariable("FORMWIND_OPEN") is { Length: > 0 } open) StartOpening(open);

        // Tek zamanlayıcı: yeni formları bulur, donmaları (geç gelen tik) ve saniyedeki çizim sayısını kaydeder.
        var timer = new Timer { Interval = 50 };
        long lastTick = 0, lastFlush = 0;
        timer.Tick += (_, _) =>
        {
            long now = Clock.ElapsedMilliseconds;
            if (lastTick > 0 && now - lastTick > 250) Log($"UI STALL {now - lastTick} ms");
            lastTick = now;
            Scan();
            if (now - lastFlush < 1000) return;
            // Tema değişimi gibi tek seferlik yeniden çizimler kısa sürer; 3 saniye üst üste süren yoğun çizim bir
            // döngüdür (çizim sırasında yeniden geçersiz kılma).
            if (_paints > 100)
            {
                _busySeconds++;
                Log(_busySeconds >= 3 ? $"PAINT STORM {_paints} in {now - lastFlush} ms ({_busySeconds} s in a row)" : $"PAINTS {_paints} in {now - lastFlush} ms");
            }
            else _busySeconds = 0;
            _paints = 0;
            lastFlush = now;
        };
        timer.Start();
        Scan();
    }

    private static void Scan()
    {
        foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
        {
            if (form.IsDisposed || !form.Visible) continue;
            if (_dump)
            {
                if (Dumped.Add(form)) ScheduleDump(form);
                continue;
            }
            if (!Providers.ContainsKey(form)) Attach(form);
        }
    }

    // Form yüklemesi (uygulamanın Load'da atadıkları) bittikten sonra bir kez yazılır.
    private static void ScheduleDump(Form form)
    {
        var timer = new Timer { Interval = 2000 };
        timer.Tick += (_, _) =>
        {
            timer.Dispose();
            if (form.IsDisposed) return;
            var lines = new List<string>();
            Dump(form, form.Name.Length > 0 ? form.Name : form.GetType().Name, lines);
            string path = Path.Combine(_directory, $"dump-{form.GetType().FullName}.txt");
            File.WriteAllLines(path, lines);
            Log($"DUMP {form.GetType().FullName}: {lines.Count} controls -> {path}");
            CloseIfRequested(form);
            if (form == _opened) CloseLater(form, 100);
        };
        timer.Start();
    }

    private static void Dump(Control control, string path, List<string> lines)
    {
        var font = control.Font;
        lines.Add($"{path} [{control.GetType().Name}] back=#{control.BackColor.ToArgb():X8} fore=#{control.ForeColor.ToArgb():X8} " +
                  $"font={font.Name} {font.SizeInPoints:0.##}pt {font.Style} padding={control.Padding.Left},{control.Padding.Top},{control.Padding.Right},{control.Padding.Bottom}");
        int index = 0;
        foreach (Control child in control.Controls)
            Dump(child, $"{path}/{(child.Name.Length > 0 ? child.Name : "#" + index++)}", lines);
    }

    private static void Attach(Form form)
    {
        var provider = new TailwindProvider { DarkMode = _dark };
        provider.UnknownClasses += (_, e) =>
        {
            foreach (string cls in e.Unknown)
                if (Reported.Add($"unknown {e.Control.GetType().Name}:{cls}")) Log($"UNKNOWN class '{cls}' on {Describe(e.Control)}");
        };
        Providers.Add(form, provider);
        form.Disposed += (_, _) =>
        {
            Providers.Remove(form);
            provider.Dispose();
        };
        _forms++;

        var sw = Stopwatch.StartNew();
        var counts = new Dictionary<string, int>();
        var unstyled = new List<string>();
        form.SuspendLayout();
        try
        {
            Walk(provider, form, counts, unstyled);
        }
        finally
        {
            form.ResumeLayout(true);
        }
        Log($"FORM {form.GetType().FullName} styled {counts.Values.Sum()} controls in {sw.ElapsedMilliseconds} ms [{Summary(counts)}]");
        if (unstyled.Count > 0)
            Log($"  unstyled: {string.Join(", ", unstyled.GroupBy(t => t).OrderByDescending(g => g.Count()).Select(g => $"{g.Key}={g.Count()}"))}");

        AuditContrast(form, provider, _dark ? "dark" : "light");
        // Renklerini uygulamanın çizdiği kontroller: kontrast denetimi bunları ölçemez, koyu temada açık kalabilirler.
        foreach (var drawn in TwContrast.FindOwnerDrawn(form))
            if (Reported.Add($"ownerdraw {Describe(drawn.Control)}")) Log($"OWNERDRAW {Describe(drawn.Control)}: {drawn.Reason}");
        if (form is ThreadExceptionDialog) Log($"APP EXCEPTION dialog opened: {form.Text}");
        if (_walkText != null)
        {
            if (_walkRoot is null)
            {
                _walkRoot = form;
                StartWalk(form);
            }
            else if (form != _walkRoot)
            {
                CloseLater(form, _walkWait);
            }
        }
        else if (form == _opened)
        {
            CloseLater(form, _walkWait);
        }
        if (_selfTest) SelfTest(form, provider);
        else if (_close.Contains(form.GetType().Name))
        {
            var timer = new Timer { Interval = 2000 };
            timer.Tick += (_, _) =>
            {
                timer.Dispose();
                CloseIfRequested(form);
            };
            timer.Start();
        }
    }

    // Formlar sırayla açılır: bir sonraki, öncekinin kapanmasını bekler.
    private static void StartOpening(string list)
    {
        var names = new Queue<string>(list.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(n => n.Trim()));
        Log($"OPEN queue: {string.Join(", ", names)}");
        var timer = new Timer { Interval = 1000 };
        int idle = 0;
        timer.Tick += (_, _) =>
        {
            if (_opened != null && !_opened.IsDisposed) return;
            if (++idle < 3) return; // açılışın ve önceki formun kapanışının oturması için
            idle = 0;
            if (names.Count == 0)
            {
                timer.Dispose();
                Log($"OPEN done: {_forms} forms styled");
                Environment.Exit(0);
                return;
            }
            string name = names.Dequeue();
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { return e.Types.Where(t => t != null)!; } })
                .FirstOrDefault(t => t != null && typeof(Form).IsAssignableFrom(t) && !t.IsAbstract && (t.FullName == name || t.Name == name));
            if (type is null)
            {
                Log($"OPEN {name}: type not found");
                return;
            }
            try
            {
                _opened = (Form)Activator.CreateInstance(type, nonPublic: true)!;
                Log($"OPEN {type.FullName}");
                _opened.Show();
            }
            catch (Exception ex)
            {
                _opened = null;
                Log($"OPEN {type.FullName} failed: {(ex.InnerException ?? ex).GetType().Name}: {(ex.InnerException ?? ex).Message}");
            }
        };
        timer.Start();
    }

    private static void CloseLater(Form form, int milliseconds)
    {
        var timer = new Timer { Interval = milliseconds };
        timer.Tick += (_, _) =>
        {
            timer.Dispose();
            if (form.IsDisposed) return;
            Log($"WALK closing {form.GetType().FullName}");
            form.Close();
            // Bazı formlar kapanmayı reddeder (örn. seçim yapılmadan kapanmayan bir dialog); dolaşma takılmasın diye
            // form zorla dispose edilir.
            if (!form.IsDisposed && form.Visible)
            {
                Log($"WALK close cancelled by {form.GetType().FullName}; disposing");
                form.Dispose();
            }
        };
        timer.Start();
    }

    // Butonlara sırayla basılır. Buton modal bir form açarsa PerformClick form kapanana kadar döner (iç içe mesaj döngüsü);
    // zamanlayıcılar o sırada da çalışır, bu yüzden bir sonraki tıklama önceki bitene kadar bekler.
    private static void StartWalk(Form root)
    {
        var start = new Timer { Interval = 2000 };
        start.Tick += (_, _) =>
        {
            start.Dispose();
            var buttons = Descendants(root).Where(c => c is IButtonControl && c.Text == _walkText).ToList();
            Log($"WALK {buttons.Count} '{_walkText}' buttons");
            int index = 0;
            bool busy = false;
            var step = new Timer { Interval = 500 };
            step.Tick += (_, _) =>
            {
                if (busy) return;
                if (index >= buttons.Count)
                {
                    step.Dispose();
                    Log($"WALK done: {buttons.Count} buttons, {_forms} forms styled");
                    Environment.Exit(0);
                    return;
                }
                var button = buttons[index++];
                if (_walkSkip.Contains(index))
                {
                    Log($"WALK {index}/{buttons.Count} skipped");
                    return;
                }
                busy = true;
                Log($"WALK {index}/{buttons.Count} click {Describe(button)}");
                try
                {
                    ((IButtonControl)button).PerformClick();
                }
                catch (Exception ex)
                {
                    Log($"EXCEPTION walking button {index}: {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    busy = false;
                }
            };
            step.Start();
        };
        start.Start();
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    private static void CloseIfRequested(Form form)
    {
        if (form.IsDisposed || !_close.Contains(form.GetType().Name)) return;
        Log($"CLOSE {form.GetType().FullName}");
        form.Close();
    }

    private static string Summary(Dictionary<string, int> counts) =>
        string.Join(", ", counts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}"));

    // Yalnızca içerik taşımak için yapılmış konteynerlere inilir. Bileşik kontrollerin (KryptonTextBox'ın içindeki
    // TextBox, PropertyGrid'in yardım paneli, NumericUpDown'ın düzenleme kutusu) iç parçaları kontrolün kendisine aittir
    // ve kendi renkleriyle çizilir; onları stillemek uyumsuz renk çiftleri üretir.
    private static bool IsContentContainer(Control control) =>
        control is not PropertyGrid && control is not UpDownBase
        && (control is Form || control is Panel || control is GroupBox || control is TabControl || control is SplitContainer
            || control is UserControl || control is ToolStripContainer || control is ToolStripPanel);

    private static void Walk(TailwindProvider provider, Control control, Dictionary<string, int> counts, List<string>? unstyled)
    {
        Style(provider, control, counts, unstyled);
        if (!IsContentContainer(control))
        {
            if (Hooked.Add(control))
            {
                control.Paint += (_, _) => _paints++;
                control.Disposed += (_, _) => Hooked.Remove(control);
            }
            return;
        }
        if (Hooked.Add(control))
        {
            control.Paint += (_, _) => _paints++;
            control.ControlAdded += (_, e) =>
            {
                if (e.Control is null || Hooked.Contains(e.Control)) return;
                var added = new Dictionary<string, int>();
                Walk(provider, e.Control, added, null);
                if (added.Count > 0) Log($"ADDED {e.Control.GetType().FullName} into {control.FindForm()?.GetType().Name} styled {added.Values.Sum()} [{Summary(added)}]");
            };
            control.Disposed += (_, _) => Hooked.Remove(control);
        }

        foreach (Control child in control.Controls.Cast<Control>().ToList())
            Walk(provider, child, counts, unstyled);
    }

    private static void Style(TailwindProvider provider, Control control, Dictionary<string, int> counts, List<string>? unstyled)
    {
        if (provider.GetClasses(control).Length > 0) return;
        var match = Map.FirstOrDefault(m => m.Type.IsInstanceOfType(control));
        if (match.Type is null)
        {
            unstyled?.Add(control.GetType().Name);
            return;
        }
        if (_skip.Contains(match.Type.Name) || _skip.Contains("*")) return;

        try
        {
            provider.SetClasses(control, match.Classes);
            counts[match.Type.Name] = counts.TryGetValue(match.Type.Name, out int n) ? n + 1 : 1;
        }
        catch (Exception ex)
        {
            Log($"EXCEPTION styling {Describe(control)} with '{match.Classes}': {ex}");
        }
    }

    private static void AuditContrast(Form form, TailwindProvider provider, string mode)
    {
        try
        {
            var issues = TwContrast.Audit(form, provider, includeDisabled: true);
            int fresh = 0;
            foreach (var issue in issues)
            {
                if (!Reported.Add($"contrast {mode} {form.GetType().Name} {Describe(issue.Control)} {issue.Part}")) continue;
                fresh++;
                Log($"CONTRAST {mode} {form.GetType().Name}: {issue}");
            }
            if (issues.Count > 0) Log($"  contrast {mode}: {issues.Count} issue(s), {fresh} new");
        }
        catch (Exception ex)
        {
            Log($"EXCEPTION in contrast audit: {ex.Message}");
        }
    }

    private static void ToggleDark()
    {
        _dark = !_dark;
        var sw = Stopwatch.StartNew();
        foreach (var provider in Providers.Values.ToList())
        {
            try
            {
                provider.DarkMode = _dark;
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION toggling dark mode: {ex}");
            }
        }
        Log($"DARK toggled to {_dark} across {Providers.Count} forms in {sw.ElapsedMilliseconds} ms");
        foreach (var pair in Providers.ToList()) AuditContrast(pair.Key, pair.Value, _dark ? "dark" : "light");
    }

    // Formu o anki ve karşı temada çizer, karşı temanın kontrastını denetler ve geri döner. Görüntüler formun kendi
    // çizimidir (DrawToBitmap): ekrandaki başka pencereler görüntüye girmez.
    private static void SelfTest(Form form, TailwindProvider provider)
    {
        int index = _forms;
        var timer = new Timer { Interval = 1500 };
        int phase = 0;
        timer.Tick += (_, _) =>
        {
            if (form.IsDisposed)
            {
                timer.Dispose();
                return;
            }
            string mode = provider.DarkMode ? "dark" : "light";
            using (var image = Snapshot(form, $"{index:00}-{form.GetType().Name}-{mode}"))
                if (image != null) AuditScreen(form, image, mode);
            if (phase++ == 1)
            {
                timer.Dispose();
                provider.DarkMode = _dark;
                return;
            }
            var sw = Stopwatch.StartNew();
            provider.DarkMode = !provider.DarkMode;
            Log($"SELFTEST {form.GetType().Name} switched to {(provider.DarkMode ? "dark" : "light")} in {sw.ElapsedMilliseconds} ms");
            AuditContrast(form, provider, provider.DarkMode ? "dark" : "light");
        };
        timer.Start();
    }

    // Görüntü PrintWindow ile alınır: başlık çubuğu, RichTextBox ve MDI çocukları ekrandaki gibi çıkar; pencere başka bir
    // pencerenin altında kalsa da çalışır. Olmazsa DrawToBitmap'e düşülür (başlık çubuğu ve RichTextBox yanlış çizilir).
    private static Bitmap? Snapshot(Form form, string name)
    {
        try
        {
            var bitmap = ScreenContrast.Capture(form) ?? DrawToBitmap(form);
            string path = Path.Combine(_directory, name + ".png");
            bitmap.Save(path);
            Log($"SNAPSHOT {path}");
            return bitmap;
        }
        catch (Exception ex)
        {
            Log($"SNAPSHOT failed for {name}: {ex.Message}");
            return null;
        }
    }

    private static Bitmap DrawToBitmap(Form form)
    {
        var bitmap = new Bitmap(Math.Max(1, form.Width), Math.Max(1, form.Height));
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        return bitmap;
    }

    // Class renklerinden yapılan denetimin göremediklerini (uygulamanın ya da Windows'un kendi çizdiği renkler) yakalar.
    private static void AuditScreen(Form form, Bitmap image, string mode)
    {
        try
        {
            foreach (var issue in ScreenContrast.Audit(form, image, ScreenContrast.WindowOrigin(form)))
                if (Reported.Add($"screen {mode} {Describe(issue.Control)}"))
                    Log($"SCREEN {mode} {Describe(issue.Control)}: {issue.Ratio:0.0}:1 (yazı #{issue.Foreground.ToArgb() & 0xFFFFFF:X6}, zemin #{issue.Background.ToArgb() & 0xFFFFFF:X6})");
        }
        catch (Exception ex)
        {
            Log($"EXCEPTION in screen audit: {ex.Message}");
        }
    }

    private static string Describe(Control control)
    {
        var path = new StringBuilder();
        for (Control? c = control; c is not null; c = c.Parent)
            path.Insert(0, $"/{(string.IsNullOrEmpty(c.Name) ? "?" : c.Name)}");
        return $"{control.GetType().FullName} {path}";
    }

    private static void Log(string message)
    {
        lock (LogLock)
        {
            File.AppendAllText(_logPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
    }

    private sealed class DarkToggleFilter : IMessageFilter
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSysKeyDown = 0x0104;

        // Ctrl+Alt birçok klavye düzeninde AltGr'dir; WM_SYSKEYDOWN olarak da gelebilir.
        public bool PreFilterMessage(ref Message m)
        {
            if ((m.Msg != WmKeyDown && m.Msg != WmSysKeyDown) || (Keys)(int)m.WParam != Keys.D
                || Control.ModifierKeys != (Keys.Control | Keys.Alt | Keys.Shift)) return false;
            Log("DARK hotkey pressed");
            ToggleDark();
            return true;
        }
    }
}

/// <summary>
/// Ekran görüntüsünden kontrast: metin taşıyan her kontrolün alanında en sık renk zemin, zemine en zıt piksel yazı sayılır.
/// Class renkleriyle yapılan denetimin (<see cref="TwContrast"/>) bilemediği durumları yakalar: Windows'un kendi çizdiği
/// devre dışı yazı, kendi paletiyle çizen kontroller (Krypton), uygulamanın elle çizdiği yerler. Kenarlık pikselleri dışarıda
/// bırakılır; yazı yumuşatması nedeniyle ölçüm yaklaşıktır, bu yüzden eşik büyük metninki (3:1).
/// </summary>
internal static class ScreenContrast
{
    public const double Minimum = 3.0;
    private const int Inset = 3;

    public sealed class Issue
    {
        public Issue(Control control, Color foreground, Color background, double ratio)
        {
            Control = control;
            Foreground = foreground;
            Background = background;
            Ratio = ratio;
        }

        public Control Control { get; }
        public Color Foreground { get; }
        public Color Background { get; }
        public double Ratio { get; }
    }

    /// <summary>Pencerenin ekrandaki sol üst köşesi (çerçeve dahil); görüntüdeki koordinatların başlangıcı.</summary>
    public static Point WindowOrigin(Control window) =>
        GetWindowRect(window.Handle, out var rect) ? new Point(rect.Left, rect.Top) : window.PointToScreen(Point.Empty);

    /// <summary>
    /// Üst düzey pencerenin PrintWindow ile alınmış görüntüsü; alınamazsa <c>null</c>. Alt pencerelerde (MDI çocukları)
    /// PrintWindow üstteki kardeş pencereleri de yakaladığından <c>null</c> döner; onlar DrawToBitmap ile çizilmeli.
    /// </summary>
    public static Bitmap? Capture(Control window)
    {
        if (window is Form { TopLevel: false }) return null;
        if (!window.IsHandleCreated || !GetWindowRect(window.Handle, out var rect)) return null;
        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0) return null;
        var bitmap = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bitmap))
        {
            var hdc = g.GetHdc();
            bool ok;
            try
            {
                ok = PrintWindow(window.Handle, hdc, 2 /* PW_RENDERFULLCONTENT */);
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
            // Pencerenin DWM yüzeyi henüz hazır değilken (örn. yeni gösterilmiş) PrintWindow başarılı dönüp istemci alanını
            // düz siyah verebilir; zemini siyah olmayan pencerede böyle bir görüntü başarısız sayılır.
            if (ok && !(window.BackColor.ToArgb() != Color.Black.ToArgb() && ClientIsBlack(window, bitmap, new Point(rect.Left, rect.Top)))) return bitmap;
        }
        bitmap.Dispose();
        return null;
    }

    private static bool ClientIsBlack(Control window, Bitmap bitmap, Point origin)
    {
        var client = window.RectangleToScreen(window.ClientRectangle);
        client.Offset(-origin.X, -origin.Y);
        client.Intersect(new Rectangle(Point.Empty, bitmap.Size));
        if (client.Width < 2 || client.Height < 2) return false;
        int black = Color.Black.ToArgb();
        for (int i = 0; i < 16; i++)
            for (int j = 0; j < 16; j++)
                if (bitmap.GetPixel(client.Left + (client.Width - 1) * i / 15, client.Top + (client.Height - 1) * j / 15).ToArgb() != black) return false;
        return true;
    }

    public static List<Issue> Audit(Control root, Bitmap image, Point origin, double minimum = Minimum)
    {
        var issues = new List<Issue>();
        Visit(root, root, image, origin, minimum, issues);
        return issues;
    }

    private static void Visit(Control root, Control control, Bitmap image, Point origin, double minimum, List<Issue> issues)
    {
        if (!control.Visible || !control.IsHandleCreated) return;
        // İç içe formlar (MDI çocukları) kendi görüntüleriyle denetlenir; üst formun görüntüsünde birbirlerinin altında kalabilirler.
        if (control is Form && control != root) return;
        if (CarriesText(control) && Measure(control, image, origin) is var (fore, back) && fore is Color f && back is Color b)
        {
            double ratio = TwContrast.Ratio(f, b);
            if (ratio < minimum) issues.Add(new Issue(control, f, b, ratio));
        }
        foreach (Control child in control.Controls) Visit(root, child, image, origin, minimum, issues);
    }

    private static bool CarriesText(Control control) =>
        !string.IsNullOrWhiteSpace(control.Text)
        && (control is Label || control is ButtonBase || control is TextBoxBase || control is ComboBox);

    /// <summary>Kontrolün görüntüdeki yazı ve zemin rengi; ölçülemiyorsa <c>(null, null)</c>.</summary>
    public static (Color? Fore, Color? Back) Measure(Control control, Bitmap image, Point origin)
    {
        // Görünen alan: kontrolün ekrandaki dikdörtgeni, üst kontrollerin istemci alanlarıyla kesilir.
        var visible = control.RectangleToScreen(control.ClientRectangle);
        for (var parent = control.Parent; parent != null; parent = parent.Parent)
            visible.Intersect(parent.RectangleToScreen(parent.ClientRectangle));
        visible.Offset(-origin.X, -origin.Y);
        visible.Intersect(new Rectangle(Point.Empty, image.Size));
        visible.Inflate(-Inset, -Inset);
        if (visible.Width < 4 || visible.Height < 4) return (null, null);

        var counts = new Dictionary<int, int>();
        for (int y = visible.Top; y < visible.Bottom; y++)
            for (int x = visible.Left; x < visible.Right; x++)
            {
                int argb = image.GetPixel(x, y).ToArgb();
                counts[argb] = counts.TryGetValue(argb, out int n) ? n + 1 : 1;
            }
        var back = Color.FromArgb(counts.OrderByDescending(c => c.Value).First().Key);

        Color fore = back;
        double best = 1;
        foreach (var argb in counts.Keys)
        {
            var color = Color.FromArgb(argb);
            double ratio = TwContrast.Ratio(color, back);
            if (ratio > best)
            {
                best = ratio;
                fore = color;
            }
        }
        return (fore, back);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
}
