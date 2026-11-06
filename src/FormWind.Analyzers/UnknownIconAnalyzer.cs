using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FormWind.Analyzers;

/// <summary>
/// FW003: a string literal assigned to <c>IconName</c> (<c>TwIcon</c>, <c>TwButton</c>, <c>TwChip</c> …) that is neither a
/// built-in icon nor a code point (<c>#E710</c>) nor registered in the project with <c>TwIcons.Register("name", …)</c>.
/// An unknown name draws an empty icon without an error.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnknownIconAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FW003";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        "Unknown icon name",
        "'{0}' is not a FormWind icon; the icon is drawn empty. Use a name from TwIcons.Names, a code point (\"#E710\") or register it with TwIcons.Register.",
        "FormWind",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "FormWind draws nothing for an icon name it does not know. Names added only in a theme file (icons) are not seen by the analyzer; suppress the warning for those.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>The built-in names of <c>TwIcons</c> (kept equal by a test).</summary>
    internal static readonly ImmutableHashSet<string> KnownIcons = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase,
        "arrow-down", "arrow-left", "arrow-right", "arrow-up", "attach", "bell", "calculator", "calendar", "camera", "cart",
        "check", "chevron-down", "chevron-left", "chevron-right", "chevron-up", "clock", "copy", "cut", "download", "edit",
        "error", "external-link", "eye", "eye-off", "file", "filter", "flag", "folder", "folder-open", "fullscreen", "globe",
        "grid", "heart", "heart-filled", "help", "history", "home", "image", "info", "key", "link", "list", "location", "lock",
        "mail", "map-pin", "menu", "minus", "moon", "more", "paste", "phone", "pin", "plus", "power", "print", "redo", "refresh",
        "save", "search", "send", "settings", "share", "shield", "sort", "star", "star-filled", "sun", "sync", "tag", "trash",
        "undo", "unlock", "upload", "user", "users", "warning", "x", "zoom-in", "zoom-out",
        "log-out", "sign-out", "sidebar", "panel-right", "wrench", "tool", "gauge", "paperclip",
        "bookmark", "bookmark-filled", "factory");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName("FormWind.TwIcons") is not INamedTypeSymbol icons) return;
            var registered = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var candidates = new ConcurrentBag<(string Name, Location Location)>();

            start.RegisterSyntaxNodeAction(node =>
            {
                var invocation = (InvocationExpressionSyntax)node.Node;
                if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Register" }) return;
                if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax literal
                    || !literal.IsKind(SyntaxKind.StringLiteralExpression)) return;
                if (node.SemanticModel.GetSymbolInfo(invocation, node.CancellationToken).Symbol is IMethodSymbol method
                    && SymbolEqualityComparer.Default.Equals(method.ContainingType, icons))
                    registered[literal.Token.ValueText] = true;
            }, SyntaxKind.InvocationExpression);

            start.RegisterSyntaxNodeAction(node =>
            {
                var assignment = (AssignmentExpressionSyntax)node.Node;
                if (assignment.Right is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.StringLiteralExpression)) return;
                if (node.SemanticModel.GetSymbolInfo(assignment.Left, node.CancellationToken).Symbol is not IPropertySymbol
                    { Name: "IconName" } property) return;
                if (property.ContainingType.ContainingNamespace?.ToDisplayString() != "FormWind") return;
                var name = literal.Token.ValueText;
                if (!IsKnown(name)) candidates.Add((name, literal.GetLocation()));
            }, SyntaxKind.SimpleAssignmentExpression);

            start.RegisterCompilationEndAction(end =>
            {
                foreach (var (name, location) in candidates)
                    if (!registered.ContainsKey(name)) end.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
            });
        });
    }

    internal static bool IsKnown(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || KnownIcons.Contains(name)) return true;
        // Kod noktası yalnızca "#E710" biçiminde çizilir (TwIcons.TryGetGlyph).
        if (!name.StartsWith("#", StringComparison.Ordinal)) return false;
        var hex = name.Substring(1).Trim();
        if (hex.StartsWith("U+", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);
        return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) && code > 0 && code <= 0xFFFF;
    }
}
