using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// A card: a header with a <see cref="Title"/>, a muted <see cref="Description"/> and controls on its right
/// (<see cref="Header"/>: actions, a badge, a pager), and a body made of the card's other children. It is the control form of
/// the <c>card</c> component, which it gets automatically (border, radius, padding, shadow). The header is hidden while it
/// has no title, description or controls. Style the title with <c>label:</c> and the description with <c>hint:</c>
/// (<c>card label:text-base label:font-semibold hint:text-xs</c>).
/// <code>
/// var card = new TwCard { Title = "Duruş nedenleri", Description = "Son 7 gün", Dock = DockStyle.Fill };
/// card.Header.Controls.Add(new TwButton { Text = "Tümü" });
/// card.Controls.Add(new TwChart { Dock = DockStyle.Fill });
/// </code>
/// Body children are laid out by the panel as usual (Dock, Anchor, or <c>flex</c> / <c>grid</c> classes) under the header.
/// </summary>
[ToolboxItem(true)]
[Description("Card with a title, a muted description, header controls and a body.")]
[DefaultProperty(nameof(Title))]
public class TwCard : TwPanel
{
    private int _headerGap = 12;

    public TwCard()
    {
        Header = new TwPageHeader { Dock = DockStyle.Top, Name = "header", Padding = Padding.Empty, DefaultTitleScale = 1.15f, Visible = false };
        Header.ContentChanged += (_, _) => UpdateHeader();
        Controls.Add(Header);
        UpdateHeader();
    }

    protected override Size DefaultSize => new Size(320, 200);

    /// <summary>The header: title, description and the controls added to it, on the right.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwPageHeader Header { get; }

    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string Title
    {
        get => Header.Title;
        set => Header.Title = value;
    }

    /// <summary>Muted line under the title; empty = none.</summary>
    [Category("Appearance"), DefaultValue(""), Localizable(true)]
    public string Description
    {
        get => Header.Subtitle;
        set => Header.Subtitle = value;
    }

    /// <summary>Space between the header and the body (96 DPI pixels); default 12.</summary>
    [Category("Layout"), DefaultValue(12)]
    public int HeaderGap
    {
        get => _headerGap;
        set
        {
            value = Math.Max(0, value);
            if (_headerGap == value) return;
            _headerGap = value;
            UpdateHeader();
        }
    }

#pragma warning disable CS8765 // Control.Text .NET 8'de [AllowNull] ile işaretli.
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text
    {
        get => Title;
        set => Title = value;
    }
#pragma warning restore CS8765

    private void UpdateHeader()
    {
        bool show = Header.Title.Length > 0 || Header.Subtitle.Length > 0 || Header.Items.Count > 0;
        if (Header.Visible != show) Header.Visible = show;
        var padding = new Padding(0, 0, 0, (int)Math.Round(_headerGap * TwDpi.DeviceDpi(this) / 96f));
        if (Header.Padding != padding) Header.Padding = padding;
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        // Başlık en arkada kalır: Dock yerleşimi arkadan öne gittiği için önce o yerleşir ve üstte durur.
        if (e.Control != Header) Controls.SetChildIndex(Header, Controls.Count - 1);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        UpdateHeader();
    }

    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        UpdateHeader();
    }

    internal void SetPartStyles(Color? title, Font? titleFont, Color? description, Font? descriptionFont) =>
        Header.SetPartStyles(title, titleFont, description, descriptionFont);
}
