using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FormWind.Convert;

/// <summary>Bir kontrol için yapılan dönüşüm: üretilen class'lar, kaldırılan satırlar ve elle bakılması gerekenler.</summary>
public sealed class ControlConversion
{
    public ControlConversion(string control) => Control = control;

    /// <summary>Kontrolün alan adı (<c>btnSave</c>) ya da formun kendisi için <c>this</c>.</summary>
    public string Control { get; }

    public List<string> Classes { get; } = new List<string>();

    public List<string> RemovedLines { get; } = new List<string>();

    public List<string> Notes { get; } = new List<string>();
}

public sealed class ConversionResult
{
    public ConversionResult(string source, IReadOnlyList<ControlConversion> controls, string providerName, bool providerAdded, IReadOnlyList<string>? fileNotes = null)
    {
        Source = source;
        Controls = controls;
        ProviderName = providerName;
        ProviderAdded = providerAdded;
        FileNotes = fileNotes ?? Array.Empty<string>();
    }

    /// <summary>Dosya geneli notlar (örn. eklenen <c>components</c> alanının dispose edilmesi).</summary>
    public IReadOnlyList<string> FileNotes { get; }

    /// <summary>Dönüştürülmüş <c>.Designer.cs</c> içeriği.</summary>
    public string Source { get; }

    public IReadOnlyList<ControlConversion> Controls { get; }

    public string ProviderName { get; }

    /// <summary>Dosyada TailwindProvider yoktu ve eklendi.</summary>
    public bool ProviderAdded { get; }

    public bool Changed => Controls.Any(c => c.Classes.Count > 0);
}

/// <summary>
/// Designer dosyalarındaki sabit stil atamalarını FormWind class'larına çevirir:
/// <c>BackColor</c>/<c>ForeColor</c> → <c>bg-*</c>/<c>text-*</c> (paletteki en yakın renk, değilse <c>bg-[#hex]</c>),
/// <c>Font</c> → <c>text-sm</c>/<c>text-[13px]</c> + <c>font-bold</c>/<c>font-semibold</c>, <c>Padding</c> → <c>p-*</c>,
/// <c>FlatAppearance</c> → <c>border-*</c>, <c>hover:bg-*</c>, <c>active:bg-*</c>. Çevrilen satırlar silinir ve yerine
/// <c>SetClasses</c> çağrısı yazılır; çevrilemeyenler (sistem renkleri, başka font aileleri) olduğu gibi bırakılıp not edilir.
/// </summary>
public sealed class DesignerConverter
{
    private static readonly Regex IntegerRx = new Regex(@"\d+", RegexOptions.CultureInvariant);

