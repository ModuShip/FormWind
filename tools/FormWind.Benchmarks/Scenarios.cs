using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Krypton;
using Krypton.Toolkit;
using MaterialSkin;
using MaterialSkin.Controls;

namespace FormWind.Benchmarks;

/// <summary>
/// Bir kütüphanenin aynı içerikli formu nasıl kurduğu ve temasını nasıl değiştirdiği. Her senaryo aynı altı kontrol
/// rolünü (etiket, birincil buton, ikincil buton, metin kutusu, kart, onay kutusu) sırayla tekrarlar.
/// </summary>
internal abstract class Scenario
{
    public abstract string Key { get; }

    public abstract string Title { get; }

    /// <summary>Formu kurar ve stillendirir (henüz gösterilmemiş).</summary>
    public abstract Form Create(int controls);

    public abstract void SetDark(Form form, bool dark);

    /// <summary>Hover ölçümünde fareyle üzerine gelinen kontroller (etiketler hariç).</summary>
    public IReadOnlyList<Control> HoverTargets(Form form) => ((BenchTag)form.Tag!).Interactive;

    public static readonly Scenario[] All =
    {
        new PlainScenario(),
        new FormWindScenario(),
        new FormWindTwScenario(),
        new FormWindAutoScenario(),
        new KryptonScenario(),
        new KryptonFormWindScenario(),
        new MaterialSkinScenario(),
    };

    public static Scenario Find(string key) =>
        Array.Find(All, s => s.Key == key) ?? throw new ArgumentException("Bilinmeyen senaryo: " + key);

    protected enum Role { Label, Primary, Secondary, Input, Card, Check }

    protected static Role RoleOf(int index) => (Role)(index % 6);

    protected static readonly Size ButtonSize = new(120, 36);
    protected static readonly Size InputSize = new(160, 36);
    protected static readonly Size CardSize = new(140, 60);

    /// <summary>
    /// Ekran dışında, en yakın ekranı ana ekran olan bir konum. Farklı DPI'lı ikinci bir monitöre en yakın konumda form gösterilirken
    /// WM_DPICHANGED alır ve tüm kontroller yeniden ölçeklenir; bu, açılışın değil ekran değişiminin maliyetidir.
    /// </summary>
    public static Point OffScreen = FindOffScreen(otherDpi: false)!.Value;

    /// <param name="otherDpi">
    /// true: form gösterilirken WM_DPICHANGED alan bir konum (ana ekran dışındaki, DPI'ı farklı ya da pencerenin başka DPI ile
    /// oluşturulduğu bir monitör; dizüstü + harici monitörde ikinci ekranda açılan form). Böyle bir monitör yoksa null.
    /// </param>
    public static Point? FindOffScreen(bool otherDpi)
    {
        if (otherDpi)
        {
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.Primary) continue;
                // Ekranın dışında, en yakın ekranı o olan bir nokta.
                var point = new Point(screen.Bounds.Left - 1200 - 4000, screen.Bounds.Top);
                if (Screen.FromRectangle(new Rectangle(point, new Size(1200, 900))).DeviceName != screen.DeviceName)
                    point = new Point(screen.Bounds.Left, screen.Bounds.Bottom + 4000);
                using var form = new Form { StartPosition = FormStartPosition.Manual, Location = point, ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.None };
                bool changed = false;
                form.DpiChanged += (_, _) => changed = true;
                form.Show();
                Application.DoEvents();
                if (changed) return point;
            }
            return null;
        }

        var primary = Screen.PrimaryScreen!.Bounds;
        const int gap = 4000;
        var candidates = new[]
        {
            new Point(primary.Left, primary.Bottom + gap),
            new Point(primary.Right + gap, primary.Top),
            new Point(primary.Left, primary.Top - gap - 900),
            new Point(primary.Left - gap - 1200, primary.Top),
        };
        foreach (var point in candidates)
            if (Screen.FromRectangle(new Rectangle(point, new Size(1200, 900))).Primary) return point;
        return candidates[0];
    }

    /// <summary>Ekran dışında, çerçevesiz, sabit boyutlu bir form ve kontrolleri dizen akış paneli.</summary>
    protected static FlowLayoutPanel Host(Form form, bool transparent = false)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = OffScreen;
        form.ShowInTaskbar = false;
        form.FormBorderStyle = FormBorderStyle.None;
        form.ClientSize = new Size(1200, 900);
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        if (transparent) flow.BackColor = Color.Transparent;
        form.Controls.Add(flow);
        return flow;
    }

    protected static void Add(Form form, FlowLayoutPanel flow, Control control, Role role)
    {
        flow.Controls.Add(control);
        var tag = (BenchTag)(form.Tag ??= new BenchTag());
        if (role != Role.Label) tag.Interactive.Add(control);
    }

    protected static string Text(Role role, int index) => role switch
    {
        Role.Label => "Etiket " + index,
        Role.Primary => "Kaydet " + index,
        Role.Secondary => "Vazgeç " + index,
        Role.Input => "Metin " + index,
        Role.Check => "Seçenek " + index,
        _ => "",
    };

    protected sealed class BenchTag
    {
        public List<Control> Interactive { get; } = new();
        public object? State { get; set; }
    }
}

