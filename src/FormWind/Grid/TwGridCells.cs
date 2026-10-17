using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Small chart in a cell. The value is a sequence of numbers (<c>IEnumerable</c> of numbers, or text separated by <c>;</c>
/// or spaces). Lines and areas end with a dot on the last value; bars below zero use <see cref="NegativeColor"/>.
/// </summary>
[ToolboxItem(false)]
public class TwSparklineColumn : DataGridViewColumn
{
    public TwSparklineColumn() : base(new TwSparklineCell())
    {
        ReadOnly = true;
    }

    [Category("FormWind"), DefaultValue(TwSparklineKind.Line)]
    public TwSparklineKind Kind { get; set; } = TwSparklineKind.Line;

    [Category("FormWind")]
    public Color LineColor { get; set; } = ColorTranslator.FromHtml("#2563eb");

    private bool ShouldSerializeLineColor() => LineColor.ToArgb() != ColorTranslator.FromHtml("#2563eb").ToArgb();

    [Category("FormWind")]
    public Color DarkLineColor { get; set; } = ColorTranslator.FromHtml("#60a5fa");

    private bool ShouldSerializeDarkLineColor() => DarkLineColor.ToArgb() != ColorTranslator.FromHtml("#60a5fa").ToArgb();

    [Category("FormWind"), Description("Bars below zero.")]
    public Color NegativeColor { get; set; } = ColorTranslator.FromHtml("#dc2626");

    private bool ShouldSerializeNegativeColor() => NegativeColor.ToArgb() != ColorTranslator.FromHtml("#dc2626").ToArgb();

    public override object Clone()
    {
        var clone = (TwSparklineColumn)base.Clone();
        clone.Kind = Kind;
        clone.LineColor = LineColor;
        clone.DarkLineColor = DarkLineColor;
        clone.NegativeColor = NegativeColor;
        return clone;
    }
}

public class TwSparklineCell : DataGridViewTextBoxCell
{
    public override Type ValueType => typeof(object);

    /// <summary>This row's line color (a worsening trend in red, say); empty = the column's <see cref="TwSparklineColumn.LineColor"/>
    /// (or its dark-mode color).</summary>
    public Color LineColor { get; set; } = Color.Empty;

    public override object Clone()
    {
        var clone = (TwSparklineCell)base.Clone();
        clone.LineColor = LineColor;
        return clone;
    }

    /// <summary>The numbers of a sparkline value.</summary>
    internal static List<double> Numbers(object? value)
    {
        var result = new List<double>();
        IEnumerable? items = value switch
        {
            null => null,
            string text => text.Split(new[] { ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries),
            IEnumerable sequence => sequence,
            _ => null,
        };
        if (items is null) return result;
        foreach (var item in items)
        {
            switch (item)
            {
                case string text when double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed):
                    result.Add(parsed);
                    break;
                case IConvertible convertible when !(item is string) && !(item is bool) && !(item is DateTime):
                    result.Add(convertible.ToDouble(CultureInfo.InvariantCulture));
                    break;
            }
        }
        return result;
    }

    // Erişilebilirlik ve kopyalama için değerler metin olarak verilir. Metin base üzerinden biçimlenir ki CellFormatting
    // (satır kuralları, satır hover'ı, grup başlığının boşluğu) bu hücrelerde de çalışsın; diğer özel hücreler de böyle.
    protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
        TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context) =>
        base.GetFormattedValue(string.Join("; ", Numbers(value).Select(n => n.ToString(CultureInfo.CurrentCulture))), rowIndex, ref cellStyle, null, null, context);

    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, TwCellPainting.WithoutContent(paintParts));
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0) return;

        var numbers = Numbers(value);
        if (numbers.Count == 0) return;
        var column = OwningColumn as TwSparklineColumn;
        var scale = TwCellPainting.Scale(DataGridView);
        var content = TwCellPainting.Content(cellBounds, cellStyle);
        int inset = (int)Math.Round(4 * scale);
        var area = Rectangle.Inflate(content, -inset / 2, -inset);
        if (area.Width < 4 || area.Height < 4) return;

        bool dark = TwGridState.IsDark(DataGridView);
        var color = !LineColor.IsEmpty ? LineColor : dark ? column?.DarkLineColor ?? Color.CornflowerBlue : column?.LineColor ?? Color.RoyalBlue;
        if (TwCellPainting.Selected(cellState) && cellStyle.SelectionForeColor.GetBrightness() > 0.8f && !dark) color = cellStyle.SelectionForeColor;
        var surface = TwCellPainting.Selected(cellState) ? cellStyle.SelectionBackColor : cellStyle.BackColor;
        TwCharts.DrawSparkline(graphics, area, numbers, column?.Kind ?? TwSparklineKind.Line, color,
            column?.NegativeColor ?? Color.IndianRed, surface, scale);
    }
}

