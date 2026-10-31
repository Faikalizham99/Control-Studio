using ControlStudio.Core.Widgets;

namespace ControlStudio.App;

/// <summary>Single-window, latest-request routing. Navigation runs on the UI dispatcher.</summary>
internal sealed class WidgetTapRouter(Func<bool> ready, Func<WidgetTapLink, CancellationToken, Task<bool>> open, Func<Exception, Task> failed) : IDisposable
{
    private WidgetTapLink? pending;
    private CancellationTokenSource? operation;
    private long revision;
    private bool active, running, disposed;
    public bool HasPending => pending is not null;
    public bool Submit(string? url)
    {
        if (disposed || !WidgetTapLink.TryParse(url, out var link)) return false;
        if (pending == link) return true;
        pending = link; revision++; operation?.Cancel(); return true;
    }
    public void SetActive(bool value)
    {
        active = value && !disposed;
        if (!active) operation?.Cancel();
    }
    public async Task ProcessAsync()
    {
        if (disposed || running || !active) return;
        running = true;
        try
        {
            while (!disposed && active && pending is { } request && ready())
            {
                var current = revision;
                using var cancel = new CancellationTokenSource(); operation = cancel;
                try
                {
                    if (!await open(request, cancel.Token)) break;
                    if (!cancel.IsCancellationRequested && current == revision) pending = null;
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
                catch (Exception ex)
                {
                    if (current == revision && !cancel.IsCancellationRequested)
                    {
                        pending = null;
                        try { await failed(ex); } catch { /* Native UI callbacks must not throw. */ }
                    }
                }
                finally { operation = null; }
            }
        }
        finally { running = false; }
    }
    public void Dispose() { disposed = true; active = false; pending = null; revision++; operation?.Cancel(); }
}
