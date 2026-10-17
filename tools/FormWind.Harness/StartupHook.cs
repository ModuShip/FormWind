using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Windows.Forms;

/// <summary>
/// .NET startup hook giriş noktası (global namespace'te <c>StartupHook.Initialize</c> olmalı). Uygulamanın Main'inden
/// önce, ana iş parçacığında çalışır; o an WinForms henüz başlamadığından harness ilk form açılınca o formun iş
/// parçacığında kurulur. FormWind.dll uygulamanın klasöründe değilse bu derlemenin klasöründen yüklenir.
/// </summary>
internal static class StartupHook
{
    public static void Initialize()
    {
        if (Environment.GetEnvironmentVariable("FORMWIND_DISABLE") == "1") return;

        string hookDirectory = Path.GetDirectoryName(typeof(StartupHook).Assembly.Location)!;
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string candidate = Path.Combine(hookDirectory, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };

        // UI iş parçacığına ilk form üzerinden geçilir; formun handle'ı oluşmadan BeginInvoke yapılamaz.
        var watcher = new Thread(() =>
        {
            while (true)
            {
                Thread.Sleep(200);
                Form? first = null;
                try
                {
                    foreach (Form form in Application.OpenForms)
                    {
                        if (form.IsHandleCreated && !form.IsDisposed) { first = form; break; }
                    }
                }
                catch (InvalidOperationException)
                {
                    // Koleksiyon UI iş parçacığında değişti; bir sonraki turda tekrar denenir.
                }

                if (first is null) continue;
                try
                {
                    first.BeginInvoke(new Action(FormWind.Harness.FormWindHarness.Install));
                    return;
                }
                catch (InvalidOperationException)
                {
                    // Form tam o an kapandı.
                }
            }
        })
        {
            IsBackground = true,
            Name = "FormWind harness watcher",
        };
        watcher.Start();
    }
}
