using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FormWind;

public static partial class TwGrid
{
    /// <summary>
    /// Shows hierarchical rows in the grid: each node is a row, children are indented under their parent in
    /// <paramref name="treeColumn"/>, which gets an expand arrow. The grid must be unbound (no <c>DataSource</c>); its rows are
    /// replaced. Click the arrow or double-click the cell to expand and collapse; in the tree column → expands or moves to the
    /// first child and ← collapses or moves to the parent, + and − work in every column. Header clicks sort the children of
    /// each parent. Parent rows can show the totals of their children (<see cref="TwGridTree.SetTotal"/>), and a
    /// <see cref="TwGridFooter"/> counts only the rows without children, so totals are not counted twice.
    /// <code>
    /// var tree = TwGrid.SetTree(grid, colName, new[]
    /// {
    ///     new TwGridTreeNode("Line 1", null, null) { Nodes = { new TwGridTreeNode("Press 3", 540, 6), new TwGridTreeNode("Saw", 90, 2) } },
    /// });
    /// tree.SetTotal(colMinutes, TwAggregate.Sum);
    /// </code>
    /// </summary>
    public static TwGridTree SetTree(DataGridView grid, DataGridViewColumn treeColumn, IEnumerable<TwGridTreeNode> roots)
    {
        if (grid is null) throw new ArgumentNullException(nameof(grid));
        if (treeColumn is null) throw new ArgumentNullException(nameof(treeColumn));
        if (treeColumn.DataGridView != grid) throw new ArgumentException("The column must belong to the grid.", nameof(treeColumn));
        if (grid.DataSource != null || grid.VirtualMode) throw new InvalidOperationException("Tree rows need an unbound grid (no DataSource, no VirtualMode).");
        var tree = TwGridTree.For(grid);
        tree.Attach(treeColumn, roots ?? Enumerable.Empty<TwGridTreeNode>());
        return tree;
    }

    /// <summary>
    /// Shows a hierarchy of data objects as tree rows: each column's value is read from the object's
    /// <c>DataPropertyName</c> property, and <paramref name="children"/> returns an object's children (<c>null</c> = none).
    /// See <see cref="SetTree(DataGridView, DataGridViewColumn, IEnumerable{TwGridTreeNode})"/>.
    /// </summary>
    public static TwGridTree SetTree<T>(DataGridView grid, DataGridViewColumn treeColumn, IEnumerable<T> roots, Func<T, IEnumerable<T>?> children)
    {
        if (children is null) throw new ArgumentNullException(nameof(children));
        TwGridTreeNode Node(T item, int depth)
        {
            // Döngüsel veride sonsuz özyineleme olmasın.
            if (depth > 256) throw new InvalidOperationException("The hierarchy is too deep (or has a cycle).");
            var node = new TwGridTreeNode { DataItem = item };
            foreach (var child in children(item) ?? Enumerable.Empty<T>()) node.Nodes.Add(Node(child, depth + 1));
            return node;
        }
        return SetTree(grid, treeColumn, (roots ?? Enumerable.Empty<T>()).Select(item => Node(item, 0)).ToList());
    }

    /// <summary>The grid's tree rows (<c>null</c> when <see cref="SetTree(DataGridView, DataGridViewColumn, IEnumerable{TwGridTreeNode})"/> was not called).</summary>
    public static TwGridTree? GetTree(DataGridView grid) => TwGridTree.TryGet(grid);
}

/// <summary>A row of a tree grid (<see cref="TwGrid.SetTree(DataGridView, DataGridViewColumn, IEnumerable{TwGridTreeNode})"/>).</summary>
public sealed class TwGridTreeNode
{
    private bool _expanded;

    public TwGridTreeNode() => Nodes = new TwGridTreeNodeCollection(this);

    /// <summary>A node with cell values in column order (<c>grid.Columns[i]</c>).</summary>
    public TwGridTreeNode(params object?[] values) : this() => Values = values;

    /// <summary>Cell values by column index; <c>null</c> = read from <see cref="DataItem"/>.</summary>
    public object?[]? Values { get; set; }

    /// <summary>An object whose properties (the columns' <c>DataPropertyName</c>) give the cell values.</summary>
    public object? DataItem { get; set; }

