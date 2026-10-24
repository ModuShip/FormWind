using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Look of the skeleton rows of a loading grid (<see cref="TwGrid.SetLoading(DataGridView, bool, TwLoadingStyle)"/>).</summary>
public enum TwLoadingStyle
{
    /// <summary>The skeleton bars fade in and out together (the default).</summary>
    Pulse = 0,

    /// <summary>A light band sweeps across the skeleton bars from left to right.</summary>
    Shimmer = 1,
}

/// <summary>
/// Empty and loading states for a DataGridView:
/// <code>
/// TwGrid.SetEmptyText(grid, "No records");
/// TwGrid.SetLoading(grid, true);   // skeleton rows until the data arrives
/// </code>
/// On grids with FormWind classes, the header's sort arrow is drawn with an icon and editing controls follow the cell style.
/// </summary>
public static partial class TwGrid
{
    /// <summary>Text shown in the middle while there are no rows (<c>null</c> = none). The icon is changed with <see cref="SetEmptyIcon"/>.</summary>
    public static void SetEmptyText(DataGridView grid, string? text)
    {
        TwGridDecorator.For(grid).EmptyText = text;
        grid.Invalidate();
    }

    public static string? GetEmptyText(DataGridView grid) => TwGridDecorator.TryGet(grid)?.EmptyText;

    /// <summary>Empty state icon (default <c>search</c>; <c>null</c> = no icon).</summary>
    public static void SetEmptyIcon(DataGridView grid, string? icon)
    {
        TwGridDecorator.For(grid).EmptyIcon = icon;
        grid.Invalidate();
    }

    /// <summary>
    /// Loading state: skeleton rows are drawn when there are no rows; otherwise the rows are faded and the loading text is shown.
    /// </summary>
    public static void SetLoading(DataGridView grid, bool loading)
    {
        TwGridDecorator.For(grid).Loading = loading;
        grid.Invalidate();
    }

    /// <summary>
    /// Loading state with the look of the skeleton rows: <see cref="TwLoadingStyle.Pulse"/> fades them in and out,
    /// <see cref="TwLoadingStyle.Shimmer"/> sweeps a light band across them. Both stand still when animations are off.
    /// </summary>
    public static void SetLoading(DataGridView grid, bool loading, TwLoadingStyle style)
    {
        TwGridDecorator.For(grid).LoadingStyle = style;
        SetLoading(grid, loading);
    }

    /// <summary>Look of the skeleton rows while loading (default <see cref="TwLoadingStyle.Pulse"/>).</summary>
    public static void SetLoadingStyle(DataGridView grid, TwLoadingStyle style)
    {
        TwGridDecorator.For(grid).LoadingStyle = style;
        grid.Invalidate();
    }

    public static TwLoadingStyle GetLoadingStyle(DataGridView grid) => TwGridDecorator.TryGet(grid)?.LoadingStyle ?? TwLoadingStyle.Pulse;

    public static bool IsLoading(DataGridView grid) => TwGridDecorator.TryGet(grid)?.Loading == true;

    /// <summary>Loading text (default <see cref="TwTexts.Loading"/>).</summary>
    public static void SetLoadingText(DataGridView grid, string text) => TwGridDecorator.For(grid).LoadingText = text;

    /// <summary>
    /// Filter button in the headers: clicking it lists the column's distinct values with checkboxes. The grid's data source
    /// must be an <see cref="ITwFilterableList"/> (e.g. <see cref="TwSortableBindingList{T}"/>); it shows only on columns with a
    /// <c>DataPropertyName</c>. The button of a filtered column is drawn in the <c>accent-*</c> color. Long value lists get a
    /// search box; number and date columns of an <see cref="ITwGridSource"/> get min/max boxes.
    /// </summary>
    public static void EnableFilterMenu(DataGridView grid, bool enable = true)
    {
        TwGridDecorator.For(grid).FilterMenu = enable;
        grid.Invalidate();
    }

    /// <summary>Right-clicking a header opens a menu to show and hide columns (at least one column stays visible).</summary>
    public static void EnableColumnChooser(DataGridView grid, bool enable = true) => TwGridDecorator.For(grid).ColumnChooser = enable;

    /// <summary>The open filter, column or actions menu (<c>null</c> when none).</summary>
    public static ContextMenuStrip? OpenMenu(DataGridView grid) => TwGridDecorator.TryGet(grid)?.Menu;

