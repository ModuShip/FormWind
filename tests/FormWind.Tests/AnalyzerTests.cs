#if !NETFRAMEWORK
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FormWind.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace FormWind.Tests;

public class AnalyzerTests
{
    internal static ImmutableArray<Diagnostic> Analyze(string source)
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = platform.Select(path => MetadataReference.CreateFromFile(path))
            .Concat(new[] { MetadataReference.CreateFromFile(typeof(Control).Assembly.Location), MetadataReference.CreateFromFile(typeof(TailwindProvider).Assembly.Location) })
            .GroupBy(r => Path.GetFileName(r.FilePath)).Select(g => g.First());
        var compilation = CSharpCompilation.Create("Sample", new[] { CSharpSyntaxTree.ParseText(source) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        return compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new StyleOverrideAnalyzer()))
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult()
            .OrderBy(d => d.Location.SourceSpan.Start).ToImmutableArray();
    }

    private const string Template = """
        using System.Drawing;
        using System.Windows.Forms;
        using FormWind;

        class Sample : Form
        {
            private TailwindProvider tw = new TailwindProvider();
            private Button btnSave = new Button();
            private Panel pnl = new Panel();

            void Init(string dynamic)
            {
        BODY
            }
        }
        """;

    private static ImmutableArray<Diagnostic> AnalyzeBody(string body) => Analyze(Template.Replace("BODY", body));

    [Fact]
    public void Reports_assignment_to_a_property_managed_by_classes()
    {
        var diagnostics = AnalyzeBody("""
                    this.tw.SetClasses(this.btnSave, "btn-primary hover:bg-blue-700");
                    this.btnSave.BackColor = Color.Red;
                    btnSave.ForeColor = Color.White;
            """);
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("FW001", d.Id));
        Assert.Contains("BackColor", diagnostics[0].GetMessage());
        Assert.Contains("btnSave", diagnostics[0].GetMessage());
    }

    [Fact]
    public void Ignores_properties_the_classes_do_not_touch()
    {
        var diagnostics = AnalyzeBody("""
                    tw.SetClasses(pnl, "flex items-center gap-2 border-b header:bg-gray-50");
                    pnl.Padding = new Padding(8);          // class'lar padding vermiyor
                    pnl.BackColor = Color.Red;             // header: parçası panelin zemini değil
                    tw.SetClasses(this, "font-sans font-semibold bg-white");
                    this.Font = new Font("Segoe UI", 9f);  // aile/kalınlık temel fontun boyutunu kullanır
                    btnSave.BackColor = Color.Red;         // class'ı yok
            """);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Font_size_and_dynamic_classes()
    {
        Assert.Single(AnalyzeBody("""
                    tw.SetClasses(this, "text-sm " + "bg-white");
                    this.Font = new Font("Segoe UI", 9f);
            """));
        Assert.Empty(AnalyzeBody("""
                    tw.SetClasses(pnl, dynamic);           // sabit değil: bilinemez
                    pnl.BackColor = Color.Red;
            """));
    }

    [Theory]
    [InlineData("bg-white dark:hover:bg-gray-800", "BackColor")]
    [InlineData("text-gray-900", "ForeColor")]
    [InlineData("text-right", "")]
    [InlineData("text-[13px]", "Font")]
    [InlineData("px-3 -mt-2", "Padding,Margin")]
    [InlineData("card", "BackColor,Padding")]
    [InlineData("item:hover:bg-gray-100 caption:bg-white", "")]
    public void Maps_classes_to_managed_properties(string classes, string expected) =>
        Assert.Equal(expected, string.Join(",", StyleOverrideAnalyzer.ManagedProperties(classes).Distinct()));

    private sealed class TestOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly Options _options;

        public TestOptionsProvider(string targetFramework) => _options = new Options(targetFramework);

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;

        private sealed class Options : AnalyzerConfigOptions
        {
            private readonly string _targetFramework;

            public Options(string targetFramework) => _targetFramework = targetFramework;

            public override bool TryGetValue(string key, out string value)
            {
                value = _targetFramework;
                return key == "build_property.TargetFramework";
            }
        }
    }

    // Designer'ın .NET Framework formu: font AutoScaleDimensions'tan sonra.
    private const string FontModeForm = """
        using System.Drawing;
        using System.Windows.Forms;
        using FormWind;

        partial class OrderForm : Form
        {
            private TailwindProvider tailwindProvider1 = new TailwindProvider();
            private Button btnSave = new Button();

            private void InitializeComponent()
            {
                this.tailwindProvider1.SetClasses(this.btnSave, "btn-primary");
                this.btnSave.Font = new Font("Segoe UI", 10F);
                EARLY
                this.AutoScaleDimensions = new SizeF(7F, 15F);
                this.AutoScaleMode = AutoScaleMode.MODE;
                this.ClientSize = new Size(800, 450);
                LATE
            }
        }
        """;

    private const string FormFont = "this.Font = new Font(\"Segoe UI\", 9F);";

    private static string FontModeSource(string mode, bool fontFirst = false) => FontModeForm
        .Replace("MODE", mode).Replace("EARLY", fontFirst ? FormFont : "").Replace("LATE", fontFirst ? "" : FormFont);

    internal static ImmutableArray<Diagnostic> AnalyzeFontScaling(string source, string targetFramework)
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = platform.Select(path => MetadataReference.CreateFromFile(path))
            .Concat(new[] { MetadataReference.CreateFromFile(typeof(Control).Assembly.Location), MetadataReference.CreateFromFile(typeof(TailwindProvider).Assembly.Location) })
            .GroupBy(r => Path.GetFileName(r.FilePath)).Select(g => g.First());
        var compilation = CSharpCompilation.Create("Sample", new[] { CSharpSyntaxTree.ParseText(source) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new TestOptionsProvider(targetFramework));
        return compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new FontScalingAnalyzer()), options)
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void Reports_font_assigned_after_auto_scale_dimensions_on_net_framework()
    {
        var diagnostic = Assert.Single(AnalyzeFontScaling(FontModeSource("Font"), "net472"));
        Assert.Equal("FW002", diagnostic.Id);
        Assert.Contains("OrderForm", diagnostic.GetMessage());
        Assert.Equal(FormFont.TrimEnd(';'), diagnostic.Location.SourceTree!.ToString().Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    [Theory]
    [InlineData("Dpi", "net472")]          // Dpi modu etkilenmez
    [InlineData("Font", "net8.0-windows")]  // .NET (Core) WinForms doğru ölçekler
    public void Ignores_dpi_mode_and_modern_dotnet(string mode, string targetFramework) =>
        Assert.Empty(AnalyzeFontScaling(FontModeSource(mode), targetFramework));

    [Fact]
    public void Ignores_font_assigned_before_auto_scale_dimensions() =>
        Assert.Empty(AnalyzeFontScaling(FontModeSource("Font", fontFirst: true), "net472"));

    private static ImmutableArray<Diagnostic> AnalyzeIcons(string body)
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = platform.Select(path => MetadataReference.CreateFromFile(path))
            .Concat(new[] { MetadataReference.CreateFromFile(typeof(Control).Assembly.Location), MetadataReference.CreateFromFile(typeof(TailwindProvider).Assembly.Location) })
            .GroupBy(r => Path.GetFileName(r.FilePath)).Select(g => g.First());
        var source = "using FormWind;\nclass Sample { void Init() {\n" + body + "\n} }";
        var compilation = CSharpCompilation.Create("Sample", new[] { CSharpSyntaxTree.ParseText(source) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        return compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new UnknownIconAnalyzer()))
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void Reports_unknown_icon_names_but_not_known_registered_or_code_points()
    {
        var diagnostics = AnalyzeIcons("""
            new TwIcon().IconName = "sidebar";
            new TwButton().IconName = "save";
            new TwButton { IconName = "#E710" };
            new TwChip { IconName = "Plus" };
            TwIcons.Register("gauge", (char)0xEC4A);
            new TwIcon { IconName = "gauge" };
            new TwChip { IconName = "add" };
            """);
        Assert.Equal(new[] { "FW003", "FW003" }, diagnostics.Select(d => d.Id).ToArray());
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("'sidebar'"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("'add'"));
    }

    [Fact]
    public void Icon_analyzer_knows_every_built_in_icon() =>
        Assert.Equal(TwIcons.Names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase),
            UnknownIconAnalyzer.KnownIcons.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
    private static ImmutableArray<Diagnostic> AnalyzeClasses(string body)
    {
        var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        var references = platform.Select(path => MetadataReference.CreateFromFile(path))
            .Concat(new[] { MetadataReference.CreateFromFile(typeof(Control).Assembly.Location), MetadataReference.CreateFromFile(typeof(TailwindProvider).Assembly.Location) })
            .GroupBy(r => Path.GetFileName(r.FilePath)).Select(g => g.First());
        var source = "using System.Windows.Forms;\nusing FormWind;\nclass Sample { void Init(TailwindProvider tw, Button button, DataGridView grid, DataGridViewColumn column, string dynamic) {\n" + body + "\n} }";
        var compilation = CSharpCompilation.Create("Sample", new[] { CSharpSyntaxTree.ParseText(source) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        return compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MalformedClassAnalyzer()))
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void Reports_arbitrary_values_without_their_unit()
    {
        var diagnostics = AnalyzeClasses("""
            tw.SetClasses(button, "px-4 duration-[120] hover:w-[80] delay-[50ms] h-[40px]");
            """);
        Assert.Equal(new[] { "FW004", "FW004" }, diagnostics.Select(d => d.Id).ToArray());
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("'duration-[120]'") && d.GetMessage().Contains("did you mean 'duration-[120ms]'"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("'hover:w-[80]'") && d.GetMessage().Contains("did you mean 'hover:w-[80px]'"));
        // Konum yalnızca sorunlu sınıfı kapsar.
        var first = diagnostics.OrderBy(d => d.Location.SourceSpan.Start).First();
        Assert.Equal("duration-[120]", first.Location.SourceTree!.ToString().Substring(first.Location.SourceSpan.Start, first.Location.SourceSpan.Length));
    }

    [Theory]
    [InlineData("w-[120px", "brackets are not balanced")]
    [InlineData("w-120px]", "brackets are not balanced")]
    [InlineData("duration-[120px]", "unit must be 'ms'")]
    [InlineData("w-[120em]", "unit must be 'px'")]
    [InlineData("w-[12.5px]", "only whole numbers")]
    [InlineData("w-[auto]", "followed by 'px'")]
    [InlineData("bg-[ff0000]", "did you mean 'bg-[#ff0000]'")]
    [InlineData("text-[red]", "followed by 'px'")]
    [InlineData("bg-[red]", "hex color")]
    [InlineData("grid-cols-[1fr_2fr]", "supported only for")]
    [InlineData("w-[12]px", "must wrap the whole value")]
    public void Reports_malformed_arbitrary_values(string token, string expected)
    {
        var diagnostic = Assert.Single(AnalyzeClasses($"tw.SetClasses(button, \"flex {token}\");"));
        Assert.Equal("FW004", diagnostic.Id);
        Assert.Contains(expected, diagnostic.GetMessage());
        Assert.Contains($"'{token}'", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("duration-[120ms] delay-[50ms] w-[120px] min-h-[10px] p-[8px] gap-x-[4px] rounded-[6px]")]
    [InlineData("text-[13px] text-[13.5px] text-[#2563eb] bg-[#fff] border-[#112233]/50 hover:bg-[#ff0000]")]
    [InlineData("bg-blu-600 no-such-class")]          // adlar denetlenmez: tema renk ve bileşen ekleyebilir
    public void Accepts_valid_arbitrary_values_and_leaves_names_to_the_theme(string classes) =>
        Assert.Empty(AnalyzeClasses($"tw.SetClasses(button, \"{classes}\");"));

    [Fact]
    public void Checks_the_other_class_taking_apis_but_only_literals()
    {
        var diagnostics = AnalyzeClasses("""
            tw.SetColumnClasses(column, "w-[100]");
            tw.SetRowClasses(grid, row => row.Index % 2 == 0 ? "bg-gray-50" : "bg-[#fff] h-[20]");
            new TwToolTip().Classes = "p-[4]";
            new TwListItem().BadgeClasses = "px-[2]";
            tw.SetClasses(button, dynamic);
            tw.SetClasses(button, "w-[" + dynamic + "]");
            tw.SetClasses(button, null);
            """);
        Assert.Equal(4, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("FW004", d.Id));
        foreach (var token in new[] { "'w-[100]'", "'h-[20]'", "'p-[4]'", "'px-[2]'" })
            Assert.Contains(diagnostics, d => d.GetMessage().Contains(token));
    }

    [Fact]
    public void Ignores_methods_with_the_same_name_on_other_types() =>
        Assert.Empty(AnalyzeClasses("""
            void SetClasses(Control control, string value) { }
            SetClasses(button, "w-[120]");
            """));
}
#endif