    /// <summary>Icon before the text in the tree column (<see cref="TwIconRenderer.DrawIcon"/>).</summary>
    public string? IconName { get; set; }

    public object? Tag { get; set; }

    public TwGridTreeNodeCollection Nodes { get; }

    public TwGridTreeNode? Parent { get; internal set; }

    public int Level => Parent is null ? 0 : Parent.Level + 1;

    public bool HasChildren => Nodes.Count > 0;

    /// <summary>The node's row in the grid (-1 before the tree is shown).</summary>
    public int RowIndex { get; internal set; } = -1;

    internal TwGridTree? Tree { get; set; }

    public bool IsExpanded
    {
        get => _expanded;
        set
        {
            if (value == _expanded) return;
            if (Root.Tree is TwGridTree tree) tree.SetExpanded(this, value);
            else _expanded = value;
        }
    }

    internal void SetExpandedState(bool value) => _expanded = value;

    /// <summary>Whether every parent is expanded (the row is shown).</summary>
    public bool IsVisible => Parent is null || Parent.IsExpanded && Parent.IsVisible;

    internal TwGridTreeNode Root
    {
        get
        {
            var node = this;
            while (node.Parent != null) node = node.Parent;
            return node;
        }
    }

    /// <summary>Descendants in display order.</summary>
    public IEnumerable<TwGridTreeNode> Descendants()
    {
        foreach (var child in Nodes)
        {
            yield return child;
            foreach (var grandchild in child.Descendants()) yield return grandchild;
        }
    }
}

/// <summary>Children of a <see cref="TwGridTreeNode"/>; after changing nodes of a shown tree call <see cref="TwGridTree.Refresh"/>.</summary>
public sealed class TwGridTreeNodeCollection : System.Collections.ObjectModel.Collection<TwGridTreeNode>
{
    private readonly TwGridTreeNode _owner;

    internal TwGridTreeNodeCollection(TwGridTreeNode owner) => _owner = owner;

    protected override void InsertItem(int index, TwGridTreeNode item)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));
        item.Parent = _owner;
        base.InsertItem(index, item);
    }

    protected override void SetItem(int index, TwGridTreeNode item)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));
        this[index].Parent = null;
        item.Parent = _owner;
        base.SetItem(index, item);
    }

    protected override void RemoveItem(int index)
    {
        this[index].Parent = null;
        base.RemoveItem(index);
    }

    protected override void ClearItems()
    {
        foreach (var node in this) node.Parent = null;
        base.ClearItems();
    }

    // Sıralama: aynı düğümler yeni sırayla; ebeveynleri değişmez.
    internal void Reorder(IReadOnlyList<TwGridTreeNode> order)
    {
        Items.Clear();
        foreach (var node in order) Items.Add(node);
    }
}

/// <summary>Event data of <see cref="TwGridTree.NodeExpanded"/> and <see cref="TwGridTree.NodeCollapsed"/>.</summary>
public sealed class TwGridTreeEventArgs : EventArgs
{
    public TwGridTreeEventArgs(TwGridTreeNode node) => Node = node;

    public TwGridTreeNode Node { get; }
}

/// <summary>
/// The tree rows of a grid (<see cref="TwGrid.SetTree(DataGridView, DataGridViewColumn, IEnumerable{TwGridTreeNode})"/>): the
/// nodes, expanding and collapsing, sorting within parents and parent totals.
/// </summary>
public sealed class TwGridTree
{
    private static readonly ConditionalWeakTable<DataGridView, TwGridTree> Table = new ConditionalWeakTable<DataGridView, TwGridTree>();

    private readonly DataGridView _grid;
    private readonly List<TwGridTreeNode> _roots = new List<TwGridTreeNode>();
    private readonly List<TwGridTreeNode> _rows = new List<TwGridTreeNode>();
    private readonly Dictionary<DataGridViewColumn, TwAggregate> _totals = new Dictionary<DataGridViewColumn, TwAggregate>();
    private DataGridViewColumn? _column;
    private DataGridViewColumn? _sortColumn;
    private ListSortDirection _sortDirection;
    private int _indent = 16;
    private bool _building;

