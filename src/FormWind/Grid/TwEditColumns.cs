using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Editable text column for data-entry tables: every cell is drawn as an input box (the theme's <c>input</c> look, plus
/// <see cref="Classes"/>) with a <see cref="Placeholder"/> when empty, a red border when <see cref="Validate"/> (or the cell's
/// <c>ErrorText</c>) reports an error and a focus ring on the current cell. Only the current cell hosts a real text box: with
/// <see cref="EditOnFocus"/> it opens as soon as the cell is reached, so the arrow keys, Tab and Enter move through the table
/// like a form. Read-only cells are drawn as plain text.
/// <code>
/// grid.Columns.Add(new TwTextColumn { DataPropertyName = "Note", HeaderText = "Açıklama", Placeholder = "Not ekleyin",
///     Validate = v => string.IsNullOrWhiteSpace(v as string) ? "Zorunlu" : null });
/// </code>
/// </summary>
[ToolboxItem(false)]
public class TwTextColumn : DataGridViewTextBoxColumn
{
    private readonly TwEditOnFocus _editOnFocus;

    public TwTextColumn()
    {
        CellTemplate = new TwTextCell();
        _editOnFocus = new TwEditOnFocus(this);
    }

    /// <summary>Hint drawn in empty cells.</summary>
    [Category("FormWind"), DefaultValue(""), Localizable(true)]
    public string Placeholder { get; set; } = "";

    /// <summary>Classes added to the theme's <c>input</c> look of the boxes (<c>rounded-none</c>, <c>bg-gray-50</c>, …).</summary>
    [Category("FormWind"), DefaultValue("")]
    public string Classes { get; set; } = "";

    /// <summary>Opens the editor when the cell becomes current (keyboard or mouse), not only on typing or F2.</summary>
    [Category("FormWind"), DefaultValue(true)]
    public bool EditOnFocus
    {
        get => _editOnFocus.Enabled;
        set => _editOnFocus.Enabled = value;
    }

    /// <summary>Error text for a value (<c>null</c> = valid); the box gets the <c>invalid:</c> look and the text is the cell's error.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, string?>? Validate { get; set; }

    protected override void OnDataGridViewChanged()
    {
        base.OnDataGridViewChanged();
        _editOnFocus.Attach(DataGridView);
    }

    public override object Clone()
    {
        var clone = (TwTextColumn)base.Clone();
        clone.Placeholder = Placeholder;
        clone.Classes = Classes;
        clone.EditOnFocus = EditOnFocus;
        clone.Validate = Validate;
        return clone;
    }
}

public class TwTextCell : DataGridViewTextBoxCell
{
    private TwTextColumn? Column => OwningColumn as TwTextColumn;

    private object? ValueAt(int rowIndex) =>
        DataGridView is null || rowIndex < 0 || rowIndex >= DataGridView.Rows.Count ? null : GetValue(rowIndex);

    /// <summary>The error of the value in <paramref name="rowIndex"/>: the cell's <c>ErrorText</c>, else the column's <see cref="TwTextColumn.Validate"/>.</summary>
    public string? GetError(int rowIndex) => TwInputCell.Error(this, rowIndex, base.GetErrorText(rowIndex), ValueAt(rowIndex), Column?.Validate);

    protected override string GetErrorText(int rowIndex) => GetError(rowIndex) ?? "";

    protected override Size GetPreferredSize(Graphics graphics, DataGridViewCellStyle cellStyle, int rowIndex, Size constraintSize) =>
        TwInputCell.Grow(base.GetPreferredSize(graphics, cellStyle, rowIndex, constraintSize), DataGridView, 0);

    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        if (ReadOnly || DataGridView is null)
        {
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle, advancedBorderStyle, paintParts);
            return;
        }
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, TwCellPainting.WithoutContent(paintParts) & ~DataGridViewPaintParts.ErrorIcon & ~DataGridViewPaintParts.Focus);
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0) return;
        bool editing = DataGridView.IsCurrentCellInEditMode && DataGridView.CurrentCellAddress == new Point(ColumnIndex, rowIndex);
        TwInputCell.Paint(graphics, this, cellBounds, rowIndex, cellStyle, editing ? "" : formattedValue as string,
            Column?.Placeholder ?? "", Column?.Classes ?? "", GetError(rowIndex) != null, chevron: false, hideTextWhenEmpty: editing);
    }

