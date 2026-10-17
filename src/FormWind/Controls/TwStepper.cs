using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace FormWind;

/// <summary>State of a <see cref="TwStep"/>.</summary>
public enum TwStepStatus
{
    Pending,
    Current,
    Completed,
    Error,
}

/// <summary>How a <see cref="TwStepper"/> draws its steps.</summary>
public enum TwStepperVariant
{
    /// <summary>One row per step: a numbered circle, title and description, and a badge on the right (an approval flow).</summary>
    Vertical,
    /// <summary>Tinted pills in a row joined by chevrons ("1 · Entered › 2 · Awaiting approval").</summary>
    Horizontal,
    /// <summary>Equal bars side by side without text (the phases of a corrective action in a table row).</summary>
    Segments,
}

/// <summary>A step of a <see cref="TwStepper"/>; <see cref="TwChartItem.Label"/> is its title.</summary>
public sealed class TwStep : TwChartItem
{
    private string _description = "";
    private string _badge = "";
    private TwStepStatus _status;

    public TwStep()
    {
    }

    public TwStep(string label, TwStepStatus status = TwStepStatus.Pending, string description = "")
    {
        Label = label;
        _status = status;
        _description = description ?? "";
    }

    /// <summary>Secondary line under the title (who, when).</summary>
    public string Description
    {
        get => _description;
        set
        {
            _description = value ?? "";
            OnChanged();
        }
    }

    /// <summary>Status text on the right of a vertical step ("In progress", "Waiting"); empty = none.</summary>
    public string Badge
    {
        get => _badge;
        set
        {
            _badge = value ?? "";
            OnChanged();
        }
    }

    public TwStepStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            OnChanged();
        }
    }
}

/// <summary>
/// Progress through ordered steps: an approval flow, a workflow, the phases of a corrective action. Completed steps use the
/// theme's success color, the current step <c>accent-*</c>, failed ones the danger color and pending steps <c>divide-*</c>;
/// a step's own <see cref="TwChartItem.Color"/> takes precedence. Text uses the control's color and <c>label:text-*</c>.
/// Screen readers get one item per step with its status (<see cref="TwTexts.StepCompleted"/> …).
/// <code>
/// stepper.Steps.Add(new TwStep("Operator entry", TwStepStatus.Current, "draft 00:05") { Badge = "In progress" });
/// stepper.Steps.Add(new TwStep("Supervisor approval"));
/// </code>
/// </summary>
[ToolboxItem(true)]
[Description("Progress through ordered steps.")]
public class TwStepper : Control, ITwChartControl, ITwChartItems
{
    private TwStepperVariant _variant;
    private TwChartColors _colors = new TwChartColors();