    private TwGridTree(DataGridView grid)
    {
        _grid = grid;
        grid.CellFormatting += OnCellFormatting;
        grid.RowPostPaint += OnRowPostPaint;
        grid.CellMouseClick += OnCellMouseClick;
        grid.CellMouseDoubleClick += OnCellMouseDoubleClick;
        grid.KeyDown += OnKeyDown;
        grid.ColumnHeaderMouseClick += OnColumnHeaderMouseClick;
        grid.ColumnRemoved += (_, e) =>
        {
            _totals.Remove(e.Column);
            if (e.Column == _column) _column = null;
        };
    }

    internal static TwGridTree For(DataGridView grid) => Table.GetValue(grid, g => new TwGridTree(g));

    internal static TwGridTree? TryGet(DataGridView grid) => grid != null && Table.TryGetValue(grid, out var tree) ? tree : null;

    /// <summary>Raised after a node is expanded (by the user or in code).</summary>
    public event EventHandler<TwGridTreeEventArgs>? NodeExpanded;

    /// <summary>Raised after a node is collapsed.</summary>
    public event EventHandler<TwGridTreeEventArgs>? NodeCollapsed;

    public DataGridView Grid => _grid;

    /// <summary>The column that shows the indentation and the expand arrow.</summary>
    public DataGridViewColumn? TreeColumn => _column;

    public IReadOnlyList<TwGridTreeNode> Roots => _roots;

    /// <summary>Indent per level (96 DPI pixels).</summary>
    public int Indent
    {
        get => _indent;
        set
        {
            _indent = Math.Max(0, value);
            _grid.Invalidate();
        }
    }

    /// <summary>The node of a row (<c>null</c> for rows that are not part of the tree).</summary>
    public TwGridTreeNode? NodeAt(int rowIndex) => rowIndex >= 0 && rowIndex < _rows.Count ? _rows[rowIndex] : null;

    /// <summary>The node of the current cell's row.</summary>
    public TwGridTreeNode? CurrentNode => NodeAt(_grid.CurrentCell?.RowIndex ?? -1);

    /// <summary>All nodes in display order (shown or not).</summary>
    public IEnumerable<TwGridTreeNode> AllNodes() => _roots.SelectMany(r => new[] { r }.Concat(r.Descendants()));

    internal void Attach(DataGridViewColumn column, IEnumerable<TwGridTreeNode> roots)
    {
        _column = column;
        _roots.Clear();
        _roots.AddRange(roots);
        foreach (var root in _roots)
        {
            root.Parent = null;
            root.Tree = this;
        }
        // Ağaçta yeni satır anlamsızdır; başlık tıklaması kardeşleri sıralar (düz sıralama hiyerarşiyi bozardı).
        _grid.AllowUserToAddRows = false;
        foreach (DataGridViewColumn c in _grid.Columns)
            if (c.SortMode == DataGridViewColumnSortMode.Automatic) c.SortMode = DataGridViewColumnSortMode.Programmatic;
        Refresh();
    }

    /// <summary>Parent rows show the <paramref name="aggregate"/> of their children in the column (where their own value is empty).</summary>
    public void SetTotal(DataGridViewColumn column, TwAggregate aggregate)
    {
        if (column is null) throw new ArgumentNullException(nameof(column));
        if (aggregate == TwAggregate.None) _totals.Remove(column);
        else _totals[column] = aggregate;
        Refresh();
    }

    /// <summary>Rebuilds the rows after nodes or their values changed; expanded nodes, the current node and sorting are kept.</summary>
    public void Refresh()
    {
        var current = CurrentNode;
        int currentColumn = _grid.CurrentCell?.ColumnIndex ?? -1;
        if (_sortColumn != null) SortNodes(_roots);
        _building = true;
        _grid.SuspendLayout();
        try
        {
            _rows.Clear();
            void Walk(IEnumerable<TwGridTreeNode> nodes)
            {
                foreach (var node in nodes)
                {
                    node.Tree = node.Parent is null ? this : null;
                    _rows.Add(node);
                    Walk(node.Nodes);
                }
            }
            Walk(_roots);

            _grid.Rows.Clear();
            if (_grid.ColumnCount == 0) return;
            var rows = new DataGridViewRow[_rows.Count];
            for (int i = 0; i < _rows.Count; i++)
            {
                var node = _rows[i];
                node.RowIndex = i;
                var row = new DataGridViewRow();
                row.CreateCells(_grid);
                foreach (DataGridViewColumn c in _grid.Columns) row.Cells[c.Index].Value = ValueOf(node, c);
                row.Visible = node.IsVisible;
                rows[i] = row;
            }
            _grid.Rows.AddRange(rows);
            foreach (DataGridViewColumn c in _grid.Columns)
                c.HeaderCell.SortGlyphDirection = c == _sortColumn ? (_sortDirection == ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending) : SortOrder.None;
        }
        finally
        {
            _grid.ResumeLayout();
            _building = false;
        }
        if (current is { RowIndex: >= 0 } && current.IsVisible && currentColumn >= 0) SetCurrent(current, currentColumn);
    }

