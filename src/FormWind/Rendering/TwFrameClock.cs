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

    /// <summary>The frame timers on the clock of this thread now (parked ones are not on it).</summary>
    internal static TwFrameTimer[] ActiveTimers => _pump?.Snapshot() ?? Array.Empty<TwFrameTimer>();

    /// <summary>Milliseconds on a monotonic clock (not affected by changes of the system time).</summary>
    internal static double NowMs => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    internal static void Add(TwFrameTimer timer) => (_pump ??= new Pump()).Add(timer);

    internal static void Remove(TwFrameTimer timer) => _pump?.Remove(timer);

    /// <summary>Runs one frame now (tests; normally the posted message does).</summary>
    internal static void Frame() => _pump?.Frame();

    /// <summary>The message the clock posts to its hidden window for every frame (WM_APP + …).</summary>
    internal const int FrameMessage = 0x8000 + 0x2F1;

    private sealed class Pump : NativeWindow
    {
        private const int WmFrame = FrameMessage;
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

        public TwFrameTimer[] Snapshot() => _timers.ToArray();

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
            if (_timers.Count == 0) Halt();
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

    // Kaydırılıp görünür alanın dışında kalma her karede değil bu aralıkla denetlenir (ata zinciri boyunca ekran dönüşümü).
    internal const double ViewCheckMs = 200;

    private double _last, _lastViewCheck;
    private bool _disposed;
    private Control? _owner;
    private bool _watchingOwner;
    // Park edilmişken konumu ya da boyu değişince yeniden denetlenen kontroller: sahibi ve ataları (kaydırma onları taşır).
    private readonly List<Control> _watchedChain = new List<Control>();

    /// <summary>Wanted time between ticks (ms); 16 or less = every frame.</summary>
    public int Interval { get; set; } = 16;

    /// <summary>What runs on this timer (<see cref="TwDiagnostics.ActiveFrameTimers"/>), such as <c>"grid-pulse"</c>.</summary>
    public string Source { get; set; } = "timer";

    /// <summary>The control the timer animates, for diagnostics, when it has no <see cref="Owner"/> (a finite animation).</summary>
    public Control? Subject { get; set; }

    /// <summary>
    /// The control a never-ending animation (pulse, spin, shimmer) draws on. While it is not shown (hidden itself or on a
    /// hidden page, no window handle, disposed, or scrolled out of its parents' visible area) the timer leaves the clock and
    /// the application can go idle; it comes back when the control is shown again. <c>null</c> = always run (finite
    /// animations, which must reach their end).
    /// </summary>
    public Control? Owner
    {
        get => _owner;
        set
        {
            if (ReferenceEquals(_owner, value)) return;
            UnwatchOwner();
            _owner = value;
            if (Enabled && !Parked && !IsShowing(value)) Park();
            else if (Parked)
            {
                // Yeni sahip görünmüyor ya da kaydırılıp dışarıda kaldıysa park sürer ve onu izler.
                Resume();
                if (Parked) WatchOwner();
            }
        }
    }

    public event EventHandler? Tick;

    public bool Enabled { get; private set; }

    /// <summary>Whether the timer is enabled but off the clock because its <see cref="Owner"/> is not shown.</summary>
    internal bool Parked { get; private set; }

    public void Start()
    {
        if (_disposed || Enabled) return;
        Enabled = true;
        _last = TwFrameClock.NowMs;
        if (IsShowing(_owner)) TwFrameClock.Add(this);
        else Park();
    }

    public void Stop()
    {
        if (!Enabled) return;
        Enabled = false;
        UnwatchOwner();
        if (Parked) Parked = false;
        else TwFrameClock.Remove(this);
    }

    internal void OnFrame(double now)
    {
        if (!IsShowing(_owner) || IsScrolledAway(now))
        {
            Park();
            return;
        }
        if (now - _last < Interval - SlackMs) return;
        _last = now;
        Tick?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Stop();
        _disposed = true;
    }

    // Visible is false also when a parent (a page) is hidden; WinForms raises VisibleChanged on the children then.
    private static bool IsShowing(Control? owner) =>
        owner is null || owner is { IsDisposed: false, IsHandleCreated: true, Visible: true };

    private bool IsScrolledAway(double now)
    {
        if (_owner is null || now - _lastViewCheck < ViewCheckMs) return false;
        _lastViewCheck = now;
        return !IsInView(_owner);
    }

    // Ebeveynlerinin görünür alanıyla kesişiyor mu (formun kendisi ekran dışında olsa da sayılır).
    internal static bool IsInView(Control owner) =>
        owner.Parent is null || !TwTransition.VisibleScreenBounds(owner, owner.RectangleToScreen(owner.ClientRectangle)).IsEmpty;

    private void Park()
    {
        if (Parked) return;
        Parked = true;
        TwFrameClock.Remove(this);
        WatchOwner();
    }

    private void Resume()
    {
        if (!Parked || !Enabled || !IsShowing(_owner) || _owner != null && !IsInView(_owner)) return;
        Parked = false;
        UnwatchOwner();
        _last = _lastViewCheck = TwFrameClock.NowMs;
        TwFrameClock.Add(this);
    }

    private void WatchOwner()
    {
        if (_watchingOwner || _owner is null) return;
        _watchingOwner = true;
        _owner.VisibleChanged += OnOwnerChanged;
        _owner.HandleCreated += OnOwnerChanged;
        _owner.Disposed += OnOwnerDisposed;
        WatchChain();
    }

    private void UnwatchOwner()
    {
        if (!_watchingOwner || _owner is null) return;
        _watchingOwner = false;
        _owner.VisibleChanged -= OnOwnerChanged;
        _owner.HandleCreated -= OnOwnerChanged;
        _owner.Disposed -= OnOwnerDisposed;
        UnwatchChain();
    }

    private void WatchChain()
    {
        for (var control = _owner; control != null; control = control.Parent)
        {
            control.LocationChanged += OnOwnerChanged;
            control.SizeChanged += OnOwnerChanged;
            // Zincirin herhangi bir halkası başka bir ebeveyne taşınırsa zincir yeniden kurulur.
            control.ParentChanged += OnOwnerParentChanged;
            _watchedChain.Add(control);
        }
    }

    private void UnwatchChain()
    {
        foreach (var control in _watchedChain)
        {
            control.LocationChanged -= OnOwnerChanged;
            control.SizeChanged -= OnOwnerChanged;
            control.ParentChanged -= OnOwnerParentChanged;
        }
        _watchedChain.Clear();
    }

    private void OnOwnerParentChanged(object? sender, EventArgs e)
    {
        UnwatchChain();
        WatchChain();
        Resume();
    }

    private void OnOwnerChanged(object? sender, EventArgs e) => Resume();

    private void OnOwnerDisposed(object? sender, EventArgs e) => Stop();
}