    /// <summary>Opens a column's filter menu in code (for keyboard shortcuts or tests).</summary>
    public static void ShowFilterMenu(DataGridView grid, DataGridViewColumn column) => TwGridDecorator.For(grid).ShowFilterMenu(column);

    /// <summary>Opens the column chooser menu in code.</summary>
    public static void ShowColumnChooser(DataGridView grid) => TwGridDecorator.For(grid).ShowColumnChooser(grid.PointToClient(Control.MousePosition));

    /// <summary>Number of rows visible to the user (excluding the new row).</summary>
    public static int DataRowCount(DataGridView grid) => grid.Rows.Count - (grid.AllowUserToAddRows && grid.Rows.Count > 0 ? 1 : 0);
}

/// <summary>Colors and fonts of the <c>match:</c>, <c>group:</c> and <c>footer:</c> parts (<c>null</c> = derived from the grid).</summary>
internal sealed class TwGridParts
{
    public Color? MatchBack;
    public Color? MatchFore;
    public Color? GroupBack;
    public Color? GroupFore;
    public Font? GroupFont;
    public Color? FooterBack;
    public Color? FooterFore;
    public Font? FooterFont;
}

internal sealed partial class TwGridDecorator
{
    private static readonly ConditionalWeakTable<DataGridView, TwGridDecorator> Table = new ConditionalWeakTable<DataGridView, TwGridDecorator>();

    /// <summary>The filter menu gets a search box from this many values on.</summary>
    internal const int MenuSearchThreshold = 8;

    private readonly DataGridView _grid;
    private bool _styled;
    private TwGridParts _parts = new TwGridParts();

    private TwGridDecorator(DataGridView grid)
    {
        _grid = grid;
        grid.Paint += OnPaint;
        grid.CellPainting += OnCellPainting;
        grid.CellFormatting += OnCellFormatting;
        grid.CellFormatting += OnKeptColumnFormatting;
        grid.RowPostPaint += OnRowErrorPaint;
        grid.CellToolTipTextNeeded += OnRowErrorToolTip;
        grid.RowPostPaint += OnRowPostPaint;
        grid.EditingControlShowing += OnEditingControlShowing;
        grid.RowsAdded += (_, e) =>
        {
            InvalidateOverlay(TwGrid.DataRowCount(grid) == e.RowCount);
            OnDataChanged();
        };
        grid.RowsRemoved += (_, _) =>
        {
            InvalidateOverlay(TwGrid.DataRowCount(grid) == 0);
            OnDataChanged();
        };
        grid.DataBindingComplete += (_, _) => OnDataChanged();
        grid.Sorted += (_, _) => OnDataChanged();
        grid.CellValueChanged += (_, e) => OnDataChanged();
        grid.CellMouseDown += OnCellMouseDown;
        grid.ColumnHeaderMouseClick += OnColumnHeaderMouseClick;
        grid.Disposed += (_, _) =>
        {
            Menu?.Dispose();
            DisposeDetails();
        };
    }

    // Boş durum metni yalnızca satır sayısı sıfırdan çıkınca/sıfıra inince, yükleniyor örtüsü her değişimde yeniden
    // çizilir. Her RowsAdded'da tüm grid'i geçersiz kılmak, satırları parça parça ekleyen grid'lerde (VirtualMode)
    // yükleme boyunca yüzlerce tam yeniden çizim demekti.
    private void InvalidateOverlay(bool emptyChanged)
    {
        if (Loading || (emptyChanged && !string.IsNullOrEmpty(EmptyText))) _grid.Invalidate();
    }

    /// <summary>Raised when the rows or values change (the footer recomputes its totals).</summary>
    public event EventHandler? DataChanged;

    /// <summary>Raised when the part styles change (dark mode, classes).</summary>
    public event EventHandler? StyleChanged;

    private void OnDataChanged()
    {
        if (RowLayoutActive) ScheduleRows();
        DataChanged?.Invoke(this, EventArgs.Empty);
    }