    // Hücre değeri: Values'tan, yoksa DataItem'ın özelliğinden; boşsa ve sütunun toplamı varsa çocukların toplamı.
    internal object? ValueOf(TwGridTreeNode node, DataGridViewColumn column)
    {
        object? value = null;
        if (node.Values != null) value = column.Index < node.Values.Length ? node.Values[column.Index] : null;
        else if (node.DataItem != null && !string.IsNullOrEmpty(column.DataPropertyName))
            value = TypeDescriptor.GetProperties(node.DataItem).Find(column.DataPropertyName, true)?.GetValue(node.DataItem);
        if ((value is null || value == DBNull.Value) && node.HasChildren && _totals.TryGetValue(column, out var aggregate))
            value = Total(node, column, aggregate);
        return value;
    }

    // Ortalama, sayım, en az ve en çok yaprak satırlardan; toplam çocukların (kendi toplamlarıyla) toplamından.
    private object? Total(TwGridTreeNode node, DataGridViewColumn column, TwAggregate aggregate)
    {
        var values = aggregate == TwAggregate.Sum
            ? node.Nodes.Select(child => ValueOf(child, column)).ToList()
            : node.Descendants().Where(d => !d.HasChildren).Select(d => ValueOf(d, column)).ToList();
        return TwGridFooter.AggregateValue(values, aggregate);
    }

    /// <summary>Whether a row's values are counted by a <see cref="TwGridFooter"/> (rows without children).</summary>
    internal bool IsLeafRow(int rowIndex) => NodeAt(rowIndex) is not TwGridTreeNode node || !node.HasChildren;

    public void ExpandAll() => SetAll(true);

    public void CollapseAll() => SetAll(false);

    private void SetAll(bool expanded)
    {
        foreach (var node in AllNodes()) node.SetExpandedState(expanded && node.HasChildren);
        _grid.SuspendLayout();
        try
        {
            foreach (var node in _rows)
                if (node.RowIndex >= 0 && node.RowIndex < _grid.RowCount) _grid.Rows[node.RowIndex].Visible = node.IsVisible;
        }
        finally
        {
            _grid.ResumeLayout();
        }
        _grid.Invalidate();
    }

    internal void SetExpanded(TwGridTreeNode node, bool expanded)
    {
        if (!node.HasChildren) expanded = false;
        if (node.IsExpanded == expanded) return;
        // Kapanan dalın içindeki geçerli hücre dalın kendisine taşınır (gizlenen satırda hücre kalamaz).
        if (!expanded && CurrentNode is TwGridTreeNode current && IsAncestor(node, current))
            SetCurrent(node, _grid.CurrentCell?.ColumnIndex ?? (_column?.Index ?? 0));
        node.SetExpandedState(expanded);
        if (node.RowIndex >= 0 && node.RowIndex < _grid.RowCount)
        {
            _grid.SuspendLayout();
            try
            {
                foreach (var descendant in node.Descendants())
                    if (descendant.RowIndex >= 0 && descendant.RowIndex < _grid.RowCount) _grid.Rows[descendant.RowIndex].Visible = descendant.IsVisible;
            }
            finally
            {
                _grid.ResumeLayout();
            }
            _grid.InvalidateRow(node.RowIndex);
        }
        (expanded ? NodeExpanded : NodeCollapsed)?.Invoke(this, new TwGridTreeEventArgs(node));
    }

    private static bool IsAncestor(TwGridTreeNode ancestor, TwGridTreeNode node)
    {
        for (var parent = node.Parent; parent != null; parent = parent.Parent)
            if (parent == ancestor) return true;
        return false;
    }

