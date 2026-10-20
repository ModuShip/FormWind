using System.Diagnostics.CodeAnalysis;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// A control's values before styles were applied. They are restored when a class is removed or the state changes
/// (e.g. when hover ends).
/// </summary>
[Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
public sealed class TwOriginal
{
    // "Bu değer açıkça atanmış mı?" sorusu WinForms'un kendi ShouldSerializeX metotlarıyla yanıtlanır.
    // TypeDescriptor.ShouldSerializeValue, .NET Framework'te başka iş parçacıkları aynı anda tip bilgisini
    // önbelleğe alırken yanlışlıkla true dönebiliyor (gözlemlendi: yeni bir Button'ın ForeColor'ı "atanmış" sayıldı).
    private static readonly MethodInfo? ShouldSerializeBackColor = Find("ShouldSerializeBackColor");
    private static readonly MethodInfo? ShouldSerializeForeColor = Find("ShouldSerializeForeColor");
    private static readonly MethodInfo? ShouldSerializeFont = Find("ShouldSerializeFont");
    private static readonly MethodInfo? ShouldSerializeCursor = Find("ShouldSerializeCursor");
    private static readonly object DescriptorGate = new object();

    private TwOriginal() { }

    /// <summary><c>null</c>: the value is not set explicitly and is inherited from the parent (restored with Reset).</summary>
    public Color? BackColor { get; private set; }

    public Color? ForeColor { get; private set; }
    public Font? Font { get; private set; }

    /// <summary>The control's DPI when <see cref="Font"/> was captured; the font is scaled by this ratio when the screen changes.</summary>
    public int FontDpi { get; private set; } = 96;
    public Cursor? Cursor { get; private set; }
    public Padding Padding { get; private set; }
    public Padding Margin { get; private set; }
    public FlatStyle? FlatStyle { get; private set; }
    public int? FlatBorderSize { get; private set; }

    /// <summary>A button's <c>UseVisualStyleBackColor</c>; <c>null</c> when it is not set explicitly.</summary>
    public bool? UseVisualStyleBackColor { get; private set; }

    /// <summary>Whether the ShouldSerializeX methods were found directly (otherwise a locked TypeDescriptor is used).</summary>
    internal static bool UsesDirectChecks =>
        ShouldSerializeBackColor != null && ShouldSerializeForeColor != null
        && ShouldSerializeFont != null && ShouldSerializeCursor != null;

    /// <param name="control">The control to read the values from.</param>
    /// <param name="designMode">
    /// At design time, colors and fonts that look explicitly set were most likely serialized from an earlier preview,
    /// so they are all treated as "inherited".
    /// </param>
    public static TwOriginal Capture(Control control, bool designMode)
    {
        bool IsSet(MethodInfo? method, string property) => !designMode && IsExplicit(control, method, property);

        var original = new TwOriginal
        {
            BackColor = IsSet(ShouldSerializeBackColor, nameof(Control.BackColor)) ? control.BackColor : null,
            ForeColor = IsSet(ShouldSerializeForeColor, nameof(Control.ForeColor)) ? control.ForeColor : null,
            Font = IsSet(ShouldSerializeFont, nameof(Control.Font)) ? control.Font : null,
            Cursor = IsSet(ShouldSerializeCursor, nameof(Control.Cursor)) ? control.Cursor : null,
            Padding = control.Padding,
            Margin = control.Margin,
            FontDpi = TwDpi.DeviceDpi(control),
        };

        if (control is ButtonBase button)
        {
            original.FlatStyle = button.FlatStyle;
            original.FlatBorderSize = button.FlatAppearance.BorderSize;
            if (IsExplicit(control, null, nameof(ButtonBase.UseVisualStyleBackColor))) original.UseVisualStyleBackColor = button.UseVisualStyleBackColor;
        }

        return original;
    }

    /// <summary>Whether the property is set explicitly on the control (not inherited from the parent).</summary>
    internal static bool IsExplicit(Control control, string property) => property switch
    {
        nameof(Control.BackColor) => IsExplicit(control, ShouldSerializeBackColor, property),
        nameof(Control.ForeColor) => IsExplicit(control, ShouldSerializeForeColor, property),
        nameof(Control.Font) => IsExplicit(control, ShouldSerializeFont, property),
        nameof(Control.Cursor) => IsExplicit(control, ShouldSerializeCursor, property),
        _ => IsExplicit(control, null, property),
    };

    private static bool IsExplicit(Control control, MethodInfo? method, string property)
    {
        if (method != null) return (bool)method.Invoke(control, null)!;
        lock (DescriptorGate)
            return TypeDescriptor.GetProperties(control)[property]?.ShouldSerializeValue(control) ?? false;
    }

    private static MethodInfo? Find(string name) =>
        typeof(Control).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, System.Type.EmptyTypes, null);
}
