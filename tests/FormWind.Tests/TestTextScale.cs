using System.Runtime.CompilerServices;

namespace FormWind.Tests;

/// <summary>
/// Windows'un metin boyutu ayarı (Erişilebilirlik → Metin boyutu) <c>text-*</c> boyutlarını çarpar; ayarın açık olduğu bir
/// makinede font boyutu doğrulayan testler sonuç değiştirirdi. Testler %100'de çalışır; ölçeği sınayan testler
/// <c>TwTextScale.Override</c>'ı kendileri ayarlar ve 1'e geri alır.
/// </summary>
internal static class TestTextScale
{
    [ModuleInitializer]
    internal static void Fix() => TwTextScale.Override = 1f;
}
