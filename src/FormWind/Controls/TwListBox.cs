using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Replaces ListBox: a <see cref="TwDataList"/> that holds items. Objects added to <see cref="Items"/> are shown with
/// <see cref="DisplayMember"/> (or <c>ToString()</c>); virtual drawing, multi-selection, groups and styles come from TwDataList.
/// A <see cref="TwListItem"/> is drawn with the built-in row template (status dot, icon or score ring, title and muted line,
/// value and badge on the right); rows with a second line get taller on their own.
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(SelectedIndexChanged))]
[DefaultProperty(nameof(Items))]
[Description("Stillenebilir liste kutusu.")]
public class TwListBox : TwDataList
{
    private readonly BindingList<object> _items = new BindingList<object>();
    private string _displayMember = "";

    public TwListBox()
    {
        ItemHeight = 32;
        ItemText = GetItemText;
        AccessibleItemText = GetAccessibleText;
        ItemHeights = TemplateHeight;
        _items.ListChanged += (_, _) =>
        {
            if (_updating > 0)
            {
                _changedWhileUpdating = true;
                return;
            }
            ApplyItemChanges();
        };
    }

    private int _updating;
    private bool _changedWhileUpdating;

    private void ApplyItemChanges()
    {
        ItemCount = _items.Count;
        if (ItemHeights == (Func<int, int>)TemplateHeight) InvalidateItemHeights();
        Invalidate();
    }

    /// <summary>
    /// Stops the list from updating its rows (count, heights, painting) for each change to <see cref="Items"/> until
    /// <see cref="EndUpdate"/>; adding many items is then one update. Calls nest.
    /// </summary>
    public void BeginUpdate() => _updating++;

    /// <summary>Ends <see cref="BeginUpdate"/>; the last matching call applies the changes made since.</summary>
    public void EndUpdate()
    {
        if (_updating == 0) return;
        if (--_updating > 0 || !_changedWhileUpdating) return;
        _changedWhileUpdating = false;
        ApplyItemChanges();
    }

    // İki satırlı şablon satırı (alt satır ya da değer açıklaması olan TwListItem) en az iki satır yüksekliğindedir.
    private int TemplateHeight(int index) =>
        index < _items.Count && _items[index] is TwListItem { IsTwoLine: true } ? Math.Max(ItemHeight, TwoLineHeight) : ItemHeight;

    // Ölçüm her satır için tekrarlanmasın diye yazı tipi ve DPI başına bir kez alınır.
    private (Font? Font, int Dpi, int Height) _twoLine;

    private int TwoLineHeight
    {
        get
        {
            int dpi = TwDpi.DeviceDpi(this);
            if (_twoLine.Font != Font || _twoLine.Dpi != dpi) _twoLine = (Font, dpi, TwListItemPainter.TwoLineHeight(this));
            return _twoLine.Height;
        }
    }

