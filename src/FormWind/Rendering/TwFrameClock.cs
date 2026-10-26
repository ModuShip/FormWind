using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// The frame clock every FormWind animation runs on: one per UI thread, about 60 frames per second. A thread-pool timer
/// posts a frame message to a hidden window of the UI thread. Windows hands out <c>WM_TIMER</c> only when no paint is
/// pending, so a control that keeps repainting starved the timer-driven animations; a posted message comes before painting
/// instead. At most one frame waits in the queue and the next one is posted only a moment after the last one ended, so
/// painting and input still get their turn between frames.
/// </summary>
internal static class TwFrameClock
{
    [ThreadStatic]
    private static Pump? _pump;

    /// <summary>Number of running frame timers on this thread (for tests).</summary>
    internal static int ActiveCount => _pump?.Count ?? 0;

    /// <summary>Milliseconds on a monotonic clock (not affected by changes of the system time).</summary>
    internal static double NowMs => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    internal static void Add(TwFrameTimer timer) => (_pump ??= new Pump()).Add(timer);

    internal static void Remove(TwFrameTimer timer) => _pump?.Remove(timer);

    /// <summary>Runs one frame now (tests; normally the posted message does).</summary>
    internal static void Frame() => _pump?.Frame();

    private sealed class Pump : NativeWindow
    {
        private const int WmFrame = 0x8000 + 0x2F1;   // WM_APP + …
        private const int PeriodMs = 8;
        // Sistem zamanlayıcısı 15,6 ms adımla çalışır: 8 ms'lik dönem her adımda, yüksek çözünürlükte de ~60 kare/sn verir.
        private const double MinFrameGapMs = 14;
        // Kareler arasında boyamaya ve girdiye kalan en kısa süre.
        private const double IdleGapMs = 2;

        private readonly List<TwFrameTimer> _timers = new List<TwFrameTimer>();
        private readonly System.Threading.Timer _clock;
        private int _pending;
        private long _lastPostTicks, _lastEndTicks;
        private bool _running;

        public Pump()
        {
            CreateHandle(new CreateParams { Parent = new IntPtr(-3) });   // HWND_MESSAGE: yalnızca mesaj alan pencere
            _clock = new System.Threading.Timer(_ => Post(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public int Count => _timers.Count;

        public void Add(TwFrameTimer timer)
        {
            if (_timers.Contains(timer)) return;
            _timers.Add(timer);
            if (_running) return;
            _running = true;
            _clock.Change(PeriodMs, PeriodMs);
        }

        public void Remove(TwFrameTimer timer)
        {
            _timers.Remove(timer);
            if (_timers.Count == 0) Halt();
        }

        private void Halt()
        {
            if (!_running) return;
            _running = false;
            _clock.Change(Timeout.Infinite, Timeout.Infinite);
        }

        // İş parçacığı havuzunda çalışır.
        private void Post()
        {
            if (Volatile.Read(ref _pending) != 0) return;
            long now = Stopwatch.GetTimestamp();
            if (ToMs(now - Volatile.Read(ref _lastPostTicks)) < MinFrameGapMs) return;
            if (ToMs(now - Volatile.Read(ref _lastEndTicks)) < IdleGapMs) return;
            var handle = Handle;
            if (handle == IntPtr.Zero || Interlocked.Exchange(ref _pending, 1) != 0) return;
            Volatile.Write(ref _lastPostTicks, now);
            // Pencere yok olduysa (iş parçacığı bitti) saat durur.
            if (!PostMessage(handle, WmFrame, IntPtr.Zero, IntPtr.Zero))
            {
                Volatile.Write(ref _pending, 0);
                _clock.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg != WmFrame)
            {
                base.WndProc(ref m);
                return;
            }
            try
            {
                Frame();
            }
            finally
            {
                // Bekleyen kare kare bittikten sonra serbest kalır: uzun süren bir kare arka arkaya kareler doğurmaz.
                Volatile.Write(ref _lastEndTicks, Stopwatch.GetTimestamp());
                Volatile.Write(ref _pending, 0);
            }
        }

        public void Frame()
        {
            if (_timers.Count == 0)
            {
                Halt();
                return;
            }
            double now = NowMs;
            // Tick geri çağrısı zamanlayıcı ekleyip çıkarabilir; kopya üzerinde dönülür.
            foreach (var timer in _timers.ToArray())
                if (timer.Enabled) timer.OnFrame(now);
        }

        private static double ToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}

/// <summary>
/// A timer on the shared frame clock (<see cref="TwFrameClock"/>), used like <see cref="System.Windows.Forms.Timer"/>:
/// <see cref="Tick"/> is raised on the UI thread that started it, at most once a frame and about every
/// <see cref="Interval"/> ms.
/// </summary>
internal sealed class TwFrameTimer : IDisposable
{
    // Kareler ~16 ms arayla gelir; aralığı birkaç ms kaçıran bir kare beklenmez.
    private const double SlackMs = 4;

    private double _last;
    private bool _disposed;

    /// <summary>Wanted time between ticks (ms); 16 or less = every frame.</summary>
    public int Interval { get; set; } = 16;

    public event EventHandler? Tick;

    public bool Enabled { get; private set; }

    public void Start()
    {
        if (_disposed || Enabled) return;
        Enabled = true;
        _last = TwFrameClock.NowMs;
        TwFrameClock.Add(this);
    }

    public void Stop()
    {
        if (!Enabled) return;
        Enabled = false;
        TwFrameClock.Remove(this);
    }

    internal void OnFrame(double now)
    {
        if (now - _last < Interval - SlackMs) return;
        _last = now;
        Tick?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Stop();
        _disposed = true;
    }
}
