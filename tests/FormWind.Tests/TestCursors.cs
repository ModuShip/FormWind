using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FormWind.Tests;

/// <summary>
/// .NET Framework'te <see cref="Cursors"/> özellikleri kilitsiz önbelleğe alınır ve Hand gibi imleçler gömülü .cur
/// kaynağından yüklenir; paralel testler aynı anda eriştiğinde aynı imleç için tanıtıcısı farklı iki nesne oluşabilir
/// (GoldenParseTests imleci "custom" yazar). Test derlemesi yüklenirken tüm imleçler tek iş parçacığında oluşturulur.
/// </summary>
internal static class TestCursors
{
    [ModuleInitializer]
    internal static void Warm()
    {
        foreach (var property in typeof(Cursors).GetProperties(BindingFlags.Public | BindingFlags.Static))
            property.GetValue(null);
    }
}
