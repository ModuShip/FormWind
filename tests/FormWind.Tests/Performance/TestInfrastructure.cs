using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;

namespace FormWind.Tests.Performance;

/// <summary>
/// Varsayılan test çalıştırmasında atlanan test. <c>FORMWIND_PERF=1</c> ortam değişkeniyle çalışır.
/// </summary>
public sealed class PerformanceFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "FORMWIND_PERF";

    public PerformanceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentVariable) != "1")
            Skip = $"Performans testi. Çalıştırmak için {EnvironmentVariable}=1 ortam değişkenini ayarlayın.";
    }
}

/// <summary><see cref="PerformanceFactAttribute"/> gibi, veri satırlı testler için.</summary>
public sealed class PerformanceTheoryAttribute : TheoryAttribute
{
    public PerformanceTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable(PerformanceFactAttribute.EnvironmentVariable) != "1")
            Skip = $"Performans testi. Çalıştırmak için {PerformanceFactAttribute.EnvironmentVariable}=1 ortam değişkenini ayarlayın.";
    }
}

/// <summary>Kodu WinForms'un beklediği STA iş parçacığında çalıştırır.</summary>
internal static class Sta
{
    public static void Run(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}

/// <summary>Sürecin GDI ve USER nesne sayıları.</summary>
internal static class GuiResources
{
    private const uint GdiObjects = 0;
    private const uint UserObjects = 1;

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);

    /// <param name="collectGarbage">
    /// true: sonlandırıcısı bekleyen nesneler serbest bırakıldıktan sonra sayar (gerçek sızıntı).
    /// false: o anki sayım; dispose edilmeyip GC'ye kalan nesneleri de gösterir.
    /// </param>
    public static (int Gdi, int User) Snapshot(bool collectGarbage)
    {
        if (collectGarbage)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        using var process = Process.GetCurrentProcess();
        return ((int)GetGuiResources(process.Handle, GdiObjects), (int)GetGuiResources(process.Handle, UserObjects));
    }
}
