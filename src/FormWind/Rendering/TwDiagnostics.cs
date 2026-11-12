using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;

namespace FormWind;

/// <summary>
/// What FormWind is doing behind the scenes, for an application's own performance checks and tests: the frame timers
/// running on the UI thread, the shadow cache and the timings of the page transitions.
/// <code>
/// foreach (var timer in TwDiagnostics.ActiveFrameTimers) Debug.WriteLine(timer);
/// TwDiagnostics.TransitionCompleted += (_, e) => Debug.WriteLine($"{e.Kind}: capture {e.CaptureMs:0.0} ms ({e.CaptureSource})");
/// </code>
/// </summary>
[Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
public static class TwDiagnostics
{
    /// <summary>
    /// The frame timers running on the calling thread's frame clock now. An idle application has none; a timer listed here
    /// while nothing moves on screen keeps the application from going idle.
    /// </summary>
    public static IReadOnlyList<TwFrameTimerInfo> ActiveFrameTimers
    {
        get
        {
            var timers = TwFrameClock.ActiveTimers;
            var result = new TwFrameTimerInfo[timers.Length];
            for (int i = 0; i < timers.Length; i++)
                result[i] = new TwFrameTimerInfo(timers[i].Source, timers[i].Owner ?? timers[i].Subject, timers[i].Interval, timers[i].Owner != null);
            return result;
        }
    }

    /// <summary>Whether an animation runs on the calling thread: a frame timer is on the clock or a transition picture is fading.</summary>
    public static bool IsAnimating => TwFrameClock.ActiveCount > 0 || TwFadeOut.ActiveCount > 0;

    /// <summary>Size and use of the cache of blurred shadow pictures (shared by all threads).</summary>
    public static TwShadowCacheStats ShadowCache => TwShadowRenderer.Stats;

    /// <summary>
    /// Raised on the UI thread when a transition (<see cref="TwTransition.CrossFade(Control, Action)"/>) has applied its change and its
    /// fade starts, with the time each step took. The fade itself runs afterwards for the transition's duration.
    /// </summary>
    public static event EventHandler<TwTransitionEventArgs>? TransitionCompleted;

    internal static bool HasTransitionListeners => TransitionCompleted != null;

    internal static void OnTransition(TwTransitionEventArgs e)
    {
        try
        {
            TransitionCompleted?.Invoke(null, e);
        }
        catch (Exception ex) when (!TwErrors.IsCritical(ex))
        {
            // A diagnostics handler must not break the transition that reports to it.
            TwErrors.Trace("transition diagnostics", ex);
        }
    }
}

/// <summary>A frame timer running on the UI thread (<see cref="TwDiagnostics.ActiveFrameTimers"/>).</summary>
[Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
public sealed class TwFrameTimerInfo
{
    internal TwFrameTimerInfo(string source, Control? control, int intervalMs, bool pausesWhenHidden)
    {
        Source = source;
        Control = control;
        IntervalMs = intervalMs;
        PausesWhenHidden = pausesWhenHidden;
    }

    /// <summary>What runs on the timer: <c>grid-pulse</c>, <c>list-pulse</c>, <c>progress</c>, <c>animate</c>, <c>animator</c>, <c>tween</c>, <c>outset</c>, <c>after-paint</c>.</summary>
    public string Source { get; }

    /// <summary>The control the timer animates; <c>null</c> when it serves several (the shared animators).</summary>
    public Control? Control { get; }

    /// <summary>Wanted time between ticks (ms).</summary>
    public int IntervalMs { get; }

    /// <summary>Whether the timer leaves the clock by itself while its control is not shown (never-ending animations).</summary>
    public bool PausesWhenHidden { get; }

    public override string ToString() => Control is null ? Source : $"{Source} ({Control.GetType().Name} {Control.Name})";
}

/// <summary>Size and use of the shadow cache (<see cref="TwDiagnostics.ShadowCache"/>).</summary>
[Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
public readonly struct TwShadowCacheStats
{
    internal TwShadowCacheStats(int count, long bytes, long hits, long misses)
    {
        Count = count;
        Bytes = bytes;
        Hits = hits;
        Misses = misses;
    }

    /// <summary>Number of cached pictures.</summary>
    public int Count { get; }

    /// <summary>Memory of the cached pictures (bytes).</summary>
    public long Bytes { get; }

    /// <summary>Shadows drawn from a cached picture since the start.</summary>
    public long Hits { get; }

    /// <summary>Shadows that needed a new blurred picture since the start.</summary>
    public long Misses { get; }

    public override string ToString() => $"{Count} pictures, {Bytes / 1024} KB, {Hits} hits, {Misses} misses";
}

/// <summary>Timings of a transition (<see cref="TwDiagnostics.TransitionCompleted"/>).</summary>
[Experimental(TwExperimental.DiagnosticId, UrlFormat = TwExperimental.UrlFormat)]
public sealed class TwTransitionEventArgs : EventArgs
{
    internal TwTransitionEventArgs(Control host, string kind, string captureSource, double captureMs, double coverMs, double changeMs, double paintMs, double totalMs, int durationMs)
    {
        Host = host;
        Kind = kind;
        CaptureSource = captureSource;
        CaptureMs = captureMs;
        CoverMs = coverMs;
        ChangeMs = changeMs;
        PaintMs = paintMs;
        TotalMs = totalMs;
        DurationMs = durationMs;
    }

    /// <summary>The part of the window that changed.</summary>
    public Control Host { get; }

    /// <summary>The transition: <c>CrossFade</c>.</summary>
    public string Kind { get; }

    /// <summary>Where the picture of the old look came from: <c>Screen</c> (fast), <c>PrintWindow</c>, <c>DrawToBitmap</c> or <c>None</c>.</summary>
    public string CaptureSource { get; }

    /// <summary>Time to take the picture of the old look (ms).</summary>
    public double CaptureMs { get; }

    /// <summary>Time to put the picture on screen over the part (ms).</summary>
    public double CoverMs { get; }

    /// <summary>Time the application's change took (ms).</summary>
    public double ChangeMs { get; }

    /// <summary>Time to paint the new look under the picture (ms).</summary>
    public double PaintMs { get; }

    /// <summary>Time from the call until the fade started (ms): the time the UI thread was held up.</summary>
    public double TotalMs { get; }

    /// <summary>Duration of the fade that follows (ms); 0 when the change was applied without a fade.</summary>
    public int DurationMs { get; }
}
