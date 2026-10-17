using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

public static partial class TwGrid
{
    /// <summary>
    /// Searches the grid: matching text is highlighted in the cells (<c>match:</c> part colors). With an
    /// <see cref="ITwGridSource"/> data source (<see cref="TwSortableBindingList{T}"/>) only rows matching in a visible column
    /// stay; unbound grids hide the other rows; other bound grids only highlight. <c>null</c> or empty text ends the search.
    /// </summary>
    public static void SetSearchText(DataGridView grid, string? text) => TwGridDecorator.For(grid).SetSearch(text);

    public static string? GetSearchText(DataGridView grid) => TwGridDecorator.TryGet(grid)?.SearchText;

    /// <summary>
    /// Searches the grid as the user types in <paramref name="box"/> (a TextBox, <see cref="TwTextBox"/> …), after a short
    /// pause (<paramref name="delay"/> ms). Escape clears the box. Returns an object that detaches the box when disposed.
    /// </summary>
    public static IDisposable AttachSearchBox(DataGridView grid, Control box, int delay = 200)
    {
        if (grid is null) throw new ArgumentNullException(nameof(grid));
        if (box is null) throw new ArgumentNullException(nameof(box));
        return new SearchBoxLink(grid, box, delay);
    }

    /// <summary>
    /// Groups the rows by a column: a header with the value and the row count is drawn above each group and clicking it
    /// collapses the group. A <see cref="ITwGridSource"/> data source orders the rows by the group first (the current sorting
    /// is kept within groups); otherwise consecutive rows with the same value form a group, so the rows must already be in
    /// order. Colors and font come from the <c>group:</c> part. <c>null</c> removes the grouping. Not supported in VirtualMode.
    /// </summary>
    public static void SetGroupBy(DataGridView grid, DataGridViewColumn? column) => TwGridDecorator.For(grid).GroupColumn = column;

    public static DataGridViewColumn? GetGroupBy(DataGridView grid) => TwGridDecorator.TryGet(grid)?.GroupColumn;

    /// <summary>The groups in display order (empty when the grid is not grouped).</summary>
    public static IReadOnlyList<TwGridGroup> GetGroups(DataGridView grid) =>
        TwGridDecorator.TryGet(grid)?.Groups ?? (IReadOnlyList<TwGridGroup>)Array.Empty<TwGridGroup>();

    /// <summary>Expands or collapses the group with the given key (<see cref="TwGridGroup.Key"/>).</summary>
    public static void SetGroupExpanded(DataGridView grid, string key, bool expanded) => TwGridDecorator.For(grid).SetGroupExpanded(key, expanded);

    public static void ExpandAllGroups(DataGridView grid) => TwGridDecorator.For(grid).SetAllGroupsExpanded(true);

    public static void CollapseAllGroups(DataGridView grid) => TwGridDecorator.For(grid).SetAllGroupsExpanded(false);

    /// <summary>Text of the group headers (default: <c>Column: value</c>); for example to add a total of the group.</summary>
    public static void SetGroupText(DataGridView grid, Func<TwGridGroup, string>? text)
    {
        TwGridDecorator.For(grid).GroupText = text;
        grid.Invalidate();
    }

    /// <summary>
    /// Row details: an expanded row shows the control <paramref name="factory"/> creates for it below its cells,
    /// <paramref name="height"/> pixels (96 DPI) high. Rows are expanded with a <see cref="TwExpanderColumn"/> or
    /// <see cref="SetRowExpanded"/>; the control is disposed when the row is collapsed. Expanded rows are remembered by their
    /// bound item, so sorting and filtering keep them. <c>null</c> removes the details.
    /// </summary>
    public static void SetRowDetails(DataGridView grid, Func<DataGridViewRow, Control?>? factory, int height = 160) =>
        TwGridDecorator.For(grid).SetDetails(factory, height);

    public static bool IsRowExpanded(DataGridView grid, int rowIndex) => TwGridDecorator.TryGet(grid)?.IsExpanded(rowIndex) == true;

