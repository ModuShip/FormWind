using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FormWind.Analyzers;

/// <summary>
/// FW001: code assigns a property that classes given with <c>TailwindProvider.SetClasses</c> manage (e.g. <c>bg-*</c> → BackColor).
/// Classes are reapplied on every state change (hover, dark mode), so the assigned value either gets overwritten or overrides
/// the class. Only calls with a constant (literal) class string are checked; assignments to properties the classes do not touch
/// (e.g. a plain <c>Font</c> while <c>font-sans</c> is set) do not warn.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StyleOverrideAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FW001";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        "Style property assigned on a control with classes",
        "'{0}' has classes; assigning '{1}' conflicts with them. Express the value as a class (e.g. {2}) or remove the assignment.",
        "FormWind",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "TailwindProvider reapplies classes as the state changes; a value assigned to the same property in code does not persist.");

    // Class'larla yönetilen özellikler ve karşılık gelen class örneği.
    internal static readonly IReadOnlyDictionary<string, string> StyleProperties = new Dictionary<string, string>
    {
        ["BackColor"] = "bg-*",
        ["ForeColor"] = "text-*",
        ["Font"] = "text-sm",
        ["Padding"] = "p-*",
        ["Margin"] = "m-*",
        ["Cursor"] = "cursor-pointer",
    };

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var provider = start.Compilation.GetTypeByMetadataName("FormWind.TailwindProvider");
            if (provider is null) return;

            // Tür başına: class verilen kontroller (alan/yerel değişken sembolleri) ve stil atamaları.
            start.RegisterSymbolStartAction(symbolStart =>
            {
                var classed = new ConcurrentDictionary<ISymbol, ConcurrentDictionary<string, byte>>(SymbolEqualityComparer.Default);
                var assignments = new ConcurrentBag<(ISymbol Target, string Property, Location Location)>();

                symbolStart.RegisterSyntaxNodeAction(node =>
                {
                    var invocation = (InvocationExpressionSyntax)node.Node;
                    if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "SetClasses" } access) return;
                    if (node.SemanticModel.GetSymbolInfo(access, node.CancellationToken).Symbol is not IMethodSymbol method
                        || !SymbolEqualityComparer.Default.Equals(method.ContainingType, provider)) return;
                    var arguments = invocation.ArgumentList.Arguments;
                    if (arguments.Count < 2) return;
                    // Sabit olmayan class metni bilinemez; o çağrı için uyarı verilmez.
                    if (node.SemanticModel.GetConstantValue(arguments[1].Expression, node.CancellationToken).Value is not string classes) return;
                    if (Target(node.SemanticModel, arguments[0].Expression, node.CancellationToken) is not ISymbol target) return;
                    var properties = classed.GetOrAdd(target, _ => new ConcurrentDictionary<string, byte>());
                    foreach (var property in ManagedProperties(classes)) properties.TryAdd(property, 0);
                }, SyntaxKind.InvocationExpression);

                symbolStart.RegisterSyntaxNodeAction(node =>
                {
                    var assignment = (AssignmentExpressionSyntax)node.Node;
                    if (assignment.Left is not MemberAccessExpressionSyntax left) return;
                    var property = left.Name.Identifier.ValueText;
                    if (!StyleProperties.ContainsKey(property)) return;
                    if (node.SemanticModel.GetSymbolInfo(left, node.CancellationToken).Symbol is not IPropertySymbol symbol
                        || !InheritsControl(symbol.ContainingType)) return;
                    if (Target(node.SemanticModel, left.Expression, node.CancellationToken) is ISymbol target)
                        assignments.Add((target, property, assignment.GetLocation()));
                }, SyntaxKind.SimpleAssignmentExpression);

                symbolStart.RegisterSymbolEndAction(end =>
                {
                    foreach (var (target, property, location) in assignments)
                        if (classed.TryGetValue(target, out var managed) && managed.ContainsKey(property))
                            end.ReportDiagnostic(Diagnostic.Create(Rule, location, target.Name, property, StyleProperties[property]));
                });
            }, SymbolKind.NamedType);
        });
    }

    private static readonly Regex TextColorRx = new Regex(@"^text-(white|black|transparent|current|\[#[0-9a-fA-F]{3,6}\]|[a-z]+-\d{2,3})(/\d+)?$");
    // Yalnızca boyut: aile (font-sans) ve kalınlık class'ları temel fontun boyutunu kullanır, onunla birlikte çalışır.
    private static readonly Regex FontRx = new Regex(@"^text-(xs|sm|base|lg|xl|\dxl|\[[\d.]+px\])$");

    // Varsayılan bileşenlerin yönettiği özellikler (tema dosyasındaki özel bileşenler bilinmez).
    private static readonly Dictionary<string, string[]> Components = new Dictionary<string, string[]>
    {
        ["btn"] = new[] { "Padding", "Font", "Cursor" },
        ["btn-primary"] = new[] { "BackColor", "ForeColor", "Padding", "Font", "Cursor" },
        ["btn-ghost"] = new[] { "BackColor", "ForeColor", "Padding", "Font", "Cursor" },
        ["btn-danger"] = new[] { "BackColor", "ForeColor", "Padding", "Font", "Cursor" },
        ["card"] = new[] { "BackColor", "Padding" },
        ["input"] = new[] { "BackColor", "ForeColor", "Padding" },
        ["table"] = new[] { "BackColor", "ForeColor", "Font" },
    };

    /// <summary>Control properties managed by a class string (variants included: <c>hover:bg-*</c> is BackColor too).</summary>
    internal static IEnumerable<string> ManagedProperties(string classes)
    {
        foreach (var token in classes.Split(new[] { ' ', '\t', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = token.LastIndexOf(':');
            var utility = colon >= 0 ? token.Substring(colon + 1) : token;
            // Parça önekli class'lar (header:, item:, menu: …) kontrolün kendi özelliklerini değiştirmez.
            if (colon >= 0 && IsPart(token.Substring(0, colon))) continue;
            if (Components.TryGetValue(utility, out var properties))
            {
                foreach (var property in properties) yield return property;
                continue;
            }
            if (utility.StartsWith("bg-", System.StringComparison.Ordinal) && !utility.StartsWith("bg-gradient", System.StringComparison.Ordinal)) yield return "BackColor";
            else if (TextColorRx.IsMatch(utility)) yield return "ForeColor";
            else if (FontRx.IsMatch(utility)) yield return "Font";
            else if (Regex.IsMatch(utility, @"^p[xytrbl]?-")) yield return "Padding";
            else if (Regex.IsMatch(utility, @"^-?m[xytrbl]?-")) yield return "Margin";
            else if (utility.StartsWith("cursor-", System.StringComparison.Ordinal)) yield return "Cursor";
        }
    }

    private static bool IsPart(string prefix)
    {
        foreach (var segment in prefix.Split(':'))
            if (segment is "header" or "cell" or "alt" or "selected" or "row" or "row-header" or "item" or "menu" or "caption"
                or "placeholder" or "label" or "hint" or "error") return true;
        return false;
    }

    // "this.btnSave", "btnSave", yerel değişken ya da "this" (form) → sembol.
    private static ISymbol? Target(SemanticModel model, ExpressionSyntax expression, System.Threading.CancellationToken token)
    {
        if (expression is ThisExpressionSyntax) return model.GetEnclosingSymbol(expression.SpanStart, token)?.ContainingType;
        var symbol = model.GetSymbolInfo(expression, token).Symbol;
        return symbol is IFieldSymbol or ILocalSymbol or IPropertySymbol or IParameterSymbol ? symbol : null;
    }

    private static bool InheritsControl(INamedTypeSymbol? type)
    {
        for (var current = type; current != null; current = current.BaseType)
            if (current.ToDisplayString() == "System.Windows.Forms.Control") return true;
        return false;
    }
}
