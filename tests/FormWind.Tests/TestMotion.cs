using System.Runtime.CompilerServices;

namespace FormWind.Tests;

/// <summary>
/// <see cref="TwMotion.Enabled"/> varsayılan olarak Windows'un "Animasyonları göster" ayarını izler; bu ayar kapalı olan
/// makinede (sunucu, uzak masaüstü) geçiş testleri başka sonuç verirdi. Testler animasyon açıkmış gibi çalışır; kapalı
/// davranışı sınayan testler değeri kendileri false yapar ve geri alır.
/// </summary>
internal static class TestMotion
{
    [ModuleInitializer]
    internal static void Fix() => TwMotion.Enabled = true;
}
