using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Runtime information of a grid: the dark mode state (cell types choose dark: variants accordingly).</summary>
internal static class TwGridState
{
    private static readonly ConditionalWeakTable<DataGridView, Box> Dark = new ConditionalWeakTable<DataGridView, Box>();

    public static bool IsDark(DataGridView? grid) => grid != null && Dark.TryGetValue(grid, out var box) && box.Value;

    internal static void SetDark(DataGridView grid, bool dark)
    {
        var box = Dark.GetValue(grid, _ => new Box());
        if (box.Value == dark) return;
        box.Value = dark;
        grid.Invalidate();
    }

    internal static TwState State(DataGridView? grid) => IsDark(grid) ? TwState.Dark : TwState.None;

    private sealed class Box
    {
        public bool Value;
    }
}

internal static class TwCellPainting
{
    public static float Scale(DataGridView? grid) => (grid is null ? 96 : TwDpi.DeviceDpi(grid)) / 96f;

    /// <summary>Lets the DataGridView paint the background and borders; the content is ours.</summary>
    public static DataGridViewPaintParts WithoutContent(DataGridViewPaintParts parts) =>
        parts & ~(DataGridViewPaintParts.ContentForeground | DataGridViewPaintParts.ContentBackground);

    public static Rectangle Content(Rectangle cellBounds, DataGridViewCellStyle style) => new Rectangle(
        cellBounds.X + style.Padding.Left + 2,
        cellBounds.Y + style.Padding.Top,
        Math.Max(0, cellBounds.Width - style.Padding.Horizontal - 4),
        Math.Max(0, cellBounds.Height - style.Padding.Vertical));

    /// <summary>Places a box in the content area according to the cell alignment.</summary>
    public static Rectangle Align(Rectangle content, Size size, DataGridViewContentAlignment alignment)
    {
        int value = (int)alignment;
        int x = (value & 0x222) != 0 ? content.X + (content.Width - size.Width) / 2
            : (value & 0x444) != 0 ? content.Right - size.Width
            : content.X;
        int y = (value & 0x7) != 0 ? content.Y
            : (value & 0x700) != 0 ? content.Bottom - size.Height
            : content.Y + (content.Height - size.Height) / 2;
        return new Rectangle(x, y, Math.Min(size.Width, content.Width), Math.Min(size.Height, content.Height));
    }

    public static bool Selected(DataGridViewElementStates state) => (state & DataGridViewElementStates.Selected) != 0;
}

/// <summary>
/// A badge colored by value ("Alive", "Dead" …). <see cref="Variants"/>: <c>value: classes</c> pairs separated by <c>;</c>;
/// <c>*</c> is for all other values:
/// <c>Alive: bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-100; Dead: bg-red-100 text-red-800; *: bg-gray-100 text-gray-700</c>.
/// Default look: <c>rounded-full px-2 py-0.5 text-xs font-medium</c>.
/// </summary>
[ToolboxItem(false)]
public class TwBadgeColumn : DataGridViewColumn
{
    private const string BaseClasses = "rounded-full px-2 py-0.5 text-xs font-medium";
    private string _variants = "*: bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-100";
    private Dictionary<string, string>? _parsed;

    public TwBadgeColumn() : base(new TwBadgeCell())
    {
    }

    [Category("FormWind"), DefaultValue("*: bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-100")]
    [Description("value: classes; ... (* = everything else)")]
    public string Variants
    {
        get => _variants;
        set
        {
            _variants = value ?? "";
            _parsed = null;
            DataGridView?.InvalidateColumn(Index);
        }
    }

    internal TwStyle StyleFor(string value, DataGridView? grid)
    {
        _parsed ??= ParseVariants(_variants);
        if (!_parsed.TryGetValue(value, out var classes) && !_parsed.TryGetValue("*", out classes)) classes = "";
        return TwTheme.Current.Parse(BaseClasses + " " + classes).Resolve(TwGridState.State(grid));
    }

