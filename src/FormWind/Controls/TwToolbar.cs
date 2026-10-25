using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Layout;

namespace FormWind;

/// <summary>Which end of a <see cref="TwToolbar"/> an item sits at.</summary>
public enum TwToolbarAlignment
{
    /// <summary>After the previous start items (left, right in right-to-left layout).</summary>
    Start,

    /// <summary>At the far end, before the "more" button.</summary>
    End,
}

/// <summary>What a <see cref="TwToolbar"/> does with an item when the items do not fit.</summary>
public enum TwToolbarOverflow
{
    /// <summary>Moves to the "more" menu when there is no room (lowest <see cref="TwToolbar.SetPriority"/> first, then the last).</summary>
    AsNeeded,

    /// <summary>Always stays on the bar.</summary>
    Never,

    /// <summary>Always in the "more" menu.</summary>
    Always,

    /// <summary>Stays on the bar and gets narrower first, down to its <c>MinimumSize.Width</c> (a title with AutoEllipsis).</summary>
    Shrink,
}

/// <summary>Event data of <see cref="TwToolbar.OverflowItemClick"/>.</summary>
public sealed class TwToolbarItemEventArgs : EventArgs
{
    public TwToolbarItemEventArgs(Control item) => Item = item;

    /// <summary>The toolbar item whose menu entry was clicked.</summary>
    public Control Item { get; }
}

/// <summary>
/// A responsive toolbar: its children (title, segments, buttons, selects …) sit in one row, vertically centered, separated by
/// <c>gap-*</c>. Items that do not fit move to a "more" (…) button at the end, whose menu runs them: a button is clicked, a
/// check box toggled, a <see cref="TwTabStrip"/> or <see cref="TwSelect"/> becomes a submenu. A title set to
/// <see cref="TwToolbarOverflow.Shrink"/> gets narrower first. Styled like a <see cref="TwPanel"/> (<c>bg-* border-b px-* py-*</c>);
/// the menu takes <c>menu:</c> and <c>item:</c>, the "more" button is <see cref="OverflowButton"/>.
/// <code>
/// toolbar.Controls.AddRange(new Control[] { title, segments, refresh, export, settings });
/// toolbar.SetOverflow(title, TwToolbarOverflow.Shrink);
/// toolbar.SetAlignment(settings, TwToolbarAlignment.End);
/// toolbar.SetPriority(refresh, 1);   // hidden after the others
/// </code>
/// </summary>
[ToolboxItem(true)]
[Description("Responsive toolbar: items that do not fit go to a \"more\" menu.")]
public class TwToolbar : TwPanel
{
    private readonly Dictionary<Control, ItemOptions> _options = new Dictionary<Control, ItemOptions>();
    private readonly HashSet<Control> _overflowed = new HashSet<Control>();
    private readonly ToolbarLayout _layout;
    private int _gap = 8;
    private bool _arranging;
    private ContextMenuStrip? _menu;

    public TwToolbar()
    {
        _layout = new ToolbarLayout(this);
        OverflowButton = new TwButton
        {
            IconName = "more",
            Text = "",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(6, 4, 6, 4),
            FlatStyle = FlatStyle.Flat,
            Visible = false,
            TabStop = true,
        };
        OverflowButton.AccessibleName = TwTexts.Current.More;
        OverflowButton.FlatAppearance.BorderSize = 0;
        OverflowButton.Click += (_, _) => ShowOverflowMenu();
        Controls.Add(OverflowButton);
    }

    /// <summary>A menu entry of an item that is neither a button, a check box, a tab strip nor a select was clicked.</summary>
    public event EventHandler<TwToolbarItemEventArgs>? OverflowItemClick;

    protected override Size DefaultSize => new Size(480, 44);

    protected override Padding DefaultPadding => new Padding(8, 4, 8, 4);

    /// <summary>The "more" button (style it with classes, e.g. <c>btn-ghost</c>).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwButton OverflowButton { get; }

    /// <summary>The last opened "more" menu (<c>null</c> if it was never opened).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ContextMenuStrip? OverflowMenu => _menu;

    /// <summary>Space between items (96 DPI pixels); also set with <c>gap-*</c>.</summary>
    [Category("Layout"), DefaultValue(8)]
    public int Gap
    {
        get => _gap;
        set
        {
            _gap = Math.Max(0, value);
            PerformLayout();
        }
    }

