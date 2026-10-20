using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.ComponentModel.Design.Serialization;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Designer'ın Designer.cs'e yazdığı kod: tasarım yüzeyi Visual Studio'nun kullandığı CodeDOM serileştiricisiyle koda çevrilir,
/// kod yeniden yüklenip tekrar serileştirilir.
/// </summary>
[Collection(UiCollection.Name)]
public class DesignerCodeTests
{
    // Önizleme Designer.cs'e yalnızca SetClasses olarak yansımalı: önizleme açık ya da kapalı aynı kod üretilir, kayıt sırasında
    // ComponentChanged bildirilmez (Visual Studio formu kaydedince yeniden "değişti" saymaz), kaydet → yükle döngüsü sabittir.
    [Fact]
    public void Preview_leaves_only_the_classes_in_the_generated_code() => Sta.Run(() =>
    {
        using var surface = NewSurface();
        var host = Host(surface);
        var form = (Form)host.RootComponent;
        var provider = (TailwindProvider)host.CreateComponent(typeof(TailwindProvider), "tw");
        provider.AutoStyle = true;
        void Add<T>(string name, string? classes = null) where T : Control
        {
            var control = (T)host.CreateComponent(typeof(T), name);
            form.Controls.Add(control);
            if (classes != null) TypeDescriptor.GetProperties(control)["Classes"]!.SetValue(control, classes);
        }
        Add<Button>("save", "btn-primary rounded-lg");
        Add<Button>("auto");
        Add<TextBox>("name");
        Add<RichTextBox>("rich");
        Add<DataGridView>("grid");
        Add<LinkLabel>("link");
        Add<Label>("title", "text-lg font-semibold");
        var changes = (IComponentChangeService)host.GetService(typeof(IComponentChangeService))!;
        int changed = 0;
        changes.ComponentChanged += (_, _) => changed++;

        var (declaration, with) = Serialize(host);
        Assert.Equal(0, changed);
        foreach (var leak in new[] { "FlatStyle", "FlatAppearance", "UseVisualStyleBackColor", "BorderStyle", "LinkColor", "CellStyle", "BackColor", "SetClasses(this.auto" })
            Assert.DoesNotContain(leak, with);
        Assert.Contains("this.tw.SetClasses(this.save, \"btn-primary rounded-lg\");", with);

        var resources = (IResourceService)surface.GetService(typeof(IResourceService))!;
        string? previous = null;
        for (int i = 0; i < 3; i++)
        {
            using var again = Reload(declaration, resources);
            (declaration, var code) = Serialize(Host(again));
            if (previous != null) Assert.Equal(previous, code);
            previous = code;
        }

        // AutoSize metin kutusunun yüksekliği fontundan gelir: .NET Framework'te önizlemenin page fontu (Segoe UI) varsayılan
        // fonttan büyüktür; Size satırı karşılaştırılmaz (çalışma anında yeniden hesaplanır).
        static string WithoutTextBoxHeight(string code) => System.Text.RegularExpressions.Regex.Replace(code, @"this\.name\.Size = [^;]+;", "");
        provider.DesignTimePreview = false;
        Assert.Equal(WithoutTextBoxHeight(with), WithoutTextBoxHeight(Serialize(host).Code.Replace("        this.tw.DesignTimePreview = false;" + Environment.NewLine, "")));
    });

    // Designer'da FollowParentBackColor hiçbir rengi sıfırlamaz: formun rengini kopyalayan çocukların rengi Designer.cs'te
    // kalır (sıfırlansaydı kod değişir, AutoStyle kapatılınca kontroller rengini kaybederdi).
    [Fact]
    public void Follow_parent_back_color_resets_nothing_in_the_designer() => Sta.Run(() =>
    {
        using var surface = NewSurface();
        var host = Host(surface);
        var form = (Form)host.RootComponent;
        var appBack = ColorTranslator.FromHtml("#272727");
        TypeDescriptor.GetProperties(form)["BackColor"]!.SetValue(form, appBack);
        var label = (Label)host.CreateComponent(typeof(Label), "caption");
        var panel = (Panel)host.CreateComponent(typeof(Panel), "body");
        form.Controls.Add(label);
        form.Controls.Add(panel);
        TypeDescriptor.GetProperties(label)["BackColor"]!.SetValue(label, appBack);
        TypeDescriptor.GetProperties(panel)["BackColor"]!.SetValue(panel, appBack);
        var provider = (TailwindProvider)host.CreateComponent(typeof(TailwindProvider), "tw");
        Assert.True(provider.FollowParentBackColor);
        provider.AutoStyle = true;

        Assert.Equal(appBack.ToArgb(), label.BackColor.ToArgb());
        var code = Serialize(host).Code;
        Assert.Contains("this.caption.BackColor = ", code);
        Assert.Contains("this.body.BackColor = ", code);
    });

