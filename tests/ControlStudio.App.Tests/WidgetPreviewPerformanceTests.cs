using System.Collections.Concurrent;
using ControlStudio.App.Rendering;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class WidgetPreviewPerformanceTests
{
    private sealed record Draft(int Number);
    private sealed class Image(int number) : IDisposable
    {
        public int Number { get; } = number;
        public int Disposals { get; private set; }
        public void Dispose() { Assert.Equal(0, Disposals); Disposals++; }
    }
    private static async Task Idle(LatestPreviewWorker<Draft, Image> worker) => await worker.WhenIdle.WaitAsync(TimeSpan.FromSeconds(15));
    private static void Drain(ConcurrentQueue<Action> queue) { while (queue.TryDequeue(out var action)) action(); }
    [Fact]
    public async Task ASlowFrameKeepsOnlyTheLatestWaitingDraftAndCompletedImage()
    {
        var ui = new ConcurrentQueue<Action>(); var began = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim(); var draws = new ConcurrentQueue<int>(); var images = new ConcurrentQueue<Image>(); var shown = new List<int>();
        var worker = new LatestPreviewWorker<Draft, Image>(ui.Enqueue, (draft, _) =>
        {
            draws.Enqueue(draft.Number);
            if (draft.Number == 1) { began.SetResult(); Assert.True(resume.Wait(TimeSpan.FromSeconds(10))); }
            var image = new Image(draft.Number); images.Enqueue(image); return image;
        }, () => { }, (_, image) => { shown.Add(image.Number); image.Dispose(); }, (_, ex) => throw ex);
        worker.RequestRender(new(1)); await began.Task.WaitAsync(TimeSpan.FromSeconds(10));
        for (var i = 2; i <= 100; i++) worker.RequestRender(new(i));
        resume.Set(); await Idle(worker);
        Assert.Equal(new[] { 1, 100 }, draws); Assert.Single(ui); Drain(ui);
        Assert.Equal(new[] { 100 }, shown); Assert.All(images, image => Assert.Equal(1, image.Disposals));
    }
    [Fact]
    public async Task ContinuousMovementCanPresentCompletedFramesBeforeTheGestureStops()
    {
        var ui = new ConcurrentQueue<Action>(); var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim(); var shown = new List<int>();
        var worker = new LatestPreviewWorker<Draft, Image>(ui.Enqueue, (draft, _) =>
        {
            if (draft.Number == 2) { second.SetResult(); Assert.True(resume.Wait(TimeSpan.FromSeconds(10))); }
            return new(draft.Number);
        }, () => { }, (_, image) => { shown.Add(image.Number); image.Dispose(); }, (_, ex) => throw ex);
        worker.RequestRender(new(1)); await Idle(worker); worker.RequestRender(new(2)); await second.Task.WaitAsync(TimeSpan.FromSeconds(10));
        worker.RequestRender(new(3)); Drain(ui); Assert.Equal(new[] { 1 }, shown);
        resume.Set(); await Idle(worker); Drain(ui); Assert.Equal(new[] { 1, 3 }, shown);
    }
    [Fact]
    public async Task BackgroundResetCancelsInFlightWorkReleasesItSeriallyAndCannotResurrectOldFrames()
    {
        var ui = new ConcurrentQueue<Action>(); var began = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var resume = new ManualResetEventSlim(); var trace = new ConcurrentQueue<string>(); var images = new ConcurrentQueue<Image>(); var shown = new List<int>();
        var worker = new LatestPreviewWorker<Draft, Image>(ui.Enqueue, (draft, token) =>
        {
            trace.Enqueue("render" + draft.Number);
            if (draft.Number == 1) { began.SetResult(); Assert.True(resume.Wait(TimeSpan.FromSeconds(10))); Assert.True(token.IsCancellationRequested); }
            var image = new Image(draft.Number); images.Enqueue(image); return image;
        }, () => trace.Enqueue("release"), (_, image) => { shown.Add(image.Number); image.Dispose(); }, (_, ex) => throw ex);
        worker.RequestRender(new(1)); await began.Task.WaitAsync(TimeSpan.FromSeconds(10));
        worker.Clear(); worker.RequestRender(new(2)); resume.Set(); await Idle(worker); Drain(ui);
        Assert.Equal(new[] { "render1", "release", "render2" }, trace); Assert.Equal(new[] { 2 }, shown);
        Assert.All(images, image => Assert.Equal(1, image.Disposals));
        worker.RequestRender(new(3)); await Idle(worker); worker.Clear(); await Idle(worker); Drain(ui);
        Assert.Equal(new[] { 2 }, shown); Assert.Equal(1, images.Last().Disposals);
        worker.RequestRender(new(4)); await Idle(worker); Drain(ui); Assert.Equal(new[] { 2, 4 }, shown);
    }
    [Fact]
    public async Task FailedDraftsCanRetryAndASupersededFailureDoesNotEraseThePreview()
    {
        var ui = new ConcurrentQueue<Action>(); var failures = new List<int>(); var shown = new List<int>();
        var worker = new LatestPreviewWorker<Draft, Image>(ui.Enqueue, (draft, _) => draft.Number % 2 != 0 ? throw new IOException() : new(draft.Number),
            () => { }, (_, image) => { shown.Add(image.Number); image.Dispose(); }, (draft, _) => failures.Add(draft.Number));
        worker.RequestRender(new(1)); await Idle(worker); worker.RequestRender(new(2)); await Idle(worker); Drain(ui);
        Assert.Empty(failures); Assert.Equal(new[] { 2 }, shown);
        worker.RequestRender(new(3)); await Idle(worker); Drain(ui); Assert.Equal(new[] { 3 }, failures);
        worker.RequestRender(new(4)); await Idle(worker); Drain(ui); Assert.Equal(new[] { 2, 4 }, shown);
    }
    [Fact]
    public async Task RejectedDispatcherAndResetFailureCannotStrandTheWorkerOrLeakImages()
    {
        var ui = new ConcurrentQueue<Action>(); var reject = true; var releaseFails = true; var images = new ConcurrentQueue<Image>(); var failures = 0;
        var worker = new LatestPreviewWorker<Draft, Image>(action => { if (reject) throw new IOException(); ui.Enqueue(action); }, (draft, _) =>
        { var image = new Image(draft.Number); images.Enqueue(image); return image; }, () => { if (releaseFails) { releaseFails = false; throw new IOException(); } },
        (_, image) => image.Dispose(), (_, _) => failures++);
        worker.RequestRender(new(1)); await Idle(worker); Assert.Equal(1, images.Single().Disposals);
        reject = false; worker.Clear(); await Idle(worker); worker.RequestRender(new(2)); await Idle(worker); Drain(ui); Assert.Equal(1, failures);
        worker.RequestRender(new(3)); await Idle(worker); Drain(ui); Assert.All(images, image => Assert.Equal(1, image.Disposals));
    }
    [Fact]
    public async Task AnUndeliveredFrameMustAllowTheSamePixelRequestToRetry()
    {
        var ui = new ConcurrentQueue<Action>(); var reject = true; var shown = new List<int>();
        var worker = new LatestPreviewWorker<Draft, Image>(action => { if (reject) throw new IOException(); ui.Enqueue(action); },
            (draft, _) => new(draft.Number), () => { }, (_, image) => { shown.Add(image.Number); image.Dispose(); }, (_, ex) => throw ex);
        var requested = new Draft(7);
        worker.RequestRender(requested); await Idle(worker);
        reject = false;
        // Production suppresses identical requests. An unacknowledged dispatch
        // must override that suppression instead of leaving a blank/stale image.
        if (worker.DeliveryFailed) worker.RequestRender(requested);
        await Idle(worker); Drain(ui); Assert.Equal(new[] { 7 }, shown); Assert.False(worker.DeliveryFailed);
    }
    [Theory]
    [InlineData(10)] [InlineData(20)] [InlineData(30)]
    public void ManyLayersReuseUnchangedRastersInsteadOfCyclingTheEntireCache(int count)
    {
        using var cache = new PreviewImageCache<int, Image>(64L * 1024 * 1024); var created = 0; var owned = new List<Image>();
        for (var frame = 0; frame <= 60; frame++)
        {
            if (frame > 0) cache.Remove(count / 2);
            for (var layer = 0; layer < count; layer++)
                if (!cache.TryGet(layer, out _)) { var image = new Image(++created); owned.Add(image); Assert.True(cache.TryStore(layer, image, 2 * 1024 * 1024)); }
            Assert.True(cache.Bytes <= 64L * 1024 * 1024);
        }
        Assert.Equal(count + 60, created); cache.Dispose(); Assert.All(owned, image => Assert.Equal(1, image.Disposals));
    }
    [Fact]
    public void OverBudgetScansKeepUsefulEntriesAndOverflowImagesRemainFrameOwned()
    {
        using var cache = new PreviewImageCache<int, Image>(8); var hits = 0; var images = new List<Image>();
        for (var frame = 0; frame < 10; frame++) for (var layer = 0; layer < 30; layer++)
        {
            if (cache.TryGet(layer, out _)) { hits++; continue; }
            var image = new Image(layer); images.Add(image); if (!cache.TryStore(layer, image, 4)) image.Dispose();
        }
        Assert.Equal(18, hits); Assert.Equal(8, cache.Bytes); cache.Remove(0); Assert.Equal(4, cache.Bytes);
        cache.Dispose(); Assert.All(images, image => Assert.Equal(1, image.Disposals));
    }
    [Fact]
    public void ExportEntryLimitAndChangedSizeReplacementRetainTheirMemoryBounds()
    {
        using var cache = new PreviewImageCache<int, Image>(16, 4); var images = new List<Image>();
        for (var i = 0; i < 5; i++)
        { var image = new Image(i); images.Add(image); if (!cache.TryStore(i, image, 1)) image.Dispose(); }
        Assert.Equal(1, images[4].Disposals); cache.Remove(0);
        var replacement = new Image(0); images.Add(replacement); Assert.True(cache.TryStore(0, replacement, 12));
        Assert.Equal(15, cache.Bytes); cache.Dispose(); Assert.All(images, image => Assert.Equal(1, image.Disposals));
    }
}