    internal static Dictionary<string, string> ParseVariants(string text)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in text.Split(';'))
        {
            // İlk ':' değeri ayırır; sonrasındaki "dark:bg-…" gibi varyantlar class listesine aittir.
            int colon = part.IndexOf(':');
            if (colon <= 0) continue;
            var key = part.Substring(0, colon).Trim();
            if (key.Length > 0) map[key] = part.Substring(colon + 1).Trim();
        }
        return map;
    }

    public override object Clone()
    {
        var clone = (TwBadgeColumn)base.Clone();
        clone.Variants = Variants;
        return clone;
    }
}

public class TwBadgeCell : DataGridViewTextBoxCell
{
    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, TwCellPainting.WithoutContent(paintParts));
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0) return;

        var text = formattedValue as string;
        if (string.IsNullOrEmpty(text) || !(OwningColumn is TwBadgeColumn column)) return;

        var scale = TwCellPainting.Scale(DataGridView);
        var style = column.StyleFor(text!, DataGridView);
        var font = TwFonts.Create(style, cellStyle.Font ?? DataGridView!.Font, TwTheme.Current, TwDpi.FontScale(DataGridView!));
        var textSize = TextRenderer.MeasureText(graphics, text, font, Size.Empty, TextFormatFlags.NoPadding);
        var padding = style.Padding.ApplyTo(Padding.Empty, scale);
        var size = new Size(textSize.Width + padding.Horizontal, textSize.Height + padding.Vertical);
        var pill = TwCellPainting.Align(TwCellPainting.Content(cellBounds, cellStyle), size, cellStyle.Alignment);

        TwPainter.FillRounded(graphics, pill, (style.Radius ?? 9999) * scale, style.BackColor ?? Color.Transparent);
        TextRenderer.DrawText(graphics, text, font, pill, style.ForeColor ?? cellStyle.ForeColor,
            TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

/// <summary>Round avatar column. The value is an <see cref="Image"/>; otherwise the <see cref="Placeholder"/> color is drawn.</summary>
[ToolboxItem(false)]
public class TwAvatarColumn : DataGridViewImageColumn
{
    public TwAvatarColumn()
    {
        CellTemplate = new TwAvatarCell();
        DefaultCellStyle.NullValue = null;
    }

    [Category("FormWind"), DefaultValue(32), Description("Avatar size (96 DPI pixels).")]
    public int AvatarSize { get; set; } = 32;

    [Category("FormWind"), DefaultValue(9999f), Description("Corner radius; 9999 = circle.")]
    public float Radius { get; set; } = 9999f;

    [Category("FormWind")]
    public Color Placeholder { get; set; } = ColorTranslator.FromHtml("#e5e7eb");

    private bool ShouldSerializePlaceholder() => Placeholder.ToArgb() != ColorTranslator.FromHtml("#e5e7eb").ToArgb();

    public override object Clone()
    {
        var clone = (TwAvatarColumn)base.Clone();
        clone.AvatarSize = AvatarSize;
        clone.Radius = Radius;
        clone.Placeholder = Placeholder;
        return clone;
    }
}

public class TwAvatarCell : DataGridViewImageCell
{
    public TwAvatarCell()
    {
        ValueIsIcon = false;
    }

    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, TwCellPainting.WithoutContent(paintParts));
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0) return;

        var column = OwningColumn as TwAvatarColumn;
        var scale = TwCellPainting.Scale(DataGridView);
        int side = (int)Math.Round((column?.AvatarSize ?? 32) * scale);
        var box = TwCellPainting.Align(TwCellPainting.Content(cellBounds, cellStyle), new Size(side, side), cellStyle.Alignment);
        var radius = (column?.Radius ?? 9999f) * scale;

        if (value is Image image)
        {
            using var scaled = new Bitmap(box.Width, box.Height);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, TwPictureBox.Destination(image.Size, box.Size, TwImageFit.Cover));
            }
            using var brush = new TextureBrush(scaled, System.Drawing.Drawing2D.WrapMode.Clamp);
            brush.TranslateTransform(box.X, box.Y);
            var state = graphics.Save();
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            using (var path = TwPainter.CreateRoundedRect(box, TwPainter.EffectiveRadius(radius, box.Size)))
                graphics.FillPath(brush, path);
            graphics.Restore(state);
        }
        else
        {
            TwPainter.FillRounded(graphics, box, radius, column?.Placeholder ?? Color.LightGray);
        }
    }
}