    private string GetAccessibleText(int index) =>
        index < _items.Count && _items[index] is TwListItem item ? item.AccessibleText : GetItemText(index);

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        InvalidateItemHeights();
    }

    /// <summary>Draws <see cref="TwListItem"/> rows with the template unless <see cref="TwDataList.DrawItem"/> is handled.</summary>
    protected override void OnDrawItem(TwDataListDrawEventArgs e)
    {
        if (!HasDrawItemHandler && e.Index < _items.Count && _items[e.Index] is TwListItem item)
        {
            TwListItemPainter.Paint(this, e, item, Colors.Accent,
                _hot.Index == e.Index ? _hot.Part : TwListItemPart.None, _pressed.Index == e.Index ? _pressed.Part : TwListItemPart.None);
            return;
        }
        base.OnDrawItem(e);
    }

    /// <summary>
    /// When the user flips a <see cref="TwListItem"/>'s toggle icon (click, or Space on the selected row) or
    /// <see cref="ToggleItem"/> is called; <see cref="TwListItem.IsToggled"/> already has the new value.
    /// </summary>
    public event EventHandler<TwListItemEventArgs>? ItemToggle;

    /// <summary>
    /// When the user presses a <see cref="TwListItem"/>'s action button (click, or Ctrl+Enter on the selected row) or
    /// <see cref="PerformItemAction"/> is called.
    /// </summary>
    public event EventHandler<TwListItemEventArgs>? ItemActionClick;

    private (int Index, TwListItemPart Part) _hot = (-1, TwListItemPart.None);
    private (int Index, TwListItemPart Part) _pressed = (-1, TwListItemPart.None);

    private TwListItem? ItemAt(int index) => index >= 0 && index < _items.Count ? _items[index] as TwListItem : null;

    // Şablonun satır yerleşimi; DrawItem'ı olan listede ya da TwListItem olmayan satırda yoktur.
    private TwListItemLayout? LayoutOf(int index)
    {
        if (HasDrawItemHandler || this is TwCheckedListBox || ItemAt(index) is not TwListItem item) return null;
        return TwListItemPainter.Layout(this, item, GetItemBounds(index), Colors.Back.GetBrightness() < 0.5f);
    }

    /// <summary>The toggle icon's clickable area of a <see cref="TwListItem"/> row (list coordinates); empty when the row has none.</summary>
    public Rectangle GetToggleBounds(int index)
    {
        var area = LayoutOf(index)?.Toggle ?? Rectangle.Empty;
        return area.IsEmpty ? area : Rectangle.Inflate(area, ScalePixels(4), 0);
    }

    /// <summary>The action button's area of a <see cref="TwListItem"/> row (list coordinates); empty when the row has none.</summary>
    public Rectangle GetActionBounds(int index) => LayoutOf(index)?.Action ?? Rectangle.Empty;

    private (int Index, TwListItemPart Part) PartAt(Point point)
    {
        int index = IndexFromPoint(point);
        if (index < 0) return (-1, TwListItemPart.None);
        if (GetActionBounds(index) is { IsEmpty: false } action && action.Contains(point)) return (index, TwListItemPart.Action);
        if (GetToggleBounds(index) is { IsEmpty: false } toggle && toggle.Contains(point)) return (index, TwListItemPart.Toggle);
        return (index, TwListItemPart.None);
    }

    /// <summary>Flips the toggle of the item at <paramref name="index"/> and raises <see cref="ItemToggle"/>; nothing happens when the row has no toggle.</summary>
    public void ToggleItem(int index)
    {
        if (ItemAt(index) is not TwListItem { ToggleIconName: not null } item) return;
        item.IsToggled = !item.IsToggled;
        Invalidate();
        ItemToggle?.Invoke(this, new TwListItemEventArgs(index, item));
    }

    /// <summary>Raises <see cref="ItemActionClick"/> for the item at <paramref name="index"/>; nothing happens when the row has no action button.</summary>
    public void PerformItemAction(int index)
    {
        if (ItemAt(index) is not TwListItem { ActionText: { Length: > 0 } } item) return;
        ItemActionClick?.Invoke(this, new TwListItemEventArgs(index, item));
    }

    private void SetHot((int Index, TwListItemPart Part) hot)
    {
        if (_hot == hot) return;
        _hot = hot;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHot(PartAt(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHot((-1, TwListItemPart.None));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        (int Index, TwListItemPart Part) part = e.Button == MouseButtons.Left ? PartAt(e.Location) : (-1, TwListItemPart.None);
        base.OnMouseDown(e);
        if (part.Part == TwListItemPart.None) return;
        _pressed = part;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var pressed = _pressed;
        if (pressed.Part == TwListItemPart.None) return;
        _pressed = (-1, TwListItemPart.None);
        Invalidate();
        // Düğme üzerinde basılıp yine üzerinde bırakılırsa tıklanmış sayılır.
        if (e.Button != MouseButtons.Left || PartAt(e.Location) != pressed) return;
        if (pressed.Part == TwListItemPart.Toggle) ToggleItem(pressed.Index);
        else PerformItemAction(pressed.Index);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        // Düğmeye art arda basmak satırı etkinleştirmez.
        if (PartAt(e.Location).Part != TwListItemPart.None) return;
        base.OnMouseDoubleClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Alt && SelectedIndex >= 0 && ItemAt(SelectedIndex) is TwListItem item)
        {
            // Boşluk anahtar simgesini çevirir (çoklu seçimde Ctrl+Boşluk seçim içindir); Ctrl+Enter eylem düğmesine basar.
            if (e.KeyCode == Keys.Space && !e.Control && !e.Shift && item.ToggleIconName != null)
            {
                ToggleItem(SelectedIndex);
                e.Handled = true;
                return;
            }
            if (e.KeyCode == Keys.Enter && e.Control && !string.IsNullOrEmpty(item.ActionText))
            {
                PerformItemAction(SelectedIndex);
                e.Handled = true;
                return;
            }
        }
        base.OnKeyDown(e);
    }

    internal override IReadOnlyList<TwItemPartInfo> GetItemParts(int index)
    {
        if (ItemAt(index) is not TwListItem item || HasDrawItemHandler) return Array.Empty<TwItemPartInfo>();
        var parts = new List<TwItemPartInfo>(2);
        var layout = LayoutOf(index);
        if (layout is null) return parts;
        if (item.ToggleIconName != null)
            parts.Add(new TwItemPartInfo(item.ToggleText ?? TwTexts.Current.Toggle, AccessibleRole.CheckButton,
                item.IsToggled ? AccessibleStates.Checked : AccessibleStates.None, GetToggleBounds(index), TwTexts.Current.Toggle, () => ToggleItem(index)));
        if (!string.IsNullOrEmpty(item.ActionText))
            parts.Add(new TwItemPartInfo(item.ActionText!, AccessibleRole.PushButton, AccessibleStates.None, layout.Action, item.ActionText, () => PerformItemAction(index)));
        return parts;
    }

    [Category("Data"), DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    [Editor("System.Windows.Forms.Design.StringCollectionEditor, System.Design", typeof(UITypeEditor))]
    public IList<object> Items => _items;

    /// <summary>The item property to display (<c>ToString()</c> when empty).</summary>
    [Category("Data"), DefaultValue("")]
    public string DisplayMember
    {
        get => _displayMember;
        set
        {
            _displayMember = value ?? "";
            Invalidate();
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem
    {
        get => SelectedIndex >= 0 && SelectedIndex < _items.Count ? _items[SelectedIndex] : null;
        set => SelectedIndex = value is null ? -1 : _items.IndexOf(value);
    }

    [Browsable(false)]
    public IReadOnlyList<object> SelectedItems => SelectedIndices.Where(i => i < _items.Count).Select(i => _items[i]).ToList();

    /// <summary>The item's display text.</summary>
    public string GetItemText(int index)
    {
        if (index < 0 || index >= _items.Count) return "";
        var item = _items[index];
        if (item is null) return "";
        if (_displayMember.Length > 0 && TypeDescriptor.GetProperties(item)[_displayMember] is PropertyDescriptor property)
            return Convert.ToString(property.GetValue(item), System.Globalization.CultureInfo.CurrentCulture) ?? "";
        return item.ToString() ?? "";
    }

    /// <summary>The first item whose text starts with <paramref name="text"/> (case-insensitive); -1 when none.</summary>
    public int FindString(string text, int startIndex = 0)
    {
        for (int n = 0; n < _items.Count; n++)
        {
            int i = (Math.Max(0, startIndex) + n) % _items.Count;
            if (GetItemText(i).StartsWith(text, StringComparison.CurrentCultureIgnoreCase)) return i;
        }
        return -1;
    }
}

/// <summary>
/// Replaces CheckedListBox: a rounded checkbox at the start of each row. Clicking the box or pressing Space checks it;
/// the check color is <c>accent-*</c>.
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ItemCheck))]
[Description("Stillenebilir onay kutulu liste.")]
public class TwCheckedListBox : TwListBox
{
    // Satır başına işaret (aynı metin iki kez eklenebilir); liste değiştikçe kaydırılır.
    private readonly List<bool> _checked = new List<bool>();

