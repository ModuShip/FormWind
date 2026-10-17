using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class ListTreeTests
{
    private static void Invoke(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { args.GetType() }, null)!
            .Invoke(control, new object[] { args });

    private sealed class City
    {
        public string Name { get; set; } = "";
        public override string ToString() => "City";
    }

    [Fact]
    public void List_box_tracks_items_and_display_member() => Sta.Run(() =>
    {
        using var list = new TwListBox { Size = new Size(200, 200) };
        list.Items.Add(new City { Name = "İzmir" });
        list.Items.Add(new City { Name = "Ankara" });
        Assert.Equal(2, list.ItemCount);
        Assert.Equal("City", list.GetItemText(0));
        list.DisplayMember = "Name";
        Assert.Equal("Ankara", list.GetItemText(1));
        Assert.Equal(1, list.FindString("an"));

        list.SelectedIndex = 1;
        Assert.Equal("Ankara", ((City)list.SelectedItem!).Name);
        list.Items.RemoveAt(0);
        Assert.Equal(1, list.ItemCount);
        Assert.Equal("Sipariş", new TwListBox { Items = { "Sipariş" } }.GetItemText(0));
    });

    [Fact]
    public void Checked_list_toggles_with_mouse_space_and_tracks_duplicates() => Sta.Run(() =>
    {
        using var list = new TwCheckedListBox { Size = new Size(200, 200), SmoothScrolling = false };
        foreach (var item in new[] { "A", "B", "A", "C" }) list.Items.Add(item);
        int events = 0;
        list.ItemCheck += (_, _) => events++;

        var box = list.GetCheckBoxBounds(2);
        Invoke(list, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, box.X + 2, box.Y + 2, 0));
        Assert.Equal(new[] { 2 }, list.CheckedIndices);          // aynı metinli 0. satır işaretlenmez
        Assert.Equal(2, list.SelectedIndex);

        list.SelectionMode = TwListSelectionMode.MultiExtended;
        list.SetSelected(3, true);
        Invoke(list, "OnKeyDown", new KeyEventArgs(Keys.Space));  // 2 işaretli → seçili satırlar kaldırılır
        Assert.Empty(list.CheckedIndices);

        list.SetItemChecked(1, true);
        list.Items.Insert(0, "Z");                                // işaret satırla birlikte kayar
        Assert.Equal(new[] { 2 }, list.CheckedIndices);
        Assert.Equal(new object[] { "B" }, list.CheckedItems);
        Assert.Equal(3, events);                                  // zaten işaretsiz satır olay üretmez
    });

    [Fact]
    public void Tree_expands_collapses_and_navigates_with_keys() => Sta.Run(() =>
    {
        using var tree = new TwTreeView { Size = new Size(250, 300), SmoothScrolling = false };
        var docs = new TwTreeNode("Belgeler", "folder");
        var year = docs.Nodes.Add("2026", "folder");
        year.Nodes.Add("Ekim.pdf", "file");
        docs.Nodes.Add("Notlar.txt", "file");
        docs.Expand();                                            // ağaca eklenmeden önce: durum kaydedilir
        tree.Nodes.Add(docs);
        tree.Nodes.Add("Resimler", "image").HasLazyChildren = true;

        Assert.Equal(new[] { "Belgeler", "2026", "Notlar.txt", "Resimler" }, tree.VisibleNodes.Select(n => n.Text));
        Assert.Equal("Belgeler/2026/Ekim.pdf", year.Nodes[0].FullPath);

        tree.SelectedNode = year.Nodes[0];                        // seçmek dalı açar
        Assert.True(year.IsExpanded);
        Assert.Equal(5, tree.ItemCount);

        Invoke(tree, "OnKeyDown", new KeyEventArgs(Keys.Left));  // yaprak: üst düğüme
        Assert.Same(year, tree.SelectedNode);
        Invoke(tree, "OnKeyDown", new KeyEventArgs(Keys.Left));  // açık: kapatır
        Assert.False(year.IsExpanded);
        Invoke(tree, "OnKeyDown", new KeyEventArgs(Keys.Right)); // kapalı: açar
        Invoke(tree, "OnKeyDown", new KeyEventArgs(Keys.Right)); // açık: ilk çocuğa
        Assert.Equal("Ekim.pdf", tree.SelectedNode!.Text);

        docs.Collapse();                                          // seçim kapanan dala taşınır
        Assert.Same(docs, tree.SelectedNode);

        // Tembel yükleme: ilk açılışta çocuklar eklenir.
        var pictures = tree.Nodes[1];
        tree.BeforeExpand += (_, e) =>
        {
            if (!e.Node.HasLazyChildren) return;
            e.Node.HasLazyChildren = false;
            e.Node.Nodes.Add("tatil.jpg", "image");
        };
        var arrow = tree.GetExpanderBounds(tree.VisibleNodes.ToList().IndexOf(pictures));
        Invoke(tree, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, arrow.X + 4, arrow.Y + 4, 0));
        Assert.True(pictures.IsExpanded);
        Assert.Equal("tatil.jpg", tree.VisibleNodes.Last().Text);

        var accessible = tree.AccessibilityObject;
        Assert.Equal(AccessibleRole.Outline, accessible.Role);
        var node = accessible.GetChild(1)!;
        Assert.Equal(AccessibleRole.OutlineItem, node.Role);
        Assert.True(node.State.HasFlag(AccessibleStates.Expanded));
        Assert.Equal("1", accessible.GetChild(2)!.Value);
    });
}