/// <summary>Column whose value is an icon name (e.g. <c>check</c>, <c>#E710</c>); the color is the cell's text color.</summary>
[ToolboxItem(false)]
public class TwIconColumn : DataGridViewColumn
{
    public TwIconColumn() : base(new TwIconCell())
    {
    }
}

public class TwIconCell : DataGridViewTextBoxCell
{
    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, TwCellPainting.WithoutContent(paintParts));
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0) return;
        var name = formattedValue as string;
        if (!TwTheme.Current.HasIcon(name)) return;

        var font = cellStyle.Font ?? DataGridView!.Font;
        int em = TwIconRenderer.EmPixels(font.SizeInPoints, DataGridView is null ? 96 : TwDpi.DeviceDpi(DataGridView));
        var box = TwCellPainting.Align(TwCellPainting.Content(cellBounds, cellStyle), new Size(em, em), cellStyle.Alignment);
        var color = TwCellPainting.Selected(cellState) ? cellStyle.SelectionForeColor : cellStyle.ForeColor;
        TwIconRenderer.DrawIcon(graphics, name, em, box, color);
    }
}

/// <summary>
/// Progress bar column. The value is a number from 0 to <see cref="Maximum"/>. Track <see cref="TrackColor"/>, fill <see cref="BarColor"/>;
/// <see cref="DarkTrackColor"/> is used in dark mode.
/// </summary>
[ToolboxItem(false)]
public class TwProgressColumn : DataGridViewColumn
{
    public TwProgressColumn() : base(new TwProgressCell())
    {
    }

    [Category("FormWind"), DefaultValue(100.0)]
    public double Maximum { get; set; } = 100;

    [Category("FormWind"), DefaultValue(true), Description("Percentage text to the right of the bar.")]
    public bool ShowText { get; set; } = true;

    [Category("FormWind")]
    public Color BarColor { get; set; } = ColorTranslator.FromHtml("#2563eb");

    [Category("FormWind")]
    public Color TrackColor { get; set; } = ColorTranslator.FromHtml("#e5e7eb");

    [Category("FormWind")]
    public Color DarkTrackColor { get; set; } = ColorTranslator.FromHtml("#374151");

    public override object Clone()
    {
        var clone = (TwProgressColumn)base.Clone();
        clone.Maximum = Maximum;
        clone.ShowText = ShowText;
        clone.BarColor = BarColor;
        clone.TrackColor = TrackColor;
        clone.DarkTrackColor = DarkTrackColor;
        return clone;
    }
}

