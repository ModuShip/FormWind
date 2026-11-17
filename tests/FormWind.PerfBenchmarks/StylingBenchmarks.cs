using System;
using System.Drawing;
using System.Windows.Forms;
using BenchmarkDotNet.Attributes;

namespace FormWind.PerfBenchmarks;

/// <summary>
/// Creating 60 controls of one type in a shown page, with and without classes. The difference between the two rows of a
/// type is the cost of styling; divided by <see cref="ControlCount"/> it is the time and memory per styled control (the
/// 0.1.7 report measured 10–14 KB per control).
/// </summary>
[MemoryDiagnoser]
public class StylingBenchmarks
{
    public const int ControlCount = 60;

    public enum Kind
    {
        TwButton,
        TwPanel,
        TwTextBox,
        TwSelect,
        Label,
    }

    private BenchmarkHost _host = null!;

    [Params(Kind.TwButton, Kind.TwPanel, Kind.TwTextBox, Kind.TwSelect, Kind.Label)]
    public Kind Type { get; set; }

    [Params(false, true)]
    public bool Styled { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _host = new BenchmarkHost(new Size(1200, 900));
        _host.Provider.SetClasses(_host.Form, "bg-surface-50");
        _host.Show();
    }

    [GlobalCleanup]
    public void Cleanup() => _host.Dispose();

    // The page is removed and disposed in the measured part too (in both rows), so its handles and bindings do not pile up
    // over the iterations and slow down the later ones.
    [Benchmark]
    [STAThread]
    public void CreateStyleAndShow()
    {
        var page = new FlowLayoutPanel { Dock = DockStyle.Fill };
        _host.Form.Controls.Add(page);
        page.SuspendLayout();
        for (int i = 0; i < ControlCount; i++)
        {
            var (control, classes) = Create(Type, i);
            page.Controls.Add(control);
            if (Styled) _host.Provider.SetClasses(control, classes);
        }
        page.ResumeLayout();
        _host.Form.Controls.Remove(page);
        page.Dispose();
    }

    private static (Control Control, string Classes) Create(Kind kind, int index) => kind switch
    {
        Kind.TwButton => (new TwButton { Size = new Size(120, 36), Text = "Button " + index }, "btn-primary"),
        Kind.TwPanel => (new TwPanel { Size = new Size(160, 70) }, "card"),
        Kind.TwTextBox => (new TwTextBox { Size = new Size(160, 36), Text = "Text " + index }, "input"),
        Kind.TwSelect => (CreateSelect(index), "select"),
        Kind.Label => (new Label { AutoSize = true, Text = "Label " + index }, "text-sm text-surface-700"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static TwSelect CreateSelect(int index)
    {
        var select = new TwSelect { Size = new Size(160, 36) };
        select.Items.AddRange(new[] { "One", "Two", "Three " + index });
        return select;
    }
}
