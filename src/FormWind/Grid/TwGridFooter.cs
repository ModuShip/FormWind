using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Summary of a column in a <see cref="TwGridFooter"/>.</summary>
public enum TwAggregate
{
    None,
    Sum,
    Average,
    /// <summary>Number of non-blank values.</summary>
    Count,
    Min,
    Max,
}

public static partial class TwGrid
{
    /// <summary>
    /// Adds a <see cref="TwGridFooter"/> right below the grid, in the same container: a grid docked to fill (or to the bottom)
    /// gets a footer docked below it, a grid docked to the top a footer docked under it; otherwise the grid gets shorter and the
    /// footer takes its place.
    /// </summary>
    public static TwGridFooter AddFooter(DataGridView grid)
    {
        if (grid is null) throw new ArgumentNullException(nameof(grid));
        var parent = grid.Parent ?? throw new InvalidOperationException("Add the grid to a container first.");
        var footer = new TwGridFooter { Grid = grid };
        int height = Math.Max(footer.Height, grid.ColumnHeadersHeight);
        footer.Height = height;
        parent.SuspendLayout();
        if (grid.Dock == DockStyle.Fill || grid.Dock == DockStyle.Bottom)
        {
            // Yerleştirme z-sırasının tersinedir: alt toplam grid'den önce yerleşsin diye grid'in arkasına konur.
            footer.Dock = DockStyle.Bottom;
            parent.Controls.Add(footer);
            parent.Controls.SetChildIndex(footer, parent.Controls.GetChildIndex(grid) + 1);
        }
        else if (grid.Dock == DockStyle.Top)
        {
            // Üste yerleşen grid'in hemen altı: grid'den sonra yerleşsin diye önüne konur.
            footer.Dock = DockStyle.Top;
            parent.Controls.Add(footer);
            parent.Controls.SetChildIndex(footer, parent.Controls.GetChildIndex(grid));
        }
        else
        {
            grid.Height = Math.Max(0, grid.Height - height);
            footer.SetBounds(grid.Left, grid.Bottom, grid.Width, height);
            footer.Anchor = grid.Anchor & ~AnchorStyles.Top | ((grid.Anchor & AnchorStyles.Bottom) != 0 ? AnchorStyles.Bottom : AnchorStyles.Top);
            parent.Controls.Add(footer);
            parent.Controls.SetChildIndex(footer, parent.Controls.GetChildIndex(grid));
        }
        parent.ResumeLayout(true);
        return footer;
    }
}

/// <summary>
/// Totals row below a DataGridView: its cells line up with the grid's columns (widths, order, horizontal scrolling) and show
/// a summary of each column (<see cref="SetAggregate"/>) or a fixed text (<see cref="SetText"/>). Totals are recomputed when
/// the rows or values change; with a filtered or searched <see cref="TwSortableBindingList{T}"/> only the shown rows count,
/// and in a tree grid (<see cref="TwGrid.SetTree(DataGridView, DataGridViewColumn, IEnumerable{TwGridTreeNode})"/>) only the
/// rows without children, collapsed or not.
/// Colors and font come from the grid's <c>footer:</c> part (otherwise the header background with bold text).
/// <code>
/// var footer = TwGrid.AddFooter(grid);
/// footer.SetText(colName, "Total");
/// footer.SetAggregate(colAmount, TwAggregate.Sum, "C2");
/// </code>
/// </summary>
[ToolboxItem(true)]
[Description("Totals row below a DataGridView.")]
public class TwGridFooter : Control
{
    private DataGridView? _grid;
    private readonly Dictionary<DataGridViewColumn, Cell> _cells = new Dictionary<DataGridViewColumn, Cell>();
    private Dictionary<DataGridViewColumn, string>? _texts;
    private bool _invalidated = true;
    private Font? _boldFont;

