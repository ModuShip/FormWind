using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// A column of <see cref="TwDataList.Columns"/>: the header text, how wide it is and whether the header sorts by it. A column
/// with a <see cref="Width"/> keeps that width; columns without one share the rest in proportion to their <see cref="Weight"/>.
/// </summary>
public sealed class TwDataListColumn
{
    private string _text;
    private int _width;
    private float _weight = 1f;
    private HorizontalAlignment _alignment;
    private bool _sortable;

    public TwDataListColumn(string text, int width = 0)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _width = Math.Max(0, width);
    }

    internal event EventHandler? Changed;

    /// <summary>The header text.</summary>
    public string Text
    {
        get => _text;
        set => Set(ref _text, value ?? "");
    }

    /// <summary>Width in 96 DPI pixels; 0 = share the remaining width by <see cref="Weight"/>.</summary>
    public int Width
    {
        get => _width;
        set => Set(ref _width, Math.Max(0, value));
    }

    /// <summary>Share of the remaining width for a column without a <see cref="Width"/> (default 1).</summary>
    public float Weight
    {
        get => _weight;
        set => Set(ref _weight, value);
    }

    /// <summary>Where the header text sits in the column; draw the row parts the same way.</summary>
    public HorizontalAlignment Alignment
    {
        get => _alignment;
        set => Set(ref _alignment, value);
    }

    /// <summary>Whether a click on the header (or Enter / Space on it) sorts by this column.</summary>
    public bool Sortable
    {
        get => _sortable;
        set => Set(ref _sortable, value);
    }

    /// <summary>A name the application keeps with the column (a field, a data member); not used by the control.</summary>
    public string? Key { get; set; }

    private void Set<T>(ref T field, T value)
    {
        if (Equals(field, value)) return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public override string ToString() => _text;
}

/// <summary>
/// The header of a <see cref="TwDataList"/> with <see cref="TwDataList.Columns"/> (made by <see cref="TwDataList.CreateHeader"/>):
/// the column names above the rows, aligned with <see cref="TwDataListDrawEventArgs.GetColumnBounds"/>. A click on a sortable
/// column toggles <see cref="TwDataList.SortColumn"/> / <see cref="TwDataList.SortOrder"/> (▴ ascending, ▾ descending) and raises
/// <see cref="TwDataList.SortChanged"/>. Keyboard: Tab reaches the header; ←/→ move between sortable columns, Enter or
/// Space sorts. Screen readers see one column header per column, named with its sort state. Colors follow the list
/// (<c>header:text-*</c> for the text, <c>row:border-*</c> for the line under it).
/// </summary>
[ToolboxItem(false)]
public class TwDataListHeader : Control
{
    private readonly TwDataList _list;
    private int _hot = -1;
    private int _focus = -1;
    private bool _focusVisible;

    public TwDataListHeader(TwDataList list)
    {
        _list = list ?? throw new ArgumentNullException(nameof(list));
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Dock = DockStyle.Top;
        _list.HeaderChanged += OnListChanged;
        _list.FontChanged += OnListChanged;
        _list.LocationChanged += OnListChanged;
        UpdateTabStop();
    }

    protected override Size DefaultSize => new Size(200, 32);

    /// <summary>The list the header belongs to.</summary>
    public TwDataList List => _list;

    /// <summary>The sortable column the keyboard is on; -1 = none.</summary>
    internal int FocusColumn => _focus;

    private int Px(int value) => (int)Math.Round(value * TwDpi.DeviceDpi(this) / 96f);

