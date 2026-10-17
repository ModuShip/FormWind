using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>Colors a form's title bar with DWM attributes; reapplies the values when the handle is recreated.</summary>
internal sealed class TwTitleBar
{
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaColorDefault = unchecked((int)0xFFFFFFFF);

    private static readonly ConditionalWeakTable<Form, TwTitleBar> Table = new ConditionalWeakTable<Form, TwTitleBar>();

    private readonly Form _form;

    private TwTitleBar(Form form)
    {
        _form = form;
        _form.HandleCreated += (_, _) => Push();
    }

    public bool Dark { get; private set; }
    public Color? Caption { get; private set; }
    public Color? Text { get; private set; }
    public Color? Border { get; private set; }
    public TwBackdrop? Backdrop { get; private set; }

    private bool BackdropPushed { get; set; }

    /// <summary>Whether all of the last DWM calls succeeded (false on older Windows).</summary>
    public bool Applied { get; private set; }

    public static TwTitleBar For(Form form) => Table.GetValue(form, f => new TwTitleBar(f));

    public void Update(bool dark, Color? caption, Color? text, Color? border, TwBackdrop? backdrop = null)
    {
        Backdrop = backdrop;
        Dark = dark;
        Caption = caption;
        Text = text;
        Border = border;
        if (_form.IsHandleCreated) Push();
    }

    private void Push()
    {
        var handle = _form.Handle;
        int dark = Dark ? 1 : 0;
        bool ok = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int)) == 0
                  || DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref dark, sizeof(int)) == 0;

        // Windows 11 renk öznitelikleri; desteklenmiyorsa hata döner ve yok sayılır.
        ok &= SetColor(handle, DwmwaCaptionColor, Caption);
        ok &= SetColor(handle, DwmwaTextColor, Text);
        ok &= SetColor(handle, DwmwaBorderColor, Border);
        Applied = ok;

        // Windows 11 22H2+: Mica/Acrylic başlık çubuğunda ve çerçevede görünür. İstemci alanı WinForms'ta opak çizildiğinden
        // zemin orada görünmez (bkz. README). Desteklenmeyen sistemlerde çağrı hata döner ve yok sayılır.
        if (Backdrop is TwBackdrop backdrop || BackdropPushed)
        {
            int type = (int)(Backdrop ?? TwBackdrop.Auto);
            DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref type, sizeof(int));
            BackdropPushed = Backdrop != null;
        }

        // Başlık çubuğu, pencere çerçevesi yeniden çizilene kadar eski rengini koruyabilir.
        SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, SwpFrameChanged | SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    private static bool SetColor(IntPtr handle, int attribute, Color? color)
    {
        int value = color is Color c ? c.R | (c.G << 8) | (c.B << 16) : DwmwaColorDefault; // COLORREF: 0x00BBGGRR
        return DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int)) == 0;
    }

    private const uint SwpNoSize = 0x0001, SwpNoMove = 0x0002, SwpNoZOrder = 0x0004, SwpNoActivate = 0x0010, SwpFrameChanged = 0x0020;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
}
