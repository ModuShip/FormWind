using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Rastgele senaryolarla değişmezler: rastgele bir kontrole rastgele işlem dizisi (class atama, koyu tema, devre dışı,
/// hover, ebeveyn rengi, uygulamanın renk ataması, handle yeniden oluşturma) uygulanır; her adımdan sonra renkler
/// çözülen stile (ya da uygulamanın atadığı renge) eşit olmalı, sonda class'lar kaldırılınca özgün değerler dönmeli.
/// Senaryolar seed'lidir: hata mesajındaki seed <c>FORMWIND_INVARIANT_SEED</c> ile tek başına tekrar çalıştırılır;
/// senaryo sayısı <c>FORMWIND_INVARIANT_RUNS</c> ile artırılır.
/// </summary>
public class InvariantTests
{
    private static readonly string[] ClassPool =
    {
        "bg-white", "bg-gray-50", "bg-blue-600", "bg-[#123456]", "bg-transparent",
        "text-white", "text-gray-700", "text-red-600", "text-sm", "text-lg", "font-bold", "font-semibold", "italic", "underline",
        "p-2", "px-4", "py-1", "pt-3", "m-2", "rounded", "rounded-md", "rounded-full", "border", "border-2", "border-gray-300",
        "cursor-pointer", "text-center", "shadow-sm",
        "hover:bg-gray-100", "hover:text-blue-700", "active:bg-gray-200", "focus:border-blue-500",
        "disabled:bg-gray-300", "disabled:text-gray-500",
        "dark:bg-gray-800", "dark:text-gray-100", "dark:hover:bg-gray-700", "dark:border-gray-600",
        "transition", "btn-primary", "btn-ghost", "card", "input", "hidden",
    };

    private static readonly Func<Control>[] ControlFactories =
    {
        () => new Button { Text = "Kaydet", Size = new Size(90, 30) },
        () => new Label { Text = "Ad soyad", AutoSize = true },
        () => new TextBox { Text = "Ayşe" },
        () => new CheckBox { Text = "Aktif", AutoSize = true },
        () => new RadioButton { Text = "Seçenek" },
        () => new Panel { Size = new Size(120, 60) },
        () => new GroupBox { Text = "Grup", Size = new Size(120, 60) },
        () => new ComboBox { Items = { "Bir", "İki" } },
        () => new ListBox { Items = { "Bir", "İki" } },
        () => new TreeView { Nodes = { "Kök" } },
        () => new ToolStrip { Items = { "Öğe" } },
        () => new DataGridView { ColumnCount = 2, RowCount = 3 },
        () => new NumericUpDown(),
        () => new TwButton { Text = "Tw", Size = new Size(90, 30) },
        () => new TwPanel { Size = new Size(120, 60) },
        () => new TwTextBox { Size = new Size(120, 30) },
    };

    private static readonly MethodInfo OnMouseEnter = typeof(Control).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo OnMouseLeave = typeof(Control).GetMethod("OnMouseLeave", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo RecreateHandle = typeof(Control).GetMethod("RecreateHandle", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static IEnumerable<int> Seeds()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable("FORMWIND_INVARIANT_SEED"), out int only)) return new[] { only };
        int runs = int.TryParse(Environment.GetEnvironmentVariable("FORMWIND_INVARIANT_RUNS"), out int n) ? n : 300;
        return Enumerable.Range(1, runs);
    }

    [Fact]
    public void Random_operation_sequences_keep_colors_consistent_and_restore_originals() => Sta.Run(() =>
    {
        var failures = new List<string>();
        foreach (int seed in Seeds())
        {
            var failure = RunScenario(seed);
            if (failure != null) failures.Add(failure);
            if (failures.Count >= 5) break;
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine + Environment.NewLine, failures));
    });

    private static string? RunScenario(int seed)
    {
        var random = new Random(seed);
        var log = new StringBuilder();
        var theme = new TwTheme();
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var parent = new Panel { Dock = DockStyle.Fill };
        if (random.Next(2) == 0) parent.BackColor = RandomColor(random);
        if (random.Next(2) == 0) parent.ForeColor = RandomColor(random);
        form.Controls.Add(parent);
        var control = ControlFactories[random.Next(ControlFactories.Length)]();
        parent.Controls.Add(control);
        log.Append($"seed={seed} control={control.GetType().Name}");

        // Designer'ın atadığı değerler.
        if (random.Next(3) == 0) { control.BackColor = RandomColor(random); log.Append(" designerBack"); }
        if (random.Next(3) == 0) { control.ForeColor = RandomColor(random); log.Append(" designerFore"); }
        if (random.Next(4) == 0) { control.Padding = new Padding(random.Next(0, 6)); log.Append(" designerPadding"); }
        if (random.Next(4) == 0) { control.Font = new Font("Segoe UI", 11f, FontStyle.Italic); log.Append(" designerFont"); }
        if (random.Next(2) == 0) { form.Show(); log.Append(" shown"); }

        var before = Snapshot.Take(control);
        using var provider = new TailwindProvider();
        string classes = "";
        bool hovered = false;
        bool bound = false;
        Color? appBack = null, appFore = null;
        bool appTouchedBack = false, appTouchedFore = false;

        try
        {
            int steps = random.Next(3, 12);
            for (int step = 0; step < steps; step++)
            {
                switch (random.Next(9))
                {
                    case 0:
                    case 1:
                    {
                        string next = string.Join(" ", Enumerable.Range(0, random.Next(1, 6)).Select(_ => ClassPool[random.Next(ClassPool.Length)]));
                        if (next != classes) { appBack = null; appFore = null; }
                        classes = next;
                        log.Append($"\n  SetClasses(\"{classes}\")");
                        provider.SetClasses(control, classes);
                        // Binding ilk class'larla oluşur; önceki sentetik MouseEnter'ı görmemiştir, imleç de kontrolün
                        // üzerinde değildir (form ekran dışında).
                        // Kontrolün kendi fare durumu da sıfırlanır (DataGridView eşleşmeyen ikinci MouseEnter'ı yok sayar).
                        if (!bound)
                        {
                            bound = true;
                            if (hovered) OnMouseLeave.Invoke(control, new object[] { EventArgs.Empty });
                            hovered = false;
                        }
                        break;
                    }
                    case 2:
                        log.Append($"\n  DarkMode={!provider.DarkMode}");
                        provider.DarkMode = !provider.DarkMode;
                        break;
                    case 3:
                        log.Append($"\n  Enabled={!control.Enabled}");
                        control.Enabled = !control.Enabled;
                        break;
                    case 4:
                        log.Append($"\n  Hover={!hovered}");
                        (hovered ? OnMouseLeave : OnMouseEnter).Invoke(control, new object[] { EventArgs.Empty });
                        hovered = !hovered;
                        break;
                    case 5:
                        log.Append("\n  parent colors changed");
                        parent.BackColor = RandomColor(random);
                        parent.ForeColor = RandomColor(random);
                        break;
                    case 6:
                        if (random.Next(2) == 0) { log.Append("\n  app sets BackColor"); control.BackColor = RandomColor(random); appBack = control.BackColor; appTouchedBack = true; }
                        else { log.Append("\n  app sets ForeColor"); control.ForeColor = RandomColor(random); appFore = control.ForeColor; appTouchedFore = true; }
                        break;
                    case 7:
                        if (control.IsHandleCreated) { log.Append("\n  RecreateHandle"); RecreateHandle.Invoke(control, null); }
                        break;
                    case 8:
                        log.Append("\n  Refresh");
                        provider.Refresh(control);
                        break;
                }

                TwAnimator.Step(DateTime.UtcNow.AddSeconds(10));
                var state = CurrentState(provider, control, hovered);
                // Devre dışı native metin kutusunda yalnızca disabled: renkleri uygulanır (Windows zemin rengine uymaz).
                var style = control is TextBoxBase && !control.Enabled
                    ? theme.Parse(classes).ResolveExact(TwState.Disabled, state)
                    : theme.Parse(classes).Resolve(state);
                var mismatch = CheckColor("BackColor", control.BackColor, appBack ?? style.BackColor)
                               ?? CheckColor("ForeColor", control.ForeColor, appFore ?? style.ForeColor);
                if (mismatch != null) return $"{log}\n  -> state={state}: {mismatch}";
            }

            // Class'lar kaldırılınca özgün değerler dönmeli (uygulamanın kendi atadıkları kalır).
            log.Append("\n  SetClasses(\"\") + DarkMode=False + Enabled=True");
            provider.SetClasses(control, "");
            provider.DarkMode = false;
            if (!control.Enabled) control.Enabled = true;
            TwAnimator.Step(DateTime.UtcNow.AddSeconds(10));
            var after = Snapshot.Take(control);
            var diff = before.Diff(after, parent, ignoreBack: appTouchedBack, ignoreFore: appTouchedFore);
            return diff == null ? null : $"{log}\n  -> not restored: {diff}";
        }
        catch (Exception ex)
        {
            return $"{log}\n  -> exception: {ex}";
        }
    }

    private static TwState CurrentState(TailwindProvider provider, Control control, bool hovered)
    {
        var state = provider.DarkMode ? TwState.Dark : TwState.None;
        if (control is CheckBox { Checked: true } || control is RadioButton { Checked: true }) state |= TwState.Checked;
        if (!control.Enabled) return state | TwState.Disabled;
        if (control.ContainsFocus) state |= TwState.Focus;
        if (hovered) state |= TwState.Hover;
        return state;
    }

    // Saydam renk desteklemeyen kontroller atamayı reddeder; o durumda karşılaştırma yapılmaz.
    private static string? CheckColor(string property, Color actual, Color? expected)
    {
        if (expected is not Color color || color.A < 255) return null;
        return actual.ToArgb() == color.ToArgb() ? null : $"{property} is {Hex(actual)}, expected {Hex(color)}";
    }

    private static Color RandomColor(Random random) => Color.FromArgb(255, random.Next(256), random.Next(256), random.Next(256));

    private static string Hex(Color color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private sealed class Snapshot
    {
        public int Back;
        public bool BackExplicit;
        public int Fore;
        public bool ForeExplicit;
        public string Font = "";
        public bool FontExplicit;
        public Padding Padding;
        public Padding Margin;
        public Cursor? Cursor;
        public bool Visible;
        public bool RegionNull;
        public FlatStyle? FlatStyle;
        public string? Renderer;

        public static Snapshot Take(Control control) => new Snapshot
        {
            Back = control.BackColor.ToArgb(),
            BackExplicit = TwOriginal.IsExplicit(control, nameof(Control.BackColor)),
            Fore = control.ForeColor.ToArgb(),
            ForeExplicit = TwOriginal.IsExplicit(control, nameof(Control.ForeColor)),
            Font = $"{control.Font.Name} {control.Font.SizeInPoints} {control.Font.Style}",
            FontExplicit = TwOriginal.IsExplicit(control, nameof(Control.Font)),
            Padding = control.Padding,
            Margin = control.Margin,
            Cursor = control.Cursor,
            Visible = control.Visible,
            RegionNull = control.Region is null,
            FlatStyle = (control as ButtonBase)?.FlatStyle,
            Renderer = (control as ToolStrip)?.Renderer.GetType().Name,
        };

        // Kalıtılan değer ebeveyn değiştiyse değişebilir; o yüzden kalıtılmışlık ve açık değerler karşılaştırılır.
        public string? Diff(Snapshot after, Control parent, bool ignoreBack, bool ignoreFore)
        {
            var diffs = new List<string>();
            if (!ignoreBack)
            {
                if (BackExplicit != after.BackExplicit) diffs.Add($"BackColor explicit {BackExplicit} -> {after.BackExplicit}");
                else if (BackExplicit && Back != after.Back) diffs.Add($"BackColor {Back:X8} -> {after.Back:X8}");
            }
            if (!ignoreFore)
            {
                if (ForeExplicit != after.ForeExplicit) diffs.Add($"ForeColor explicit {ForeExplicit} -> {after.ForeExplicit}");
                else if (ForeExplicit && Fore != after.Fore) diffs.Add($"ForeColor {Fore:X8} -> {after.Fore:X8}");
            }
            if (FontExplicit != after.FontExplicit || (FontExplicit && Font != after.Font)) diffs.Add($"Font {Font} ({FontExplicit}) -> {after.Font} ({after.FontExplicit})");
            if (Padding != after.Padding) diffs.Add($"Padding {Padding} -> {after.Padding}");
            if (Margin != after.Margin) diffs.Add($"Margin {Margin} -> {after.Margin}");
            if (Cursor != after.Cursor) diffs.Add($"Cursor {Cursor} -> {after.Cursor}");
            if (Visible != after.Visible) diffs.Add($"Visible {Visible} -> {after.Visible}");
            if (RegionNull != after.RegionNull) diffs.Add($"Region null {RegionNull} -> {after.RegionNull}");
            if (FlatStyle != after.FlatStyle) diffs.Add($"FlatStyle {FlatStyle} -> {after.FlatStyle}");
            if (Renderer != after.Renderer) diffs.Add($"Renderer {Renderer} -> {after.Renderer}");
            return diffs.Count == 0 ? null : string.Join("; ", diffs);
        }
    }
}
