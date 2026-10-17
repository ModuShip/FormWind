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
        _items.ListChanged += (_, _) =>
        {
            ItemCount = _items.Count;
            Invalidate();
        };
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