/// <summary>
/// Date column. The value is a <see cref="DateTime"/> or <see cref="DateTimeOffset"/>, shown with the column's format
/// (default <c>g</c>); with <see cref="Relative"/>, values in the past week are shown as "5 min ago", "3 h ago"
/// (<see cref="TwTexts.MinutesAgo"/> …).
/// </summary>
[ToolboxItem(false)]
public class TwDateColumn : DataGridViewTextBoxColumn
{
    public TwDateColumn()
    {
        CellTemplate = new TwDateCell();
        DefaultCellStyle.Format = "g";
    }

    [Category("FormWind"), DefaultValue(false), Description("Recent dates as \"5 min ago\".")]
    public bool Relative { get; set; }

    public override object Clone()
    {
        var clone = (TwDateColumn)base.Clone();
        clone.Relative = Relative;
        return clone;
    }
}

public class TwDateCell : DataGridViewTextBoxCell
{
    /// <summary>The clock relative dates are measured against (replaced in the tests).</summary>
    internal static Func<DateTime> Now = () => DateTime.Now;

    internal static string? RelativeText(DateTime value, DateTime now)
    {
        var age = now - value;
        if (age < TimeSpan.Zero || age >= TimeSpan.FromDays(7)) return null;
        var texts = TwTexts.Current;
        if (age < TimeSpan.FromMinutes(1)) return texts.JustNow;
        if (age < TimeSpan.FromHours(1)) return string.Format(CultureInfo.CurrentCulture, texts.MinutesAgo, (int)age.TotalMinutes);
        if (age < TimeSpan.FromDays(1)) return string.Format(CultureInfo.CurrentCulture, texts.HoursAgo, (int)age.TotalHours);
        return string.Format(CultureInfo.CurrentCulture, texts.DaysAgo, (int)age.TotalDays);
    }

    protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
        TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context)
    {
        if (OwningColumn is TwDateColumn { Relative: true } && Local(value) is DateTime local && RelativeText(local, Now()) is string relative)
            return base.GetFormattedValue(relative, rowIndex, ref cellStyle, null, null, context);
        return base.GetFormattedValue(value, rowIndex, ref cellStyle, valueTypeConverter, formattedValueTypeConverter, context);
    }

    private static DateTime? Local(object? value) => value switch
    {
        DateTime d => d.Kind == DateTimeKind.Utc ? d.ToLocalTime() : d,
        DateTimeOffset o => o.LocalDateTime,
        _ => null,
    };

    // Düzenlenirken göreli metin değil tarihin kendisi gösterilir (geri ayrıştırılabilsin).
#pragma warning disable CS8765 // .NET 8'de nullable parametreli.
    public override void InitializeEditingControl(int rowIndex, object initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
    {
        if (OwningColumn is TwDateColumn { Relative: true } && Local(GetValue(rowIndex)) is DateTime local)
            initialFormattedValue = local.ToString(dataGridViewCellStyle.Format, dataGridViewCellStyle.FormatProvider ?? CultureInfo.CurrentCulture);
        base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
    }
#pragma warning restore CS8765
}

/// <summary>
/// Number column: right-aligned, formatted with the column's format (default <c>N2</c>; <c>C2</c> for currency) and
/// <see cref="Culture"/>. Negative values take <see cref="NegativeClasses"/> (red by default), positive ones
/// <see cref="PositiveClasses"/>.
/// </summary>
[ToolboxItem(false)]
public class TwNumberColumn : DataGridViewTextBoxColumn
{
    internal const string DefaultNegative = "text-red-600 dark:text-red-400";
    private string _culture = "";

    public TwNumberColumn()
    {
        CellTemplate = new TwNumberCell();
        DefaultCellStyle.Format = "N2";
        DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
    }

    /// <summary>Culture of the format, e.g. <c>tr-TR</c> or <c>en-US</c> for the currency symbol (empty = current culture).</summary>
    [Category("FormWind"), DefaultValue("")]
    public string Culture
    {
        get => _culture;
        set
        {
            _culture = value ?? "";
            DefaultCellStyle.FormatProvider = _culture.Length == 0 ? null : CultureInfo.GetCultureInfo(_culture);
            DataGridView?.InvalidateColumn(Index);
        }
    }

