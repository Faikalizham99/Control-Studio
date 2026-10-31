using ControlStudio.Core.Widgets;
using Microsoft.Maui.Dispatching;

namespace ControlStudio.App;

/// <summary>Optional per-window routing: ordinary launches never construct a router or timer.</summary>
internal sealed class WidgetTapSession(Func<WidgetTapRouter> createRouter, Func<IDispatcherTimer> createTimer, Action<Exception> failed) : IDisposable
{
    private WidgetTapRouter? router;
    private IDispatcherTimer? timer;
    private string? pending;
    private bool active, disposed;
    private int generation;
    public bool HasPending => !disposed && (pending is not null || router?.HasPending == true);

    public bool Submit(string? url)
    {
        if (disposed || !WidgetTapLink.TryParse(url, out _)) return false;
        try
        {
            pending = url;
            if (router is not null) { router.Submit(url); pending = null; }
            return true;
        }
        catch (Exception ex) { Reset(ex); return false; }
    }

    public void SetActive(bool value)
    {
        if (disposed) return;
        active = value;
        try { router?.SetActive(value); if (!value) timer?.Stop(); }
        catch (Exception ex) { Reset(ex); }
    }

    public async Task ProcessAsync()
    {
        if (disposed || !active || !HasPending) return;
        var currentGeneration = generation;
        try
        {
            if (router is null)
            {
                // Assign each owned resource before the next fallible operation, so
                // partial construction and native timer failures can be cleaned up.
                router = createRouter(); timer = createTimer();
                timer.Interval = TimeSpan.FromMilliseconds(250); timer.IsRepeating = true;
                timer.Tick += Tick;
            }
            if (pending is { } url) { router.Submit(url); pending = null; }
            router.SetActive(active);
            await router.ProcessAsync();
            if (currentGeneration != generation) return;
            if (disposed || !active || !HasPending) timer?.Stop(); else timer?.Start();
        }
        catch (Exception ex) { if (currentGeneration == generation) Reset(ex); }
    }

    private void Tick(object? sender, EventArgs args) => _ = ProcessAsync();
    private void Reset(Exception error)
    {
        pending = null; Release();
        try { failed(error); } catch { /* Logging cannot abort a native lifecycle callback. */ }
    }
    private void Release()
    {
        generation++;
        var previousRouter = router; var previousTimer = timer; router = null; timer = null;
        try { previousRouter?.Dispose(); } catch { }
        if (previousTimer is not null)
        {
            try { previousTimer.Tick -= Tick; } catch { }
            try { previousTimer.Stop(); } catch { }
        }
    }
    public void Dispose() { if (disposed) return; disposed = true; active = false; pending = null; Release(); }
}
