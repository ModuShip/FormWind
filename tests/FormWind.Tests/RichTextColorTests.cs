using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

// KeePass 2.61.1 denemesinde görüldü: giriş ayrıntıları (RichTextBox) RTF olarak yükleniyor; renk tablosu olmayan metin
// "otomatik" renk alıp koyu zeminde siyah çiziliyordu. Otomatik renkli parçalar kutunun yazı rengini alır, RTF'deki renkler,
// seçim ve geri alma geçmişi değişmez.
[Collection(UiCollection.Name)]
public class RichTextColorTests
{
    private const string Colored = @"{\rtf1\ansi{\colortbl;\red255\green0\blue0;}\cf1 red\cf0  plain text\par}";

    [Fact]
    public void Rtf_without_colors_takes_the_text_color() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var box = new RichTextBox { ReadOnly = true };
        form.Controls.Add(box);
        form.Show();
        provider.SetClasses(box, "input");
        var fore = ColorTranslator.ToWin32(box.ForeColor);

        using (var source = new RichTextBox { Text = "Kullanıcı adı: deneme" })
            box.Rtf = source.Rtf;
        Assert.Equal((false, fore), Format(box, 0));
        Assert.Equal((false, fore), Format(box, box.TextLength - 1));
        Assert.False(box.CanUndo);

        box.Rtf = Colored;
        Assert.Equal((false, ColorTranslator.ToWin32(Color.Red)), Format(box, 0));          // RTF'nin rengi kalır
        Assert.Equal((false, fore), Format(box, 5));                                          // \cf0: otomatik → yazı rengi
        Assert.Equal(0, box.SelectionStart);
        form.Close();
    });

    [Fact]
    public void Boxes_without_a_text_color_are_left_alone() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false };
        var box = new RichTextBox();
        form.Controls.Add(box);
        form.Show();
        provider.SetClasses(box, "input");
        provider.SetClasses(box, "");

        box.Rtf = Colored;
        Assert.True(Format(box, 5).Auto);
        form.Close();
    });

    private static (bool Auto, int Color) Format(RichTextBox box, int index)
    {
        var range = new CharRange { Min = index, Max = index + 1 };
        SendMessage(box.Handle, 0x0437 /* EM_EXSETSEL */, IntPtr.Zero, ref range);
        var format = new CharFormat { Size = Marshal.SizeOf<CharFormat>() };
        SendMessage(box.Handle, 0x043A /* EM_GETCHARFORMAT */, (IntPtr)1, ref format);
        box.Select(0, 0);
        bool auto = (format.Effects & 0x40000000) != 0;
        return (auto, auto ? 0 : format.TextColor);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CharRange
    {
        public int Min;
        public int Max;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CharFormat
    {
        public int Size;
        public uint Mask;
        public uint Effects;
        public int Height;
        public int Offset;
        public int TextColor;
        public byte CharacterSet;
        public byte PitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FaceName;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref CharRange lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref CharFormat lParam);
}
