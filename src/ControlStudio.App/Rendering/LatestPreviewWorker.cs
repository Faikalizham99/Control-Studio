namespace ControlStudio.App.Rendering;

/// <summary>One worker, one replaceable request and one replaceable completed
/// image. Reset cancels a lifetime and releases native state on the same worker.
/// Present/reset/request are called by the UI; render/release run off the UI.</summary>
internal sealed class LatestPreviewWorker<TRequest, TResult>(Action<Action> post,
    Func<TRequest, CancellationToken, TResult> render, Action release,
    Action<TRequest, TResult> present, Action<TRequest, Exception> failed)
    where TRequest : class where TResult : class, IDisposable
{
    private sealed record Request(TRequest Value, int Generation, long Revision);
    private sealed record Completed(Request Request, TResult? Result, Exception? Error);
    private readonly object gate = new();
    private Request? pending;
    private Completed? ready;
    private CancellationTokenSource? cancellation;
    private bool running, reset, posted, deliveryFailed;
    private int generation;
    private long revision;
    private Exception? releaseError;
    private TaskCompletionSource idle = Finished();
    public Task WhenIdle { get { lock (gate) return idle.Task; } }
    public bool DeliveryFailed { get { lock (gate) return deliveryFailed; } }
    private static TaskCompletionSource Finished() { var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); done.SetResult(); return done; }
    public void RequestRender(TRequest request)
    {
        lock (gate) { deliveryFailed = false; pending = new(request, generation, ++revision); Start(); }
    }
    public void Clear()
    {
        lock (gate)
        {
            generation++; pending = null; deliveryFailed = false; cancellation?.Cancel();
            ready?.Result?.Dispose(); ready = null; reset = true; Start();
        }
    }
    private void Start()
    {
        if (running) return;
        running = true; idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(Process);
    }
    private void Process()
    {
        while (true)
        {
            Request? request; bool clearing;
            lock (gate)
            {
                clearing = reset; reset = false;
                request = clearing ? null : pending;
                if (!clearing) pending = null;
                if (!clearing && request is null) { running = false; idle.TrySetResult(); return; }
                if (request is not null) cancellation = new();
            }
            if (clearing)
            {
                try { release(); } catch (Exception ex) { releaseError = ex; }
                continue;
            }
            TResult? result = null; Exception? error = null;
            try
            {
                if (releaseError is { } previous) { releaseError = null; throw new IOException("The preview could not reset. Try the edit again.", previous); }
                result = render(request!.Value, cancellation!.Token);
            }
            catch (Exception ex) { error = ex; }
            lock (gate)
            {
                cancellation!.Dispose(); cancellation = null;
                if (request!.Generation != generation) { result?.Dispose(); continue; }
                ready?.Result?.Dispose(); ready = new(request, result, error); deliveryFailed = false;
                if (!posted)
                {
                    posted = true;
                    // Dispatch is nonblocking; a stalled UI retains only the latest
                    // completed frame, never a growing queue of native images.
                    try { post(Present); }
                    catch { posted = false; deliveryFailed = true; ready?.Result?.Dispose(); ready = null; }
                }
            }
        }
    }
    private void Present()
    {
        Completed? item;
        lock (gate) { posted = false; item = ready; ready = null; }
        if (item is null) return;
        if (item.Error is { } error)
        {
            // A failed superseded draft must not clear a newer working preview.
            lock (gate) if (item.Request.Generation != generation || item.Request.Revision != revision) return;
            failed(item.Request.Value, error);
        }
        else if (item.Result is { } image) present(item.Request.Value, image);
    }
}
