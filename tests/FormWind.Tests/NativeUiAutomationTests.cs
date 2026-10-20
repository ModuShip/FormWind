#if NETFRAMEWORK
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// FormWind'in Windows'un yerine çizdiği native kontroller (sekme şeridi, devre dışı yazı, koyu DropDownList kutusu, devre
/// dışı liste, PropertyGrid) erişilebilirlik ağacında Windows'un verdiği ad, rol, durum ve değeri korumalı: çizim yalnızca
/// görüntüyü değiştirir. <c>FORMWIND_UIA=1</c> ile çalışır (bkz. <see cref="UiaFactAttribute"/>).
/// </summary>
[Collection(UiCollection.Name)]
[Trait("Category", "UIAutomation")]
public sealed class NativeUiAutomationTests : IDisposable
{
    private readonly Thread _ui;
    private readonly ManualResetEventSlim _shown = new ManualResetEventSlim();
    private Form? _form;
    private TabControl? _tabs;
    private TailwindProvider? _provider;

    public NativeUiAutomationTests()
    {
        _ui = new Thread(() =>
        {
            _form = new Form { Text = "Native UIA testi", StartPosition = FormStartPosition.Manual, Location = new Point(20, 20), ClientSize = new Size(640, 420), ShowInTaskbar = false };
            _tabs = new TabControl { Bounds = new Rectangle(10, 10, 300, 150), TabPages = { new TabPage("Genel"), new TabPage("Gelişmiş") } };
            var check = new CheckBox { Bounds = new Rectangle(10, 170, 200, 24), Text = "Devre dışı seçenek", Checked = true, Enabled = false };
            var label = new Label { Bounds = new Rectangle(10, 200, 200, 24), Text = "Devre dışı etiket", Enabled = false };
            var combo = new ComboBox { Bounds = new Rectangle(330, 10, 200, 24), DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Şehir" };
            combo.Items.AddRange(new object[] { "İstanbul", "Ankara" });
            combo.SelectedIndex = 1;
            var list = new ListView { Bounds = new Rectangle(330, 50, 200, 100), View = View.Details, AccessibleName = "Girdiler", Enabled = false, Columns = { "Ad" } };
            list.Items.Add("Bir");
            list.Items.Add("İki");
            _form.Controls.AddRange(new Control[] { _tabs, check, label, combo, list });
            _provider = new TailwindProvider { DarkMode = true };
            _provider.SetClasses(_form, "bg-gray-900 text-gray-100");
            _provider.SetClasses(_tabs, "tab-control");
            _provider.SetClasses(check, "text-gray-200");
            _provider.SetClasses(label, "text-gray-200");
            _provider.SetClasses(combo, "bg-gray-800 text-gray-100");
            _provider.SetClasses(list, "bg-gray-800 text-gray-100");
            _form.Shown += (_, _) => _shown.Set();
            Application.Run(_form);
        });
        _ui.SetApartmentState(ApartmentState.STA);
        _ui.IsBackground = true;
        _ui.Start();
        Assert.True(_shown.Wait(15_000), "Form açılmadı.");
    }

    public void Dispose()
    {
        _form?.BeginInvoke((Action)(() =>
        {
            _form.Close();
            _provider?.Dispose();
        }));
        _ui.Join(5_000);
        _shown.Dispose();
    }

    private AutomationElement Window => AutomationElement.FromHandle(Invoke(() => _form!.Handle));

    private T Invoke<T>(Func<T> func) => (T)_form!.Invoke(func);

    private AutomationElement Find(string name, ControlType type)
    {
        var element = Window.FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.NameProperty, name),
            new PropertyCondition(AutomationElement.ControlTypeProperty, type)));
        Assert.True(element != null, $"{type.ProgrammaticName} '{name}' UI Automation ağacında bulunamadı.");
        return element!;
    }

    [UiaFact]
    public void Painted_tab_strip_keeps_its_tab_items_and_selection()
    {
        Assert.True(Invoke(() => TabControlApplier.IsPainted(_tabs!)));
        var tab = Window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Tab));
        Assert.NotNull(tab);
        var items = tab!.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
        Assert.Equal(new[] { "Genel", "Gelişmiş" }, items.Cast<AutomationElement>().Select(e => e.Current.Name));
        Assert.True(((SelectionItemPattern)items[0].GetCurrentPattern(SelectionItemPattern.Pattern)).Current.IsSelected);

        ((SelectionItemPattern)items[1].GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        Assert.True(SpinWait.SpinUntil(() => Invoke(() => _tabs!.SelectedIndex) == 1, 3_000));
    }

    [UiaFact]
    public void Redrawn_disabled_text_keeps_name_and_state()
    {
        var check = Find("Devre dışı seçenek", ControlType.CheckBox);
        Assert.False(check.Current.IsEnabled);
        Assert.Equal(ToggleState.On, ((TogglePattern)check.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState);
        Assert.False(Find("Devre dışı etiket", ControlType.Text).Current.IsEnabled);
    }

    [UiaFact]
    public void Painted_drop_down_list_and_disabled_list_keep_value_and_items()
    {
        var combo = Find("Şehir", ControlType.ComboBox);
        // DropDownList kutusunun Windows proxy'si değeri seçim olarak verir (ValuePattern yok).
        var selection = ((SelectionPattern)combo.GetCurrentPattern(SelectionPattern.Pattern)).Current.GetSelection();
        Assert.Equal("Ankara", Assert.Single(selection).Current.Name);

        // Details görünümündeki liste Windows'un proxy'sinde DataGrid (öğeleri DataItem), diğer görünümlerde List'tir.
        var list = Window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Girdiler"));
        Assert.NotNull(list);
        Assert.Contains(list!.Current.ControlType, new[] { ControlType.DataGrid, ControlType.List });
        Assert.False(list.Current.IsEnabled);
        var items = list.FindAll(TreeScope.Children, new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem),
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)));
        Assert.Equal(new[] { "Bir", "İki" }, items.Cast<AutomationElement>().Select(e => e.Current.Name));
    }
}
#endif
