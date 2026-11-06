using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Layout;

namespace FormWind;

/// <summary>
/// The header of a page or a card: a large <see cref="Title"/>, a muted <see cref="Subtitle"/> under it and, on the right, the
/// controls added to it (chips, a summary label, buttons, a pager) in the order they were added. When the window is too
/// narrow for the title and the controls side by side, the controls move under the title and wrap. The height follows the
/// content (<c>AutoSize</c>). Style the title with the <c>label:</c> part and the subtitle with <c>hint:</c>
/// (<c>label:text-2xl label:font-semibold hint:text-sm</c>); the <c>page-header</c> component is applied automatically.
/// <code>
/// var header = new TwPageHeader { Title = "Kayıt Arşivi", Subtitle = "Tümü · 739 kayıt", Dock = DockStyle.Top };
/// header.Controls.Add(new TwChip { Text = "Son 30 gün" });
/// </code>
/// </summary>
[ToolboxItem(true)]
[Description("Page header: title, subtitle and controls on the right that move under the title when narrow.")]
[DefaultProperty(nameof(Title))]
public class TwPageHeader : TwPanel
{
    private int _gap = 8;
    private bool _arranging;
    private Color _titleColor = Color.Empty;
    private Font? _titleFont;
    private Font? _ownTitleFont;