    // %150'de (144 DPI) class değerleri bir kez ölçeklenir: h-10 = 60 px yazılır; yeniden açıp kaydetmek boyutu büyütmez.
    [Fact]
    public void Sizes_do_not_grow_when_saved_at_150_percent() => Sta.Run(() =>
    {
        TwDpi.DeviceDpiOverride = 144;
        try
        {
            using var surface = NewSurface();
            var host = Host(surface);
            var form = (Form)host.RootComponent;
            var provider = (TailwindProvider)host.CreateComponent(typeof(TailwindProvider), "tw");
            provider.AutoStyle = true;
            var button = (Button)host.CreateComponent(typeof(Button), "save");
            var name = (TextBox)host.CreateComponent(typeof(TextBox), "name");
            form.Controls.Add(button);
            form.Controls.Add(name);
            TypeDescriptor.GetProperties(button)["Classes"]!.SetValue(button, "btn-primary h-10 w-32");

            var (declaration, code) = Serialize(host);
            Assert.Contains("this.save.Size = new System.Drawing.Size(192, 60);", code);
            Assert.DoesNotContain("Padding", code);
            // Metin kutusunun yüksekliği fontundan gelir (AutoSize): .NET Framework'te ilk açılışta formun varsayılan fontuyla bir
            // kez küçülebilir; sonra sabit kalır ve hiç büyümez.
            int NameHeight(string source) => int.Parse(System.Text.RegularExpressions.Regex.Match(source, @"this\.name\.Size = new System\.Drawing\.Size\(\d+, (\d+)\)").Groups[1].Value);
            var resources = (IResourceService)surface.GetService(typeof(IResourceService))!;
            string? previous = null;
            for (int i = 0; i < 3; i++)
            {
                using var again = Reload(declaration, resources);
                (declaration, var next) = Serialize(Host(again));
                Assert.Contains("this.save.Size = new System.Drawing.Size(192, 60);", next);
                Assert.True(NameHeight(next) <= NameHeight(code), next);
                if (previous != null) Assert.Equal(previous, next);
                previous = next;
            }
        }
        finally
        {
            TwDpi.DeviceDpiOverride = null;
        }
    });

#if !NETFRAMEWORK
    // FW001/FW002 Designer'ın ürettiği kodda doğru yerde uyarır: önizlemeli temiz formda FW001 yok; önizleme kapalıyken elle
    // atanan BackColor class'larla çakışır (FW001). .NET Framework'te AutoScaleDimensions'tan sonra atanan form fontu FW002.
    [Fact]
    public void Analyzers_warn_in_the_right_places_of_generated_code() => Sta.Run(() =>
    {
        using var surface = NewSurface();
        var host = Host(surface);
        var form = (Form)host.RootComponent;
        var properties = TypeDescriptor.GetProperties(form);
        properties["AutoScaleDimensions"]!.SetValue(form, new SizeF(7F, 15F));
        properties["AutoScaleMode"]!.SetValue(form, AutoScaleMode.Font);
        properties["Font"]!.SetValue(form, new Font("Segoe UI", 9F));
        var provider = (TailwindProvider)host.CreateComponent(typeof(TailwindProvider), "tw");
        var button = (Button)host.CreateComponent(typeof(Button), "save");
        form.Controls.Add(button);
        TypeDescriptor.GetProperties(button)["Classes"]!.SetValue(button, "btn-primary");

        string Source() => Serialize(host).Code
            .Replace("public class  :", "public class OrderForm :").Replace("private () {", "private OrderForm() {");
        var clean = Source();
        Assert.Empty(AnalyzerTests.Analyze(clean));
        var fw002 = Assert.Single(AnalyzerTests.AnalyzeFontScaling(clean, "net472"));
        Assert.Equal("FW002", fw002.Id);
        Assert.StartsWith("this.Font = ", clean.Substring(fw002.Location.SourceSpan.Start));
        Assert.Empty(AnalyzerTests.AnalyzeFontScaling(clean, "net8.0-windows"));

        provider.DesignTimePreview = false;
        TypeDescriptor.GetProperties(button)["BackColor"]!.SetValue(button, Color.Red);
        var conflict = Source();
        var fw001 = Assert.Single(AnalyzerTests.Analyze(conflict));
        Assert.Equal("FW001", fw001.Id);
        Assert.StartsWith("this.save.BackColor = ", conflict.Substring(fw001.Location.SourceSpan.Start));
    });
#endif

    internal static DesignSurface NewSurface()
    {
        var surface = new DesignSurface(typeof(Form));
        Host(surface).AddService(typeof(IResourceService), new MemoryResources());
        return surface;
    }