#pragma warning disable CS8765 // .NET 8'de nullable parametreli.
    public override void InitializeEditingControl(int rowIndex, object initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
    {
        var colors = TwInputCell.Colors(DataGridView, Column?.Classes ?? "", focused: true, invalid: false);
        var style = new DataGridViewCellStyle(dataGridViewCellStyle) { BackColor = colors.Back, ForeColor = colors.Fore, SelectionBackColor = colors.Back, SelectionForeColor = colors.Fore };
        base.InitializeEditingControl(rowIndex, initialFormattedValue, style);
        if (DataGridView?.EditingControl is TextBox box)
        {
            box.BackColor = colors.Back;
            box.ForeColor = colors.Fore;
            box.BorderStyle = BorderStyle.None;
        }
        if (DataGridView?.EditingPanel is Panel panel) panel.BackColor = colors.Back;
    }
#pragma warning restore CS8765

    // Düzenleyici hücrenin tamamını değil kutunun içini kaplar: kenar ve odak halkasını hücre çizer.
    public override void PositionEditingControl(bool setLocation, bool setSize, Rectangle cellBounds, Rectangle cellClip,
        DataGridViewCellStyle cellStyle, bool singleVerticalBorderAdded, bool singleHorizontalBorderAdded,
        bool isFirstDisplayedColumn, bool isFirstDisplayedRow)
    {
        base.PositionEditingControl(setLocation, setSize, cellBounds, cellClip, cellStyle, singleVerticalBorderAdded,
            singleHorizontalBorderAdded, isFirstDisplayedColumn, isFirstDisplayedRow);
        if (DataGridView is not { EditingPanel: Panel panel, EditingControl: Control editor }) return;
        var inner = TwInputCell.EditorBounds(DataGridView, cellBounds, Column?.Classes ?? "", chevron: false);
        var visible = Rectangle.Intersect(inner, cellClip);
        if (visible.Width <= 0 || visible.Height <= 0) return;
        panel.Bounds = visible;
        float s = TwCellPainting.Scale(DataGridView);
        int pad = Math.Max(0, (int)Math.Round(TwInputCell.TextInset * s) - (inner.Left - TwInputCell.BoxBounds(cellBounds, s).Left));
        int height = Math.Min(visible.Height, editor.PreferredSize.Height);
        editor.SetBounds(inner.Left - visible.Left + pad, (visible.Height - height) / 2, Math.Max(1, inner.Width - pad * 2), height);
    }
}

/// <summary>
/// Editable choice column: cells look like a <see cref="TwSelect"/> (input box, the choice's text, a down arrow; the
/// <see cref="Placeholder"/> when empty). Clicking the cell, Space, F4 or Alt+↓ opens the list as a menu on the cell;
/// typing a letter picks the next item starting with it. The value stored is the item (or its <see cref="ValueMember"/>);
/// the text shown is its <see cref="DisplayMember"/> (or <c>ToString()</c>).
/// <code>
/// grid.Columns.Add(new TwSelectColumn { DataPropertyName = "Cause", HeaderText = "Neden", Placeholder = "Seçin",
///     Items = { "Arıza", "Ayar", "Malzeme yok" } });
/// </code>
/// </summary>
[ToolboxItem(false)]
public class TwSelectColumn : DataGridViewColumn
{
    public TwSelectColumn() : base(new TwSelectCell())
    {
    }

    /// <summary>The choices.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<object> Items { get; } = new List<object>();

    /// <summary>Property of an item shown in the cell and the list (empty = <c>ToString()</c>).</summary>
    [Category("FormWind"), DefaultValue("")]
    public string DisplayMember { get; set; } = "";

    /// <summary>Property of an item stored as the cell's value (empty = the item itself).</summary>
    [Category("FormWind"), DefaultValue("")]
    public string ValueMember { get; set; } = "";

    [Category("FormWind"), DefaultValue(""), Localizable(true)]
    public string Placeholder { get; set; } = "";

    /// <summary>Classes added to the theme's <c>input</c> look of the boxes.</summary>
    [Category("FormWind"), DefaultValue("")]
    public string Classes { get; set; } = "";

    /// <summary>Error text for a value (<c>null</c> = valid), shown as the <c>invalid:</c> look.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, string?>? Validate { get; set; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override DataGridViewCell CellTemplate
    {
        get => base.CellTemplate;
        set
        {
            if (value != null && value is not TwSelectCell) throw new InvalidCastException("CellTemplate must be a TwSelectCell.");
            base.CellTemplate = value;
        }
    }