    public TwGridParts Parts
    {
        get => _parts;
        set
        {
            _parts = value ?? new TwGridParts();
            _grid.Invalidate();
            StyleChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool FilterMenu { get; set; }

    public bool ColumnChooser { get; set; }

    public ContextMenuStrip? Menu { get; private set; }

    private ITwFilterableList? FilterSource => _grid.DataSource switch
    {
        ITwFilterableList list => list,
        BindingSource { List: ITwFilterableList list } => list,
        _ => null,
    };

    internal ITwGridSource? GridSource => FilterSource as ITwGridSource;

    private bool CanFilter(DataGridViewColumn column) =>
        FilterMenu && FilterSource != null && !string.IsNullOrEmpty(column.DataPropertyName);

    private bool IsFiltered(DataGridViewColumn column)
    {
        var source = FilterSource;
        if (source is null) return false;
        return source.GetColumnFilter(column.DataPropertyName) != null
            || (source is ITwGridSource grid && grid.TryGetRangeFilter(column.DataPropertyName, out _, out _));
    }

    /// <summary>Position of the filter button in the header.</summary>
    internal Rectangle FilterIconBounds(int columnIndex)
    {
        var cell = _grid.GetCellDisplayRectangle(columnIndex, -1, false);
        var style = _grid.ColumnHeadersDefaultCellStyle;
        var font = style.Font ?? _grid.Font;
        int em = TwIconRenderer.EmPixels(font.SizeInPoints, 96);
        int right = cell.Right - Math.Max(4, style.Padding.Right);
        return new Rectangle(right - em, cell.Y, em, cell.Height);
    }

    private void OnCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex >= 0)
        {
            // Grup başlığına tıklama grubu açar/kapatır.
            if (e.Button == MouseButtons.Left && GroupAt(e.RowIndex) is TwGridGroup group && e.Y < GroupHeaderHeight)
                _grid.BeginInvoke((Action)(() => SetGroupExpanded(group.Key, !group.Expanded)));
            return;
        }
        if (e.ColumnIndex < 0 || e.Button != MouseButtons.Left) return;
        var column = _grid.Columns[e.ColumnIndex];
        if (!CanFilter(column)) return;
        var cell = _grid.GetCellDisplayRectangle(e.ColumnIndex, -1, false);
        var point = new Point(cell.X + e.X, cell.Y + e.Y);
        if (!FilterIconBounds(e.ColumnIndex).Contains(point)) return;

        // Düğmeye tıklama sıralamasın: bu tık için sıralama kapatılır.
        var mode = column.SortMode;
        column.SortMode = DataGridViewColumnSortMode.Programmatic;
        _grid.BeginInvoke((Action)(() => column.SortMode = mode));
        ShowFilterMenu(column);
    }