    [Category("FormWind"), DefaultValue(DefaultNegative)]
    public string NegativeClasses { get; set; } = DefaultNegative;

    [Category("FormWind"), DefaultValue("")]
    public string PositiveClasses { get; set; } = "";

    public override object Clone()
    {
        var clone = (TwNumberColumn)base.Clone();
        clone.Culture = Culture;
        clone.NegativeClasses = NegativeClasses;
        clone.PositiveClasses = PositiveClasses;
        return clone;
    }
}

public class TwNumberCell : DataGridViewTextBoxCell
{
    protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
        TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context)
    {
        if (OwningColumn is TwNumberColumn column && value is IConvertible convertible && !(value is string) && !(value is bool) && !(value is DateTime))
        {
            double number;
            try
            {
                number = convertible.ToDouble(CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is OverflowException || ex is InvalidCastException)
            {
                number = 0;
            }
            var classes = number < 0 ? column.NegativeClasses : number > 0 ? column.PositiveClasses : "";
            if (!string.IsNullOrWhiteSpace(classes))
            {
                var style = TwTheme.Current.Parse(classes).Resolve(TwGridState.State(DataGridView));
                if (style.ForeColor is Color fore || style.HasFont)
                {
                    cellStyle = new DataGridViewCellStyle(cellStyle);
                    if (style.ForeColor is Color color) cellStyle.ForeColor = color;
                    if (style.HasFont && DataGridView != null)
                        cellStyle.Font = TwFonts.Create(style, cellStyle.Font ?? DataGridView.Font, TwTheme.Current, TwDpi.FontScale(DataGridView));
                }
            }
        }
        return base.GetFormattedValue(value, rowIndex, ref cellStyle, valueTypeConverter, formattedValueTypeConverter, context);
    }
}

/// <summary>
/// Link column in the theme's colors: <see cref="DataGridViewLinkColumn.LinkColor"/> (dark mode: <see cref="DarkLinkColor"/>),
/// underlined under the mouse. Clicks are handled with the <c>CellContentClick</c> event.
/// </summary>
[ToolboxItem(false)]
public class TwLinkColumn : DataGridViewLinkColumn
{
    public TwLinkColumn()
    {
        CellTemplate = new TwLinkCell();
        LinkColor = ColorTranslator.FromHtml("#2563eb");
        ActiveLinkColor = ColorTranslator.FromHtml("#1d4ed8");
        VisitedLinkColor = ColorTranslator.FromHtml("#2563eb");
        LinkBehavior = LinkBehavior.HoverUnderline;
        TrackVisitedState = false;
    }

    [Category("FormWind")]
    public Color DarkLinkColor { get; set; } = ColorTranslator.FromHtml("#60a5fa");

    private bool ShouldSerializeDarkLinkColor() => DarkLinkColor.ToArgb() != ColorTranslator.FromHtml("#60a5fa").ToArgb();

    /// <summary>The row under the mouse (-1 = none).</summary>
    internal int HoverRow { get; set; } = -1;

    public override object Clone()
    {
        var clone = (TwLinkColumn)base.Clone();
        clone.DarkLinkColor = DarkLinkColor;
        return clone;
    }
}

public class TwLinkCell : DataGridViewLinkCell
{
    protected override void OnMouseEnter(int rowIndex)
    {
        base.OnMouseEnter(rowIndex);
        if (OwningColumn is TwLinkColumn column)
        {
            column.HoverRow = rowIndex;
            DataGridView?.InvalidateCell(ColumnIndex, rowIndex);
        }
    }

    protected override void OnMouseLeave(int rowIndex)
    {
        base.OnMouseLeave(rowIndex);
        if (OwningColumn is TwLinkColumn column && column.HoverRow == rowIndex)
        {
            column.HoverRow = -1;
            DataGridView?.InvalidateCell(ColumnIndex, rowIndex);
        }
    }

#pragma warning disable CS8765 // DataGridViewLinkCell.Paint .NET 8'de nullable parametreli.
    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, paintParts & (DataGridViewPaintParts.Background | DataGridViewPaintParts.Border | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.ErrorIcon));
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0) return;
        var column = OwningColumn as TwLinkColumn;
        var text = column?.UseColumnTextForLinkValue == true ? column.Text : formattedValue as string;
        if (string.IsNullOrEmpty(text)) return;

        bool dark = TwGridState.IsDark(DataGridView);
        var color = dark ? column?.DarkLinkColor ?? Color.LightSkyBlue : LinkColor;
        var font = cellStyle.Font ?? DataGridView!.Font;
        bool hover = column?.HoverRow == rowIndex;
        using var underline = hover ? new Font(font, font.Style | FontStyle.Underline) : null;
        var content = TwCellPainting.Content(cellBounds, cellStyle);
        int align = (int)cellStyle.Alignment;
        var flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding
            | ((align & 0x222) != 0 ? TextFormatFlags.HorizontalCenter : (align & 0x444) != 0 ? TextFormatFlags.Right : TextFormatFlags.Left)
            | ((align & 0x7) != 0 ? TextFormatFlags.Top : (align & 0x700) != 0 ? TextFormatFlags.Bottom : TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(graphics, text, underline ?? font, content, color, flags);
    }
