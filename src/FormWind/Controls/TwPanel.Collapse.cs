using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace FormWind;

// Açılıp kapanma: Expanded değişince panelin yüksekliği kayarak sıfıra iner (sonra gizlenir) ya da açık yüksekliğine çıkar.
public partial class TwPanel : ITwTimed
{
    private bool _expanded = true;
    private TwTween? _collapse;
    private TwTimingClasses _timing;
    private int _collapseFrom, _collapseTo;
    private int _expandedHeight;
    private bool _collapseSuspended, _autoSizeWasOn;

    /// <summary>Default time (ms) of collapsing and expanding.</summary>
    internal const int CollapseDuration = 200;

    /// <summary>
    /// Whether the panel is open. Setting it to <c>false</c> slides the panel's height down to zero and then hides it;
    /// <c>true</c> shows it and slides it back to its height (its preferred height when <c>AutoSize</c>). Its children keep
    /// their places and are cut off meanwhile. Takes 200 ms (<c>duration-*</c>, <c>ease-*</c>; <c>transition-none</c> or
    /// animations off = at once). The height must be the panel's own: docked to the top or bottom, or not docked.
    /// </summary>
    [Category("Behavior"), DefaultValue(true), Description("Whether the panel is open; changes slide its height.")]
    public bool Expanded
    {
        get => _expanded;
        set
        {
            if (_expanded == value) return;
            _expanded = value;
            if (value) StartExpand();
            else StartCollapse();
            ExpandedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>When <see cref="Expanded"/> changes (at the start of the animation).</summary>
    public event EventHandler? ExpandedChanged;

    /// <summary>Opens the panel (<see cref="Expanded"/> = true).</summary>
    public void Expand() => Expanded = true;

    /// <summary>Closes the panel (<see cref="Expanded"/> = false).</summary>
    public void Collapse() => Expanded = false;

    /// <summary>Whether the panel's height is sliding now.</summary>
    [Browsable(false)]
    public bool IsCollapsing => _collapse is { Running: true };

    void ITwTimed.SetTiming(TwTimingClasses timing) => _timing = timing;

    private void StartCollapse()
    {
        // Açılış sürerken kapanırsa açık yükseklik açılışın hedefidir.
        _expandedHeight = IsCollapsing ? _collapseTo : Height;
        if (!Visible || !TwMotion.CanAnimate(this))
        {
            StopSlide();
            Visible = false;
            return;
        }
        Slide(Height, 0);
    }

    private void StartExpand()
    {
        int target = AutoSize || _autoSizeWasOn ? GetPreferredSize(new System.Drawing.Size(Width, 0)).Height
            : _expandedHeight > 0 ? _expandedHeight : Height;
        // Kapalı panel görünmez; animasyonun yapılıp yapılamayacağını ebeveyni belirler.
        var host = Parent ?? (Control)this;
        if (!TwMotion.CanAnimate(host) || Site?.DesignMode == true)
        {
            StopSlide();
            if (!AutoSize && target > 0) Height = target;
            Visible = true;
            return;
        }
        int from = Visible ? Height : 0;
        if (!Visible)
        {
            Height = 0;
            Visible = true;
        }
        Slide(from, target);
    }

    private void Slide(int from, int to)
    {
        _collapseFrom = from;
        _collapseTo = to;
        if (!_collapseSuspended)
        {
            // Çocuklar yerinde kalır, kesilir (yerleşim her karede yeniden çalışıp onları ezmez).
            SuspendLayout();
            _collapseSuspended = true;
        }
        if (AutoSize)
        {
            _autoSizeWasOn = true;
            AutoSize = false;
        }
        if (_collapse is null)
        {
            _collapse = new TwTween(this);
            _collapse.Tick += (_, _) => Height = (int)Math.Round(_collapseFrom + (_collapseTo - _collapseFrom) * _collapse.Progress);
            _collapse.Completed += (_, _) => FinishSlide();
            Disposed += (_, _) => _collapse.Dispose();
        }
        _collapse.Start(_timing.For(CollapseDuration));
        if (!_collapse.Running) FinishSlide();
    }

    private void FinishSlide()
    {
        if (_expanded) Height = _collapseTo;
        else
        {
            Visible = false;
            // Gizli panel açık yüksekliğini korur (tasarımcı ve yerleşim için); açılış sıfırdan başlar.
            Height = _expandedHeight;
        }
        StopSlide();
    }

    private void StopSlide()
    {
        _collapse?.Stop();
        if (_autoSizeWasOn)
        {
            _autoSizeWasOn = false;
            AutoSize = true;
        }
        if (_collapseSuspended)
        {
            _collapseSuspended = false;
            ResumeLayout(true);
        }
    }
}
