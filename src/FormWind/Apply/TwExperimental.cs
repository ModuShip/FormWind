namespace FormWind;

/// <summary>
/// The diagnostic of the extension points (<see cref="IStyleApplier"/>, <see cref="TwStyle"/>, <see cref="TwParsed"/> …) that may
/// still change before 1.0. Suppress it (<c>&lt;NoWarn&gt;FW9001&lt;/NoWarn&gt;</c>) to use them.
/// </summary>
internal static class TwExperimental
{
    public const string DiagnosticId = "FW9001";
    public const string UrlFormat = "https://github.com/ModuShip/FormWind/blob/main/README.en.md#api-stability";
}