#pragma warning restore CS8765
}

/// <summary>
/// Tags: several badges in a cell. The value is a sequence of texts or one text split at <see cref="Separator"/>
/// (<c>"vip, new"</c>). Colors come from <see cref="TwBadgeColumn.Variants"/>; tags that do not fit are counted as <c>+N</c>.
/// </summary>
[ToolboxItem(false)]
public class TwTagsColumn : TwBadgeColumn
{
    public TwTagsColumn()
    {
        CellTemplate = new TwTagsCell();
    }

    [Category("FormWind"), DefaultValue(",")]
    public string Separator { get; set; } = ",";

    public override object Clone()
    {
        var clone = (TwTagsColumn)base.Clone();
        clone.Separator = Separator;
        return clone;
    }
}

public class TwTagsCell : DataGridViewTextBoxCell
{
    public override Type ValueType => typeof(object);

    internal List<string> Tags(object? value)
    {
        var separator = (OwningColumn as TwTagsColumn)?.Separator;
        IEnumerable<string> tags = value switch
        {
            null => Array.Empty<string>(),
            string text => string.IsNullOrEmpty(separator) ? new[] { text } : text.Split(new[] { separator }, StringSplitOptions.None),
            IEnumerable items => items.Cast<object?>().Select(i => Convert.ToString(i, CultureInfo.CurrentCulture) ?? ""),
            _ => new[] { Convert.ToString(value, CultureInfo.CurrentCulture) ?? "" },
        };
        return tags.Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
    }

    protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
        TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context) =>
        base.GetFormattedValue(string.Join(", ", Tags(value)), rowIndex, ref cellStyle, null, null, context);

    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, TwCellPainting.WithoutContent(paintParts));
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0 || !(OwningColumn is TwTagsColumn column)) return;
        var tags = Tags(value);
        if (tags.Count == 0) return;

        var scale = TwCellPainting.Scale(DataGridView);
        var content = TwCellPainting.Content(cellBounds, cellStyle);
        int gap = (int)Math.Round(4 * scale);
        int x = content.X;
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        for (int i = 0; i < tags.Count; i++)
        {
            var style = column.StyleFor(tags[i], DataGridView);
            var font = TwFonts.Create(style, cellStyle.Font ?? DataGridView!.Font, TwTheme.Current, TwDpi.FontScale(DataGridView!));
            var padding = style.Padding.ApplyTo(Padding.Empty, scale);
            var size = TextRenderer.MeasureText(graphics, tags[i], font, Size.Empty, TextFormatFlags.NoPadding);
            int width = size.Width + padding.Horizontal;
            int height = size.Height + padding.Vertical;

            // Sığmayanlar "+N" olarak sayılır (son etiket için yer kalmazsa onun yerine).
            var more = "+" + (tags.Count - i).ToString(CultureInfo.CurrentCulture);
            int moreWidth = i < tags.Count - 1 ? TextRenderer.MeasureText(graphics, "+" + (tags.Count - i - 1), font, Size.Empty, TextFormatFlags.NoPadding).Width + padding.Horizontal + gap : 0;
            if (x + width + moreWidth > content.Right && i > 0)
            {
                var moreSize = TextRenderer.MeasureText(graphics, more, font, Size.Empty, TextFormatFlags.NoPadding);
                var pillMore = new Rectangle(x, content.Y + (content.Height - height) / 2, moreSize.Width + padding.Horizontal, height);
                var muted = column.StyleFor("\u0000", DataGridView);
                TwPainter.FillRounded(graphics, pillMore, (muted.Radius ?? 9999) * scale, muted.BackColor ?? Color.Transparent);
                TextRenderer.DrawText(graphics, more, font, pillMore, muted.ForeColor ?? cellStyle.ForeColor, flags);
                return;
            }
            var pill = new Rectangle(x, content.Y + (content.Height - height) / 2, Math.Min(width, Math.Max(0, content.Right - x)), height);
            TwPainter.FillRounded(graphics, pill, (style.Radius ?? 9999) * scale, style.BackColor ?? Color.Transparent);
            TextRenderer.DrawText(graphics, tags[i], font, pill, style.ForeColor ?? cellStyle.ForeColor, flags | TextFormatFlags.EndEllipsis);
            x += width + gap;
        }
    }
}