/// <summary>Kütüphanesiz: renkler elle atanır, koyu tema kontroller gezilerek uygulanır. Taban çizgisi (alt sınır).</summary>
internal sealed class PlainScenario : Scenario
{
    public override string Key => "plain";
    public override string Title => "Düz WinForms (elle renk)";

    private static readonly Color Blue = Color.FromArgb(37, 99, 235), BlueHover = Color.FromArgb(29, 78, 216);

    public override Form Create(int controls)
    {
        var form = new Form();
        var flow = Host(form);
        form.SuspendLayout();
        flow.SuspendLayout();
        for (int i = 0; i < controls; i++)
        {
            var role = RoleOf(i);
            Control control = role switch
            {
                Role.Label => new Label { AutoSize = true, Text = Text(role, i) },
                Role.Primary or Role.Secondary => FlatButton(role, i),
                Role.Input => new TextBox { Size = InputSize, BorderStyle = BorderStyle.FixedSingle, Text = Text(role, i) },
                Role.Card => new Panel { Size = CardSize, BorderStyle = BorderStyle.FixedSingle },
                _ => new CheckBox { AutoSize = true, Text = Text(role, i) },
            };
            Add(form, flow, control, role);
        }
        Paint(form, dark: false);
        flow.ResumeLayout();
        form.ResumeLayout();
        return form;
    }

    private static Button FlatButton(Role role, int index)
    {
        var button = new Button { Size = ButtonSize, Text = Text(role, index), FlatStyle = FlatStyle.Flat };
        button.FlatAppearance.BorderSize = role == Role.Primary ? 0 : 1;
        if (role == Role.Primary)
        {
            button.BackColor = Blue;
            button.ForeColor = Color.White;
            button.FlatAppearance.MouseOverBackColor = BlueHover;
        }
        return button;
    }

    public override void SetDark(Form form, bool dark) => Paint(form, dark);

    private static void Paint(Control root, bool dark)
    {
        Color page = dark ? Color.FromArgb(17, 24, 39) : Color.FromArgb(249, 250, 251);
        Color surface = dark ? Color.FromArgb(31, 41, 55) : Color.White;
        Color text = dark ? Color.FromArgb(229, 231, 235) : Color.FromArgb(55, 65, 81);
        Color border = dark ? Color.FromArgb(55, 65, 81) : Color.FromArgb(229, 231, 235);
        root.BackColor = page;
        foreach (Control flow in root.Controls)
        foreach (Control control in flow.Controls)
        {
            switch (control)
            {
                case Button { BackColor: var back } button when back == Blue:
                    break;
                case Button button:
                    button.BackColor = surface;
                    button.ForeColor = text;
                    button.FlatAppearance.BorderColor = border;
                    button.FlatAppearance.MouseOverBackColor = dark ? Color.FromArgb(55, 65, 81) : Color.FromArgb(243, 244, 246);
                    break;
                case TextBox or Panel:
                    control.BackColor = surface;
                    control.ForeColor = text;
                    break;
                default:
                    control.ForeColor = text;
                    break;
            }
        }
    }
}

/// <summary>FormWind, standart kontroller ve class'lar.</summary>
internal class FormWindScenario : Scenario
{
    public override string Key => "formwind";
    public override string Title => "FormWind (standart kontroller + class)";