    public TwPageHeader()
    {
        Heading = new TwHeading { AutoSize = false, Name = "heading" };
        Controls.Add(Heading);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    protected override Size DefaultSize => new Size(640, 56);

    /// <summary>The control that draws the title and subtitle.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwHeading Heading { get; }

    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string Title
    {
        get => Heading.Text;
        set
        {
            Heading.Text = value ?? "";
            PerformLayout();
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Muted line under the title; empty = none.</summary>
    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string Subtitle
    {
        get => Heading.Subtitle;
        set
        {
            Heading.Subtitle = value;
            PerformLayout();
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Space between the title and the controls, and between the controls (96 DPI pixels); set with <c>gap-*</c>.</summary>
    [Category("Layout"), DefaultValue(8)]
    public int Gap
    {
        get => _gap;
        set
        {
            value = Math.Max(0, value);
            if (_gap == value) return;
            _gap = value;
            PerformLayout();
        }
    }

    /// <summary>The controls on the right: every child except <see cref="Heading"/>, in the order they were added.</summary>
    [Browsable(false)]
    public IReadOnlyList<Control> Items => Controls.Cast<Control>().Where(c => c != Heading).ToList();

    /// <summary>Whether the controls are currently under the title (the header is too narrow for one row).</summary>
    [Browsable(false)]
    public bool IsStacked { get; private set; }

#pragma warning disable CS8765 // Control.Text .NET 8'de [AllowNull] ile işaretli.
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text
    {
        get => Title;
        set => Title = value;
    }
#pragma warning restore CS8765

    internal void SetPartStyles(Color? title, Font? titleFont, Color? subtitle, Font? subtitleFont)
    {
        _titleColor = title ?? Color.Empty;
        _titleFont = titleFont;
        Heading.SetPartStyles(subtitle, subtitleFont);
        ApplyTitleStyle();
        PerformLayout();
    }

    private void ApplyTitleStyle()
    {
        Heading.ForeColor = _titleColor.IsEmpty ? ForeColor : _titleColor;
        // Uygulama Heading.Font'u kendisi verdiyse korunur.
        if (_ownTitleFont is null || ReferenceEquals(Heading.Font, _ownTitleFont))
        {
            Heading.Font = _titleFont ?? DefaultTitleFont();
            _ownTitleFont = Heading.Font;
        }
    }

    // Class verilmemiş başlık: satırın fontundan büyük ve kalın.
    private Font DefaultTitleFont() => TwFonts.Get(Font.FontFamily.Name, Font.SizeInPoints * DefaultTitleScale, FontStyle.Bold);

    // Kart başlığı sayfa başlığından küçüktür.
    internal float DefaultTitleScale { get; set; } = 1.5f;

    internal event EventHandler? ContentChanged;

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ApplyTitleStyle();
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        ApplyTitleStyle();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTitleStyle();
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is { } item && item != Heading)
        {
            item.VisibleChanged += OnItemChanged;
            item.SizeChanged += OnItemChanged;
            ContentChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        if (e.Control is { } item)
        {
            item.VisibleChanged -= OnItemChanged;
            item.SizeChanged -= OnItemChanged;
        }
        PerformLayout();
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnItemChanged(object? sender, EventArgs e)
    {
        if (!_arranging) PerformLayout();
    }

    private int Scale(int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(this) / 96f);

    private static Size ItemSize(Control item) => item.AutoSize ? item.GetPreferredSize(Size.Empty) : item.Size;

    // Başlık solda, öğeler sağda tek satırda; sığmazsa öğeler başlığın altında soldan sağa sarar.
    private (Rectangle Heading, List<(Control Item, Rectangle Bounds)> Items, int Height, bool Stacked) Measure(int width)
    {
        var border = BorderInsets();
        int left = border.Left + Padding.Left, right = width - border.Right - Padding.Right, top = border.Top + Padding.Top;
        int inner = Math.Max(0, right - left);
        int gap = Scale(_gap);
        var items = Items.Where(i => i.Visible || !IsHandleCreated).Select(i => (Item: i, Size: ItemSize(i))).ToList();
        var heading = Heading.GetPreferredSize(Size.Empty);
        int itemsWidth = items.Sum(i => i.Size.Width) + Math.Max(0, items.Count - 1) * gap;
        bool stacked = items.Count > 0 && heading.Width + gap + itemsWidth > inner;
        var placed = new List<(Control, Rectangle)>();
        int height;
        if (!stacked)
        {
            int rowHeight = Math.Max(heading.Height, items.Count == 0 ? 0 : items.Max(i => i.Size.Height));
            int x = right - itemsWidth;
            foreach (var (item, size) in items)
            {
                placed.Add((item, new Rectangle(x, top + (rowHeight - size.Height) / 2, size.Width, size.Height)));
                x += size.Width + gap;
            }
            var headingBounds = new Rectangle(left, top + (rowHeight - heading.Height) / 2,
                Math.Max(0, (items.Count > 0 ? right - itemsWidth - gap : right) - left), heading.Height);
            height = rowHeight;
            return (headingBounds, placed, height + Padding.Vertical + border.Vertical, false);
        }

        var headingRect = new Rectangle(left, top, inner, heading.Height);
        int y = headingRect.Bottom + gap, lineX = left, lineHeight = 0;
        foreach (var (item, size) in items)
        {
            if (lineX > left && lineX + size.Width > right)
            {
                y += lineHeight + gap;
                lineX = left;
                lineHeight = 0;
            }
            placed.Add((item, new Rectangle(lineX, y, Math.Min(size.Width, inner), size.Height)));
            lineX += size.Width + gap;
            lineHeight = Math.Max(lineHeight, size.Height);
        }
        height = y + lineHeight - top;
        return (headingRect, placed, height + Padding.Vertical + border.Vertical, true);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int width = proposedSize.Width > 0 && proposedSize.Width < int.MaxValue / 2 ? proposedSize.Width : Width;
        return new Size(width, Measure(width).Height);
    }

    public override LayoutEngine LayoutEngine => HeaderLayout.Instance;

    private void Arrange()
    {
        if (_arranging) return;
        _arranging = true;
        try
        {
            var (heading, items, height, stacked) = Measure(Width);
            bool rtl = TwRtl.NeedsMirroring(this);
            Rectangle Mirror(Rectangle r) => rtl ? new Rectangle(Width - r.Right, r.Y, r.Width, r.Height) : r;
            IsStacked = stacked;
            Heading.Bounds = Mirror(heading);
            foreach (var (item, bounds) in items) item.Bounds = Mirror(bounds);
            if (AutoSize && Height != height && Dock is not (DockStyle.Fill or DockStyle.Left or DockStyle.Right)) Height = height;
        }
        finally
        {
            _arranging = false;
        }
    }

    private sealed class HeaderLayout : LayoutEngine
    {
        public static readonly HeaderLayout Instance = new HeaderLayout();

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            if (container is TwPageHeader header) header.Arrange();
            return container is Control { AutoSize: true };
        }
    }
}