/// <summary>
/// Expander column for row details (<see cref="TwGrid.SetRowDetails"/>): a chevron that expands and collapses the row
/// (click or Space).
/// </summary>
[ToolboxItem(false)]
public class TwExpanderColumn : DataGridViewColumn
{
    public TwExpanderColumn() : base(new TwExpanderCell())
    {
        Width = 32;
        ReadOnly = true;
        Resizable = DataGridViewTriState.False;
        SortMode = DataGridViewColumnSortMode.NotSortable;
        HeaderText = "";
    }
}

public class TwExpanderCell : DataGridViewTextBoxCell
{
    public override Type ValueType => typeof(object);

    protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
        TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context) =>
        base.GetFormattedValue(DataGridView != null && rowIndex >= 0 && TwGrid.IsRowExpanded(DataGridView, rowIndex) ? TwTexts.Current.Collapse : TwTexts.Current.Expand, rowIndex, ref cellStyle, null, null, context);

    protected override void OnMouseClick(DataGridViewCellMouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left) Toggle(e.RowIndex);
    }

    protected override void OnKeyDown(KeyEventArgs e, int rowIndex)
    {
        base.OnKeyDown(e, rowIndex);
        if (e.KeyCode != Keys.Space || e.Modifiers != Keys.None) return;
        Toggle(rowIndex);
        e.Handled = true;
    }

    private void Toggle(int rowIndex)
    {
        if (DataGridView is DataGridView grid && rowIndex >= 0 && rowIndex < TwGrid.DataRowCount(grid))
            TwGrid.ToggleRowDetails(grid, rowIndex);
    }

    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, TwCellPainting.WithoutContent(paintParts));
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0 || DataGridView is null || rowIndex >= TwGrid.DataRowCount(DataGridView)) return;
        var font = cellStyle.Font ?? DataGridView.Font;
        int em = TwIconRenderer.EmPixels(font.SizeInPoints, TwDpi.DeviceDpi(DataGridView));
        var box = TwCellPainting.Align(TwCellPainting.Content(cellBounds, cellStyle), new Size(em, em), DataGridViewContentAlignment.MiddleCenter);
        var color = TwCellPainting.Selected(cellState) ? cellStyle.SelectionForeColor : cellStyle.ForeColor;
        TwIconRenderer.DrawIcon(graphics, TwGrid.IsRowExpanded(DataGridView, rowIndex) ? "chevron-down" : "chevron-right", em, box, color);
    }
}

/// <summary>Event data of <see cref="TwActionsColumn.ActionClicked"/>.</summary>
public sealed class TwGridActionEventArgs : EventArgs
{
    public TwGridActionEventArgs(int rowIndex, string action)
    {
        RowIndex = rowIndex;
        Action = action;
    }

    public int RowIndex { get; }

    /// <summary>The text of the chosen item.</summary>
    public string Action { get; }
}

/// <summary>Event data of <see cref="TwActionsColumn.MenuOpening"/>: the menu can be changed for the row.</summary>
public sealed class TwGridActionMenuEventArgs : EventArgs
{
    public TwGridActionMenuEventArgs(int rowIndex, ContextMenuStrip menu)
    {
        RowIndex = rowIndex;
        Menu = menu;
    }

    public int RowIndex { get; }

    public ContextMenuStrip Menu { get; }
}

/// <summary>
/// Row actions: a "⋯" button that opens a menu in the grid's colors. <see cref="Actions"/> lists the items separated by
/// <c>;</c> (<c>-</c> = separator); the chosen item raises <see cref="ActionClicked"/>. <see cref="MenuOpening"/> can
/// disable or hide items for a row.
/// </summary>
[ToolboxItem(false)]
public class TwActionsColumn : DataGridViewColumn
{
    public TwActionsColumn() : base(new TwActionsCell())
    {
        Width = 40;
        ReadOnly = true;
        Resizable = DataGridViewTriState.False;
        SortMode = DataGridViewColumnSortMode.NotSortable;
        HeaderText = "";
    }

