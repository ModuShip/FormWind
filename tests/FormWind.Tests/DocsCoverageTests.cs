using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Her public kontrol web sitesinin belgelerinde (website/en/docs) veya llms.txt'de adıyla geçer; İngilizce belgelerde geçen
/// her Tw* tipi Türkçe belgelerde de geçer. Yeni bir kontrol eklendiğinde belgesi unutulursa bu testler düşer.
/// </summary>
public class DocsCoverageTests
{
    // Kendi başına kullanılmayan, başka API'lerin oluşturduğu pencereler (TwDialog.Show, TwToast.Show).
    private static readonly HashSet<string> NotDocumented = new HashSet<string>(StringComparer.Ordinal)
    {
        "TwDialogForm",
        "TwToastForm",
    };

    private static readonly string Website = FindWebsite();

    private static string FindWebsite()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "website");
            if (File.Exists(Path.Combine(path, "llms.txt")) && Directory.Exists(Path.Combine(path, "en", "docs"))) return path;
        }
        throw new DirectoryNotFoundException("website klasörü bulunamadı.");
    }

    private static string Docs(string lang) =>
        string.Join("\n", Directory.GetFiles(Path.Combine(Website, lang, "docs"), "*.html").OrderBy(f => f, StringComparer.Ordinal).Select(File.ReadAllText));

    private static HashSet<string> TypeNames(string text) =>
        new HashSet<string>(Regex.Matches(text, @"\bTw[A-Z]\w*").Cast<Match>().Select(m => m.Value), StringComparer.Ordinal);

    [Fact]
    public void Every_public_control_is_documented()
    {
        var mentioned = TypeNames(Docs("en") + "\n" + File.ReadAllText(Path.Combine(Website, "llms.txt")));
        var controls = typeof(TwTheme).Assembly.GetExportedTypes()
            .Where(t => typeof(Control).IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.IsGenericType ? t.Name.Substring(0, t.Name.IndexOf('`')) : t.Name)
            .Where(n => !NotDocumented.Contains(n))
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(Array.Empty<string>(), controls.Where(n => !mentioned.Contains(n)));
    }

    [Fact]
    public void Turkish_docs_mention_the_same_types()
    {
        var turkish = TypeNames(Docs("tr"));

        Assert.Equal(Array.Empty<string>(), TypeNames(Docs("en")).Where(n => !turkish.Contains(n)).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Documented_types_exist()
    {
        var exported = new HashSet<string>(typeof(TwTheme).Assembly.GetExportedTypes()
            .Concat(typeof(FormWind.Krypton.KryptonApplier).Assembly.GetExportedTypes())
            .Select(t => t.IsGenericType ? t.Name.Substring(0, t.Name.IndexOf('`')) : t.Name), StringComparer.Ordinal);

        Assert.Equal(Array.Empty<string>(), TypeNames(Docs("en")).Where(n => !exported.Contains(n)).OrderBy(n => n, StringComparer.Ordinal));
    }
}
