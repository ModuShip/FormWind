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
}
#endif
