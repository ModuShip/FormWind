#if NETFRAMEWORK
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// <c>FORMWIND_UIA=1</c> ile çalışır. UI Automation istemcisi süreç genelinde ayar değiştirdiğinden (sonraki testlerde metin
/// farklı çizilir) varsayılan çalıştırmada atlanır; CI ayrı bir adımda çalıştırır.
/// </summary>
public sealed class UiaFactAttribute : FactAttribute
{
    public UiaFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FORMWIND_UIA") != "1")
            Skip = "UI Automation testleri FORMWIND_UIA=1 ile ayrı çalıştırılır.";
    }
}

/// <summary>
/// Kontrolleri ekran okuyucuların ve UI test araçlarının kullandığı UI Automation istemcisiyle, dışarıdan doğrular.
/// Form kendi mesaj döngüsüyle ayrı bir thread'de açılır; sorgular test thread'inden yapılır.
/// </summary>
[Collection(UiCollection.Name)]
[Trait("Category", "UIAutomation")]
public sealed class UiAutomationTests : IDisposable
{
    private readonly Thread _ui;
    private readonly ManualResetEventSlim _shown = new ManualResetEventSlim();
    private Form? _form;
    private TwDataList? _list;
    private TwTabStrip? _tabs;
    private TwSelect? _select;
    private TwCalendar? _calendar;

    public UiAutomationTests()
    {
        _ui = new Thread(() =>
        {
            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = new CultureInfo("tr-TR");
            _form = new Form { Text = "UIA testi", StartPosition = FormStartPosition.Manual, Location = new Point(20, 20), ClientSize = new Size(640, 420), ShowInTaskbar = false };
            _list = new TwDataList { Bounds = new Rectangle(10, 10, 300, 200), ItemCount = 40, AccessibleName = "Siparişler", AccessibleItemText = i => "Sipariş " + (1000 + i), SmoothScrolling = false };
            _tabs = new TwTabStrip { Bounds = new Rectangle(330, 10, 300, 40), AccessibleName = "Bölümler" };
            _tabs.Tabs.AddRange(new[] { "Genel", "Adres", "Siparişler" });
            _select = new TwSelect { Bounds = new Rectangle(330, 60, 160, 30), AccessibleName = "Şehir" };
            _select.Items.AddRange(new[] { "İstanbul", "Ankara" });
            _select.SelectedIndex = 0;
            _calendar = new TwCalendar { Location = new Point(330, 100), AccessibleName = "Teslim", DisplayMonth = new DateTime(2026, 10, 1) };
            _form.Controls.AddRange(new Control[] { _list, _tabs, _select, _calendar });
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
        _form?.BeginInvoke((Action)(() => _form.Close()));
        _ui.Join(5_000);
        _shown.Dispose();
    }

    private AutomationElement Window => AutomationElement.FromHandle(Invoke(() => _form!.Handle));

    private T Invoke<T>(Func<T> func) => (T)_form!.Invoke(func);

    private AutomationElement Find(string name, ControlType type)
    {
        var condition = new AndCondition(
            new PropertyCondition(AutomationElement.NameProperty, name),
            new PropertyCondition(AutomationElement.ControlTypeProperty, type));
        // Kontroller formun doğrudan çocukları; Descendants sanal listenin yüzlerce satırını da dolaşır (yavaş).
        var element = Window.FindFirst(TreeScope.Children, condition);
        Assert.True(element != null, $"{type.ProgrammaticName} '{name}' UI Automation ağacında bulunamadı.");
        return element!;
    }

    /// <summary>Öğeyi seçer: MSAA köprüsü öğe türüne göre SelectionItem ya da Invoke (varsayılan eylem) sunar.</summary>
    private static void SelectItem(AutomationElement item)
    {
        if (item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selection)) ((SelectionItemPattern)selection).Select();
        else if (item.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke)) ((InvokePattern)invoke).Invoke();
        else Assert.Fail("Desteklenen desenler: " + string.Join(", ", item.GetSupportedPatterns().Select(p => p.ProgrammaticName)));
    }

    [UiaFact]
    public void Data_list_rows_are_list_items_and_selectable()
    {
        var list = Find("Siparişler", ControlType.List);
        var items = list.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
        Assert.Equal(40, items.Count);
        Assert.Equal("Sipariş 1007", items[7].Current.Name);

        SelectItem(items[7]);
        Assert.True(SpinWait.SpinUntil(() => Invoke(() => _list!.SelectedIndex) == 7, 3_000), "Seçim UI Automation ile yapılamadı.");
    }

    [UiaFact]
    public void Tabs_are_tab_items_with_selection()
    {
        var tabs = Find("Bölümler", ControlType.Tab);
        var items = tabs.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));
        Assert.Equal(new[] { "Genel", "Adres", "Siparişler" }, items.Cast<AutomationElement>().Select(e => e.Current.Name));

        SelectItem(items[2]);
        Assert.True(SpinWait.SpinUntil(() => Invoke(() => _tabs!.SelectedIndex) == 2, 3_000));
    }

    [UiaFact]
    public void Select_reports_its_value_and_calendar_its_days()
    {
        var select = Find("Şehir", ControlType.ComboBox);
        Assert.Equal("İstanbul", ((ValuePattern)select.GetCurrentPattern(ValuePattern.Pattern)).Current.Value);
        Invoke(() => _select!.SelectedIndex = 1);
        Assert.Equal("Ankara", ((ValuePattern)select.GetCurrentPattern(ValuePattern.Pattern)).Current.Value);

        var calendar = Find("Teslim", ControlType.Table);
        var day = calendar.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "15 Ekim 2026 Perşembe"));
        Assert.NotNull(day);
        Assert.NotNull(calendar.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "Sonraki ay")));
    }
}
#endif