    /// <summary>The items in the "more" menu right now, in toolbar order.</summary>
    [Browsable(false)]
    public IReadOnlyList<Control> OverflowItems => Items.Where(_overflowed.Contains).ToList();

    internal TwMenuColors MenuColors { get; set; } = new TwMenuColors();

    public override LayoutEngine LayoutEngine => _layout;

    public void SetAlignment(Control item, TwToolbarAlignment alignment)
    {
        Options(item).Alignment = alignment;
        PerformLayout();
    }

    public TwToolbarAlignment GetAlignment(Control item) => _options.TryGetValue(item, out var o) ? o.Alignment : TwToolbarAlignment.Start;

    /// <summary>
    /// How the item behaves when the items do not fit. A <see cref="TwToolbarOverflow.Shrink"/> item's <c>AutoSize</c> is turned
    /// off: the toolbar gives it its content width or less (restored when it stops shrinking).
    /// </summary>
    public void SetOverflow(Control item, TwToolbarOverflow overflow)
    {
        var options = Options(item);
        options.Overflow = overflow;
        // AutoSize'lı etiket SetBounds'ta kendi boyuna döner; daralabilmesi için genişliği araç çubuğu yönetir.
        if (overflow == TwToolbarOverflow.Shrink && item.AutoSize)
        {
            options.MeasuresContent = true;
            item.AutoSize = false;
        }
        else if (overflow != TwToolbarOverflow.Shrink && options.MeasuresContent)
        {
            options.MeasuresContent = false;
            item.AutoSize = true;
        }
        PerformLayout();
    }

    public TwToolbarOverflow GetOverflow(Control item) => _options.TryGetValue(item, out var o) ? o.Overflow : TwToolbarOverflow.AsNeeded;

    /// <summary>Items with a higher priority move to the menu later (default 0).</summary>
    public void SetPriority(Control item, int priority)
    {
        Options(item).Priority = priority;
        PerformLayout();
    }

    public int GetPriority(Control item) => _options.TryGetValue(item, out var o) ? o.Priority : 0;

    /// <summary>The item's text in the "more" menu; <c>null</c> = its Text or AccessibleName.</summary>
    public void SetOverflowText(Control item, string? text) => Options(item).Text = text;