    private void OnColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (!ColumnChooser || e.Button != MouseButtons.Right) return;
        var cell = _grid.GetCellDisplayRectangle(e.ColumnIndex, -1, false);
        ShowColumnChooser(new Point(cell.X + e.X, cell.Y + e.Y));
    }

    /// <summary>A menu in the grid's colors (the filter, column and actions menus); <see cref="Menu"/> until the next one.</summary>
    internal ContextMenuStrip CreateMenu()
    {
        Menu?.Dispose();
        Menu = new ContextMenuStrip { Renderer = new TwToolStripRenderer(MenuColors), ShowImageMargin = false, ShowCheckMargin = true, Font = _grid.Font };
        return Menu;
    }

    private TwMenuColors MenuColors => TwGridState.IsDark(_grid)
        ? new TwMenuColors
        {
            Back = ColorTranslator.FromHtml("#1f2937"), MenuBack = ColorTranslator.FromHtml("#1f2937"), Fore = ColorTranslator.FromHtml("#e5e7eb"),
            MenuBorder = ColorTranslator.FromHtml("#374151"), ItemHoverBack = ColorTranslator.FromHtml("#374151"), ItemHoverFore = Color.White,
            Separator = ColorTranslator.FromHtml("#374151"), Accent = ColorTranslator.FromHtml("#60a5fa"), InputBack = ColorTranslator.FromHtml("#111827"),
        }
        : new TwMenuColors { Accent = Accent };

    private ToolStripTextBox MenuTextBox(string hint)
    {
        var colors = MenuColors;
        var box = new ToolStripTextBox { BorderStyle = BorderStyle.FixedSingle, BackColor = colors.InputBack, ForeColor = colors.Fore, AutoSize = false };
        box.Width = (int)(180 * Scale);
        TwSelect.SetCueBanner(box.TextBox, hint);
        return box;
    }

    internal void ShowFilterMenu(DataGridViewColumn column)
    {
        var source = FilterSource;
        if (source is null || string.IsNullOrEmpty(column.DataPropertyName)) return;
        var property = column.DataPropertyName;
        var values = source.DistinctValues(property);
        var allowed = source.GetColumnFilter(property);

        var menu = CreateMenu();
        if (source is ITwGridSource gridSource && RangeType(gridSource.GetPropertyType(property)) is Type rangeType)
        {
            AddRangeBoxes(menu, gridSource, property, rangeType);
            menu.Items.Add(new ToolStripSeparator());
        }

        ToolStripTextBox? search = null;
        if (values.Count >= MenuSearchThreshold)
        {
            search = MenuTextBox(TwTexts.Current.Search);
            search.Name = "search";
            menu.Items.Add(search);
        }
        var all = new ToolStripMenuItem(TwTexts.Current.SelectAll) { Checked = allowed is null };
        menu.Items.Add(all);
        menu.Items.Add(new ToolStripSeparator());
        var items = new List<ToolStripMenuItem>();
        foreach (var value in values)
        {
            // Boş değerlerin anahtarı sabittir (TwSortableBindingList.EmptyValue); yalnızca görünen adı yerelleştirilir.
            var text = value == TwSortableBindingList<object>.EmptyValue ? TwTexts.Current.Blank : value;
            var item = new ToolStripMenuItem(text) { CheckOnClick = true, Checked = allowed is null || allowed.Contains(value), Tag = value };
            items.Add(item);
            menu.Items.Add(item);
        }
        menu.Items.Add(new ToolStripSeparator());
        var clear = new ToolStripMenuItem(TwTexts.Current.ClearFilter) { Enabled = IsFiltered(column) };
        menu.Items.Add(clear);

        bool bulk = false;
        void Apply()
        {
            if (bulk) return;
            var selected = items.Where(i => i.Checked).Select(i => (string)i.Tag!).ToList();
            source.SetColumnFilter(property, selected.Count == items.Count ? null : selected);
            all.Checked = selected.Count == items.Count;
            clear.Enabled = IsFiltered(column);
            _grid.Invalidate();
        }

        foreach (var item in items) item.CheckedChanged += (_, _) => Apply();
        all.Click += (_, _) =>
        {
            // Arama varken yalnızca görünen değerler işaretlenir/kaldırılır.
            var shown = items.Where(i => i.Available).ToList();
            bool check = !shown.All(i => i.Checked);
            bulk = true;
            foreach (var item in shown) item.Checked = check;
            bulk = false;
            Apply();
        };
        if (search != null)
        {
            search.TextChanged += (_, _) =>
            {
                var query = search.Text;
                menu.SuspendLayout();
                foreach (var item in items)
                    item.Available = query.Length == 0 || TwSortableBindingList<object>.Contains(item.Text, query);
                menu.ResumeLayout();
            };
        }
        clear.Click += (_, _) =>
        {
            source.SetColumnFilter(property, null);
            if (source is ITwGridSource ranged) ranged.SetRangeFilter(property, null, null);
            _grid.Invalidate();
            menu.Close();
        };
        // Onay kutularına tıklamak menüyü kapatmasın.
        menu.Closing += (_, e) =>
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked && clear.Selected == false) e.Cancel = true;
        };

        var icon = FilterIconBounds(column.Index);
        menu.Show(_grid, new Point(icon.Left, icon.Bottom));
        search?.Focus();
    }

    /// <summary>Type a range filter applies to (numbers, dates, durations); <c>null</c> otherwise.</summary>
    internal static Type? RangeType(Type? type)
    {
        if (type is null) return null;
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(decimal)) return type;
        return type.IsPrimitive && type != typeof(bool) && type != typeof(char) && type != typeof(IntPtr) && type != typeof(UIntPtr) ? type : null;
    }

    private void AddRangeBoxes(ContextMenuStrip menu, ITwGridSource source, string property, Type type)
    {
        source.TryGetRangeFilter(property, out var min, out var max);
        var low = MenuTextBox(TwTexts.Current.RangeMin);
        var high = MenuTextBox(TwTexts.Current.RangeMax);
        low.Name = "min";
        high.Name = "max";
        low.Text = RangeText(min);
        high.Text = RangeText(max);
        var fore = low.ForeColor;

        void Apply()
        {
            bool okLow = TryParseRange(low.Text, type, false, out var from);
            bool okHigh = TryParseRange(high.Text, type, true, out var to);
            low.ForeColor = okLow ? fore : Color.Red;
            high.ForeColor = okHigh ? fore : Color.Red;
            if (!okLow || !okHigh) return;
            source.SetRangeFilter(property, from, to);
            _grid.Invalidate();
        }

        low.TextChanged += (_, _) => Apply();
        high.TextChanged += (_, _) => Apply();
        menu.Items.Add(low);
        menu.Items.Add(high);
    }

    private static string RangeText(object? value) => value switch
    {
        null => "",
        DateTime date when date.TimeOfDay == TimeSpan.Zero || date.TimeOfDay == new TimeSpan(TimeSpan.TicksPerDay - 1) => date.ToString("d", CultureInfo.CurrentCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
        _ => value.ToString() ?? "",
    };

    /// <summary>Parses a range box with the current culture. A date without time as the upper bound covers that whole day.</summary>
    internal static bool TryParseRange(string text, Type type, bool upper, out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        try
        {
            value = TypeDescriptor.GetConverter(type).ConvertFromString(null, CultureInfo.CurrentCulture, text.Trim());
        }
        catch (Exception ex) when (!TwErrors.IsCritical(ex))
        {
            return false;
        }
        if (upper && value is DateTime date && date.TimeOfDay == TimeSpan.Zero) value = date.AddDays(1).AddTicks(-1);
        return value != null;
    }

    internal void ShowColumnChooser(Point location)
    {
        var menu = CreateMenu();
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            var title = string.IsNullOrEmpty(column.HeaderText) ? column.Name : column.HeaderText;
            var item = new ToolStripMenuItem(title) { CheckOnClick = true, Checked = column.Visible, Tag = column };
            item.CheckedChanged += (_, _) =>
            {
                // Son görünen kolon gizlenemez.
                if (!item.Checked && _grid.Columns.GetColumnCount(DataGridViewElementStates.Visible) <= 1)
                {
                    item.Checked = true;
                    return;
                }
                column.Visible = item.Checked;
            };
            menu.Items.Add(item);
        }
        menu.Closing += (_, e) =>
        {
            if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
        };
        menu.Show(_grid, location);
    }

    public static TwGridDecorator For(DataGridView grid) => Table.GetValue(grid, g => new TwGridDecorator(g));

    public static TwGridDecorator? TryGet(DataGridView grid) => Table.TryGetValue(grid, out var decorator) ? decorator : null;

    public string? EmptyText { get; set; }

    public string? EmptyIcon { get; set; } = "search";

    public bool Loading { get; set; }

    public TwLoadingStyle LoadingStyle { get; set; }

    private string? _loadingText;

    /// <summary><see cref="TwTexts.Loading"/> when not set.</summary>
    public string LoadingText
    {
        get => _loadingText ?? TwTexts.Current.Loading;
        set => _loadingText = value;
    }

    /// <summary>
    /// The text color the application designed its own column backgrounds for (the grid's original cell text color), while
    /// the classes set the cell text color; <c>null</c> otherwise. See <see cref="ColumnFore"/>.
    /// </summary>
    public Color? KeptPairFore { get; set; }

    /// <summary>
    /// The text color of a column whose background the application set without a text color: the classes' text color was
    /// chosen for the class background (light on a dark grid) and is unreadable on the application's background (a beige
    /// total column), so the column keeps the text color it was designed with. <c>null</c> when the column is not affected.
    /// </summary>
    internal static Color? ColumnFore(DataGridView grid, DataGridViewColumn column) =>
        TryGet(grid) is { KeptPairFore: Color fore } && !column.DefaultCellStyle.BackColor.IsEmpty && column.DefaultCellStyle.ForeColor.IsEmpty
            ? fore
            : null;

    private void OnKeptColumnFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.CellStyle is null || KeptPairFore is null) return;
        // Uygulama hücrenin ya da satırın yazı rengini kendisi verdiyse dokunulmaz.
        if (e.CellStyle.ForeColor.ToArgb() != _grid.DefaultCellStyle.ForeColor.ToArgb()) return;
        if (ColumnFore(_grid, _grid.Columns[e.ColumnIndex]) is Color fore) e.CellStyle.ForeColor = fore;
    }

    /// <summary>
    /// Color of the row error mark: while the classes hide the row headers (<c>row-header:hidden</c>), the error icon of
    /// <see cref="DataGridViewRow.ErrorText"/> has nowhere to go, so a row with an error gets a bar on its leading edge and the
    /// error as its cells' tooltip. <c>null</c> when the row headers are visible.
    /// </summary>
    public Color? RowErrorColor
    {
        get => _rowErrorColor;
        set
        {
            if (_rowErrorColor == value) return;
            _rowErrorColor = value;
            _grid.Invalidate();
        }
    }

    private Color? _rowErrorColor;

    /// <summary>The row error bar on the leading edge of a row (96 DPI: 3 px).</summary>
    internal Rectangle RowErrorBar(Rectangle rowBounds)
    {
        int width = _grid.LogicalToDeviceUnits(3);
        int x = _grid.RightToLeft == RightToLeft.Yes ? rowBounds.Right - width : rowBounds.Left;
        return new Rectangle(x, rowBounds.Top, width, rowBounds.Height - 1);
    }

    private void OnRowErrorPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
    {
        if (RowErrorColor is not Color color || _grid.RowHeadersVisible || !_grid.ShowRowErrors || e.Graphics is null) return;
        if (string.IsNullOrEmpty(e.ErrorText)) return;
        // Görünen satır alanı: yatay kaydırmada da sol kenar.
        var bounds = Rectangle.Intersect(e.RowBounds, _grid.DisplayRectangle);
        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, RowErrorBar(bounds));
    }

    private void OnRowErrorToolTip(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (RowErrorColor is null || _grid.RowHeadersVisible || !_grid.ShowRowErrors || e.RowIndex < 0 || !string.IsNullOrEmpty(e.ToolTipText)) return;
        var error = _grid.Rows.SharedRow(e.RowIndex).GetErrorText(e.RowIndex);
        if (!string.IsNullOrEmpty(error)) e.ToolTipText = error;
    }

    /// <summary>Color of the sort arrow and editing controls; turned on when FormWind classes are given.</summary>
    public Color Accent { get; set; } = ColorTranslator.FromHtml("#2563eb");

    /// <summary>Whether the header sort arrow is drawn with an icon (on for grids with classes).</summary>
    public bool Styled
    {
        get => _styled;
        set
        {
            if (_styled == value) return;
            _styled = value;
            _grid.Invalidate();
        }
    }

    private float Scale => TwDpi.DeviceDpi(_grid) / 96f;

    private Color Muted
    {
        get
        {
            var fore = _grid.DefaultCellStyle.ForeColor;
            var back = _grid.BackgroundColor;
            return Color.FromArgb((fore.R + back.R) / 2, (fore.G + back.G) / 2, (fore.B + back.B) / 2);
        }
    }

    /// <summary>Empty area below the rows (excluding the header).</summary>
    internal Rectangle BodyBounds
    {
        get
        {
            var rect = _grid.DisplayRectangle;
            int top = rect.Top + (_grid.ColumnHeadersVisible ? _grid.ColumnHeadersHeight : 0);
            return Rectangle.FromLTRB(rect.Left, top, rect.Right, Math.Max(top, rect.Bottom));
        }
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        PositionDetails();
        int rows = TwGrid.DataRowCount(_grid);
        var body = BodyBounds;
        if (body.Height <= 0) return;

        bool skeleton = Loading && rows == 0;
        UpdatePulse(skeleton);
        if (skeleton) PaintSkeleton(e.Graphics, body);
        else if (Loading) PaintBusyOverlay(e.Graphics, body);
        else if (rows == 0 && !string.IsNullOrEmpty(EmptyText)) PaintEmpty(e.Graphics, body);
    }

    private void PaintEmpty(Graphics g, Rectangle body)
    {
        var font = _grid.DefaultCellStyle.Font ?? _grid.Font;
        var color = Muted;
        var textSize = TextRenderer.MeasureText(g, EmptyText, font, new Size(body.Width - 32, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        int em = TwIconRenderer.EmPixels(font.SizeInPoints * 2f, TwDpi.DeviceDpi(_grid));
        bool icon = TwTheme.Current.HasIcon(EmptyIcon);
        int gap = icon ? (int)(10 * Scale) : 0;
        int total = (icon ? em : 0) + gap + textSize.Height;
        int y = body.Top + Math.Max((int)(16 * Scale), (body.Height - total) / 2);

        if (icon) TwIconRenderer.DrawIcon(g, EmptyIcon, em, new Rectangle(body.Left, y, body.Width, em), color);
        TextRenderer.DrawText(g, EmptyText, font, new Rectangle(body.Left + 16, y + (icon ? em : 0) + gap, body.Width - 32, textSize.Height), color,
            TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
    }

    /// <summary>Time of one shimmer sweep across the grid (ms).</summary>
    internal const int ShimmerPeriod = 1400;

    /// <summary>Clock of the shimmer (ms); tests set it to draw a chosen moment.</summary>
    [ThreadStatic]
    internal static int? ShimmerClockForTests;

    /// <summary>Center x of the shimmer band at <paramref name="clockMs"/>: it enters at the left and leaves at the right.</summary>
    internal static float ShimmerCenter(Rectangle body, int clockMs, float band)
    {
        float phase = (clockMs & int.MaxValue) % ShimmerPeriod / (float)ShimmerPeriod;
        return body.Left - band / 2 + (body.Width + band) * phase;
    }

    private void PaintSkeleton(Graphics g, Rectangle body)
    {
        bool dark = TwGridState.IsDark(_grid);
        bool shimmer = LoadingStyle == TwLoadingStyle.Shimmer;
        var baseColor = ColorTranslator.FromHtml(dark ? "#374151" : "#e5e7eb");
        // Parıltıda çubuklar sabit tondadır; nabızda tonları birlikte gidip gelir.
        var bar = shimmer ? baseColor : TwMotion.Blend(_grid.BackgroundColor, baseColor, TwDataList.PulseFactor(_grid));
        using var bars = new GraphicsPath();
        int rowHeight = Math.Max((int)(24 * Scale), _grid.RowTemplate.Height);
        int barHeight = Math.Max(4, (int)Math.Round(8 * Scale));
        int pad = (int)(12 * Scale);
        var columns = VisibleColumnBounds(body);
        if (columns.Count == 0) columns.Add((body.Left, body.Width));

        int row = 0;
        for (int y = body.Top; y + rowHeight <= body.Bottom + rowHeight / 2 && row < 50; y += rowHeight, row++)
        {
            for (int c = 0; c < columns.Count; c++)
            {
                var (x, width) = columns[c];
                // Sabit ama satırdan satıra değişen genişlikler (rastgele görünüm, her çizimde aynı).
                float ratio = 0.45f + ((row * 7 + c * 13) % 10) * 0.05f;
                int w = Math.Max(barHeight, (int)((width - pad * 2) * ratio));
                if (width - pad * 2 <= 0) continue;
                var rect = new Rectangle(x + pad, y + (rowHeight - barHeight) / 2, w, barHeight);
                TwPainter.FillRounded(g, rect, barHeight / 2f, bar);
                if (shimmer) bars.AddRectangle(rect);
            }
        }
        if (shimmer && (TwMotion.CanAnimate(_grid) || ShimmerClockForTests.HasValue)) PaintShimmer(g, body, bars, dark);
    }

    // Çubukların üstünden geçen açık bant: kenarlarda saydam, ortada yüzeyin açık tonu.
    private void PaintShimmer(Graphics g, Rectangle body, GraphicsPath bars, bool dark)
    {
        float band = Math.Max(80 * Scale, body.Width * 0.35f);
        float center = ShimmerCenter(body, ShimmerClockForTests ?? Environment.TickCount, band);
        var area = new RectangleF(center - band / 2, body.Top, band, body.Height);
        var light = dark ? Color.FromArgb(70, 255, 255, 255) : Color.FromArgb(170, 255, 255, 255);
        var state = g.Save();
        g.SetClip(bars);
        using (var brush = new LinearGradientBrush(new RectangleF(area.Left - 1, area.Top, area.Width + 2, area.Height), Color.Transparent, Color.Transparent, LinearGradientMode.Horizontal))
        {
            brush.InterpolationColors = new ColorBlend
            {
                Colors = new[] { Color.FromArgb(0, light), light, Color.FromArgb(0, light) },
                Positions = new[] { 0f, 0.5f, 1f },
            };
            g.FillRectangle(brush, area);
        }
        g.Restore(state);
    }

    private Timer? _pulse;

    private void UpdatePulse(bool active)
    {
        if (active && TwMotion.CanAnimate(_grid))
        {
            if (_pulse is null)
            {
                _pulse = new Timer { Interval = 33 };
                _pulse.Tick += (_, _) => _grid.Invalidate(BodyBounds);
                _grid.Disposed += (_, _) => _pulse.Dispose();
            }
            _pulse.Start();
        }
        else _pulse?.Stop();
    }

    private void PaintBusyOverlay(Graphics g, Rectangle body)
    {
        using (var veil = new SolidBrush(Color.FromArgb(150, _grid.BackgroundColor)))
            g.FillRectangle(veil, body);
        var font = _grid.DefaultCellStyle.Font ?? _grid.Font;
        TextRenderer.DrawText(g, LoadingText, font, body, _grid.DefaultCellStyle.ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    private List<(int X, int Width)> VisibleColumnBounds(Rectangle body)
    {
        var result = new List<(int, int)>();
        int x = body.Left + (_grid.RowHeadersVisible ? _grid.RowHeadersWidth : 0) - _grid.HorizontalScrollingOffset;
        foreach (DataGridViewColumn column in _grid.Columns)
        {
            if (!column.Visible) continue;
            if (x + column.Width > body.Left && x < body.Right) result.Add((x, column.Width));
            x += column.Width;
        }
        return result;
    }

    // Başlıkta sıralama oku: native üçgen yerine tema ikonu, metnin yanında.
    private void OnCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex >= 0)
        {
            if (SearchText != null && e.ColumnIndex >= 0) PaintMatches(e);
            return;
        }
        if (e.RowIndex != -1 || e.ColumnIndex < 0 || e.CellStyle is null || e.Graphics is null) return;
        var column = _grid.Columns[e.ColumnIndex];
        var direction = _styled ? column.HeaderCell.SortGlyphDirection : SortOrder.None;
        bool filter = CanFilter(column);
        if (direction == SortOrder.None && !filter) return;
        if (direction == SortOrder.None)
        {
            // Yalnızca süzme düğmesi: DataGridView başlığı çizer, düğme üstüne eklenir.
            e.Paint(e.ClipBounds, DataGridViewPaintParts.All);
            PaintFilterIcon(e.Graphics, column);
            e.Handled = true;
            return;
        }

        e.PaintBackground(e.ClipBounds, false);
        var style = e.CellStyle;
        var font = style.Font ?? _grid.Font;
        int em = TwIconRenderer.EmPixels(font.SizeInPoints, TwDpi.DeviceDpi(_grid));
        var content = new Rectangle(e.CellBounds.X + style.Padding.Left + 2, e.CellBounds.Y + style.Padding.Top,
            Math.Max(0, e.CellBounds.Width - style.Padding.Horizontal - 4), Math.Max(0, e.CellBounds.Height - style.Padding.Vertical));
        int gap = (int)(4 * Scale);
        var text = e.FormattedValue as string ?? column.HeaderText;
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter;
        // Ölçüm ellipsis'siz yapılır; çizimde 1–2 px yuvarlama farkı metni kesmesin diye pay bırakılır.
        int measured = TextRenderer.MeasureText(e.Graphics, text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + 2;
        if (filter) content.Width = Math.Max(0, content.Width - em - gap);
        var textWidth = Math.Min(measured, Math.Max(0, content.Width - em - gap));

        // Metin + ok birlikte hücre hizalamasına göre yerleşir.
        var block = TwCellPainting.Align(content, new Size(textWidth + gap + em, content.Height), style.Alignment);
        TextRenderer.DrawText(e.Graphics, text, font, new Rectangle(block.X, content.Y, textWidth, content.Height), style.ForeColor, flags);
        TwIconRenderer.DrawIcon(e.Graphics, direction == SortOrder.Ascending ? "arrow-up" : "arrow-down", (int)(em * 0.85f),
            new Rectangle(block.X + textWidth + gap, content.Y, em, content.Height), Accent);
        if (filter) PaintFilterIcon(e.Graphics, column);
        e.Handled = true;
    }

    private void PaintFilterIcon(Graphics g, DataGridViewColumn column)
    {
        var bounds = FilterIconBounds(column.Index);
        bool active = IsFiltered(column);
        var fore = _grid.ColumnHeadersDefaultCellStyle.ForeColor;
        var color = active ? Accent : Color.FromArgb(150, fore);
        TwIconRenderer.DrawIcon(g, "filter", (int)(bounds.Width * 0.85f), bounds, color);
    }

    // Düzenleme kontrolleri: kenarlıksız, hücre renkleriyle; koyu temada koyu kaydırma çubukları.
    private void OnEditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
    {
        bool inverted = TwGridInversion.IsActive(_grid);
        if ((!_styled && !inverted) || e.Control is null) return;
        var style = e.CellStyle;
        // Çevrilen grid'de düzenleme kontrolü ayrı penceredir (çevrilmez): renkleri ayrıca çevrilir.
        var back = inverted ? TwGridInversion.Invert(style.BackColor) : style.BackColor;
        var fore = inverted ? TwGridInversion.Invert(style.ForeColor) : style.ForeColor;
        bool dark = TwGridState.IsDark(_grid) || inverted;
        switch (e.Control)
        {
            case TextBox box:
                box.BorderStyle = BorderStyle.None;
                box.BackColor = back;
                box.ForeColor = fore;
                break;
            case ComboBox combo:
                combo.FlatStyle = FlatStyle.Flat;
                combo.BackColor = back;
                combo.ForeColor = fore;
                break;
        }
        if (e.Control.Parent != null) e.Control.Parent.BackColor = back;
        TwNativeTheme.Apply(e.Control, dark);
    }
}
