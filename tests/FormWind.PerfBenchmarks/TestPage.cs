using System.Drawing;
using System.Windows.Forms;

namespace FormWind.PerfBenchmarks;

/// <summary>A page of 60 mixed styled controls with shadowed cards, like a typical application page.</summary>
internal static class TestPage
{
    public const int ControlCount = 60;

    public static Panel Create(TailwindProvider provider, string name)
    {
        var page = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        provider.SetClasses(page, "bg-surface-50");
        for (int i = 0; i < ControlCount; i++)
        {
            var (control, classes) = (i % 4) switch
            {
                0 => ((Control)new Label { AutoSize = true, Text = $"{name} label {i}" }, "text-sm text-surface-700"),
                1 => (new TwButton { Size = new Size(120, 36), Text = $"{name} {i}" }, "btn-primary"),
                2 => (new TwPanel { Size = new Size(160, 70) }, "card shadow-md"),
                _ => (new TwTextBox { Size = new Size(160, 36), Text = $"{name} text {i}" }, "input"),
            };
            page.Controls.Add(control);
            provider.SetClasses(control, classes);
        }
        return page;
    }
}
