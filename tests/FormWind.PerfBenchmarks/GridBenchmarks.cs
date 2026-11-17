using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using BenchmarkDotNet.Attributes;

namespace FormWind.PerfBenchmarks;

/// <summary>
/// Painting a DataGridView with and without the <c>table</c> classes. A styled grid is painted by FormWind's grid decorator
/// (rounded frame, header, row hover, sort arrows), so the difference between the rows is its paint cost.
/// </summary>
[MemoryDiagnoser]
public class GridBenchmarks
{
    public const int RowCount = 200;
    public const int ColumnCount = 6;

    private static readonly Size GridSize = new Size(900, 620);

    private OffscreenHost _host = null!;
    private DataGridView _grid = null!;
    private Bitmap _surface = null!;

    [Params(false, true)]
    public bool Styled { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _host = new OffscreenHost(GridSize);
        _grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
        for (int column = 0; column < ColumnCount; column++) _grid.Columns.Add("c" + column, "Column " + column);
        for (int row = 0; row < RowCount; row++)
        {
            var values = new object[ColumnCount];
            for (int column = 0; column < ColumnCount; column++) values[column] = $"Row {row}, cell {column}";
            _grid.Rows.Add(values);
        }
        _host.Form.Controls.Add(_grid);
        if (Styled) _host.Provider.SetClasses(_grid, "table table-striped");
        _host.Show();
        _surface = new Bitmap(GridSize.Width, GridSize.Height, PixelFormat.Format32bppPArgb);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _surface.Dispose();
        _host.Dispose();
    }

    [Benchmark]
    [STAThread]
    public void Paint() => _grid.DrawToBitmap(_surface, new Rectangle(Point.Empty, _grid.Size));

    /// <summary>Scrolling a page down and back, with the paint of each position (the visible rows change).</summary>
    [Benchmark]
    [STAThread]
    public void ScrollAndPaint()
    {
        _grid.FirstDisplayedScrollingRowIndex = RowCount / 2;
        _grid.Refresh();
        _grid.FirstDisplayedScrollingRowIndex = 0;
        _grid.Refresh();
    }
}