    /// <summary>Loads the serialized class into a new design surface, as Visual Studio does when the form is reopened.</summary>
    internal static DesignSurface Reload(CodeTypeDeclaration declaration, IResourceService resources)
    {
        var surface = new DesignSurface();
        surface.BeginLoad(new CodeLoader(declaration, resources));
        if (surface.LoadErrors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, System.Linq.Enumerable.Cast<object>(surface.LoadErrors)));
        return surface;
    }

    private sealed class CodeLoader : BasicDesignerLoader
    {
        private readonly CodeTypeDeclaration _declaration;
        private readonly IResourceService _resources;

        public CodeLoader(CodeTypeDeclaration declaration, IResourceService resources)
        {
            _declaration = declaration;
            _resources = resources;
        }

        protected override void Initialize()
        {
            base.Initialize();
            LoaderHost.AddService(typeof(IResourceService), _resources);
            LoaderHost.AddService(typeof(ITypeResolutionService), new LoadedTypes());
        }

        protected override void PerformLoad(IDesignerSerializationManager manager)
        {
            var serializer = (TypeCodeDomSerializer)manager.GetSerializer(typeof(Form), typeof(TypeCodeDomSerializer))!;
            serializer.Deserialize(manager, _declaration);
        }

        protected override void PerformFlush(IDesignerSerializationManager manager) { }
    }

    // Kodda tam adıyla geçen türler yüklü derlemelerde aranır (Visual Studio projenin başvurularında arar).
    private sealed class LoadedTypes : ITypeResolutionService
    {
        public System.Reflection.Assembly? GetAssembly(System.Reflection.AssemblyName name) => null;
        public System.Reflection.Assembly? GetAssembly(System.Reflection.AssemblyName name, bool throwOnError) => null;
        public string? GetPathOfAssembly(System.Reflection.AssemblyName name) => null;
        public Type? GetType(string name) => GetType(name, false, false);
        public Type? GetType(string name, bool throwOnError) => GetType(name, throwOnError, false);

        public Type? GetType(string name, bool throwOnError, bool ignoreCase)
        {
            var type = Type.GetType(name, false, ignoreCase);
            if (type != null) return type;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetType(name, false, ignoreCase) is Type found) return found;
            if (throwOnError) throw new TypeLoadException(name);
            return null;
        }

        public void ReferenceAssembly(System.Reflection.AssemblyName name) { }
    }

    internal static IDesignerHost Host(DesignSurface surface) => (IDesignerHost)surface.GetService(typeof(IDesignerHost))!;

    /// <summary>The root as Visual Studio writes it: the class declaration and its C# text.</summary>
    internal static (CodeTypeDeclaration Declaration, string Code) Serialize(IDesignerHost host)
    {
        var manager = new DesignerSerializationManager(host);
        CodeTypeDeclaration declaration;
        using (manager.CreateSession())
        {
            var serializer = (TypeCodeDomSerializer)manager.GetSerializer(host.RootComponent.GetType(), typeof(TypeCodeDomSerializer))!;
            declaration = serializer.Serialize(manager, host.RootComponent, host.Container.Components);
        }
        using var writer = new StringWriter();
        using (var provider = CodeDomProvider.CreateProvider("CSharp"))
            provider.GenerateCodeFromType(declaration, writer, new CodeGeneratorOptions { BlankLinesBetweenMembers = false });
        return (declaration, writer.ToString());
    }

    // Visual Studio'nun .resx'i yerine bellek: .NET'in ResourceWriter'ı ikili serileştirilmiş değerleri yazamaz.
    private sealed class MemoryResources : IResourceService
    {
        private readonly System.Collections.Hashtable _values = new System.Collections.Hashtable();

        public System.Resources.IResourceReader GetResourceReader(System.Globalization.CultureInfo info) => new Reader(_values);

        public System.Resources.IResourceWriter GetResourceWriter(System.Globalization.CultureInfo info) => new Writer(_values);

        private sealed class Reader : System.Resources.IResourceReader
        {
            private readonly System.Collections.Hashtable _values;
            public Reader(System.Collections.Hashtable values) => _values = values;
            public System.Collections.IDictionaryEnumerator GetEnumerator() => _values.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            public void Close() { }
            public void Dispose() { }
        }

        private sealed class Writer : System.Resources.IResourceWriter
        {
            private readonly System.Collections.Hashtable _values;
            public Writer(System.Collections.Hashtable values) => _values = values;
            public void AddResource(string name, byte[]? value) => _values[name] = value;
            public void AddResource(string name, object? value) => _values[name] = value;
            public void AddResource(string name, string? value) => _values[name] = value;
            public void Generate() { }
            public void Close() { }
            public void Dispose() { }
        }
    }
}
