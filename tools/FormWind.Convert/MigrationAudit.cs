using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FormWind.Convert;

/// <summary>Taramanın bulduğu yer türleri; rapor bu sırayla yazılır.</summary>
public enum AuditCategory
{
    /// <summary>Kodda çalışma anında atanan BackColor/ForeColor.</summary>
    RuntimeColor,
    /// <summary>ListViewItem, ListViewSubItem ve TreeNode renkleri.</summary>
    ItemColor,
    /// <summary>DataGridView hücre stillerinin kodda atanan renkleri.</summary>
    GridCellStyle,
    /// <summary>Kendi çizen kontroller (OwnerDraw, DrawItem, CellPainting …).</summary>
    OwnerDraw,
    /// <summary>Görselli, sabit boyutlu butonlar.</summary>
    ImageButton,
    /// <summary>SetWindowTheme çağrıları.</summary>
    WindowTheme,
    /// <summary>RichTextBox'a RTF yükleme.</summary>
    Rtf,
}

public sealed record AuditFinding(AuditCategory Category, string File, int Line, string Code, string Advice);

/// <summary>
/// Geçiş öncesi tarama: bir uygulamayı FormWind'e geçirmeden önce elle bakılması gereken yerleri kaynak koddan bulur
/// (Designer dışı kod dahil). Roslyn'in yalnızca sözdizimi ağacı kullanılır; derleme ya da başvuru gerekmez. Tipler alan ve
/// yerel değişken bildirimlerinden çözülür (Designer'daki <c>private Button btnSave;</c> gibi).
/// </summary>
public static class MigrationAudit
{
    private static readonly string[] ColorProperties = { "BackColor", "ForeColor" };
    private static readonly string[] DrawEvents = { "DrawItem", "DrawNode", "DrawSubItem", "DrawColumnHeader", "CellPainting", "RowPrePaint", "RowPostPaint", "MeasureItem" };
    private static readonly string[] ItemTypes = { "ListViewItem", "ListViewSubItem", "TreeNode" };

    /// <summary>Klasördeki (ya da tek dosyadaki) C# kaynaklarını tarar; <c>bin</c> ve <c>obj</c> atlanır.</summary>
    public static IReadOnlyList<AuditFinding> AuditPath(string target)
    {
        var files = Directory.Exists(target)
            ? Directory.EnumerateFiles(target, "*.cs", SearchOption.AllDirectories)
                .Where(f => !IsBuildOutput(f)).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray()
            : File.Exists(target) ? new[] { target } : Array.Empty<string>();
        var root = Directory.Exists(target) ? Path.GetFullPath(target) : Path.GetDirectoryName(Path.GetFullPath(target))!;
        return Audit(files.Select(f => (Relative(root, f), SafeRead(f))));
    }