    protected virtual Control Make(Role role, int index) => role switch
    {
        Role.Label => new Label { AutoSize = true, Text = Text(role, index) },
        Role.Primary or Role.Secondary => new Button { Size = ButtonSize, Text = Text(role, index) },
        Role.Input => new TextBox { Size = InputSize, Text = Text(role, index) },
        Role.Card => new Panel { Size = CardSize },
        _ => new CheckBox { AutoSize = true, Text = Text(role, index) },
    };

    protected virtual string Classes(Role role) => role switch
    {
        Role.Label or Role.Check => "text-sm text-gray-700 dark:text-gray-200",
        Role.Primary => "btn-primary",
        Role.Secondary => "btn-ghost",
        Role.Input => "input",
        _ => "card",
    };

    protected virtual void Configure(TailwindProvider provider, Form form) { }

    public override Form Create(int controls)
    {
        var form = new Form();
        var components = new Container();
        form.Disposed += (_, _) => components.Dispose();
        var provider = new TailwindProvider(components);
        var flow = Host(form);

        // InitializeComponent sırası: BeginInit, SuspendLayout, … EndInit, ResumeLayout.
        provider.BeginInit();
        form.SuspendLayout();
        flow.SuspendLayout();
        provider.SetClasses(form, "page");
        for (int i = 0; i < controls; i++)
        {
            var role = RoleOf(i);
            var control = Make(role, i);
            Add(form, flow, control, role);
            var classes = Classes(role);
            if (classes.Length > 0) provider.SetClasses(control, classes);
        }
        Configure(provider, form);
        provider.EndInit();
        flow.ResumeLayout();
        form.ResumeLayout();

        ((BenchTag)form.Tag!).State = provider;
        return form;
    }

    public override void SetDark(Form form, bool dark) => ((TailwindProvider)((BenchTag)form.Tag!).State!).DarkMode = dark;
}

/// <summary>FormWind'in kendi çizen kontrolleri (kenar yumuşatmalı köşeler, TwTextBox çerçevesi).</summary>
internal sealed class FormWindTwScenario : FormWindScenario
{
    public override string Key => "formwind-tw";
    public override string Title => "FormWind (Tw* kontrolleri)";

    protected override Control Make(Role role, int index) => role switch
    {
        Role.Primary or Role.Secondary => new TwButton { Size = ButtonSize, Text = Text(role, index) },
        Role.Input => new TwTextBox { Size = InputSize, Text = Text(role, index) },
        Role.Card => new TwPanel { Size = CardSize },
        Role.Check => new TwCheckBox { AutoSize = true, Text = Text(role, index) },
        _ => base.Make(role, index),
    };
}

/// <summary>FormWind AutoStyle + Fluent hazır görünümü: class yazılmaz (yalnızca birincil butonda btn-primary).</summary>
internal sealed class FormWindAutoScenario : FormWindScenario
{
    private static readonly TwTheme Fluent = new(TwPreset.Fluent);

    public override string Key => "formwind-auto";
    public override string Title => "FormWind AutoStyle + Fluent";

    protected override string Classes(Role role) => role == Role.Primary ? "btn-primary" : "";

    protected override void Configure(TailwindProvider provider, Form form)
    {
        provider.Theme = Fluent;
        provider.ContainerControl = form;
        provider.AutoStyle = true;
    }
}

/// <summary>Krypton Toolkit: Krypton kontrolleri, global palet (Microsoft 365 White / Black Dark Mode).</summary>
internal class KryptonScenario : Scenario
{
    protected static readonly KryptonManager Manager = new() { GlobalPaletteMode = PaletteMode.Microsoft365White };

    public override string Key => "krypton";
    public override string Title => "Krypton Toolkit 100.25";

    protected static Control Make(Role role, int index)
    {
        switch (role)
        {
            case Role.Label: return new KryptonLabel { Values = { Text = Text(role, index) } };
            case Role.Primary:
            case Role.Secondary:
                return new KryptonButton { Size = ButtonSize, Values = { Text = Text(role, index) } };
            case Role.Input: return new KryptonTextBox { Size = InputSize, Text = Text(role, index) };
            case Role.Card: return new KryptonPanel { Size = CardSize, PanelBackStyle = PaletteBackStyle.ControlClient };
            default: return new KryptonCheckBox { Values = { Text = Text(role, index) } };
        }
    }

