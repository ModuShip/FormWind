using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Layout;

namespace FormWind;

/// <summary>
/// A horizontal strip of cards (the child controls, in <c>Controls</c> order) shown a page at a time: as many cards as fit at
/// <see cref="MinCardWidth"/> share the width equally, and the ‹ › buttons with a "1 / 3" indicator in the top right corner
/// turn the pages (also ← → while the strip has focus). With a single page the buttons are hidden. Styled like a
/// <see cref="TwPanel"/>; <see cref="PreviousButton"/>, <see cref="NextButton"/> and <see cref="PageLabel"/> take their own
/// classes.
/// <code>
/// var strip = new TwCardStrip { Dock = DockStyle.Top, Height = 160, MinCardWidth = 220 };
/// foreach (var line in lines) strip.Controls.Add(new TwStat { Title = line.Name, Value = line.Oee.ToString("P0") });
/// </code>
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(PageChanged))]
[Description("Paged horizontal strip of cards.")]
public class TwCardStrip : TwPanel
{
    private int _minCardWidth = 220;
    private int _gap = 12;
    private int _page;
    private bool _arranging;

    public TwCardStrip()
    {
        PreviousButton = new TwButton { IconName = "chevron-left", AutoSize = true, AccessibleName = TwTexts.Current.Previous, TabStop = false };
        NextButton = new TwButton { IconName = "chevron-right", AutoSize = true, AccessibleName = TwTexts.Current.Next, TabStop = false };
        PageLabel = new Label { AutoSize = true, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter, UseMnemonic = false };
        PreviousButton.Click += (_, _) => PageIndex--;
        NextButton.Click += (_, _) => PageIndex++;
        Controls.Add(PreviousButton);
        Controls.Add(PageLabel);
        Controls.Add(NextButton);
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    protected override Size DefaultSize => new Size(720, 160);

    /// <summary>Raised when <see cref="PageIndex"/> changes (also when fewer pages fit after a resize).</summary>
    public event EventHandler? PageChanged;

    /// <summary>The narrowest a card gets (96 DPI pixels); it decides how many cards fit on a page.</summary>
    [Category("Layout"), DefaultValue(220)]
    public int MinCardWidth
    {
        get => _minCardWidth;
        set
        {
            value = Math.Max(16, value);
            if (_minCardWidth == value) return;
            _minCardWidth = value;
            PerformLayout();
        }
    }

    /// <summary>Space between cards (96 DPI pixels).</summary>
    [Category("Layout"), DefaultValue(12)]
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

    /// <summary>The page shown (0-based); kept within <see cref="PageCount"/>.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PageIndex
    {
        get => _page;
        set
        {
            value = Math.Max(0, Math.Min(PageCount - 1, value));
            if (_page == value) return;
            // Sayfa değişimi eski görünümden yeniye solar (animasyonlar kapalıyken hemen).
            TwTransition.CrossFade(this, () =>
            {
                _page = value;
                Arrange();
            });
            PageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Number of pages for the current width.</summary>
    [Browsable(false)]
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Cards.Length / (double)CardsPerPage));

    /// <summary>Cards that fit on a page at <see cref="MinCardWidth"/>.</summary>
    [Browsable(false)]
    public int CardsPerPage
    {
        get
        {
            int width = Math.Max(1, DisplayRectangle.Width - Padding.Horizontal), gap = Scale(_gap);
            return Math.Max(1, (width + gap) / (Scale(_minCardWidth) + gap));
        }
    }

    /// <summary>The cards: the children other than the page buttons and indicator, in <c>Controls</c> order.</summary>
    [Browsable(false)]
    public Control[] Cards => Controls.Cast<Control>().Where(c => c != PreviousButton && c != NextButton && c != PageLabel).ToArray();

    [Browsable(false)]
    public TwButton PreviousButton { get; }

    [Browsable(false)]
    public TwButton NextButton { get; }

    /// <summary>The "1 / 3" indicator between the buttons.</summary>
    [Browsable(false)]
    public Label PageLabel { get; }

    /// <summary>Shows the page with <paramref name="card"/> on it.</summary>
    public void ShowCard(Control card)
    {
        int index = Array.IndexOf(Cards, card);
        if (index >= 0) PageIndex = index / CardsPerPage;
    }

    private int Scale(int pixels) => (int)Math.Round(pixels * TwDpi.DeviceDpi(this) / 96f);

    public override LayoutEngine LayoutEngine => StripLayout.Instance;

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        PerformLayout();
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        PerformLayout();
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left) PageIndex--;
        else if (e.KeyCode == Keys.Right) PageIndex++;
        else return;
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (CanFocus) Focus();
    }

    internal void Arrange()
    {
        if (_arranging) return;
        _arranging = true;
        try
        {
            var area = Rectangle.FromLTRB(DisplayRectangle.Left + Padding.Left, DisplayRectangle.Top + Padding.Top,
                DisplayRectangle.Right - Padding.Right, DisplayRectangle.Bottom - Padding.Bottom);
            var cards = Cards;
            int perPage = CardsPerPage, pages = PageCount;
            int page = Math.Min(_page, pages - 1);
            bool pageMoved = page != _page;
            _page = page;

            // Sayfa düğmeleri sağ üstte; tek sayfada gizlenir ve kartlar tüm yüksekliği alır.
            bool paged = pages > 1;
            int navHeight = 0;
            foreach (var part in new Control[] { PreviousButton, PageLabel, NextButton })
                if (part.Visible != paged) part.Visible = paged;
            if (paged)
            {
                PageLabel.Text = (page + 1) + " / " + pages;
                var prev = PreviousButton.GetPreferredSize(Size.Empty);
                var next = NextButton.GetPreferredSize(Size.Empty);
                var label = PageLabel.GetPreferredSize(Size.Empty);
                navHeight = Math.Max(prev.Height, Math.Max(next.Height, label.Height));
                int gap = Scale(4);
                bool rtl = TwRtl.NeedsMirroring(this);
                int x = area.Right;
                void Place(Control part, Size size)
                {
                    x -= size.Width;
                    int left = rtl ? area.Left + area.Right - x - size.Width : x;
                    part.SetBounds(left, area.Top + (navHeight - size.Height) / 2, size.Width, size.Height);
                    x -= gap;
                }
                Place(NextButton, next);
                Place(PageLabel, label);
                Place(PreviousButton, prev);
                PreviousButton.Enabled = page > 0;
                NextButton.Enabled = page < pages - 1;
                navHeight += Scale(8);
            }

            int cardGap = Scale(_gap);
            int top = area.Top + navHeight, height = Math.Max(1, area.Bottom - top);
            int shown = Math.Min(perPage, Math.Max(0, cards.Length - page * perPage));
            // Son sayfa da tam sayfa genişliğinde bölünür: kartların boyu sayfadan sayfaya değişmez.
            float width = (area.Width - (perPage - 1) * cardGap) / (float)perPage;
            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                int slot = i - page * perPage;
                bool visible = slot >= 0 && slot < shown;
                if (!visible)
                {
                    if (card.Visible) card.Visible = false;
                    continue;
                }
                int left = (int)Math.Round(area.Left + slot * (width + cardGap));
                int right = (int)Math.Round(area.Left + slot * (width + cardGap) + width);
                if (TwRtl.NeedsMirroring(this)) (left, right) = (area.Left + area.Right - right, area.Left + area.Right - left);
                card.SetBounds(left, top, right - left, height);
                if (!card.Visible) card.Visible = true;
            }
            if (pageMoved) PageChanged?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _arranging = false;
        }
    }

    private sealed class StripLayout : LayoutEngine
    {
        public static readonly StripLayout Instance = new StripLayout();

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            if (container is TwCardStrip strip) strip.Arrange();
            return false;
        }
    }
}