    /// <summary>Kaynakları tarar: (dosya adı, içerik).</summary>
    public static IReadOnlyList<AuditFinding> Audit(IEnumerable<(string File, string Source)> sources)
    {
        var trees = sources.Select(s => (s.File, Tree: CSharpSyntaxTree.ParseText(s.Source))).ToList();
        var fieldTypes = FieldTypes(trees.Select(t => t.Tree));
        var findings = new List<AuditFinding>();
        var themeWrappers = ThemeWrappers(trees.Select(t => t.Tree));

        foreach (var (file, tree) in trees)
        {
            var root = tree.GetRoot();
            bool designer = file.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase);
            int Line(SyntaxNode node) => tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
            void Add(AuditCategory category, SyntaxNode node, string advice) =>
                findings.Add(new AuditFinding(category, file, Line(node), Snippet(node), advice));

            foreach (var method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
            {
                if (designer && method is MethodDeclarationSyntax { Identifier.ValueText: "InitializeComponent" }) continue;
                var locals = LocalTypes(method);
                string? TypeOf(ExpressionSyntax expression)
                {
                    var name = LastName(expression);
                    if (name is null) return null;
                    return locals.TryGetValue(name, out var local) ? local : fieldTypes.TryGetValue(name, out var field) ? field : null;
                }

                foreach (var assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                {
                    if (assignment.Left is not MemberAccessExpressionSyntax left) continue;
                    var property = left.Name.Identifier.ValueText;
                    if (Array.IndexOf(ColorProperties, property) >= 0 && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
                    {
                        var receiverType = TypeOf(left.Expression);
                        var receiverName = LastName(left.Expression) ?? "";
                        if (receiverType != null && ItemTypes.Contains(receiverType))
                            Add(AuditCategory.ItemColor, assignment, ItemAdvice(assignment.Right));
                        else if (receiverType == "DataGridViewCellStyle" || receiverName.EndsWith("CellStyle", StringComparison.Ordinal) || receiverName == "Style")
                            Add(AuditCategory.GridCellStyle, assignment, "Hücre stili kodda boyanıyor; koşullu renkler için `provider.SetRowClasses(grid, row => …)` ya da `cell:`/`row:` class'ları, koyu temaya uyan renk için `provider.GetColors(…)`.");
                        else
                            Add(AuditCategory.RuntimeColor, assignment, ColorAdvice(assignment.Right, property));
                    }
                    else if (property is "Rtf" or "SelectedRtf")
                        Add(AuditCategory.Rtf, assignment, "RTF olarak yüklenen renksiz metin otomatik renk alır; class'lar yazı rengi verdiğinde FormWind onu kutunun yazı rengine çevirir. RTF'de sabit renkler (\\cf) varsa koyu temada kontrol edin.");
                    else if (property == "OwnerDraw" && assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression)
                             || property == "DrawMode" && assignment.Right.ToString().Contains("OwnerDraw"))
                        Add(AuditCategory.OwnerDraw, assignment, OwnerDrawAdvice);
                    else if (Array.IndexOf(DrawEvents, property) >= 0 && assignment.IsKind(SyntaxKind.AddAssignmentExpression))
                        Add(AuditCategory.OwnerDraw, assignment, OwnerDrawAdvice);
                }

                foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    var name = InvokedName(invocation);
                    if (name == "SetWindowTheme" || name != null && themeWrappers.Contains(name))
                        Add(AuditCategory.WindowTheme, invocation, "Uygulama kontrole kendi temasını veriyor; koyu temada FormWind ListView ve TreeView'e koyu temayı geri verir (WM_THEMECHANGED). Başka kontrollerde koyu temada kontrol edin.");
                    else if (name == "LoadFile" && invocation.Expression is MemberAccessExpressionSyntax access && TypeOf(access.Expression) is string type && type.Contains("RichTextBox"))
                        Add(AuditCategory.Rtf, invocation, "RTF olarak yüklenen renksiz metin kutunun yazı rengini alır; RTF'deki sabit renkleri koyu temada kontrol edin.");
                }

                if (method is MethodDeclarationSyntax { Modifiers: var modifiers, Identifier.ValueText: var methodName }
                    && modifiers.Any(SyntaxKind.OverrideKeyword) && methodName is "OnDrawItem" or "OnDrawNode" or "OnDrawSubItem" or "OnDrawColumnHeader" or "OnCellPainting")
                    findings.Add(new AuditFinding(AuditCategory.OwnerDraw, file, Line(method), $"override {methodName}", OwnerDrawAdvice));
            }
        }

        findings.AddRange(ImageButtons(trees, fieldTypes));
        return findings.OrderBy(f => f.Category).ThenBy(f => f.File, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Line).ToList();
    }

