using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text.Json;
using System.Windows.Forms;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FormWind.Mcp;

public sealed record RenderChild(
    [property: Description("Control type, as for the parent (Button, Label, TwButton …).")] string Type,
    [property: Description("Classes of the child, e.g. \"flex-1\" or \"btn-ghost\".")] string? Classes = null,
    [property: Description("Text of the child.")] string? Text = null,
    [property: Description("Width in pixels (96 DPI).")] int? Width = null,
    [property: Description("Height in pixels (96 DPI).")] int? Height = null);

public sealed record RenderSize(int Width, int Height);

public sealed record SheetLayout(
    [property: Description("Width of the label column on the left, in image pixels.")] int LabelWidth,
    [property: Description("Height of the label row on top, in image pixels.")] int LabelHeight,
    [property: Description("Width of each frame; frame (mode m, state s) starts at x = labelWidth + s * cellWidth.")] int CellWidth,
    [property: Description("Height of each frame; frame (mode m, state s) starts at y = labelHeight + m * cellHeight.")] int CellHeight,
    [property: Description("Offset of the control inside its frame (room for shadows and focus rings).")] int ControlOffset);

public sealed record RenderSummary(
    string ControlType,
    [property: Description("Size of the control in the base state, in pixels at 96 DPI.")] RenderSize Size,
    [property: Description("Rows of the image, top to bottom.")] IReadOnlyList<string> Modes,
    [property: Description("Columns of the image, left to right.")] IReadOnlyList<string> States,
    [property: Description("The control the states were applied to: \"control\" or \"children[i]\".")] string StateTarget,
    IReadOnlyList<UnknownClass> Unknown,
    SheetLayout Layout);

[McpServerToolType]
public sealed class RenderTools
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [McpServerTool(Name = "render", Title = "Render a control with FormWind", ReadOnly = true, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true, OutputSchemaType = typeof(RenderSummary))]
    [Description("Renders a WinForms control styled with FormWind classes and returns one PNG: a row per mode (light, dark) and a column per state (base, hover, focus, active, disabled …), plus a structured summary (size, unknown classes, image layout). Use it to look at the result of the classes you wrote. Children are laid out inside the control when it is a container (TwPanel with flex or grid classes, Panel, GroupBox). Without width and height, buttons, labels and check boxes size to their text and padding classes.")]
    public static async Task<CallToolResult> Render(
        StaThread sta,
        [Description("Control type: a System.Windows.Forms control (Button, Label, TextBox, CheckBox, ComboBox, Panel, DataGridView …) or a FormWind control (TwButton, TwPanel, TwTextBox, TwSelect, TwSwitch …).")] string controlType,
        [Description("Classes of the control.")] string classes,
        [Description("Text of the control (button caption, label text …).")] string? text = null,
        [Description("Width in pixels (96 DPI). Default: the control's own size, or its text and padding for buttons, labels and check boxes.")] int? width = null,
        [Description("Height in pixels (96 DPI). Default: as for width.")] int? height = null,
        [Description("States to draw, from: base, hover, focus, active, disabled, checked, invalid. Default: base, hover, disabled.")] string[]? states = null,
        [Description("Modes to draw: light, dark or both (default).")] string mode = "both",
        [Description("Child controls, for containers.")] RenderChild[]? children = null,
        [Description("Index of the child the states apply to (0 = first child). Default: the control itself. Hover on a container with the group class also shows the children's group-hover: classes.")] int? stateTarget = null,
        [Description(Themes.PresetDescription)] string? preset = null,
        [Description(Themes.ThemeFileDescription)] string? themeFile = null,
        CancellationToken cancellationToken = default)
    {
        var theme = Themes.Resolve(preset, themeFile);
        var request = new RenderRequest(controlType, classes, text, width, height, children ?? Array.Empty<RenderChild>(), stateTarget);
        var stateList = ParseStates(states);
        var modes = ParseModes(mode);
        if (stateTarget is int index && (index < 0 || index >= request.Children.Count))
            throw ToolErrors.Create("invalid_state_target", $"stateTarget {index} is not a child index (the request has {request.Children.Count} children).",
                "Pass the 0-based index of a child, or omit stateTarget to apply the states to the control itself.");

        RenderResult result;
        try
        {
            result = await sta.InvokeAsync(() => Renderer.Render(request, theme, stateList, modes), cancellationToken)
                .WaitAsync(Timeout, cancellationToken);
        }
        catch (TimeoutException ex)
        {
            throw ToolErrors.Create("timeout", $"Rendering took longer than {Timeout.TotalSeconds:0} seconds.",
                "Render fewer states or modes, or a smaller control with fewer children.", ex);
        }
        catch (Exception ex) when (ex is not McpException and not OperationCanceledException)
        {
            throw ToolErrors.Create("render_failed", $"{ex.GetType().Name}: {ex.Message}",
                "Check the control type and its children; some controls need properties set in code before they can be drawn.", ex);
        }

        var summary = new RenderSummary(
            result.ControlType,
            new RenderSize(result.Size.Width, result.Size.Height),
            modes,
            stateList,
            stateTarget is int target ? $"children[{target}]" : "control",
            result.Unknown.Select(u => new UnknownClass(u, theme.SuggestClass(u))).ToList(),
            result.Layout);
        return new CallToolResult
        {
            Content = new List<ContentBlock>
            {
                new TextContentBlock { Text = JsonSerializer.Serialize(summary, McpJsonUtilities.DefaultOptions) },
                ImageContentBlock.FromBytes(result.Png, "image/png"),
            },
            StructuredContent = JsonSerializer.SerializeToElement(summary, McpJsonUtilities.DefaultOptions),
        };
    }

    internal static readonly string[] KnownStates = { "base", "hover", "focus", "active", "disabled", "checked", "invalid" };

    internal static IReadOnlyList<string> ParseStates(string[]? states)
    {
        if (states is null || states.Length == 0) return new[] { "base", "hover", "disabled" };
        var result = new List<string>();
        foreach (var state in states.Select(s => s.Trim().ToLowerInvariant()))
        {
            if (Array.IndexOf(KnownStates, state) < 0)
                throw ToolErrors.Create("unknown_state", $"Unknown state '{state}'.", $"Use: {string.Join(", ", KnownStates)}.");
            if (!result.Contains(state)) result.Add(state);
        }
        return result;
    }

    internal static IReadOnlyList<string> ParseModes(string? mode) => (mode ?? "both").Trim().ToLowerInvariant() switch
    {
        "both" or "" => new[] { "light", "dark" },
        "light" => new[] { "light" },
        "dark" => new[] { "dark" },
        _ => throw ToolErrors.Create("unknown_mode", $"Unknown mode '{mode}'.", "Use light, dark or both."),
    };
}

