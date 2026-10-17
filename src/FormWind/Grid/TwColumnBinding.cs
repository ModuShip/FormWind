using System;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Classes of a DataGridView column. Base classes apply to the column's cells (<c>text-right font-semibold px-3</c>),
/// <c>header:</c> to the header cell; <c>w-32</c>, <c>min-w-20</c>, <c>flex-1</c>/<c>grow</c> (Fill weight) and
/// <c>hidden</c> affect the column itself.
/// </summary>
internal sealed class TwColumnBinding
{
    private readonly TailwindProvider _owner;
    private readonly DataGridViewColumn _column;
    private DataGridViewCellStyle? _originalCells;
    private DataGridViewCellStyle? _originalHeader;
    private TwParsed _parsed = TwParsed.Empty;

    public TwColumnBinding(TailwindProvider owner, DataGridViewColumn column)
    {
        _owner = owner;
        _column = column;
        _column.Disposed += (_, _) => _owner.RemoveColumn(_column);
    }

    public string Classes { get; private set; } = "";

    public void SetClasses(string classes, TwTheme theme)
    {
        Classes = classes;
        Reparse(theme);
    }

    public void Reparse(TwTheme theme) => _parsed = theme.Parse(Classes);

    public void Refresh()
    {
        if (!_owner.ShouldApply) return;
        try
        {
            RefreshCore();
        }
        catch (System.Exception ex) when (!TwErrors.IsCritical(ex))
        {
            _owner.ReportError(_column, Classes, ex);
        }
    }

    private void RefreshCore()
    {

        _originalCells ??= new DataGridViewCellStyle(_column.DefaultCellStyle);
        _originalHeader ??= new DataGridViewCellStyle(_column.HeaderCell.Style);

        var grid = _column.DataGridView;
        var state = _owner.BaseState;
        var scale = _owner.ScaleWithDpi ? (grid is null ? 96 : TwDpi.DeviceDpi(grid)) / 96f : 1f;
        var cells = _parsed.Resolve(state);
        var header = _parsed.ResolvePart("header", state);
        // Kolon fontu, grid'in hücre ve başlık stillerinin fontundan türetilir (grid.Font'tan değil).
        var gridFont = grid?.Font ?? Control.DefaultFont;
        var cellFont = grid?.DefaultCellStyle.Font ?? gridFont;
        var headerFont = grid?.ColumnHeadersDefaultCellStyle.Font ?? gridFont;

        var cellStyle = Build(_originalCells, cells, cellFont, scale);
        if (!_column.DefaultCellStyle.Equals(cellStyle)) _column.DefaultCellStyle = cellStyle;

        var headerStyle = Build(_originalHeader, header, headerFont, scale);
        if (!_column.HeaderCell.Style.Equals(headerStyle)) _column.HeaderCell.Style = headerStyle;

        if (cells.Width is { IsPixels: true } width)
        {
            _column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _column.Width = Math.Max(_column.MinimumWidth, (int)Math.Round(width.Value * scale));
        }
        else if (cells.FlexGrow is float grow && grow > 0)
        {
            _column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _column.FillWeight = grow * 100;
        }

        if (cells.MinWidth is int minWidth) _column.MinimumWidth = Math.Max(2, (int)Math.Round(minWidth * scale));
        if (cells.Hidden is bool hidden && !_owner.IsDesignMode) _column.Visible = !hidden;
    }

    private DataGridViewCellStyle Build(DataGridViewCellStyle original, TwStyle style, Font gridFont, float scale)
    {
        var result = new DataGridViewCellStyle(original);
        if (style.BackColor is Color back) result.BackColor = back;
        if (style.ForeColor is Color fore) result.ForeColor = fore;
        if (!style.Padding.IsEmpty) result.Padding = style.Padding.ApplyTo(original.Padding, scale);
        if (style.HasFont) result.Font = TwFonts.Create(style, original.Font ?? gridFont, _owner.Theme, _column.DataGridView is DataGridView grid ? TwDpi.FontScale(grid) : 1f);
        if (style.NoWrap is bool noWrap) result.WrapMode = noWrap ? DataGridViewTriState.False : DataGridViewTriState.True;
        if (style.TextAlign is TwTextAlign align)
        {
            result.Alignment = align switch
            {
                TwTextAlign.Center => DataGridViewContentAlignment.MiddleCenter,
                TwTextAlign.Right => DataGridViewContentAlignment.MiddleRight,
                _ => DataGridViewContentAlignment.MiddleLeft,
            };
        }
        return result;
    }
}

/// <summary>
/// Conditional style per row: the rule returns classes for each row (e.g. inactive customers faded, cancelled orders red).
/// Background, text color and font are applied to all cells of the row during formatting.
/// </summary>
internal sealed class TwRowRule
{
    private readonly TailwindProvider _owner;
    private readonly DataGridView _grid;
    private readonly Func<DataGridViewRow, string?> _rule;

    public TwRowRule(TailwindProvider owner, DataGridView grid, Func<DataGridViewRow, string?> rule)
    {
        _owner = owner;
        _grid = grid;
        _rule = rule;
        _grid.CellFormatting += OnCellFormatting;
        _grid.Invalidate();
    }

    public void Detach()
    {
        _grid.CellFormatting -= OnCellFormatting;
        _grid.Invalidate();
    }

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count || e.CellStyle is null) return;
        string? classes = null;
        try
        {
            // Kural uygulamanın kodudur; istisnası hücre çizimini (ve grid'i) düşürmemeli.
            classes = _rule(_grid.Rows[e.RowIndex]);
        }
        catch (System.Exception ex) when (!TwErrors.IsCritical(ex))
        {
            _owner.ReportError(_grid, "SetRowClasses", ex);
            return;
        }
        if (string.IsNullOrWhiteSpace(classes)) return;

        var style = _owner.Theme.Parse(classes).Resolve(_owner.BaseState);
        if (style.BackColor is Color back) e.CellStyle.BackColor = back;
        if (style.ForeColor is Color fore) e.CellStyle.ForeColor = fore;
        if (style.HasFont) e.CellStyle.Font = TwFonts.Create(style, e.CellStyle.Font ?? _grid.Font, _owner.Theme, TwDpi.FontScale(_grid));
    }
}
