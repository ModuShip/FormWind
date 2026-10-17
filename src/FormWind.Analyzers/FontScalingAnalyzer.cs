using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FormWind.Analyzers;

/// <summary>
/// FW002: on .NET Framework, the <c>Font</c> of a form set up with <c>AutoScaleMode.Font</c> is assigned after <c>AutoScaleDimensions</c>
/// in <c>InitializeComponent</c> (the Designer writes it in this order). When the application runs as PerMonitorV2, WinForms skips
/// scaling for this font assignment: controls stay at their 96 DPI size while fonts and FormWind spacing grow with the screen DPI,
/// and text gets clipped. .NET (Core) WinForms does not have this problem.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FontScalingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FW002";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        "Font assigned after AutoScaleDimensions on an AutoScaleMode.Font form",
        "'{0}' uses AutoScaleMode.Font and assigns Font after AutoScaleDimensions; on .NET Framework with PerMonitorV2 the form is not scaled. Use AutoScaleMode.Dpi (AutoScaleDimensions = 96, 96).",
        "FormWind",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "With PerMonitorV2, .NET Framework WinForms does not scale the layout for a font changed after AutoScaleDimensions; since FormWind scales fonts and spacing with DPI, text gets clipped.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName("FormWind.TailwindProvider") is null) return;
            if (!IsNetFramework(start.Compilation, start.Options)) return;
            start.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
        });
    }

    // Proje özelliği (build_property.TargetFramework = net472) ya da referans verilen WinForms'un sürümü (.NET Framework'te 4.x).
    private static bool IsNetFramework(Compilation compilation, AnalyzerOptions options)
    {
        if (options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.TargetFramework", out var framework)
            && !string.IsNullOrEmpty(framework))
            return framework.StartsWith("net4", System.StringComparison.OrdinalIgnoreCase);
        var winForms = compilation.ReferencedAssemblyNames.FirstOrDefault(a => a.Name == "System.Windows.Forms");
        return winForms != null && winForms.Version.Major == 4;
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (method.Identifier.ValueText != "InitializeComponent" || method.Body is null) return;

        int dimensions = -1;
        bool fontMode = false, usesFormWind = false;
        AssignmentExpressionSyntax? lateFont = null;
        foreach (var node in method.Body.DescendantNodes())
        {
            if (node is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "SetClasses" } })
                usesFormWind = true;
            if (node is not AssignmentExpressionSyntax assignment || OwnProperty(assignment.Left) is not string property) continue;
            switch (property)
            {
                case "AutoScaleDimensions":
                    dimensions = assignment.SpanStart;
                    break;
                case "AutoScaleMode":
                    fontMode = assignment.Right is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Font" };
                    break;
                case "Font":
                    if (dimensions >= 0 && assignment.SpanStart > dimensions) lateFont ??= assignment;
                    break;
            }
        }

        if (fontMode && usesFormWind && lateFont != null)
        {
            var type = method.FirstAncestorOrSelf<TypeDeclarationSyntax>()?.Identifier.ValueText ?? "Form";
            context.ReportDiagnostic(Diagnostic.Create(Rule, lateFont.GetLocation(), type));
        }
    }

    // Formun kendi özelliği: "this.Font" ya da "Font" (yeni Designer "this." yazmaz); "btnSave.Font" sayılmaz.
    private static string? OwnProperty(ExpressionSyntax left) => left switch
    {
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access => access.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        _ => null,
    };
}
