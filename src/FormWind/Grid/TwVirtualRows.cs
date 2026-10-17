using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Binds a DataGridView to a data source in VirtualMode: rows are loaded page by page as they are seen, and the most
/// recently used pages are cached. A table with a million rows keeps only the visible pages in memory.
/// <code>
/// var rows = new TwVirtualRows&lt;Order&gt;(grid, (start, count) =&gt; db.Orders.Skip(start).Take(count).ToList());
/// rows.Reset(db.Orders.Count());
/// </code>
/// A cell value is read from the column's <see cref="DataGridViewColumn.DataPropertyName"/> (or <see cref="DataGridViewColumn.Name"/>);
/// it can be changed with <see cref="CellValue"/>. <c>loadPage</c> is called on the UI thread and must be fast
/// (e.g. OFFSET/FETCH in the database). In VirtualMode the grid cannot sort by itself; sort in the query and call
/// <see cref="Reset"/>.
/// </summary>
public sealed class TwVirtualRows<T> : IDisposable
{
    private readonly DataGridView _grid;
    private readonly Func<int, int, IReadOnlyList<T>> _loadPage;
    private readonly Dictionary<int, LinkedListNode<(int Page, IReadOnlyList<T> Rows)>> _pages = new Dictionary<int, LinkedListNode<(int, IReadOnlyList<T>)>>();
    private readonly LinkedList<(int Page, IReadOnlyList<T> Rows)> _recent = new LinkedList<(int, IReadOnlyList<T>)>();
    private readonly Dictionary<string, PropertyInfo?> _properties = new Dictionary<string, PropertyInfo?>(StringComparer.Ordinal);

    /// <param name="grid">The grid to bind; VirtualMode is turned on and users cannot add rows.</param>
    /// <param name="loadPage">The rows in the <c>(start, count)</c> range; may return fewer on the last page.</param>
    /// <param name="pageSize">Number of rows per page.</param>
    /// <param name="cachedPages">Number of pages kept in memory (at least 2, to avoid thrashing at a page boundary while scrolling).</param>
    public TwVirtualRows(DataGridView grid, Func<int, int, IReadOnlyList<T>> loadPage, int pageSize = 100, int cachedPages = 8)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _loadPage = loadPage ?? throw new ArgumentNullException(nameof(loadPage));
        if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize));
        PageSize = pageSize;
        CachedPages = Math.Max(2, cachedPages);

        _grid.VirtualMode = true;
        _grid.AllowUserToAddRows = false;
        _grid.CellValueNeeded += OnCellValueNeeded;
    }

    public int PageSize { get; }

    public int CachedPages { get; }

    /// <summary>Number of rows (changed with <see cref="Reset"/>).</summary>
    public int RowCount => _grid.RowCount;

    /// <summary>The value of a row in a column; when not set, the property named by the column's <c>DataPropertyName</c> is read.</summary>
    public Func<T, DataGridViewColumn, object?>? CellValue { get; set; }

    /// <summary>Number of pages loaded (those served from the cache are not counted); for diagnostics and tests.</summary>
    public int PagesLoaded { get; private set; }

    /// <summary>Clears the cache and sets the row count (when the data or the sort changes).</summary>
    public void Reset(int rowCount)
    {
        _pages.Clear();
        _recent.Clear();
        _grid.RowCount = Math.Max(0, rowCount);
        _grid.Invalidate();
    }

    /// <summary>The row's data (its page is loaded if needed); <c>default</c> when there is no such row.</summary>
    public T? GetItem(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _grid.RowCount) return default;
        var rows = Page(rowIndex / PageSize);
        int offset = rowIndex % PageSize;
        return offset < rows.Count ? rows[offset] : default;
    }

    private IReadOnlyList<T> Page(int page)
    {
        if (_pages.TryGetValue(page, out var node))
        {
            _recent.Remove(node);
            _recent.AddFirst(node);
            return node.Value.Rows;
        }
        int start = page * PageSize;
        var rows = _loadPage(start, Math.Min(PageSize, _grid.RowCount - start)) ?? Array.Empty<T>();
        PagesLoaded++;
        _pages[page] = _recent.AddFirst((page, rows));
        while (_recent.Count > CachedPages)
        {
            _pages.Remove(_recent.Last!.Value.Page);
            _recent.RemoveLast();
        }
        return rows;
    }

    private void OnCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.ColumnIndex < 0) return;
        var item = GetItem(e.RowIndex);
        if (item is null) return;
        var column = _grid.Columns[e.ColumnIndex];
        if (CellValue != null)
        {
            e.Value = CellValue(item, column);
            return;
        }
        string name = string.IsNullOrEmpty(column.DataPropertyName) ? column.Name : column.DataPropertyName;
        if (!_properties.TryGetValue(name, out var property))
            _properties[name] = property = typeof(T).GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (property != null) e.Value = property.GetValue(item);
    }

    public void Dispose() => _grid.CellValueNeeded -= OnCellValueNeeded;
}