    private void SetCurrent(TwGridTreeNode node, int column)
    {
        if (node.RowIndex < 0 || node.RowIndex >= _grid.RowCount || column < 0 || column >= _grid.ColumnCount) return;
        if (!_grid.Rows[node.RowIndex].Visible || !_grid.Columns[column].Visible) return;
        _grid.CurrentCell = _grid.Rows[node.RowIndex].Cells[column];
    }

    private float Scale => TwDpi.DeviceDpi(_grid) / 96f;

    private int ArrowWidth => (int)Math.Round(16 * Scale);

    // Ağaç sütununda girinti, ok ve simge için sola dolgu eklenir; metin varsayılan çizimle kalır (arama vurgusu dahil).
    private int LeadWidth(TwGridTreeNode node) =>
        (int)Math.Round(node.Level * _indent * Scale) + ArrowWidth + (int)Math.Round(4 * Scale)
        + (node.IconName != null ? (int)Math.Round(20 * Scale) : 0);

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_column is null || e.ColumnIndex != _column.Index || NodeAt(e.RowIndex) is not TwGridTreeNode node || e.CellStyle is null) return;
        var padding = e.CellStyle.Padding;
        e.CellStyle.Padding = new Padding(padding.Left + LeadWidth(node), padding.Top, padding.Right, padding.Bottom);
    }

    /// <summary>The expand arrow's area of a row in grid coordinates (empty when the row has no children or is not shown).</summary>
    public Rectangle GetExpanderBounds(int rowIndex)
    {
        if (_column is null || NodeAt(rowIndex) is not TwGridTreeNode node || !node.HasChildren) return Rectangle.Empty;
        var cell = _grid.GetCellDisplayRectangle(_column.Index, rowIndex, false);
        if (cell.IsEmpty) return Rectangle.Empty;
        int x = cell.X + _column.InheritedStyle.Padding.Left + 2 + (int)Math.Round(node.Level * _indent * Scale);
        return new Rectangle(x, cell.Y, ArrowWidth, cell.Height);
    }

    // Oklar ve simgeler satır çizildikten sonra, dolgu alanına çizilir.
    private void OnRowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
    {
        if (_column is null || !_column.Visible || NodeAt(e.RowIndex) is not TwGridTreeNode node) return;
        var cell = _grid.GetCellDisplayRectangle(_column.Index, e.RowIndex, false);
        if (cell.IsEmpty) return;
        bool selected = (e.State & DataGridViewElementStates.Selected) != 0;
        var style = _grid.Rows[e.RowIndex].Cells[_column.Index].InheritedStyle;
        var color = selected ? style.SelectionForeColor : style.ForeColor;
        if (color.IsEmpty) color = _grid.ForeColor;
        var state = e.Graphics.Save();
        e.Graphics.SetClip(cell);
        var font = style.Font ?? _grid.Font;
        int x = cell.X + _column.InheritedStyle.Padding.Left + 2 + (int)Math.Round(node.Level * _indent * Scale);
        if (node.HasChildren)
        {
            var arrow = new Rectangle(x, cell.Y, ArrowWidth, cell.Height);
            TwIconRenderer.DrawIcon(e.Graphics, node.IsExpanded ? "chevron-down" : "chevron-right",
                TwIconRenderer.EmPixels(font.SizeInPoints * 0.85f, TwDpi.DeviceDpi(_grid)), arrow, Color.FromArgb(selected ? 255 : 180, color));
        }
        if (node.IconName != null)
        {
            var icon = new Rectangle(x + ArrowWidth + (int)Math.Round(2 * Scale), cell.Y, (int)Math.Round(18 * Scale), cell.Height);
            TwIconRenderer.DrawIcon(e.Graphics, node.IconName, TwIconRenderer.EmPixels(font.SizeInPoints * 1.05f, TwDpi.DeviceDpi(_grid)), icon, color);
        }
        e.Graphics.Restore(state);
    }

    private void OnCellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _column is null || e.ColumnIndex != _column.Index || NodeAt(e.RowIndex) is not TwGridTreeNode node) return;
        var arrow = GetExpanderBounds(e.RowIndex);
        var cell = _grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
        var point = new Point(cell.X + e.X, cell.Y + e.Y);
        if (Rectangle.Inflate(arrow, (int)Math.Round(3 * Scale), 0).Contains(point)) node.IsExpanded = !node.IsExpanded;
    }

    private void OnCellMouseDoubleClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _column is null || e.ColumnIndex != _column.Index || NodeAt(e.RowIndex) is not TwGridTreeNode node) return;
        if (!node.HasChildren || !(_grid.ReadOnly || _column.ReadOnly)) return;
        if (GetExpanderBounds(e.RowIndex).Contains(_grid.PointToClient(Control.MousePosition))) return;   // tek tık zaten açtı/kapattı
        node.IsExpanded = !node.IsExpanded;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_grid.IsCurrentCellInEditMode || CurrentNode is not TwGridTreeNode node) return;
        int column = _grid.CurrentCell?.ColumnIndex ?? -1;
        bool inTree = _column != null && column == _column.Index;
        switch (e.KeyCode)
        {
            case Keys.Right when inTree && e.Modifiers == Keys.None && node.HasChildren:
                if (!node.IsExpanded) node.IsExpanded = true;
                else SetCurrent(node.Nodes[0], column);
                break;
            case Keys.Left when inTree && e.Modifiers == Keys.None && (node.IsExpanded || node.Parent != null):
                if (node.IsExpanded) node.IsExpanded = false;
                else SetCurrent(node.Parent!, column);
                break;
            case Keys.Add or Keys.Oemplus when node.HasChildren && !node.IsExpanded:
                node.IsExpanded = true;
                break;
            case Keys.Subtract or Keys.OemMinus when node.IsExpanded:
                node.IsExpanded = false;
                break;
            case Keys.Multiply when node.HasChildren:
                // * düğümün tüm alt dallarını açar (Windows ağaçları gibi).
                foreach (var branch in new[] { node }.Concat(node.Descendants()).Where(n => n.HasChildren)) branch.IsExpanded = true;
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (_building || e.Button != MouseButtons.Left || e.ColumnIndex < 0 || _roots.Count == 0) return;
        var column = _grid.Columns[e.ColumnIndex];
        if (column.SortMode != DataGridViewColumnSortMode.Programmatic) return;
        Sort(column, column == _sortColumn && _sortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending);
    }

    /// <summary>Sorts the children of every parent (and the roots) by a column; the hierarchy stays.</summary>
    public void Sort(DataGridViewColumn column, ListSortDirection direction)
    {
        _sortColumn = column ?? throw new ArgumentNullException(nameof(column));
        _sortDirection = direction;
        Refresh();
    }

    // Kökler ve her düğümün çocukları kendi içinde sıralanır.
    private void SortNodes(List<TwGridTreeNode> nodes)
    {
        var sorted = Sorted(nodes);
        nodes.Clear();
        nodes.AddRange(sorted);
        foreach (var node in nodes) SortChildren(node);
    }

    private void SortChildren(TwGridTreeNode node)
    {
        if (node.Nodes.Count == 0) return;
        node.Nodes.Reorder(Sorted(node.Nodes));
        foreach (var child in node.Nodes) SortChildren(child);
    }

    // Toplamlar decimal, yapraklar kendi türünde (int, double …): sayılar türlerinden bağımsız karşılaştırılır.
    private static int Compare(object? x, object? y)
    {
        static decimal? Number(object? value) => value is IConvertible and not (string or bool or DateTime or char) && value is not Enum
            ? Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture) : null;
        try
        {
            if (Number(x) is decimal a && Number(y) is decimal b) return a.CompareTo(b);
        }
        catch (OverflowException)
        {
        }
        return TwSortableBindingList<object>.CompareValues(x, y);
    }

    private List<TwGridTreeNode> Sorted(IList<TwGridTreeNode> nodes)
    {
        var column = _sortColumn!;
        int sign = _sortDirection == ListSortDirection.Ascending ? 1 : -1;
        var keyed = nodes.Select((n, i) => (Node: n, Index: i, Value: ValueOf(n, column))).ToList();
        // Kararlı sıralama: eşit değerler eski sırasını korur.
        keyed.Sort((a, b) =>
        {
            int result = Compare(a.Value, b.Value) * sign;
            return result != 0 ? result : a.Index.CompareTo(b.Index);
        });
        return keyed.Select(x => x.Node).ToList();
    }
}
