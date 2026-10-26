using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using FormWind.Tests.Performance;
using Xunit;

namespace FormWind.Tests;

/// <summary>
/// The shared frame clock, pictures taken from the screen, cross-fades that start after the new look is painted, panel and
/// window slides, and the settings around them (toast inset, timings, motion preference, busy and loading helpers).
/// </summary>
[Collection(UiCollection.Name)]
public class MotionRevisionTests
{
    private static void WaitUntil(Func<bool> condition, int milliseconds = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    // Mesaj döngüsü gibi her seferinde tek mesaj alır (GetMessage sırası: gönderilen, kuyruğa konan, girdi, boyama,
    // zamanlayıcı). DoEvents kuyruk boşalana kadar döner; sürekli boyanan bir kontrol varken hiç dönmez.
    private static void PumpUntil(Func<bool> condition, int milliseconds = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            if (PeekMessage(out var msg, IntPtr.Zero, 0, 0, 1))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
            else Thread.Sleep(1);
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Msg
    {
        public IntPtr Hwnd;
        public int Message;
        public IntPtr WParam, LParam;
        public int Time, X, Y;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PeekMessage(out Msg msg, IntPtr hwnd, int min, int max, int remove);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Msg msg);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Msg msg);

    private static Form ShowForm(Size clientSize) => ShowForm(clientSize, new Point(120, 120));

    private static Form ShowForm(Size clientSize, Point location)
    {
        var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = location, ClientSize = clientSize };
        form.Show();
        form.Activate();
        Application.DoEvents();
        return form;
    }

    private static void WithMotion(Action action)
    {
        var previous = TwMotion.Preference;
        TwMotion.Enabled = true;
        try { action(); }
        finally { TwMotion.Preference = previous; }
    }

    /// <summary>Invalidates itself on every paint, so a paint is always pending (a chart opening, a pulsing skeleton).</summary>
    private sealed class Repainting : Control
    {
        public int Paints;

        public Repainting() => SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);

