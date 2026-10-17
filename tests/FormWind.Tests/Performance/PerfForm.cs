using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace FormWind.Tests.Performance;

/// <summary>
/// Ekran dışında açılan, karışık kontrollerden oluşan test formu. Her kontrol tipinin farklı bir
/// FormWind yolunu (Region, owner-draw, font, sarmalayıcı) çalıştırması için sınıflar seçildi.
/// </summary>
internal sealed class PerfForm : Form
{
    private readonly IContainer _components = new Container();
    private readonly List<Control> _styled = new List<Control>();

    public PerfForm(int controlCount)
    {
        StartPosition = FormStartPosition.Manual;
        Location = new Point(-20000, -20000);
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        ClientSize = new Size(1200, 900);

        Provider = new TailwindProvider(_components);
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill };
        Controls.Add(layout);

        Provider.BeginInit();
        Provider.SetClasses(this, "bg-gray-50 dark:bg-gray-900");
        for (int i = 0; i < controlCount; i++)
        {
            var (control, classes) = Create(i);
            layout.Controls.Add(control);
            Provider.SetClasses(control, classes);
            _styled.Add(control);
        }

        var stopwatch = Stopwatch.StartNew();
        Provider.EndInit();
        InitialStyling = stopwatch.Elapsed;
    }

    public TailwindProvider Provider { get; }

    public TimeSpan InitialStyling { get; }

    public IReadOnlyList<Control> Styled => _styled;

    protected override void Dispose(bool disposing)
    {
        if (disposing) _components.Dispose();
        base.Dispose(disposing);
    }

    private static (Control Control, string Classes) Create(int index) => (index % 6) switch
    {
        0 => (new Label { AutoSize = true, Text = "Etiket " + index },
              "text-sm text-gray-700 hover:text-blue-600 dark:text-gray-200"),
        1 => (new Button { Size = new Size(120, 36), Text = "Buton " + index },
              "btn-primary hover:rounded-lg"),
        2 => (new TwButton { Size = new Size(120, 36), Text = "Tw " + index },
              "btn-ghost hover:font-normal"),
        3 => (new TwPanel { Size = new Size(140, 60) },
              "card hover:border-blue-500"),
        4 => (new TwTextBox { Size = new Size(160, 36), Text = "Metin " + index },
              "input"),
        _ => (new Panel { Size = new Size(140, 60) },
              "bg-white rounded-lg border border-gray-200 hover:border-blue-500 dark:bg-gray-800 dark:border-gray-700"),
    };
}
