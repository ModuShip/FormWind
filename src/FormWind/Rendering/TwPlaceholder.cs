using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Hint text of a plain <see cref="TextBox"/> in the color of its <c>placeholder:text-*</c> class, as <see cref="TwTextBox"/>
/// draws it. A <see cref="TextBox"/> styled with <c>input</c> is a single window (its frame is drawn on the parent), where a
/// <see cref="TwTextBox"/> is a panel with a text box inside; with this hint it needs no <see cref="TwTextBox"/> for a
/// colored placeholder. On .NET 8 and later the box's own <c>PlaceholderText</c> is used when no text is set here (Windows
/// draws it in a fixed gray, which this replaces); on .NET Framework the text is set here.
/// <code>
/// TwPlaceholder.Set(txtSearch, "Kayıt ara…");
/// tw.SetClasses(txtSearch, "input placeholder:text-gray-500");
/// </code>
/// </summary>
public static class TwPlaceholder
{
    private static readonly Color DefaultColor = ColorTranslator.FromHtml("#9ca3af"); // gray-400, as TwTextBox
    private static readonly ConditionalWeakTable<TextBox, Painter> Painters = new ConditionalWeakTable<TextBox, Painter>();
    // Sınıftan gelen renk, ipucu sonradan verilse de kullanılsın diye kutudan ayrı tutulur.
    private static readonly ConditionalWeakTable<TextBox, StrongBox<Color?>> Colors = new ConditionalWeakTable<TextBox, StrongBox<Color?>>();

    /// <summary>Sets the hint shown while <paramref name="box"/> is empty; <c>null</c> or empty removes it.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> is <c>null</c>.</exception>
    public static void Set(TextBox box, string? text)
    {
        if (box is null) throw new ArgumentNullException(nameof(box));
        Painters.GetValue(box, b => new Painter(b)).Text = string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>The hint set with <see cref="Set"/> (the box's own <c>PlaceholderText</c> on .NET 8 and later when none was set); empty when none.</summary>
    public static string Get(TextBox box)
    {
        if (box is null) throw new ArgumentNullException(nameof(box));
        return Painters.TryGetValue(box, out var painter) ? painter.ShownText ?? "" : NativeText(box) ?? "";
    }

    /// <summary>The color the hint is drawn in (<c>placeholder:text-*</c>); <c>null</c> = gray-400.</summary>
    internal static void SetColor(TextBox box, Color? color)
    {
        Colors.GetValue(box, _ => new StrongBox<Color?>()).Value = color;
        // Çizici yalnızca ipucu olan kutuya bağlanır: input sınıflı her kutuya pencere alt sınıfı eklenmez.
        if (Painters.TryGetValue(box, out var painter)) box.Invalidate();
        else if (NativeText(box) != null) Painters.GetValue(box, b => new Painter(b));
    }

    internal static Color? ColorOf(TextBox box) => Colors.TryGetValue(box, out var color) ? color.Value : null;

    private static string? NativeText(TextBox box)
    {
#if NET
        return string.IsNullOrEmpty(box.PlaceholderText) ? null : box.PlaceholderText;
#else
        return null;
#endif
    }

    // Kutunun penceresine bağlanır ve her boyamanın ardından boşsa ipucunu çizer (EM_SETCUEBANNER'ın rengi değişmez).
    private sealed class Painter : NativeWindow
    {
        private const int WmPaint = 0x000F, WmPrint = 0x0317, WmPrintClient = 0x0318;
        private readonly TextBox _box;
        private string? _text;

        public Painter(TextBox box)
        {
            _box = box;
            box.HandleCreated += (_, _) => AssignHandle(box.Handle);
            box.HandleDestroyed += (_, _) => ReleaseHandle();
            box.TextChanged += (_, _) =>
            {
                if (box.TextLength <= 1) box.Invalidate();
            };
            if (box.IsHandleCreated) AssignHandle(box.Handle);
        }

        public string? Text
        {
            get => _text;
            set
            {
                _text = value;
                _box.Invalidate();
            }
        }

        public string? ShownText => _text ?? NativeText(_box);

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Önce mesaj türü: TextLength WM_GETTEXTLENGTH gönderir ve buraya geri döner.
            if (m.Msg != WmPaint && m.Msg != WmPrint && m.Msg != WmPrintClient) return;
            var text = ShownText;
            if (text is null || _box.TextLength > 0) return;
            if (m.Msg == WmPaint)
            {
                using var graphics = Graphics.FromHwnd(Handle);
                Draw(graphics, text);
            }
            else if (m.WParam != IntPtr.Zero)
            {
                using var graphics = Graphics.FromHdc(m.WParam);
                Draw(graphics, text);
            }
        }

        private void Draw(Graphics graphics, string text)
        {
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
                        | (_box.Multiline ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine)
                        | (_box.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right
                           : _box.TextAlign == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left);
            if (_box.RightToLeft == RightToLeft.Yes) flags |= TextFormatFlags.RightToLeft;
            // Yerli kutu metni küçük bir kenar boşluğuyla çizer; ipucu aynı yere hizalanır (yerli ipucunun üstü örtülür).
            var bounds = new Rectangle(1, 0, Math.Max(0, _box.ClientSize.Width - 2), _box.ClientSize.Height);
            TextRenderer.DrawText(graphics, text, _box.Font, bounds, ColorOf(_box) ?? DefaultColor, _box.BackColor, flags);
        }
    }
}
