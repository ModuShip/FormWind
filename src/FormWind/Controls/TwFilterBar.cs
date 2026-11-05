using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Layout;

namespace FormWind;

/// <summary>What a <see cref="TwFilter"/> asks for.</summary>
public enum TwFilterKind
{
    /// <summary>One of the <see cref="TwFilter.Options"/>.</summary>
    Select,

    /// <summary>Free text (a record number, a word).</summary>
    Text,

    /// <summary>Always applied (the user's own site, the open period): shown as a chip without ✕; its value can still change
    /// when it has <see cref="TwFilter.Options"/>.</summary>
    Fixed,

    /// <summary>A date range picked in a <see cref="TwPeriodPicker"/> popup ("Tarih: Son 90 gün"). The filter's
    /// <see cref="TwFilter.Period"/> is the picker (presets, shifts); its text is the filter's value.</summary>
    Period,
}

/// <summary>A filter of <see cref="TwFilterBar"/>: the field it narrows, how its value is picked and the value.</summary>
public sealed class TwFilter
{
    public TwFilter(string key, string label, TwFilterKind kind)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Kind = kind;
        if (kind == TwFilterKind.Period) Period = new TwPeriodPicker();
    }

    /// <summary>The picker of a <see cref="TwFilterKind.Period"/> filter (<c>null</c> for other kinds): change its
    /// <see cref="TwPeriodPicker.Presets"/> or <see cref="TwPeriodPicker.Shifts"/> before use. It is never placed on a form;
    /// the bar shows its panel under the chip. Read the chosen days with <see cref="TwFilterBar.GetPeriod"/>.</summary>
    public TwPeriodPicker? Period { get; }

    /// <summary>The name the application uses for the filter (a column, a query parameter).</summary>
    public string Key { get; }

    /// <summary>The name shown to the user ("Hat", "Neden").</summary>
    public string Label { get; }

    public TwFilterKind Kind { get; }

    /// <summary>The values to choose from (<see cref="TwFilterKind.Select"/>, and <see cref="TwFilterKind.Fixed"/> if it can change).</summary>
    public List<string> Options { get; } = new List<string>();

    /// <summary>Hint in the text box of a <see cref="TwFilterKind.Text"/> filter.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Chip text for a value; <c>null</c> = "Label: value".</summary>
    public Func<string, string>? Format { get; set; }

    /// <summary>The current value; <c>null</c> = not applied. Set it with <see cref="TwFilterBar.SetValue"/>. For a
    /// <see cref="TwFilterKind.Period"/> filter it is the picker's text ("Son 90 gün", "01.01.2026 – 31.03.2026").</summary>
    public string? Value { get; internal set; }

    internal string ChipText => Value is null ? Label : Format?.Invoke(Value) ?? Label + ": " + Value;
}

/// <summary>
/// Filter bar: every applied filter is a <see cref="TwChip"/> ("Hat: 3", "Neden: Arıza") with ✕ to remove it; clicking a chip
/// changes its value; "+ Add filter" opens a menu of the filters not yet applied, each with its options or a text box.
/// <see cref="TwFilterKind.Fixed"/> filters are always shown and cannot be removed. The chips wrap onto more lines when the
/// bar is narrow (<c>AutoSize</c>). <see cref="FiltersChanged"/> is raised when the user changes a filter or
/// <see cref="SetValue"/> is called; <see cref="LoadValues"/> sets values silently (restoring saved filters).
/// <code>
/// var filters = new TwFilterBar { Dock = DockStyle.Top };
/// filters.Filters.Add(new TwFilter("line", "Hat", TwFilterKind.Select) { Options = { "Hat 1", "Hat 2" } });
/// filters.Filters.Add(new TwFilter("text", "Açıklama", TwFilterKind.Text));
/// filters.FiltersChanged += (_, _) => Reload(filters.Values);
/// </code>
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(FiltersChanged))]
[Description("Applied filters as chips with an add-filter menu.")]
public class TwFilterBar : TwPanel
{
    private readonly Dictionary<TwFilter, TwChip> _chips = new Dictionary<TwFilter, TwChip>();
    private bool _arranging;
    private bool _addVisible = true;
    private int _gap = 6;
    private ContextMenuStrip? _menu;
    private bool _syncingPeriod;

