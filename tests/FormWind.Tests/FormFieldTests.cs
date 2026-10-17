using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class FormFieldTests
{
    private static void Invoke(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { args.GetType() }, null)!
            .Invoke(control, new object[] { args });

    [Theory]
    [InlineData("10000000146", true)]
    [InlineData("10000000147", false)]
    [InlineData("01234567890", false)]
    [InlineData("1234", false)]
    public void Tc_kimlik_checksum(string value, bool valid) => Assert.Equal(valid, TwValidators.TcKimlikNo(value) is null);

    [Theory]
    [InlineData("TR33 0006 1005 1978 6457 8413 26", true)]
    [InlineData("TR33 0006 1005 1978 6457 8413 27", false)]
    [InlineData("DE89 3704 0044 0532 0130 00", true)]
    [InlineData("TR12", false)]
    public void Iban_checksum(string value, bool valid) => Assert.Equal(valid, TwValidators.Iban(value) is null);

    [Theory]
    [InlineData("0532 123 45 67", true)]
    [InlineData("+90 532 123 45 67", true)]
    [InlineData("0212 123 45 67", false)]
    public void Turkish_mobile(string value, bool valid) => Assert.Equal(valid, TwValidators.TurkishMobile(value) is null);

    [Fact]
    public void Email_and_min_length()
    {
        Assert.Null(TwValidators.Email("ayse@firma.com"));
        Assert.NotNull(TwValidators.Email("ayse@firma"));
        Assert.NotNull(TwValidators.MinLength(3)("ab"));
        Assert.Null(TwValidators.MinLength(3)("abc"));
    }

    [Fact]
    public void Mask_accepts_only_editable_characters_and_reports_completion() => Sta.Run(() =>
    {
        using var box = new TwTextBox { Mask = TwMasks.Phone };
        Assert.True(box.HasMask);
        Assert.Equal("", box.Text);                 // boşken ipucu görünsün diye metin yok
        Assert.False(box.MaskCompleted);

        void Type(string text)
        {
            foreach (var c in text)
                typeof(TwTextBox).GetMethod("OnMaskKeyPress", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(box, new object?[] { box.InnerTextBox, new KeyPressEventArgs(c) });
        }

        Type("5a3");                                 // harf reddedilir
        Assert.Equal("53", box.UnmaskedText);
        Type("21234567");
        Assert.Equal("(532) 123 45 67", box.Text);
        Assert.Equal("5321234567", box.UnmaskedText);
        Assert.True(box.MaskCompleted);

        box.Text = "5551112233";               // sabit karakterler olmadan atama
        Assert.Equal("(555) 111 22 33", box.Text);
        box.Text = "";
        Assert.Equal("", box.UnmaskedText);

        box.Mask = "";
        Assert.False(box.HasMask);
    });

    [Fact]
    public void Field_validates_rules_and_shows_errors() => Sta.Run(() =>
    {
        using var form = new Form();
        using var provider = new TailwindProvider();
        var name = new TwField { LabelText = "Ad", Required = true, Width = 200 };
        var nameBox = new TwTextBox();
        name.Controls.Add(nameBox);
        var id = new TwField { LabelText = "T.C. kimlik no", Validator = TwValidators.TcKimlikNo, Width = 200 };
        var idBox = new TwTextBox { Mask = TwMasks.TcKimlikNo };
        id.Controls.Add(idBox);
        var mail = new TwField { LabelText = "E-posta", HintText = "İş adresiniz", Pattern = @"@firma\.com$", Width = 200 };
        var mailBox = new TwTextBox { Text = "ayse@gmail.com" };
        mail.Controls.Add(mailBox);
        form.Controls.AddRange(new Control[] { name, id, mail });
        provider.SetClasses(nameBox, "input");

        Assert.Same(nameBox, name.Editor);
        Assert.Equal("Ad *", name.CaptionLabel.Text);
        Assert.Equal("Ad", nameBox.AccessibleName);
        Assert.Equal("İş adresiniz", mail.MessageLabel.Text);

        Assert.False(provider.Validate(form));
        Assert.Equal("Bu alan zorunludur.", name.ErrorText);
        Assert.Null(id.ErrorText);                   // boş ve zorunlu değil
        Assert.Equal("Geçersiz biçim.", mail.ErrorText);
        Assert.Equal("Geçersiz biçim.", mail.MessageLabel.Text);
        Assert.Equal("Geçersiz biçim.", mailBox.AccessibleDescription);
        Assert.True(provider.HasErrors);

        // invalid: varyantı: input bileşeninin kırmızı kenarlığı
        Assert.Equal(ColorTranslator.FromHtml("#ef4444").ToArgb(), ((ITwChromeAccess)new ChromeProbe(nameBox)).BorderColor.ToArgb());

        nameBox.Text = "Ayşe";                       // düzenleme hatayı temizler
        Assert.Null(name.ErrorText);
        Assert.Null(provider.GetError(nameBox));

        idBox.Text = "10000000147";
        mailBox.Text = "ayse@firma.com";
        Assert.False(provider.Validate(form));
        Assert.Equal("Geçersiz T.C. kimlik numarası.", id.ErrorText);
        idBox.Text = "1000000014";
        Assert.False(provider.Validate(form));
        Assert.Equal("Eksik giriş.", id.ErrorText);
        idBox.Text = "10000000146";
        Assert.True(provider.Validate(form));
        Assert.False(provider.HasErrors);
    });

    [Fact]
    public void Field_part_classes_style_label_and_messages() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var field = new TwField { LabelText = "Ad", HintText = "ipucu", Width = 200 };
        field.Controls.Add(new TwTextBox());
        provider.SetClasses(field, "label:text-blue-700 label:font-semibold hint:text-gray-400 error:text-red-700");
        Assert.Equal(ColorTranslator.FromHtml("#1d4ed8"), field.CaptionLabel.ForeColor);
        Assert.Equal(ColorTranslator.FromHtml("#9ca3af"), field.MessageLabel.ForeColor);
        Assert.True(field.CaptionLabel.Font.Bold || field.CaptionLabel.Font.Name.Contains("Semibold"));
        field.ErrorText = "Hata";
        Assert.Equal(ColorTranslator.FromHtml("#b91c1c"), field.MessageLabel.ForeColor);
        Assert.True(field.GetPreferredSize(new Size(200, 0)).Height > field.Editor!.Height + field.CaptionLabel.PreferredHeight);
    });

    private interface ITwChromeAccess
    {
        Color BorderColor { get; }
    }

    // Kontrolün çizim bilgisinden (TwChrome) kenarlık rengini okur.
    private sealed class ChromeProbe : ITwChromeAccess
    {
        private readonly Control _control;

        public ChromeProbe(Control control) => _control = control;

        public Color BorderColor
        {
            get
            {
                var info = typeof(TwPanel).GetField("_chrome", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var chrome = (TwChrome)info.GetValue(_control)!;
                return chrome.BorderColor;
            }
        }
    }
}
