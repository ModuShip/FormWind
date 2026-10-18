using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FormWind.Design;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class AutoStyleTests
{
    private static Color Color(string hex) => ColorTranslator.FromHtml(hex);

    [Fact]
    public void Controls_without_classes_get_the_themes_components() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        var panel = new Panel();
        var text = new TextBox();
        var button = new Button();
        var own = new Button();
        var number = new TwNumberBox();
        var label = new Label();
        panel.Controls.AddRange(new Control[] { text, button, own, number });
        form.Controls.Add(panel);
        form.Controls.Add(label);
        provider.SetClasses(own, "btn-primary");

        provider.ContainerControl = form;
        provider.AutoStyle = true;

        Assert.Equal(Color("#f9fafb").ToArgb(), form.BackColor.ToArgb());                 // page: surface-50
        Assert.Equal(Color("#ffffff").ToArgb(), text.BackColor.ToArgb());                 // input (panelin içinde)
        Assert.Equal(Color("#ffffff").ToArgb(), button.BackColor.ToArgb());               // btn-ghost
        Assert.Equal(Color("#2563eb").ToArgb(), own.BackColor.ToArgb());                  // kendi class'ı kalır
        Assert.Equal("", provider.GetClasses(text));                                      // Designer.cs'e yazılmaz
        Assert.Equal("input", provider.GetAutomaticClasses(text));
        Assert.Null(provider.GetAutomaticClasses(own));                                   // bileşen adı: otomatik bileşen yok
        Assert.Null(provider.GetAutomaticClasses(label));                                 // etiket formun rengini alır
        Assert.Null(provider.GetAutomaticClasses(panel));
        // Bileşik kontrolün iç metin kutusu ayrıca stillenmez.
        Assert.All(number.Controls.Cast<Control>(), c => Assert.Null(provider.GetAutomaticClasses(c)));
        Assert.Equal("input", provider.GetAutomaticClasses(number));
    });

    [Fact]
    public void Own_classes_replace_and_clearing_them_restores_the_automatic_ones() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        var button = new Button();
        form.Controls.Add(button);
        provider.ContainerControl = form;
        provider.AutoStyle = true;

        provider.SetClasses(button, "bg-red-600 text-white mt-2");                        // utility'ler otomatik bileşenin üzerine
        Assert.Equal("bg-red-600 text-white mt-2", provider.GetClasses(button));
        Assert.Equal("btn-ghost", provider.GetAutomaticClasses(button));
        Assert.Equal(Color("#dc2626").ToArgb(), button.BackColor.ToArgb());
        Assert.Equal(1, provider.GetParsed(button)!.Resolve(TwState.None).BorderWidth);      // btn-ghost'un kenarlığı kalır

        provider.SetClasses(button, "btn-danger");                                        // bileşen adı otomatik bileşenin yerine geçer
        Assert.Null(provider.GetAutomaticClasses(button));
        Assert.Null(provider.GetParsed(button)!.Resolve(TwState.None).BorderWidth);

        provider.SetClasses(button, "");
        Assert.Equal("btn-ghost", provider.GetAutomaticClasses(button));
        Assert.Equal(Color("#ffffff").ToArgb(), button.BackColor.ToArgb());
    });

    [Fact]
    public void Controls_added_later_are_styled_and_turning_it_off_restores_them() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        var original = form.BackColor;
        provider.ContainerControl = form;
        provider.AutoStyle = true;

        var group = new GroupBox();
        form.Controls.Add(group);
        var text = new TextBox();
        group.Controls.Add(text);                                                         // gezilen konteynerin çocuğu
        Assert.Equal("input", provider.GetAutomaticClasses(text));

        provider.AutoStyle = false;
        Assert.Equal(original.ToArgb(), form.BackColor.ToArgb());
        Assert.Null(provider.GetAutomaticClasses(text));
        var late = new Button();
        form.Controls.Add(late);
        Assert.Null(provider.GetAutomaticClasses(late));                                  // kapalıyken dinlenmez
    });

    // Uygulamanın tema motoru (örn. ShareX) formun zeminini her kontrole kopyalar; yalnızca yazı rengi alan etiketler
    // formun yeni zeminini izlemeli, eski koyu zeminde kalmamalı.
    [Fact]
    public void Controls_that_copy_the_parents_background_follow_the_new_one() => Sta.Run(() =>
    {
        var appBack = Color("#272727");
        using var provider = new TailwindProvider();
        using var form = new Form { BackColor = appBack };
        var panel = new Panel { BackColor = appBack };
        var label = new Label { BackColor = appBack, ForeColor = Color("#e7e9ea") };
        var text = new TextBox { BackColor = appBack };
        var status = new Label { BackColor = Color("#16a34a") };                          // kendi rengi olan etiket
        panel.Controls.AddRange(new Control[] { label, text, status });
        form.Controls.Add(panel);

        provider.ContainerControl = form;
        provider.AutoStyle = true;

        Assert.Equal(Color("#f9fafb").ToArgb(), form.BackColor.ToArgb());                 // page
        Assert.Equal(form.BackColor.ToArgb(), panel.BackColor.ToArgb());
        Assert.Equal(form.BackColor.ToArgb(), label.BackColor.ToArgb());                  // torun da izler
        Assert.Equal(Color("#ffffff").ToArgb(), text.BackColor.ToArgb());                 // input
        Assert.Equal(Color("#16a34a").ToArgb(), status.BackColor.ToArgb());

        provider.DarkMode = true;
        Assert.Equal(Color("#111827").ToArgb(), label.BackColor.ToArgb());

        var late = new Label { BackColor = appBack };                                      // sonradan, uygulamanın renginde
        form.Controls.Add(late);
        Assert.Equal(form.BackColor.ToArgb(), late.BackColor.ToArgb());
    });

    [Fact]
    public void Follow_parent_back_color_can_be_turned_off() => Sta.Run(() =>
    {
        var appBack = Color("#272727");
        using var provider = new TailwindProvider { FollowParentBackColor = false };
        using var form = new Form { BackColor = appBack };
        var label = new Label { BackColor = appBack };
        form.Controls.Add(label);

        provider.ContainerControl = form;
        provider.AutoStyle = true;

        Assert.Equal(appBack.ToArgb(), label.BackColor.ToArgb());
    });

    [Fact]
    public void Inherit_copied_back_colors_compares_original_colors_and_keeps_other_defaults()
    {
        var appBack = Color("#272727");
        using var form = new Form { BackColor = appBack };
        var group = new GroupBox { BackColor = appBack };
        var inner = new Label { BackColor = appBack };
        var text = new TextBox { BackColor = appBack };                                   // sıfırlanınca Window olurdu
        var skipped = new CheckBox { BackColor = appBack };
        group.Controls.AddRange(new Control[] { inner, text, skipped });
        form.Controls.Add(group);

        int count = TailwindProvider.InheritCopiedBackColors(form, c => c is not CheckBox);

        Assert.Equal(2, count);                                                            // group, inner
        form.BackColor = Color("#f9fafb");
        Assert.Equal(form.BackColor.ToArgb(), inner.BackColor.ToArgb());
        Assert.Equal(appBack.ToArgb(), text.BackColor.ToArgb());
        Assert.Equal(appBack.ToArgb(), skipped.BackColor.ToArgb());
    }

    [Fact]
    public void Changing_the_theme_restyles_automatic_controls() => Sta.Run(() =>
    {
        var previous = TwTheme.Current;
        try
        {
            using var provider = new TailwindProvider();
            using var form = new Form();
            var button = new Button();
            var list = new ListView();
            form.Controls.Add(button);
            form.Controls.Add(list);
            provider.ContainerControl = form;
            provider.AutoStyle = true;
            Assert.Equal(Color("#ffffff").ToArgb(), button.BackColor.ToArgb());

            var material = new TwTheme(TwPreset.Material);
            material.SetAutoStyle("ListView", "");
            material.SetAutoStyle("Button", "btn-primary");
            TwTheme.Current = material;
            Assert.Equal(Color("#6750a4").ToArgb(), button.BackColor.ToArgb());
            Assert.Equal(Color("#fef7ff").ToArgb(), form.BackColor.ToArgb());
            Assert.Null(provider.GetAutomaticClasses(list));                              // eşlemesi kaldırıldı
        }
        finally
        {
            TwTheme.Current = previous;
        }
    });

    // Designer'ın ürettiği sıra: BeginInit, özellikler, kontroller, SetClasses, EndInit.
    [Fact]
    public void Initialization_applies_automatic_classes_once_at_the_end() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = new Form();
        ((ISupportInitialize)provider).BeginInit();
        provider.AutoStyle = true;
        provider.ContainerControl = form;
        var text = new TextBox();
        var save = new Button();
        form.Controls.Add(text);
        form.Controls.Add(save);
        provider.SetClasses(save, "btn-primary");
        Assert.Null(provider.GetAutomaticClasses(text));
        ((ISupportInitialize)provider).EndInit();

        Assert.Equal("input", provider.GetAutomaticClasses(text));
        Assert.Equal("btn-primary", provider.GetClasses(save));
        Assert.Equal(Color("#2563eb").ToArgb(), save.BackColor.ToArgb());
    });

    [Fact]
    public void Designer_sets_the_form_and_does_not_serialize_automatic_values() => Sta.Run(() =>
    {
        using var surface = new DesignSurface(typeof(Form));
        var host = (IDesignerHost)surface.GetService(typeof(IDesignerHost))!;
        using var actions = new DesignerActionService(surface);
        var form = (Form)host.RootComponent;
        var provider = (TailwindProvider)host.CreateComponent(typeof(TailwindProvider), "tw");
        Assert.Same(form, provider.ContainerControl);                                     // ErrorProvider gibi

        provider.AutoStyle = true;
        var text = (TextBox)host.CreateComponent(typeof(TextBox), "txt");
        form.Controls.Add(text);
        Assert.Equal("input", provider.GetAutomaticClasses(text));
        var properties = TypeDescriptor.GetProperties(text);
        Assert.Equal("", properties["Classes"]!.GetValue(text));
        Assert.False(properties["BackColor"]!.ShouldSerializeValue(text));                // önizleme Designer.cs'e yazılmaz

        // Smart tag otomatik class'ları gösterir; oluşturucu onlardan başlar.
        var list = actions.GetComponentActions(text).Cast<DesignerActionList>().OfType<TailwindProviderDesigner.ClassesActionList>().Single();
        Assert.Contains(list.GetSortedActionItems().OfType<DesignerActionTextItem>(), i => i.DisplayName == "AutoStyle: input");
        Assert.Equal("input", list.ThemeComponent);
        list.Background = "bg-red-50";                                                   // otomatik bileşenin üzerine eklenir
        Assert.Equal("bg-red-50", provider.GetClasses(text));
        Assert.Equal("input", provider.GetAutomaticClasses(text));
        Assert.Equal("bg-red-50", list.Background);
    });
}
