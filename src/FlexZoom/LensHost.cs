using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace FlexZoom;

internal sealed record LensDiagnostics(bool Enabled, bool RawInputActive, int WorkerThreadId, long InputEvents,
    long InputUpdates, long TimerRefreshes, long PositionMoves, long MonitorQueries, double MeanInputDispatchMs, double MaxInputDispatchMs);

// Every magnifier HWND and native API call lives on this dedicated STA dispatcher.
// Settings rendering, file writes, and modal UI cannot stall pointer tracking.
internal sealed class Lens : IDisposable
{
    private readonly Thread thread;
    private readonly Dispatcher owner = Dispatcher.CurrentDispatcher;
    private readonly Dispatcher worker;
    private readonly LensRenderer renderer;
    private bool disposed;
    public event Action<string>? Failed;
    public bool Enabled => !disposed && worker.Invoke(() => renderer.Enabled);
    internal nint Host => renderer.Host;
    internal nint Magnifier => renderer.Magnifier;
    internal LensDiagnostics Diagnostics => worker.Invoke(() => renderer.Diagnostics);

    public Lens()
    {
        var started = new TaskCompletionSource<(Dispatcher, LensRenderer)>(TaskCreationOptions.RunContinuationsAsynchronously);
        thread = new Thread(() =>
        {
            LensRenderer? engine = null;
            try
            {
                engine = new LensRenderer();
                engine.Failed += message => owner.BeginInvoke(() => { if (!disposed) Failed?.Invoke(message); });
                started.SetResult((Dispatcher.CurrentDispatcher, engine));
                Dispatcher.Run();
            }
            catch (Exception ex) { if (!started.TrySetException(ex)) owner.BeginInvoke(() => { if (!disposed) Failed?.Invoke("Lens stopped: " + ex.Message); }); }
            finally { engine?.Dispose(); }
        }) { IsBackground = true, Name = "Flex Zoom lens" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        (worker, renderer) = started.Task.GetAwaiter().GetResult();
    }
    public void Apply(Settings settings)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var copy = settings with { };
        worker.Invoke(() => renderer.Apply(copy));
    }
    public void SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        worker.Invoke(() => renderer.SetEnabled(enabled));
    }
    internal static Native.Rect SourceFor(Native.Point cursor, Native.Rect lens, Native.Rect monitor, double zoom, int border)
        => LensRenderer.SourceFor(cursor, lens, monitor, zoom, border);
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        worker.InvokeShutdown();
        thread.Join();
    }
}