    public TwGridFooter()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 36;
    }

    /// <summary>The grid whose columns the footer follows.</summary>
    [Category("Data"), DefaultValue(null)]
    public DataGridView? Grid
    {
        get => _grid;
        set
        {
            if (_grid == value) return;
            if (_grid != null) Unhook(_grid);
            _grid = value;
            if (_grid != null) Hook(_grid);
            Recompute();
        }
    }

    /// <summary>Shows a summary of the column's values, formatted with <paramref name="format"/> (default: the column's format).</summary>
    public void SetAggregate(DataGridViewColumn column, TwAggregate aggregate, string? format = null)
    {
        if (column is null) throw new ArgumentNullException(nameof(column));
        if (aggregate == TwAggregate.None) _cells.Remove(column);
        else _cells[column] = new Cell { Aggregate = aggregate, Format = format };
        Recompute();
    }

    /// <summary>Shows a fixed text in the column's cell (for example "Total"); <c>null</c> clears it.</summary>
    public void SetText(DataGridViewColumn column, string? text)
    {
        if (column is null) throw new ArgumentNullException(nameof(column));
        if (text is null) _cells.Remove(column);
        else _cells[column] = new Cell { Text = text };
        Recompute();
    }

    /// <summary>Computes the column's cell from its values in the shown rows; <c>null</c> clears it.</summary>
    public void SetValue(DataGridViewColumn column, Func<IReadOnlyList<object?>, string?>? compute)
    {
        if (column is null) throw new ArgumentNullException(nameof(column));
        if (compute is null) _cells.Remove(column);
        else _cells[column] = new Cell { Compute = compute };
        Recompute();
    }

    /// <summary>The text shown in the column's cell (empty when none).</summary>
    public string GetText(DataGridViewColumn column)
    {
        EnsureTexts();
        return _texts!.TryGetValue(column, out var text) ? text : "";
    }

    /// <summary>Recomputes the totals (done automatically when the grid's rows or values change).</summary>
    public void Recompute()
    {
        _invalidated = true;
        Invalidate();
    }

    private void Hook(DataGridView grid)
    {
        var decorator = TwGridDecorator.For(grid);
        decorator.DataChanged += OnDataChanged;
        decorator.StyleChanged += OnLayoutChanged;
        grid.ColumnWidthChanged += OnLayoutChanged;
        grid.ColumnDisplayIndexChanged += OnLayoutChanged;
        grid.ColumnStateChanged += OnLayoutChanged;
        grid.Scroll += OnLayoutChanged;
        grid.RowHeadersWidthChanged += OnLayoutChanged;
        grid.Resize += OnLayoutChanged;
        grid.Disposed += OnGridDisposed;
    }

    private void Unhook(DataGridView grid)
    {
        if (TwGridDecorator.TryGet(grid) is TwGridDecorator decorator)
        {
            decorator.DataChanged -= OnDataChanged;
            decorator.StyleChanged -= OnLayoutChanged;
        }
        grid.ColumnWidthChanged -= OnLayoutChanged;
        grid.ColumnDisplayIndexChanged -= OnLayoutChanged;
        grid.ColumnStateChanged -= OnLayoutChanged;
        grid.Scroll -= OnLayoutChanged;
        grid.RowHeadersWidthChanged -= OnLayoutChanged;
        grid.Resize -= OnLayoutChanged;
        grid.Disposed -= OnGridDisposed;
    }

    private void OnDataChanged(object? sender, EventArgs e) => Recompute();

    private void OnLayoutChanged(object? sender, EventArgs e) => Invalidate();

    private void OnGridDisposed(object? sender, EventArgs e) => Grid = null;

    private void EnsureTexts()
    {
        if (!_invalidated && _texts != null) return;
        _invalidated = false;
        _texts = new Dictionary<DataGridViewColumn, string>();
        if (_grid is null) return;
        int rows = TwGrid.DataRowCount(_grid);
        foreach (var pair in _cells.ToList())
        {
            var column = pair.Key;
            var cell = pair.Value;
            if (column.DataGridView != _grid)
            {
                _cells.Remove(column);
                continue;
            }
            if (cell.Text != null)
            {
                _texts[column] = cell.Text;
                continue;
            }
            var read = TwGridData.Reader(_grid, column);
            var values = new List<object?>(rows);
            var tree = TwGridTree.TryGet(_grid);
            for (int i = 0; i < rows; i++)
            {
                // Aramayla gizlenen satırlar sayılmaz; kapalı gruplardaki satırlar sayılır. Ağaçta üst satırlar
                // çocuklarının toplamını taşır, yalnızca yapraklar sayılır.
                if (TwGridDecorator.TryGet(_grid)?.IsSearchHidden(i) == true) continue;
                if (tree != null && !tree.IsLeafRow(i)) continue;
                values.Add(read(i));
            }
            _texts[column] = cell.Compute != null ? cell.Compute(values) ?? "" : Aggregate(values, cell, column);
        }
    }

    private static string Aggregate(List<object?> values, Cell cell, DataGridViewColumn column)
    {
        if (cell.Aggregate == TwAggregate.Count) return AggregateValue(values, TwAggregate.Count)!.ToString()!;
        var result = AggregateValue(values, cell.Aggregate);
        if (result is null) return "";
        if (cell.Format != null && result is IFormattable formattable) return formattable.ToString(cell.Format, CultureInfo.CurrentCulture);
        return TwGridData.Format(result, column);
    }

    /// <summary>The summary of values (<c>null</c> when there is nothing to summarize); Count is an <see cref="int"/>, sums a <see cref="decimal"/>.</summary>
    internal static object? AggregateValue(IReadOnlyList<object?> values, TwAggregate aggregate)
    {
        var present = values.Where(v => v != null && v != DBNull.Value && !(v is string s && s.Length == 0)).ToList();
        if (aggregate == TwAggregate.Count) return present.Count;
        if (present.Count == 0 || aggregate == TwAggregate.None) return null;
        switch (aggregate)
        {
            case TwAggregate.Min:
            case TwAggregate.Max:
                var sorted = present.OrderBy(v => v, Comparer<object?>.Create(TwSortableBindingList<object>.CompareValues)).ToList();
                return aggregate == TwAggregate.Min ? sorted[0] : sorted[sorted.Count - 1];
            default:
                var numbers = present.Select(ToDecimal).Where(n => n.HasValue).Select(n => n!.Value).ToList();
                if (numbers.Count == 0) return null;
                decimal sum = numbers.Sum();
                return aggregate == TwAggregate.Average ? sum / numbers.Count : sum;
        }
    }

    private static decimal? ToDecimal(object? value)
    {
        try
        {
            return value switch
            {
                decimal d => d,
                double d when double.IsNaN(d) || double.IsInfinity(d) => null,
                IConvertible convertible when !(value is string) && !(value is DateTime) && !(value is bool) => convertible.ToDecimal(CultureInfo.InvariantCulture),
                string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed) => parsed,
                _ => null,
            };
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var grid = _grid;
        var parts = grid is null ? new TwGridParts() : TwGridDecorator.For(grid).Parts;
        var headerBack = grid?.ColumnHeadersDefaultCellStyle.BackColor ?? SystemColors.Control;
        var back = parts.FooterBack ?? (headerBack.IsEmpty ? SystemColors.Control : headerBack);
        using (var brush = new SolidBrush(back)) g.FillRectangle(brush, ClientRectangle);
        if (grid is null) return;

        using (var pen = new Pen(grid.GridColor)) g.DrawLine(pen, 0, 0, Width, 0);
        EnsureTexts();
        var cellFont = grid.DefaultCellStyle.Font ?? grid.Font;
        if (_boldFont is null || !_boldFont.FontFamily.Equals(cellFont.FontFamily) || _boldFont.Size != cellFont.Size)
        {
            _boldFont?.Dispose();
            _boldFont = new Font(cellFont, FontStyle.Bold);
        }
        var font = parts.FooterFont ?? _boldFont;
        var fore = parts.FooterFore ?? (grid.DefaultCellStyle.ForeColor.IsEmpty ? grid.ForeColor : grid.DefaultCellStyle.ForeColor);

        foreach (var (column, bounds) in ColumnBounds(grid))
        {
            if (!_texts!.TryGetValue(column, out var text) || text.Length == 0) continue;
            var style = column.InheritedStyle;
            var content = new Rectangle(bounds.X + style.Padding.Left + 2, 1, Math.Max(0, bounds.Width - style.Padding.Horizontal - 4), Height - 1);
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            int value = (int)style.Alignment;
            flags |= (value & 0x222) != 0 ? TextFormatFlags.HorizontalCenter : (value & 0x444) != 0 ? TextFormatFlags.Right : TextFormatFlags.Left;
            TextRenderer.DrawText(g, text, font, content, fore, flags);
        }
    }

    /// <summary>Bounds of the visible columns in footer coordinates, following the grid's horizontal scrolling.</summary>
    internal IEnumerable<(DataGridViewColumn Column, Rectangle Bounds)> ColumnBounds(DataGridView grid)
    {
        int offset = PointToClient(grid.PointToScreen(Point.Empty)).X;
        int x = offset + grid.DisplayRectangle.X + (grid.RowHeadersVisible ? grid.RowHeadersWidth : 0);
        int scroll = grid.HorizontalScrollingOffset;
        var column = grid.Columns.GetFirstColumn(DataGridViewElementStates.Visible);
        while (column != null)
        {
            // Dondurulmuş kolonlar kaymaz.
            int left = column.Frozen ? x : x - scroll;
            yield return (column, new Rectangle(left, 0, column.Width, Height));
            x += column.Width;
            column = grid.Columns.GetNextColumn(column, DataGridViewElementStates.Visible, DataGridViewElementStates.None);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Grid = null;
            _boldFont?.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class Cell
    {
        public TwAggregate Aggregate;
        public string? Format;
        public string? Text;
        public Func<IReadOnlyList<object?>, string?>? Compute;
    }
}