    // Tipi WinForms'ta bulunamayan (uygulamanın kendi) öğeleri: adından ToolStrip öğesi ya da kolon olduğu anlaşılanlar.
    private static readonly Regex NonControlNameRx = new Regex(
        @"(ToolStrip\w*(Item|Button|Label|Separator|ComboBox|TextBox|ProgressBar|DropDown\w*)|MenuItem|Column|Cell|CellStyle)$", RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, Type> WinFormsTypes = typeof(System.Windows.Forms.Control).Assembly.GetExportedTypes()
        .GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

    private static readonly (string Name, float Px)[] FontSizes =
    {
        ("xs", 12), ("sm", 14), ("base", 16), ("lg", 18), ("xl", 20), ("2xl", 24), ("3xl", 30), ("4xl", 36),
    };

    private readonly TwTheme _theme;

    public DesignerConverter(TwTheme? theme = null)
    {
        _theme = theme ?? new TwTheme();
    }

    /// <summary>Renk farkı (RGB uzaklığı) bu değerin altındaysa palet rengi kullanılır; üstündeyse <c>[#hex]</c>.</summary>
    public double ColorTolerance { get; set; } = 10;

    /// <summary>C# (<c>.Designer.cs</c>) ya da Visual Basic (<c>.Designer.vb</c>) Designer dosyası; dil içerikten anlaşılır.</summary>
    public ConversionResult Convert(string source)
    {
        var syntax = DesignerSyntax.For(source);
        var newline = source.Contains("\r\n") ? "\r\n" : "\n";
        var lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();

        var providerMatch = syntax.ProviderField.Match(source);
        bool providerAdded = !providerMatch.Success;
        string provider = providerMatch.Success ? providerMatch.Groups["name"].Value : syntax.DefaultProvider;

        var conversions = new Dictionary<string, ControlConversion>(StringComparer.Ordinal);
        var lastLine = new Dictionary<string, int>(StringComparer.Ordinal);
        var indents = new Dictionary<string, string>(StringComparer.Ordinal);
        var remove = new HashSet<int>();

        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var creation = syntax.Creation.Match(line);
            if (creation.Success) types[creation.Groups["name"].Value] = creation.Groups["type"].Value;
        }
        var skipped = new HashSet<string>(StringComparer.Ordinal);
        // Kontrolün kaynakta yazıldığı biçim: "this.btnSave" ya da "btnSave" (yerel değişkenlerde "this." olamaz). Formun
        // kendisi dilden bağımsız olarak "this" anahtarıyla tutulur (VB'de "Me").
        var qualified = new Dictionary<string, bool>(StringComparer.Ordinal);

        for (int i = 0; i < lines.Count; i++)
        {
            var match = syntax.Assignment.Match(lines[i]);
            if (!match.Success) continue;
            var control = match.Groups["control"].Success ? match.Groups["control"].Value : "this";
            // Yalnızca formun kendisi ve InitializeComponent'te oluşturulan kontroller: yerel nesneler (hücre stilleri)
            // ve Control olmayan bileşenler (ToolStrip öğeleri, kolonlar) SetClasses alamaz.
            if (control != "this")
            {
                if (!types.TryGetValue(control, out var typeName)) continue;
                if (!IsControlType(typeName, BaseTypes, out var unverified))
                {
                    if (skipped.Add(control))
                    {
                        if (!conversions.TryGetValue(control, out var skip)) conversions[control] = skip = new ControlConversion(control);
                        skip.Notes.Add($"{typeName} bir Control değil; stil satırları bırakıldı.");
                    }
                    continue;
                }
                if (unverified && skipped.Add(control))
                {
                    if (!conversions.TryGetValue(control, out var note)) conversions[control] = note = new ControlConversion(control);
                    note.Notes.Add($"{typeName} tipi doğrulanamadı; Control olduğu varsayıldı.");
                }
            }
            if (!conversions.TryGetValue(control, out var conversion)) conversions[control] = conversion = new ControlConversion(control);
            if (control != "this") qualified[control] = lines[i].TrimStart().StartsWith(syntax.Self + ".", StringComparison.OrdinalIgnoreCase);

            var classes = Translate(match.Groups["property"].Value, match.Groups["value"].Value, conversion.Notes);
            if (classes is null) continue;
            conversion.Classes.AddRange(classes);
            conversion.RemovedLines.Add(lines[i].Trim());
            remove.Add(i);
            lastLine[control] = i;
            indents[control] = match.Groups["indent"].Value;
        }

        // Var olan SetClasses çağrıları: yeni class'lar sonlarına eklenir.
        var existing = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < lines.Count; i++)
        {
            var match = syntax.SetClasses.Match(lines[i]);
            if (match.Success && match.Groups["provider"].Value == provider)
                existing[match.Groups["control"].Success ? match.Groups["control"].Value : "this"] = i;
        }