internal sealed record RenderRequest(
    string ControlType, string Classes, string? Text, int? Width, int? Height, IReadOnlyList<RenderChild> Children, int? StateTarget = null);

internal sealed record RenderResult(string ControlType, Size Size, IReadOnlyList<string> Unknown, byte[] Png, SheetLayout Layout);

/// <summary>Draws controls off screen with FormWind (like tools/FormWind.Docs) and lays the frames out as a labeled sheet.</summary>
internal static class Renderer
{
    // Gölgelerin (shadow-xl: 25 px bulanıklık + 20 px kayma) sığacağı kenar payı.
    private const int Margin = 32;
    private const int MaxSize = 1600;
    private const int LabelHeight = 22;
    private const int LabelWidth = 56;

    public static RenderResult Render(RenderRequest request, TwTheme theme, IReadOnlyList<string> states, IReadOnlyList<string> modes)
    {
        // Kontrol içi animasyonlar (sekme göstergesi, satır hover'ı) da hedef durumda çizilsin.
        TwMotion.Enabled = false;
        var type = ResolveType(request.ControlType);
        foreach (var child in request.Children) ResolveType(child.Type);

        var frames = new Bitmap[modes.Count, states.Count];
        var unknown = new List<string>();
        Size size = Size.Empty;
        try
        {
            for (int m = 0; m < modes.Count; m++)
                for (int s = 0; s < states.Count; s++)
                    frames[m, s] = RenderFrame(request, type, theme, states[s], modes[m] == "dark", unknown, ref size);
            var (png, layout) = Sheet(frames, states, modes);
            return new RenderResult(type.Name, size, unknown.Distinct().ToList(), png, layout);
        }
        finally
        {
            foreach (var frame in frames) frame?.Dispose();
        }
    }

    internal static Type ResolveType(string name)
    {
        var trimmed = name?.Trim() ?? "";
        var type = typeof(TwTheme).Assembly.GetType("FormWind." + trimmed)
                   ?? typeof(Control).Assembly.GetType("System.Windows.Forms." + trimmed);
        if (type is null || !typeof(Control).IsAssignableFrom(type) || type.IsAbstract || typeof(Form).IsAssignableFrom(type)
            || type.GetConstructor(Type.EmptyTypes) is null)
            throw ToolErrors.Create("unknown_control_type", $"Unknown control type '{name}'.",
                "Use a System.Windows.Forms control (Button, Label, TextBox …) or a FormWind control (TwButton, TwPanel …) by its short name; forms are not supported.");
        return type;
    }

