using System;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace FormWind.Design;

/// <summary>
/// Visual Studio Designer support for <see cref="TwTabControl"/>: "Add page" / "Remove selected page" commands and a smart tag,
/// switching pages by clicking tabs at design time; only <see cref="TwTabPage"/>s can be dropped onto the control.
/// In the .NET 8+ out-of-process Designer the designer runs in the server process; the commands and smart tag show there too.
/// </summary>
internal class TwTabControlDesigner : ParentControlDesigner
{
    private DesignerVerbCollection? _verbs;
    private DesignerActionListCollection? _actionLists;

    private TwTabControl Tabs => (TwTabControl)Control;

    public override DesignerVerbCollection Verbs => _verbs ??= new DesignerVerbCollection
    {
        new DesignerVerb("Add page", (_, _) => AddPage()),
        new DesignerVerb("Remove selected page", (_, _) => RemovePage()),
    };

    public override DesignerActionListCollection ActionLists => _actionLists ??= new DesignerActionListCollection { new ActionList(this) };

    public override void Initialize(IComponent component)
    {
        base.Initialize(component);
        if (GetService(typeof(ISelectionService)) is ISelectionService selection)
            selection.SelectionChanged += OnSelectionChanged;
    }

    public override void InitializeNewComponent(IDictionary defaultValues)
    {
        base.InitializeNewComponent(defaultValues);
        // Araç kutusundan yeni bırakılan kontrol iki sayfayla başlar.
        AddPage();
        AddPage();
        Tabs.SelectedIndex = 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && GetService(typeof(ISelectionService)) is ISelectionService selection)
            selection.SelectionChanged -= OnSelectionChanged;
        base.Dispose(disposing);
    }

    // Yalnızca sayfalar bırakılabilir; diğer kontroller seçili sayfaya bırakılmalı.
    public override bool CanParent(Control control) => control is TwTabPage;

    // Sekme şeridine tıklamalar kontrole iletilir (sayfa değişir).
    protected override bool GetHitTest(Point point)
    {
        var client = Tabs.PointToClient(point);
        return Tabs.Strip.Bounds.Contains(client);
    }

    // Designer'da bir sayfa (ya da içindeki bir kontrol) seçilince o sayfa gösterilir.
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (sender is not ISelectionService selection) return;
        foreach (var item in selection.GetSelectedComponents())
        {
            for (var control = item as Control; control != null; control = control.Parent)
            {
                if (control is TwTabPage page && page.Parent == Tabs)
                {
                    Tabs.SelectedTab = page;
                    return;
                }
            }
        }
    }

    internal void AddPage()
    {
        if (GetService(typeof(IDesignerHost)) is not IDesignerHost host) return;
        using var transaction = host.CreateTransaction("Add page");
        var controls = TypeDescriptor.GetProperties(Tabs)["Controls"];
        RaiseComponentChanging(controls);
        var page = (TwTabPage)host.CreateComponent(typeof(TwTabPage));
        page.Text = "Page " + (Tabs.TabPages.Count + 1);
        Tabs.Controls.Add(page);
        RaiseComponentChanged(controls, null, null);
        Tabs.SelectedTab = page;
        transaction.Commit();
    }

    internal void RemovePage()
    {
        if (Tabs.SelectedTab is not TwTabPage page || GetService(typeof(IDesignerHost)) is not IDesignerHost host) return;
        using var transaction = host.CreateTransaction("Remove page");
        var controls = TypeDescriptor.GetProperties(Tabs)["Controls"];
        RaiseComponentChanging(controls);
        host.DestroyComponent(page);
        RaiseComponentChanged(controls, null, null);
        transaction.Commit();
    }

    /// <summary>Smart tag: style, add/remove page.</summary>
    private sealed class ActionList : DesignerActionList
    {
        private readonly TwTabControlDesigner _designer;

        public ActionList(TwTabControlDesigner designer) : base(designer.Component) => _designer = designer;

        public TwTabVariant Variant
        {
            get => _designer.Tabs.Variant;
            set => TypeDescriptor.GetProperties(_designer.Tabs)[nameof(TwTabControl.Variant)]!.SetValue(_designer.Tabs, value);
        }

        public void AddPage() => _designer.AddPage();

        public void RemovePage() => _designer.RemovePage();

        public override DesignerActionItemCollection GetSortedActionItems() => new DesignerActionItemCollection
        {
            new DesignerActionPropertyItem(nameof(Variant), "Style"),
            new DesignerActionMethodItem(this, nameof(AddPage), "Add page", true),
            new DesignerActionMethodItem(this, nameof(RemovePage), "Remove selected page", true),
        };
    }
}