    protected virtual void Style(Control control, Role role)
    {
        // Birincil buton: Krypton'da kontrol başına durum renkleri.
        if (role == Role.Primary && control is KryptonButton button)
        {
            button.StateCommon.Back.Color1 = Color.FromArgb(37, 99, 235);
            button.StateCommon.Back.Color2 = Color.FromArgb(37, 99, 235);
            button.StateCommon.Content.ShortText.Color1 = Color.White;
            button.StateTracking.Back.Color1 = Color.FromArgb(29, 78, 216);
            button.StateTracking.Back.Color2 = Color.FromArgb(29, 78, 216);
        }
    }

    public override Form Create(int controls)
    {
        var form = new KryptonForm();
        var flow = Host(form, transparent: true);
        form.SuspendLayout();
        flow.SuspendLayout();
        for (int i = 0; i < controls; i++)
        {
            var role = RoleOf(i);
            var control = Make(role, i);
            Style(control, role);
            Add(form, flow, control, role);
        }
        Created(form);
        flow.ResumeLayout();
        form.ResumeLayout();
        return form;
    }

    protected virtual void Created(Form form) { }

    public override void SetDark(Form form, bool dark) =>
        Manager.GlobalPaletteMode = dark ? PaletteMode.Microsoft365BlackDarkMode : PaletteMode.Microsoft365White;
}

/// <summary>Krypton + FormWind.Krypton: palet FormWind temasından (Fluent) üretilir, birincil butonda btn-primary.</summary>
internal sealed class KryptonFormWindScenario : KryptonScenario
{
    private static readonly TwTheme Fluent = new(TwPreset.Fluent);
    private static IDisposable? _follow;

    static KryptonFormWindScenario() => KryptonApplier.Register();

    public override string Key => "krypton-formwind";
    public override string Title => "Krypton + FormWind.Krypton (Fluent)";

    private TailwindProvider? _provider;
    private Container? _components;

    protected override void Style(Control control, Role role)
    {
        if (role == Role.Primary) _provider!.SetClasses(control, "btn-primary");
    }

    public override Form Create(int controls)
    {
        _components = new Container();
        _provider = new TailwindProvider(_components) { Theme = Fluent };
        _provider.BeginInit();
        var form = base.Create(controls);
        var components = _components;
        form.Disposed += (_, _) => components.Dispose();
        return form;
    }

    protected override void Created(Form form)
    {
        _provider!.EndInit();
        ((BenchTag)form.Tag!).State = _provider;
        // Palet bu formun sağlayıcısını izler (uygulamada bir kez, ana formda yapılır).
        _follow?.Dispose();
        _follow = TwKryptonTheme.Follow(_provider, Manager);
    }

    public override void SetDark(Form form, bool dark) => ((TailwindProvider)((BenchTag)form.Tag!).State!).DarkMode = dark;
}

/// <summary>MaterialSkin.2: MaterialForm ve Material kontrolleri, MaterialSkinManager teması.</summary>
internal sealed class MaterialSkinScenario : Scenario
{
    private static readonly MaterialSkinManager Manager = MaterialSkinManager.Instance;

    public override string Key => "materialskin";
    public override string Title => "MaterialSkin.2 2.3.1";

    public override Form Create(int controls)
    {
        var form = new MaterialForm { Sizable = false };
        var flow = Host(form);
        form.SuspendLayout();
        flow.SuspendLayout();
        for (int i = 0; i < controls; i++)
        {
            var role = RoleOf(i);
            Control control = role switch
            {
                Role.Label => new MaterialLabel { AutoSize = true, Text = Text(role, i) },
                Role.Primary => new MaterialButton { AutoSize = false, Size = ButtonSize, Text = Text(role, i), Type = MaterialButton.MaterialButtonType.Contained },
                Role.Secondary => new MaterialButton { AutoSize = false, Size = ButtonSize, Text = Text(role, i), Type = MaterialButton.MaterialButtonType.Outlined },
                Role.Input => new MaterialTextBox2 { Size = InputSize, Text = Text(role, i) },
                Role.Card => new MaterialCard { Size = CardSize },
                _ => new MaterialCheckbox { AutoSize = true, Text = Text(role, i) },
            };
            Add(form, flow, control, role);
        }
        flow.ResumeLayout();
        form.ResumeLayout();
        Manager.AddFormToManage(form);
        form.Disposed += (_, _) => Manager.RemoveFormToManage(form);
        return form;
    }

    public override void SetDark(Form form, bool dark) =>
        Manager.Theme = dark ? MaterialSkinManager.Themes.DARK : MaterialSkinManager.Themes.LIGHT;
}