public class TwProgressCell : DataGridViewTextBoxCell
{
    public override Type ValueType => typeof(double);

    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, TwCellPainting.WithoutContent(paintParts));
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0) return;

        var column = OwningColumn as TwProgressColumn;
        double max = column?.Maximum > 0 ? column.Maximum : 100;
        double amount = value is IConvertible convertible ? Math.Max(0, Math.Min(1, convertible.ToDouble(CultureInfo.InvariantCulture) / max)) : 0;

        var scale = TwCellPainting.Scale(DataGridView);
        var content = TwCellPainting.Content(cellBounds, cellStyle);
        var font = cellStyle.Font ?? DataGridView!.Font;
        string label = ((int)Math.Round(amount * 100)).ToString(CultureInfo.CurrentCulture) + "%";
        int textWidth = column?.ShowText != false ? TextRenderer.MeasureText(graphics, "100%", font, Size.Empty, TextFormatFlags.NoPadding).Width : 0;
        int gap = textWidth > 0 ? (int)(8 * scale) : 0;
        int height = Math.Max(2, (int)Math.Round(6 * scale));

        var track = new Rectangle(content.X, content.Y + (content.Height - height) / 2, Math.Max(0, content.Width - textWidth - gap), height);
        bool dark = TwGridState.IsDark(DataGridView);
        TwPainter.FillRounded(graphics, track, height / 2f, dark ? column?.DarkTrackColor ?? Color.DimGray : column?.TrackColor ?? Color.LightGray);
        if (amount > 0)
        {
            var bar = new Rectangle(track.X, track.Y, Math.Max(height, (int)Math.Round(track.Width * amount)), height);
            TwPainter.FillRounded(graphics, bar, height / 2f, column?.BarColor ?? Color.RoyalBlue);
        }

        if (textWidth > 0)
        {
            var color = TwCellPainting.Selected(cellState) ? cellStyle.SelectionForeColor : cellStyle.ForeColor;
            TextRenderer.DrawText(graphics, label, font, new Rectangle(track.Right + gap, content.Y, textWidth, content.Height), color,
                TextFormatFlags.NoPadding | TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}

/// <summary>
/// Checkbox column with rounded corners (TwCheckBox look). A checked box is filled with <see cref="AccentColor"/> with a white tick;
/// with <see cref="DataGridViewCheckBoxColumn.ThreeState"/> the indeterminate state is shown as a dash.
/// </summary>
[ToolboxItem(false)]
public class TwCheckColumn : DataGridViewCheckBoxColumn
{
    public TwCheckColumn()
    {
        CellTemplate = new TwCheckCell();
    }

    [Category("FormWind")]
    public Color AccentColor { get; set; } = ColorTranslator.FromHtml("#2563eb");

    private bool ShouldSerializeAccentColor() => AccentColor.ToArgb() != ColorTranslator.FromHtml("#2563eb").ToArgb();

    [Category("FormWind"), DefaultValue(16), Description("Box size (96 DPI pixels).")]
    public int BoxSize { get; set; } = 16;

    public override object Clone()
    {
        var clone = (TwCheckColumn)base.Clone();
        clone.AccentColor = AccentColor;
        clone.BoxSize = BoxSize;
        return clone;
    }
}

public class TwCheckCell : DataGridViewCheckBoxCell
{
    /// <summary>Check state of the cell value (bool, CheckState or TrueValue/IndeterminateValue).</summary>
    internal CheckState StateOf(object? value)
    {
        if (value is CheckState state) return state;
        if (value is bool flag) return flag ? CheckState.Checked : CheckState.Unchecked;
        if (value is null || value == DBNull.Value) return ThreeState ? CheckState.Indeterminate : CheckState.Unchecked;
        if (TrueValue != null && Equals(value, TrueValue)) return CheckState.Checked;
        if (IndeterminateValue != null && Equals(value, IndeterminateValue)) return CheckState.Indeterminate;
        return CheckState.Unchecked;
    }

    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, TwCellPainting.WithoutContent(paintParts));
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0) return;

        var column = OwningColumn as TwCheckColumn;
        var scale = TwCellPainting.Scale(DataGridView);
        int side = (int)Math.Round((column?.BoxSize ?? 16) * scale);
        var content = TwCellPainting.Content(cellBounds, cellStyle);
        var alignment = cellStyle.Alignment == DataGridViewContentAlignment.NotSet ? DataGridViewContentAlignment.MiddleCenter : cellStyle.Alignment;
        var box = TwCellPainting.Align(content, new Size(side, side), alignment);
        float radius = 4 * scale;
        var accent = column?.AccentColor ?? ColorTranslator.FromHtml("#2563eb");
        bool dark = TwGridState.IsDark(DataGridView);
        var state = StateOf(formattedValue ?? value);

        if (state == CheckState.Unchecked)
        {
            TwPainter.FillRounded(graphics, box, radius, ColorTranslator.FromHtml(dark ? "#111827" : "#ffffff"));
            TwPainter.StrokeRounded(graphics, box, radius, Math.Max(1f, scale), ColorTranslator.FromHtml(dark ? "#4b5563" : "#9ca3af"));
            return;
        }

        TwPainter.FillRounded(graphics, box, radius, accent);
        var icon = state == CheckState.Checked ? "check" : "minus";
        TwIconRenderer.DrawIcon(graphics, icon, (int)(side * 0.8f), box, Color.White);
    }
}

