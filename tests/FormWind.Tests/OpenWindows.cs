using System.Linq;
using System.Windows.Forms;

namespace FormWind.Tests;

/// <summary>
/// The windows open now for leak checks: a form keeps its transition picture window hidden for its next transition, so an
/// idle picture window is not a window left open.
/// </summary>
internal static class OpenWindows
{
    public static int Count => Application.OpenForms.Cast<Form>().Count(f => f is not TwFadeOut.Ghost { InUse: false });
}
