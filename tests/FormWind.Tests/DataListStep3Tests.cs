using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

[Collection(UiCollection.Name)]
public class DataListStep3Tests
{
    private static void Invoke(Control control, string method, EventArgs args) =>
        typeof(Control).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { args.GetType() }, null)!
            .Invoke(control, new object[] { args });

    private static void Select(TwDataList list, int index, Keys modifiers) =>
        typeof(TwDataList).GetMethod("SelectWithModifiers", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(list, new object[] { index, modifiers });

    private static TwDataList NewList(int count) =>
        new TwDataList { Size = new Size(300, 208), Padding = new Padding(4), ItemHeight = 40, ItemCount = count, SmoothScrolling = false };

    [Fact]
    public void Multi_select_with_ctrl_shift_and_select_all() => Sta.Run(() =>
    {
        using var list = NewList(1_000_000);
        list.SelectionMode = TwListSelectionMode.MultiExtended;
        int changes = 0;
        list.SelectionChanged += (_, _) => changes++;

        Select(list, 2, Keys.None);
        Select(list, 5, Keys.Shift);                      // 2..5
        Select(list, 9, Keys.Control);                    // + 9
        Select(list, 3, Keys.Control);                    // - 3
        Assert.Equal(new[] { 2, 4, 5, 9 }, list.SelectedIndices);
        Assert.Equal(3, list.SelectedIndex);              // imleç
        Assert.True(changes >= 4);

        Invoke(list, "OnKeyDown", new KeyEventArgs(Keys.Control | Keys.A));
        Assert.Equal(1_000_000, list.SelectedCount);      // tümü, milyon dizin tutmadan
        list.SetSelected(10, false);
        Assert.False(list.IsSelected(10));
        Assert.Equal(999_999, list.SelectedCount);

        list.SelectedIndex = 7;                           // programatik atama tek seçime döner
        Assert.Equal(new[] { 7 }, list.SelectedIndices);
        Invoke(list, "OnKeyDown", new KeyEventArgs(Keys.Shift | Keys.Down));
        Invoke(list, "OnKeyDown", new KeyEventArgs(Keys.Shift | Keys.Down));
        Assert.Equal(new[] { 7, 8, 9 }, list.SelectedIndices);

        list.ClearSelection();
        Assert.Equal(0, list.SelectedCount);
        Assert.True(list.AccessibilityObject.GetChild(1)!.State.HasFlag(AccessibleStates.MultiSelectable));
    });

    [Fact]
    public void Groups_offset_rows_and_keep_a_sticky_header() => Sta.Run(() =>
    {
        using var list = NewList(30);
        list.GroupHeaderHeight = 28;
        list.SetGroups(new[] { (0, "Bugün"), (3, "Dün"), (10, "Bu hafta") });
        Assert.Equal(3, list.GroupCount);
        Assert.Equal(1, list.GroupOf(5));

        // Satır 0, ilk başlığın altında; satır 3, ikinci başlığın altında.
        Assert.Equal(4 + 28, list.GetItemBounds(0).Top);
        Assert.Equal(4 + 28 + 3 * 40 + 28, list.GetItemBounds(3).Top);
        Assert.Equal(-1, list.IndexFromPoint(new Point(50, 4 + 10)));                    // başlığın üzeri
        Assert.Equal(3, list.IndexFromPoint(new Point(50, list.GetItemBounds(3).Top + 5)));
        Assert.Equal(30 * 40 + 3 * 28 + 8 - 208, list.MaxScrollOffset);

        // "Dün" grubunun ortasına kaydırınca başlığı yapışık kalır ve satıra tıklanamaz.
        list.ScrollTo(list.GetGroupHeaderBounds(1).Top + 60, animate: false);
        var bitmap = new Bitmap(list.Width, list.Height);
        list.DrawToBitmap(bitmap, new Rectangle(Point.Empty, list.Size));
        Assert.Equal(-1, list.IndexFromPoint(new Point(50, 4 + 5)));
        int top = list.TopIndex;
        Assert.Equal(1, list.GroupOf(top));
        bitmap.Dispose();

        list.EnsureVisible(12, animate: false);
        var row = list.GetItemBounds(12);
        Assert.True(row.Top >= 4 + 28 && row.Bottom <= 208 - 4);                          // yapışık başlığın altında
    });

    [Fact]
    public void Unloaded_rows_draw_skeletons_and_request_items() => Sta.Run(() =>
    {
        using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000) };
        var list = NewList(1000);
        form.Controls.Add(list);
        form.Show();
        var loaded = new bool[1000];
        list.IsItemLoaded = i => loaded[i];
        int drawn = 0;
        list.DrawItem += (_, _) => drawn++;
        TwItemsNeededEventArgs? request = null;
        list.ItemsNeeded += (_, e) => request = e;

        using (var bitmap = new Bitmap(list.Width, list.Height)) list.DrawToBitmap(bitmap, new Rectangle(Point.Empty, list.Size));
        Application.DoEvents();
        Assert.Equal(0, drawn);
        Assert.NotNull(request);
        Assert.Equal(0, request!.First);
        Assert.Equal(4, request.Last);

        for (int i = request.First; i <= request.Last; i++) loaded[i] = true;
        request = null;
        using (var bitmap = new Bitmap(list.Width, list.Height)) list.DrawToBitmap(bitmap, new Rectangle(Point.Empty, list.Size));
        Application.DoEvents();
        Assert.Equal(5, drawn);
        Assert.Null(request);
        form.Close();
    });

    // Farklı yükseklikte satırlar: çift satırlar 40, tek satırlar 80 px; gruplar ve kaydırma hesapları bunlara göre.
    [Fact]
    public void Item_heights_can_vary_per_row() => Sta.Run(() =>
    {
        using var list = NewList(10);
        list.ItemHeights = i => i % 2 == 0 ? 40 : 80;
        list.SetGroups(new[] { (0, "A"), (4, "B") });

        Assert.Equal(40, list.GetItemBounds(0).Height);
        Assert.Equal(4 + 28, list.GetItemBounds(0).Top);
        Assert.Equal(80, list.GetItemBounds(1).Height);
        Assert.Equal(4 + 28 + 40 + 80 + 40, list.GetItemBounds(3).Top);
        Assert.Equal(4 + 28 + 2 * 120 + 28, list.GetItemBounds(4).Top);           // ikinci grubun başlığından sonra
        Assert.Equal(1, list.IndexFromPoint(new Point(50, list.GetItemBounds(1).Top + 70)));
        Assert.Equal(2, list.IndexFromPoint(new Point(50, list.GetItemBounds(1).Bottom + 1)));
        Assert.Equal(5 * 120 + 2 * 28 + 8 - 208, list.MaxScrollOffset);

        list.EnsureVisible(9, animate: false);
        Assert.Equal(list.MaxScrollOffset, list.ScrollOffset);
        Assert.Equal(list.ClientSize.Height - 4, list.GetItemBounds(9).Bottom);

        list.ItemHeights = null;                                                       // sabit yüksekliğe dönüş
        Assert.Equal(40, list.GetItemBounds(1).Height);
        Assert.Equal(10 * 40 + 2 * 28 + 8 - 208, list.MaxScrollOffset);
    });
}
