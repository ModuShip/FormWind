using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace FormWind.Design;

/// <summary>
/// Finds the theme of the project being designed, so that the Designer's preview, component lists and class builder use the
/// project's colors and components instead of the defaults. The class being designed is resolved from the project's build
/// output; its assembly's folder is searched for <c>tw.config.json</c> (copied there by the project), then the assembly for
/// the copy that FormWind's MSBuild targets embed (<see cref="ResourceName"/>). Both come from the last build, so changes to the
/// theme file show up after a rebuild and reopening the form. Works the same in the .NET Framework and the out-of-process
/// (.NET 8+) Designer, because both load the project's output to resolve its types.
/// </summary>
internal static class TwDesignTheme
{
    /// <summary>Logical name of the embedded theme file (see <c>build/FormWind.targets</c>).</summary>
    public const string ResourceName = "FormWind.tw.config.json";

    public const string FileName = "tw.config.json";

    /// <summary>The project's theme, or <c>null</c> when it has none, has not been built yet, or the file is invalid.</summary>
    public static TwTheme? Find(Func<Type, object?> getService)
    {
        try
        {
            var host = getService(typeof(IDesignerHost)) as IDesignerHost;
            var resolver = getService(typeof(ITypeResolutionService)) as ITypeResolutionService;
            var name = host?.RootComponentClassName;
            if (resolver is null || string.IsNullOrEmpty(name)) return null;

            var type = resolver.GetType(name!, throwOnError: false);
            if (type is null) return null;
            var directory = OutputFolder(type.Assembly, resolver);
            return FromAssembly(type.Assembly, directory);
        }
        catch (Exception ex) when (ex is IOException or FormatException or System.Text.Json.JsonException or UnauthorizedAccessException
                                       or BadImageFormatException or NotSupportedException)
        {
            Debug.WriteLine($"FormWind: the Designer could not load the project's theme: {ex}");
            return null;
        }
    }

    /// <summary>The theme file next to the assembly, otherwise the embedded copy; <c>null</c> when neither exists.</summary>
    internal static TwTheme? FromAssembly(Assembly assembly, string? directory)
    {
        if (directory != null)
        {
            var path = Path.Combine(directory, FileName);
            if (File.Exists(path)) return Load(File.ReadAllText(path), directory);
        }

        if (!assembly.GetManifestResourceNames().Contains(ResourceName)) return null;
        using var stream = assembly.GetManifestResourceStream(ResourceName)!;
        using var reader = new StreamReader(stream);
        return Load(reader.ReadToEnd(), directory);
    }

    private static TwTheme Load(string json, string? directory)
    {
        var theme = new TwTheme();
        theme.ApplyJson(json, directory ?? AppDomain.CurrentDomain.BaseDirectory, skipMissingFiles: true);
        return theme;
    }

    private static string? OutputFolder(Assembly assembly, ITypeResolutionService resolver)
    {
        var location = assembly.IsDynamic ? null : assembly.Location;
        if (string.IsNullOrEmpty(location)) location = resolver.GetPathOfAssembly(assembly.GetName());
        return string.IsNullOrEmpty(location) ? null : Path.GetDirectoryName(location);
    }

    /// <summary>
    /// The theme behind a property being edited: that of the <see cref="TailwindProvider"/> providing the property, or of the
    /// provider on the same form; <see cref="TwTheme.Current"/> otherwise.
    /// </summary>
    public static TwTheme Of(ITypeDescriptorContext? context)
    {
        if (context is null) return TwTheme.Current;
        if (context.Instance is TailwindProviderDesigner.ClassesActionList list) return list.Provider.Theme;
        if (context.PropertyDescriptor?.Attributes[typeof(ExtenderProvidedPropertyAttribute)] is ExtenderProvidedPropertyAttribute
            { Provider: TailwindProvider provider })
            return provider.Theme;
        if (context.Instance is IComponent { Site: { } site } && site.GetService(typeof(IDesignerHost)) is IDesignerHost host)
        {
            var owner = host.Container.Components.OfType<TailwindProvider>().FirstOrDefault();
            if (owner != null) return owner.Theme;
        }
        return TwTheme.Current;
    }
}
