using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Where a <see cref="TwPopover"/> opens relative to its anchor: the side, then the alignment along that side.</summary>
public enum TwPopoverPlacement
{
    /// <summary>Below, aligned with the anchor's start edge (left; right when right-to-left).</summary>
    BottomStart,
    Bottom,
    BottomEnd,
    TopStart,
    Top,
    TopEnd,
    /// <summary>To the right, aligned with the anchor's top edge.</summary>
    RightStart,
    Right,
    RightEnd,
    LeftStart,
    Left,
    LeftEnd,
}

/// <summary>
/// A small panel that opens next to a control (drop-down calendar, status menu, filter panel) and closes when the user
/// clicks outside it or presses Esc. The <see cref="Content"/> control is shown as is and provides the look; the popover
/// places it on the <see cref="Placement"/> side of the anchor, flips to the opposite side when it does not fit on the screen
/// and keeps it inside the screen's working area.
/// <code>
/// var popover = new TwPopover { Content = statusPanel, Placement = TwPopoverPlacement.BottomEnd };
/// btnStatus.Click += (_, _) => popover.Toggle(btnStatus);
/// </code>
/// The popover owns the content: disposing the popover disposes it.
/// </summary>
[ToolboxItem(false)]
[DefaultEvent(nameof(Closed))]
public class TwPopover : Component
{
    private ToolStripDropDown? _dropDown;
    private ToolStripControlHost? _host;
    private Control? _content;
    private Control? _anchor;
    private Rectangle? _anchorBounds;
    private bool _restoreFocus;
    private bool _closedByAnchorClick;
    private Control? _anchorMouseUpOwner;
    private MouseEventHandler? _anchorMouseUp;

    public TwPopover()
    {
    }

    public TwPopover(IContainer container) : this() => container.Add(this);

    /// <summary>Before the popover opens; set <see cref="CancelEventArgs.Cancel"/> to keep it closed.</summary>
    public event CancelEventHandler? Opening;

    /// <summary>After the popover opens and the content has focus.</summary>
    public event EventHandler? Opened;

    /// <summary>After the popover closes; <see cref="ToolStripDropDownClosedEventArgs.CloseReason"/> tells why.</summary>
    public event ToolStripDropDownClosedEventHandler? Closed;

    /// <summary>The control shown in the popover. Its <see cref="Control.Size"/> (or preferred size when it auto-sizes) is used.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Content
    {
        get => _content;
        set
        {
            if (value == _content) return;
            Close();
            if (_host is not null)
            {
                // Değiştirilen içerik atılmaz (uygulama yeniden kullanabilir); host'u atmak onu da atardı.
                _dropDown!.Items.Remove(_host);
                _host = null;
                _content?.Parent?.Controls.Remove(_content);
            }
            _content = value;
        }
    }

    [DefaultValue(TwPopoverPlacement.BottomStart)]
    public TwPopoverPlacement Placement { get; set; } = TwPopoverPlacement.BottomStart;

    /// <summary>Gap between the anchor and the popover in logical pixels (scaled with the anchor's DPI).</summary>
    [DefaultValue(4)]
    public int Offset { get; set; } = 4;

    /// <summary>Focuses the content (or its first focusable child) when the popover opens.</summary>
    [DefaultValue(true)]
    public bool FocusContent { get; set; } = true;

    /// <summary>
    /// Returns the focus to the anchor when the popover is closed with Esc or <see cref="Close"/> while the focus is in it.
    /// Clicking elsewhere leaves the focus where the user clicked.
    /// </summary>
    [DefaultValue(true)]
    public bool RestoreFocus { get; set; } = true;

    [Browsable(false)]
    public bool IsOpen => _dropDown?.Visible == true;

    /// <summary>The control the popover is (or was last) opened from.</summary>
    [Browsable(false)]
    public Control? Anchor => _anchor;

    /// <summary>The underlying drop-down window (created on the first <see cref="Show(Control)"/>).</summary>
    internal ToolStripDropDown? DropDown => _dropDown;

    /// <summary>Opens the popover next to <paramref name="anchor"/>.</summary>
    public void Show(Control anchor) => Show(anchor, null);