        var insertAfter = new Dictionary<int, List<string>>();
        foreach (var conversion in conversions.Values.Where(c => c.Classes.Count > 0))
        {
            var target = conversion.Control == "this" ? syntax.Self
                : qualified.TryGetValue(conversion.Control, out var withThis) && withThis ? syntax.Self + "." + conversion.Control : conversion.Control;
            if (existing.TryGetValue(conversion.Control, out var index))
            {
                var match = syntax.SetClasses.Match(lines[index]);
                var merged = Merge(match.Groups["classes"].Value, conversion.Classes);
                lines[index] = lines[index].Replace("\"" + match.Groups["classes"].Value + "\"", "\"" + merged + "\"");
                continue;
            }
            var line = indents[conversion.Control] + syntax.SetClassesCall(provider, target, string.Join(" ", Dedupe(conversion.Classes)));
            int after = lastLine[conversion.Control];
            if (!insertAfter.TryGetValue(after, out var list)) insertAfter[after] = list = new List<string>();
            list.Add(line);
        }

        var output = new List<string>(lines.Count + 8);
        for (int i = 0; i < lines.Count; i++)
        {
            if (!remove.Contains(i)) output.Add(lines[i]);
            if (insertAfter.TryGetValue(i, out var added)) output.AddRange(added);
        }

        bool changed = conversions.Values.Any(c => c.Classes.Count > 0);
        var fileNotes = new List<string>();
        if (changed && providerAdded) AddProvider(output, syntax, provider, fileNotes);
        else providerAdded = false;

