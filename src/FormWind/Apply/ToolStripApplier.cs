using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Menu and toolbar colors. Colors left empty come from the default light theme.</summary>
internal sealed class TwMenuColors
{
    public Color Back { get; set; } = Color.White;
    public Color Fore { get; set; } = ColorTranslator.FromHtml("#374151");          // gray-700
    public Color MenuBack { get; set; } = Color.White;
    public Color MenuBorder { get; set; } = ColorTranslator.FromHtml("#e5e7eb");    // gray-200
    public Color ItemHoverBack { get; set; } = ColorTranslator.FromHtml("#f3f4f6"); // gray-100
    public Color ItemHoverFore { get; set; } = ColorTranslator.FromHtml("#111827"); // gray-900
    public Color Separator { get; set; } = ColorTranslator.FromHtml("#e5e7eb");
    public Color Accent { get; set; } = ColorTranslator.FromHtml("#2563eb");        // blue-600
    /// <summary>Background of text boxes and combo boxes hosted in a strip (<see cref="ToolStripTextBox"/>, <see cref="ToolStripComboBox"/>).</summary>
    public Color InputBack { get; set; } = Color.White;
    public Color? BottomBorder { get; set; }
    public float ItemRadius { get; set; } = 4;
}

/// <summary>ToolStrip renderer that paints with theme colors: flat surfaces, rounded hover, thin borders.</summary>
internal class TwToolStripRenderer : ToolStripRenderer
{
    public TwToolStripRenderer(TwMenuColors colors) => Colors = colors ?? throw new ArgumentNullException(nameof(colors));

    public TwMenuColors Colors { get; }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        var back = e.ToolStrip is ToolStripDropDown ? Colors.MenuBack : Colors.Back;
        using var brush = new SolidBrush(back);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is ToolStripDropDown)
        {
            using var pen = new Pen(Colors.MenuBorder);
            var r = e.ToolStrip.ClientRectangle;
            e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
        }
        else if (Colors.BottomBorder is Color bottom)
        {
            using var pen = new Pen(bottom);
            var r = e.ToolStrip.ClientRectangle;
            e.Graphics.DrawLine(pen, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
        }
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        // Görüntü kenar boşluğu ayrı renklendirilmez; menü arka planıyla aynı kalır.
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) => PaintItemBackground(e);

    protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e) => PaintItemBackground(e);

    protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e) => PaintItemBackground(e);

    protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e) => PaintItemBackground(e);

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = !e.Item.Enabled ? Color.FromArgb(128, Colors.Fore)
            : IsHighlighted(e.Item) ? Colors.ItemHoverFore
            : Colors.Fore;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item is { Enabled: false } ? Color.FromArgb(128, Colors.Fore) : Colors.Fore;
        base.OnRenderArrow(e);
    }

    // Büyütülmüş MDI çocuğunun menüye eklenen küçült/geri yükle/kapat düğmeleri siyah sistem glifleridir; koyu şeritte
    // görünmezdi. Glifler şeridin yazı rengine boyanır (saydamlık korunur).
    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Image is null || e.Item?.GetType().Name != "ControlBoxMenuItem")
        {
            base.OnRenderItemImage(e);
            return;
        }
        var color = !e.Item.Enabled ? Color.FromArgb(128, Colors.Fore) : IsHighlighted(e.Item) ? Colors.ItemHoverFore : Colors.Fore;
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix(new[]
        {
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, color.A / 255f, 0 },
            new float[] { color.R / 255f, color.G / 255f, color.B / 255f, 0, 1 },
        }));
        e.Graphics.DrawImage(e.Image, e.ImageRectangle, 0, 0, e.Image.Width, e.Image.Height, GraphicsUnit.Pixel, attributes);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(Colors.Separator);
        var r = e.Item.ContentRectangle;
        if (e.Vertical) e.Graphics.DrawLine(pen, r.Width / 2, r.Top + 4, r.Width / 2, r.Bottom - 4);
        else e.Graphics.DrawLine(pen, r.Left + 4, r.Height / 2, r.Right - 4, r.Height / 2);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var r = e.ImageRectangle;
        if (r.Width <= 0) return;
        TwToggleDrawing.DrawCheck(e.Graphics, r, Colors.Accent, Math.Max(1.5f, r.Width / 8f));
    }

    private void PaintItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        bool checkedButton = item is ToolStripButton { Checked: true };
        if (!IsHighlighted(item) && !checkedButton) return;

        var bounds = new Rectangle(Point.Empty, item.Size);
        if (item.Owner is ToolStripDropDown) bounds.Inflate(-4, -1);
        else bounds.Inflate(-1, -1);

        var state = e.Graphics.Save();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        TwPainter.FillRounded(e.Graphics, bounds, Colors.ItemRadius, Colors.ItemHoverBack);
        e.Graphics.Restore(state);
    }

    private static bool IsHighlighted(ToolStripItem item) => item.Enabled && (item.Selected || item.Pressed);
}

