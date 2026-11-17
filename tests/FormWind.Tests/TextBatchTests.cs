using System;
using System.Drawing;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// <see cref="TwTextBatch"/> (0.1.9 report R5): texts drawn on one device context look the same as with
/// <c>TextRenderer.DrawText(Graphics, ...)</c>, keep the clip and translation of the Graphics, and fall back to drawing on
/// the Graphics when it is rotated or scaled.
/// </summary>
[Collection(UiCollection.Name)]
public class TextBatchTests
{
    private static readonly Font TextFont = new Font("Segoe UI", 9f);

    private const TextFormatFlags Preserve = TextFormatFlags.PreserveGraphicsClipping | TextFormatFlags.PreserveGraphicsTranslateTransform;

    private static Bitmap Draw(Action<Graphics> setup, Action<Graphics> draw)
    {
        var bitmap = new Bitmap(200, 80);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.White);
        setup(g);
        draw(g);
        return bitmap;
    }

    private static void AssertSame(Bitmap expected, Bitmap actual)
    {
        for (int y = 0; y < expected.Height; y++)
            for (int x = 0; x < expected.Width; x++)
                if (expected.GetPixel(x, y) != actual.GetPixel(x, y))
                    Assert.Fail($"pixel {x},{y}; expected ink {Ink(expected, 0, 28)} / {Ink(expected, 28, 80)}, actual {Ink(actual, 0, 28)} / {Ink(actual, 28, 80)}");
    }

    private static string Ink(Bitmap bitmap, int top, int bottom)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (int y = top; y < bottom; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).ToArgb() != Color.White.ToArgb())
                {
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
        return maxX < 0 ? "none" : $"x {minX}-{maxX} y {minY}-{maxY}";
    }

    private static bool HasInk(Bitmap bitmap, Rectangle area)
    {
        for (int y = area.Top; y < area.Bottom; y++)
            for (int x = area.Left; x < area.Right; x++)
                if (bitmap.GetPixel(x, y).ToArgb() != Color.White.ToArgb()) return true;
        return false;
    }

    [Theory]
    [InlineData(Preserve | TextFormatFlags.EndEllipsis)]
    [InlineData(TextFormatFlags.EndEllipsis)]
    public void A_text_drawn_in_a_batch_matches_a_text_drawn_on_the_graphics(TextFormatFlags flags) => Sta.Run(() =>
    {
        void Setup(Graphics g)
        {
            g.TranslateTransform(12, 6);
            g.SetClip(new Rectangle(0, 0, 150, 60));
        }
        var bounds = new Rectangle(4, 4, 170, 20);
        using var expected = Draw(Setup, g => TextRenderer.DrawText(g, "Duruş kaydı 1042", TextFont, bounds, Color.Black, flags));
        using var actual = Draw(Setup, g =>
        {
            using var batch = new TwTextBatch(g);
            Assert.True(batch.IsBatched);
            batch.DrawText("Duruş kaydı 1042", TextFont, bounds, Color.Black, flags);
        });
        AssertSame(expected, actual);
    });

    [Fact]
    public void Several_texts_in_a_batch_keep_the_clip_and_translation_they_ask_for() => Sta.Run(() =>
    {
        using var bitmap = Draw(g =>
        {
            g.TranslateTransform(12, 6);
            g.SetClip(new Rectangle(0, 0, 150, 60));
        }, g =>
        {
            using var batch = new TwTextBatch(g);
            batch.DrawText("Duruş kaydı 1042 uzun bir başlık", TextFont, new Rectangle(4, 4, 190, 20), Color.Black, Preserve);
            batch.DrawText("Hat 3", TextFont, new Rectangle(4, 30, 100, 20), Color.Red);
        });
        Assert.False(HasInk(bitmap, new Rectangle(162, 0, 38, 30)));   // clipped at 150 + 12
        // Without the flags the text is not translated: it starts left of the translation (12 px).
        Assert.True(HasInk(bitmap, new Rectangle(0, 28, 12, 20)), Ink(bitmap, 28, 80));
    });

    [Fact]
    public void A_scaled_graphics_is_drawn_on_directly() => Sta.Run(() =>
    {
        using var bitmap = Draw(g => g.ScaleTransform(2, 2), g =>
        {
            using var batch = new TwTextBatch(g);
            Assert.False(batch.IsBatched);
            batch.DrawText("Ölçek", TextFont, new Rectangle(0, 0, 80, 20), Color.Black);
        });
        Assert.True(HasInk(bitmap, new Rectangle(0, 0, 200, 80)));
    });

    [Fact]
    public void The_graphics_can_be_drawn_on_after_the_batch_and_the_batch_cannot() => Sta.Run(() =>
    {
        using var bitmap = new Bitmap(100, 40);
        using var g = Graphics.FromImage(bitmap);
        var batch = new TwTextBatch(g);
        Assert.Equal(TextRenderer.MeasureText("Ölçü", TextFont).Width, batch.MeasureText("Ölçü", TextFont, Size.Empty, TextFormatFlags.Default).Width);
        batch.Dispose();
        batch.Dispose();
        g.FillRectangle(Brushes.Black, 0, 0, 10, 10);
        Assert.Throws<ObjectDisposedException>(() => batch.DrawText("x", TextFont, new Rectangle(0, 0, 10, 10), Color.Black));
        Assert.Throws<ArgumentNullException>(() => new TwTextBatch(null!));
    });
}
