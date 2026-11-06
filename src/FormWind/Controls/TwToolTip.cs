using System.Diagnostics.CodeAnalysis;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Tooltip styled with classes (owner-drawn ToolTip). Follows the dark mode of <see cref="Provider"/> when set.
/// Default: <c>bg-gray-900 text-white text-xs px-2 py-1 dark:bg-gray-100 dark:text-gray-900</c>.
/// </summary>
[ToolboxItem(true)]
[Description("Class'larla stillenen ipucu kutusu.")]
public class TwToolTip : ToolTip
{
    public const string DefaultClasses = "bg-gray-900 text-white text-xs px-2 py-1 border-gray-700 dark:bg-gray-100 dark:text-gray-900 dark:border-gray-300";

    public TwToolTip()
    {
        Initialize();
    }

    public TwToolTip(IContainer container) : base(container)
    {
        Initialize();
    }

    [Category("FormWind"), DefaultValue(DefaultClasses)]
    public string Classes { get; set; } = DefaultClasses;

    /// <summary>The provider to take the dark mode state from (optional).</summary>
    [Category("FormWind"), DefaultValue(null)]
    public TailwindProvider? Provider { get; set; }

    /// <summary>The style resolved for the current theme state.</summary>
    [Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
    public TwStyle ResolveStyle() =>
        CurrentTheme.Parse(Classes).Resolve((Provider?.DarkMode ?? DarkMode) ? TwState.Dark : TwState.None);

    // Sağlayıcısı olmayan ipucu (kesilen etiketlerin ipucu) temayı ve koyu modu, onu kuran stil uygulamasından alır.
    internal TwTheme? Theme { get; set; }

    internal bool DarkMode { get; set; }

    private TwTheme CurrentTheme => Provider?.Theme ?? Theme ?? TwTheme.Current;

    /// <summary>Size of the tooltip box for a text (including padding).</summary>
    public Size MeasureTip(string text, int dpi = 96)
    {
        var style = ResolveStyle();
        var font = TwFonts.Create(style, SystemFonts.StatusFont ?? Control.DefaultFont, CurrentTheme);
        var padding = style.Padding.ApplyTo(Padding.Empty, dpi / 96f);
        var size = TextRenderer.MeasureText(text ?? "", font, Size.Empty, TextFormatFlags.NoPadding);
        return new Size(size.Width + padding.Horizontal + 2, size.Height + padding.Vertical + 2);
    }

    private void Initialize()
    {
        OwnerDraw = true;
        Popup += OnPopup;
        Draw += OnDraw;
    }

    private void OnPopup(object? sender, PopupEventArgs e)
    {
        var dpi = e.AssociatedControl is Control associated ? TwDpi.DeviceDpi(associated) : 96;
        e.ToolTipSize = MeasureTip(GetToolTip(e.AssociatedControl) ?? "", dpi);
    }

    private void OnDraw(object? sender, DrawToolTipEventArgs e)
    {
        var style = ResolveStyle();
        var font = TwFonts.Create(style, SystemFonts.StatusFont ?? Control.DefaultFont, CurrentTheme);

        using (var back = new SolidBrush(style.BackColor ?? SystemColors.Info))
            e.Graphics.FillRectangle(back, e.Bounds);
        using (var pen = new Pen(style.BorderColor ?? Color.FromArgb(60, 0, 0, 0)))
            e.Graphics.DrawRectangle(pen, e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);

        TextRenderer.DrawText(e.Graphics, e.ToolTipText, font, e.Bounds, style.ForeColor ?? SystemColors.InfoText,
            TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

/// <summary>
/// The full text of a label that <c>AutoEllipsis</c> cuts short ("…"), in a themed <see cref="TwToolTip"/>. It replaces
/// WinForms' own light tooltip (a label with a ToolTip of its own does not show that one) and shows only while the text is
/// actually cut.
/// </summary>
internal static class TwTruncationTip
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Label, TwToolTip> Tips =
        new System.Runtime.CompilerServices.ConditionalWeakTable<Label, TwToolTip>();

    /// <summary>The tooltip attached to the label (<c>null</c> if none).</summary>
    public static TwToolTip? Get(Label label) => Tips.TryGetValue(label, out var tip) ? tip : null;

    public static void Attach(Label label, TwTheme theme, bool dark)
    {
        if (!Tips.TryGetValue(label, out var tip))
        {
            tip = new TwToolTip { ShowAlways = true };
            Tips.Add(label, tip);
            tip.Popup += (_, e) =>
            {
                if (e.AssociatedControl is Label shown && !IsTruncated(shown)) e.Cancel = true;
            };
            label.TextChanged += OnTextChanged;
            label.Disposed += (_, _) =>
            {
                label.TextChanged -= OnTextChanged;
                if (Tips.TryGetValue(label, out var owned)) owned.Dispose();
                Tips.Remove(label);
            };
        }
        tip.Theme = theme;
        tip.DarkMode = dark;
        tip.SetToolTip(label, DisplayText(label));
    }

    public static void Detach(Label label)
    {
        if (!Tips.TryGetValue(label, out var tip)) return;
        label.TextChanged -= OnTextChanged;
        tip.SetToolTip(label, null);
        tip.Dispose();
        Tips.Remove(label);
    }

    private static void OnTextChanged(object? sender, EventArgs e)
    {
        if (sender is Label label && Tips.TryGetValue(label, out var tip)) tip.SetToolTip(label, DisplayText(label));
    }

    /// <summary>Whether the label's text does not fit its client area (what AutoEllipsis cuts).</summary>
    public static bool IsTruncated(Label label)
    {
        var text = DisplayText(label);
        if (text.Length == 0) return false;
        var inner = Size.Subtract(label.ClientSize, label.Padding.Size);
        if (inner.Width <= 0 || inner.Height <= 0) return true;
        var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
        var measured = TextRenderer.MeasureText(text, label.Font, new Size(inner.Width, int.MaxValue), flags);
        return measured.Width > inner.Width || measured.Height > inner.Height;
    }

    // Kısayol işareti açıksa & ipucunda görünmez (&& tek & olur).
    private static string DisplayText(Label label)
    {
        var text = label.Text ?? "";
        if (!label.UseMnemonic || text.IndexOf('&') < 0) return text;
        var result = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '&' && i + 1 < text.Length) i++;
            else if (text[i] == '&') continue;
            result.Append(text[i]);
        }
        return result.ToString();
    }
}
