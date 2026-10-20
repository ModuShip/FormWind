using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// KeePass 2.61.1 denemesi: entegrasyonun çoğu her forma AutoStyle'lı sağlayıcı bağlayan 44 satırlık bir yardımcı sınıftı
// (GlobalWindowManager'a bir çağrı ve koyu tema anahtarı). TwAutoTheme.Install bunu tek satıra indirir.
[Collection(UiCollection.Name)]
public class TwAutoThemeTests
{
    private static Color Color(string hex) => ColorTranslator.FromHtml(hex);

    // Var olmayan tema dosyası: testler TwTheme.Current'ı değiştirmez.
    private static readonly string NoThemeFile = Path.Combine(Path.GetTempPath(), "formwind-no-theme-" + Guid.NewGuid().ToString("N") + ".json");

    private static Form NewForm() => new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };

    private static void WithAutoTheme(Action action) => Sta.Run(() =>
    {
        TwAutoTheme.Install(NoThemeFile);
        try
        {
            action();
        }
        finally
        {
            TwAutoTheme.Uninstall();
            TwAutoTheme.DarkMode = false;
            TwAutoTheme.Filter = null;
        }
    });

    [Fact]
    public void Forms_are_styled_after_load_and_before_they_appear() => WithAutoTheme(() =>
    {
        using var form = NewForm();
        var text = new TextBox();
        form.Controls.Add(text);
        Button? added = null;
        form.Load += (_, _) => form.Controls.Add(added = new Button { Text = "Load'da eklendi" });
        bool styledWhenShown = false;
        form.VisibleChanged += (_, _) =>
        {
            if (form.Visible) styledWhenShown = TwAutoTheme.GetProvider(form) != null;
        };
        form.Show();

        var provider = TwAutoTheme.GetProvider(form);
        Assert.NotNull(provider);
        Assert.True(styledWhenShown);
        Assert.Equal("input", provider!.GetAutomaticClasses(text));
        Assert.Equal("btn-ghost", provider.GetAutomaticClasses(added!));                   // Load'dan sonra
        Assert.Equal(Color("#f9fafb").ToArgb(), form.BackColor.ToArgb());                   // page
        form.Close();
    });

    [Fact]
    public void Dark_mode_switches_every_form_and_applies_to_new_ones() => WithAutoTheme(() =>
    {
        using var first = NewForm();
        first.Show();
        TwAutoTheme.DarkMode = true;
        Assert.True(TwAutoTheme.GetProvider(first)!.DarkMode);
        Assert.Equal(Color("#111827").ToArgb(), first.BackColor.ToArgb());                  // dark:bg-surface-900

        using var second = NewForm();
        second.Show();
        Assert.True(TwAutoTheme.GetProvider(second)!.DarkMode);
        first.Close();
        second.Close();
    });

    [Fact]
    public void Forms_with_their_own_provider_and_filtered_forms_are_left_alone() => WithAutoTheme(() =>
    {
        using var own = new OwnProviderForm { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        own.Show();
        Assert.Null(TwAutoTheme.GetProvider(own));

        TwAutoTheme.Filter = f => f.Text != "Hariç";
        using var excluded = NewForm();
        excluded.Text = "Hariç";
        excluded.Show();
        Assert.Null(TwAutoTheme.GetProvider(excluded));
        own.Close();
        excluded.Close();
    });

    [Fact]
    public void Form_attached_lets_the_app_give_single_controls_classes() => WithAutoTheme(() =>
    {
        using var form = NewForm();
        var save = new Button { Name = "save" };
        form.Controls.Add(save);
        EventHandler<TwAutoThemeEventArgs> handler = (_, e) =>
        {
            if (e.Form == form) e.Provider.SetClasses(save, "btn-primary");
        };
        TwAutoTheme.FormAttached += handler;
        try
        {
            form.Show();
        }
        finally
        {
            TwAutoTheme.FormAttached -= handler;
        }
        Assert.Equal(Color("#2563eb").ToArgb(), save.BackColor.ToArgb());
        form.Close();
    });

    [Fact]
    public void Disposed_forms_release_their_provider_and_uninstall_restores_the_look() => Sta.Run(() =>
    {
        TwAutoTheme.Install(NoThemeFile);
        Form form;
        TextBox text;
        try
        {
            using (var closed = NewForm())
            {
                closed.Show();
                Assert.NotNull(TwAutoTheme.GetProvider(closed));
                closed.Close();
                Assert.Null(TwAutoTheme.GetProvider(closed));                               // Close dispose eder
            }

            form = NewForm();
            text = new TextBox();
            form.Controls.Add(text);
            form.Show();
            Assert.Equal(Color("#f9fafb").ToArgb(), form.BackColor.ToArgb());
        }
        finally
        {
            TwAutoTheme.Uninstall();
        }
        Assert.False(TwAutoTheme.IsInstalled);
        Assert.Equal(SystemColors.Control.ToArgb(), form.BackColor.ToArgb());
        Assert.Equal(SystemColors.Window.ToArgb(), text.BackColor.ToArgb());

        using var later = NewForm();                                                          // kaldırıldıktan sonra açılan
        later.Show();
        Assert.Null(TwAutoTheme.GetProvider(later));
        later.Close();
        form.Dispose();
    });

    // Open Live Writer gibi pencerelerini ayrı UI iş parçacıklarında açan uygulamalar: o iş parçacığının formları da stillenir.
    [Fact]
    public void Forms_on_other_ui_threads_are_styled() => WithAutoTheme(() =>
    {
        Form? other = null;
        var shown = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            other = NewForm();
            other.Shown += (_, _) => shown.Set();
            Application.Run(other);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try
        {
            Assert.True(shown.Wait(5000));
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (TwAutoTheme.GetProvider(other!) is null && DateTime.UtcNow < deadline) Thread.Sleep(50);
            Assert.NotNull(TwAutoTheme.GetProvider(other!));

            // Aynı iş parçacığında sonradan açılan form kancayla, görünmeden önce stillenir.
            bool styledWhenShown = false;
            other!.Invoke(new Action(() =>
            {
                var second = NewForm();
                second.VisibleChanged += (_, _) => styledWhenShown |= second.Visible && TwAutoTheme.GetProvider(second) != null;
                second.Show();
                second.Close();
            }));
            Assert.True(styledWhenShown);
        }
        finally
        {
            other?.Invoke(new Action(other.Close));
            thread.Join(5000);
        }
    });

    private sealed class OwnProviderForm : Form
    {
        private readonly TailwindProvider _tw = new TailwindProvider();

        public OwnProviderForm()
        {
            _tw.SetClasses(this, "page");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tw.Dispose();
            base.Dispose(disposing);
        }
    }
}
