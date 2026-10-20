using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Styles a DataGridView with part prefixes:
/// <list type="bullet">
/// <item><description>base classes: background (<c>bg-*</c>), text, font, border; <c>divide-x/y</c> and <c>divide-{color}</c> grid lines</description></item>
/// <item><description><c>cell:</c> all cells (color, font, <c>px-*</c>/<c>py-*</c>, <c>text-right</c>)</description></item>
/// <item><description><c>alt:</c> alternating rows, <c>selected:</c> selected cells</description></item>
/// <item><description><c>header:</c> column headers (color, font, padding, <c>h-*</c>, <c>border-0</c>)</description></item>
/// <item><description><c>row:</c> row height (<c>row:h-10</c>) and row hover (<c>row:hover:bg-*</c>)</description></item>
/// <item><description><c>row-header:</c> row headers (<c>row-header:hidden</c>)</description></item>
/// </list>
/// Styles are rebuilt from the control's original values on every application; removing the classes restores the original.
/// </summary>
internal class DataGridViewApplier : WinFormsApplier
{
    private static readonly ConditionalWeakTable<DataGridView, GridOriginal> Originals =
        new ConditionalWeakTable<DataGridView, GridOriginal>();

    public override bool CanApply(Control control) => control is DataGridView;

    public override void Apply(Control control, TwStyle style, TwApplyContext context)
    {
        var grid = (DataGridView)control;
        // base.Apply'dan önce: grid.Font değişince DataGridView bunu varsayılan hücre stillerine de yayar.
        var original = Originals.GetValue(grid, GridOriginal.Capture);

        base.Apply(control, style, context);
        TwGridState.SetDark(grid, (context.State & TwState.Dark) != 0);
        var parsed = context.Parsed;
        var scale = context.Scale;

        // Kontrolün kendi hover/active durumu parçaları etkilemez; satır hover'ı ayrıca izlenir.
        var state = context.State & ~(TwState.Hover | TwState.Active);
        var cell = parsed.ResolvePart("cell", state);
        var alt = parsed.ResolvePart("alt", state);
        var selected = parsed.ResolvePart("selected", state);
        var header = parsed.ResolvePart("header", state);
        var row = parsed.ResolvePart("row", state);
        var rowHeader = parsed.ResolvePart("row-header", state);

        // Kontrol alanı ve kenarlık (renkli kenarlığı WinFormsApplier çizer; native kenarlık kapatılır).
        // BackgroundColor saydamlığı desteklemez (atama ArgumentException fırlatır); saydam bg-* özgün renge düşer.
        var background = style.BackColor is Color back && back.A == 255 ? back : original.BackgroundColor;
        Set(grid.BackgroundColor, background, v => grid.BackgroundColor = v);
        var borderStyle = style.BorderWidth.HasValue ? BorderStyle.None : original.BorderStyle;
        if (grid.BorderStyle != borderStyle) grid.BorderStyle = borderStyle;

        // Izgara çizgileri
        Set(grid.GridColor, style.DivideColor ?? original.GridColor, v => grid.GridColor = v);
        var cellBorder = style.DivideX.HasValue || style.DivideY.HasValue
            ? ToCellBorderStyle(style.DivideX ?? false, style.DivideY ?? false)
            : original.CellBorderStyle;
        if (grid.CellBorderStyle != cellBorder) grid.CellBorderStyle = cellBorder;

        // Hücreler: temel metin rengi, arka plan ve font hücrelere de uygulanır; cell: bunları ezer.
        var cells = new TwStyle();
        cells.BackColor = style.BackColor;
        cells.ForeColor = style.ForeColor;
        cells.MergeFrom(cell);
        var cellStyle = BuildCellStyle(original.DefaultCellStyle, cells, grid.Font, scale, context.Theme, TwDpi.FontScale(grid), TwDpi.DeviceDpi(grid) / (float)original.Dpi);
        if (selected.BackColor is Color selectionBack) cellStyle.SelectionBackColor = selectionBack;
        if (selected.ForeColor is Color selectionFore) cellStyle.SelectionForeColor = selectionFore;
        Assign(grid.DefaultCellStyle, cellStyle, v => grid.DefaultCellStyle = v);

        var altStyle = BuildCellStyle(original.AlternatingRowsDefaultCellStyle, alt, grid.Font, scale, context.Theme, TwDpi.FontScale(grid), TwDpi.DeviceDpi(grid) / (float)original.Dpi);
        Assign(grid.AlternatingRowsDefaultCellStyle, altStyle, v => grid.AlternatingRowsDefaultCellStyle = v);

        ApplyHeader(grid, original, header, scale, context.Theme);
        ApplyRows(grid, original, row, scale);
        ApplyRowHeaders(grid, original, rowHeader, scale, context.Theme);

        // Satır hover'ı: yalnızca row:hover: varyantı tanımlıysa.
        if (parsed.HasPartCondition("row", TwState.Hover) || TwGridHover.IsAttached(grid))
        {
            var hover = parsed.HasPartCondition("row", TwState.Hover)
                ? parsed.ResolvePart("row", state | TwState.Hover)
                : new TwStyle();
            TwGridHover.For(grid).Update(hover.BackColor, hover.ForeColor);
        }

#if NETFRAMEWORK
        // .NET Framework'te DataGridView ekran değişince kolon genişliklerini ölçeklemez (PerMonitorV2).
        int dpi = TwDpi.DeviceDpi(grid);
        if (original.LastDpi != dpi && original.LastDpi > 0)
        {
            float ratio = dpi / (float)original.LastDpi;
            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.MinimumWidth = Math.Max(2, (int)Math.Round(column.MinimumWidth * ratio));
                if (column.InheritedAutoSizeMode != DataGridViewAutoSizeColumnMode.Fill)
                    column.Width = Math.Max(column.MinimumWidth, (int)Math.Round(column.Width * ratio));
            }
            if (grid.RowHeadersVisible) grid.RowHeadersWidth = Math.Max(4, (int)Math.Round(grid.RowHeadersWidth * ratio));
        }
        original.LastDpi = dpi;
#endif