    [Category("FormWind"), DefaultValue(""), Description("Menu items separated by ; (- = separator).")]
    public string Actions { get; set; } = "";

    [Category("FormWind"), DefaultValue("more"), Description("Icon of the button.")]
    public string Icon { get; set; } = "more";

    public event EventHandler<TwGridActionEventArgs>? ActionClicked;

    public event EventHandler<TwGridActionMenuEventArgs>? MenuOpening;

    /// <summary>The row under the mouse (-1 = none).</summary>
    internal int HoverRow { get; set; } = -1;

    /// <summary>Opens the row's menu below its button (also from code and tests).</summary>
    public ContextMenuStrip? ShowMenu(int rowIndex)
    {
        var grid = DataGridView;
        if (grid is null || rowIndex < 0 || rowIndex >= grid.RowCount) return null;
        var menu = TwGridDecorator.For(grid).CreateMenu();
        menu.ShowCheckMargin = false;
        foreach (var part in Actions.Split(';'))
        {
            var text = part.Trim();
            if (text.Length == 0) continue;
            if (text == "-")
            {
                menu.Items.Add(new ToolStripSeparator());
                continue;
            }
            var item = new ToolStripMenuItem(text);
            item.Click += (_, _) => ActionClicked?.Invoke(this, new TwGridActionEventArgs(rowIndex, text));
            menu.Items.Add(item);
        }
        MenuOpening?.Invoke(this, new TwGridActionMenuEventArgs(rowIndex, menu));
        if (menu.Items.Count == 0) return menu;
        var cell = grid.GetCellDisplayRectangle(Index, rowIndex, false);
        menu.Show(grid, new Point(cell.Left, cell.Bottom));
        return menu;
    }

    public override object Clone()
    {
        var clone = (TwActionsColumn)base.Clone();
        clone.Actions = Actions;
        clone.Icon = Icon;
        return clone;
    }
}

public class TwActionsCell : DataGridViewTextBoxCell
{
    public override Type ValueType => typeof(object);

    protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
        TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context) =>
        base.GetFormattedValue(TwTexts.Current.Actions, rowIndex, ref cellStyle, null, null, context);

    protected override void OnMouseEnter(int rowIndex)
    {
        base.OnMouseEnter(rowIndex);
        if (OwningColumn is TwActionsColumn column)
        {
            column.HoverRow = rowIndex;
            DataGridView?.InvalidateCell(ColumnIndex, rowIndex);
        }
    }

    protected override void OnMouseLeave(int rowIndex)
    {
        base.OnMouseLeave(rowIndex);
        if (OwningColumn is TwActionsColumn column && column.HoverRow == rowIndex)
        {
            column.HoverRow = -1;
            DataGridView?.InvalidateCell(ColumnIndex, rowIndex);
        }
    }

    protected override void OnMouseClick(DataGridViewCellMouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && OwningColumn is TwActionsColumn column) column.ShowMenu(e.RowIndex);
    }

    protected override void OnKeyDown(KeyEventArgs e, int rowIndex)
    {
        base.OnKeyDown(e, rowIndex);
        if ((e.KeyCode == Keys.Space || e.KeyCode == Keys.Apps) && OwningColumn is TwActionsColumn column)
        {
            column.ShowMenu(rowIndex);
            e.Handled = true;
        }
    }

    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, TwCellPainting.WithoutContent(paintParts));
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0 || DataGridView is null || !(OwningColumn is TwActionsColumn column)) return;
        if (rowIndex >= TwGrid.DataRowCount(DataGridView)) return;
        var font = cellStyle.Font ?? DataGridView.Font;
        var scale = TwCellPainting.Scale(DataGridView);
        int em = TwIconRenderer.EmPixels(font.SizeInPoints, TwDpi.DeviceDpi(DataGridView));
        int side = em + (int)Math.Round(8 * scale);
        var box = TwCellPainting.Align(TwCellPainting.Content(cellBounds, cellStyle), new Size(side, side), DataGridViewContentAlignment.MiddleCenter);
        var color = TwCellPainting.Selected(cellState) ? cellStyle.SelectionForeColor : cellStyle.ForeColor;
        if (column.HoverRow == rowIndex) TwPainter.FillRounded(graphics, box, 6 * scale, Color.FromArgb(32, color));
        TwIconRenderer.DrawIcon(graphics, column.Icon, em, box, color);
    }
}
