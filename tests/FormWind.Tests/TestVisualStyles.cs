using System.Runtime.CompilerServices;

namespace FormWind.Tests;

/// <summary>
/// Test süreci Application.EnableVisualStyles çağırmaz (görsel stiller kapalı, comctl32 v5): temaya bağlı yollar (koyu liste
/// başlığının yazı rengi) temasızken kapanır. Testler tema varmış gibi çalışır; temasız davranışı sınayan testler
/// <c>TwNativeTheme.ThemesOverride</c>'ı kendileri false yapar ve geri alır.
/// </summary>
internal static class TestVisualStyles
{
    [ModuleInitializer]
    internal static void Fix() => TwNativeTheme.ThemesOverride = true;
}