        // Sıralama oku ve düzenleme kontrolleri: accent-* ile renklenir.
        var decorator = TwGridDecorator.For(grid);
        decorator.Accent = style.AccentColor ?? ColorTranslator.FromHtml((context.State & TwState.Dark) != 0 ? "#60a5fa" : "#2563eb");
        decorator.Styled = true;
        // Satır başlıkları gizliyken satır hatası (ErrorText) şeritle gösterilir.
        decorator.RowErrorColor = grid.RowHeadersVisible ? null
            : (context.State & TwState.HighContrast) != 0 ? SystemColors.Highlight
            : context.Theme.Parse("text-danger-600 dark:text-danger-400").Resolve(state & TwState.Dark).ForeColor;
        // Uygulamanın kolon zeminleri özgün yazı rengiyle tasarlanmıştır (bkz. TwGridDecorator.ColumnFore).
        decorator.KeptPairFore = cells.ForeColor.HasValue
            ? original.DefaultCellStyle.ForeColor.IsEmpty ? SystemColors.ControlText : original.DefaultCellStyle.ForeColor
            : null;

        // Arama eşleşmesi (match:), grup başlıkları (group:) ve alt toplam satırı (footer:).
        var match = parsed.ResolvePart("match", state);
        var group = parsed.ResolvePart("group", state);
        var footer = parsed.ResolvePart("footer", state);
        var partFont = grid.DefaultCellStyle.Font ?? grid.Font;
        decorator.Parts = new TwGridParts
        {
            MatchBack = match.BackColor,
            MatchFore = match.ForeColor,
            GroupBack = group.BackColor,
            GroupFore = group.ForeColor,
            GroupFont = group.HasFont ? TwFonts.Create(group, partFont, context.Theme, TwDpi.FontScale(grid)) : null,
            FooterBack = footer.BackColor,
            FooterFore = footer.ForeColor,
            FooterFont = footer.HasFont ? TwFonts.Create(footer, partFont, context.Theme, TwDpi.FontScale(grid)) : null,
        };