    public TwStepper()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        BackColor = Color.Transparent;
        Steps = new TwChartItemCollection<TwStep>(OnDataChanged);
    }

    protected override Size DefaultSize => new Size(320, 160);

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TwChartItemCollection<TwStep> Steps { get; }

    [Category("Appearance"), DefaultValue(TwStepperVariant.Vertical)]
    public TwStepperVariant Variant
    {
        get => _variant;
        set
        {
            _variant = value;
            Invalidate();
        }
    }

    /// <summary>Marks the steps before <paramref name="index"/> completed, that step current and the rest pending.</summary>
    public void SetCurrent(int index)
    {
        for (int i = 0; i < Steps.Count; i++)
            Steps[i].Status = i < index ? TwStepStatus.Completed : i == index ? TwStepStatus.Current : TwStepStatus.Pending;
    }

    /// <summary>The color a step is drawn with (its own, else by status).</summary>
    public Color GetStepColor(int index)
    {
        var step = Steps[index];
        if (!step.Color.IsEmpty) return step.Color;
        return step.Status switch
        {
            TwStepStatus.Completed => _colors.Positive ?? ColorTranslator.FromHtml(_colors.Dark ? "#4ade80" : "#15803d"),
            TwStepStatus.Current => _colors.Accent ?? TwCharts.SeriesColor(0, _colors.Dark, null),
            TwStepStatus.Error => _colors.Negative ?? ColorTranslator.FromHtml(_colors.Dark ? "#f87171" : "#b91c1c"),
            _ => TwCharts.Muted(this, _colors),
        };
    }

    private void OnDataChanged()
    {
        Invalidate();
        AccessibilityNotifyClients(AccessibleEvents.Reorder, -1);
    }

    void ITwChartControl.SetChartColors(TwChartColors colors)
    {
        _colors = colors;
        Invalidate();
    }

    // ---- Yerleşim ----

    private float DpiScale => TwDpi.DeviceDpi(this) / 96f;

    private const TextFormatFlags Flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

    private Rectangle Content => Rectangle.FromLTRB(Padding.Left, Padding.Top, Math.Max(Padding.Left, Width - Padding.Right), Math.Max(Padding.Top, Height - Padding.Bottom));

    private int Line => TextRenderer.MeasureText("Ag", Font, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis).Height;

    private int RowHeight => Line * 2 + (int)(14 * DpiScale);

    private string PillText(int index) =>
        (index + 1) + " · " + Steps[index].Label + (Steps[index].Description.Length > 0 ? " · " + Steps[index].Description : "");

    /// <summary>Bounds of each step: rows, pills or segments.</summary>
    internal Rectangle[] StepBounds()
    {
        float s = DpiScale;
        var content = Content;
        var result = new Rectangle[Steps.Count];
        switch (_variant)
        {
            case TwStepperVariant.Vertical:
                for (int i = 0; i < Steps.Count; i++) result[i] = new Rectangle(content.X, content.Y + i * RowHeight, content.Width, RowHeight);
                break;
            case TwStepperVariant.Horizontal:
            {
                int x = content.X, h = Line + (int)(6 * s), chevron = (int)(20 * s);
                int y = content.Y + Math.Max(0, (content.Height - h) / 2);
                for (int i = 0; i < Steps.Count; i++)
                {
                    int w = TextRenderer.MeasureText(PillText(i), Font, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis).Width + (int)(16 * s);
                    w = Math.Max(0, Math.Min(w, content.Right - x));
                    result[i] = new Rectangle(x, y, w, h);
                    x += w + chevron;
                }
                break;
            }
            default:
            {
                float gap = 4 * s, h = Math.Min(content.Height, 4 * s);
                float w = Steps.Count == 0 ? 0 : (content.Width - gap * (Steps.Count - 1)) / Steps.Count;
                for (int i = 0; i < Steps.Count; i++)
                    result[i] = Rectangle.Round(new RectangleF(content.X + i * (w + gap), content.Y + (content.Height - h) / 2, Math.Max(1, w), h));
                break;
            }
        }
        return result;
    }

    /// <summary>The height the steps need at the current width (for AutoSize-like layouts).</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        float s = DpiScale;
        int height = _variant switch
        {
            TwStepperVariant.Vertical => Steps.Count * RowHeight,
            TwStepperVariant.Horizontal => Line + (int)(6 * s),
            _ => (int)(4 * s),
        };
        return new Size(Math.Max(Width, 1), height + Padding.Vertical);
    }

    // ---- Çizim ----

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        TwPainter.PaintParentBackground(this, g);
        if (BackColor.A > 0) using (var brush = new SolidBrush(BackColor)) g.FillRectangle(brush, ClientRectangle);
        float s = DpiScale;
        var surface = TwCharts.Surface(this);
        var muted = TwCharts.Muted(this, _colors);
        var grid = TwCharts.Grid(this, _colors);
        var bounds = StepBounds();

        for (int i = 0; i < Steps.Count; i++)
        {
            var step = Steps[i];
            var color = GetStepColor(i);
            if (!Enabled) color = TwCharts.Blend(color, surface, 0.5f);
            var box = bounds[i];
            switch (_variant)
            {
                case TwStepperVariant.Vertical:
                    PaintRow(g, i, box, color, surface, muted, grid, s);
                    break;
                case TwStepperVariant.Horizontal:
                {
                    if (box.Width <= 0) continue;
                    bool pending = step.Status == TwStepStatus.Pending;
                    TwPainter.FillRounded(g, box, box.Height / 2f, pending ? grid : TwCharts.Blend(surface, color, 0.2f));
                    TextRenderer.DrawText(g, PillText(i), Font, Rectangle.Inflate(box, -(int)(8 * s), 0), pending ? muted : color, Flags | TextFormatFlags.VerticalCenter);
                    if (i < Steps.Count - 1 && box.Right + 4 * s < Content.Right)
                        TextRenderer.DrawText(g, "›", Font, new Rectangle(box.Right, box.Y, (int)(20 * s), box.Height), muted, Flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    break;
                }
                default:
                    TwPainter.FillRounded(g, box, box.Height / 2f, step.Status == TwStepStatus.Pending && step.Color.IsEmpty ? grid : color);
                    break;
            }
        }
    }

    private void PaintRow(Graphics g, int index, Rectangle row, Color color, Color surface, Color muted, Color grid, float s)
    {
        var step = Steps[index];
        int d = (int)(24 * s), line = Line;
        var circle = new Rectangle(row.X, row.Y + (int)(4 * s), d, d);
        bool solid = step.Status is TwStepStatus.Current or TwStepStatus.Error;
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var fill = new SolidBrush(step.Status == TwStepStatus.Pending ? grid : solid ? color : TwCharts.Blend(surface, color, 0.2f)))
            g.FillEllipse(fill, circle);
        g.Restore(state);
        string mark = step.Status switch
        {
            TwStepStatus.Completed => "✓",
            TwStepStatus.Error => "!",
            _ => (index + 1).ToString(System.Globalization.CultureInfo.CurrentCulture),
        };
        using var bold = new Font(Font, FontStyle.Bold);
        var markColor = solid ? TwCharts.ReadableOn(color) : step.Status == TwStepStatus.Pending ? ForeColor : color;
        TextRenderer.DrawText(g, mark, bold, circle, markColor, Flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        int x = circle.Right + (int)(12 * s);
        int badgeWidth = 0;
        if (step.Badge.Length > 0)
        {
            var size = TextRenderer.MeasureText(step.Badge, Font, Size.Empty, Flags & ~TextFormatFlags.EndEllipsis);
            var badge = new Rectangle(row.Right - size.Width - (int)(16 * s), circle.Y + (d - size.Height - (int)(4 * s)) / 2, size.Width + (int)(16 * s), size.Height + (int)(4 * s));
            bool pending = step.Status == TwStepStatus.Pending;
            TwPainter.FillRounded(g, badge, badge.Height / 2f, pending ? grid : TwCharts.Blend(surface, color, 0.2f));
            TextRenderer.DrawText(g, step.Badge, Font, badge, pending ? muted : color, Flags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            badgeWidth = badge.Width + (int)(8 * s);
        }
        int width = Math.Max(0, row.Right - badgeWidth - x);
        TextRenderer.DrawText(g, step.Label, bold, new Rectangle(x, circle.Y + d / 2 - line + (step.Description.Length > 0 ? 0 : line / 2), width, line), ForeColor, Flags);
        if (step.Description.Length > 0)
            TextRenderer.DrawText(g, step.Description, Font, new Rectangle(x, circle.Y + d / 2, width, line), muted, Flags);
    }

    // ---- Erişilebilirlik ----

    int ITwChartItems.ItemCount => Steps.Count;

    int ITwChartItems.ActiveItem
    {
        get
        {
            for (int i = 0; i < Steps.Count; i++)
                if (Steps[i].Status == TwStepStatus.Current) return i;
            return -1;
        }
        set { }
    }

    int ITwChartItems.ItemAt(Point client) => Array.FindIndex(StepBounds(), b => b.Contains(client));

    Rectangle ITwChartItems.ItemBounds(int index) => StepBounds()[index];

    /// <summary>Text of a step for screen readers: position, title, status and description.</summary>
    public string GetItemText(int index)
    {
        var step = Steps[index];
        var texts = TwTexts.Current;
        string status = step.Status switch
        {
            TwStepStatus.Completed => texts.StepCompleted,
            TwStepStatus.Current => texts.StepCurrent,
            TwStepStatus.Error => texts.StepError,
            _ => texts.StepPending,
        };
        return (index + 1) + ". " + step.Label + ", " + status + (step.Description.Length > 0 ? ", " + step.Description : "");
    }

    string ITwChartItems.ItemText(int index) => GetItemText(index);

    string ITwChartItems.Summary => Steps.Count(x => x.Status == TwStepStatus.Completed) + " / " + Steps.Count;

    protected override AccessibleObject CreateAccessibilityInstance() => new TwChartAccessibleObject(this, this);
}