        protected override void OnPaint(PaintEventArgs e)
        {
            Paints++;
            e.Graphics.Clear(Paints % 2 == 0 ? Color.White : Color.WhiteSmoke);
            Thread.Sleep(2);
            Invalidate();
        }
    }

    [Fact]
    public void Animations_go_on_while_a_control_keeps_repainting() => Sta.Run(() => WithMotion(() =>
    {
        using var form = ShowForm(new Size(300, 200));
        var busy = new Repainting { Dock = DockStyle.Fill };
        var target = new Panel { Size = new Size(10, 10) };
        form.Controls.Add(target);
        form.Controls.Add(busy);

        PumpUntil(() => busy.Paints > 3);
        double last = -1;
        bool done = false;
        TwAnimator.Animate(target, "probe", 0, 100, 150, v => last = v, TwEasing.Linear, 0, () => done = true);
        PumpUntil(() => done, 3000);
        busy.Visible = false;

        Assert.True(busy.Paints > 5);                                              // boyama gerçekten sürüyordu
        Assert.True(done, $"animation stopped at {last}");                          // WM_TIMER'a bağlıyken burada kalırdı
        Assert.Equal(100, last);
        form.Close();
    }));

    [Fact]
    public void After_paint_completes_while_a_control_keeps_repainting() => Sta.Run(() =>
    {
        using var form = ShowForm(new Size(300, 200));
        var busy = new Repainting { Dock = DockStyle.Fill };
        form.Controls.Add(busy);
        PumpUntil(() => busy.Paints > 3);

        var task = TwMotion.AfterPaintAsync(busy);
        PumpUntil(() => task.IsCompleted);
        busy.Visible = false;
        Assert.True(task.IsCompleted);

        using var hidden = new Panel();
        Assert.True(TwMotion.AfterPaintAsync(hidden).IsCompleted);                 // gösterilmeyen kontrol: hemen
        Assert.IsType<ArgumentNullException>(Record.Exception((Action)(() => { _ = TwMotion.AfterPaintAsync(null!); })));
        form.Close();
    });

    [Fact]
    public void Frame_timers_leave_the_clock_when_stopped() => Sta.Run(() =>
    {
        int before = TwFrameClock.ActiveCount;
        using var timer = new TwFrameTimer();
        int ticks = 0;
        timer.Tick += (_, _) => ticks++;
        timer.Start();
        Assert.Equal(before + 1, TwFrameClock.ActiveCount);
        WaitUntil(() => ticks >= 3);
        Assert.True(ticks >= 3);
        timer.Stop();
        Assert.Equal(before, TwFrameClock.ActiveCount);
        timer.Start();
        timer.Dispose();
        Assert.Equal(before, TwFrameClock.ActiveCount);
        Assert.False(timer.Enabled);
    });

    [Fact]
    public void The_picture_shows_overlapping_controls_as_they_are_on_screen() => Sta.Run(() =>
    {
        // Diğer testlerin pencerelerinden uzakta: ekrandan alınan resim üstteki her şeyi içerir.
        using var form = ShowForm(new Size(200, 120), new Point(700, 420));
        var below = new Panel { BackColor = Color.Red, Bounds = new Rectangle(10, 10, 100, 60) };
        var above = new Panel { BackColor = Color.Blue, Bounds = new Rectangle(60, 30, 100, 60) };
        form.Controls.Add(above);                                                  // ilk eklenen en üstte
        form.Controls.Add(below);
        WaitUntil(() => false, 500);                                               // Windows'un pencere açılış animasyonu biter

        var bounds = form.RectangleToScreen(form.ClientRectangle);
        using var picture = TwCapture.Take(form, bounds);
        Assert.NotNull(picture);
        Assert.NotEqual(TwCapture.Source.DrawToBitmap, TwCapture.LastSource);
        var overlap = picture!.GetPixel(80, 50);                                   // iki panelin kesiştiği yer
        Assert.True(Color.Blue.ToArgb() == overlap.ToArgb(), $"{TwCapture.LastSource}: {overlap} size {picture.Size} corner {picture.GetPixel(2, 2)} red {picture.GetPixel(20, 20)}");
        form.Close();
    });

    [Fact]
    public void The_window_content_is_taken_without_the_windows_above_it() => Sta.Run(() =>
    {
        using var form = ShowForm(new Size(200, 120), new Point(700, 120));
        form.BackColor = Color.Lime;
        WaitUntil(() => false, 300);                                               // Windows'un pencere açılış animasyonu biter
        var bounds = form.RectangleToScreen(form.ClientRectangle);
        using var cover = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(cover)) g.Clear(Color.Black);
        var ghost = TwFadeOut.Cover((Bitmap)cover.Clone(), bounds, form, roundCorners: false);
        Assert.NotNull(ghost);
        try
        {
            using var content = TwCapture.TakeContent(form, bounds, ghost);
            Assert.NotNull(content);
            Assert.Equal(Color.Lime.ToArgb(), content!.GetPixel(50, 50).ToArgb());   // üstteki resim değil, pencerenin kendisi
        }
        finally
        {
            ghost!.Close();
            ghost.Dispose();
        }
        form.Close();
    });

    [Fact]
    public void The_fade_starts_after_the_change_is_painted() => Sta.Run(() => WithMotion(() =>
    {
        using var form = ShowForm(new Size(300, 200));
        var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        form.Controls.Add(page);
        Application.DoEvents();
        int before = TwFadeOut.ActiveCount;

        TwTransition.CrossFade(page, () =>
        {
            page.BackColor = Color.Black;
            Thread.Sleep(200);                                                     // uzun süren bir değişiklik
        }, 250);

        Assert.True(TwTransition.LastCovered);
        var ghost = Application.OpenForms.Cast<Form>().OfType<TwFadeOut.Ghost>().LastOrDefault(g => g.Bounds.IntersectsWith(form.Bounds) && g.Opacity > 0);
        Assert.NotNull(ghost);
        Assert.True(ghost!.Opacity > 0.9, $"opacity {ghost.Opacity}");               // solma daha başlamadı
        WaitUntil(() => TwFadeOut.ActiveCount == before);
        Assert.Equal(before, TwFadeOut.ActiveCount);
        form.Close();
    }));

    [Fact]
    public void A_panel_slides_in_and_out_with_one_layout() => Sta.Run(() => WithMotion(() =>
    {
        using var form = ShowForm(new Size(400, 240));
        var grid = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var inspector = new Panel { Dock = DockStyle.Right, Width = 120, BackColor = Color.SteelBlue, Visible = false };
        form.Controls.Add(grid);
        form.Controls.Add(inspector);
        Application.DoEvents();
        int layouts = 0;
        grid.Resize += (_, _) => layouts++;
        int before = TwFadeOut.ActiveCount;
        // OpenForms süreç geneli: başka testlerin iş parçacıklarında kalmış pencereler de sayılır.
        int open = Application.OpenForms.Count;

        TwTransition.SlideIn(inspector, TwEdge.Right, 120);
        Assert.True(inspector.Visible);
        Assert.True(TwTransition.LastCovered);
        Assert.Equal(280, grid.Width);
        WaitUntil(() => TwFadeOut.ActiveCount == before && Application.OpenForms.Count == open);
        Assert.Equal(1, layouts);                                                  // tablo bir kez yerleşti

        TwTransition.SlideOut(inspector, TwEdge.Right, 120);
        Assert.False(inspector.Visible);
        WaitUntil(() => TwFadeOut.ActiveCount == before && Application.OpenForms.Count == open);
        Assert.Equal(2, layouts);
        Assert.Equal(open, Application.OpenForms.Count);                           // kayan resim de kapandı

        var previous = TwMotion.Enabled;
        TwMotion.Enabled = false;
        TwTransition.SlideIn(inspector, TwEdge.Right);
        Assert.True(inspector.Visible);
        Assert.False(TwTransition.LastCovered);
        TwMotion.Enabled = previous;
        form.Close();
    }));

    [Fact]
    public void An_attached_window_slides_in_and_fades_out() => Sta.Run(() => WithMotion(() =>
    {
        using var owner = ShowForm(new Size(400, 300));
        var sheet = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(300, 200), Size = new Size(200, 150) };
        TwWindowMotion.Attach(sheet, TwEdge.Right);
        sheet.Show(owner);
        Application.DoEvents();
        Assert.True(TwAnimator.IsAnimating(sheet, "tw-window-slide"));
        TwAnimator.Step(DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(new Point(300, 200), sheet.Location);
        Assert.Equal(1.0, sheet.Opacity, 2);

        int before = TwFadeOut.ActiveCount;
        sheet.Close();
        Assert.Equal(before + 1, TwFadeOut.ActiveCount);                           // son görüntüsü solarak kayıyor
        WaitUntil(() => TwFadeOut.ActiveCount == before);
        Assert.Equal(before, TwFadeOut.ActiveCount);
        owner.Close();
    }));

    [Fact]
    public void Toasts_keep_the_inset_free() => Sta.Run(() => WithMotion(() =>
    {
        using var form = ShowForm(new Size(500, 360));
        var position = TwToast.Position;
        TwToast.Position = TwToastPosition.TopRight;
        try
        {
            var toast = TwToast.Show(form, "Kaydedildi");
            Application.DoEvents();
            var client = form.RectangleToScreen(form.ClientRectangle);
            TwToast.Inset = new Padding(0, 56, 30, 0);
            float dpi = TwDpi.DeviceDpi(form) / 96f;
            WaitUntil(() => toast.Top >= client.Top + (int)Math.Round(72 * dpi) - 1, 1000);
            Assert.True(toast.Top >= client.Top + (int)Math.Round(72 * dpi) - 1, $"top {toast.Top - client.Top}");   // 56 + 16 kenar payı
            Assert.True(toast.Right <= client.Right - (int)Math.Round(46 * dpi) + 1);
            toast.Dismiss();
        }
        finally
        {
            TwToast.Inset = Padding.Empty;
            TwToast.Position = position;
            form.Close();
        }
    }));

    [Fact]
    public void Styled_grids_are_double_buffered_until_the_classes_are_removed() => Sta.Run(() =>
    {
        var provider = new TailwindProvider();
        using var grid = new DataGridView();
        var doubleBuffered = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.False((bool)doubleBuffered.GetValue(grid)!);
        provider.SetClasses(grid, "bg-white header:bg-gray-50");
        Assert.True((bool)doubleBuffered.GetValue(grid)!);
        provider.SetClasses(grid, "");
        Assert.False((bool)doubleBuffered.GetValue(grid)!);
        provider.Dispose();
    });

    [Fact]
    public void Run_busy_keeps_the_button_busy_while_the_work_runs() => Sta.Run(() =>
    {
        using var button = new TwButton();
        var gate = new TaskCompletionSource<bool>();
        var run = button.RunBusyAsync(() => gate.Task);
        Assert.True(button.IsBusy);
        var second = button.RunBusyAsync(() => Task.CompletedTask);
        Assert.True(second.IsCompleted);
        Assert.False(second.Result);                                               // zaten meşgul: çalışmaz
        gate.SetResult(true);
        WaitUntil(() => run.IsCompleted);
        Assert.True(run.Result);
        Assert.False(button.IsBusy);

        var failing = button.RunBusyAsync(() => Task.FromException(new InvalidOperationException("boom")));
        WaitUntil(() => failing.IsCompleted);
        Assert.True(failing.IsFaulted);
        Assert.False(button.IsBusy);                                               // hata da düğmeyi serbest bırakır
        Assert.IsType<ArgumentNullException>(Record.Exception((Action)(() => { _ = button.RunBusyAsync(null!); })));       // hemen, görevde değil
    });

    [Fact]
    public void Stats_follow_the_default_unless_set() => Sta.Run(() =>
    {
        var previous = TwStat.DefaultAnimateValue;
        try
        {
            using var stat = new TwStat();
            using var own = new TwStat { AnimateValue = true };
            TwStat.DefaultAnimateValue = false;
            Assert.False(stat.AnimateValue);
            Assert.True(own.AnimateValue);
            TwStat.DefaultAnimateValue = true;
            Assert.True(stat.AnimateValue);
        }
        finally
        {
            TwStat.DefaultAnimateValue = previous;
        }
    });

    [Fact]
    public void Timings_can_be_set_and_reset() => Sta.Run(() =>
    {
        try
        {
            Assert.Equal(600, TwMotion.Timings.ChartIntro);
            Assert.Equal(35, TwMotion.Timings.ChartStagger);
            Assert.Equal(700, TwMotion.Timings.AttentionPulse);
            TwMotion.Timings.ChartStagger = 70;
            TwMotion.Timings.AttentionPulse = 1200;
            Assert.Equal(70, TwMotion.Timings.ChartStagger);
            Assert.Throws<ArgumentOutOfRangeException>(() => TwMotion.Timings.PopupOpen = -1);
        }
        finally
        {
            TwMotion.Timings.Reset();
        }
        Assert.Equal(35, TwMotion.Timings.ChartStagger);
        Assert.Equal(700, TwMotion.Timings.AttentionPulse);
    });

    [Fact]
    public void The_motion_preference_round_trips() => Sta.Run(() =>
    {
        var previous = TwMotion.Preference;
        try
        {
            TwMotion.Preference = TwMotionPreference.Off;
            Assert.False(TwMotion.Enabled);
            Assert.Equal(TwMotionPreference.Off, TwMotion.Preference);
            TwMotion.Preference = TwMotionPreference.On;
            Assert.True(TwMotion.Enabled);
            TwMotion.Preference = TwMotionPreference.System;
            Assert.Equal(TwMotionPreference.System, TwMotion.Preference);
            Assert.Throws<ArgumentOutOfRangeException>(() => TwMotion.Preference = (TwMotionPreference)9);
        }
        finally
        {
            TwMotion.Preference = previous;
        }
    });

    [Fact]
    public void A_loading_list_shows_skeletons_and_ignores_input() => Sta.Run(() => WithMotion(() =>
    {
        using var form = ShowForm(new Size(300, 300));
        var list = new TwDataList { Dock = DockStyle.Fill, ItemCount = 20 };
        form.Controls.Add(list);
        Application.DoEvents();
        int drawn = 0;
        list.DrawItem += (_, _) => drawn++;

        list.IsLoading = true;
        list.Refresh();
        Assert.Equal(0, drawn);                                                    // satırlar değil iskeletler
        list.Focus();
        var handler = typeof(Control).GetMethod("OnMouseDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        handler.Invoke(list, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 20, 20, 0) });
        Assert.Equal(-1, list.SelectedIndex);

        list.IsLoading = false;
        list.Refresh();
        Assert.True(drawn > 0);
        form.Close();
    }));
}
