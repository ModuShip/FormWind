using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Colors a standard ProgressBar. The themed bar ignores colors, so when the classes give <c>accent-*</c> (bar) or
/// <c>fill-*</c>/<c>bg-*</c> (track), the visual style is turned off for the control and the colors are sent to the native
/// control (<c>PBM_SETBARCOLOR</c>, <c>PBM_SETBKCOLOR</c>). The classic bar draws <see cref="ProgressBarStyle.Blocks"/> as
/// separate blocks, so a block bar is made <see cref="ProgressBarStyle.Continuous"/> meanwhile. Removing the colors restores
/// the theme and the style. The colors are sent again when the handle is recreated.
/// </summary>
internal sealed class ProgressBarApplier : WinFormsApplier
{
    private const int PbmSetBarColor = 0x0409;
    private const int PbmSetBkColor = 0x2001;
    private const int ClrDefault = unchecked((int)0xFF000000);

    private static readonly ConditionalWeakTable<ProgressBar, State> States = new ConditionalWeakTable<ProgressBar, State>();

    public override bool CanApply(Control control) => control is ProgressBar;

    /// <summary>The colors FormWind sends to the bar; <c>null</c> when the bar keeps its theme.</summary>
    internal static (Color Bar, Color Track)? Colors(ProgressBar bar) =>
        States.TryGetValue(bar, out var state) && state.Active ? (state.Bar ?? SystemColors.Highlight, state.Track ?? SystemColors.Control) : null;

    public override void Apply(Control control, TwStyle style, TwApplyContext context)
    {
        var bar = (ProgressBar)control;
        // Zemin kanalını uygulayıcı kendisi gönderir; WinFormsApplier ProgressBar.BackColor'a dokunmaz.
        var rest = new TwStyle();
        rest.MergeFrom(style);
        rest.BackColor = null;
        base.Apply(control, rest, context);

        var track = Opaque(style.FillColor) ?? Opaque(style.BackColor);
        var fill = Opaque(style.AccentColor);
        bool active = !context.DesignMode && (track.HasValue || fill.HasValue);

        if (!States.TryGetValue(bar, out var state))
        {
            if (!active) return;
            state = States.GetValue(bar, b =>
            {
                var created = new State();
                b.HandleCreated += (_, _) => Push(b, created);
                return created;
            });
        }
        bool changed = state.Active != active || state.Bar != fill || state.Track != track;
        // Style değişimi handle'ı yeniden oluşturur; renkler HandleCreated'da yeniden gönderilir.
        if (active && !state.Active && bar.Style == ProgressBarStyle.Blocks)
        {
            state.MadeContinuous = true;
            state.Active = true;
            state.Bar = fill;
            state.Track = track;
            bar.Style = ProgressBarStyle.Continuous;
        }
        else if (!active && state.MadeContinuous)
        {
            state.MadeContinuous = false;
            state.Active = false;
            if (bar.Style == ProgressBarStyle.Continuous) bar.Style = ProgressBarStyle.Blocks;
        }
        state.Active = active;
        state.Bar = fill;
        state.Track = track;
        if (changed && bar.IsHandleCreated) Push(bar, state);
    }

    private static Color? Opaque(Color? color) => color is Color c && c.A == 255 ? c : null;

    private static void Push(ProgressBar bar, State state)
    {
        try
        {
            if (state.Active)
            {
                SetWindowTheme(bar.Handle, "", "");
                SendMessage(bar.Handle, PbmSetBarColor, IntPtr.Zero, (IntPtr)(state.Bar is Color b ? ColorTranslator.ToWin32(b) : ClrDefault));
                SendMessage(bar.Handle, PbmSetBkColor, IntPtr.Zero, (IntPtr)(state.Track is Color t ? ColorTranslator.ToWin32(t) : ClrDefault));
            }
            else
            {
                SendMessage(bar.Handle, PbmSetBarColor, IntPtr.Zero, (IntPtr)ClrDefault);
                SendMessage(bar.Handle, PbmSetBkColor, IntPtr.Zero, (IntPtr)ClrDefault);
                SetWindowTheme(bar.Handle, null, null);
            }
            bar.Invalidate();
        }
        catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
        {
        }
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    private sealed class State
    {
        public bool Active;
        public Color? Bar;
        public Color? Track;
        public bool MadeContinuous;
    }
}