/// <summary>
/// Button column with rounded corners. The look is set with <see cref="Classes"/> (<c>bg-* text-* rounded-* hover:bg-* dark:…</c>);
/// clicks are handled with the <c>CellContentClick</c> event, as with DataGridViewButtonColumn.
/// </summary>
[ToolboxItem(false)]
public class TwButtonColumn : DataGridViewButtonColumn
{
    internal const string DefaultClasses =
        "rounded-md text-xs font-semibold bg-gray-100 text-gray-800 hover:bg-gray-200 dark:bg-gray-700 dark:text-gray-100 dark:hover:bg-gray-600";

    private string _classes = DefaultClasses;

    public TwButtonColumn()
    {
        CellTemplate = new TwButtonCell();
    }

    [Category("FormWind"), DefaultValue(DefaultClasses)]
    public string Classes
    {
        get => _classes;
        set
        {
            _classes = value ?? "";
            DataGridView?.InvalidateColumn(Index);
        }
    }

    /// <summary>The row under the mouse (-1 = none).</summary>
    internal int HoverRow { get; set; } = -1;

    internal TwStyle StyleFor(DataGridView? grid, bool hover)
    {
        var state = TwGridState.State(grid);
        if (hover) state |= TwState.Hover;
        return TwTheme.Current.Parse(_classes).Resolve(state);
    }

    public override object Clone()
    {
        var clone = (TwButtonColumn)base.Clone();
        clone.Classes = Classes;
        return clone;
    }
}

public class TwButtonCell : DataGridViewButtonCell
{
    protected override void OnMouseEnter(int rowIndex)
    {
        base.OnMouseEnter(rowIndex);
        if (OwningColumn is TwButtonColumn column)
        {
            column.HoverRow = rowIndex;
            DataGridView?.InvalidateCell(ColumnIndex, rowIndex);
        }
    }

    protected override void OnMouseLeave(int rowIndex)
    {
        base.OnMouseLeave(rowIndex);
        if (OwningColumn is TwButtonColumn column && column.HoverRow == rowIndex)
        {
            column.HoverRow = -1;
            DataGridView?.InvalidateCell(ColumnIndex, rowIndex);
        }
    }

#pragma warning disable CS8765 // DataGridViewButtonCell.Paint .NET 8'de nullable parametreli.
    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates elementState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        // Arka plan ve kenarlık DataGridView'dan; native buton yüzeyi çizilmez.
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, elementState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, paintParts & (DataGridViewPaintParts.Background | DataGridViewPaintParts.Border | DataGridViewPaintParts.SelectionBackground));
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0) return;

        var column = OwningColumn as TwButtonColumn;
        var text = column?.UseColumnTextForButtonValue == true ? column.Text : formattedValue as string;
        if (string.IsNullOrEmpty(text)) return;

        var scale = TwCellPainting.Scale(DataGridView);
        var style = column?.StyleFor(DataGridView, column.HoverRow == rowIndex) ?? new TwStyle();
        var font = TwFonts.Create(style, cellStyle.Font ?? DataGridView!.Font, TwTheme.Current, TwDpi.FontScale(DataGridView!));
        var content = TwCellPainting.Content(cellBounds, cellStyle);
        var inset = (int)Math.Round(3 * scale);
        var button = Rectangle.Inflate(new Rectangle(content.X, cellBounds.Y, content.Width, cellBounds.Height), 0, -inset);

        TwPainter.FillRounded(graphics, button, (style.Radius ?? 6) * scale, style.BackColor ?? ColorTranslator.FromHtml("#f3f4f6"));
        TextRenderer.DrawText(graphics, text, font, button, style.ForeColor ?? cellStyle.ForeColor,
            TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
#pragma warning restore CS8765
}
