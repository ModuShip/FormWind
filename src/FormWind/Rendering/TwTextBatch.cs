using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Draws many texts with <see cref="TextRenderer"/> on one device context. Every <c>TextRenderer.DrawText(Graphics, ...)</c>
/// call gets the device context of the <see cref="Graphics"/> again and copies its clip and transform; on a
/// <see cref="Graphics"/> of a bitmap (<c>Graphics.FromImage</c>, <c>DrawToBitmap</c>, off-screen pictures) it also copies
/// the bitmap into a new GDI context and back, which made a card with a dozen labels cost several milliseconds. A batch takes
/// the device context once. As with <see cref="TextRenderer"/>, the clip and translation of the <see cref="Graphics"/> (taken
/// when the batch starts) apply to a text drawn with <see cref="TextFormatFlags.PreserveGraphicsClipping"/> and
/// <see cref="TextFormatFlags.PreserveGraphicsTranslateTransform"/>:
/// <code>
/// using (var text = new TwTextBatch(e.Graphics))
/// {
///     foreach (var row in rows) text.DrawText(row.Title, Font, row.Bounds, ForeColor);
/// }
/// // e.Graphics can be drawn on again here
/// </code>
/// While the batch is open the <see cref="Graphics"/> must not be drawn on (GDI+ and GDI would draw out of order); draw the
/// shapes first and the texts in a batch after them. A <see cref="Graphics"/> with a rotation or scale is drawn on directly,
/// as without a batch. Use the batch on the thread that owns the <see cref="Graphics"/>, and dispose it.
/// </summary>
public sealed class TwTextBatch : IDeviceContext
{
    private const int RgnAnd = 1;

    private readonly Graphics _graphics;
    private readonly IntPtr _hdc;
    private readonly IntPtr _clip;
    private readonly Point _offset;
    private bool _disposed;

    /// <summary>Starts a batch on <paramref name="graphics"/> (see <see cref="TwTextBatch"/>).</summary>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> is <c>null</c>.</exception>
    public TwTextBatch(Graphics graphics)
    {
        _graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        using var transform = graphics.Transform;
        var elements = transform.Elements;
        // Yalnızca öteleme GDI'ya taşınır; döndürme/ölçek varsa metinler Graphics üzerinden çizilir.
        if (elements[0] != 1f || elements[1] != 0f || elements[2] != 0f || elements[3] != 1f) return;
        _offset = new Point((int)Math.Round(elements[4]), (int)Math.Round(elements[5]));

        using (var region = graphics.Clip)
        {
            if (!region.IsInfinite(graphics))
            {
                // Graphics.Clip dünya koordinatında; GDI aygıt koordinatı ister.
                region.Translate(_offset.X, _offset.Y);
                graphics.ResetTransform();
                try { _clip = region.GetHrgn(graphics); }
                finally { graphics.Transform = transform; }
            }
        }
        try
        {
            _hdc = graphics.GetHdc();
        }
        catch
        {
            if (_clip != IntPtr.Zero) DeleteObject(_clip);
            throw;
        }
    }

    /// <summary>Whether the texts go to the shared device context (false: the <see cref="Graphics"/> has a rotation or scale).</summary>
    public bool IsBatched => _hdc != IntPtr.Zero;

    /// <summary>Draws <paramref name="text"/> as <c>TextRenderer.DrawText</c> would on the <see cref="Graphics"/>.</summary>
    public void DrawText(string? text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
    {
        ThrowIfDisposed();
        if (!IsBatched)
        {
            TextRenderer.DrawText(_graphics, text, font, bounds, color, flags);
            return;
        }
        if ((flags & TextFormatFlags.PreserveGraphicsTranslateTransform) != 0) bounds.Offset(_offset);
        bool clipped = (flags & TextFormatFlags.PreserveGraphicsClipping) != 0 && _clip != IntPtr.Zero;
        int saved = clipped ? SaveDC(_hdc) : 0;
        try
        {
            if (clipped) ExtSelectClipRgn(_hdc, _clip, RgnAnd);
            TextRenderer.DrawText(this, text, font, bounds, color, flags & ~(TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform));
        }
        finally
        {
            if (clipped) RestoreDC(_hdc, saved);
        }
    }

    /// <summary>Draws <paramref name="text"/> with the default flags (left, vertically centered, as <see cref="TextRenderer"/>).</summary>
    public void DrawText(string? text, Font font, Rectangle bounds, Color color) => DrawText(text, font, bounds, color, TextFormatFlags.Default);

    /// <summary>Measures <paramref name="text"/> on the batch's device context.</summary>
    public Size MeasureText(string? text, Font font, Size proposedSize, TextFormatFlags flags)
    {
        ThrowIfDisposed();
        return IsBatched
            ? TextRenderer.MeasureText(this, text, font, proposedSize, flags)
            : TextRenderer.MeasureText(_graphics, text, font, proposedSize, flags);
    }

    /// <summary>Gives the device context back to the <see cref="Graphics"/>, which can be drawn on again.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_clip != IntPtr.Zero) DeleteObject(_clip);
        if (IsBatched) _graphics.ReleaseHdc(_hdc);
    }

    IntPtr IDeviceContext.GetHdc()
    {
        ThrowIfDisposed();
        return _hdc;
    }

    // TextRenderer gives the context back after each call; it stays with the batch until the batch is disposed.
    void IDeviceContext.ReleaseHdc() { }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(TwTextBatch));
    }

    [DllImport("gdi32.dll")]
    private static extern int SaveDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool RestoreDC(IntPtr hdc, int savedDc);

    [DllImport("gdi32.dll")]
    private static extern int ExtSelectClipRgn(IntPtr hdc, IntPtr region, int mode);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}