    public TwCheckedListBox()
    {
        ((BindingList<object>)Items).ListChanged += (_, e) =>
        {
            switch (e.ListChangedType)
            {
                case ListChangedType.ItemAdded: _checked.Insert(e.NewIndex, false); break;
                case ListChangedType.ItemDeleted: _checked.RemoveAt(e.NewIndex); break;
                case ListChangedType.ItemMoved:
                    bool moved = _checked[e.OldIndex];
                    _checked.RemoveAt(e.OldIndex);
                    _checked.Insert(e.NewIndex, moved);
                    break;
                case ListChangedType.Reset:
                    _checked.Clear();
                    _checked.AddRange(Enumerable.Repeat(false, Items.Count));
                    break;
            }
        };
    }

    /// <summary>When an item's check state changes (after the change).</summary>
    public event EventHandler<ItemCheckEventArgs>? ItemCheck;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color CheckColor { get; set; } = ColorTranslator.FromHtml("#2563eb");

    [Browsable(false)]
    public IReadOnlyList<int> CheckedIndices => Enumerable.Range(0, _checked.Count).Where(i => _checked[i]).ToList();

    [Browsable(false)]
    public IReadOnlyList<object> CheckedItems => CheckedIndices.Select(i => Items[i]).ToList();

    public bool GetItemChecked(int index) => index >= 0 && index < _checked.Count && _checked[index];

