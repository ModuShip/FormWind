using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

namespace FormWind;

public static partial class TwGrid
{
    // .NET Framework'te bağlama yönlendirmesi olmayan bir süreçte System.Text.Json'ın bağımlılıkları (bkz. TwDependencyResolver).
    static TwGrid() => TwDependencyResolver.EnsureInstalled();

    /// <summary>
    /// The user's view of the grid as JSON: column order, widths and visibility, sorting, grouping, the search and (with an
    /// <see cref="ITwGridSource"/> data source) the filters. Store it in the user's settings and restore it with
    /// <see cref="LoadLayout"/>. Columns are identified by <c>Name</c>.
    /// </summary>
    public static string SaveLayout(DataGridView grid)
    {
        if (grid is null) throw new ArgumentNullException(nameof(grid));
        var layout = new GridLayout
        {
            Columns = grid.Columns.Cast<DataGridViewColumn>().Select(c => new ColumnLayout
            {
                Name = c.Name,
                DisplayIndex = c.DisplayIndex,
                Width = c.Width,
                FillWeight = c.FillWeight,
                Visible = c.Visible,
            }).ToList(),
            SortColumn = grid.SortedColumn?.Name,
            SortDescending = grid.SortOrder == SortOrder.Descending,
            GroupBy = GetGroupBy(grid)?.Name,
            Search = GetSearchText(grid),
        };
        if (FilterSource(grid) is ITwFilterableList source)
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                var property = column.DataPropertyName;
                if (string.IsNullOrEmpty(property)) continue;
                if (source.GetColumnFilter(property) is IReadOnlyCollection<string> allowed)
                    (layout.Filters ??= new Dictionary<string, List<string>>())[property] = allowed.OrderBy(v => v, StringComparer.Ordinal).ToList();
                if (source is ITwGridSource ranged && ranged.TryGetRangeFilter(property, out var min, out var max))
                    (layout.Ranges ??= new Dictionary<string, string?[]>())[property] = new[] { Invariant(min), Invariant(max) };
            }
        }
        return JsonSerializer.Serialize(layout);
    }

    /// <summary>
    /// Restores a view saved with <see cref="SaveLayout"/>. Columns missing from the grid and invalid values are skipped,
    /// so a layout saved by an older version of the form still loads.
    /// </summary>
    public static void LoadLayout(DataGridView grid, string layout)
    {
        if (grid is null) throw new ArgumentNullException(nameof(grid));
        if (string.IsNullOrWhiteSpace(layout)) return;
        GridLayout? saved;
        try
        {
            saved = JsonSerializer.Deserialize<GridLayout>(layout);
        }
        catch (JsonException)
        {
            return;
        }
        if (saved is null) return;

        var columns = (saved.Columns ?? new List<ColumnLayout>())
            .Where(c => c.Name != null && grid.Columns.Contains(c.Name))
            .ToList();
        foreach (var item in columns.OrderBy(c => c.DisplayIndex))
        {
            var column = grid.Columns[item.Name!]!;
            if (item.DisplayIndex >= 0 && item.DisplayIndex < grid.Columns.Count) column.DisplayIndex = item.DisplayIndex;
        }
        foreach (var item in columns)
        {
            var column = grid.Columns[item.Name!]!;
            if (column.InheritedAutoSizeMode == DataGridViewAutoSizeColumnMode.Fill)
            {
                if (item.FillWeight > 0) column.FillWeight = item.FillWeight;
            }
            else if (item.Width > 0 && column.InheritedAutoSizeMode == DataGridViewAutoSizeColumnMode.None)
            {
                column.Width = Math.Max(column.MinimumWidth, item.Width);
            }
        }
        // Önce gösterilenler: son görünen kolon gizlenmeye çalışılmasın.
        foreach (var item in columns.OrderByDescending(c => c.Visible)) grid.Columns[item.Name!]!.Visible = item.Visible;

        if (FilterSource(grid) is ITwFilterableList source)
        {
            if (saved.Filters != null)
                foreach (var filter in saved.Filters)
                    TrySet(() => source.SetColumnFilter(filter.Key, filter.Value));
            if (source is ITwGridSource ranged && saved.Ranges != null)
            {
                foreach (var range in saved.Ranges)
                {
                    var type = ranged.GetPropertyType(range.Key);
                    if (type is null || range.Value is null || range.Value.Length != 2) continue;
                    TrySet(() => ranged.SetRangeFilter(range.Key, FromInvariant(range.Value[0], type), FromInvariant(range.Value[1], type)));
                }
            }
        }

        if (saved.SortColumn != null && grid.Columns.Contains(saved.SortColumn))
        {
            var direction = saved.SortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending;
            TrySet(() => grid.Sort(grid.Columns[saved.SortColumn]!, direction));
        }
        SetGroupBy(grid, saved.GroupBy != null && grid.Columns.Contains(saved.GroupBy) ? grid.Columns[saved.GroupBy] : null);
        if (saved.Search != null || GetSearchText(grid) != null) SetSearchText(grid, saved.Search);
    }

    /// <summary>
    /// The grid's visible columns (in display order) and rows as delimited text: headers on the first line, values formatted
    /// with each column's format. Fields containing the separator, quotes or line breaks are quoted. With
    /// <paramref name="selectedOnly"/>, only the rows and columns of the selected cells are written. Button, image, expander
    /// and actions columns are skipped.
    /// </summary>
    public static string ToDelimitedText(DataGridView grid, char separator = '\t', bool selectedOnly = false, bool includeHeaders = true)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        WriteDelimited(grid, writer, separator, selectedOnly, includeHeaders);
        return writer.ToString();
    }

    /// <summary>
    /// Writes the grid as CSV (<see cref="ToDelimitedText"/>) in UTF-8 with a byte order mark, so Excel opens it with the
    /// right characters. The separator defaults to the current culture's list separator (<c>;</c> in Turkish, <c>,</c> in English).
    /// </summary>
    public static void ExportCsv(DataGridView grid, string path, char? separator = null)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        using var writer = new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        WriteDelimited(grid, writer, separator ?? ListSeparator, selectedOnly: false, includeHeaders: true);
    }

    /// <summary>Copies the selected cells (or every row when nothing is selected) to the clipboard as tab-separated text with headers.</summary>
    public static void CopyToClipboard(DataGridView grid, bool selectedOnly = true)
    {
        if (grid is null) throw new ArgumentNullException(nameof(grid));
        var text = ToDelimitedText(grid, '\t', selectedOnly && grid.SelectedCells.Count > 0);
        if (text.Length > 0) Clipboard.SetText(text);
    }

    private static char ListSeparator
    {
        get
        {
            var separator = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
            return string.IsNullOrEmpty(separator) ? ',' : separator[0];
        }
    }

    private static void WriteDelimited(DataGridView grid, TextWriter writer, char separator, bool selectedOnly, bool includeHeaders)
    {
        if (grid is null) throw new ArgumentNullException(nameof(grid));
        var columns = grid.Columns.Cast<DataGridViewColumn>()
            .Where(c => c.Visible && Exportable(c))
            .OrderBy(c => c.DisplayIndex)
            .ToList();
        var rows = Enumerable.Range(0, DataRowCount(grid))
            .Where(i => (grid.Rows.GetRowState(i) & DataGridViewElementStates.Visible) != 0);

        HashSet<(int Row, int Column)>? selected = null;
        if (selectedOnly)
        {
            selected = new HashSet<(int, int)>(grid.SelectedCells.Cast<DataGridViewCell>().Select(c => (c.RowIndex, c.ColumnIndex)));
            var selectedColumns = new HashSet<int>(selected.Select(s => s.Column));
            var selectedRows = new HashSet<int>(selected.Select(s => s.Row));
            columns = columns.Where(c => selectedColumns.Contains(c.Index)).ToList();
            rows = rows.Where(selectedRows.Contains);
        }
        if (columns.Count == 0) return;

        var line = new StringBuilder();
        void Flush()
        {
            writer.Write(line.ToString());
            writer.Write("\r\n");
            line.Clear();
        }
        void Field(string value, bool first)
        {
            if (!first) line.Append(separator);
            bool quote = value.IndexOf(separator) >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0
                || (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1])));
            line.Append(quote ? "\"" + value.Replace("\"", "\"\"") + "\"" : value);
        }

        if (includeHeaders)
        {
            for (int c = 0; c < columns.Count; c++)
                Field(string.IsNullOrEmpty(columns[c].HeaderText) ? columns[c].Name : columns[c].HeaderText, c == 0);
            Flush();
        }
        var readers = columns.Select(c => TwGridData.Reader(grid, c)).ToList();
        bool bound = TwGridData.List(grid) != null;
        foreach (int row in rows)
        {
            for (int c = 0; c < columns.Count; c++)
            {
                string text = "";
                if (selected is null || selected.Contains((row, columns[c].Index)))
                {
                    // Bağlı olmayan grid'in hücre metni (CellFormatting dahil); bağlı grid'de satırlar paylaşımdan çıkarılmaz.
                    text = !bound && grid.Rows[row].Cells[columns[c].Index].FormattedValue is string formatted
                        ? formatted
                        : TwGridData.Format(readers[c](row), columns[c]);
                }
                Field(text, c == 0);
            }
            Flush();
        }
    }

    private static bool Exportable(DataGridViewColumn column) =>
        !(column is DataGridViewButtonColumn || column is DataGridViewImageColumn || column is TwExpanderColumn || column is TwActionsColumn);

    private static ITwFilterableList? FilterSource(DataGridView grid) => grid.DataSource switch
    {
        ITwFilterableList list => list,
        BindingSource { List: ITwFilterableList list } => list,
        _ => null,
    };

    private static string? Invariant(object? value) => value switch
    {
        null => null,
        DateTime date => date.ToString("o", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("o", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    private static object? FromInvariant(string? text, Type type)
    {
        if (text is null) return null;
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(DateTime)) return DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (type == typeof(DateTimeOffset)) return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return TypeDescriptor.GetConverter(type).ConvertFromInvariantString(text);
    }

    // Kaydedilmiş düzende artık geçersiz olan bir parça (silinmiş özellik, sıralanamayan kolon) yüklemeyi durdurmaz.
    private static void TrySet(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (!TwErrors.IsCritical(ex))
        {
        }
    }

    private sealed class GridLayout
    {
        public List<ColumnLayout>? Columns { get; set; }
        public string? SortColumn { get; set; }
        public bool SortDescending { get; set; }
        public string? GroupBy { get; set; }
        public string? Search { get; set; }
        public Dictionary<string, List<string>>? Filters { get; set; }
        public Dictionary<string, string?[]>? Ranges { get; set; }
    }

    private sealed class ColumnLayout
    {
        public string? Name { get; set; }
        public int DisplayIndex { get; set; }
        public int Width { get; set; }
        public float FillWeight { get; set; }
        public bool Visible { get; set; } = true;
    }
}