    public static void SetRowExpanded(DataGridView grid, int rowIndex, bool expanded) => TwGridDecorator.For(grid).SetExpanded(rowIndex, expanded);

    public static void ToggleRowDetails(DataGridView grid, int rowIndex) => SetRowExpanded(grid, rowIndex, !IsRowExpanded(grid, rowIndex));

    /// <summary>The detail control of an expanded row (<c>null</c> when collapsed).</summary>
    public static Control? GetRowDetails(DataGridView grid, int rowIndex) => TwGridDecorator.TryGet(grid)?.DetailsOf(rowIndex);

    private sealed class SearchBoxLink : IDisposable
    {
        private readonly DataGridView _grid;
        private readonly Control _box;
        private readonly Timer _timer;

        public SearchBoxLink(DataGridView grid, Control box, int delay)
        {
            _grid = grid;
            _box = box;
            _timer = new Timer { Interval = Math.Max(1, delay) };
            _timer.Tick += OnTick;
            _box.TextChanged += OnTextChanged;
            _box.KeyDown += OnKeyDown;
            _grid.Disposed += (_, _) => Dispose();
        }

        private void OnTextChanged(object? sender, EventArgs e)
        {
            _timer.Stop();
            _timer.Start();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            _timer.Stop();
            if (!_grid.IsDisposed) SetSearchText(_grid, _box.Text);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Escape || _box.Text.Length == 0) return;
            _box.Text = "";
            e.Handled = e.SuppressKeyPress = true;
        }

        public void Dispose()
        {
            _timer.Dispose();
            _box.TextChanged -= OnTextChanged;
            _box.KeyDown -= OnKeyDown;
        }
    }
}

/// <summary>A group of rows (<see cref="TwGrid.SetGroupBy"/>).</summary>
public sealed class TwGridGroup
{
    internal TwGridGroup(string key, object? value, int firstRow, bool expanded)
    {
        Key = key;
        Value = value;
        FirstRow = firstRow;
        Expanded = expanded;
    }

    /// <summary>Display text of the value; blank values have <see cref="TwSortableBindingList{T}.EmptyValue"/>.</summary>
    public string Key { get; }

    public object? Value { get; }

    /// <summary>Index of the first row, which carries the header.</summary>
    public int FirstRow { get; }

    public int RowCount { get; internal set; }

    public bool Expanded { get; }

    internal readonly List<int> Rows = new List<int>();
}

// Satır düzeni: arama, gruplar ve satır detayları. Grup başlığı grubun ilk satırının üstüne, detay satırın altına eklenir:
// satır o kadar uzar ve hücre içeriği padding ile kayar; başlık ve detay zemini RowPostPaint'te çizilir.
internal sealed partial class TwGridDecorator
{
    private DataGridViewColumn? _groupColumn;
    private readonly List<TwGridGroup> _groups = new List<TwGridGroup>();
    private TwGridGroup?[] _groupAt = Array.Empty<TwGridGroup?>();
    private readonly HashSet<int> _collapsedRows = new HashSet<int>();
    private readonly HashSet<string> _collapsed = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<DataGridViewRow> _hiddenByUs = new HashSet<DataGridViewRow>();
    private bool[]? _matches;
    private bool _dirty;
    private bool _scheduled;
    private bool _refreshing;
    private bool _heightsTouched;

    private Func<DataGridViewRow, Control?>? _detailFactory;
    private int _detailHeight;
    private readonly Dictionary<object, Control?> _details = new Dictionary<object, Control?>();
    private readonly Dictionary<Control, Region> _clipped = new Dictionary<Control, Region>();

    public string? SearchText { get; private set; }

    public Func<TwGridGroup, string>? GroupText { get; set; }

    /// <summary>Whether grouping, an unbound search or details change the rows' height or visibility.</summary>
    private bool RowLayoutActive => _groupColumn != null || _details.Count > 0 || SearchesRows || _hiddenByUs.Count > 0;

    private bool SearchesRows => SearchText != null && _grid.DataSource is null && !_grid.VirtualMode;