    /// <summary>Markdown rapor.</summary>
    public static string Report(IReadOnlyList<AuditFinding> findings, string target)
    {
        var markdown = new StringBuilder("# FormWind geçiş öncesi tarama\n\n");
        markdown.Append($"Taranan: `{target}`. Bu yerler FormWind eklendikten sonra elle bakılması gerekebilecek yerlerdir; hiçbir dosya değiştirilmedi.\n\n");
        markdown.Append("| Bölüm | Yer |\n| --- | ---: |\n");
        foreach (AuditCategory category in Enum.GetValues(typeof(AuditCategory)))
            markdown.Append($"| {Title(category)} | {findings.Count(f => f.Category == category)} |\n");
        markdown.Append('\n');
        foreach (var group in findings.GroupBy(f => f.Category))
        {
            markdown.Append($"## {Title(group.Key)}\n\n{Summary(group.Key)}\n\n");
            foreach (var finding in group)
                markdown.Append($"- `{finding.File}:{finding.Line}` `{finding.Code.Replace("`", "'")}`  \n  {finding.Advice}\n");
            markdown.Append('\n');
        }
        return markdown.ToString();
    }

    public static string Title(AuditCategory category) => category switch
    {
        AuditCategory.RuntimeColor => "Kodda atanan renkler",
        AuditCategory.ItemColor => "Liste öğesi ve ağaç düğümü renkleri",
        AuditCategory.GridCellStyle => "Grid hücre stilleri",
        AuditCategory.OwnerDraw => "Kendi çizen kontroller",
        AuditCategory.ImageButton => "Görselli sabit boyutlu butonlar",
        AuditCategory.WindowTheme => "SetWindowTheme",
        AuditCategory.Rtf => "RTF yükleme",
        _ => category.ToString(),
    };

    private static string Summary(AuditCategory category) => category switch
    {
        AuditCategory.RuntimeColor => "Kod bir kontrolün rengini atayınca o renk class'ların önüne geçer (koyu temada açık kalabilir). Rengi varsayılana döndüren kod `ResetBackColor()`/`ResetForeColor()` kullanırsa renk yeniden class'lardan gelir; durum renkleri (hata kırmızısı gibi) koyu temada okunur mu kontrol edin.",
        AuditCategory.ItemColor => "Öğe renkleri kontrolün renklerinden bağımsızdır; koyu temada tek tek kalır. Kontrolün rengine eşitleyen kod tema değişince güncellenmez.",
        AuditCategory.GridCellStyle => "Kodla verilen hücre stilleri class'ların hücre renklerinin üzerine yazılır.",
        AuditCategory.OwnerDraw => "FormWind kendi çizen kontrolleri boyayamaz; çizim kodu renkleri `provider.GetColors(\"list\", \"selected\")` gibi temadan almalıdır. Harness bunları OWNERDRAW olarak listeler.",
        AuditCategory.ImageButton => "FormWind sabit boyutlu butonun iç boşluğunu yazı ve görsel sığacak kadar kısar; class'lar fontu büyütürse yazı yine kesilebilir. `AutoSize` açın ya da butonu koyu ve açık temada kontrol edin.",
        AuditCategory.WindowTheme => "Bilgi: FormWind koyu temada ListView ve TreeView'e koyu temayı geri verir.",
        AuditCategory.Rtf => "Bilgi: renksiz RTF metni kutunun yazı rengini alır.",
        _ => "",
    };

    private const string OwnerDrawAdvice = "Kendi çizen kontrol: renkleri temadan alın (`provider.GetColors(…)`, `TwAutoTheme.DarkMode`); harness'in OWNERDRAW listesine ve koyu tema görüntüsüne bakın.";

    private static string ColorAdvice(ExpressionSyntax value, string property)
    {
        var text = value.ToString();
        string reset = property == "BackColor" ? "ResetBackColor()" : "ResetForeColor()";
        if (text.Contains("SystemColors."))
            return $"Sistem rengi atanıyor; koyu temada açık kalır. Varsayılana dönüş için `{reset}` kullanın (renk class'lardan gelir).";
        if (text.Contains("Color.FromArgb") || text.Contains("ColorTranslator") || text.StartsWith("Color.", StringComparison.Ordinal) || text.Contains(".Color."))
            return $"Sabit renk; koyu temada okunur mu kontrol edin (durum rengiyse koyu karşılığını `provider.GetColors(…)` ile seçin). Rengi kaldıran kod `{reset}` kullanmalı.";
        if (text.Contains("Empty"))
            return $"Color.Empty atanıyor: varsayılana dönüş; `{reset}` ile aynıdır, renk class'lardan gelir.";
        return $"Uygulamanın kendi renk değeri (`{text}`); değer sistem rengi ya da açık bir renkse koyu temada açık kalır. Varsayılana dönüş için `{reset}`.";
    }

    private static string ItemAdvice(ExpressionSyntax value) =>
        value.ToString().Contains("Empty")
            ? "Öğe rengi sıfırlanıyor; sorun yok."
            : $"Öğeye renk veriliyor (`{value}`); kontrolün renginden kopyalanıyorsa tema değişince eski renkte kalır. Kullanıcı rengi değilse atamayı kaldırın ya da renkleri `provider.GetColors(\"list\")`'ten alın.";

    // Görseli olan, yazısı da olan, AutoSize kapalı ve boyutu Designer'da sabit butonlar.
    private static IEnumerable<AuditFinding> ImageButtons(List<(string File, SyntaxTree Tree)> trees, Dictionary<string, string> fieldTypes)
    {
        // Anahtar: sınıf ve alan adı (partial sınıfın Designer'ı ve kodu aynı anahtarı paylaşır; farklı formlardaki aynı adlı
        // butonlar karışmaz).
        var info = new Dictionary<string, ButtonInfo>(StringComparer.Ordinal);
        ButtonInfo Info(SyntaxNode node, string name)
        {
            var key = (node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "") + "." + name;
            return info.TryGetValue(key, out var b) ? b : info[key] = new ButtonInfo { Name = name };
        }

        foreach (var (file, tree) in trees)
        {
            foreach (var assignment in tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assignment.Left is not MemberAccessExpressionSyntax left || LastName(left.Expression) is not string name) continue;
                if (!fieldTypes.TryGetValue(name, out var type) || !IsButtonType(type)) continue;
                var button = Info(assignment, name);
                switch (left.Name.Identifier.ValueText)
                {
                    case "Image":
                    case "ImageIndex":
                    case "ImageKey":
                        button.HasImage = true;
                        break;
                    case "TextImageRelation":
                        button.SideBySide = !assignment.Right.ToString().EndsWith("Overlay", StringComparison.Ordinal);
                        break;
                    case "Size":
                        button.File = file;
                        button.Line = tree.GetLineSpan(assignment.Span).StartLinePosition.Line + 1;
                        button.Size = assignment.Right.ToString();
                        break;
                    case "AutoSize":
                        button.AutoSize = assignment.Right.IsKind(SyntaxKind.TrueLiteralExpression);
                        break;
                    case "Text":
                        button.HasText = assignment.Right is not LiteralExpressionSyntax literal || literal.Token.ValueText.Length > 0;
                        break;
                }
            }
            // Görseli kodda bir yardımcıyla verenler (KeePass: UIUtil.SetButtonImage(m_btnTools, …)).
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (InvokedName(invocation) is not string method || method.IndexOf("Image", StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (var argument in invocation.ArgumentList.Arguments)
                    if (LastName(argument.Expression) is string name && fieldTypes.TryGetValue(name, out var type) && IsButtonType(type))
                        Info(invocation, name).HasImage = true;
            }
        }
        foreach (var pair in info.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var b = pair.Value;
            if (b.HasImage && b.SideBySide && b.HasText && !b.AutoSize && b.File != null)
                yield return new AuditFinding(AuditCategory.ImageButton, b.File, b.Line, $"{b.Name}.Size = {b.Size}",
                    "Görsel yazının yanında; sabit boyutta class'ların fontu ve iç boşluğu yazıyı kesebilir. `AutoSize = true` ya da koyu/açık temada kontrol.");
        }
    }

    private sealed class ButtonInfo
    {
        // Designer boş metni yazmaz: Text ataması yoksa yalnızca görsel vardır.
        public bool HasImage, SideBySide, AutoSize, HasText;
        public string Name = "";
        public string? File, Size;
        public int Line;
    }

    private static bool IsButtonType(string type) =>
        type.EndsWith("Button", StringComparison.Ordinal) && !type.StartsWith("ToolStrip", StringComparison.Ordinal) && type != "RadioButton";

    // SetWindowTheme'i çağıran yöntemler (KeePass: UIUtil.SetExplorerTheme); onları çağıran yerler de raporlanır.
    private static HashSet<string> ThemeWrappers(IEnumerable<SyntaxTree> trees)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in trees.SelectMany(t => t.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()))
            if (method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(i => InvokedName(i) == "SetWindowTheme")
                && method.Identifier.ValueText != "SetWindowTheme")
                result.Add(method.Identifier.ValueText);
        return result;
    }

    // Alan adı → kısa tip adı (çakışan adlar belirsiz sayılır).
    private static Dictionary<string, string> FieldTypes(IEnumerable<SyntaxTree> trees)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in trees.SelectMany(t => t.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>()))
        {
            var type = ShortName(field.Declaration.Type);
            foreach (var variable in field.Declaration.Variables)
            {
                var name = variable.Identifier.ValueText;
                if (result.TryGetValue(name, out var existing) && existing != type) ambiguous.Add(name);
                else result[name] = type;
            }
        }
        foreach (var name in ambiguous) result.Remove(name);
        return result;
    }

    // Yöntemdeki yerel değişkenler, parametreler ve foreach değişkenleri.
    private static Dictionary<string, string> LocalTypes(BaseMethodDeclarationSyntax method)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in method.ParameterList.Parameters)
            if (parameter.Type != null) result[parameter.Identifier.ValueText] = ShortName(parameter.Type);
        foreach (var declaration in method.DescendantNodes().OfType<VariableDeclarationSyntax>())
            foreach (var variable in declaration.Variables)
            {
                var type = declaration.Type.IsVar
                    ? variable.Initializer?.Value is ObjectCreationExpressionSyntax creation ? ShortName(creation.Type) : null
                    : ShortName(declaration.Type);
                if (type != null) result[variable.Identifier.ValueText] = type;
            }
        foreach (var loop in method.DescendantNodes().OfType<ForEachStatementSyntax>())
            if (!loop.Type.IsVar) result[loop.Identifier.ValueText] = ShortName(loop.Type);
        return result;
    }

    private static string ShortName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        NullableTypeSyntax nullable => ShortName(nullable.ElementType),
        _ => type.ToString(),
    };

    private static string? LastName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        ParenthesizedExpressionSyntax parenthesized => LastName(parenthesized.Expression),
        _ => null,
    };

    private static string? InvokedName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        _ => null,
    };

    private static string Snippet(SyntaxNode node)
    {
        var text = string.Join(" ", node.ToString().Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
        return text.Length > 120 ? text.Substring(0, 117) + "..." : text;
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
        || path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar);

    private static string Relative(string root, string file)
    {
        var full = Path.GetFullPath(file);
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\', '/') : Path.GetFileName(file);
    }

    private static string SafeRead(string file)
    {
        try
        {
            return File.ReadAllText(file);
        }
        catch (IOException)
        {
            return "";
        }
    }
}
