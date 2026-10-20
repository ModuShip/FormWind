using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// Windows'un devre dışıyken sistem renkleriyle çizdiği native kontroller (bkz. TwDisabledNative). KeePass'te veritabanı
/// açık değilken girdi listesi, Bulk Crap Uninstaller'da yükleme sırasında listeler ve kutular koyu temada açık kalıyordu.
/// Test süreci görsel stilsiz çalıştığından Windows'un bildirimleri doğrudan gönderilir.
/// </summary>
[Collection(UiCollection.Name)]
public class DisabledNativeTests
{
    private static readonly Color Back = ColorTranslator.FromHtml("#1f2937");
    private static readonly Color Fore = ColorTranslator.FromHtml("#f3f4f6");

    private static Form NewForm() =>
        new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), ShowInTaskbar = false, ClientSize = new Size(300, 200) };

    [Fact]
    public void Disabled_list_view_paints_its_background_and_items_in_the_class_colors() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = NewForm();
        var list = new ListView { View = View.Details, Dock = DockStyle.Fill, Columns = { "Ad" } };
        list.Items.Add("Bir");
        list.Items.Add("İki").Selected = true;
        form.Controls.Add(list);
        form.Show();
        provider.SetClasses(list, "bg-gray-800 text-gray-100");
        list.Enabled = false;
        Assert.True(TwDisabledNative.IsActive(list));

        using var bitmap = new Bitmap(50, 50);
        using var g = Graphics.FromImage(bitmap);
        var hdc = g.GetHdc();
        try
        {
            SendMessage(list.Handle, 0x0014 /* WM_ERASEBKGND */, hdc, IntPtr.Zero);
            var first = CustomDraw(list, hdc, 0x00010001 /* CDDS_ITEMPREPAINT */, 0);
            Assert.Equal(ColorTranslator.ToWin32(TwDisabledText.TextColor(list)), first.TextColor);
            Assert.Equal(ColorTranslator.ToWin32(Back), first.TextBackColor);
            Assert.Equal(0, first.ItemState & 0x11);                                          // seçim ve odak çizilmez
            var selected = CustomDraw(list, hdc, 0x00010001, 1);
            Assert.Equal(ColorTranslator.ToWin32(TwDisabledNative.SelectionShade(Back, Fore)), selected.TextBackColor);
        }
        finally
        {
            g.ReleaseHdc(hdc);
        }
        Assert.Equal(Back.ToArgb(), bitmap.GetPixel(10, 10).ToArgb());

        provider.SetClasses(list, "");                                                        // zemin yok: Windows çizer
        Assert.False(TwDisabledNative.IsActive(list));
        form.Close();
    });

    [Fact]
    public void Disabled_editable_combo_box_colors_its_edit_area() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { DarkMode = true };
        using var form = NewForm();
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Text = "Düzenlenebilir" };
        form.Controls.Add(combo);
        form.Show();
        provider.SetClasses(combo, "bg-gray-800 text-gray-100");
        combo.Enabled = false;

        using var bitmap = new Bitmap(10, 10);
        using var g = Graphics.FromImage(bitmap);
        var hdc = g.GetHdc();
        try
        {
            var brush = SendMessage(combo.Handle, 0x0138 /* WM_CTLCOLORSTATIC */, hdc, IntPtr.Zero);
            Assert.NotEqual(IntPtr.Zero, brush);
            Assert.Equal(ColorTranslator.ToWin32(Back), GetBkColor(hdc));
            Assert.Equal(ColorTranslator.ToWin32(TwDisabledText.TextColor(combo)), GetTextColor(hdc));
        }
        finally
        {
            g.ReleaseHdc(hdc);
        }
        form.Close();
    });

    // Temalı DropDownList kutusu BackColor'ı yok sayar; koyu zeminde (etkin ve devre dışı) FormWind çizer.
    [Fact]
    public void Dark_drop_down_lists_are_painted_by_formwind() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var form = NewForm();
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        form.Controls.Add(combo);
        provider.SetClasses(combo, "bg-white text-gray-900 dark:bg-gray-800 dark:text-gray-100");
        Assert.False(TwComboPainter.IsAttached(combo));                                       // açık zeminde tema çizer
        provider.DarkMode = true;
        Assert.True(TwComboPainter.IsAttached(combo));
        provider.DarkMode = false;
        Assert.False(TwComboPainter.IsAttached(combo));
    });

    private static NmListViewCustomDraw CustomDraw(ListView list, IntPtr hdc, int stage, int item)
    {
        var draw = new NmListViewCustomDraw
        {
            Header = new NmHdr { HwndFrom = list.Handle, Code = -12 /* NM_CUSTOMDRAW */ },
            DrawStage = stage, Hdc = hdc, Right = 50, Bottom = 20, ItemSpec = (UIntPtr)(uint)item, ItemState = 0x11,
        };
        var memory = Marshal.AllocHGlobal(Marshal.SizeOf<NmListViewCustomDraw>());
        try
        {
            Marshal.StructureToPtr(draw, memory, false);
            SendMessage(list.Handle, 0x204E /* WM_REFLECT + WM_NOTIFY */, IntPtr.Zero, memory);
            return Marshal.PtrToStructure<NmListViewCustomDraw>(memory);
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NmHdr
    {
        public IntPtr HwndFrom;
        public UIntPtr IdFrom;
        public int Code;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NmListViewCustomDraw
    {
        public NmHdr Header;
        public int DrawStage;
        public IntPtr Hdc;
        public int Left, Top, Right, Bottom;
        public UIntPtr ItemSpec;
        public int ItemState;
        public IntPtr ItemParam;
        public int TextColor;
        public int TextBackColor;
        public int SubItem;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("gdi32.dll")]
    private static extern int GetBkColor(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetTextColor(IntPtr hdc);
}