    public IReadOnlyList<TwGridGroup> Groups
    {
        get
        {
            if (_dirty) RefreshRows();
            return _groups;
        }
    }

    public DataGridViewColumn? GroupColumn
    {
        get => _groupColumn;
        set
        {
            if (value != null && value.DataGridView != _grid) throw new ArgumentException("The column belongs to another grid.", nameof(value));
            _groupColumn = value;
            _collapsed.Clear();
            if (GridSource is ITwGridSource source)
                source.GroupProperty = string.IsNullOrEmpty(value?.DataPropertyName) ? null : value!.DataPropertyName;
            RefreshRows();
        }
    }

    // ---- Arama ----

    public void SetSearch(string? text)
    {
        SearchText = string.IsNullOrEmpty(text) ? null : text;
        if (GridSource is ITwGridSource source)
        {
            var properties = _grid.Columns.Cast<DataGridViewColumn>()
                .Where(c => c.Visible && !string.IsNullOrEmpty(c.DataPropertyName))
                .Select(c => c.DataPropertyName)
                .ToList();
            source.SetSearch(SearchText, properties);
        }
        RefreshRows();
        _grid.Invalidate();
    }

    /// <summary>Whether an unbound grid's search hides the row.</summary>
    internal bool IsSearchHidden(int row) => _matches != null && row >= 0 && row < _matches.Length && !_matches[row];

