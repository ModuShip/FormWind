using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>A <see cref="TwTabControl"/> page. The tab title is <see cref="Text"/>; the content is styled like a normal <see cref="TwPanel"/>.</summary>
[ToolboxItem(false)]
[DesignTimeVisible(false)]
public class TwTabPage : TwPanel
{
    public TwTabPage()
    {
        Visible = false;
    }

    public TwTabPage(string text) : this()
    {
        Text = text;
    }

#pragma warning disable CS8765 // Control.Text .NET 8'de [AllowNull] ile işaretli.
    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text
    {
        get => base.Text;
        set => base.Text = value;
    }
#pragma warning restore CS8765

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        (Parent as TwTabControl)?.SyncTabs();
    }
}

/// <summary>
/// Tab control with pages (instead of TabControl): a <see cref="TwTabStrip"/> on top and the selected <see cref="TwTabPage"/> below.
/// The strip is styled through <see cref="Strip"/> (<c>provider.SetClasses(tabs.Strip, "...")</c>), pages with their own classes.
/// Ctrl+Tab / Ctrl+Shift+Tab and Ctrl+PageDown / Ctrl+PageUp move between pages.
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(SelectedIndexChanged))]
[Description("Stylable tab control with pages.")]
[Designer("FormWind.Design.TwTabControlDesigner, FormWind")]
public class TwTabControl : Control
{
    private readonly TwTabStrip _strip;
    private int _stripGap = 12;
    private bool _syncing;

    public TwTabControl()
    {
        SetStyle(ControlStyles.ContainerControl | ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
        _strip = new TwTabStrip { AutoSize = true };
        _strip.SelectedIndexChanged += (_, _) => ShowSelected();
        Controls.Add(_strip);
    }

    public event EventHandler? SelectedIndexChanged;

    /// <summary>The tab strip; its style (<see cref="TwTabStrip.Variant"/>) and classes are set here.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwTabStrip Strip => _strip;

    [Category("Appearance"), DefaultValue(TwTabVariant.Underline)]
    public TwTabVariant Variant
    {
        get => _strip.Variant;
        set => _strip.Variant = value;
    }

    /// <summary>Space between the strip and the page (96 DPI pixels).</summary>
    [Category("Layout"), DefaultValue(12)]
    public int StripGap
    {
        get => _stripGap;
        set
        {
            _stripGap = Math.Max(0, value);
            PerformLayout();
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<TwTabPage> TabPages => Controls.OfType<TwTabPage>().ToList();

    [Category("Behavior"), DefaultValue(0)]
    public int SelectedIndex
    {
        get => _strip.SelectedIndex;
        set => _strip.SelectedIndex = value;
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwTabPage? SelectedTab
    {
        get
        {
            var pages = TabPages;
            return SelectedIndex >= 0 && SelectedIndex < pages.Count ? pages[SelectedIndex] : null;
        }
        set
        {
            int index = value is null ? -1 : TabPages.ToList().IndexOf(value);
            if (index >= 0) SelectedIndex = index;
        }
    }

    /// <summary>Adds a new page with the given title.</summary>
    public TwTabPage AddPage(string text)
    {
        var page = new TwTabPage(text);
        Controls.Add(page);
        return page;
    }

    /// <summary>The page's position in client coordinates.</summary>
    [Browsable(false)]
    public Rectangle PageBounds
    {
        get
        {
            var display = DisplayRectangle;
            int top = _strip.Bottom + (int)Math.Round(_stripGap * TwDpi.DeviceDpi(this) / 96f);
            return Rectangle.FromLTRB(display.Left, Math.Min(top, display.Bottom), display.Right, display.Bottom);
        }
    }

    internal void SyncTabs()
    {
        if (_syncing) return;
        _syncing = true;
        try
        {
            int selected = _strip.SelectedIndex;
            _strip.Tabs.Clear();
            _strip.Tabs.AddRange(TabPages.Select(p => p.Text));
            _strip.Invalidate();
            if (_strip.Tabs.Count > 0) _strip.SelectedIndex = Math.Max(0, Math.Min(selected, _strip.Tabs.Count - 1));
            _strip.PerformLayout();
            PerformLayout();
            ShowSelected();
        }
        finally
        {
            _syncing = false;
        }
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (e.Control is TwTabPage) SyncTabs();
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        if (e.Control is TwTabPage) SyncTabs();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        var display = DisplayRectangle;
        var size = _strip.GetPreferredSize(new Size(display.Width, 0));
        _strip.Bounds = new Rectangle(display.Left, display.Top, Math.Min(display.Width, size.Width), size.Height);
        var bounds = PageBounds;
        foreach (var page in TabPages) page.Bounds = bounds;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        int count = _strip.Tabs.Count;
        if (count > 1)
        {
            int step = keyData switch
            {
                Keys.Control | Keys.Tab or Keys.Control | Keys.PageDown => 1,
                Keys.Control | Keys.Shift | Keys.Tab or Keys.Control | Keys.PageUp => -1,
                _ => 0,
            };
            if (step != 0)
            {
                SelectedIndex = (SelectedIndex + step + count) % count;
                return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void ShowSelected()
    {
        var pages = TabPages;
        var selected = SelectedIndex >= 0 && SelectedIndex < pages.Count ? pages[SelectedIndex] : null;
        bool hadFocus = pages.Any(p => p.ContainsFocus);
        SuspendLayout();
        foreach (var page in pages)
            if (page != selected) page.Visible = false;
        if (selected != null)
        {
            selected.Bounds = PageBounds;
            selected.Visible = true;
            if (hadFocus) selected.SelectNextControl(null, true, true, true, false);
        }
        ResumeLayout(false);
        if (!_syncing) SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
    }
}
