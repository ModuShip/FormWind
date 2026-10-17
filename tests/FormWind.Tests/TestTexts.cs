using System.Runtime.CompilerServices;

namespace FormWind.Tests;

/// <summary>
/// Testler kütüphanenin Türkçe metinlerini doğrular; varsayılan dil makinenin kullanıcı arayüzü diline bağlı olduğundan
/// (CI İngilizce) test derlemesi yüklenirken Türkçe metinler seçilir. İngilizce varsayılanlar TwTextsTests'te denetlenir.
/// </summary>
internal static class TestTexts
{
    [ModuleInitializer]
    internal static void UseTurkish() => TwTexts.Current = TwTexts.Turkish;
}
