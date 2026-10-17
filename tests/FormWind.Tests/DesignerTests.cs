using System.ComponentModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Windows.Forms;
using FormWind.Design;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class DesignerTests
{
    [Fact]
    public void Tab_control_designer_adds_and_removes_sited_pages() => Sta.Run(() =>
    {
        using var surface = new DesignSurface(typeof(Form));
        var host = (IDesignerHost)surface.GetService(typeof(IDesignerHost))!;
        var form = (Form)host.RootComponent;
        var tabs = (TwTabControl)host.CreateComponent(typeof(TwTabControl), "tabs");
        form.Controls.Add(tabs);

        var designer = Assert.IsType<TwTabControlDesigner>(host.GetDesigner(tabs));
        Assert.Equal(new[] { "Add page", "Remove selected page" }, designer.Verbs.Cast<DesignerVerb>().Select(v => v.Text));

        designer.Verbs[0]!.Invoke();
        designer.Verbs[0]!.Invoke();
        Assert.Equal(2, tabs.TabPages.Count);
        Assert.All(tabs.TabPages, p => Assert.NotNull(p.Site));            // Designer.cs'e yazılacak bileşenler
        Assert.Equal(new[] { "Page 1", "Page 2" }, tabs.Strip.Tabs);
        Assert.Same(tabs.TabPages[1], tabs.SelectedTab);

        Assert.True(designer.CanParent(new TwTabPage()));
        Assert.False(designer.CanParent(new Button()));                     // diğer kontroller sayfaya bırakılır

        // Bir sayfayı seçmek o sayfayı gösterir.
        var selection = (ISelectionService)surface.GetService(typeof(ISelectionService))!;
        selection.SetSelectedComponents(new[] { tabs.TabPages[0] });
        Assert.Equal(0, tabs.SelectedIndex);

        designer.Verbs[1]!.Invoke();
        Assert.Single(tabs.TabPages);
        Assert.Equal("Page 2", tabs.TabPages[0].Text);
        Assert.Single(designer.ActionLists);
    });

    [Fact]
    public void Classes_property_suggests_components() => Sta.Run(() =>
    {
        using var surface = new DesignSurface(typeof(Form));
        var host = (IDesignerHost)surface.GetService(typeof(IDesignerHost))!;
        var form = (Form)host.RootComponent;
        host.CreateComponent(typeof(TailwindProvider), "tw");
        var button = (Button)host.CreateComponent(typeof(Button), "btn");
        form.Controls.Add(button);

        var property = System.ComponentModel.TypeDescriptor.GetProperties(button)["Classes"]!;
        var converter = Assert.IsType<ClassesConverter>(property.Converter);
        Assert.True(converter.GetStandardValuesSupported());
        Assert.False(converter.GetStandardValuesExclusive());              // serbest metin de yazılır
        var values = converter.GetStandardValues()!.Cast<string>().ToArray();
        Assert.Contains("btn-primary", values);
        Assert.Contains("card", values);
    });

    // Standart kontrollerin akıllı etiketine provider'ın tasarımcısı bir FormWind bölümü ekler.
    [Fact]
    public void Smart_tag_of_standard_controls_offers_components() => Sta.Run(() =>
    {
        using var surface = new DesignSurface(typeof(Form));
        var host = (IDesignerHost)surface.GetService(typeof(IDesignerHost))!;
        var form = (Form)host.RootComponent;
        // Visual Studio akıllı etiket servisini kendisi sağlar; düz DesignSurface'te oluşturulur (kendini kaydeder).
        using var actions = new DesignerActionService(surface);
        var button = (Button)host.CreateComponent(typeof(Button), "btn");
        form.Controls.Add(button);
        var provider = (TailwindProvider)host.CreateComponent(typeof(TailwindProvider), "tw");
        Assert.IsType<TailwindProviderDesigner>(host.GetDesigner(provider));
        var textBox = (TextBox)host.CreateComponent(typeof(TextBox), "txt");     // provider'dan sonra eklenen
        form.Controls.Add(textBox);

        DesignerActionItem[] Items(System.ComponentModel.IComponent component) => actions.GetComponentActions(component)
            .Cast<DesignerActionList>().OfType<TailwindProviderDesigner.ClassesActionList>()
            .SelectMany(l => l.GetSortedActionItems().Cast<DesignerActionItem>()).ToArray();

        var buttonItems = Items(button);
        Assert.Equal(new[] { "btn-primary", "btn-ghost", "btn-danger" },
            buttonItems.OfType<DesignerActionMethodItem>().Select(i => i.DisplayName));
        Assert.Contains("input", Items(textBox).Select(i => i.DisplayName));

        int changes = 0;
        ((IComponentChangeService)surface.GetService(typeof(IComponentChangeService))!).ComponentChanged += (_, _) => changes++;
        buttonItems.OfType<DesignerActionMethodItem>().First(i => i.DisplayName == "btn-ghost").Invoke();
        Assert.Equal("btn-ghost", provider.GetClasses(button));
        Assert.True(changes > 0);                                                 // Designer.cs'e yazılır, geri alınabilir

        var clear = Items(button).OfType<DesignerActionMethodItem>().Single(i => i.DisplayName == "Clear classes");
        clear.Invoke();
        Assert.Equal("", provider.GetClasses(button));

        host.DestroyComponent(textBox);
        Assert.Empty(actions.GetComponentActions(textBox).Cast<DesignerActionList>().OfType<TailwindProviderDesigner.ClassesActionList>());
    });

    // Önizleme class renklerini kontrole uygular; bu değerler Designer.cs'e yazılmamalı (çalışma anında class'lar zaten verir).
    [Fact]
    public void Preview_values_managed_by_classes_are_not_serialized() => Sta.Run(() =>
    {
        using var surface = new DesignSurface(typeof(Form));
        var host = (IDesignerHost)surface.GetService(typeof(IDesignerHost))!;
        var form = (Form)host.RootComponent;
        var provider = (TailwindProvider)host.CreateComponent(typeof(TailwindProvider), "tw");
        var button = (Button)host.CreateComponent(typeof(Button), "btn");
        form.Controls.Add(button);
        button.Font = new System.Drawing.Font("Segoe UI", 12f);                    // class'ların yönetmediği, elle atanmış

        var properties = System.ComponentModel.TypeDescriptor.GetProperties(button);
        properties["Classes"]!.SetValue(button, "bg-blue-600 text-white p-2");
        Assert.Equal(System.Drawing.ColorTranslator.FromHtml("#2563eb").ToArgb(), button.BackColor.ToArgb());   // önizleme
        Assert.False(properties["BackColor"]!.ShouldSerializeValue(button));
        Assert.False(properties["ForeColor"]!.ShouldSerializeValue(button));
        Assert.False(properties["Padding"]!.ShouldSerializeValue(button));
        Assert.True(properties["Font"]!.ShouldSerializeValue(button));

        provider.DesignTimePreview = false;                                         // önizleme kapalıyken dokunulmaz
        button.BackColor = System.Drawing.Color.Red;
        Assert.True(System.ComponentModel.TypeDescriptor.GetProperties(button)["BackColor"]!.ShouldSerializeValue(button));
    });

    private static (DesignSurface Surface, DesignerActionService Actions, TailwindProvider Provider, Button Button) CreateForm()
    {
        var surface = new DesignSurface(typeof(Form));
        var host = (IDesignerHost)surface.GetService(typeof(IDesignerHost))!;
        var actions = new DesignerActionService(surface);
        var provider = (TailwindProvider)host.CreateComponent(typeof(TailwindProvider), "tw");
        var button = (Button)host.CreateComponent(typeof(Button), "btn");
        ((Form)host.RootComponent).Controls.Add(button);
        return (surface, actions, provider, button);
    }

    private static TailwindProviderDesigner.ClassesActionList ActionList(DesignerActionService actions, IComponent component) =>
        actions.GetComponentActions(component).Cast<DesignerActionList>().OfType<TailwindProviderDesigner.ClassesActionList>().Single();

    // Akıllı etiketteki class oluşturucu: her alan yalnızca kendi grubundaki class'ları okur ve değiştirir, gerisi korunur.
    // Alanlar TypeConverter'lı düz özellikler olduğu için süreç dışı (.NET 8+) Designer'da da çalışır.
    [Fact]
    public void Class_builder_fields_edit_only_their_own_classes() => Sta.Run(() =>
    {
        var (surface, actions, provider, button) = CreateForm();
        using (surface)
        using (actions)
        {
            var list = ActionList(actions, button);
            list.Classes = "btn-ghost px-6 hover:bg-blue-700 dark:bg-gray-900";

            Assert.Equal("btn-ghost", list.ThemeComponent);
            Assert.Equal("px-6", list.Padding);
            Assert.Equal("hover:bg-blue-700", list.HoverBackground);
            Assert.Equal("", list.Background);

            int changes = 0;
            ((IComponentChangeService)surface.GetService(typeof(IComponentChangeService))!).ComponentChanged += (_, _) => changes++;

            list.Background = "bg-red-500";
            list.Padding = "px-3 py-2";
            list.HoverBackground = "";
            list.DarkTextColor = "dark:text-white";
            list.ThemeComponent = "btn-primary";                                   // bileşen değişir, utility'ler kalır
            Assert.Equal("btn-primary dark:bg-gray-900 bg-red-500 px-3 py-2 dark:text-white", provider.GetClasses(button));
            Assert.True(changes >= 5);                                              // Designer.cs'e yazılır, geri alınabilir

            // Tek tıklık bileşen komutu da utility'leri korur.
            Items(actions, button).OfType<DesignerActionMethodItem>().First(i => i.DisplayName == "btn-danger").Invoke();
            Assert.Equal("btn-danger dark:bg-gray-900 bg-red-500 px-3 py-2 dark:text-white", provider.GetClasses(button));

            Assert.DoesNotContain(Items(actions, button).OfType<DesignerActionTextItem>(), i => i.DisplayName!.StartsWith("Unknown"));
            list.Background = "bg-nope-500";                                        // bilinmeyen class uyarısı
            Assert.Equal("Unknown classes: bg-nope-500",
                Items(actions, button).OfType<DesignerActionTextItem>().Single(i => i.DisplayName!.StartsWith("Unknown")).DisplayName);

            var names = list.GetSortedActionItems().OfType<DesignerActionPropertyItem>().Select(i => i.MemberName).ToArray();
            Assert.Contains(nameof(list.Corners), names);
            Assert.Contains(nameof(list.DarkBackground), names);
        }
    });

    private static DesignerActionItem[] Items(DesignerActionService actions, IComponent component) =>
        ActionList(actions, component).GetSortedActionItems().Cast<DesignerActionItem>().ToArray();

    [Fact]
    public void Class_builder_drop_downs_list_the_providers_theme() => Sta.Run(() =>
    {
        var (surface, actions, provider, button) = CreateForm();
        using (surface)
        using (actions)
        {
            var theme = new TwTheme();
            theme.SetColor("brand", "500", System.Drawing.Color.Purple);
            theme.SetComponent("pill", "rounded-full px-3");
            provider.Theme = theme;
            var list = ActionList(actions, button);

            string[] Values(string property)
            {
                var descriptor = TypeDescriptor.GetProperties(list)[property]!;
                return descriptor.Converter.GetStandardValues(new Context(list, descriptor))!.Cast<string>().ToArray();
            }

            Assert.Equal("bg-primary-50", Values(nameof(list.Background))[0]);  // anlamsal ve temanın kendi renkleri önce
            Assert.Contains("bg-brand-500", Values(nameof(list.Background)));
            Assert.Contains("hover:bg-blue-600", Values(nameof(list.HoverBackground)));
            Assert.Contains("rounded-lg", Values(nameof(list.Corners)));
            var components = Values(nameof(list.ThemeComponent));
            Assert.Equal(new[] { "btn-primary", "btn-ghost", "btn-danger" }, components.Take(3));  // kontrol tipine uygun olanlar önce
            Assert.Contains("pill", components);

            // Classes alanının listesi mevcut bileşenin yerine geçer, utility'leri korur.
            provider.SetClasses(button, "btn-ghost px-6");
            var classes = TypeDescriptor.GetProperties(button)["Classes"]!;
            var suggested = classes.Converter.GetStandardValues(new Context(button, classes))!.Cast<string>().ToArray();
            Assert.Contains("btn-primary px-6", suggested);
            Assert.Contains("pill px-6", suggested);
        }
    });

    // Designer, tasarlanan sınıfın derlemesindeki temayı (FormWind.targets'in gömdüğü tw.config.json) kullanır.
    [Fact]
    public void Designer_uses_the_theme_embedded_in_the_project_assembly() => Sta.Run(() =>
    {
        var previous = TwTheme.Current;
        try
        {
            using var surface = new DesignSurface(typeof(Form));
            var host = (IDesignerHost)surface.GetService(typeof(IDesignerHost))!;
            Assert.False(string.IsNullOrEmpty(host.RootComponentClassName));
            host.AddService(typeof(ITypeResolutionService), new ProjectTypes(host.RootComponentClassName, typeof(DesignerTests)));
            var provider = (TailwindProvider)host.CreateComponent(typeof(TailwindProvider), "tw");
            var button = (Button)host.CreateComponent(typeof(Button), "btn");
            ((Form)host.RootComponent).Controls.Add(button);

            Assert.True(provider.Theme.TryGetColor("brand", "600", out _));
            Assert.Same(provider.Theme, TwTheme.Current);                        // ikonlar ve grid kolonları da projenin temasıyla çizilir
            Assert.Contains("pill", provider.Theme.Components.Keys);

            provider.SetClasses(button, "btn-primary");                               // önizleme projenin bileşeniyle
            Assert.Equal(System.Drawing.ColorTranslator.FromHtml("#6d28d9").ToArgb(), button.BackColor.ToArgb());
        }
        finally
        {
            TwTheme.Current = previous;
        }
    });

    // Tema bulunamazsa (proje derlenmemiş, dosya yok) varsayılan tema kullanılır; başka bir formun Current'ına bağlanılmaz.
    [Fact]
    public void Designer_falls_back_to_the_default_theme() => Sta.Run(() =>
    {
        var previous = TwTheme.Current;
        var other = new TwTheme();
        other.SetColor("brand", "500", System.Drawing.Color.Purple);
        TwTheme.Current = other;
        try
        {
            var (surface, actions, provider, _) = CreateForm();
            using (surface)
            using (actions)
            {
                Assert.NotSame(other, provider.Theme);
                Assert.False(provider.Theme.TryGetColor("brand", "500", out _));
                Assert.Same(other, TwTheme.Current);
            }
        }
        finally
        {
            TwTheme.Current = previous;
        }
    });

    [Fact]
    public void Theme_file_next_to_the_assembly_wins_over_the_embedded_copy()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FormWindDesignTheme_" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        try
        {
            var assembly = typeof(DesignerTests).Assembly;
            var embedded = TwDesignTheme.FromAssembly(assembly, folder)!;
            Assert.True(embedded.TryGetColor("brand", "600", out _));              // klasörde dosya yok: gömülü kopya

            System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "tw.config.json"), "{ \"colors\": { \"accent\": \"#00ff00\" } }");
            var file = TwDesignTheme.FromAssembly(assembly, folder)!;
            Assert.True(file.TryGetColor("accent", "", out _));
            Assert.False(file.TryGetColor("brand", "600", out _));

            Assert.Null(TwDesignTheme.FromAssembly(typeof(Form).Assembly, null));  // temasız derleme
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }

    private sealed class Context : ITypeDescriptorContext
    {
        public Context(object instance, PropertyDescriptor property)
        {
            Instance = instance;
            PropertyDescriptor = property;
        }

        public IContainer? Container => null;
        public object Instance { get; }
        public PropertyDescriptor PropertyDescriptor { get; }
        public object? GetService(System.Type serviceType) => null;
        public bool OnComponentChanging() => true;
        public void OnComponentChanged() { }
    }

    // Visual Studio'nun tür çözümleme servisinin yerine: tasarlanan sınıfı test derlemesinden verir.
    private sealed class ProjectTypes : ITypeResolutionService
    {
        private readonly string _rootName;
        private readonly System.Type _root;

        public ProjectTypes(string rootName, System.Type root)
        {
            _rootName = rootName;
            _root = root;
        }

        public System.Reflection.Assembly? GetAssembly(System.Reflection.AssemblyName name) => null;
        public System.Reflection.Assembly? GetAssembly(System.Reflection.AssemblyName name, bool throwOnError) => null;
        public string? GetPathOfAssembly(System.Reflection.AssemblyName name) => null;
        public System.Type? GetType(string name) => GetType(name, false, false);
        public System.Type? GetType(string name, bool throwOnError) => GetType(name, throwOnError, false);

        // Tasarımcı türleri de bu servisle çözülür; yalnızca tasarlanan sınıfın adı test derlemesine yönlenir.
        public System.Type? GetType(string name, bool throwOnError, bool ignoreCase) =>
            name == _rootName ? _root : System.Type.GetType(name, throwOnError, ignoreCase);
        public void ReferenceAssembly(System.Reflection.AssemblyName name) { }
    }
}
