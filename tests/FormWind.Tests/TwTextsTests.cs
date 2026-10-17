using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class TwTextsTests
{
    [Fact]
    public void Culture_selects_the_language()
    {
        Assert.Equal("OK", TwTexts.ForCulture(new CultureInfo("en-US")).Ok);
        Assert.Equal("OK", TwTexts.ForCulture(new CultureInfo("de-DE")).Ok);     // çevirisi olmayan diller İngilizce
        Assert.Equal("Tamam", TwTexts.ForCulture(new CultureInfo("tr-TR")).Ok);
        Assert.Equal("This field is required.", TwTexts.English.Required);
    }

    [Fact]
    public void Controls_and_validators_use_the_current_texts() => Sta.Run(() =>
    {
        var previous = TwTexts.Current;
        try
        {
            TwTexts.Current = new TwTexts { Required = "Pflichtfeld.", MinLength = "Mindestens {0} Zeichen.", Yes = "Ja", Page = "Seite {0} von {1}" };
            using var field = new TwField { Required = true };
            field.Controls.Add(new TwTextBox());
            Assert.Equal("Pflichtfeld.", field.Validate());
            Assert.Equal("Mindestens 3 Zeichen.", TwValidators.MinLength(3)("ab"));

            using var dialog = TwDialog.Create(new TwDialogOptions { Message = "?", Buttons = TwDialogButtons.YesNo });
            Assert.Contains("Ja", dialog.Buttons.Select(b => b.Text));

            using var pager = new TwPager { TotalItems = 40, PageSize = 20 };
            Assert.Equal("Seite 1 von 2", pager.AccessibilityObject.Value);

            // Kontrolün kendi metni TwTexts'in önüne geçer ve yalnızca o zaman Designer.cs'e yazılır.
            field.RequiredMessage = "Ad gerekli.";
            Assert.Equal("Ad gerekli.", field.Validate());
            var property = System.ComponentModel.TypeDescriptor.GetProperties(field)["RequiredMessage"]!;
            Assert.True(property.ShouldSerializeValue(field));
            property.ResetValue(field);
            Assert.False(property.ShouldSerializeValue(field));
        }
        finally
        {
            TwTexts.Current = previous;
        }
    });
}