    private static Bitmap RenderFrame(RenderRequest request, Type type, TwTheme theme, string state, bool dark, List<string> unknown, ref Size size)
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-10000, -10000),
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            AutoScaleMode = AutoScaleMode.None,
        };
        using var provider = new TailwindProvider { Theme = theme, DarkMode = dark };
        provider.UnknownClasses += (_, e) => unknown.AddRange(e.Unknown);

        var control = Create(type, request.Text, request.Width, request.Height);
        // Boyut verilmezse metinli kontroller metne ve padding class'larına göre boyutlanır (varsayılan 75×23 yerine).
        if (request.Width is null && request.Height is null && control is ButtonBase or Label)
        {
            control.AutoSize = true;
            if (control is Button button) button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }
        foreach (var child in request.Children)
        {
            var childControl = Create(ResolveType(child.Type), child.Text, child.Width, child.Height);
            control.Controls.Add(childControl);
            if (!string.IsNullOrWhiteSpace(child.Classes)) provider.SetClasses(childControl, child.Classes!);
        }

        var stage = new TwPanel { Location = Point.Empty };
        control.Location = new Point(Margin, Margin);
        stage.Controls.Add(control);
        form.Controls.Add(stage);
        // Odak örnekten alınıp görünmez bir düğmeye verilir (focus: stilleri ve seçili metin yalnızca istenince görünsün).
        var focusSink = new Button { Size = Size.Empty, Location = new Point(-10, -10), TabIndex = 0 };
        form.Controls.Add(focusSink);

        provider.SetClasses(stage, "page");
        provider.SetClasses(control, request.Classes);
        Fit(stage, form, control);
        form.Show();
        focusSink.Focus();
        if (control is DataGridView grid) grid.ClearSelection();

        // Durum kontrolün kendisine ya da stateTarget ile seçilen alt kontrole uygulanır (eklenme sırası = dizin).
        var target = request.StateTarget is int index ? control.Controls[index] : control;
        switch (state)
        {
            case "disabled": target.Enabled = false; break;
            case "checked":
                if (target is CheckBox check) check.Checked = true;
                else if (target is RadioButton radio) radio.Checked = true;
                else provider.ForceState(target, TwState.Checked);
                break;
            case "hover": provider.ForceState(target, TwState.Hover); break;
            case "focus": provider.ForceState(target, TwState.Focus | TwState.FocusVisible); break;
            case "active": provider.ForceState(target, TwState.Active | TwState.Hover); break;
            case "invalid": provider.ForceState(target, TwState.Invalid); break;
        }
        Application.DoEvents();
        // transition: renk geçişleri zamanlayıcıyla ilerler; kare geçişin başında değil hedef renkte alınır.
        TwAnimator.Step(DateTime.Now.AddMinutes(1));

        // AutoSize kontroller class'larla büyüyebilir; sahne yeni boyuta göre yeniden ayarlanır.
        Fit(stage, form, control);
        Application.DoEvents();
        if (size.IsEmpty) size = control.Size;

        var bitmap = new Bitmap(stage.Width, stage.Height);
        stage.DrawToBitmap(bitmap, new Rectangle(Point.Empty, stage.Size));
        form.Close();
        return bitmap;
    }

    private static Control Create(Type type, string? text, int? width, int? height)
    {
        var control = (Control)Activator.CreateInstance(type)!;
        if (text != null) control.Text = text;
        if (width.HasValue || height.HasValue)
        {
            if (control is Label label) label.AutoSize = false;
            control.Size = new Size(Clamp(width ?? control.Width), Clamp(height ?? control.Height));
        }
        return control;
    }

    private static int Clamp(int value) => Math.Max(1, Math.Min(MaxSize, value));

    private static void Fit(Control stage, Form form, Control control)
    {
        stage.Size = new Size(Math.Min(MaxSize, control.Width) + Margin * 2, Math.Min(MaxSize, control.Height) + Margin * 2);
        form.ClientSize = stage.Size;
    }

    // Satır başına bir mod, sütun başına bir durum; başlıklar çerçevelerin üstünde ve solunda.
    private static (byte[] Png, SheetLayout Layout) Sheet(Bitmap[,] frames, IReadOnlyList<string> states, IReadOnlyList<string> modes)
    {
        int cellWidth = 0, cellHeight = 0;
        foreach (var frame in frames)
        {
            cellWidth = Math.Max(cellWidth, frame.Width);
            cellHeight = Math.Max(cellHeight, frame.Height);
        }
        using var sheet = new Bitmap(LabelWidth + cellWidth * states.Count, LabelHeight + cellHeight * modes.Count);
        using (var g = Graphics.FromImage(sheet))
        using (var font = new Font("Segoe UI", 9f))
        {
            g.Clear(Color.White);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            for (int s = 0; s < states.Count; s++)
                g.DrawString(states[s], font, Brushes.DimGray, LabelWidth + s * cellWidth + 6, 3);
            for (int m = 0; m < modes.Count; m++)
            {
                g.DrawString(modes[m], font, Brushes.DimGray, 6, LabelHeight + m * cellHeight + 6);
                for (int s = 0; s < states.Count; s++)
                    g.DrawImage(frames[m, s], LabelWidth + s * cellWidth, LabelHeight + m * cellHeight);
            }
        }
        using var stream = new MemoryStream();
        sheet.Save(stream, ImageFormat.Png);
        return (stream.ToArray(), new SheetLayout(LabelWidth, LabelHeight, cellWidth, cellHeight, Margin));
    }
}
