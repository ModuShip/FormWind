using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// The usage report (LaboratuvarKamera on 0.1.10-preview.2): grid frame and lines set in code, smooth label corners,
/// wrapping TwButton text, state changes from a worker thread, a menu item's own text color, the theme file fallback,
/// font classes on an AutoScaleMode.Font form and <see cref="TailwindProvider.PreserveLayout"/>.
/// </summary>
[Collection(UiCollection.Name)]
public class FeedbackUsageReportTests
{
    private static readonly int Blue600 = ColorTranslator.FromHtml("#2563eb").ToArgb();

    private static Form HostForm(int width = 300, int height = 200) =>
        new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-3000, -3000), ClientSize = new Size(width, height) };

    private static Bitmap Picture(Control control)
    {
        var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    private static bool PumpUntil(Func<bool> done, int milliseconds)
    {
        var clock = Stopwatch.StartNew();
        while (!done() && clock.ElapsedMilliseconds < milliseconds)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
        return done();
    }

    private sealed class CapturedTrace : IDisposable
    {
        private readonly StringWriter _writer = new StringWriter();
        private readonly TextWriterTraceListener _listener;

        public CapturedTrace()
        {
            _listener = new TextWriterTraceListener(_writer);
            Trace.Listeners.Add(_listener);
        }

        public string Text
        {
            get
            {
                _listener.Flush();
                return _writer.ToString();
            }
        }

        public void Dispose()
        {
            Trace.Listeners.Remove(_listener);
            _listener.Dispose();
        }
    }

    // ---- 1: the grid keeps the frame and lines the application set after the classes ----

    [Fact]
    public void Grid_keeps_frame_and_lines_set_in_code_when_the_classes_are_applied_again() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var grid = new DataGridView();
        provider.SetClasses(grid, "bg-white header:bg-gray-50 cell:px-3");

        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.None;
        grid.GridColor = Color.Red;
        provider.DarkMode = true;
        provider.DarkMode = false;

        Assert.Equal(BorderStyle.None, grid.BorderStyle);
        Assert.Equal(DataGridViewCellBorderStyle.None, grid.CellBorderStyle);
        Assert.Equal(Color.Red.ToArgb(), grid.GridColor.ToArgb());
    });

    [Fact]
    public void Grid_divide_classes_still_set_the_lines() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var grid = new DataGridView { CellBorderStyle = DataGridViewCellBorderStyle.None };
        provider.SetClasses(grid, "divide-y");

        Assert.Equal(DataGridViewCellBorderStyle.SingleHorizontal, grid.CellBorderStyle);
        provider.SetClasses(grid, "bg-white");
        Assert.Equal(DataGridViewCellBorderStyle.None, grid.CellBorderStyle);
    });

    // ---- 2: a rounded label is drawn with anti-aliased corners, not clipped with a Region ----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rounded_label_has_smooth_corners_on_its_parents_color(bool compatible) => Sta.Run(() =>
    {
        using var form = HostForm();
        form.BackColor = Color.White;
        using var provider = new TailwindProvider();
        var label = new Label { AutoSize = false, Size = new Size(80, 24), Text = "OK", TextAlign = ContentAlignment.MiddleCenter, UseCompatibleTextRendering = compatible };
        form.Controls.Add(label);
        provider.SetClasses(label, "bg-blue-600 text-white rounded-full");
        form.Show();
        Application.DoEvents();

        Assert.Null(label.Region);
        using var picture = Picture(label);
        Assert.Equal(Color.White.ToArgb(), picture.GetPixel(0, 0).ToArgb());
        Assert.Equal(Blue600, picture.GetPixel(12, 12).ToArgb());
        // Anti-aliasing leaves pixels between the two colors along the curve.
        bool blended = Enumerable.Range(0, 12).SelectMany(x => Enumerable.Range(0, 12).Select(y => picture.GetPixel(x, y).ToArgb()))
            .Any(c => c != Blue600 && c != Color.White.ToArgb());
        Assert.True(blended);
    });

    [Fact]
    public void Rounded_panel_is_still_clipped_with_a_region() => Sta.Run(() =>
    {
        using var form = HostForm();
        using var provider = new TailwindProvider();
        var panel = new Panel { Size = new Size(80, 40) };
        form.Controls.Add(panel);
        provider.SetClasses(panel, "bg-blue-600 rounded-lg");

        Assert.NotNull(panel.Region);
    });

    // ---- 4: TwButton wraps on a line break and with whitespace-normal ----

    private static int TextHeight(TwButton button)
    {
        using var picture = Picture(button);
        int top = int.MaxValue, bottom = -1;
        for (int y = 0; y < picture.Height; y++)
            for (int x = 0; x < picture.Width; x++)
                if (picture.GetPixel(x, y).GetBrightness() < 0.5f)
                {
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
        return bottom < 0 ? 0 : bottom - top + 1;
    }

    [Fact]
    public void TwButton_draws_a_line_break_on_two_lines() => Sta.Run(() =>
    {
        using var form = HostForm();
        using var provider = new TailwindProvider();
        var single = new TwButton { Size = new Size(120, 80), Text = "Save all" };
        var broken = new TwButton { Size = new Size(120, 80), Text = "Save\nall" };
        form.Controls.Add(single);
        form.Controls.Add(broken);
        provider.SetClasses(single, "bg-white text-black");
        provider.SetClasses(broken, "bg-white text-black");
        form.Show();
        Application.DoEvents();

        int one = TextHeight(single), two = TextHeight(broken);
        Assert.True(two > one * 3 / 2, $"{two} vs {one}");
    });

    [Fact]
    public void Whitespace_classes_set_TwButton_word_wrap() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var button = new TwButton { Size = new Size(70, 80), Text = "Remove the selected rows" };
        provider.SetClasses(button, "whitespace-normal");
        Assert.True(button.WordWrap);
        provider.SetClasses(button, "whitespace-nowrap");
        Assert.False(button.WordWrap);
    });

    // ---- 5: a state change on a worker thread is applied on the UI thread ----

    [Fact]
    public void Enabled_changed_on_a_worker_thread_restyles_the_button_on_the_ui_thread() => Sta.Run(() =>
    {
        using var form = HostForm();
        using var provider = new TailwindProvider();
        var button = new TwButton { Size = new Size(100, 30), Text = "Run" };
        form.Controls.Add(button);
        provider.SetClasses(button, "bg-blue-600 disabled:bg-gray-300 transition");
        form.Show();
        button.Enabled = false;
        Assert.True(PumpUntil(() => button.BackColor.ToArgb() != Blue600, 2000));

        int uiThread = Thread.CurrentThread.ManagedThreadId;
        int? appliedOn = null;
        button.BackColorChanged += (_, _) => appliedOn ??= Thread.CurrentThread.ManagedThreadId;
        Task.Run(() => button.Enabled = true).Wait();

        Assert.True(PumpUntil(() => button.BackColor.ToArgb() == Blue600, 2000), button.BackColor.ToString());
        Assert.Equal(uiThread, appliedOn);
    });

    // ---- 6: a menu item's own ForeColor wins over the menu's text color ----

    private static Color TextColorOf(ToolStripRenderer renderer, ToolStripItem item)
    {
        using var bitmap = new Bitmap(100, 20);
        using var g = Graphics.FromImage(bitmap);
        var e = new ToolStripItemTextRenderEventArgs(g, item, item.Text, new Rectangle(0, 0, 100, 20), Color.Black, item.Font, TextFormatFlags.Default);
        renderer.DrawItemText(e);
        return e.TextColor;
    }

    [Fact]
    public void Status_strip_item_keeps_the_fore_color_set_in_code() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider();
        using var strip = new StatusStrip();
        var connected = new ToolStripStatusLabel("Bağlı") { ForeColor = Color.Green };
        var plain = new ToolStripStatusLabel("Hazır");
        strip.Items.Add(connected);
        strip.Items.Add(plain);
        provider.SetClasses(strip, "bg-gray-900 text-white");

        Assert.Equal(Color.Green.ToArgb(), TextColorOf(strip.Renderer, connected).ToArgb());
        Assert.Equal(Color.White.ToArgb(), TextColorOf(strip.Renderer, plain).ToArgb());
    });

    // ---- 7: font classes on an AutoScaleMode.Font form are reported ----

    [Fact]
    public void Font_classes_on_an_autoscale_font_form_write_a_warning() => Sta.Run(() =>
    {
        using var trace = new CapturedTrace();
        using var provider = new TailwindProvider();
        using var form = new Form { AutoScaleMode = AutoScaleMode.Font, Name = "MainForm" };
        provider.SetClasses(form, "font-semibold");

        Assert.Contains("AutoScaleMode.Font", trace.Text);
        Assert.Contains("MainForm", trace.Text);
    });

    // ---- 8: PreserveLayout keeps the Designer's bounds ----

    [Fact]
    public void Preserve_layout_ignores_size_spacing_and_font_size_classes() => Sta.Run(() =>
    {
        using var provider = new TailwindProvider { PreserveLayout = true };
        using var button = new TwButton { Size = new Size(100, 30), Padding = new Padding(2) };
        float points = button.Font.SizeInPoints;
        provider.SetClasses(button, "w-64 h-12 px-6 text-lg bg-blue-600 rounded-md");

        Assert.Equal(new Size(100, 30), button.Size);
        Assert.Equal(new Padding(2), button.Padding);
        Assert.Equal(points, button.Font.SizeInPoints);
        Assert.Equal(Blue600, button.BackColor.ToArgb());
    });

    [Theory]
    [InlineData("w-64", true)]
    [InlineData("row:h-10", true)]
    [InlineData("md:hover:px-4", true)]
    [InlineData("flex", true)]
    [InlineData("grid-cols-3", true)]
    [InlineData("text-sm", true)]
    [InlineData("text-[13px]", true)]
    [InlineData("-mt-2", true)]
    [InlineData("text-gray-500", false)]
    [InlineData("hover:bg-blue-50", false)]
    [InlineData("rounded-full", false)]
    [InlineData("font-semibold", false)]
    [InlineData("border-0", false)]
    [InlineData("bg-[#F3F5F8]", false)]
    public void Layout_classes_are_recognized(string token, bool layout) =>
        Assert.Equal(layout, TwLayoutClasses.IsLayout(token));

    // ---- 9: the theme falls back to the embedded copy and warns when there is none ----

    [Fact]
    public void Missing_theme_file_without_an_embedded_copy_writes_a_warning()
    {
        using var trace = new CapturedTrace();
        var path = Path.Combine(Path.GetTempPath(), "formwind-missing-" + Guid.NewGuid().ToString("N"), "tw.config.json");

        var theme = TwTheme.LoadIfExists(path);

        Assert.NotNull(theme);
        Assert.Contains(path, trace.Text);
    }

    [Fact]
    public void Embedded_theme_is_read_from_the_assembly_that_has_one()
    {
        // The test project embeds a theme for the Designer tests through FormWind's targets.
        Assert.NotNull(TwTheme.ReadEmbeddedTheme(typeof(FeedbackUsageReportTests).Assembly));
        Assert.Null(TwTheme.ReadEmbeddedTheme(typeof(object).Assembly));
    }
}