/// <summary>
/// Styles MenuStrip, ToolStrip, StatusStrip and ContextMenuStrip. Base classes apply to the strip itself,
/// <c>menu:</c> to drop-down menus and <c>item:</c> to items:
/// <c>bg-white text-gray-700 border-b menu:bg-white menu:border-gray-200 item:hover:bg-gray-100 item:rounded-md divide-gray-200</c>.
/// </summary>
internal class ToolStripApplier : WinFormsApplier
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ToolStripDropDown, object> SharedDropDowns =
        new System.Runtime.CompilerServices.ConditionalWeakTable<ToolStripDropDown, object>();

    // Class'lar kaldırılınca geri yüklenecek özgün renderer (uygulamanın kendi renderer'ı ya da yöneticininki).
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ToolStrip, OriginalRenderer> Originals =
        new System.Runtime.CompilerServices.ConditionalWeakTable<ToolStrip, OriginalRenderer>();

    private sealed class OriginalRenderer
    {
        public OriginalRenderer(ToolStrip strip)
        {
            Mode = strip.RenderMode;
            Renderer = strip.RenderMode == ToolStripRenderMode.Custom ? strip.Renderer : null;
        }

        public ToolStripRenderMode Mode { get; }
        public ToolStripRenderer? Renderer { get; }
    }

    public override bool CanApply(Control control) => control is ToolStrip;

    public override void Apply(Control control, TwStyle style, TwApplyContext context)
    {
        base.Apply(control, style, context);

        var strip = (ToolStrip)control;
        // Kendi class'ları olan menü artık sahibinin renderer'ını almaz.
        if (strip is ToolStripDropDown ownDropDown) SharedDropDowns.Remove(ownDropDown);
        var original = Originals.GetValue(strip, s => new OriginalRenderer(s));
        if (context.Parsed.Variants.Count == 0)
        {
            Restore(strip, original);
            TwHostedColors.Update(strip, null);
            return;
        }
        var colors = BuildColors(style, context);
        // Renderer değişimi şeridi yeniden yerleştirir; renkler aynıysa mevcut renderer korunur.
        if (!(strip.Renderer is TwToolStripRenderer current && SameColors(current.Colors, colors)))
        {
            strip.RenderMode = ToolStripRenderMode.ManagerRenderMode; // Renderer atanınca Custom'a geçer
            strip.Renderer = new TwToolStripRenderer(colors);
        }
        ShareWithDropDowns(strip.Items, strip.Renderer);
        TwHostedColors.Update(strip, colors);
    }

    private static void Restore(ToolStrip strip, OriginalRenderer original)
    {
        if (!(strip.Renderer is TwToolStripRenderer)) return;
        var ours = strip.Renderer;
        if (original.Renderer != null) strip.Renderer = original.Renderer;
        else strip.RenderMode = original.Mode;
        // Bu şeritten paylaşılan alt menüler de özgün renderer'larına döner.
        RestoreDropDowns(strip.Items, ours);
    }

    private static void RestoreDropDowns(ToolStripItemCollection items, ToolStripRenderer ours)
    {
        foreach (ToolStripItem item in items)
        {
            if (item is not ToolStripDropDownItem { HasDropDown: true } owner) continue;
            var dropDown = owner.DropDown;
            if (dropDown.Renderer != ours || !SharedDropDowns.TryGetValue(dropDown, out _)) continue;
            SharedDropDowns.Remove(dropDown);
            dropDown.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            RestoreDropDowns(dropDown.Items, ours);
        }
    }

    // Kendiliğinden oluşan alt menüler sahibinin renderer'ını devralır, öğeye elle atanmış bir ContextMenuStrip ise
    // devralmaz ve açık temada kalırdı. Uygulamanın kendi renderer'ını atamadığı (ya da daha önce buradan paylaşılmış)
    // menülere şeridin renderer'ı verilir; kendi class'ları ya da özel renderer'ı olanlara dokunulmaz.
    private static void ShareWithDropDowns(ToolStripItemCollection items, ToolStripRenderer renderer)
    {
        foreach (ToolStripItem item in items)
        {
            if (item is not ToolStripDropDownItem { HasDropDown: true } owner) continue;
            var dropDown = owner.DropDown;
            if (dropDown.Renderer != renderer
                && (dropDown.RenderMode == ToolStripRenderMode.ManagerRenderMode || SharedDropDowns.TryGetValue(dropDown, out _)))
            {
                dropDown.Renderer = renderer;
                SharedDropDowns.GetValue(dropDown, _ => new object());
            }
            if (dropDown.Renderer == renderer) ShareWithDropDowns(dropDown.Items, renderer);
        }
    }

    private static Color Mix(Color back, Color fore, float amount) => Color.FromArgb(
        (int)Math.Round(back.R + (fore.R - back.R) * amount),
        (int)Math.Round(back.G + (fore.G - back.G) * amount),
        (int)Math.Round(back.B + (fore.B - back.B) * amount));

    private static bool SameColors(TwMenuColors a, TwMenuColors b) =>
        a.Back.ToArgb() == b.Back.ToArgb()
        && a.Fore.ToArgb() == b.Fore.ToArgb()
        && a.MenuBack.ToArgb() == b.MenuBack.ToArgb()
        && a.MenuBorder.ToArgb() == b.MenuBorder.ToArgb()
        && a.ItemHoverBack.ToArgb() == b.ItemHoverBack.ToArgb()
        && a.ItemHoverFore.ToArgb() == b.ItemHoverFore.ToArgb()
        && a.Separator.ToArgb() == b.Separator.ToArgb()
        && a.Accent.ToArgb() == b.Accent.ToArgb()
        && a.InputBack.ToArgb() == b.InputBack.ToArgb()
        && a.BottomBorder?.ToArgb() == b.BottomBorder?.ToArgb()
        && a.ItemRadius == b.ItemRadius;

    /// <summary>Builds the strip/menu colors from the base classes and the <c>menu:</c> and <c>item:</c> parts.</summary>
    public static TwMenuColors BuildColors(TwStyle style, TwApplyContext context)
    {
        var state = context.State & ~(TwState.Hover | TwState.Active);
        var parsed = context.Parsed;
        var menu = parsed.ResolvePart("menu", state);
        var item = parsed.ResolvePart("item", state);
        var itemHover = parsed.ResolvePart("item", state | TwState.Hover);

        var colors = new TwMenuColors();
        if (style.BackColor is Color back) colors.Back = back;
        if (style.ForeColor is Color fore) colors.Fore = fore;
        colors.MenuBack = menu.BackColor ?? style.BackColor ?? colors.MenuBack;
        if (menu.BorderColor is Color menuBorder) colors.MenuBorder = menuBorder;
        if (item.ForeColor is Color itemFore) colors.Fore = itemFore;
        // Varsayılan hover/ayırıcı/kenarlık renkleri açık şerit içindir; koyu şeritte (açık yazı) beyaz hover kutusunda
        // yazı kaybolurdu. Verilmemişlerse şeridin zemin ve yazı renginden türetilir.
        bool darkStrip = style.BackColor is Color stripBack && stripBack.GetBrightness() < 0.5f;
        if (darkStrip)
        {
            colors.ItemHoverBack = Mix(colors.Back, colors.Fore, 0.12f);
            colors.MenuBorder = Mix(colors.MenuBack, colors.Fore, 0.2f);
            colors.Separator = Mix(colors.Back, colors.Fore, 0.2f);
            colors.InputBack = Mix(colors.Back, colors.Fore, 0.08f);
        }
        if (itemHover.BackColor is Color hoverBack) colors.ItemHoverBack = hoverBack;
        colors.ItemHoverFore = itemHover.ForeColor ?? colors.Fore;
        if (darkStrip && menu.BorderColor is Color darkMenuBorder) colors.MenuBorder = darkMenuBorder;
        if (style.DivideColor is Color divide) colors.Separator = divide;
        if (style.AccentColor is Color accent) colors.Accent = accent;
        if (item.Radius is int radius) colors.ItemRadius = radius * context.Scale;
        if ((style.BorderSides.Bottom ?? style.BorderWidth ?? 0) > 0)
            colors.BottomBorder = style.BorderColor ?? context.Theme.DefaultBorderColor;
        return colors;
    }
}

