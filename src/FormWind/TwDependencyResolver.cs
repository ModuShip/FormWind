using System;
using System.IO;
using System.Reflection;

namespace FormWind;

/// <summary>
/// .NET Framework: FormWind's dependencies (System.Text.Json and the packages it uses) reference each other with older
/// assembly versions than the files shipped next to FormWind (System.Memory → System.Runtime.CompilerServices.Unsafe 4.0.4.1,
/// file 6.0.0.0); an application fixes this with binding redirects. A host that FormWind does not own has none for them, e.g.
/// a KeePass plugin: loading the theme failed with FileNotFoundException. When the runtime cannot bind one of these
/// assemblies, the file next to FormWind is loaded if it has the same public key and at least the requested version (what a
/// binding redirect does). Other requests and successful bindings are not touched. No effect on .NET (Core).
/// </summary>
internal static class TwDependencyResolver
{
    private static readonly string[] Dependencies =
    {
        "System.Text.Json", "System.Text.Encodings.Web", "System.Memory", "System.Buffers", "System.Numerics.Vectors",
        "System.Runtime.CompilerServices.Unsafe", "System.Threading.Tasks.Extensions", "Microsoft.Bcl.AsyncInterfaces",
        "System.ValueTuple",
    };

#if NETFRAMEWORK
    private static int _installed;
#endif
    // Çözücünün kendisi çözdüğü derlemelere dokunursa (ör. dizide SequenceEqual, C# 14'te MemoryExtensions'a, yani
    // System.Memory/Unsafe'e bağlanır) çalışma zamanı aynı isteği yeniden sorar; iç içe çağrı null döner, yoksa olay kendini
    // sonsuza dek çağırıp yığını taşırıyordu (KeePass 0xC00000FD).
    [ThreadStatic] private static bool _resolving;

    public static void EnsureInstalled()
    {
#if NETFRAMEWORK
        if (System.Threading.Interlocked.Exchange(ref _installed, 1) == 0) AppDomain.CurrentDomain.AssemblyResolve += (_, e) => Resolve(e.Name);
#endif
    }

    /// <summary>The dependency for an assembly reference the runtime could not bind; <c>null</c> when it is not FormWind's.</summary>
    internal static Assembly? Resolve(string reference)
    {
        if (_resolving) return null;
        _resolving = true;
        try
        {
            return ResolveCore(reference);
        }
        finally
        {
            _resolving = false;
        }
    }

    private static Assembly? ResolveCore(string reference)
    {
        AssemblyName requested;
        try
        {
            requested = new AssemblyName(reference);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is FileLoadException)
        {
            return null;
        }
        if (requested.Name is null || Array.IndexOf(Dependencies, requested.Name) < 0) return null;
        var token = requested.GetPublicKeyToken();
        bool Matches(AssemblyName candidate) =>
            string.Equals(candidate.Name, requested.Name, StringComparison.OrdinalIgnoreCase)
            && (token is null || token.Length == 0 || SameBytes(candidate.GetPublicKeyToken(), token))
            && (requested.Version is null || candidate.Version is not null && candidate.Version >= requested.Version);

        // Başka bir bileşen uygun bir sürümü zaten yüklediyse o kullanılır (aynı türlerin iki kopyası olmasın).
        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
            if (Matches(loaded.GetName())) return loaded;

        foreach (var folder in Folders())
        {
            var path = Path.Combine(folder, requested.Name + ".dll");
            try
            {
                if (File.Exists(path) && Matches(AssemblyName.GetAssemblyName(path))) return Assembly.LoadFrom(path);
            }
            catch (Exception ex) when (ex is IOException || ex is BadImageFormatException || ex is UnauthorizedAccessException)
            {
            }
        }
        return null;
    }

    // FormWind.dll'in klasörü; gölge kopyalamada (ör. xUnit, ASP.NET) Location önbellektedir, bağımlılıklar CodeBase'in yanında.
    private static string[] Folders()
    {
        var assembly = typeof(TwDependencyResolver).Assembly;
        var location = Path.GetDirectoryName(assembly.Location);
        string? original = null;
        try
        {
#pragma warning disable SYSLIB0012 // CodeBase: yalnızca .NET Framework'te çağrılır (gölge kopyalama orada var).
            var codeBase = assembly.CodeBase;
#pragma warning restore SYSLIB0012
            if (codeBase != null && Uri.TryCreate(codeBase, UriKind.Absolute, out var uri) && uri.IsFile) original = Path.GetDirectoryName(uri.LocalPath);
        }
        catch (Exception ex) when (ex is NotSupportedException || ex is PlatformNotSupportedException || ex is ArgumentException)
        {
        }
        if (string.IsNullOrEmpty(location)) return string.IsNullOrEmpty(original) ? Array.Empty<string>() : new[] { original! };
        return string.IsNullOrEmpty(original) || string.Equals(original, location, StringComparison.OrdinalIgnoreCase)
            ? new[] { location! }
            : new[] { location!, original! };
    }

    // Bilerek elle: System.Memory'ye bağlanan hiçbir şey kullanılmaz (yukarıya bkz.).
    private static bool SameBytes(byte[]? a, byte[] b)
    {
        if (a is null || a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }
}
