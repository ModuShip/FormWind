using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// A cell with a bold title and a muted second line under it, and optionally a status dot before them: the "record code and
/// what it is" cell of a table. The value is a <see cref="TwTwoLine"/>, or any value (shown as the title) whose second line and
/// dot come from the row's data object: <see cref="SubtitleMember"/>, <see cref="DotColorMember"/> or <see cref="ValueSelector"/>.
/// <see cref="MonospaceTitle"/> draws the title in the theme's monospace font (a code such as <c>PR-2041</c>). The cell reports the
/// height of its two lines as its preferred height (<c>AutoSizeRowsMode</c>); <see cref="FitRowHeight"/> sets the grid's row
/// height to it. It follows the grid's selection colors, dark mode, DPI and right-to-left layout. The same drawing is used by
/// <see cref="TwListItem"/> rows and the tree column of <see cref="TwGridTree"/> (<see cref="TwGridTreeNode.Subtitle"/>).
/// <code>
/// var col = new TwTwoLineColumn { HeaderText = "Record", DataPropertyName = "Code", SubtitleMember = "Machine", DotColorMember = "StatusColor", MonospaceTitle = true };
/// grid.Columns.Add(col);
/// col.FitRowHeight();
/// </code>
/// </summary>
[ToolboxItem(false)]
public class TwTwoLineColumn : DataGridViewColumn
{
    public TwTwoLineColumn() : base(new TwTwoLineCell())
    {
        ReadOnly = true;
    }

    /// <summary>Property of the row's data object that gives the second line (empty = none; the value may be a <see cref="TwTwoLine"/> instead).</summary>
    [Category("FormWind"), DefaultValue("")]
    [Description("Property of the row's data object that gives the muted second line.")]
    public string SubtitleMember
    {
        get => _subtitleMember;
        set
        {
            _subtitleMember = value ?? "";
            DataGridView?.InvalidateColumn(Index);
        }
    }

    /// <summary>Property of the row's data object that gives the dot's <see cref="Color"/> (or an HTML color string); empty = none.</summary>
    [Category("FormWind"), DefaultValue("")]
    [Description("Property of the row's data object that gives the status dot's color.")]
    public string DotColorMember
    {
        get => _dotColorMember;
        set
        {
            _dotColorMember = value ?? "";
            DataGridView?.InvalidateColumn(Index);
        }
    }

    /// <summary>
    /// Builds the cell from the row's data object (<c>null</c> for unbound rows); a non-null result replaces the cell value and the
    /// members above.
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, TwTwoLine?>? ValueSelector { get; set; }

    /// <summary>Draws the title in the theme's monospace font (<see cref="TwTheme.FontFamilyMono"/>).</summary>
    [Category("FormWind"), DefaultValue(false)]
    public bool MonospaceTitle
    {
        get => _monospaceTitle;
        set
        {
            _monospaceTitle = value;
            DataGridView?.InvalidateColumn(Index);
        }
    }

    private string _subtitleMember = "";
    private string _dotColorMember = "";
    private bool _monospaceTitle;

    /// <summary>The height (device pixels) a two-line cell needs in the grid's font and DPI.</summary>
    public int GetPreferredRowHeight()
    {
        var grid = DataGridView;
        if (grid is null) throw new InvalidOperationException("The column must belong to a grid.");
        float scale = TwCellPainting.Scale(grid);
        var font = InheritedStyle?.Font ?? grid.Font;
        var (title, subtitle) = TwTwoLineCell.Fonts(this, font);
        return TwTwoLinePainter.TextHeight(title, subtitle, scale) + (InheritedStyle?.Padding.Vertical ?? 0) + (int)Math.Round(10 * scale);
    }

    /// <summary>Makes the grid's rows (and <c>RowTemplate</c>) at least as tall as two lines of this column need.</summary>
    public void FitRowHeight()
    {
        var grid = DataGridView;
        if (grid is null) throw new InvalidOperationException("The column must belong to a grid.");
        int height = GetPreferredRowHeight();
        if (grid.RowTemplate.Height < height) grid.RowTemplate.Height = height;
        for (int i = 0; i < grid.RowCount; i++)
        {
            var row = grid.Rows[i];
            if (row.Height < height) row.Height = height;
        }
    }

