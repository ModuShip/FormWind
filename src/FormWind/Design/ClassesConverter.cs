using System;
using System.ComponentModel;
using System.Linq;

namespace FormWind.Design;

/// <summary>
/// Drop-down for the <c>Classes</c> field that suggests the theme's components (btn-primary, card, input …); free text can be
/// typed too. Picking a component replaces the field's current component and keeps its other classes: with
/// <c>btn-ghost px-6</c> in the field the list offers <c>btn-primary px-6</c>, <c>card px-6</c> and so on.
/// TypeConverters also work in the .NET 8+ out-of-process Designer, where <see cref="ClassesEditor"/> does not open.
/// </summary>
internal sealed class ClassesConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var theme = TwDesignTheme.Of(context);
        var current = CurrentValue(context);
        return new StandardValuesCollection(theme.Components.Keys
            .OrderBy(k => k, StringComparer.Ordinal)
            .Select(component => TwClassCategories.WithComponent(theme, current, component))
            .ToArray());
    }

    // Çoklu seçimde (Instance bir dizi) değer ortak değildir; liste yalın bileşen adlarını gösterir.
    private static string? CurrentValue(ITypeDescriptorContext? context)
    {
        if (context?.PropertyDescriptor is not PropertyDescriptor property || context.Instance is null or Array) return null;
        try
        {
            return property.GetValue(context.Instance) as string;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidCastException or NotSupportedException)
        {
            return null;
        }
    }
}
