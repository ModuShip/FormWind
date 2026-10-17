using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Reads cell values without unsharing rows: a bound grid's values come from its list (the grid's row order is the list's
/// order), an unbound grid's from its cells.
/// </summary>
internal static class TwGridData
{
    /// <summary>The bound list (<c>null</c> for unbound and VirtualMode grids).</summary>
    public static IList? List(DataGridView grid)
    {
        if (grid.DataSource is null || grid.VirtualMode) return null;
        try
        {
            return ListBindingHelper.GetList(grid.DataSource, grid.DataMember) as IList;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Returns a reader of a column's values by row index.</summary>
    public static Func<int, object?> Reader(DataGridView grid, DataGridViewColumn column)
    {
        var list = List(grid);
        if (list != null && !string.IsNullOrEmpty(column.DataPropertyName))
        {
            var property = ListBindingHelper.GetListItemProperties(grid.DataSource, grid.DataMember, null)
                .Find(column.DataPropertyName, true);
            if (property != null)
                return row => row >= 0 && row < list.Count && list[row] is object item ? property.GetValue(item) : null;
        }
        int index = column.Index;
        return row => row >= 0 && row < grid.RowCount ? grid.Rows[row].Cells[index].Value : null;
    }

    /// <summary>Identity of a row for state that must survive sorting (the bound item, or the row itself).</summary>
    public static object? RowKey(DataGridView grid, int row)
    {
        if (row < 0 || row >= grid.RowCount) return null;
        if (grid.VirtualMode) return row;
        var list = List(grid);
        if (list != null) return row < list.Count ? list[row] : null;
        return grid.Rows.SharedRow(row);
    }

    /// <summary>Display text of a value with the column's format (as the export and the footer write it).</summary>
    public static string Format(object? value, DataGridViewColumn column)
    {
        if (value is null || value == DBNull.Value) return "";
        var style = column.DataGridView is null ? column.DefaultCellStyle : column.InheritedStyle;
        var provider = style.FormatProvider ?? CultureInfo.CurrentCulture;
        return value switch
        {
            string text => text,
            IFormattable formattable => formattable.ToString(string.IsNullOrEmpty(style.Format) ? null : style.Format, provider),
            IEnumerable items => string.Join(", ", items.Cast<object?>().Select(item => Format(item, column))),
            _ => Convert.ToString(value, provider) ?? "",
        };
    }
}
