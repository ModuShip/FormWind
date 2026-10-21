using System.Collections.Concurrent;
using System.Windows.Forms;

namespace FormWind.Mcp;

/// <summary>
/// One STA thread that runs every WinForms job in order. MCP requests arrive on thread-pool threads and may overlap; controls,
/// fonts and GDI handles must stay on one UI thread.
/// </summary>
public sealed class StaThread : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    public StaThread()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "formwind-render" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> InvokeAsync<T>(Func<T> job, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(cancellationToken);
                return;
            }
            try
            {
                completion.TrySetResult(job());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }, cancellationToken);
        return completion.Task;
    }

    private void Run()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        foreach (var job in _queue.GetConsumingEnumerable()) job();
    }

    public void Dispose() => _queue.CompleteAdding();
}