        // Koyu tema çevirmesi (TwGrid.EnableDarkInversion): grid açık stillerle çizilip çevrilir; kaydırma çubukları
        // ayrı pencerelerdir, çevrilmez, bu yüzden koyu çubuklar burada açılır.
        if (TwGridInversion.IsActive(grid) && !context.DesignMode && TwNativeTheme.Supports(grid))
            TwNativeTheme.Apply(grid, context.DarkScrollbars);
    }

    private static void ApplyHeader(DataGridView grid, GridOriginal original, TwStyle header, float scale, TwTheme theme)
    {
        // Görsel stiller açıkken (Windows teması) başlık renkleri yok sayılır.
        bool styled = header.BackColor.HasValue || header.ForeColor.HasValue;
        var visualStyles = styled ? false : original.EnableHeadersVisualStyles;
        if (grid.EnableHeadersVisualStyles != visualStyles) grid.EnableHeadersVisualStyles = visualStyles;

        var headerStyle = BuildCellStyle(original.ColumnHeadersDefaultCellStyle, header, grid.Font, scale, theme, TwDpi.FontScale(grid), TwDpi.DeviceDpi(grid) / (float)original.Dpi);
        if (styled)
        {
            // Seçim sırasında başlıklar vurgulanmasın.
            headerStyle.SelectionBackColor = headerStyle.BackColor;
            headerStyle.SelectionForeColor = headerStyle.ForeColor;
        }
        Assign(grid.ColumnHeadersDefaultCellStyle, headerStyle, v => grid.ColumnHeadersDefaultCellStyle = v);

        var headerBorder = header.BorderWidth == 0 ? DataGridViewHeaderBorderStyle.None
            : styled ? DataGridViewHeaderBorderStyle.Single
            : original.ColumnHeadersBorderStyle;
        if (grid.ColumnHeadersBorderStyle != headerBorder) grid.ColumnHeadersBorderStyle = headerBorder;

        if (header.Height is { IsPixels: true } height)
        {
            if (grid.ColumnHeadersHeightSizeMode != DataGridViewColumnHeadersHeightSizeMode.DisableResizing)
                grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            var value = System.Math.Max(4, (int)System.Math.Round(height.Value * scale));
            if (grid.ColumnHeadersHeight != value) grid.ColumnHeadersHeight = value;
        }
        else
        {
            if (grid.ColumnHeadersHeightSizeMode != original.ColumnHeadersHeightSizeMode)
                grid.ColumnHeadersHeightSizeMode = original.ColumnHeadersHeightSizeMode;
            if (original.ColumnHeadersHeightSizeMode != DataGridViewColumnHeadersHeightSizeMode.AutoSize
                && grid.ColumnHeadersHeight != original.ColumnHeadersHeight)
                grid.ColumnHeadersHeight = original.ColumnHeadersHeight;
        }
    }

    private static void ApplyRows(DataGridView grid, GridOriginal original, TwStyle row, float scale)
    {
        int? classHeight = row.Height is { IsPixels: true } length
            ? System.Math.Max(2, (int)System.Math.Round(length.Value * scale))
            : null;
        var height = classHeight ?? original.RowHeight;

        // VirtualMode/DataSource'ta grid satır yüksekliğini RowHeightInfoNeeded ile sorar: satırlar paylaşımdan
        // çıkarılmaz ve tek tek yerleşim yapılmaz (17 bin satırda ~10 sn yerine birkaç ms).
        if (TwGridRowHeights.Applies(grid))
        {
            var heights = TwGridRowHeights.For(grid);
            if (grid.RowTemplate.Height == height && heights.Height == classHeight) return;
            grid.RowTemplate.Height = height;
            heights.Height = classHeight;
            if (grid.RowCount > 0) grid.UpdateRowHeightInfo(0, updateToEnd: true);
            return;
        }

        if (grid.RowTemplate.Height == height) return;
        grid.RowTemplate.Height = height;
        // SharedRow okumak satırı paylaşımdan çıkarmaz; yalnızca yüksekliği farklı olanlara dokunulur.
        // Grup başlığı ve detayı olan satırlar bu yüksekliğe eklerini korur.
        var decorator = TwGridDecorator.TryGet(grid);
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            int want = decorator?.DesiredHeight(i, height) ?? height;
            if (grid.Rows.SharedRow(i).Height != want) grid.Rows[i].Height = want;
        }
    }

    private static void ApplyRowHeaders(DataGridView grid, GridOriginal original, TwStyle rowHeader, float scale, TwTheme theme)
    {
        var visible = rowHeader.Hidden is bool hidden ? !hidden : original.RowHeadersVisible;
        if (grid.RowHeadersVisible != visible) grid.RowHeadersVisible = visible;

        var style = BuildCellStyle(original.RowHeadersDefaultCellStyle, rowHeader, grid.Font, scale, theme, TwDpi.FontScale(grid), TwDpi.DeviceDpi(grid) / (float)original.Dpi);
        Assign(grid.RowHeadersDefaultCellStyle, style, v => grid.RowHeadersDefaultCellStyle = v);
    }

    private static DataGridViewCellStyle BuildCellStyle(DataGridViewCellStyle original, TwStyle part, Font gridFont, float scale, TwTheme theme, float fontScale, float dpiRatio)
    {
        var style = new DataGridViewCellStyle(original);
        // Özgün fontlar yakalandıkları DPI'dadır; ekran değiştiyse orana göre büyütülür.
        var originalFont = original.Font is Font own ? ScaleFont(own, dpiRatio) : null;
        if (originalFont != null) style.Font = originalFont;
        if (part.BackColor is Color back) style.BackColor = back;
        if (part.ForeColor is Color fore) style.ForeColor = fore;
        if (!part.Padding.IsEmpty) style.Padding = part.Padding.ApplyTo(original.Padding, scale);
        if (part.TextAlign is TwTextAlign align) style.Alignment = ToAlignment(align);
        if (part.NoWrap is bool noWrap) style.WrapMode = noWrap ? DataGridViewTriState.False : DataGridViewTriState.True;

        if (part.HasFont)
        {
            style.Font = TwFonts.Create(part, originalFont ?? gridFont, theme, fontScale);
        }
        else if (original.Font is null)
        {
            // Font kontrolden miras kalsın (temel text-sm gibi class'lar grid.Font'u değiştirir).
            style.Font = null;
        }

        return style;
    }

    private static DataGridViewContentAlignment ToAlignment(TwTextAlign align) => align switch
    {
        TwTextAlign.Center => DataGridViewContentAlignment.MiddleCenter,
        TwTextAlign.Right => DataGridViewContentAlignment.MiddleRight,
        _ => DataGridViewContentAlignment.MiddleLeft,
    };

    private static DataGridViewCellBorderStyle ToCellBorderStyle(bool vertical, bool horizontal) =>
        vertical && horizontal ? DataGridViewCellBorderStyle.Single
        : horizontal ? DataGridViewCellBorderStyle.SingleHorizontal
        : vertical ? DataGridViewCellBorderStyle.SingleVertical
        : DataGridViewCellBorderStyle.None;

    private static void Set(Color current, Color value, System.Action<Color> assign)
    {
        if (current.ToArgb() != value.ToArgb()) assign(value);
    }

    // DataGridViewCellStyle.Equals tüm property'leri karşılaştırır; aynıysa atama (ve yeniden çizim) yapılmaz.
    private static void Assign(DataGridViewCellStyle current, DataGridViewCellStyle value, System.Action<DataGridViewCellStyle> assign)
    {
        if (!current.Equals(value)) assign(value);
    }

    private sealed class GridOriginal
    {
        public Color BackgroundColor;
        public BorderStyle BorderStyle;
        public Color GridColor;
        public DataGridViewCellBorderStyle CellBorderStyle;
        public bool EnableHeadersVisualStyles;
        public DataGridViewHeaderBorderStyle ColumnHeadersBorderStyle;
        public DataGridViewColumnHeadersHeightSizeMode ColumnHeadersHeightSizeMode;
        public int ColumnHeadersHeight;
        public int RowHeight;
        public bool RowHeadersVisible;
        public DataGridViewCellStyle DefaultCellStyle = null!;

        /// <summary>DPI at which the original values were captured and the last applied DPI (to scale column widths).</summary>
        public int Dpi = 96;
        public int LastDpi;
        public DataGridViewCellStyle AlternatingRowsDefaultCellStyle = null!;
        public DataGridViewCellStyle ColumnHeadersDefaultCellStyle = null!;
        public DataGridViewCellStyle RowHeadersDefaultCellStyle = null!;

        public static GridOriginal Capture(DataGridView grid) => new GridOriginal
        {
            BackgroundColor = grid.BackgroundColor,
            BorderStyle = grid.BorderStyle,
            GridColor = grid.GridColor,
            CellBorderStyle = grid.CellBorderStyle,
            EnableHeadersVisualStyles = grid.EnableHeadersVisualStyles,
            ColumnHeadersBorderStyle = grid.ColumnHeadersBorderStyle,
            ColumnHeadersHeightSizeMode = grid.ColumnHeadersHeightSizeMode,
            ColumnHeadersHeight = grid.ColumnHeadersHeight,
            RowHeight = grid.RowTemplate.Height,
            RowHeadersVisible = grid.RowHeadersVisible,
            DefaultCellStyle = new DataGridViewCellStyle(grid.DefaultCellStyle),
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle(grid.AlternatingRowsDefaultCellStyle),
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle(grid.ColumnHeadersDefaultCellStyle),
            RowHeadersDefaultCellStyle = new DataGridViewCellStyle(grid.RowHeadersDefaultCellStyle),
            Dpi = TwDpi.DeviceDpi(grid),
            LastDpi = TwDpi.DeviceDpi(grid),
        };
    }
}