    /// <summary>The value an item stores.</summary>
    public object? ValueOf(object? item) => item is null || ValueMember.Length == 0 ? item : Member(item, ValueMember);

    /// <summary>The text shown for an item.</summary>
    public string TextOf(object? item)
    {
        if (item is null) return "";
        var shown = DisplayMember.Length == 0 ? item : Member(item, DisplayMember);
        return Convert.ToString(shown, CultureInfo.CurrentCulture) ?? "";
    }

    /// <summary>The item that stores <paramref name="value"/>; <c>null</c> when none does.</summary>
    public object? ItemOf(object? value)
    {
        if (value is null || value is DBNull) return null;
        foreach (var item in Items)
            if (Equals(ValueOf(item), value)) return item;
        return null;
    }

    private static object? Member(object item, string name) => TypeDescriptor.GetProperties(item)[name]?.GetValue(item) ?? item;

    public override object Clone()
    {
        var clone = (TwSelectColumn)base.Clone();
        clone.Items.AddRange(Items);
        clone.DisplayMember = DisplayMember;
        clone.ValueMember = ValueMember;
        clone.Placeholder = Placeholder;
        clone.Classes = Classes;
        clone.Validate = Validate;
        return clone;
    }
}

public class TwSelectCell : DataGridViewTextBoxCell
{
    private string _typeAhead = "";
    private DateTime _typeAheadTime;

    private TwSelectColumn? Column => OwningColumn as TwSelectColumn;

    private object? ValueAt(int rowIndex) =>
        DataGridView is null || rowIndex < 0 || rowIndex >= DataGridView.Rows.Count ? null : GetValue(rowIndex);

    public override Type ValueType => typeof(object);

    // Metin kutusu açılmaz; seçim hücrenin üstünde açılan menüyle yapılır.
    public override Type? EditType => null;

    public override bool KeyEntersEditMode(KeyEventArgs e) => false;

    /// <summary>The menu open on this column's cell, while it is open.</summary>
    internal static ContextMenuStrip? OpenMenu { get; private set; }

    public string? GetError(int rowIndex) => TwInputCell.Error(this, rowIndex, base.GetErrorText(rowIndex), ValueAt(rowIndex), Column?.Validate);

    protected override string GetErrorText(int rowIndex) => GetError(rowIndex) ?? "";

    protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
        TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter, DataGridViewDataErrorContexts context)
    {
        var column = Column;
        string text = column is null ? Convert.ToString(value, CultureInfo.CurrentCulture) ?? ""
            : column.ItemOf(value) is object item ? column.TextOf(item)
            : value is null || value is DBNull ? "" : Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
        return base.GetFormattedValue(text, rowIndex, ref cellStyle, null, null, context);
    }

    protected override Size GetPreferredSize(Graphics graphics, DataGridViewCellStyle cellStyle, int rowIndex, Size constraintSize) =>
        TwInputCell.Grow(base.GetPreferredSize(graphics, cellStyle, rowIndex, constraintSize), DataGridView, TwInputCell.ChevronWidth);

    protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        if (ReadOnly || DataGridView is null)
        {
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle, advancedBorderStyle, paintParts);
            return;
        }
        base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle,
            advancedBorderStyle, TwCellPainting.WithoutContent(paintParts) & ~DataGridViewPaintParts.ErrorIcon & ~DataGridViewPaintParts.Focus);
        if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0) return;
        TwInputCell.Paint(graphics, this, cellBounds, rowIndex, cellStyle, formattedValue as string, Column?.Placeholder ?? "",
            Column?.Classes ?? "", GetError(rowIndex) != null, chevron: true, hideTextWhenEmpty: false);
    }

    protected override void OnMouseClick(DataGridViewCellMouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && e.RowIndex >= 0) ShowDropDown(e.RowIndex);
    }

    protected override void OnKeyDown(KeyEventArgs e, int rowIndex)
    {
        base.OnKeyDown(e, rowIndex);
        if (e.KeyCode is Keys.F4 or Keys.Space && !e.Alt || e.Alt && e.KeyCode == Keys.Down)
        {
            ShowDropDown(rowIndex);
            e.Handled = true;
        }
    }

    protected override void OnKeyPress(KeyPressEventArgs e, int rowIndex)
    {
        base.OnKeyPress(e, rowIndex);
        var column = Column;
        if (column is null || DataGridView is null || ReadOnlyAt(rowIndex) || char.IsControl(e.KeyChar) || e.KeyChar == ' ') return;

        // Yazarak seçme (TwSelect gibi): 1 sn içindeki harfler birikir; aynı harf tekrar o harfle başlayan sonrakine geçer.
        var now = DateTime.UtcNow;
        if ((now - _typeAheadTime).TotalMilliseconds > 1000) _typeAhead = "";
        _typeAheadTime = now;
        bool repeat = _typeAhead.Length == 1 && char.ToUpper(_typeAhead[0], CultureInfo.CurrentCulture) == char.ToUpper(e.KeyChar, CultureInfo.CurrentCulture);
        if (!repeat) _typeAhead += e.KeyChar;
        int current = column.Items.IndexOf(column.ItemOf(DataGridView.Rows[rowIndex].Cells[ColumnIndex].Value)!);
        int start = repeat ? current + 1 : Math.Max(0, current);
        for (int n = 0; n < column.Items.Count; n++)
        {
            int i = (start + n) % column.Items.Count;
            if (CultureInfo.CurrentCulture.CompareInfo.IsPrefix(column.TextOf(column.Items[i]), _typeAhead, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace))
            {
                Choose(rowIndex, column.Items[i]);
                break;
            }
        }
        e.Handled = true;
    }

    private bool ReadOnlyAt(int rowIndex) =>
        DataGridView is null || DataGridView.ReadOnly || OwningColumn?.ReadOnly == true || DataGridView.Rows[rowIndex].Cells[ColumnIndex].ReadOnly;

    /// <summary>Opens the list on the cell in <paramref name="rowIndex"/> (as a click, Space, F4 or Alt+↓ does).</summary>
    public void ShowDropDown(int rowIndex)
    {
        var grid = DataGridView;
        var column = Column;
        if (grid is null || column is null || rowIndex < 0 || ReadOnlyAt(rowIndex) || column.Items.Count == 0) return;
        OpenMenu?.Close();
        var cellBounds = grid.GetCellDisplayRectangle(ColumnIndex, rowIndex, false);
        if (cellBounds.IsEmpty) return;
        float s = TwCellPainting.Scale(grid);
        var box = TwInputCell.BoxBounds(cellBounds, s);
        var current = column.ItemOf(grid.Rows[rowIndex].Cells[ColumnIndex].Value);
        var menu = new ContextMenuStrip { Renderer = new TwToolStripRenderer(TwInputCell.MenuColors(grid)), ShowImageMargin = false, ShowCheckMargin = true, Font = grid.Font };
        foreach (var item in column.Items)
        {
            var choice = item;
            var menuItem = new ToolStripMenuItem(column.TextOf(item)) { Checked = Equals(item, current) };
            menuItem.Click += (_, _) => Choose(rowIndex, choice);
            menu.Items.Add(menuItem);
        }
        menu.MinimumSize = new Size(box.Width, 0);
        menu.Closed += (_, _) =>
        {
            if (OpenMenu == menu) OpenMenu = null;
            TwInputCell.Post(grid, menu.Dispose);
        };
        OpenMenu = menu;
        menu.Show(grid, new Point(box.Left, box.Bottom + (int)Math.Round(2 * s)));
    }

    private void Choose(int rowIndex, object item)
    {
        var grid = DataGridView;
        var column = Column;
        if (grid is null || column is null || rowIndex >= grid.Rows.Count) return;
        var cell = grid.Rows[rowIndex].Cells[ColumnIndex];
        var value = column.ValueOf(item);
        if (Equals(cell.Value, value)) return;
        cell.Value = value;
        grid.InvalidateCell(cell);
    }
}

/// <summary>Opens the editor of a column's cell when it becomes current (<see cref="TwTextColumn.EditOnFocus"/>).</summary>
internal sealed class TwEditOnFocus
{
    private readonly DataGridViewColumn _column;
    private DataGridView? _grid;

    public TwEditOnFocus(DataGridViewColumn column) => _column = column;

    public bool Enabled { get; set; } = true;

    public void Attach(DataGridView? grid)
    {
        if (_grid == grid) return;
        if (_grid != null) _grid.CellEnter -= OnCellEnter;
        _grid = grid;
        if (_grid != null) _grid.CellEnter += OnCellEnter;
    }