/// <summary>
/// Text boxes and combo boxes hosted in a strip (<see cref="ToolStripTextBox"/>, <see cref="ToolStripComboBox"/>) are not
/// painted by the strip's renderer and keep the native control's own colors; on a dark strip they showed up as white
/// boxes (for the combo box frame and arrow button see <see cref="TwHostedComboPainter"/>). They get the strip's text color
/// and the <see cref="TwMenuColors.InputBack"/> background, as do those in sub-menus sharing the renderer. Colors the
/// application assigned to these controls are left alone; removing the classes restores the original colors.
/// </summary>
internal static class TwHostedColors
{
    private sealed class State
    {
        public bool AppOwned;
        public Color? Back;
        public Color? Fore;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, State> States =
        new System.Runtime.CompilerServices.ConditionalWeakTable<Control, State>();

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ToolStrip, object> Watched =
        new System.Runtime.CompilerServices.ConditionalWeakTable<ToolStrip, object>();

    /// <param name="strip">The strip.</param>
    /// <param name="colors">The strip's colors; <c>null</c> = classes removed, the original colors are restored.</param>
    public static void Update(ToolStrip strip, TwMenuColors? colors)
    {
        if (colors != null && !Watched.TryGetValue(strip, out _))
        {
            Watched.Add(strip, new object());
            // Sonradan eklenen öğeler de renk alır.
            strip.ItemAdded += (_, _) =>
            {
                if (strip.Renderer is TwToolStripRenderer renderer) Visit(strip.Items, renderer, renderer.Colors);
            };
        }
        Visit(strip.Items, strip.Renderer, colors);
    }

