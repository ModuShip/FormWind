using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FormWind;

/// <summary>A ready-made period of <see cref="TwPeriodPicker"/> ("Last 90 days"): its text and the days it covers for a given today.</summary>
public sealed class TwPeriodPreset
{
    public TwPeriodPreset(string text, Func<DateTime, (DateTime Start, DateTime End)> range)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Range = range ?? throw new ArgumentNullException(nameof(range));
    }

    public string Text { get; }

    /// <summary>First and last day (inclusive) of the period, for the given today.</summary>
    public Func<DateTime, (DateTime Start, DateTime End)> Range { get; }

    public static TwPeriodPreset Today() => new TwPeriodPreset(TwTexts.Current.Today, t => (t, t));

    public static TwPeriodPreset Yesterday() => new TwPeriodPreset(TwTexts.Current.Yesterday, t => (t.AddDays(-1), t.AddDays(-1)));

    /// <summary>The last <paramref name="days"/> days, today included.</summary>
    public static TwPeriodPreset LastDays(int days)
    {
        if (days < 1) throw new ArgumentOutOfRangeException(nameof(days));
        return new TwPeriodPreset(string.Format(CultureInfo.CurrentCulture, TwTexts.Current.LastDays, days), t => (t.AddDays(1 - days), t));
    }

    /// <summary>From the first of this month to today.</summary>
    public static TwPeriodPreset ThisMonth() => new TwPeriodPreset(TwTexts.Current.ThisMonth, t => (new DateTime(t.Year, t.Month, 1), t));

    /// <summary>The whole previous month.</summary>
    public static TwPeriodPreset LastMonth() => new TwPeriodPreset(TwTexts.Current.LastMonth, t =>
    {
        var first = new DateTime(t.Year, t.Month, 1).AddMonths(-1);
        return (first, first.AddMonths(1).AddDays(-1));
    });

    /// <summary>From the first of January to today.</summary>
    public static TwPeriodPreset ThisYear() => new TwPeriodPreset(TwTexts.Current.ThisYear, t => (new DateTime(t.Year, 1, 1), t));

    public override string ToString() => Text;
}

/// <summary>
/// Period picker for reports: a <see cref="TwSelect"/>-style box that shows the period ("Last 90 days", "01.01.2026 – 31.03.2026",
/// with the shift: "Last 7 days · Shift B"). It opens a panel with the <see cref="Presets"/>, "Custom range…" and a calendar
/// (two clicks pick the first and the last day) and, when <see cref="Shifts"/> are given, a segment to narrow the period to
/// one shift. ↑/↓ on the closed box step through the presets. Styled with the <c>select</c> classes (the calendar with
/// <c>menu:</c>, <c>selected:</c>, <c>item:hover:</c>).
/// <code>
/// var period = new TwPeriodPicker { Width = 220 };
/// period.Shifts.Add("A"); period.Shifts.Add("B");     // optional
/// period.PeriodChanged += (_, _) => Reload(period.Start, period.End, period.Shift);
/// </code>
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(PeriodChanged))]
[Description("Period picker with presets, a calendar range and an optional shift.")]
public class TwPeriodPicker : TwSelect
{
    private int _presetIndex;
    private DateTime _customStart = DateTime.Today, _customEnd = DateTime.Today;
    private string? _shift;
    private bool _updating;
    private TwPopover? _popover;
    private TwPeriodPanel? _panel;

    /// <summary>Today, for the presets (replaced in the tests).</summary>
    internal static Func<DateTime> Clock = () => DateTime.Today;

    public TwPeriodPicker()
    {
        Presets = new ObservableCollection<TwPeriodPreset>
        {
            TwPeriodPreset.Today(), TwPeriodPreset.Yesterday(), TwPeriodPreset.LastDays(7), TwPeriodPreset.LastDays(30),
            TwPeriodPreset.LastDays(90), TwPeriodPreset.ThisMonth(), TwPeriodPreset.LastMonth(), TwPeriodPreset.ThisYear(),
        };
        _presetIndex = 3;
        Shifts = new ObservableCollection<string>();
        Presets.CollectionChanged += OnPresetsChanged;
        Shifts.CollectionChanged += (_, _) =>
        {
            if (_shift != null && !Shifts.Contains(_shift)) Shift = null;
            UpdateItems();
        };
        SelectedIndexChanged += OnSelectedIndexChanged;
        UpdateItems();
    }

    protected override Size DefaultSize => new Size(200, 30);