    private void OnCellEnter(object? sender, DataGridViewCellEventArgs e)
    {
        var grid = _grid;
        if (!Enabled || grid is null || e.ColumnIndex != _column.Index || e.RowIndex < 0) return;
        // CellEnter içinde düzenlemeye geçilemez; hücre değişimi bitince açılır.
        TwInputCell.Post(grid, () =>
        {
            if (grid.IsDisposed || grid.IsCurrentCellInEditMode || grid.CurrentCell is not DataGridViewCell cell) return;
            if (cell.ColumnIndex != _column.Index || cell.RowIndex != e.RowIndex || cell.ReadOnly || !grid.ContainsFocus) return;
            grid.BeginEdit(true);
        });
    }
}

/// <summary>Drawing shared by <see cref="TwTextCell"/> and <see cref="TwSelectCell"/>: the input box inside the cell.</summary>
internal static class TwInputCell
{
    /// <summary>Space between the cell's edges and the box (96 DPI pixels; leaves room for the focus ring).</summary>
    public const int Inset = 4;

    /// <summary>Left/right space inside the box before the text.</summary>
    public const int TextInset = 8;

    /// <summary>Width of the arrow area of a select cell.</summary>
    public const int ChevronWidth = 20;

    /// <summary>Runs <paramref name="action"/> after the current message, if the control has a handle.</summary>
    public static void Post(Control control, Action action)
    {
        if (control.IsHandleCreated && !control.IsDisposed) control.BeginInvoke(action);
    }

    internal readonly struct InputColors
    {
        public InputColors(Color back, Color fore, Color border, Color placeholder, Color? ring, int ringWidth, int radius)
        {
            Back = back;
            Fore = fore;
            Border = border;
            Placeholder = placeholder;
            Ring = ring;
            RingWidth = ringWidth;
            Radius = radius;
        }

        public Color Back { get; }
        public Color Fore { get; }
        public Color Border { get; }
        public Color Placeholder { get; }
        public Color? Ring { get; }
        public int RingWidth { get; }
        public int Radius { get; }
    }

    public static InputColors Colors(DataGridView? grid, string classes, bool focused, bool invalid)
    {
        var parsed = TwTheme.Current.Parse("input " + classes);
        var state = TwGridState.State(grid) | (focused ? TwState.Focus : TwState.None) | (invalid ? TwState.Invalid : TwState.None);
        var style = parsed.Resolve(state);
        var placeholder = parsed.ResolvePart("placeholder", state);
        bool dark = TwGridState.IsDark(grid);
        var back = style.BackColor ?? (dark ? ColorTranslator.FromHtml("#111827") : Color.White);
        var fore = style.ForeColor ?? (dark ? ColorTranslator.FromHtml("#f3f4f6") : ColorTranslator.FromHtml("#111827"));
        var border = style.BorderColor ?? (dark ? ColorTranslator.FromHtml("#4b5563") : ColorTranslator.FromHtml("#d1d5db"));
        var hint = placeholder.ForeColor ?? Color.FromArgb(150, fore);
        return new InputColors(back, fore, border, hint, style.RingColor, style.RingWidth ?? 0, style.Radius ?? 6);
    }