    private static void Visit(ToolStripItemCollection items, ToolStripRenderer renderer, TwMenuColors? colors)
    {
        foreach (ToolStripItem item in items)
        {
            if (item is ToolStripComboBox || item is ToolStripTextBox)
            {
                var control = ((ToolStripControlHost)item).Control;
                if (colors != null) Apply(control, colors.InputBack, colors.Fore);
                else Restore(control);
            }
            else if (item is ToolStripDropDownItem { HasDropDown: true } owner && (colors is null || owner.DropDown.Renderer == renderer))
            {
                Visit(owner.DropDown.Items, renderer, colors);
            }
        }
    }

    private static void Apply(Control control, Color back, Color fore)
    {
        if (!States.TryGetValue(control, out var state))
        {
            state = new State
            {
                AppOwned = TwOriginal.IsExplicit(control, nameof(Control.BackColor)) || TwOriginal.IsExplicit(control, nameof(Control.ForeColor)),
            };
            States.Add(control, state);
        }
        // Bizden sonra değiştirilen renk uygulamanındır.
        if (state.Back is Color setBack && control.BackColor.ToArgb() != setBack.ToArgb()
            || state.Fore is Color setFore && control.ForeColor.ToArgb() != setFore.ToArgb())
            state.AppOwned = true;
        if (state.AppOwned) return;

        if (control.BackColor.ToArgb() != back.ToArgb()) control.BackColor = back;
        if (control.ForeColor.ToArgb() != fore.ToArgb()) control.ForeColor = fore;
        state.Back = back;
        state.Fore = fore;
        if (control is ComboBox combo) TwHostedComboPainter.Attach(combo);
    }