    /// <summary>
    /// Opens the popover next to an area of <paramref name="anchor"/> in its client coordinates (a grid cell, a chart bar);
    /// <c>null</c> uses the whole control.
    /// </summary>
    public void Show(Control anchor, Rectangle? anchorBounds)
    {
        if (anchor is null) throw new ArgumentNullException(nameof(anchor));
        if (_content is null) throw new InvalidOperationException("TwPopover.Content is not set.");
        if (IsOpen && anchor != _anchor) Close();

        var opening = new CancelEventArgs();
        Opening?.Invoke(this, opening);
        if (opening.Cancel) return;

        _anchor = anchor;
        _anchorBounds = anchorBounds;
        var dropDown = EnsureDropDown();
        var size = _content.AutoSize ? _content.GetPreferredSize(Size.Empty) : _content.Size;
        _content.Size = size;
        _host!.Size = size;

        var screenAnchor = anchor.RectangleToScreen(anchorBounds ?? anchor.ClientRectangle);
        var gap = (int)Math.Round(Offset * TwDpi.DeviceDpi(anchor) / 96f);
        var bounds = Place(screenAnchor, dropDown.GetPreferredSize(Size.Empty), Screen.FromRectangle(screenAnchor).WorkingArea,
            Placement, gap, anchor.RightToLeft == RightToLeft.Yes);
        // BelowRight: verilen nokta sol üst köşe; sağdan sola düzende de konumu Place belirler. Açılırken solarak belirir.
        if (TwMotion.Enabled) dropDown.Opacity = 0;
        dropDown.Show(anchor, anchor.PointToClient(bounds.Location), ToolStripDropDownDirection.BelowRight);
        FadeIn(dropDown);

        if (FocusContent) FocusFirst(_content);
        Opened?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Opens the popover, or closes it when it is open. A click on the anchor that just closed it does not reopen it.</summary>
    public void Toggle(Control anchor) => Toggle(anchor, null);

    /// <inheritdoc cref="Toggle(Control)"/>
    public void Toggle(Control anchor, Rectangle? anchorBounds)
    {
        if (IsOpen && anchor == _anchor) Close();
        else if (_closedByAnchorClick && anchor == _anchor) _closedByAnchorClick = false;
        else Show(anchor, anchorBounds);
    }

    public void Close()
    {
        if (IsOpen) _dropDown!.Close(ToolStripDropDownCloseReason.CloseCalled);
    }

    /// <summary>
    /// Bounds of a popover of <paramref name="size"/> next to <paramref name="anchor"/> (screen coordinates): on the
    /// placement side, or the opposite one when only that fits; then kept inside <paramref name="area"/>.
    /// </summary>
    internal static Rectangle Place(Rectangle anchor, Size size, Rectangle area, TwPopoverPlacement placement, int gap, bool rightToLeft)
    {
        var side = (int)placement / 3;   // 0 alt, 1 üst, 2 sağ, 3 sol
        var align = (int)placement % 3;  // 0 başlangıç, 1 orta, 2 son
        bool vertical = side < 2;
        if (vertical && rightToLeft && align != 1) align = 2 - align;

        int x, y;
        if (vertical)
        {
            int below = anchor.Bottom + gap, above = anchor.Top - gap - size.Height;
            bool fitsBelow = below + size.Height <= area.Bottom, fitsAbove = above >= area.Top;
            bool up = side == 1 ? fitsAbove || !fitsBelow && anchor.Top - area.Top > area.Bottom - anchor.Bottom
                                : !fitsBelow && (fitsAbove || anchor.Top - area.Top > area.Bottom - anchor.Bottom);
            y = up ? above : below;
            x = align switch
            {
                0 => anchor.Left,
                1 => anchor.Left + (anchor.Width - size.Width) / 2,
                _ => anchor.Right - size.Width,
            };
        }
        else
        {
            int right = anchor.Right + gap, left = anchor.Left - gap - size.Width;
            bool fitsRight = right + size.Width <= area.Right, fitsLeft = left >= area.Left;
            bool toLeft = side == 3 ? fitsLeft || !fitsRight && anchor.Left - area.Left > area.Right - anchor.Right
                                    : !fitsRight && (fitsLeft || anchor.Left - area.Left > area.Right - anchor.Right);
            x = toLeft ? left : right;
            y = align switch
            {
                0 => anchor.Top,
                1 => anchor.Top + (anchor.Height - size.Height) / 2,
                _ => anchor.Bottom - size.Height,
            };
        }

        // Ekrana sığmayan kenar kaydırılır; popover ekrandan büyükse sol/üst kenarı görünür kalır.
        x = Math.Max(area.Left, Math.Min(x, area.Right - size.Width));
        y = Math.Max(area.Top, Math.Min(y, area.Bottom - size.Height));
        return new Rectangle(new Point(x, y), size);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fadeIn?.Dispose();
            _fadeIn = null;
            _dropDown?.Dispose();
            _dropDown = null;
            _host = null;
            _content?.Dispose();
            _content = null;
            _anchor = null;
        }
        base.Dispose(disposing);
    }