    public override object Clone()
    {
        var clone = (TwTwoLineColumn)base.Clone();
        clone.SubtitleMember = SubtitleMember;
        clone.DotColorMember = DotColorMember;
        clone.ValueSelector = ValueSelector;
        clone.MonospaceTitle = MonospaceTitle;
        return clone;
    }
}

/// <summary>The cell of a <see cref="TwTwoLineColumn"/>.</summary>
public class TwTwoLineCell : DataGridViewTextBoxCell
{
    // Satırın veri nesnesi; DataSource'suz (ya da sanal) ızgarada yoktur. Satır paylaşımı bozulmasın diye CurrencyManager'dan okunur.
    private object? DataItem(int rowIndex)
    {
        var grid = DataGridView;
        if (grid?.DataSource is null || rowIndex < 0) return null;
        var manager = grid.BindingContext?[grid.DataSource, grid.DataMember] as CurrencyManager;
        return manager != null && rowIndex < manager.List.Count ? manager.List[rowIndex] : null;
    }

    private static object? Read(object? item, string member)
    {
        if (item is null || member.Length == 0) return null;
        return TypeDescriptor.GetProperties(item).Find(member, true)?.GetValue(item);
    }

    internal static (Font Title, Font Subtitle) Fonts(DataGridViewColumn? column, Font baseFont)
    {
        var style = new TwStyle { FontWeight = 600 };
        if (column is TwTwoLineColumn { MonospaceTitle: true }) style.FontFamily = TwTheme.Current.FontFamilyMono;
        return (TwFonts.Create(style, baseFont, TwTheme.Current), baseFont);
    }

    private bool HasSources => OwningColumn is TwTwoLineColumn { } c && (c.ValueSelector != null || c.SubtitleMember.Length > 0 || c.DotColorMember.Length > 0);

    /// <summary>
    /// What the cell shows: the value if it is a <see cref="TwTwoLine"/>, otherwise <paramref name="formatted"/> (the formatted value;
    /// the cell value when <c>null</c>) as the title with the column's members.
    /// </summary>
    internal TwTwoLine Resolve(object? value, string? formatted, int rowIndex)
    {
        if (value is TwTwoLine two) return two;
        var column = OwningColumn as TwTwoLineColumn;
        object? item = HasSources ? DataItem(rowIndex) : null;
        if (item != null && column!.ValueSelector?.Invoke(item) is TwTwoLine mapped) return mapped;
        var result = new TwTwoLine(formatted ?? Convert.ToString(value, CultureInfo.CurrentCulture));
        if (item != null && column != null)
        {
            result.Subtitle = Convert.ToString(Read(item, column.SubtitleMember), CultureInfo.CurrentCulture);
            result.DotColor = Read(item, column.DotColorMember) switch
            {
                Color color => color,
                string text when text.Length > 0 => TryColor(text),
                _ => Color.Empty,
            };
            // Biçimli değer "başlık, alt satır" okunur (GetFormattedValue); başlık için sondaki alt satır ayrılır.
            string tail = ", " + result.Subtitle;
            if (!string.IsNullOrEmpty(result.Subtitle) && result.Title.EndsWith(tail, StringComparison.Ordinal))
                result.Title = result.Title.Substring(0, result.Title.Length - tail.Length);
        }
        return result;
    }

    private static Color TryColor(string text)
    {
        try
        {
            return ColorTranslator.FromHtml(text);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is FormatException)
        {
            return Color.Empty;
        }
    }