/// <summary>Supplies <c>row:h-*</c> heights to VirtualMode/DataSource grids without unsharing rows.</summary>
internal sealed class TwGridRowHeights
{
    private static readonly ConditionalWeakTable<DataGridView, TwGridRowHeights> Table = new ConditionalWeakTable<DataGridView, TwGridRowHeights>();

    private TwGridRowHeights(DataGridView grid)
    {
        grid.RowHeightInfoNeeded += (_, e) =>
        {
            // Grup başlığı ve satır detayı satıra eklenir (TwGridDecorator); yükseklik saklanmaz, her sorguda hesaplanır.
            int normal = Height ?? e.Height;
            var decorator = TwGridDecorator.TryGet(grid);
            int height = decorator?.DesiredHeight(e.RowIndex, normal) ?? normal;
            if (height != e.Height) e.Height = height;
        };
    }

    /// <summary>Height from the classes; <c>null</c> = rows keep their own height.</summary>
    public int? Height { get; set; }

    public static TwGridRowHeights? TryGet(DataGridView grid) => Table.TryGetValue(grid, out var heights) ? heights : null;

    /// <summary>The grid raises RowHeightInfoNeeded only under these conditions.</summary>
    public static bool Applies(DataGridView grid) =>
        (grid.VirtualMode || grid.DataSource != null) && grid.AutoSizeRowsMode == DataGridViewAutoSizeRowsMode.None;

