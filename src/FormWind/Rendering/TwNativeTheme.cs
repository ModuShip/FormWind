using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// Switches native scrollbars to the dark theme (<c>SetWindowTheme(hwnd, "DarkMode_Explorer")</c>, Windows 10 1809+).
/// This undocumented theme name is used by Windows Explorer; it has no effect on unsupported systems.
/// The value is reapplied when the handle is recreated.
/// </summary>
internal static class TwNativeTheme
{
    private const string DarkTheme = "DarkMode_Explorer";

    private static readonly ConditionalWeakTable<Control, State> States = new ConditionalWeakTable<Control, State>();

    /// <summary>Controls that can have native scrollbars. Form excluded: FormApplier manages the title bar.</summary>
    public static bool Supports(Control control) =>
        !(control is Form)
        && (control is ScrollableControl || control is TextBoxBase || control is ListBox || control is TreeView
            || control is ListView || control is DataGridView || control is ScrollBar);

    public static bool IsDark(Control control) => States.TryGetValue(control, out var state) && state.Dark;

    public static void Apply(Control control, bool dark)
    {
        var state = States.GetValue(control, c =>
        {
            var created = new State();
            c.HandleCreated += (_, _) => Push(c, created.Dark);
            // DataGridView kaydırma çubuklarının handle'ı grid'inkinden sonra, ilk gösterildiklerinde oluşur.
            if (c is DataGridView)
            {
                foreach (Control child in c.Controls)
                    if (child is ScrollBar bar)
                    {
                        bar.HandleCreated += (_, _) => SetTheme(bar.Handle, created.Dark);
                        bar.VisibleChanged += (_, _) => { if (bar.IsHandleCreated) SetTheme(bar.Handle, created.Dark); };
                    }
            }
            return created;
        });
        if (state.Dark == dark && state.Pushed) return;
        state.Dark = dark;
        state.Pushed = true;
        if (control.IsHandleCreated) Push(control, dark);
    }

    private static void Push(Control control, bool dark)
    {
        SetTheme(control.Handle, dark);

        // DataGridView kaydırma çubuklarını ayrı ScrollBar alt kontrolleri olarak çizer.
        if (control is DataGridView)
        {
            foreach (Control child in control.Controls)
                if (child is ScrollBar && child.IsHandleCreated) SetTheme(child.Handle, dark);
        }
    }

    private static void SetTheme(IntPtr handle, bool dark)
    {
        try
        {
            // null/null varsayılan temayı geri yükler.
            SetWindowTheme(handle, dark ? DarkTheme : null, null);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

    private sealed class State
    {
        public bool Dark;
        public bool Pushed;
    }
}
