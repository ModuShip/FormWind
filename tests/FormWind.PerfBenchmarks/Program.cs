using System;
using BenchmarkDotNet.Running;

namespace FormWind.PerfBenchmarks;

internal static class Program
{
    // The benchmarks create windows: the generated benchmark process runs them on an STA main thread because every
    // benchmark method carries [STAThread]; this host process only selects and starts them.
    [STAThread]
    private static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
