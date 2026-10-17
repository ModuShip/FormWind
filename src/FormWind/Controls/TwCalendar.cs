using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Takvim renkleri; FormWind class'lardan doldurur.</summary>
internal sealed class TwCalendarColors
{
    public Color Back { get; set; } = Color.White;
    public Color Border { get; set; } = ColorTranslator.FromHtml("#e5e7eb");       // gray-200
    public Color Fore { get; set; } = ColorTranslator.FromHtml("#111827");         // gray-900
    public Color Muted { get; set; } = ColorTranslator.FromHtml("#9ca3af");        // gray-400
    public Color Accent { get; set; } = ColorTranslator.FromHtml("#2563eb");       // blue-600
    public Color AccentFore { get; set; } = Color.White;
    public Color Hover { get; set; } = ColorTranslator.FromHtml("#f3f4f6");        // gray-100
}

/// <summary>
/// Owner-drawn month view: month/year title, previous/next arrows, weekdays and a 6×7 day grid. Today is drawn with a ring,
/// the selected day with an accent fill. Day and month names come from <see cref="Culture"/> (including the first day of the week).
/// Keyboard: arrows move by day/week, PageUp/PageDown by month, Home/End to the first/last day, Enter selects, Esc cancels.
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(DateSelected))]
[Description("Stillenebilir ay takvimi.")]
public class TwCalendar : Control
{
    private DateTime _displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime? _selectedDate;
    private DateTime _focusDate = DateTime.Today;
    private DateTime? _hoverDate;
    private CultureInfo _culture = CultureInfo.CurrentCulture;
    private TwCalendarColors _colors = new TwCalendarColors();

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _focusVisible = TwFocusVisible.LastInputWasKeyboard;
        Invalidate();
    }

    // Odak halkası yalnızca odak klavyeyle geldiyse çizilir (bkz. TwFocusVisible).
    private bool _focusVisible;

    public TwCalendar()
    {
        TwFocusVisible.EnsureInstalled();
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        TabStop = true;
        Size = GetPreferredSize(Size.Empty);
    }

    /// <summary>When a day is clicked or Enter is pressed.</summary>
    public event EventHandler? DateSelected;

    /// <summary>When Esc is pressed.</summary>
    public event EventHandler? Cancelled;

    [Category("Data"), DefaultValue(null)]
    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set
        {
            _selectedDate = value?.Date;
            if (value is DateTime date)
            {
                _focusDate = date.Date;
                DisplayMonth = date;
            }
            Invalidate();
        }
    }

    /// <summary>The displayed month (the day part is ignored).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DateTime DisplayMonth
    {
        get => _displayMonth;
        set
        {
            _displayMonth = new DateTime(value.Year, value.Month, 1);
            Invalidate();
        }
    }

    [Category("Behavior")]
    public DateTime MinDate { get; set; } = DateTime.MinValue;

    [Category("Behavior")]
    public DateTime MaxDate { get; set; } = DateTime.MaxValue;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            _culture = value ?? CultureInfo.CurrentCulture;
            Invalidate();
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal TwCalendarColors Colors
    {
        get => _colors;
        set
        {
            _colors = value ?? new TwCalendarColors();
            Invalidate();
        }
    }

    private int Scale(int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(this) / 96f);

    private int CellSize => Scale(34);

    private int Inset => Scale(10);

    private int HeaderHeight => Scale(36);

    private int WeekdayHeight => Scale(26);

    /// <summary>The date the month's first week starts on, based on the culture's first day of the week.</summary>
    public DateTime FirstVisibleDate
    {
        get
        {
            var first = _culture.DateTimeFormat.FirstDayOfWeek;
            int offset = ((int)_displayMonth.DayOfWeek - (int)first + 7) % 7;
            return _displayMonth.AddDays(-offset);
        }
    }

    public override Size GetPreferredSize(Size proposedSize) =>
        new Size(Inset * 2 + CellSize * 7, Inset * 2 + HeaderHeight + WeekdayHeight + CellSize * 6);

    /// <summary>A day's cell in the calendar.</summary>
    public Rectangle GetDayBounds(DateTime date)
    {
        int index = (int)(date.Date - FirstVisibleDate).TotalDays;
        if (index < 0 || index >= 42) return Rectangle.Empty;
        return new Rectangle(Inset + index % 7 * CellSize, Inset + HeaderHeight + WeekdayHeight + index / 7 * CellSize, CellSize, CellSize);
    }

    public Rectangle PreviousButtonBounds => new Rectangle(Inset, Inset + (HeaderHeight - Scale(28)) / 2, Scale(28), Scale(28));

    public Rectangle NextButtonBounds => new Rectangle(Width - Inset - Scale(28), Inset + (HeaderHeight - Scale(28)) / 2, Scale(28), Scale(28));

    /// <summary>Month and year shown in the title ("October 2026").</summary>
    [Browsable(false)]
    public string Title => _culture.TextInfo.ToTitleCase(_displayMonth.ToString("MMMM yyyy", _culture));

    /// <summary>Position of the keyboard-focused day in the visible grid (0–41); -1 when not visible.</summary>
    internal int FocusIndex
    {
        get
        {
            int index = (int)(_focusDate - FirstVisibleDate).TotalDays;
            return index is >= 0 and < 42 ? index : -1;
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new TwCalendarAccessibleObject(this);

    internal void SelectDate(DateTime date) => Select(date);

    private void NotifyFocusedDay()
    {
        if (FocusIndex < 0) return;
        int child = TwCalendarAccessibleObject.FirstDayChild + FocusIndex;
        if (Focused) AccessibilityNotifyClients(AccessibleEvents.Focus, child);
    }

    public void ShowPreviousMonth() => DisplayMonth = _displayMonth.AddMonths(-1);

    public void ShowNextMonth() => DisplayMonth = _displayMonth.AddMonths(1);

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var date = HitTest(e.Location);
        if (date != _hoverDate)
        {
            _hoverDate = date;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoverDate = null;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (PreviousButtonBounds.Contains(e.Location)) ShowPreviousMonth();
        else if (NextButtonBounds.Contains(e.Location)) ShowNextMonth();
        else if (HitTest(e.Location) is DateTime date && IsInRange(date)) Select(date);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown
            or Keys.Home or Keys.End or Keys.Enter or Keys.Escape
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _focusVisible = true;
        switch (e.KeyCode)
        {
            case Keys.Left: MoveFocus(_focusDate.AddDays(-1)); break;
            case Keys.Right: MoveFocus(_focusDate.AddDays(1)); break;
            case Keys.Up: MoveFocus(_focusDate.AddDays(-7)); break;
            case Keys.Down: MoveFocus(_focusDate.AddDays(7)); break;
            case Keys.PageUp: MoveFocus(_focusDate.AddMonths(-1)); break;
            case Keys.PageDown: MoveFocus(_focusDate.AddMonths(1)); break;
            case Keys.Home: MoveFocus(new DateTime(_focusDate.Year, _focusDate.Month, 1)); break;
            case Keys.End: MoveFocus(new DateTime(_focusDate.Year, _focusDate.Month, 1).AddMonths(1).AddDays(-1)); break;
            case Keys.Enter: if (IsInRange(_focusDate)) Select(_focusDate); break;
            case Keys.Escape: Cancelled?.Invoke(this, EventArgs.Empty); break;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using (var back = new SolidBrush(_colors.Back))
            g.FillRectangle(back, ClientRectangle);

        var format = _culture.DateTimeFormat;
        var font = Font;
        var bold = TwFonts.Create(new TwStyle { FontWeight = 600 }, font, TwTheme.Current);
        const TextFormatFlags center = TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;

        // Başlık: "Ekim 2026" ve oklar
        var title = Title;
        TextRenderer.DrawText(g, title, bold, new Rectangle(Inset, Inset, Width - Inset * 2, HeaderHeight), _colors.Fore, center);
        DrawArrow(g, "chevron-left", PreviousButtonBounds);
        DrawArrow(g, "chevron-right", NextButtonBounds);

        // Hafta günleri (kültürün ilk gününden başlayarak)
        for (int i = 0; i < 7; i++)
        {
            var day = (DayOfWeek)(((int)format.FirstDayOfWeek + i) % 7);
            var name = format.AbbreviatedDayNames[(int)day];
            if (name.Length > 2) name = name.Substring(0, 2);
            TextRenderer.DrawText(g, name, font, new Rectangle(Inset + i * CellSize, Inset + HeaderHeight, CellSize, WeekdayHeight), Opaque(_colors.Muted), center);
        }

        var today = DateTime.Today;
        var first = FirstVisibleDate;
        for (int i = 0; i < 42; i++)
        {
            var date = first.AddDays(i);
            var cell = GetDayBounds(date);
            var dot = Rectangle.Inflate(cell, -Scale(3), -Scale(3));
            bool selected = _selectedDate == date;
            bool inMonth = date.Month == _displayMonth.Month;
            bool enabled = IsInRange(date);

            if (selected) TwPainter.FillRounded(g, dot, dot.Width / 2f, _colors.Accent);
            else if (_hoverDate == date && enabled) TwPainter.FillRounded(g, dot, dot.Width / 2f, _colors.Hover);
            if (date == today && !selected) TwPainter.StrokeRounded(g, dot, dot.Width / 2f, Math.Max(1f, Scale(1)), _colors.Accent);
            if (Focused && _focusVisible && date == _focusDate && !selected)
                TwPainter.StrokeRounded(g, Rectangle.Inflate(dot, 1, 1), dot.Width / 2f + 1, 1, Color.FromArgb(120, _colors.Accent));

            var color = selected ? _colors.AccentFore : !enabled ? Color.FromArgb(90, _colors.Muted) : inMonth ? _colors.Fore : _colors.Muted;
            TextRenderer.DrawText(g, date.Day.ToString(_culture), date == today ? bold : font, cell, Opaque(color), center);
        }

        using var pen = new Pen(_colors.Border);
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    private void DrawArrow(Graphics g, string icon, Rectangle bounds)
    {
        if (_hoverArrow(bounds)) TwPainter.FillRounded(g, bounds, Scale(6), _colors.Hover);
        TwIconRenderer.DrawIcon(g, icon, TwIconRenderer.EmPixels(Font.SizeInPoints, TwDpi.DeviceDpi(this)), bounds, _colors.Fore);
    }

    private bool _hoverArrow(Rectangle bounds) => bounds.Contains(PointToClient(MousePosition)) && ClientRectangle.Contains(PointToClient(MousePosition));

    // TextRenderer alfa kanalını yok sayar; yarı saydam renkler zeminle karıştırılır.
    private Color Opaque(Color color)
    {
        if (color.A == 255) return color;
        var back = _colors.Back;
        int Mix(int fore, int under) => (fore * color.A + under * (255 - color.A)) / 255;
        return Color.FromArgb(Mix(color.R, back.R), Mix(color.G, back.G), Mix(color.B, back.B));
    }

    private DateTime? HitTest(Point point)
    {
        var grid = new Rectangle(Inset, Inset + HeaderHeight + WeekdayHeight, CellSize * 7, CellSize * 6);
        if (!grid.Contains(point)) return null;
        int column = (point.X - grid.X) / CellSize, row = (point.Y - grid.Y) / CellSize;
        return FirstVisibleDate.AddDays(row * 7 + column);
    }

    internal bool IsInRange(DateTime date) => date >= MinDate.Date && date <= MaxDate.Date;

    private void MoveFocus(DateTime date)
    {
        if (!IsInRange(date)) return;
        _focusDate = date;
        if (date.Month != _displayMonth.Month || date.Year != _displayMonth.Year) DisplayMonth = date;
        Invalidate();
        NotifyFocusedDay();
    }

    private void Select(DateTime date)
    {
        _selectedDate = date;
        _focusDate = date;
        if (date.Month != _displayMonth.Month) DisplayMonth = date;
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.Selection, TwCalendarAccessibleObject.FirstDayChild + FocusIndex);
        NotifyFocusedDay();
        DateSelected?.Invoke(this, EventArgs.Empty);
    }
}