    // Erişilebilirlik ve kopyalama için değerler metin olarak verilir (TwSparklineCell gibi); CellFormatting de çalışır.
    protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
        TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context) =>
        value is TwTwoLine two
            ? base.GetFormattedValue(two.ToString(), rowIndex, ref cellStyle, null, null, context)
            : FormatMapped(base.GetFormattedValue(value, rowIndex, ref cellStyle, valueTypeConverter, formattedValueTypeConverter, context), value, rowIndex);

    // Üyelerden ya da ValueSelector'dan gelen ikinci satır ekran okuyucuya ve kopyalamaya da gider.
    private object FormatMapped(object formatted, object value, int rowIndex) =>
        HasSources && formatted is string text ? Resolve(value, text, rowIndex).ToString() : formatted;

    protected override Size GetPreferredSize(Graphics graphics, DataGridViewCellStyle cellStyle, int rowIndex, Size constraintSize)
    {
        var grid = DataGridView;
        if (grid is null || rowIndex < 0) return new Size(-1, -1);
        float scale = TwCellPainting.Scale(grid);
        var (titleFont, subFont) = Fonts(OwningColumn, cellStyle.Font ?? grid.Font);
        var model = Resolve(GetValue(rowIndex), null, rowIndex);
        bool two = !string.IsNullOrEmpty(model.Subtitle);
        const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        int width = Math.Max(
            string.IsNullOrEmpty(model.Title) ? 0 : TextRenderer.MeasureText(graphics, model.Title, titleFont, Size.Empty, flags).Width,
            !two ? 0 : TextRenderer.MeasureText(graphics, model.Subtitle, subFont, Size.Empty, flags).Width);
        if (!model.DotColor.IsEmpty) width += TwTwoLinePainter.DotWidth(scale);
        int height = (two ? TwTwoLinePainter.TextHeight(titleFont, subFont, scale) : TwTwoLinePainter.LineHeight(titleFont)) + (int)Math.Round(10 * scale);
        return new Size(width + cellStyle.Padding.Horizontal + 4, height + cellStyle.Padding.Vertical);
    }

    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, TwCellPainting.WithoutContent(paintParts));
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0 || DataGridView is not DataGridView grid) return;

        var model = Resolve(value, value is TwTwoLine ? null : formattedValue as string, rowIndex);
        TwTwoLineCellPainting.Paint(graphics, grid, OwningColumn, cellBounds, cellState, cellStyle, model, 0);
    }
}

/// <summary>Draws the two lines in a grid cell (shared by <see cref="TwTwoLineCell"/> and the tree column of <see cref="TwGridTree"/>).</summary>
internal static class TwTwoLineCellPainting
{
    /// <summary>Muted color of the second line against the cell's background.</summary>
    public static Color Muted(Color fore, Color back, bool selected) => TwCharts.Blend(back, fore, selected ? 0.8f : 0.62f);

    // leadPadding: başta ek boşluk (aygıt pikseli), örneğin ağacın girintisi.
    public static void Paint(Graphics graphics, DataGridView grid, DataGridViewColumn? column, Rectangle cellBounds, DataGridViewElementStates cellState,
                             DataGridViewCellStyle cellStyle, TwTwoLine model, int leadPadding)
    {
        float scale = TwCellPainting.Scale(grid);
        bool rtl = grid.RightToLeft == RightToLeft.Yes;
        bool selected = TwCellPainting.Selected(cellState);
        var fore = selected ? cellStyle.SelectionForeColor : cellStyle.ForeColor;
        var back = selected ? cellStyle.SelectionBackColor : cellStyle.BackColor;
        var (titleFont, subFont) = TwTwoLineCell.Fonts(column, cellStyle.Font ?? grid.Font);

        var content = TwCellPainting.Content(cellBounds, cellStyle);
        if (leadPadding > 0) content = rtl ? Rectangle.FromLTRB(content.X, content.Y, content.Right - leadPadding, content.Bottom) : Rectangle.FromLTRB(content.X + leadPadding, content.Y, content.Right, content.Bottom);
        if (!model.DotColor.IsEmpty)
        {
            TwTwoLinePainter.DrawDot(graphics, content, rtl, model.DotColor, scale);
            int shift = TwTwoLinePainter.DotWidth(scale);
            content = rtl ? Rectangle.FromLTRB(content.X, content.Y, content.Right - shift, content.Bottom) : Rectangle.FromLTRB(content.X + shift, content.Y, content.Right, content.Bottom);
        }
        var state = graphics.Save();
        graphics.IntersectClip(cellBounds);
        TwTwoLinePainter.Paint(graphics, content, model.Title, titleFont, fore, model.Subtitle, subFont, Muted(fore, back, selected), back, rtl, scale);
        graphics.Restore(state);
    }
}