    private static void Restore(Control control)
    {
        if (!States.TryGetValue(control, out var state) || state.AppOwned || state.Back is null) return;
        if (control.BackColor.ToArgb() == state.Back.Value.ToArgb()) control.ResetBackColor();
        if (control.ForeColor.ToArgb() == state.Fore!.Value.ToArgb()) control.ResetForeColor();
        state.Back = null;
        state.Fore = null;
        if (control is ComboBox combo) TwHostedComboPainter.Detach(combo);
    }
}

/// <summary>
/// Styles a Form's title bar (Windows 10 20H1+: dark title bar; Windows 11: caption, text and border colors).
/// The title bar turns dark automatically in dark mode; colors are given with <c>caption:bg-*</c>, <c>caption:text-*</c>,
/// <c>caption:border-*</c>. Silently ignored on older Windows versions.
/// </summary>
internal class FormApplier : WinFormsApplier
{
    public override bool CanApply(Control control) => control is Form;

    public override void Apply(Control control, TwStyle style, TwApplyContext context)
    {
        base.Apply(control, style, context);
        if (context.DesignMode) return;

        var form = (Form)control;
        TwMdiBackground.Update(form, style.BackColor.HasValue);
        var caption = context.Parsed.ResolvePart("caption", context.State & ~(TwState.Hover | TwState.Active));
        TwTitleBar.For(form).Update(
            (context.State & TwState.Dark) != 0,
            caption.BackColor,
            caption.ForeColor,
            caption.BorderColor,
            style.Backdrop);
    }
}

/// <summary>
/// The area behind an MDI parent's child windows (<see cref="MdiClient"/>) does not take the form's background and stays
/// <see cref="SystemColors.AppWorkspace"/>; in dark mode it showed up as a light gray area. The form's <c>bg-*</c>
/// color is given to this area too and followed as the form background changes (transitions included). A color the
/// application assigned to the MdiClient is left alone; the original color is restored when the classes release the background.
/// </summary>
internal sealed class TwMdiBackground
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, TwMdiBackground> States =
        new System.Runtime.CompilerServices.ConditionalWeakTable<Form, TwMdiBackground>();

    private readonly Form _form;
    private MdiClient? _client;
    private Color _original;
    private Color? _set;
    private bool _active;
    private bool _appOwned;

    private TwMdiBackground(Form form)
    {
        _form = form;
        form.BackColorChanged += (_, _) => Sync();
        // IsMdiContainer class'lardan sonra da açılabilir; MdiClient o zaman eklenir.
        form.ControlAdded += (_, e) => { if (e.Control is MdiClient) Sync(); };
    }

    public static void Update(Form form, bool hasBackground)
    {
        if (!hasBackground && !States.TryGetValue(form, out _)) return;
        var state = States.GetValue(form, f => new TwMdiBackground(f));
        state._active = hasBackground;
        state.Sync();
    }

    private void Sync()
    {
        var client = _form.IsMdiContainer ? FindClient(_form) : null;
        if (client != _client)
        {
            _client = client;
            _set = null;
            _appOwned = false;
            if (client != null) _original = client.BackColor;
        }
        if (client is null || _appOwned) return;

        // Uygulamanın atadığı renk (ilk görüldüğünde sistem renginden farklı ya da bizden sonra değiştirilmiş) korunur.
        _appOwned = _set is Color set ? client.BackColor.ToArgb() != set.ToArgb()
            : _original.ToArgb() != SystemColors.AppWorkspace.ToArgb();
        if (_appOwned) return;
        if (_active)
        {
            var back = _form.BackColor;
            if (back.A < 255) back = Color.FromArgb(255, back); // MdiClient saydam zemini desteklemez
            client.BackColor = back;
            _set = back;
        }
        else if (_set != null)
        {
            client.BackColor = _original;
            _set = null;
        }
    }

    private static MdiClient? FindClient(Form form)
    {
        foreach (Control child in form.Controls)
            if (child is MdiClient client) return client;
        return null;
    }
}