    private ItemOptions Options(Control item)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));
        if (!_options.TryGetValue(item, out var options)) _options[item] = options = new ItemOptions();
        return options;
    }

    // Araç çubuğu öğeleri: "daha fazla" düğmesi hariç çocuklar, Controls sırasıyla.
    private IEnumerable<Control> Items => Controls.Cast<Control>().Where(c => c != OverflowButton);

    // Öğe çubukta olmak istiyor mu: kendi gizlediklerimiz istiyor sayılır.
    private bool Wanted(Control item) => _overflowed.Contains(item) || item.Visible;

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        if (e.Control is null) return;
        _options.Remove(e.Control);
        if (_overflowed.Remove(e.Control)) e.Control.Visible = true;
        e.Control.VisibleChanged -= OnItemVisibleChanged;
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control != null && e.Control != OverflowButton) e.Control.VisibleChanged += OnItemVisibleChanged;
    }

    // Uygulama gizlediğimiz bir öğeyi kendisi gösterirse öğe yeniden yerleşime katılır.
    private void OnItemVisibleChanged(object? sender, EventArgs e)
    {
        if (_arranging || sender is not Control item) return;
        if (item.Visible) _overflowed.Remove(item);
        PerformLayout();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible) PerformLayout();
    }

    private int Scale(int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(this) / 96f);

    private Size Preferred(Control item) =>
        item.AutoSize || _options.TryGetValue(item, out var o) && o.MeasuresContent ? item.GetPreferredSize(Size.Empty) : item.Size;

    public override Size GetPreferredSize(Size proposedSize)
    {
        var display = DisplayRectangle;
        int border = Width - display.Width, borderY = Height - display.Height;
        var items = Items.Where(Wanted).ToList();
        int width = 0, height = 0;
        foreach (var item in items)
        {
            var size = Preferred(item);
            width += size.Width + item.Margin.Horizontal;
            height = Math.Max(height, size.Height + item.Margin.Vertical);
        }
        width += Math.Max(0, items.Count - 1) * Scale(_gap);
        return new Size(width + Padding.Horizontal + border, Math.Max(height, Preferred(OverflowButton).Height) + Padding.Vertical + borderY);
    }

    internal void Arrange()
    {
        if (_arranging) return;
        _arranging = true;
        try
        {
            ArrangeCore();
        }
        finally
        {
            _arranging = false;
        }
    }

    private void ArrangeCore()
    {
        // Görünmeyen çubukta çocuklar da görünmez sayılır; yerleşim gösterilince yapılır.
        if (!Visible) return;
        var area = Rectangle.FromLTRB(DisplayRectangle.Left + Padding.Left, DisplayRectangle.Top + Padding.Top,
            DisplayRectangle.Right - Padding.Right, DisplayRectangle.Bottom - Padding.Bottom);
        int gap = Scale(_gap);
        var items = Items.Where(Wanted).ToList();
        var widths = items.ToDictionary(i => i, i => Preferred(i).Width);
        var hidden = new HashSet<Control>(items.Where(i => GetOverflow(i) == TwToolbarOverflow.Always));

        int Needed(bool withButton)
        {
            var shown = items.Where(i => !hidden.Contains(i)).ToList();
            int total = shown.Sum(i => widths[i] + i.Margin.Horizontal) + Math.Max(0, shown.Count - 1) * gap;
            if (withButton) total += Preferred(OverflowButton).Width + OverflowButton.Margin.Horizontal + (shown.Count > 0 ? gap : 0);
            return total;
        }

        // Önce Shrink öğeleri en küçük genişliklerine kadar daralır, sonra öğeler (önceliği düşük ve sondakiler önce) menüye geçer.
        int deficit = Needed(hidden.Count > 0) - area.Width;
        foreach (var item in items.Where(i => GetOverflow(i) == TwToolbarOverflow.Shrink))
        {
            if (deficit <= 0) break;
            int give = Math.Min(deficit, Math.Max(0, widths[item] - item.MinimumSize.Width));
            widths[item] -= give;
            deficit -= give;
        }
        var candidates = items.Where(i => GetOverflow(i) == TwToolbarOverflow.AsNeeded)
            .Select((item, index) => (item, index)).OrderBy(x => GetPriority(x.item)).ThenByDescending(x => x.index).Select(x => x.item).ToList();
        foreach (var item in candidates)
        {
            if (Needed(hidden.Count > 0) <= area.Width) break;
            hidden.Add(item);
        }
        // Menüye geçenler yer açtıysa daralan öğeler geri genişler.
        int spare = area.Width - Needed(hidden.Count > 0);
        foreach (var item in items.Where(i => GetOverflow(i) == TwToolbarOverflow.Shrink))
        {
            if (spare <= 0) break;
            int take = Math.Min(spare, Preferred(item).Width - widths[item]);
            widths[item] += take;
            spare -= take;
        }

        foreach (var item in items)
        {
            bool hide = hidden.Contains(item);
            if (hide) _overflowed.Add(item);
            else _overflowed.Remove(item);
            if (item.Visible == hide) item.Visible = !hide;
        }
        bool overflow = hidden.Count > 0;
        if (OverflowButton.Visible != overflow) OverflowButton.Visible = overflow;

        bool rtl = TwRtl.NeedsMirroring(this);
        void Place(Control item, int x, int width)
        {
            var size = Preferred(item);
            int height = Math.Min(area.Height, size.Height);
            int y = area.Top + (area.Height - height) / 2 + (item.Margin.Top - item.Margin.Bottom) / 2;
            int left = rtl ? area.Left + area.Right - x - width : x;
            item.SetBounds(left, y, width, height);
        }

        int start = area.Left;
        foreach (var item in items.Where(i => !hidden.Contains(i) && GetAlignment(i) == TwToolbarAlignment.Start))
        {
            start += item.Margin.Left;
            Place(item, start, widths[item]);
            start += widths[item] + item.Margin.Right + gap;
        }
        int end = area.Right;
        if (overflow)
        {
            int width = Preferred(OverflowButton).Width;
            end -= OverflowButton.Margin.Right + width;
            Place(OverflowButton, end, width);
            end -= OverflowButton.Margin.Left + gap;
        }
        foreach (var item in items.Where(i => !hidden.Contains(i) && GetAlignment(i) == TwToolbarAlignment.End).Reverse())
        {
            end -= item.Margin.Right + widths[item];
            Place(item, end, widths[item]);
            end -= item.Margin.Left + gap;
        }
    }

    /// <summary>Opens the menu of the items that do not fit, under the "more" button.</summary>
    public void ShowOverflowMenu()
    {
        var items = OverflowItems;
        if (items.Count == 0) return;
        _menu?.Dispose();
        _menu = new ContextMenuStrip { Renderer = new TwToolStripRenderer(MenuColors), ShowImageMargin = false, ShowCheckMargin = items.Any(i => i is CheckBox), Font = Font };
        foreach (var item in items) _menu.Items.Add(CreateMenuItem(item));
        bool rtl = TwRtl.NeedsMirroring(this);
        var button = OverflowButton.Bounds;
        _menu.Show(this, new Point(rtl ? button.Left : button.Right, button.Bottom + Scale(2)),
            rtl ? ToolStripDropDownDirection.BelowRight : ToolStripDropDownDirection.BelowLeft);
    }

    private string MenuText(Control item)
    {
        var text = _options.TryGetValue(item, out var o) && o.Text != null ? o.Text
            : !string.IsNullOrEmpty(item.Text) ? item.Text
            : item.AccessibleName ?? item.Name;
        // Kısayol işareti kullanmayan öğelerin & işareti menüde de metin kalır.
        bool mnemonic = item is ButtonBase b ? b.UseMnemonic : item is Label l ? l.UseMnemonic : item is TwTabStrip t && t.UseMnemonic;
        return mnemonic ? text : text.Replace("&", "&&");
    }

    // Menü öğesi kontrolün kendisini çalıştırır: düğme tıklanır, onay kutusu değişir, sekme ve seçim alt menü olur.
    private ToolStripItem CreateMenuItem(Control item)
    {
        var menuItem = new ToolStripMenuItem(MenuText(item)) { Enabled = item.Enabled };
        switch (item)
        {
            case TwTabStrip tabs:
                for (int i = 0; i < tabs.Tabs.Count; i++)
                {
                    int index = i;
                    var tab = new ToolStripMenuItem(tabs.UseMnemonic ? tabs.Tabs[i] : tabs.Tabs[i].Replace("&", "&&")) { Checked = i == tabs.SelectedIndex };
                    tab.Click += (_, _) => tabs.SelectedIndex = index;
                    menuItem.DropDownItems.Add(tab);
                }
                if (string.IsNullOrEmpty(menuItem.Text)) menuItem.Text = tabs.SelectedIndex >= 0 && tabs.SelectedIndex < tabs.Tabs.Count ? tabs.GetDisplayText(tabs.SelectedIndex) : TwTexts.Current.More;
                break;
            case TwSelect select:
                for (int i = 0; i < select.Items.Count; i++)
                {
                    int index = i;
                    var option = new ToolStripMenuItem(select.Items[i].Replace("&", "&&")) { Checked = i == select.SelectedIndex };
                    option.Click += (_, _) => select.SelectedIndex = index;
                    menuItem.DropDownItems.Add(option);
                }
                if (string.IsNullOrEmpty(menuItem.Text) || menuItem.Text == select.SelectedItem) menuItem.Text = select.AccessibleName ?? select.SelectedItem ?? "";
                break;
            case CheckBox check:
                menuItem.Checked = check.Checked;
                menuItem.Click += (_, _) => check.Checked = !check.Checked;
                break;
            case ButtonBase button:
                menuItem.Click += (_, _) => InvokeOnClick(button, EventArgs.Empty);
                break;
            default:
                menuItem.Click += (_, _) =>
                {
                    InvokeOnClick(item, EventArgs.Empty);
                    OverflowItemClick?.Invoke(this, new TwToolbarItemEventArgs(item));
                };
                break;
        }
        return menuItem;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _menu?.Dispose();
        base.Dispose(disposing);
    }

    private sealed class ItemOptions
    {
        public TwToolbarAlignment Alignment;
        public TwToolbarOverflow Overflow;
        public int Priority;
        public string? Text;
        public bool MeasuresContent;
    }

    private sealed class ToolbarLayout : LayoutEngine
    {
        private readonly TwToolbar _owner;

        public ToolbarLayout(TwToolbar owner) => _owner = owner;

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            _owner.Arrange();
            return _owner.AutoSize;
        }
    }
}