    public TwFilterBar()
    {
        Filters = new ObservableCollection<TwFilter>();
        Filters.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
                foreach (TwFilter filter in e.NewItems)
                    if (filter.Period is TwPeriodPicker picker)
                    {
                        var owner = filter;
                        picker.PeriodChanged += (_, _) => OnPeriodChanged(owner);
                    }
            Rebuild();
        };
        AddButton = new TwButton { Text = TwTexts.Current.AddFilter, IconName = "plus", AutoSize = true };
        AddButton.Click += (_, _) => ShowAddMenu();
        Controls.Add(AddButton);
        AutoSize = true;
    }

    protected override Size DefaultSize => new Size(480, 36);

    /// <summary>Raised when a filter's value changes (by the user or <see cref="SetValue"/>; not by <see cref="LoadValues"/>).</summary>
    public event EventHandler? FiltersChanged;

    /// <summary>The filters the bar offers, in the order their chips are shown.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ObservableCollection<TwFilter> Filters { get; }

    /// <summary>The "+ Add filter" button (hidden when every filter is applied).</summary>
    [Browsable(false)]
    public TwButton AddButton { get; }

    /// <summary>Space between chips (96 DPI pixels).</summary>
    [Category("Layout"), DefaultValue(6)]
    public int Gap
    {
        get => _gap;
        set
        {
            _gap = Math.Max(0, value);
            PerformLayout();
        }
    }

    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), DefaultValue(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set => base.AutoSize = value;
    }

    /// <summary>The applied filters: key → value.</summary>
    [Browsable(false)]
    public IReadOnlyDictionary<string, string> Values =>
        Filters.Where(f => f.Value != null).ToDictionary(f => f.Key, f => f.Value!);

    /// <summary>The menu open on the bar (for tests).</summary>
    internal ContextMenuStrip? OpenMenu => _menu?.Visible == true ? _menu : null;

    public TwFilter? Find(string key) => Filters.FirstOrDefault(f => f.Key == key);

    public string? GetValue(string key) => Find(key)?.Value;

    /// <summary>The first and last day (inclusive) of an applied <see cref="TwFilterKind.Period"/> filter; <c>null</c> when
    /// the filter is not applied or is of another kind. The shift is <c>Find(key).Period.Shift</c>.</summary>
    public (DateTime Start, DateTime End)? GetPeriod(string key) =>
        Find(key) is { Value: not null, Period: TwPeriodPicker picker } ? (picker.Start, picker.End) : null;

    /// <summary>Applies a custom range to a <see cref="TwFilterKind.Period"/> filter and raises <see cref="FiltersChanged"/>.</summary>
    public void SetPeriod(string key, DateTime start, DateTime end)
    {
        if (Find(key) is not { Period: TwPeriodPicker picker } filter) throw new ArgumentException("Not a period filter: " + key, nameof(key));
        Sync(() => picker.SetRange(start, end));
        if (Assign(filter, picker.PeriodText)) Changed();
    }

    // Seçicinin değişimi (panelden) uygulanmış filtrenin değerine yansır; kodla ayarlarken olay iki kez doğmasın diye susturulur.
    private void Sync(Action change)
    {
        bool before = _syncingPeriod;
        _syncingPeriod = true;
        try { change(); }
        finally { _syncingPeriod = before; }
    }

    private void OnPeriodChanged(TwFilter filter)
    {
        if (_syncingPeriod || filter.Value is null || filter.Period is not TwPeriodPicker picker || !Filters.Contains(filter)) return;
        if (Assign(filter, picker.PeriodText)) Changed();
    }

    // Dönem filtresinin değeri hazır dönemin metnidir (ya da SetPeriod ile özel aralık); tanımadığı metni yok sayar.
    private bool AssignPeriod(TwFilter filter, string? value)
    {
        var picker = filter.Period!;
        if (value != null)
        {
            int index = picker.Presets.ToList().FindIndex(p => p.Text == value);
            if (index < 0 && value != picker.PeriodText) return false;
            if (index >= 0) Sync(() => picker.PresetIndex = index);
            value = picker.PeriodText;
        }
        if (filter.Value == value) return false;
        filter.Value = value;
        return true;
    }

    /// <summary>The chip of a filter; <c>null</c> when the filter is not applied.</summary>
    public TwChip? ChipOf(string key) => Find(key) is TwFilter filter && _chips.TryGetValue(filter, out var chip) ? chip : null;

    /// <summary>Applies (or with <c>null</c> removes) a filter and raises <see cref="FiltersChanged"/>.</summary>
    public void SetValue(string key, string? value)
    {
        if (Find(key) is not TwFilter filter) throw new ArgumentException("Unknown filter: " + key, nameof(key));
        if (Assign(filter, value)) Changed();
    }

    /// <summary>Sets the values of the given filters (unknown keys are skipped) without raising <see cref="FiltersChanged"/>.</summary>
    public void LoadValues(IEnumerable<KeyValuePair<string, string?>> values)
    {
        if (values is null) throw new ArgumentNullException(nameof(values));
        foreach (var pair in values)
            if (Find(pair.Key) is TwFilter filter) Assign(filter, pair.Value);
        Rebuild();
    }

    /// <summary>Removes every filter that can be removed (fixed ones stay).</summary>
    public void ClearFilters()
    {
        bool changed = false;
        foreach (var filter in Filters)
            if (filter.Kind != TwFilterKind.Fixed) changed |= Assign(filter, null);
        if (changed) Changed();
    }

    private bool Assign(TwFilter filter, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) value = null;
        if (filter.Period != null) return AssignPeriod(filter, value);
        if (filter.Kind == TwFilterKind.Fixed && value is null) return false;
        if (filter.Value == value) return false;
        filter.Value = value;
        return true;
    }

    private void Changed()
    {
        Rebuild();
        FiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    // Çipler filtrelerin sırasıyla yeniden kurulur; aynı filtrenin çipi korunur (odak ve sınıflar kalsın).
    private void Rebuild()
    {
        SuspendLayout();
        try
        {
            foreach (var pair in _chips.ToList())
            {
                if (Filters.Contains(pair.Key) && (pair.Key.Value != null || pair.Key.Kind == TwFilterKind.Fixed)) continue;
                _chips.Remove(pair.Key);
                Controls.Remove(pair.Value);
                pair.Value.Dispose();
            }
            int index = 0;
            foreach (var filter in Filters)
            {
                if (filter.Value is null && filter.Kind != TwFilterKind.Fixed) continue;
                if (!_chips.TryGetValue(filter, out var chip))
                {
                    var owner = filter;
                    chip = new TwChip { Removable = filter.Kind != TwFilterKind.Fixed, AutoSize = true };
                    chip.RemoveClick += (_, _) => SetValue(owner.Key, null);
                    chip.Click += (_, _) => ShowEditor(owner.Key);
                    _chips[filter] = chip;
                    Controls.Add(chip);
                }
                chip.Text = filter.ChipText;
                chip.Clickable = filter.Kind != TwFilterKind.Fixed || filter.Options.Count > 0;
                Controls.SetChildIndex(chip, index++);
            }
            Controls.SetChildIndex(AddButton, index);
            _addVisible = Filters.Any(f => f.Value is null && f.Kind != TwFilterKind.Fixed);
            AddButton.Visible = _addVisible;
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    /// <summary>Opens the menu of the filters that are not applied yet.</summary>
    public void ShowAddMenu()
    {
        var menu = NewMenu();
        foreach (var filter in Filters)
        {
            if (filter.Value != null || filter.Kind == TwFilterKind.Fixed) continue;
            var item = new ToolStripMenuItem(filter.Label);
            if (filter.Period != null)
            {
                // Dönem: tıklayınca seçicinin o anki dönemi uygulanır ve panel değiştirmek için açılır.
                var owner = filter;
                item.Click += (_, _) =>
                {
                    if (Assign(owner, owner.Period!.PeriodText)) Changed();
                    ShowPeriodPanel(owner, ChipOf(owner.Key) ?? (Control)AddButton);
                };
            }
            else AddValueItems(item.DropDownItems, filter, item);
            menu.Items.Add(item);
        }
        if (menu.Items.Count == 0)
        {
            menu.Dispose();
            return;
        }
        Open(menu, AddButton);
    }

    /// <summary>Opens the value menu of a filter under its chip (or the add button when it is not applied).</summary>
    public void ShowEditor(string key)
    {
        if (Find(key) is not TwFilter filter) return;
        if (filter.Kind == TwFilterKind.Fixed && filter.Options.Count == 0) return;
        if (filter.Period != null)
        {
            ShowPeriodPanel(filter, ChipOf(key) ?? (Control)AddButton);
            return;
        }
        var menu = NewMenu();
        AddValueItems(menu.Items, filter, null);
        Open(menu, ChipOf(key) ?? (Control)AddButton);
    }

    // Dönem filtresi menüyle değil seçicinin paneliyle düzenlenir; çipin altında açılır.
    private void ShowPeriodPanel(TwFilter filter, Control anchor)
    {
        CloseMenu();
        var picker = filter.Period!;
        picker.Font = Font;
        picker.CalendarColors = PeriodColors();
        picker.ShowPanel(anchor);
    }

    private TwCalendarColors PeriodColors()
    {
        var back = TwColorsOf.EffectiveBack(this);
        if (back.GetBrightness() >= 0.4f) return new TwCalendarColors();
        return new TwCalendarColors
        {
            Back = ColorTranslator.FromHtml("#1f2937"),
            Border = ColorTranslator.FromHtml("#374151"),
            Fore = ColorTranslator.FromHtml("#e5e7eb"),
            Muted = ColorTranslator.FromHtml("#9ca3af"),
            Accent = ColorTranslator.FromHtml("#60a5fa"),
            AccentFore = ColorTranslator.FromHtml("#111827"),
            Hover = ColorTranslator.FromHtml("#374151"),
        };
    }

    private void AddValueItems(ToolStripItemCollection items, TwFilter filter, ToolStripMenuItem? parent)
    {
        if (filter.Kind == TwFilterKind.Text)
        {
            var colors = MenuColors();
            var box = new ToolStripTextBox
            {
                AutoSize = false,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = colors.InputBack,
                ForeColor = colors.Fore,
                Text = filter.Value ?? "",
                Size = new Size(Scale(200), Font.Height + Scale(8)),
                Margin = new Padding(4, 4, 4, 4),
            };
            TwSelect.SetCueBanner(box.TextBox, filter.Placeholder ?? filter.Label);
            box.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    CloseMenu();
                    SetValue(filter.Key, box.Text);
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    e.SuppressKeyPress = true;
                    CloseMenu();
                }
            };
            items.Add(box);
            if (parent != null) parent.DropDownOpened += (_, _) => box.Focus();
            else if (_pendingFocus is null) _pendingFocus = box;
            return;
        }
        foreach (var option in filter.Options)
        {
            var value = option;
            var item = new ToolStripMenuItem(option) { Checked = filter.Value == option };
            item.Click += (_, _) => SetValue(filter.Key, value);
            items.Add(item);
        }
    }

    private ToolStripTextBox? _pendingFocus;

    private ContextMenuStrip NewMenu()
    {
        CloseMenu();
        _pendingFocus = null;
        var colors = MenuColors();
        var menu = new ContextMenuStrip { Renderer = new TwToolStripRenderer(colors), ShowImageMargin = false, ShowCheckMargin = true, Font = Font };
        // Alt menüler (filtrenin seçenekleri) aynı görünümü alır.
        menu.ItemAdded += (_, e) =>
        {
            if (e.Item is ToolStripMenuItem { DropDown: ToolStripDropDownMenu sub })
            {
                sub.Renderer = new TwToolStripRenderer(colors);
                sub.ShowImageMargin = false;
                sub.ShowCheckMargin = true;
            }
        };
        return menu;
    }

    private void Open(ContextMenuStrip menu, Control anchor)
    {
        _menu = menu;
        var focus = _pendingFocus;
        _pendingFocus = null;
        if (focus != null) menu.Opened += (_, _) => focus.Focus();
        menu.Closed += (_, _) =>
        {
            if (_menu == menu) _menu = null;
            if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(menu.Dispose));
        };
        menu.Show(anchor, new Point(0, anchor.Height + Scale(2)));
    }

    private void CloseMenu() => _menu?.Close();

    private TwMenuColors MenuColors()
    {
        // Koyu zeminde koyu menü: çubuğun etkin arka planından karar verilir.
        var back = TwColorsOf.EffectiveBack(this);
        return back.GetBrightness() < 0.4f
            ? new TwMenuColors
            {
                Back = ColorTranslator.FromHtml("#1f2937"),
                Fore = ColorTranslator.FromHtml("#e5e7eb"),
                MenuBack = ColorTranslator.FromHtml("#1f2937"),
                MenuBorder = ColorTranslator.FromHtml("#374151"),
                ItemHoverBack = ColorTranslator.FromHtml("#374151"),
                ItemHoverFore = Color.White,
                Separator = ColorTranslator.FromHtml("#374151"),
                Accent = ColorTranslator.FromHtml("#60a5fa"),
                InputBack = ColorTranslator.FromHtml("#111827"),
            }
            : new TwMenuColors();
    }

    private int Scale(int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(this) / 96f);

    public override LayoutEngine LayoutEngine => WrapLayout.Instance;

    public override Size GetPreferredSize(Size proposedSize)
    {
        int width = proposedSize.Width > 0 && proposedSize.Width < int.MaxValue / 2 ? proposedSize.Width : Width;
        int height = Flow(width, place: false);
        return new Size(width, height);
    }

    // Soldan sağa dizilir, sığmayan alt satıra geçer; her satırdaki öğeler dikeyde ortalanır.
    private int Flow(int width, bool place)
    {
        int gap = Scale(_gap);
        int left = Padding.Left, right = Math.Max(left + 1, width - Padding.Right);
        int x = left, y = Padding.Top, lineHeight = 0;
        var line = new List<(Control Control, Size Size)>();
        bool rtl = TwRtl.NeedsMirroring(this);

        void Flush()
        {
            if (place)
            {
                int cx = left;
                foreach (var (control, size) in line)
                {
                    int px = rtl ? width - cx - size.Width : cx;
                    control.SetBounds(px, y + (lineHeight - size.Height) / 2, size.Width, size.Height);
                    cx += size.Width + gap;
                }
            }
            line.Clear();
        }

        foreach (Control control in Controls)
        {
            // Visible, üst gizliyken (form açılmadan) false döner; ekleme düğmesinin durumu ayrıca tutulur.
            bool shown = control == AddButton ? _addVisible : control.Visible || !Visible;
            if (!shown) continue;
            var size = control.GetPreferredSize(Size.Empty);
            size.Width = Math.Min(size.Width, right - left);
            if (line.Count > 0 && x + size.Width > right)
            {
                Flush();
                y += lineHeight + gap;
                x = left;
                lineHeight = 0;
            }
            line.Add((control, size));
            x += size.Width + gap;
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        Flush();
        return y + lineHeight + Padding.Bottom;
    }

    internal void Arrange()
    {
        if (_arranging) return;
        _arranging = true;
        try
        {
            Flow(Width, place: true);
            if (AutoSize && Dock is DockStyle.Top or DockStyle.Bottom or DockStyle.None)
            {
                int height = Flow(Width, place: false);
                if (Height != height) Height = height;
            }
        }
        finally
        {
            _arranging = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _menu?.Dispose();
            foreach (var filter in Filters) filter.Period?.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class WrapLayout : LayoutEngine
    {
        public static readonly WrapLayout Instance = new WrapLayout();

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            if (container is TwFilterBar bar) bar.Arrange();
            return false;
        }
    }
}

/// <summary>Background helpers.</summary>
internal static class TwColorsOf
{
    /// <summary>The first opaque background up the parent chain.</summary>
    public static Color EffectiveBack(Control control)
    {
        for (Control? c = control; c != null; c = c.Parent)
            if (c.BackColor.A == 255) return c.BackColor;
        return SystemColors.Control;
    }
}