    public static string? Error(DataGridViewCell cell, int rowIndex, string? own, object? value, Func<object?, string?>? validate)
    {
        var grid = cell.DataGridView;
        if (grid is null || rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;
        if (!string.IsNullOrEmpty(own)) return own;
        if (validate is null || grid.Rows.SharedRow(rowIndex).IsNewRow) return null;
        var text = validate(value is DBNull ? null : value);
        return string.IsNullOrEmpty(text) ? null : text;
    }

    public static Size Grow(Size size, DataGridView? grid, int extra)
    {
        float s = TwCellPainting.Scale(grid);
        return new Size(size.Width + (int)Math.Round((Inset + TextInset + extra) * 2 * s), size.Height + (int)Math.Round((Inset + 4) * 2 * s));
    }

    /// <summary>The box in a cell: inset from the edges, at most a text line plus padding tall, centered.</summary>
    public static Rectangle BoxBounds(Rectangle cellBounds, float s)
    {
        int inset = (int)Math.Round(Inset * s);
        var box = Rectangle.Inflate(cellBounds, -inset, -inset);
        box.Width -= 1; // hücrenin sağ ve alt ızgara çizgisi
        box.Height -= 1;
        return box.Width < 4 || box.Height < 4 ? Rectangle.Empty : box;
    }

    /// <summary>Where the editor goes: inside the box's border, away from its rounded corners.</summary>
    public static Rectangle EditorBounds(DataGridView grid, Rectangle cellBounds, string classes, bool chevron)
    {
        float s = TwCellPainting.Scale(grid);
        var box = BoxBounds(cellBounds, s);
        var colors = Colors(grid, classes, true, false);
        int deflate = (int)Math.Ceiling(s) + (int)Math.Ceiling(colors.Radius * s * 0.3f);
        var inner = Rectangle.Inflate(box, -deflate, -deflate);
        if (chevron) inner.Width -= (int)Math.Round(ChevronWidth * s);
        return inner;
    }

    public static void Paint(Graphics g, DataGridViewCell cell, Rectangle cellBounds, int rowIndex, DataGridViewCellStyle cellStyle,
        string? text, string placeholder, string classes, bool invalid, bool chevron, bool hideTextWhenEmpty)
    {
        var grid = cell.DataGridView!;
        float s = TwCellPainting.Scale(grid);
        var box = BoxBounds(cellBounds, s);
        if (box.IsEmpty) return;
        bool focused = grid.CurrentCellAddress == new Point(cell.ColumnIndex, rowIndex) && grid.ContainsFocus;
        var colors = Colors(grid, classes, focused, invalid);
        float radius = colors.Radius * s;
        var state = g.Save();
        try
        {
            g.IntersectClip(cellBounds);
            if (colors.Ring is Color ring && colors.RingWidth > 0 && ring.A > 0)
            {
                float width = colors.RingWidth * s;
                var outer = Rectangle.Inflate(box, (int)Math.Ceiling(width / 2), (int)Math.Ceiling(width / 2));
                TwPainter.StrokeRounded(g, outer, radius + width / 2, width, ring);
            }
            TwPainter.FillRounded(g, box, radius, colors.Back);
            TwPainter.StrokeRounded(g, box, radius, Math.Max(1, s), colors.Border);

            int pad = (int)Math.Round(TextInset * s);
            int arrow = chevron ? (int)Math.Round(ChevronWidth * s) : 0;
            bool rtl = grid.RightToLeft == RightToLeft.Yes;
            var textBounds = Rectangle.FromLTRB(box.Left + pad + (rtl ? arrow : 0), box.Top, box.Right - pad - (rtl ? 0 : arrow), box.Bottom);
            var font = cellStyle.Font ?? grid.Font;
            bool empty = string.IsNullOrEmpty(text);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
                        | (cellStyle.Alignment is DataGridViewContentAlignment.MiddleRight or DataGridViewContentAlignment.TopRight or DataGridViewContentAlignment.BottomRight
                            ? TextFormatFlags.Right
                            : cellStyle.Alignment is DataGridViewContentAlignment.MiddleCenter or DataGridViewContentAlignment.TopCenter or DataGridViewContentAlignment.BottomCenter
                                ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left)
                        | (rtl ? TextFormatFlags.RightToLeft : 0);
            if (!empty) TextRenderer.DrawText(g, text, font, textBounds, colors.Fore, flags);
            else if (!hideTextWhenEmpty && placeholder.Length > 0) TextRenderer.DrawText(g, placeholder, font, textBounds, colors.Placeholder, flags);

            if (chevron)
            {
                int em = Math.Max(8, (int)Math.Round(font.Height * 0.9f));
                var area = rtl ? new Rectangle(box.Left + pad / 2, box.Top, arrow, box.Height) : new Rectangle(box.Right - pad / 2 - arrow, box.Top, arrow, box.Height);
                TwIconRenderer.DrawIcon(g, "chevron-down", em, area, colors.Placeholder);
            }
        }
        finally
        {
            g.Restore(state);
        }
    }

    public static TwMenuColors MenuColors(DataGridView grid) => TwGridState.IsDark(grid)
        ? new TwMenuColors
        {
            Back = ColorTranslator.FromHtml("#1f2937"),
            Fore = ColorTranslator.FromHtml("#e5e7eb"),
            MenuBack = ColorTranslator.FromHtml("#1f2937"),
            MenuBorder = ColorTranslator.FromHtml("#374151"),
            ItemHoverBack = ColorTranslator.FromHtml("#374151"),
            ItemHoverFore = Color.White,
            Separator = ColorTranslator.FromHtml("#374151"),
            Accent = ColorTranslator.FromHtml("#60a5fa"),
            InputBack = ColorTranslator.FromHtml("#111827"),
        }
        : new TwMenuColors();
}