    private void ComputeMatches()
    {
        _matches = null;
        if (!SearchesRows) return;
        int rows = TwGrid.DataRowCount(_grid);
        _matches = new bool[rows];
        var columns = _grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).ToList();
        for (int i = 0; i < rows; i++)
        {
            var row = _grid.Rows[i];
            _matches[i] = columns.Any(c => row.Cells[c.Index].FormattedValue is string text
                && TwSortableBindingList<object>.Contains(text, SearchText!));
        }
    }

    /// <summary>Draws the cell with the search matches highlighted (plain text cells on one line).</summary>
    private void PaintMatches(DataGridViewCellPaintingEventArgs e)
    {
        var search = SearchText!;
        var column = _grid.Columns[e.ColumnIndex];
        if (column.CellType != typeof(DataGridViewTextBoxCell) || e.Graphics is null || e.CellStyle is null) return;
        if (!(e.FormattedValue is string text) || text.Length == 0 || e.CellStyle.WrapMode == DataGridViewTriState.True) return;
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        int index = compare.IndexOf(text, search, CompareOptions.IgnoreCase);
        if (index < 0) return;

        var g = e.Graphics;
        var style = e.CellStyle;
        e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.ContentForeground);
        if ((e.PaintParts & DataGridViewPaintParts.ContentForeground) == 0)
        {
            e.Handled = true;
            return;
        }

        var font = style.Font ?? _grid.Font;
        var bounds = e.CellBounds;
        var rect = new Rectangle(bounds.X + style.Padding.Left, bounds.Y + style.Padding.Top,
            Math.Max(0, bounds.Width - style.Padding.Horizontal - 1), Math.Max(0, bounds.Height - style.Padding.Vertical - 1));
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping
            | AlignmentFlags(style.Alignment);
        var exact = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        int Width(string s) => s.Length == 0 ? 0 : TextRenderer.MeasureText(g, s, font, new Size(int.MaxValue, int.MaxValue), exact).Width;

        // DataGridView metni TextRenderer'ın varsayılan yan boşluğuyla çizer; vurgu aynı konuma gelsin diye o boşluk hesaplanır.
        int lead = (TextRenderer.MeasureText(g, "x", font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width - Width("x")) / 2;
        int total = Width(text);
        int value = (int)style.Alignment;
        int x0 = (value & 0x222) != 0 ? rect.X + (rect.Width - total) / 2
            : (value & 0x444) != 0 ? rect.Right - lead - total
            : rect.X + lead;
        int line = font.Height;
        int y0 = (value & 0x7) != 0 ? rect.Y : (value & 0x700) != 0 ? rect.Bottom - line : rect.Y + (rect.Height - line) / 2;

        bool dark = TwGridState.IsDark(_grid);
        var back = _parts.MatchBack ?? ColorTranslator.FromHtml(dark ? "#854d0e" : "#fef08a");
        var highlights = new List<Rectangle>();
        while (index >= 0)
        {
            var hit = Rectangle.Intersect(new Rectangle(x0 + Width(text.Substring(0, index)), y0, Width(text.Substring(index, Math.Min(search.Length, text.Length - index))), line), rect);
            if (!hit.IsEmpty)
            {
                TwPainter.FillRounded(g, hit, 2 * Scale, back);
                highlights.Add(hit);
            }
            int next = index + Math.Max(1, search.Length);
            index = next < text.Length ? compare.IndexOf(text, search, next, CompareOptions.IgnoreCase) : -1;
        }

        var fore = TwCellPainting.Selected(e.State) ? style.SelectionForeColor : style.ForeColor;
        TextRenderer.DrawText(g, text, font, rect, fore, flags);
        if (_parts.MatchFore is Color matchFore)
        {
            var state = g.Save();
            foreach (var hit in highlights)
            {
                g.SetClip(hit);
                TextRenderer.DrawText(g, text, font, rect, matchFore, flags);
            }
            g.Restore(state);
        }
        e.Handled = true;
    }

    private static TextFormatFlags AlignmentFlags(DataGridViewContentAlignment alignment)
    {
        int value = (int)alignment;
        var flags = (value & 0x222) != 0 ? TextFormatFlags.HorizontalCenter : (value & 0x444) != 0 ? TextFormatFlags.Right : TextFormatFlags.Left;
        flags |= (value & 0x7) != 0 ? TextFormatFlags.Top : (value & 0x700) != 0 ? TextFormatFlags.Bottom : TextFormatFlags.VerticalCenter;
        return flags;
    }

    // ---- Gruplar ----

    internal TwGridGroup? GroupAt(int row) => !_dirty && row >= 0 && row < _groupAt.Length ? _groupAt[row] : null;

    /// <summary>Height of a group header (one row, at least 28 px).</summary>
    internal int GroupHeaderHeight => Math.Max((int)(28 * Scale), TwGridRowHeights.TryGet(_grid)?.Height ?? _grid.RowTemplate.Height);

    private int DetailHeight => Math.Max(8, (int)Math.Round(_detailHeight * Scale));

    public void SetGroupExpanded(string key, bool expanded)
    {
        if (expanded ? !_collapsed.Remove(key) : !_collapsed.Add(key)) return;
        RefreshRows();
    }

    public void SetAllGroupsExpanded(bool expanded)
    {
        _collapsed.Clear();
        if (!expanded) foreach (var group in Groups) _collapsed.Add(group.Key);
        RefreshRows();
    }

    private void ComputeGroups()
    {
        _groups.Clear();
        _collapsedRows.Clear();
        int rows = TwGrid.DataRowCount(_grid);
        if (_groupColumn is null || _groupColumn.DataGridView != _grid || _grid.VirtualMode)
        {
            _groupAt = Array.Empty<TwGridGroup?>();
            return;
        }

        var read = TwGridData.Reader(_grid, _groupColumn);
        _groupAt = new TwGridGroup?[rows];
        TwGridGroup? current = null;
        for (int i = 0; i < rows; i++)
        {
            // Aramaya uymayan satırlar gizlidir; grup başlığı grubun ilk görünen satırına gelir.
            if (_matches != null && !_matches[i]) continue;
            var value = read(i);
            var key = TwSortableBindingList<object>.Display(value);
            if (current is null || current.Key != key)
            {
                current = new TwGridGroup(key, value, i, !_collapsed.Contains(key));
                _groups.Add(current);
                _groupAt[i] = current;
            }
            else if (!current.Expanded)
            {
                _collapsedRows.Add(i);
            }
            current.RowCount++;
            current.Rows.Add(i);
        }
    }

    /// <summary>Height of a row: the base height plus its group header and details (a collapsed group shows only its header).</summary>
    internal int DesiredHeight(int row, int normal)
    {
        if (_dirty || row < 0) return normal;
        var group = GroupAt(row);
        if (group != null && !group.Expanded) return GroupHeaderHeight;
        int height = normal;
        if (group != null) height += GroupHeaderHeight;
        if (_details.Count > 0 && IsExpanded(row)) height += DetailHeight;
        return height;
    }

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle is null || (_groups.Count == 0 && _details.Count == 0)) return;
        var group = GroupAt(e.RowIndex);
        int top = group != null ? GroupHeaderHeight : 0;
        int bottom = group is { Expanded: false } || _details.Count == 0 || !IsExpanded(e.RowIndex) ? 0 : DetailHeight;
        if (top == 0 && bottom == 0) return;
        var padding = e.CellStyle.Padding;
        e.CellStyle.Padding = new Padding(padding.Left, padding.Top + top, padding.Right, padding.Bottom + bottom);
    }

    private void ScheduleRows()
    {
        _dirty = true;
        if (_scheduled || _refreshing || !_grid.IsHandleCreated) return;
        _scheduled = true;
        _grid.BeginInvoke((Action)(() =>
        {
            _scheduled = false;
            if (_dirty && !_grid.IsDisposed) RefreshRows();
        }));
    }

    /// <summary>Recomputes the groups and the search, then the rows' visibility and height.</summary>
    internal void RefreshRows()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            _dirty = false;
            ComputeMatches();
            ComputeGroups();
            ApplyVisibility();
            ApplyHeights();
        }
        finally
        {
            _refreshing = false;
        }
        PositionDetails();
        _grid.Invalidate();
    }

    private void ApplyVisibility()
    {
        int rows = TwGrid.DataRowCount(_grid);
        if (_matches is null && _collapsedRows.Count == 0 && _hiddenByUs.Count == 0) return;
        for (int i = 0; i < rows; i++)
        {
            bool visible = (_matches is null || _matches[i]) && !_collapsedRows.Contains(i);
            bool shown = (_grid.Rows.GetRowState(i) & DataGridViewElementStates.Visible) != 0;
            if (visible == shown) continue;
            var row = _grid.Rows[i];
            // Uygulamanın kendi gizlediği satırlara dokunulmaz.
            if (visible && !_hiddenByUs.Contains(row)) continue;
            if (!visible && _grid.CurrentCell?.RowIndex == i) MoveCurrentCellFrom(i);
            try
            {
                row.Visible = visible;
                if (visible) _hiddenByUs.Remove(row);
                else _hiddenByUs.Add(row);
            }
            catch (InvalidOperationException)
            {
                // Bağlı grid'de geçerli satır gizlenemez; satır görünür kalır.
            }
        }
        _hiddenByUs.RemoveWhere(r => r.DataGridView != _grid || r.Index < 0);
    }

    private void MoveCurrentCellFrom(int row)
    {
        var column = _grid.CurrentCell?.ColumnIndex ?? -1;
        int target = GroupStartOf(row);
        try
        {
            _grid.CurrentCell = target >= 0 && target != row && column >= 0 ? _grid[column, target] : null;
        }
        catch (InvalidOperationException)
        {
        }
    }

    private int GroupStartOf(int row)
    {
        foreach (var group in _groups)
            if (group.Rows.Contains(row)) return group.FirstRow;
        return -1;
    }

    private void ApplyHeights()
    {
        bool active = _groups.Count > 0 || _details.Count > 0;
        if (!active && !_heightsTouched) return;
        _heightsTouched = active;
        if (TwGridRowHeights.Applies(_grid))
        {
            TwGridRowHeights.For(_grid);
            if (_grid.RowCount > 0) _grid.UpdateRowHeightInfo(0, updateToEnd: true);
            return;
        }
        int normal = TwGridRowHeights.TryGet(_grid)?.Height ?? _grid.RowTemplate.Height;
        int rows = TwGrid.DataRowCount(_grid);
        for (int i = 0; i < rows; i++)
        {
            int want = DesiredHeight(i, normal);
            if (_grid.Rows.SharedRow(i).Height != want) _grid.Rows[i].Height = want;
        }
    }

    private void OnRowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
    {
        if (_dirty || (_groups.Count == 0 && _details.Count == 0) || e.Graphics is null) return;
        var bounds = e.RowBounds;
        var body = BodyBounds;
        int right = Math.Max(bounds.Right, body.Right);
        if (GroupAt(e.RowIndex) is TwGridGroup group)
            PaintGroupHeader(e.Graphics, new Rectangle(bounds.X, bounds.Y, right - bounds.X, Math.Min(bounds.Height, GroupHeaderHeight)), group);
        if (_details.Count > 0 && IsExpanded(e.RowIndex) && GroupAt(e.RowIndex) is not { Expanded: false })
        {
            var area = new Rectangle(bounds.X, bounds.Bottom - DetailHeight, right - bounds.X, DetailHeight);
            using var brush = new SolidBrush(_grid.DefaultCellStyle.BackColor.IsEmpty ? _grid.BackgroundColor : _grid.DefaultCellStyle.BackColor);
            e.Graphics.FillRectangle(brush, area);
        }
    }

    private Font? _boldFont;

    private void PaintGroupHeader(Graphics g, Rectangle band, TwGridGroup group)
    {
        var header = _grid.ColumnHeadersDefaultCellStyle;
        var back = _parts.GroupBack ?? (header.BackColor.IsEmpty ? SystemColors.Control : header.BackColor);
        var fore = _parts.GroupFore ?? (_grid.DefaultCellStyle.ForeColor.IsEmpty ? _grid.ForeColor : _grid.DefaultCellStyle.ForeColor);
        var baseFont = _grid.DefaultCellStyle.Font ?? _grid.Font;
        if (_boldFont is null || !_boldFont.FontFamily.Equals(baseFont.FontFamily) || _boldFont.Size != baseFont.Size)
        {
            _boldFont?.Dispose();
            _boldFont = new Font(baseFont, FontStyle.Bold);
        }
        var font = _parts.GroupFont ?? _boldFont;

        using (var brush = new SolidBrush(back)) g.FillRectangle(brush, band);
        using (var pen = new Pen(_grid.GridColor)) g.DrawLine(pen, band.Left, band.Bottom - 1, band.Right, band.Bottom - 1);

        int pad = (int)(8 * Scale);
        int em = TwIconRenderer.EmPixels(font.SizeInPoints, TwDpi.DeviceDpi(_grid));
        TwIconRenderer.DrawIcon(g, group.Expanded ? "chevron-down" : "chevron-right", em, new Rectangle(band.X + pad, band.Y, em, band.Height), fore);
        int x = band.X + pad + em + (int)(6 * Scale);

        var text = GroupText?.Invoke(group) ?? DefaultGroupText(group);
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
        int width = Math.Min(TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, band.Height), flags).Width, Math.Max(0, band.Right - x - pad));
        TextRenderer.DrawText(g, text, font, new Rectangle(x, band.Y, width, band.Height), fore, flags);

        var count = group.RowCount.ToString(CultureInfo.CurrentCulture);
        var muted = Color.FromArgb(160, fore);
        TextRenderer.DrawText(g, "(" + count + ")", baseFont, new Rectangle(x + width + (int)(6 * Scale), band.Y, Math.Max(0, band.Right - x - width), band.Height), muted, flags);
    }

    private string DefaultGroupText(TwGridGroup group)
    {
        var title = _groupColumn is null ? "" : string.IsNullOrEmpty(_groupColumn.HeaderText) ? _groupColumn.Name : _groupColumn.HeaderText;
        var value = group.Key == TwSortableBindingList<object>.EmptyValue ? TwTexts.Current.Blank
            : group.Value is null ? group.Key
            : TwGridData.Format(group.Value, _groupColumn!);
        return title.Length == 0 ? value : title + ": " + value;
    }

    // ---- Satır detayları ----

    public void SetDetails(Func<DataGridViewRow, Control?>? factory, int height)
    {
        DisposeDetails();
        _detailFactory = factory;
        _detailHeight = Math.Max(8, height);
        RefreshRows();
    }

    public bool IsExpanded(int row) => _details.Count > 0 && TwGridData.RowKey(_grid, row) is object key && _details.ContainsKey(key);

    public Control? DetailsOf(int row) => TwGridData.RowKey(_grid, row) is object key && _details.TryGetValue(key, out var control) ? control : null;

    public void SetExpanded(int row, bool expanded)
    {
        if (row < 0 || row >= TwGrid.DataRowCount(_grid)) throw new ArgumentOutOfRangeException(nameof(row));
        if (expanded && _detailFactory is null) throw new InvalidOperationException("Call TwGrid.SetRowDetails first.");
        // Bağlı olmayan grid'de anahtar satırın kendisidir: paylaşımdan çıkarılır ki nesnesi kalıcı olsun.
        var gridRow = _grid.Rows[row];
        var key = TwGridData.RowKey(_grid, row);
        if (key is null || IsExpanded(row) == expanded) return;
        if (expanded)
        {
            var control = _detailFactory!(gridRow);
            if (control != null)
            {
                control.Visible = false;
                _grid.Controls.Add(control);
                control.SendToBack();
            }
            _details[key] = control;
        }
        else if (_details.TryGetValue(key, out var control))
        {
            _details.Remove(key);
            DisposeDetail(control);
        }
        RefreshRows();
    }

    private void DisposeDetails()
    {
        foreach (var control in _details.Values) DisposeDetail(control);
        _details.Clear();
    }

    private void DisposeDetail(Control? control)
    {
        if (control is null) return;
        if (_clipped.TryGetValue(control, out var region))
        {
            _clipped.Remove(control);
            region.Dispose();
        }
        control.Dispose();
    }

    /// <summary>Places the detail controls over their rows (called on every paint, so scrolling moves them).</summary>
    private void PositionDetails()
    {
        if (_details.Count == 0 || _refreshing) return;
        var shown = new HashSet<Control>();
        var body = BodyBounds;
        int first = _grid.FirstDisplayedScrollingRowIndex;
        if (first >= 0 && !_dirty)
        {
            int last = Math.Min(_grid.RowCount - 1, first + _grid.DisplayedRowCount(true));
            for (int i = first; i <= last; i++)
            {
                if (!(TwGridData.RowKey(_grid, i) is object key) || !_details.TryGetValue(key, out var control) || control is null) continue;
                if (GroupAt(i) is { Expanded: false }) continue;
                if ((_grid.Rows.GetRowState(i) & DataGridViewElementStates.Visible) == 0) continue;
                var row = _grid.GetRowDisplayRectangle(i, false);
                if (row.IsEmpty) continue;
                int x = row.X + (_grid.RowHeadersVisible ? _grid.RowHeadersWidth : 0);
                var bounds = new Rectangle(x, row.Bottom - DetailHeight, Math.Max(0, body.Right - x), DetailHeight - 1);
                if (bounds.Bottom <= body.Top || bounds.Top >= body.Bottom) continue;
                if (control.Bounds != bounds) control.Bounds = bounds;
                Clip(control, bounds.Top < body.Top ? new Rectangle(0, body.Top - bounds.Top, bounds.Width, bounds.Bottom - body.Top) : (Rectangle?)null);
                if (!control.Visible) control.Visible = true;
                shown.Add(control);
            }
        }
        foreach (var control in _details.Values)
            if (control != null && !shown.Contains(control) && control.Visible) control.Visible = false;
    }

    // Başlığın altına kayan detayın üst kısmı kesilir (çocuk kontrol başlığın üstüne çizilmesin).
    private void Clip(Control control, Rectangle? visible)
    {
        _clipped.TryGetValue(control, out var old);
        if (visible is null)
        {
            if (old is null) return;
            control.Region = null;
            _clipped.Remove(control);
            old.Dispose();
            return;
        }
        var region = new Region(visible.Value);
        control.Region = region;
        _clipped[control] = region;
        old?.Dispose();
    }
}