    public static TwGridRowHeights For(DataGridView grid) => Table.GetValue(grid, g => new TwGridRowHeights(g));
}

/// <summary>Adds row hover to a DataGridView: the row under the cursor is painted with the <c>row:hover:</c> colors.</summary>
internal sealed class TwGridHover
{
    private static readonly ConditionalWeakTable<DataGridView, TwGridHover> Table = new ConditionalWeakTable<DataGridView, TwGridHover>();

    private readonly DataGridView _grid;
    private Color? _back;
    private Color? _fore;

    private TwGridHover(DataGridView grid)
    {
        _grid = grid;
        _grid.CellMouseEnter += (_, e) => SetRow(e.RowIndex);
        _grid.MouseLeave += (_, _) => SetRow(-1);
        _grid.CellFormatting += (_, e) => Format(e);
    }

    public int HoveredRow { get; private set; } = -1;

    public static bool IsAttached(DataGridView grid) => Table.TryGetValue(grid, out _);

    public static TwGridHover For(DataGridView grid) => Table.GetValue(grid, g => new TwGridHover(g));

    public void Update(Color? back, Color? fore)
    {
        if (_back == back && _fore == fore) return;
        _back = back;
        _fore = fore;
        InvalidateRow(HoveredRow);
    }

    internal void SetRow(int rowIndex)
    {
        if (rowIndex == HoveredRow) return;
        var previous = HoveredRow;
        HoveredRow = rowIndex;
        if (_back is null && _fore is null) return;
        InvalidateRow(previous);
        InvalidateRow(rowIndex);
    }

    // Seçili satırın seçim renkleri korunur; hover yalnızca normal renkleri değiştirir.
    internal void Format(DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex != HoveredRow || e.CellStyle is null) return;
        if (_back is Color back) e.CellStyle.BackColor = back;
        if (_fore is Color fore) e.CellStyle.ForeColor = fore;
    }

    private void InvalidateRow(int rowIndex)
    {
        if (rowIndex >= 0 && rowIndex < _grid.RowCount) _grid.InvalidateRow(rowIndex);
    }
}