    private void OnListChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        UpdateTabStop();
        Invalidate();
        // Ad, sıralama durumuyla değişir.
        if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
    }

    private void UpdateTabStop()
    {
        TabStop = _list.Columns.Any(c => c.Sortable);
        if (_focus >= _list.Columns.Count) _focus = -1;
    }

    // Başlığın sütunları listenin satırlarıyla aynı yerden başlar; ikisi aynı kapta değilse yalnız yatay fark kapatılır.
    private Rectangle ColumnRect(int column)
    {
        var area = _list.ColumnArea;
        int dx = _list.Parent != null && _list.Parent == Parent ? _list.Left - Left : 0;
        var rect = _list.ColumnRect(column, new Rectangle(area.X + dx, 0, area.Width, Height));
        return rect;
    }

    /// <summary>The cell of a column in the header (client coordinates), including the space around its text.</summary>
    internal Rectangle GetColumnBounds(int column) => ColumnRect(column);

    internal int ColumnAt(Point point)
    {
        for (int i = 0; i < _list.Columns.Count; i++)
        {
            var rect = ColumnRect(i);
            // Boşluklar da en yakın sütuna aittir; tıklama alanı sütunun yarım aralık kadar genişletilmişidir.
            rect.Inflate(Px(_list.ColumnGap) / 2, 0);
            if (rect.Contains(point)) return i;
        }
        return -1;
    }

    private bool IsSortable(int column) => column >= 0 && column < _list.Columns.Count && _list.Columns[column].Sortable;

    private void Sort(int column)
    {
        if (!IsSortable(column)) return;
        _focus = column;
        _list.ToggleSort(column);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int hot = ColumnAt(e.Location);
        if (!IsSortable(hot)) hot = -1;
        if (hot == _hot) return;
        _hot = hot;
        Cursor = hot >= 0 ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hot < 0) return;
        _hot = -1;
        Cursor = Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        int column = ColumnAt(e.Location);
        if (!IsSortable(column)) return;
        _focusVisible = false;
        Sort(column);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _focusVisible = true;
        switch (e.KeyCode)
        {
            case Keys.Left: Step(-1); e.Handled = true; break;
            case Keys.Right: Step(1); e.Handled = true; break;
            case Keys.Home: MoveTo(0, 1); e.Handled = true; break;
            case Keys.End: MoveTo(_list.Columns.Count - 1, -1); e.Handled = true; break;
            case Keys.Enter:
            case Keys.Space:
                Sort(_focus);
                e.Handled = true;
                break;
        }
    }

    // Yalnız sıralanabilir sütunlara durulur.
    private void Step(int step)
    {
        int rtl = TwRtl.NeedsMirroring(this) ? -1 : 1;
        MoveTo(_focus < 0 ? (step * rtl > 0 ? 0 : _list.Columns.Count - 1) : _focus + step * rtl, step * rtl);
    }

    private void MoveTo(int column, int step)
    {
        for (; column >= 0 && column < _list.Columns.Count; column += step)
            if (IsSortable(column))
            {
                _focus = column;
                Invalidate();
                if (IsHandleCreated) AccessibilityNotifyClients(AccessibleEvents.Focus, column);
                return;
            }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _focusVisible = TwFocusVisible.LastInputWasKeyboard;
        if (!IsSortable(_focus)) MoveTo(0, 1);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new HeaderAccessibleObject(this);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _list.HeaderChanged -= OnListChanged;
            _list.FontChanged -= OnListChanged;
            _list.LocationChanged -= OnListChanged;
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var colors = _list.Colors;
        var back = colors.GroupBack.IsEmpty ? colors.Back : colors.GroupBack;
        using (var brush = new SolidBrush(back)) g.FillRectangle(brush, ClientRectangle);
        using (var pen = new Pen(colors.Divider.IsEmpty ? Color.FromArgb(60, colors.Muted) : colors.Divider))
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

        var font = TwFonts.Create(new TwStyle { FontWeight = 600 }, _list.Font, TwTheme.Current, TwDpi.FontScale(this));
        int radius = Px(6);
        for (int i = 0; i < _list.Columns.Count; i++)
        {
            var column = _list.Columns[i];
            var cell = ColumnRect(i);
            if (cell.Width <= 0) continue;
            bool sorted = i == _list.SortColumn && _list.SortOrder != SortOrder.None;
            if (column.Sortable && i == _hot)
                TwPainter.FillRounded(g, Rectangle.Inflate(cell, Px(4), -Px(3)), radius, colors.Hover);
            if (Focused && _focusVisible && i == _focus)
                TwPainter.StrokeRounded(g, Rectangle.Inflate(cell, Px(4), -Px(3)), radius, 1, Color.FromArgb(140, colors.Accent ?? colors.SelectedFore));

            var color = sorted ? colors.Fore : colors.GroupFore;
            int glyph = sorted ? Px(8) + Px(4) : 0;
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            var textBounds = Rectangle.FromLTRB(cell.Left, cell.Top, Math.Max(cell.Left, cell.Right - glyph), cell.Bottom);
            int textWidth = Math.Min(textBounds.Width, TextRenderer.MeasureText(g, column.Text, font, Size.Empty, TextFormatFlags.NoPadding).Width);
            bool right = column.Alignment == HorizontalAlignment.Right, center = column.Alignment == HorizontalAlignment.Center;
            int textX = right ? cell.Right - glyph - textWidth : center ? cell.Left + (cell.Width - glyph - textWidth) / 2 : cell.Left;
            textX = Math.Max(cell.Left, textX);
            TextRenderer.DrawText(g, column.Text, font, new Rectangle(textX, cell.Top, textWidth, cell.Height), color, back, flags);
            if (sorted) PaintGlyph(g, new Point(textX + textWidth + Px(4), cell.Top + cell.Height / 2), _list.SortOrder == SortOrder.Ascending, color);
        }
    }

    // ▴ artan, ▾ azalan; yazı tipine bağlı olmasın diye üçgen çizilir.
    private void PaintGlyph(Graphics g, Point center, bool ascending, Color color)
    {
        int half = Px(4), height = Px(4);
        var points = ascending
            ? new[] { new Point(center.X - half, center.Y + height / 2), new Point(center.X + half, center.Y + height / 2), new Point(center.X, center.Y - height / 2 - 1) }
            : new[] { new Point(center.X - half, center.Y - height / 2), new Point(center.X + half, center.Y - height / 2), new Point(center.X, center.Y + height / 2 + 1) };
        var mode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var brush = new SolidBrush(color)) g.FillPolygon(brush, points);
        g.SmoothingMode = mode;
    }

    internal string GetAccessibleColumnName(int column)
    {
        var text = _list.Columns[column].Text;
        if (column != _list.SortColumn) return text;
        return _list.SortOrder switch
        {
            SortOrder.Ascending => text + ", " + TwTexts.Current.SortedAscending,
            SortOrder.Descending => text + ", " + TwTexts.Current.SortedDescending,
            _ => text,
        };
    }

    private sealed class HeaderAccessibleObject : TwListAccessibleObject
    {
        private readonly TwDataListHeader _header;

        public HeaderAccessibleObject(TwDataListHeader header) : base(header, AccessibleRole.Row) => _header = header;

        protected override int Count => _header._list.Columns.Count;

        protected override int ActiveIndex => _header._focus;

        protected override int IndexAt(Point client) => _header.ColumnAt(client);

        protected override AccessibleObject CreateChild(int index) => new Cell(_header, this, index);
    }

    private sealed class Cell : TwItemAccessibleObject
    {
        private readonly TwDataListHeader _header;

        public Cell(TwDataListHeader header, AccessibleObject parent, int index) : base(header, parent, index) => _header = header;

        protected override Rectangle ClientBounds => _header.ColumnRect(Index);

        public override string? Name
        {
            get => _header.GetAccessibleColumnName(Index);
            set { }
        }

        public override AccessibleRole Role => AccessibleRole.ColumnHeader;

        public override AccessibleStates State
        {
            get
            {
                var state = BaseState;
                if (!_header._list.Columns[Index].Sortable) state &= ~(AccessibleStates.Selectable | AccessibleStates.Focusable);
                if (_header._focus == Index && _header.Focused) state |= AccessibleStates.Focused;
                return state;
            }
        }

        public override string? DefaultAction => _header._list.Columns[Index].Sortable ? TwTexts.Current.SortBy : null;

        public override void DoDefaultAction() => _header._list.ToggleSort(Index);
    }
}
