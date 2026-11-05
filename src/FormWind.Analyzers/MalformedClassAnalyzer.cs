using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FormWind.Analyzers;

/// <summary>
/// FW004: a malformed class in a string literal given to <c>TailwindProvider.SetClasses</c>, <c>SetColumnClasses</c>,
/// <c>SetRowClasses</c> or assigned to a <c>Classes</c> property of FormWind. The analyzer cannot see the theme (colors,
/// components and fonts can be added by a theme file), so it does not judge names; it checks what is decidable without it:
/// arbitrary values (<c>duration-[120]</c>) must have their unit (<c>duration-[120ms]</c>, <c>w-[120px]</c>), colors must be
/// hex (<c>bg-[#ff0000]</c>), brackets must balance, and only the classes that take arbitrary values may use them. Such a
/// class is ignored at run time and reported through <c>TailwindProvider.UnknownClasses</c>, not <c>StyleError</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MalformedClassAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "FW004";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        "Malformed class",
        "Class '{0}' is ignored: {1}",
        "FormWind",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "FormWind ignores a class with a malformed arbitrary value (a missing unit, a wrong unit, unbalanced brackets) and reports it through TailwindProvider.UnknownClasses. Misspelled class names are not checked here because the theme can add colors and components; they are reported at run time with a suggestion.");

    // Keyfi değer alan class'lar ve değerin birimi (TwParser'daki düzenli ifadelerle uyumlu).
    private static readonly Regex ArbitraryRx = new Regex(
        @"^(?<name>duration|delay|text|rounded|(?:min-|max-)?[wh]|[pm][xytrbl]?|gap(?:-[xy])?|basis|bg|border|divide|ring-offset|ring|shadow|from|via|to|fill|accent)-\[(?<inner>[^\[\]]*)\](?<opacity>/\d{1,3})?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex NumberRx = new Regex(@"^(?<number>\d+(?:\.\d+)?)\s*(?<unit>[A-Za-z%]*)$", RegexOptions.CultureInvariant);
    private static readonly Regex HexRx = new Regex(@"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.CultureInvariant);
    private static readonly Regex BareHexRx = new Regex(@"^(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.CultureInvariant);

    private static readonly ImmutableHashSet<string> ColorNames = ImmutableHashSet.Create(
        "bg", "border", "divide", "ring-offset", "ring", "shadow", "from", "via", "to", "fill", "accent");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName("FormWind.TailwindProvider") is not INamedTypeSymbol provider) return;

            start.RegisterSyntaxNodeAction(node =>
            {
                var invocation = (InvocationExpressionSyntax)node.Node;
                var name = invocation.Expression switch
                {
                    MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    _ => "",
                };
                if (name is not ("SetClasses" or "SetColumnClasses" or "SetRowClasses")) return;
                if (node.SemanticModel.GetSymbolInfo(invocation, node.CancellationToken).Symbol is not IMethodSymbol method
                    || !SymbolEqualityComparer.Default.Equals(method.ContainingType, provider)) return;

                // SetRowClasses bir işlev alır: içindeki metinler de class'tır.
                foreach (var argument in invocation.ArgumentList.Arguments)
                    foreach (var literal in Literals(argument.Expression))
                        Check(node, literal);
            }, SyntaxKind.InvocationExpression);

            start.RegisterSyntaxNodeAction(node =>
            {
                var assignment = (AssignmentExpressionSyntax)node.Node;
                if (assignment.Right is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.StringLiteralExpression)) return;
                var propertyName = assignment.Left switch
                {
                    MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    _ => "",
                };
                if (!propertyName.EndsWith("Classes", StringComparison.Ordinal)) return;
                if (node.SemanticModel.GetSymbolInfo(assignment.Left, node.CancellationToken).Symbol is not IPropertySymbol property
                    || property.ContainingType.ContainingNamespace?.ToDisplayString() != "FormWind") return;
                Check(node, literal);
            }, SyntaxKind.SimpleAssignmentExpression);
        });
    }

    private static IEnumerable<LiteralExpressionSyntax> Literals(ExpressionSyntax expression) =>
        expression.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>()
            .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression) && l.Parent is not BinaryExpressionSyntax);   // "w-[" + x + "]": parçalar tek başına anlam taşımaz

    private static void Check(SyntaxNodeAnalysisContext context, LiteralExpressionSyntax literal)
    {
        var value = literal.Token.ValueText;
        // Kaçış dizisi yoksa sorunlu sınıfın yeri metnin içinde bulunur; yoksa bütün metin işaretlenir.
        bool exact = literal.Token.Text.Length == value.Length + 2 && literal.Token.Text[0] == '"';
        foreach (Match token in Regex.Matches(value, @"\S+"))
        {
            var problem = Problem(token.Value);
            if (problem is null) continue;
            var location = exact
                ? Location.Create(literal.SyntaxTree, new Microsoft.CodeAnalysis.Text.TextSpan(literal.Token.SpanStart + 1 + token.Index, token.Length))
                : literal.GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, token.Value, problem));
        }
    }

    /// <summary>Why <paramref name="token"/> (one class, variants included) is malformed, or <c>null</c> when it may be fine.</summary>
    internal static string? Problem(string token)
    {
        int separator = token.LastIndexOf(':');
        var prefix = separator > 0 ? token.Substring(0, separator + 1) : "";
        var utility = separator > 0 ? token.Substring(separator + 1) : token;
        if (utility.IndexOf('[') < 0 && utility.IndexOf(']') < 0) return null;

        int opening = utility.Count(c => c == '['), closing = utility.Count(c => c == ']');
        if (opening != closing) return "the brackets are not balanced.";

        var match = ArbitraryRx.Match(utility);
        if (!match.Success)
        {
            if (utility.IndexOf("-[", StringComparison.Ordinal) < 0 || !utility.EndsWith("]", StringComparison.Ordinal))
                return "the brackets must wrap the whole value, as in 'w-[120px]'.";
            return "arbitrary values are supported only for duration, delay, text size and color, rounded, width, height, padding, margin, gap, basis and colors.";
        }

        var name = match.Groups["name"].Value;
        var inner = match.Groups["inner"].Value.Trim();
        var opacity = match.Groups["opacity"].Value;
        bool isColor = ColorNames.Contains(name) || name == "text" && inner.StartsWith("#", StringComparison.Ordinal);

        if (isColor)
        {
            if (HexRx.IsMatch(inner)) return null;
            if (BareHexRx.IsMatch(inner)) return $"colors need a '#': did you mean '{prefix}{name}-[#{inner}]{opacity}'?";
            return $"a color value must be a hex color such as '{name}-[#2563eb]'.";
        }

        string unit = name is "duration" or "delay" ? "ms" : "px";
        var number = NumberRx.Match(inner);
        if (!number.Success) return $"the value must be a number followed by '{unit}', as in '{name}-[{(unit == "ms" ? "120" : "16")}{unit}]'.";
        var digits = number.Groups["number"].Value;
        var given = number.Groups["unit"].Value;
        bool decimals = digits.IndexOf('.') >= 0;
        if (given == unit && !(decimals && name != "text")) return null;
        if (decimals && name != "text") return $"only whole numbers are supported here: did you mean '{prefix}{name}-[{Math.Round(double.Parse(digits, CultureInfo.InvariantCulture))}{unit}]'?";
        var fix = $"{prefix}{name}-[{digits}{unit}]";
        return given.Length == 0 ? $"the unit is missing: did you mean '{fix}'?" : $"the unit must be '{unit}': did you mean '{fix}'?";
    }
}