    /// <summary>Raised when the period or the shift changes.</summary>
    public event EventHandler? PeriodChanged;

    /// <summary>The ready-made periods, in the order shown (default: today … this year).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ObservableCollection<TwPeriodPreset> Presets { get; }

    /// <summary>Shift names for the shift segment; empty = no segment.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ObservableCollection<string> Shifts { get; }

    /// <summary>The selected preset (index into <see cref="Presets"/>); -1 = a custom range (<see cref="SetRange"/>).</summary>
    [Category("Behavior"), DefaultValue(3)]
    public int PresetIndex
    {
        get => _presetIndex;
        set
        {
            if (value < -1 || value >= Presets.Count) value = -1;
            if (_presetIndex == value) return;
            if (value == -1)
            {
                // Özel aralığa geçişte aralık son hazır dönemden başlar.
                (_customStart, _customEnd) = (Start, End);
            }
            _presetIndex = value;
            Changed();
        }
    }

    /// <summary>The selected preset; <c>null</c> for a custom range.</summary>
    [Browsable(false)]
    public TwPeriodPreset? Preset => _presetIndex >= 0 && _presetIndex < Presets.Count ? Presets[_presetIndex] : null;

    /// <summary>First day of the period. A preset follows the date: "Last 7 days" moves on at midnight.</summary>
    [Browsable(false)]
    public DateTime Start => Preset is TwPeriodPreset preset ? preset.Range(Clock().Date).Start.Date : _customStart;

    /// <summary>Last day of the period (inclusive).</summary>
    [Browsable(false)]
    public DateTime End => Preset is TwPeriodPreset preset ? preset.Range(Clock().Date).End.Date : _customEnd;

    /// <summary>The selected shift; <c>null</c> = all shifts.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? Shift
    {
        get => _shift;
        set
        {
            if (value != null && !Shifts.Contains(value)) value = null;
            if (_shift == value) return;
            _shift = value;
            Changed();
        }
    }

    /// <summary>Selects a custom range (the ends in either order).</summary>
    public void SetRange(DateTime start, DateTime end)
    {
        if (end < start) (start, end) = (end, start);
        if (_presetIndex == -1 && _customStart == start.Date && _customEnd == end.Date) return;
        _customStart = start.Date;
        _customEnd = end.Date;
        _presetIndex = -1;
        Changed();
    }

    /// <summary>The text shown in the box.</summary>
    [Browsable(false)]
    public string PeriodText
    {
        get
        {
            var text = Preset?.Text ?? RangeText(_customStart, _customEnd);
            return _shift is null ? text : text + " · " + _shift;
        }
    }

    internal static string RangeText(DateTime start, DateTime end) => start == end
        ? start.ToString("d", CultureInfo.CurrentCulture)
        : start.ToString("d", CultureInfo.CurrentCulture) + " – " + end.ToString("d", CultureInfo.CurrentCulture);

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal TwCalendarColors CalendarColors { get; set; } = new TwCalendarColors();

    /// <summary>Whether the panel is open.</summary>
    [Browsable(false)]
    public bool DroppedDown => _popover?.IsOpen == true;

    /// <summary>The open panel (for tests).</summary>
    internal TwPeriodPanel? Panel => DroppedDown ? _panel : null;

    /// <summary>Opens the panel with the presets, the calendar and the shifts.</summary>
    public void ShowPanel() => ShowPanel(this);

    // Seçici bir forma yerleşmemişse (TwFilterBar'ın dönem filtresi) panel verilen denetimin altında açılır.
    internal void ShowPanel(Control anchor)
    {
        if (_popover is null)
        {
            _panel = new TwPeriodPanel(this);
            _popover = new TwPopover { Content = _panel };
            _popover.Opening += (_, _) => _panel.Prepare();
        }
        _popover.Show(anchor);
    }

    internal void ClosePanel() => _popover?.Close();

    private protected override void OpenDropDown() => ShowPanel();