    public void SetItemChecked(int index, bool value)
    {
        if (index < 0 || index >= Items.Count || GetItemChecked(index) == value) return;
        _checked[index] = value;
        Invalidate();
        ItemCheck?.Invoke(this, new ItemCheckEventArgs(index, value ? CheckState.Checked : CheckState.Unchecked, value ? CheckState.Unchecked : CheckState.Checked));
    }

    private int BoxSize => ScalePixels(16);

    /// <summary>The row's checkbox.</summary>
    public Rectangle GetCheckBoxBounds(int index)
    {
        var row = GetItemBounds(index);
        int left = row.X + ScaledItemPadding.Left;
        return new Rectangle(left, row.Y + (row.Height - BoxSize) / 2, BoxSize, BoxSize);
    }

    protected override void OnDrawItem(TwDataListDrawEventArgs e)
    {
        var box = GetCheckBoxBounds(e.Index);
        bool on = GetItemChecked(e.Index);
        float radius = ScalePixels(4);
        if (on)
        {
            TwPainter.FillRounded(e.Graphics, box, radius, CheckColor);
            TwIconRenderer.DrawIcon(e.Graphics, "check", (int)(box.Width * 0.8f), box, Color.White);
        }
        else
        {
            TwPainter.FillRounded(e.Graphics, box, radius, Colors.Back);
            TwPainter.StrokeRounded(e.Graphics, box, radius, Math.Max(1f, ScalePixels(1)), Color.FromArgb(150, Colors.Muted));
        }
        var content = e.ContentBounds;
        int shift = BoxSize + ScalePixels(10);
        e.DrawText(GetItemText(e.Index), new Rectangle(content.X + shift, content.Y, Math.Max(0, content.Width - shift), content.Height));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int index = IndexFromPoint(e.Location);
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left && index >= 0 && Rectangle.Inflate(GetCheckBoxBounds(index), ScalePixels(4), ScalePixels(4)).Contains(e.Location))
            SetItemChecked(index, !GetItemChecked(index));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && !e.Control && SelectedIndex >= 0)
        {
            // Çoklu seçimde tüm seçili satırlar ilk satırın tersine çevrilir.
            bool value = !GetItemChecked(SelectedIndex);
            foreach (var index in SelectedIndices) SetItemChecked(index, value);
            if (!SelectedIndices.Contains(SelectedIndex)) SetItemChecked(SelectedIndex, value);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
