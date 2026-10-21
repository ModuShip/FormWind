using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Date picker: a <see cref="TwTextBox"/>-style field with a calendar icon on the right; clicking it (or Alt+↓ / F4) shows a
/// <see cref="TwCalendar"/> in a <see cref="TwPopover"/>. The date can be typed too; it is parsed with <see cref="Culture"/> on focus loss or Enter.
/// The field is styled with classes such as <c>input</c>; the calendar with <c>menu:bg-* menu:border-*</c> (window),
/// <c>selected:bg-* selected:text-*</c> (selected day), <c>item:hover:bg-*</c> (hover), <c>accent-*</c> (today's ring).
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(ValueChanged))]
[DefaultProperty(nameof(Value))]
[Description("Stylable date picker.")]
public class TwDatePicker : TwTextBox
{
    private DateTime? _value;
    private string _format = "d";
    private CultureInfo _culture = CultureInfo.CurrentCulture;
    private TwCalendarColors _calendarColors = new TwCalendarColors();
    private TwPopover? _popover;
    private TwCalendar? _calendar;

    public TwDatePicker()
    {
        InnerTextBox.KeyDown += OnInnerKeyDown;
        InnerTextBox.Leave += (_, _) => Commit();
        PlaceholderText = "gg.aa.yyyy";
    }

    public event EventHandler? ValueChanged;

    [Category("Data"), DefaultValue(null)]
    public DateTime? Value
    {
        get => _value;
        set
        {
            var date = value?.Date;
            if (date != null && (date < MinDate.Date || date > MaxDate.Date)) return;
            bool changed = date != _value;
            _value = date;
            UpdateText();
            if (changed) ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Display format (<see cref="DateTime.ToString(string, IFormatProvider)"/>); the short date by default.</summary>
    [Category("Appearance"), DefaultValue("d")]
    public string Format
    {
        get => _format;
        set
        {
            _format = string.IsNullOrEmpty(value) ? "d" : value;
            UpdateText();
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
            UpdateText();
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal TwCalendarColors CalendarColors
    {
        get => _calendarColors;
        set => _calendarColors = value ?? new TwCalendarColors();
    }

    [Browsable(false)]
    public bool DroppedDown => _popover?.IsOpen == true;

    /// <summary>The open calendar (<c>null</c> when closed).</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwCalendar? Calendar => DroppedDown ? _calendar : null;

    private int IconWidth => (int)Math.Round(30 * TwDpi.DeviceDpi(this) / 96f);

    public override Rectangle DisplayRectangle
    {
        get
        {
            var rect = base.DisplayRectangle;
            return new Rectangle(rect.X, rect.Y, Math.Max(0, rect.Width - IconWidth), rect.Height);
        }
    }

    public void ShowCalendar() => EnsurePopover().Show(this);

    private TwPopover EnsurePopover()
    {
        if (_popover is not null) return _popover;
        var calendar = _calendar = new TwCalendar();
        var popover = _popover = new TwPopover { Content = calendar };
        popover.Opening += (_, _) => PrepareCalendar(calendar);
        calendar.DateSelected += (_, _) =>
        {
            Value = calendar.SelectedDate;
            popover.Close();
            InnerTextBox.Focus();
        };
        calendar.Cancelled += (_, _) => popover.Close();
        return popover;
    }

    private void PrepareCalendar(TwCalendar calendar)
    {
        Commit();
        calendar.Font = Font;
        calendar.Culture = _culture;
        calendar.Colors = _calendarColors;
        calendar.MinDate = MinDate;
        calendar.MaxDate = MaxDate;
        calendar.Size = calendar.GetPreferredSize(Size.Empty);
        calendar.SelectedDate = _value;
        if (_value is null) calendar.DisplayMonth = DateTime.Today;
    }

    /// <summary>Converts the text to a date; if invalid or empty the previous value is written back (empty text clears the value).</summary>
    public void Commit()
    {
        var text = InnerTextBox.Text.Trim();
        if (text.Length == 0)
        {
            Value = null;
            return;
        }
        if (DateTime.TryParseExact(text, _format, _culture, DateTimeStyles.None, out var exact)) Value = exact;
        else if (DateTime.TryParse(text, _culture, DateTimeStyles.None, out var parsed)) Value = parsed;
        else UpdateText();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var icon = IconBounds();
        {
            var color = Enabled ? ForeColor : Color.FromArgb(128, ForeColor);
            TwIconRenderer.DrawIcon(e.Graphics, "calendar", TwIconRenderer.EmPixels(Font.SizeInPoints, TwDpi.DeviceDpi(this)), icon, color);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && IconBounds().Contains(e.Location))
        {
            // Açıkken simgeye tıklamak takvimi kapatır; tıklama onu zaten kapattıysa yeniden açılmaz.
            EnsurePopover().Toggle(this);
            return;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = IconBounds().Contains(e.Location) ? Cursors.Hand : Cursors.IBeam;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Popover içeriği olan takvimi de atar.
            _popover?.Dispose();
        }
        base.Dispose(disposing);
    }

    private Rectangle IconBounds()
    {
        var border = BorderInsets();
        return new Rectangle(Width - border.Right - IconWidth, border.Top, IconWidth, Math.Max(0, Height - border.Vertical));
    }

    private void OnInnerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode == Keys.Down))
        {
            ShowCalendar();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            Commit();
            e.SuppressKeyPress = true;
        }
    }

    private void UpdateText() => InnerTextBox.Text = _value?.ToString(_format, _culture) ?? "";
}