    private void OnPresetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_presetIndex >= Presets.Count) _presetIndex = Presets.Count - 1;
        UpdateItems();
    }

    private void Changed()
    {
        UpdateItems();
        PeriodChanged?.Invoke(this, EventArgs.Empty);
    }

    // Kutu TwSelect'in öğe listesini kullanır: hazır dönemler (↑/↓ ve yazarak seçme için) ve özel aralıksa onun metni.
    private void UpdateItems()
    {
        _updating = true;
        try
        {
            string suffix = _shift is null ? "" : " · " + _shift;
            Items.Clear();
            foreach (var preset in Presets) Items.Add(preset.Text + suffix);
            if (_presetIndex == -1) Items.Add(RangeText(_customStart, _customEnd) + suffix);
            SelectedIndex = -1;
            SelectedIndex = _presetIndex == -1 ? Items.Count - 1 : _presetIndex;
            Invalidate();
            if (AutoSize) Parent?.PerformLayout(this, nameof(PeriodText));
        }
        finally
        {
            _updating = false;
        }
    }

    private void OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_updating) return;
        if (SelectedIndex >= 0 && SelectedIndex < Presets.Count) PresetIndex = SelectedIndex;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _popover?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>The panel of <see cref="TwPeriodPicker"/>: shift segment on top, presets on the left, calendar on the right.</summary>
internal sealed class TwPeriodPanel : Control
{
    private readonly TwPeriodPicker _picker;
    private readonly TwCalendar _calendar = new TwCalendar();
    private DateTime? _anchor;
    private int _hoverRow = -1;
    private int _hoverSegment = -1;
    private int _focusRow;
    private bool _custom;

    public TwPeriodPanel(TwPeriodPicker picker)
    {
        _picker = picker;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Controls.Add(_calendar);
        _calendar.DateSelected += OnDateSelected;
        _calendar.Cancelled += (_, _) => _picker.ClosePanel();
    }

    private float S => TwDpi.DeviceDpi(_picker) / 96f;

    private int Px(float value) => (int)Math.Round(value * S);

    private TwCalendarColors Colors => _picker.CalendarColors;

    /// <summary>The calendar (for tests).</summary>
    internal TwCalendar Calendar => _calendar;

    private int RowCount => _picker.Presets.Count + 1; // + "Özel aralık…"

    private string RowText(int row) => row < _picker.Presets.Count ? _picker.Presets[row].Text : TwTexts.Current.CustomRange;

    private int SelectedRow => _custom || _picker.PresetIndex < 0 ? RowCount - 1 : _picker.PresetIndex;

    private string[] Segments()
    {
        if (_picker.Shifts.Count == 0) return new string[0];
        var segments = new string[_picker.Shifts.Count + 1];
        segments[0] = TwTexts.Current.AllShifts;
        _picker.Shifts.CopyTo(segments, 1);
        return segments;
    }

    private int SegmentHeight => _picker.Shifts.Count == 0 ? 0 : Px(30) + Px(8);

    private int RowHeight => Math.Max(Px(28), Font.Height + Px(10));

    private int ListWidth
    {
        get
        {
            int width = Px(120);
            for (int i = 0; i < RowCount; i++) width = Math.Max(width, TextRenderer.MeasureText(RowText(i), Font).Width + Px(24));
            return width;
        }
    }

    private Rectangle RowBounds(int row) => new Rectangle(Px(8), Px(8) + SegmentHeight + row * RowHeight, ListWidth, RowHeight);

    private Rectangle SegmentBounds(int index, int count)
    {
        int left = Px(8), width = Width - Px(16);
        int each = width / Math.Max(1, count);
        return new Rectangle(left + index * each, Px(8), index == count - 1 ? width - each * (count - 1) : each, Px(30));
    }

    /// <summary>Gets the panel ready to open: sizes, colors and the current period on the calendar.</summary>
    public void Prepare()
    {
        Font = _picker.Font;
        _calendar.Font = _picker.Font;
        _calendar.Colors = Colors;
        _calendar.Size = _calendar.GetPreferredSize(Size.Empty);
        _custom = _picker.PresetIndex < 0;
        _anchor = null;
        _focusRow = SelectedRow;
        _calendar.SelectedDate = null;
        _calendar.Range = (_picker.Start, _picker.End);
        _calendar.DisplayMonth = _picker.End;
        int listHeight = RowCount * RowHeight;
        int top = Px(8) + SegmentHeight;
        _calendar.Location = new Point(Px(8) + ListWidth + Px(8), top);
        Size = new Size(_calendar.Right + Px(8), top + Math.Max(listHeight, _calendar.Height) + Px(8));
        Invalidate();
    }

    /// <summary>Clicks a preset row (the last row starts a custom range).</summary>
    internal void ChooseRow(int row)
    {
        if (row < 0 || row >= RowCount) return;
        if (row < _picker.Presets.Count)
        {
            _picker.PresetIndex = row;
            _picker.ClosePanel();
            return;
        }
        // Özel aralık: takvimde iki tıklama, önce ilk gün sonra son gün.
        _custom = true;
        _anchor = null;
        _calendar.Range = null;
        _calendar.Focus();
        Invalidate();
    }