    /// <summary>Fade-in time when the popover opens (ms).</summary>
    internal const int FadeDuration = 150;

    private TwTween? _fadeIn;

    private void FadeIn(ToolStripDropDown dropDown)
    {
        if (dropDown.Opacity >= 1) return;
        if (_fadeIn is null)
        {
            _fadeIn = new TwTween(dropDown);
            _fadeIn.Tick += (_, _) => { if (_dropDown != null) _dropDown.Opacity = _fadeIn.Progress; };
            _fadeIn.Completed += (_, _) => { if (_dropDown != null) _dropDown.Opacity = 1; };
        }
        _fadeIn.Start(FadeDuration);
        if (!_fadeIn.Running) dropDown.Opacity = 1;
    }

    private ToolStripDropDown EnsureDropDown()
    {
        if (_dropDown is null)
        {
            _dropDown = new ToolStripDropDown { Padding = Padding.Empty, AutoClose = true, DropShadowEnabled = true };
            _dropDown.Closing += OnDropDownClosing;
            _dropDown.Closed += OnDropDownClosed;
        }
        if (_host is null)
        {
            _host = new ToolStripControlHost(_content!) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false };
            _dropDown.Items.Add(_host);
        }
        return _dropDown;
    }

    private void OnDropDownClosing(object? sender, ToolStripDropDownClosingEventArgs e)
    {
        // Kapanırken odak içerikten çıkar; geri verilip verilmeyeceği şimdi belli olur.
        _restoreFocus = RestoreFocus && _content?.ContainsFocus == true
            && e.CloseReason is ToolStripDropDownCloseReason.Keyboard or ToolStripDropDownCloseReason.CloseCalled;
        // Popover hemen kapanır (IsOpen, Closed ve odak eskisi gibi); son görüntüsü solarak kaybolur.
        if (!e.Cancel && _dropDown is { Visible: true } dropDown)
        {
            _fadeIn?.Stop();
            TwFadeOut.Play(dropDown, null, FadeDuration, roundCorners: false);
        }
    }

    private void OnDropDownClosed(object? sender, ToolStripDropDownClosedEventArgs e)
    {
        var anchor = _anchor;
        if (e.CloseReason == ToolStripDropDownCloseReason.AppClicked && anchor is { IsHandleCreated: true }
            && anchor.RectangleToScreen(_anchorBounds ?? anchor.ClientRectangle).Contains(Control.MousePosition))
        {
            // Bu tıklama, popover'ı kapattıktan sonra çapaya da ulaşır; Toggle onu yeniden açmasın. Button.Click fare
            // bırakılınca geldiğinden işaret, bırakma mesajı işlendikten sonra silinir.
            _closedByAnchorClick = true;
            WatchAnchorMouseUp(anchor);
        }
        if (_restoreFocus && anchor is { CanFocus: true }) anchor.Focus();
        _restoreFocus = false;
        Closed?.Invoke(this, e);
    }

    private void WatchAnchorMouseUp(Control anchor)
    {
        if (_anchorMouseUp is not null) _anchorMouseUpOwner!.MouseUp -= _anchorMouseUp;
        _anchorMouseUpOwner = anchor;
        _anchorMouseUp = (_, _) =>
        {
            anchor.MouseUp -= _anchorMouseUp;
            _anchorMouseUp = null;
            _anchorMouseUpOwner = null;
            anchor.BeginInvoke((Action)(() => _closedByAnchorClick = false));
        };
        anchor.MouseUp += _anchorMouseUp;
    }

    private static void FocusFirst(Control content)
    {
        // Select/SelectNextControl, ContainerControl olmayan ToolStripDropDown içinde Windows odağını vermez; Focus verir.
        var target = content.CanSelect && content.Controls.Count == 0 ? content : null;
        for (var c = content.GetNextControl(content, true); target is null && c is not null; c = content.GetNextControl(c, true))
        {
            if (c.CanSelect && c.TabStop) target = c;
        }
        (target ?? content).Focus();
    }
}