        return new ConversionResult(string.Join(newline, output), conversions.Values.ToList(), provider, providerAdded, fileNotes);
    }

    /// <summary>
    /// Tip WinForms'ta varsa Control'dan türeyip türemediğine bakılır; yoksa (uygulamanın kendi tipi) adına bakılır ve
    /// <paramref name="unverified"/> true döner.
    /// </summary>
    internal static bool IsControlType(string typeName, out bool unverified) => IsControlType(typeName, null, out unverified);

    /// <summary>
    /// <paramref name="baseTypes"/> uygulamanın kendi tiplerinin taban tipleridir (kaynak koddan: "class FontFamilyComboBox :
    /// ToolStripComboBox"); zincir bir WinForms tipine ulaşırsa sonuç kesindir.
    /// </summary>
    internal static bool IsControlType(string typeName, IReadOnlyDictionary<string, string>? baseTypes, out bool unverified)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = typeName;
        while (visited.Add(current))
        {
            var simple = current.Substring(current.LastIndexOf('.') + 1);
            if (WinFormsTypes.TryGetValue(simple, out var type) && (current == simple || current == type.FullName || current.EndsWith("Forms." + simple, StringComparison.Ordinal)))
            {
                unverified = false;
                return typeof(System.Windows.Forms.Control).IsAssignableFrom(type);
            }
            if (baseTypes is null || !baseTypes.TryGetValue(simple, out var baseType)) break;
            current = baseType;
        }
        unverified = true;
        return !NonControlNameRx.IsMatch(typeName.Substring(typeName.LastIndexOf('.') + 1));
    }

    /// <summary>Uygulamanın kendi tiplerinin taban tipleri; <see cref="CollectBaseTypes"/> ile kaynak koddan toplanır.</summary>
    public IReadOnlyDictionary<string, string>? BaseTypes { get; set; }

    private static readonly Regex ClassDeclarationRx = new Regex(@"\bclass\s+(?<name>\w+)(?:<[^>]*>)?\s*:\s*(?<base>[\w.]+)", RegexOptions.CultureInvariant);

    // VB: "Public Class FontComboBox" ardından (aynı satırda ":" ile ya da sonraki satırda) "Inherits ToolStripComboBox".
    private static readonly Regex VbClassDeclarationRx = new Regex(
        @"^[ \t]*(?:(?:Public|Friend|Private|Protected|Partial|MustInherit|NotInheritable)\s+)*Class\s+(?<name>\w+)(?:\s*\(Of[^)]*\))?[ \t]*(?::|\r?\n)\s*Inherits\s+(?:Global\.)?(?<base>[\w.]+)",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    /// <summary>Kaynak dosyalardaki "class X : Y" (C#) ve "Class X / Inherits Y" (VB) bildirimlerinden ad → taban tip eşlemesi.</summary>
    public static Dictionary<string, string> CollectBaseTypes(IEnumerable<string> sources)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources)
            foreach (Match match in ClassDeclarationRx.Matches(source).Cast<Match>().Concat(VbClassDeclarationRx.Matches(source).Cast<Match>()))
                if (!map.ContainsKey(match.Groups["name"].Value)) map[match.Groups["name"].Value] = match.Groups["base"].Value;
        return map;
    }

    /// <summary>Bir atamayı class'lara çevirir; çevrilemiyorsa <c>null</c> (satır kalır, not düşülür).</summary>
    internal List<string>? Translate(string property, string value, List<string> notes)
    {
        switch (property)
        {
            case "BackColor":
                return Color("bg", value, notes, property);
            case "ForeColor":
                return Color("text", value, notes, property);
            case "FlatAppearance.BorderColor":
                return Color("border", value, notes, property);
            case "FlatAppearance.MouseOverBackColor":
                return Color("hover:bg", value, notes, property);
            case "FlatAppearance.MouseDownBackColor":
                return Color("active:bg", value, notes, property);
            case "FlatAppearance.BorderSize":
                if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var size)) break;
                return new List<string> { size switch { 0 => "border-0", 1 => "border", 2 => "border-2", 4 => "border-4", 8 => "border-8", _ => "border-2" } };
            case "Padding":
                return Padding(value, notes);
            case "Font":
                return Font(value, notes);
        }
        notes.Add($"{property} = {value}: çevrilemedi.");
        return null;
    }

    private List<string>? Color(string prefix, string value, List<string> notes, string property)
    {
        if (!TryParseColor(value, out var color, out var reason))
        {
            notes.Add($"{property} = {value}: {reason}; satır bırakıldı.");
            return null;
        }
        return new List<string> { prefix + "-" + ColorClass(color, out var approximate) }
            .Also(_ => { if (approximate != null) notes.Add($"{property}: {approximate}"); });
    }

    /// <summary>Paletteki adı (<c>blue-600</c>, <c>white</c>) ya da <c>[#1e40af]</c>.</summary>
    internal string ColorClass(Color color, out string? approximate)
    {
        approximate = null;
        if (color.A == 0) return "transparent";
        if (color.R == 255 && color.G == 255 && color.B == 255) return "white";
        if (color.R == 0 && color.G == 0 && color.B == 0) return "black";

        string? best = null;
        double bestDistance = double.MaxValue;
        foreach (var name in _theme.ColorNames)
        {
            foreach (var shade in TwPalette.Shades)
            {
                if (!_theme.TryGetColor(name, shade, out var candidate)) continue;
                double distance = Math.Sqrt(Math.Pow(candidate.R - color.R, 2) + Math.Pow(candidate.G - color.G, 2) + Math.Pow(candidate.B - color.B, 2));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = name + "-" + shade;
                }
            }
        }

        var hex = $"#{color.R:x2}{color.G:x2}{color.B:x2}";
        if (best != null && bestDistance == 0) return best;
        if (best != null && bestDistance <= ColorTolerance)
        {
            approximate = $"{hex} yerine en yakın palet rengi {best} kullanıldı (fark {bestDistance:0.#}).";
            return best;
        }
        return "[" + hex + "]";
    }

    internal static bool TryParseColor(string value, out Color color, out string reason)
    {
        color = System.Drawing.Color.Empty;
        reason = "";
        value = value.Trim();
        if (value.Contains("SystemColors."))
        {
            reason = "sistem rengi (tema ile değişir)";
            return false;
        }
        if (value.Contains("FromArgb("))
        {
            var numbers = IntegerRx.Matches(value.Substring(value.IndexOf("FromArgb(", StringComparison.Ordinal)))
                .Cast<Match>().Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
            if (numbers.Count == 3)
            {
                color = System.Drawing.Color.FromArgb(numbers[0], numbers[1], numbers[2]);
                return true;
            }
            if (numbers.Count == 4)
            {
                color = System.Drawing.Color.FromArgb(numbers[0], numbers[1], numbers[2], numbers[3]);
                return true;
            }
            reason = "FromArgb biçimi tanınmadı";
            return false;
        }
        var named = Regex.Match(value, @"Color\.(\w+)$");
        if (named.Success)
        {
            var known = System.Drawing.Color.FromName(named.Groups[1].Value);
            if (known.IsKnownColor)
            {
                color = System.Drawing.Color.FromArgb(known.A, known.R, known.G, known.B);
                return true;
            }
        }
        reason = "renk ifadesi tanınmadı";
        return false;
    }

    private static List<string>? Padding(string value, List<string> notes)
    {
        var numbers = IntegerRx.Matches(value).Cast<Match>().Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
        if (!value.Contains("Padding(") || (numbers.Count != 1 && numbers.Count != 4))
        {
            notes.Add($"Padding = {value}: tanınmadı.");
            return null;
        }
        if (numbers.Count == 1) return new List<string> { "p-" + Spacing(numbers[0]) };

        int left = numbers[0], top = numbers[1], right = numbers[2], bottom = numbers[3];
        if (left == right && top == bottom)
            return left == top ? new List<string> { "p-" + Spacing(left) } : new List<string> { "px-" + Spacing(left), "py-" + Spacing(top) };
        return new List<string> { "pl-" + Spacing(left), "pt-" + Spacing(top), "pr-" + Spacing(right), "pb-" + Spacing(bottom) };
    }

    /// <summary>Piksel → boşluk ölçeği (1 birim = 4 px): 8 → <c>2</c>, 6 → <c>1.5</c>, 7 → <c>[7px]</c>.</summary>
    internal static string Spacing(int pixels)
    {
        if (pixels % 4 == 0) return (pixels / 4).ToString(CultureInfo.InvariantCulture);
        if (pixels % 2 == 0) return (pixels / 4).ToString(CultureInfo.InvariantCulture) + ".5";
        if (pixels == 1) return "px";
        return "[" + pixels.ToString(CultureInfo.InvariantCulture) + "px]";
    }

    private List<string>? Font(string value, List<string> notes)
    {
        // C#: 10.5F, VB: 10.5!
        var match = Regex.Match(value, @"Font\(\s*""(?<family>[^""]+)""\s*,\s*(?<size>[\d.]+)[F!]?(?<rest>.*)\)$", RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            notes.Add($"Font = {value}: tanınmadı.");
            return null;
        }

        var family = match.Groups["family"].Value;
        var rest = match.Groups["rest"].Value;
        float size = float.Parse(match.Groups["size"].Value, CultureInfo.InvariantCulture);
        bool pixelUnit = rest.Contains("GraphicsUnit.Pixel");
        float px = pixelUnit ? size : size * 96f / 72f;

        var classes = new List<string>();
        var (name, nearest) = FontSizes.OrderBy(f => Math.Abs(f.Px - px)).First();
        classes.Add(Math.Abs(nearest - px) <= 0.5f ? "text-" + name : "text-[" + Math.Round(px).ToString(CultureInfo.InvariantCulture) + "px]");

        // Ağırlık: aile adındaki kalınlık ("Segoe UI Semibold") ya da FontStyle.Bold.
        var baseFamily = family;
        foreach (var (suffix, weight) in new[] { (" Semibold", "font-semibold"), (" Light", "font-light"), (" Semilight", "font-light"), (" Black", "font-black"), (" Bold", "font-bold") })
        {
            if (family.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                classes.Add(weight);
                baseFamily = family.Substring(0, family.Length - suffix.Length);
                break;
            }
        }
        if (rest.Contains("FontStyle.Bold") && !classes.Any(c => c.StartsWith("font-", StringComparison.Ordinal))) classes.Add("font-bold");
        if (rest.Contains("FontStyle.Italic")) classes.Add("italic");
        if (rest.Contains("FontStyle.Underline")) classes.Add("underline");

        if (!string.Equals(baseFamily, _theme.FontFamilySans, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(baseFamily, "Microsoft Sans Serif", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add($"Font ailesi \"{family}\" temadakinden ({_theme.FontFamilySans}) farklı; satır bırakıldı. Tema dosyasına ekleyip font-sans ile kullanabilirsiniz.");
            return null;
        }
        return classes;
    }

    private static IEnumerable<string> Dedupe(IEnumerable<string> classes) => classes.Distinct(StringComparer.Ordinal);

    private static string Merge(string existing, IEnumerable<string> added) =>
        string.Join(" ", Dedupe(existing.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Concat(added)));

    // Alan ve InitializeComponent başına oluşturma satırı. Provider components kapsayıcısına eklenir; böylece formun
    // Dispose'unda dispose edilir (provider statik bir olaya abone olduğundan dispose edilmezse form bellekte kalır).
    private static void AddProvider(List<string> lines, DesignerSyntax syntax, string provider, List<string> notes)
    {
        int init = lines.FindIndex(l => syntax.InitializeComponent.IsMatch(l));
        if (init < 0) return;
        // C#: gövde "{" satırından sonra başlar (eski Designer süslü parantezi metotla aynı satıra yazar:
        // "private void InitializeComponent() {"). VB'de gövde Sub satırından sonra başlar.
        int brace = syntax.IsVisualBasic || lines[init].TrimEnd().EndsWith("{", StringComparison.Ordinal) ? init : lines.FindIndex(init, l => l.Trim() == "{");
        if (brace < 0) return;
        // Girinti ilk kod satırından alınır (sekmeyle girintilenen dosyalar).
        int firstCode = lines.FindIndex(brace + 1, l => l.Trim().Length > 0);
        var indent = firstCode >= 0 && !syntax.IsBodyEnd(lines[firstCode])
            ? Regex.Match(lines[firstCode], @"^\s*").Value
            : Regex.Match(lines[brace], @"^\s*").Value + "    ";

        bool hasField = lines.Any(l => syntax.ComponentsField.IsMatch(l));
        int components = lines.FindIndex(brace, l => syntax.ComponentsCreation.IsMatch(l));
        var creation = new List<string>();
        if (components < 0) creation.Add(indent + syntax.ComponentsCreationLine);
        creation.Add(indent + syntax.ProviderCreationLine(provider));
        lines.InsertRange(components >= 0 ? components + 1 : brace + 1, creation);

        var fields = new List<string> { syntax.ProviderFieldLine(provider) };
        if (!hasField)
        {
            fields.Insert(0, syntax.ComponentsFieldLine);
            if (!lines.Any(l => l.Contains("components.Dispose(") || l.Contains("components?.Dispose(")))
                notes.Add("Sınıfta components alanı yoktu; eklendi. Dispose içinde components.Dispose() çağrılmalı, yoksa TailwindProvider dispose edilmez.");
        }

        int lastField = lines.FindLastIndex(l => syntax.Field.IsMatch(l));
        if (lastField >= 0)
        {
            var fieldIndent = Regex.Match(lines[lastField], @"^\s*").Value;
            lines.InsertRange(lastField + 1, fields.Select(f => fieldIndent + f));
            return;
        }

        int classEnd = syntax.ClassEnd(lines);
        if (classEnd < 0) return;
        var classIndent = Regex.Match(lines[classEnd], @"^\s*").Value + "    ";
        lines.InsertRange(classEnd, fields.Select(f => classIndent + f));
    }
}

/// <summary>Designer dosyasının dili: C# ya da Visual Basic. Satır tanıma ve üretilen kod buna göre değişir.</summary>
internal sealed class DesignerSyntax
{
    private const RegexOptions Options = RegexOptions.CultureInvariant;
    // VB anahtar sözcükleri büyük-küçük harf duyarsızdır; Designer "Me", "New" yazar.
    private const RegexOptions VbOptions = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private const string Properties =
        "BackColor|ForeColor|Font|Padding|FlatAppearance\\.BorderColor|FlatAppearance\\.BorderSize|FlatAppearance\\.MouseOverBackColor|FlatAppearance\\.MouseDownBackColor";

    public static readonly DesignerSyntax CSharp = new DesignerSyntax
    {
        Self = "this",
        DefaultProvider = "tailwindProvider1",
        // Visual Studio'nun yeni Designer'ı "this." yazmaz (buttonOk.BackColor = ...); ikisi de tanınır.
        Assignment = new Regex(@"^(?<indent>\s*)(?:this\.)?(?:(?<control>\w+)\.)?(?<property>" + Properties + @")\s*=\s*(?<value>.+?);\s*$", Options),
        ProviderField = new Regex(@"(?:this\.)?(?<name>\w+)\s*=\s*new\s+(?:global::)?(?:FormWind\.)?TailwindProvider\(", Options),
        SetClasses = new Regex(@"^\s*(?:this\.)?(?<provider>\w+)\.SetClasses\((?:this|(?:this\.)?(?<control>\w+)),\s*""(?<classes>[^""]*)""\);\s*$", Options),
        ComponentsField = new Regex(@"\b(?:System\.ComponentModel\.)?I?Container\s+components\b", Options),
        ComponentsCreation = new Regex(@"^\s*(?:this\.)?components\s*=\s*new\b", Options),
        Field = new Regex(@"^\s*(?:private|protected|internal|public)(?:\s+(?:internal|protected|readonly|static))*\s+[\w.<>,?]+\s+\w+\s*(?:=\s*[^;(]+)?;\s*$", Options),
        // InitializeComponent'teki oluşturma satırı alanın tipini verir: "this.btnSave = new System.Windows.Forms.Button();".
        // Yerel değişkenler ("DataGridViewCellStyle style1 = new ...") tip adıyla başladığı için eşleşmez.
        Creation = new Regex(@"^\s*(?:this\.)?(?<name>\w+)\s*=\s*new\s+(?:global::)?(?<type>[\w.]+)\s*\(", Options),
        InitializeComponent = new Regex(@"void\s+InitializeComponent\s*\(", Options),
        ComponentsCreationLine = "this.components = new System.ComponentModel.Container();",
        ComponentsFieldLine = "private System.ComponentModel.IContainer components = null;",
    };

    public static readonly DesignerSyntax VisualBasic = new DesignerSyntax
    {
        IsVisualBasic = true,
        Self = "Me",
        DefaultProvider = "TailwindProvider1",
        Assignment = new Regex(@"^(?<indent>\s*)(?:Me\.)?(?:(?<control>\w+)\.)?(?<property>" + Properties + @")\s*=\s*(?<value>.+?)\s*$", VbOptions),
        ProviderField = new Regex(@"(?:Me\.)?(?<name>\w+)\s*=\s*New\s+(?:Global\.)?(?:FormWind\.)?TailwindProvider\(", VbOptions),
        SetClasses = new Regex(@"^\s*(?:Me\.)?(?<provider>\w+)\.SetClasses\((?:Me|(?:Me\.)?(?<control>\w+)),\s*""(?<classes>[^""]*)""\)\s*$", VbOptions),
        ComponentsField = new Regex(@"\bcomponents\s+As\s+(?:System\.ComponentModel\.)?I?Container\b", VbOptions),
        ComponentsCreation = new Regex(@"^\s*(?:Me\.)?components\s*=\s*New\b", VbOptions),
        // "Friend WithEvents Button1 As System.Windows.Forms.Button"; "Private Sub ..." ve "Protected Overrides Sub ..." eşleşmez.
        Field = new Regex(@"^\s*(?:Friend|Private|Protected|Public)(?:\s+(?:WithEvents|Shared|ReadOnly|Friend))*\s+\w+\s+As\s+(?:New\s+)?[\w.]+(?:\(\))?(?:\s*=.*)?\s*$", VbOptions),
        Creation = new Regex(@"^\s*(?:Me\.)?(?<name>\w+)\s*=\s*New\s+(?:Global\.)?(?<type>[\w.]+)\s*\(", VbOptions),
        InitializeComponent = new Regex(@"\bSub\s+InitializeComponent\s*\(", VbOptions),
        ComponentsCreationLine = "Me.components = New System.ComponentModel.Container()",
        ComponentsFieldLine = "Private components As System.ComponentModel.IContainer",
    };

    // C#'ta "void InitializeComponent(", VB'de "Sub InitializeComponent(".
    private static readonly Regex VbMarker = new Regex(@"\bSub\s+InitializeComponent\s*\(", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static DesignerSyntax For(string source) => VbMarker.IsMatch(source) ? VisualBasic : CSharp;

    public bool IsVisualBasic { get; private set; }
    /// <summary>Formun kendisi: <c>this</c> / <c>Me</c>.</summary>
    public string Self { get; private set; } = "";
    /// <summary>Designer'ın adlandırmasıyla: C#'ta <c>tailwindProvider1</c>, VB'de <c>TailwindProvider1</c>.</summary>
    public string DefaultProvider { get; private set; } = "";
    public Regex Assignment { get; private set; } = null!;
    public Regex ProviderField { get; private set; } = null!;
    public Regex SetClasses { get; private set; } = null!;
    public Regex ComponentsField { get; private set; } = null!;
    public Regex ComponentsCreation { get; private set; } = null!;
    public Regex Field { get; private set; } = null!;
    public Regex Creation { get; private set; } = null!;
    public Regex InitializeComponent { get; private set; } = null!;
    public string ComponentsCreationLine { get; private set; } = "";
    public string ComponentsFieldLine { get; private set; } = "";

    public string SetClassesCall(string provider, string target, string classes) => IsVisualBasic
        ? $"Me.{provider}.SetClasses({target}, \"{classes}\")"
        : $"this.{provider}.SetClasses({target}, \"{classes}\");";

    public string ProviderCreationLine(string provider) => IsVisualBasic
        ? $"Me.{provider} = New FormWind.TailwindProvider(Me.components)"
        : $"this.{provider} = new FormWind.TailwindProvider(this.components);";

    public string ProviderFieldLine(string provider) => IsVisualBasic
        ? $"Friend WithEvents {provider} As FormWind.TailwindProvider"
        : $"private FormWind.TailwindProvider {provider};";

    /// <summary>InitializeComponent gövdesinin sonu (boş gövde): C#'ta "}", VB'de "End Sub".</summary>
    public bool IsBodyEnd(string line) => IsVisualBasic
        ? Regex.IsMatch(line, @"^\s*End\s+Sub\b", RegexOptions.IgnoreCase)
        : line.Trim() == "}";

    /// <summary>Alan yoksa alanların ekleneceği satır: sınıfın kapanışı.</summary>
    public int ClassEnd(List<string> lines)
    {
        if (IsVisualBasic) return lines.FindLastIndex(l => Regex.IsMatch(l, @"^\s*End\s+Class\b", RegexOptions.IgnoreCase));
        // Dosya kapsamlı namespace'te son "}", blok namespace'te sondan ikinci.
        var closings = Enumerable.Range(0, lines.Count).Where(i => lines[i].Trim() == "}").ToList();
        bool fileScoped = lines.Any(l => Regex.IsMatch(l, @"^\s*namespace\s+[\w.]+\s*;"));
        return fileScoped ? closings.LastOrDefault(-1) : closings.Count >= 2 ? closings[closings.Count - 2] : -1;
    }
}

internal static class FluentExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