    /// <summary>Clicks a shift segment (0 = all shifts).</summary>
    internal void ChooseSegment(int index)
    {
        var segments = Segments();
        if (index < 0 || index >= segments.Length) return;
        _picker.Shift = index == 0 ? null : segments[index];
        Invalidate();
    }

    private void OnDateSelected(object? sender, EventArgs e)
    {
        if (_calendar.SelectedDate is not DateTime date) return;
        _calendar.SelectedDate = null;
        if (_anchor is not DateTime first)
        {
            _anchor = date;
            _custom = true;
            _calendar.Range = (date, date);
            Invalidate();
            return;
        }
        _anchor = null;
        _picker.SetRange(first, date);
        _picker.ClosePanel();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int row = -1, segment = -1;
        for (int i = 0; i < RowCount; i++) if (RowBounds(i).Contains(e.Location)) row = i;
        var segments = Segments();
        for (int i = 0; i < segments.Length; i++) if (SegmentBounds(i, segments.Length).Contains(e.Location)) segment = i;
        if (row == _hoverRow && segment == _hoverSegment) return;
        _hoverRow = row;
        _hoverSegment = segment;
        Cursor = row >= 0 || segment >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverRow = _hoverSegment = -1;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        for (int i = 0; i < RowCount; i++)
            if (RowBounds(i).Contains(e.Location))
            {
                ChooseRow(i);
                return;
            }
        var segments = Segments();
        for (int i = 0; i < segments.Length; i++)
            if (SegmentBounds(i, segments.Length).Contains(e.Location))
            {
                ChooseSegment(i);
                return;
            }
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.KeyCode)
        {
            case Keys.Down: _focusRow = Math.Min(RowCount - 1, _focusRow + 1); Invalidate(); e.Handled = true; break;
            case Keys.Up: _focusRow = Math.Max(0, _focusRow - 1); Invalidate(); e.Handled = true; break;
            case Keys.Enter: ChooseRow(_focusRow); e.Handled = true; break;
            case Keys.Escape: _picker.ClosePanel(); e.Handled = true; break;
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var colors = Colors;
        using (var back = new SolidBrush(colors.Back)) g.FillRectangle(back, ClientRectangle);
        const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

        var segments = Segments();
        if (segments.Length > 0)
        {
            var track = Rectangle.FromLTRB(SegmentBounds(0, segments.Length).Left, Px(8), SegmentBounds(segments.Length - 1, segments.Length).Right, Px(8) + Px(30));
            TwPainter.FillRounded(g, track, Px(6), colors.Hover);
            int active = _picker.Shift is null ? 0 : Array.IndexOf(segments, _picker.Shift, 1);
            for (int i = 0; i < segments.Length; i++)
            {
                var bounds = SegmentBounds(i, segments.Length);
                var pill = Rectangle.Inflate(bounds, -Px(2), -Px(2));
                if (i == active) TwPainter.FillRounded(g, pill, Px(5), colors.Back);
                else if (i == _hoverSegment) TwPainter.FillRounded(g, pill, Px(5), Color.FromArgb(20, colors.Fore));
                TextRenderer.DrawText(g, segments[i], Font, bounds, i == active ? colors.Fore : colors.Muted, flags | TextFormatFlags.HorizontalCenter);
            }
        }

        int selected = SelectedRow;
        for (int i = 0; i < RowCount; i++)
        {
            var row = RowBounds(i);
            var pill = Rectangle.Inflate(row, 0, -Px(1));
            if (i == selected) TwPainter.FillRounded(g, pill, Px(6), Color.FromArgb(36, colors.Accent));
            else if (i == _hoverRow) TwPainter.FillRounded(g, pill, Px(6), colors.Hover);
            if (Focused && i == _focusRow) TwPainter.StrokeRounded(g, pill, Px(6), Math.Max(1, S), Color.FromArgb(140, colors.Accent));
            var text = Rectangle.FromLTRB(row.Left + Px(10), row.Top, row.Right - Px(6), row.Bottom);
            TextRenderer.DrawText(g, RowText(i), Font, text, i == selected ? colors.Accent : colors.Fore, flags);
        }

        using var pen = new Pen(colors.Border);
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
