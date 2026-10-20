using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Text loaded into a <see cref="RichTextBox"/> as RTF (<c>Rtf</c>, <c>SelectedRtf</c>, <c>LoadFile</c>) without a color
/// table gets the "automatic" color, which Windows draws in the system text color (black) instead of <c>ForeColor</c>: in dark
/// mode it was black on a dark background (KeePass's entry details). While the classes give the box a text color, the
/// automatic-colored runs get <c>ForeColor</c> after RTF is streamed in; colors written in the RTF are kept. The selection,
/// the scroll position and the undo history stay as they were.
/// </summary>
internal sealed class TwRichTextColor : NativeWindow
{
    private const int EmStreamIn = 0x0449;
    private const int EmGetCharFormat = 0x043A;
    private const int EmSetCharFormat = 0x0444;
    private const int EmExGetSel = 0x0434;
    private const int EmExSetSel = 0x0437;
    private const int EmGetEventMask = 0x043B;
    private const int EmSetEventMask = 0x0445;
    private const int EmCanUndo = 0x00C6;
    private const int EmEmptyUndoBuffer = 0x00CD;
    private const int EmGetScrollPos = 0x04DD;
    private const int EmSetScrollPos = 0x04DE;
    private const int WmSetRedraw = 0x000B;
    private const int WmGetTextLength = 0x000E;
    private const int ScfSelection = 0x0001;
    private const uint CfmColor = 0x40000000;
    private const uint CfeAutoColor = 0x40000000;

    private static readonly ConditionalWeakTable<RichTextBox, TwRichTextColor> Table = new ConditionalWeakTable<RichTextBox, TwRichTextColor>();

    private readonly RichTextBox _box;
    private bool _active;

    private TwRichTextColor(RichTextBox box)
    {
        _box = box;
        box.HandleCreated += (_, _) => { if (_active) AssignHandle(box.Handle); };
        box.HandleDestroyed += (_, _) => ReleaseHandle();
    }

    /// <param name="box">The rich text box.</param>
    /// <param name="active">The classes give the box a text color.</param>
    public static void Update(RichTextBox box, bool active)
    {
        if (!Table.TryGetValue(box, out var state))
        {
            if (!active) return;
            state = Table.GetValue(box, b => new TwRichTextColor(b));
        }
        if (state._active == active) return;
        state._active = active;
        if (active && box.IsHandleCreated) state.AssignHandle(box.Handle);
        else if (!active) state.ReleaseHandle();
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == EmStreamIn) ColorAutomaticText();
    }

    private void ColorAutomaticText()
    {
        var handle = Handle;
        int length = (int)SendMessage(handle, WmGetTextLength, IntPtr.Zero, IntPtr.Zero);
        if (length == 0) return;

        // Seçim üzerinden çalışılır: olaylar (SelectionChanged) kapatılır, seçim ve kaydırma konumu geri yüklenir.
        var mask = SendMessage(handle, EmGetEventMask, IntPtr.Zero, IntPtr.Zero);
        SendMessage(handle, EmSetEventMask, IntPtr.Zero, IntPtr.Zero);
        SendMessage(handle, WmSetRedraw, IntPtr.Zero, IntPtr.Zero);
        var selection = new CharRange();
        SendMessage(handle, EmExGetSel, IntPtr.Zero, ref selection);
        var scroll = new Point();
        SendMessage(handle, EmGetScrollPos, IntPtr.Zero, ref scroll);
        bool hadUndo = SendMessage(handle, EmCanUndo, IntPtr.Zero, IntPtr.Zero) != IntPtr.Zero;
        bool changed = false;
        try
        {
            changed = Color(handle, 0, length, ColorTranslator.ToWin32(_box.ForeColor));
        }
        finally
        {
            // Akışla gelen içerik geri alma geçmişini boşaltır; renk değişikliği tek geri alınabilir adım olarak kalmasın.
            if (changed && !hadUndo) SendMessage(handle, EmEmptyUndoBuffer, IntPtr.Zero, IntPtr.Zero);
            SendMessage(handle, EmExSetSel, IntPtr.Zero, ref selection);
            SendMessage(handle, EmSetScrollPos, IntPtr.Zero, ref scroll);
            SendMessage(handle, EmSetEventMask, IntPtr.Zero, mask);
            SendMessage(handle, WmSetRedraw, (IntPtr)1, IntPtr.Zero);
            _box.Invalidate();
        }
    }

    // Aralığın rengi tek tipse ve otomatikse boyanır; karışıksa ikiye bölünür (renk değişimi sayısı × log uzunluk mesaj).
    private static bool Color(IntPtr handle, int start, int end, int color)
    {
        var range = new CharRange { Min = start, Max = end };
        SendMessage(handle, EmExSetSel, IntPtr.Zero, ref range);
        var format = new CharFormat { Size = Marshal.SizeOf<CharFormat>() };
        SendMessage(handle, EmGetCharFormat, (IntPtr)ScfSelection, ref format);
        if ((format.Mask & CfmColor) != 0)
        {
            if ((format.Effects & CfeAutoColor) == 0) return false;
            var set = new CharFormat { Size = Marshal.SizeOf<CharFormat>(), Mask = CfmColor, TextColor = color };
            SendMessage(handle, EmSetCharFormat, (IntPtr)ScfSelection, ref set);
            return true;
        }
        if (end - start < 2) return false;
        int middle = start + (end - start) / 2;
        bool first = Color(handle, start, middle, color);
        return Color(handle, middle, end, color) | first;
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
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref CharRange lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref Point lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, ref CharFormat lParam);
}
