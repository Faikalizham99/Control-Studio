using Xunit;

namespace ControlStudio.App.Tests;

public sealed class WidgetTapRouterTests
{
    [Fact]
    public async Task ColdLaunchWaitsForAnActiveReadyWindow()
    {
        var ready = false; var opened = new List<int>();
        using var router = new WidgetTapRouter(() => ready, (link, _) => { opened.Add(link.Number); return Task.FromResult(true); }, _ => Task.CompletedTask);
        Assert.True(router.Submit("controlstudio://widget/small/20"));
        await router.ProcessAsync(); Assert.Empty(opened);
        router.SetActive(true); await router.ProcessAsync(); Assert.Empty(opened); Assert.True(router.HasPending);
        ready = true; await router.ProcessAsync(); Assert.Equal([20], opened); Assert.False(router.HasPending);
        await router.ProcessAsync(); Assert.Single(opened);
    }
    [Fact]
    public async Task BackgroundCancelsWorkAndKeepsTheRequestForResume()
    {
        var calls = 0; var reading = new TaskCompletionSource<bool>(); CancellationToken observed = default;
        using var router = new WidgetTapRouter(() => true, async (_, token) =>
        {
            calls++; observed = token;
            if (calls == 1) await reading.Task.WaitAsync(token);
            token.ThrowIfCancellationRequested(); return true;
        }, _ => throw new Xunit.Sdk.XunitException("Cancellation is not a routing error"));
        router.SetActive(true); router.Submit("controlstudio://widget/medium/20");
        var pending = router.ProcessAsync(); router.SetActive(false); Assert.True(observed.IsCancellationRequested);
        await pending; Assert.True(router.HasPending);
        router.SetActive(true); await router.ProcessAsync(); Assert.Equal(2, calls); Assert.False(router.HasPending);
    }
    [Fact]
    public async Task RepeatedDeliveryDoesNotDuplicateNavigationAndNewerTapCancelsOlderRead()
    {
        var reads = new TaskCompletionSource<bool>(); var opened = new List<int>(); CancellationToken first = default;
        using var router = new WidgetTapRouter(() => true, async (link, token) =>
        {
            if (link.Number == 1) { first = token; await reads.Task.WaitAsync(token); }
            token.ThrowIfCancellationRequested(); opened.Add(link.Number); return true;
        }, _ => Task.CompletedTask);
        router.SetActive(true); router.Submit("controlstudio://widget/small/1");
        var operation = router.ProcessAsync(); router.Submit("controlstudio://widget/small/1");
        Assert.False(first.IsCancellationRequested);
        await router.ProcessAsync(); Assert.Empty(opened);
        router.Submit("controlstudio://widget/small/20"); Assert.True(first.IsCancellationRequested);
        await operation; Assert.Equal([20], opened); Assert.False(router.HasPending);
    }
    [Fact]
    public async Task TemporarilyUnavailableNavigationRetainsRequestWithoutSpinning()
    {
        var calls = 0; var allowed = false;
        using var router = new WidgetTapRouter(() => true, (_, _) => { calls++; return Task.FromResult(allowed); }, _ => Task.CompletedTask);
        router.SetActive(true); router.Submit("controlstudio://counter");
        await router.ProcessAsync(); Assert.Equal(1, calls); Assert.True(router.HasPending);
        allowed = true; await router.ProcessAsync(); Assert.Equal(2, calls); Assert.False(router.HasPending);
    }
    [Fact]
    public async Task FailedNavigationReportsOnceAndTheNextTapCanRecover()
    {
        var fail = true; var errors = 0; var opened = 0;
        using var router = new WidgetTapRouter(() => true, (_, _) =>
        {
            if (fail) throw new IOException("read unavailable");
            opened++; return Task.FromResult(true);
        }, _ => { errors++; throw new IOException("alert unavailable"); });
        router.SetActive(true); router.Submit("controlstudio://counter");
        await router.ProcessAsync(); await router.ProcessAsync(); Assert.Equal(1, errors); Assert.False(router.HasPending);
        fail = false; router.Submit("controlstudio://counter"); await router.ProcessAsync(); Assert.Equal(1, opened);
    }
    [Fact]
    public async Task DisposalCancelsInflightWorkAndRejectsFurtherDeliveries()
    {
        CancellationToken observed = default;
        var reading = new TaskCompletionSource<bool>();
        using var router = new WidgetTapRouter(() => true, async (_, token) => { observed = token; return await reading.Task.WaitAsync(token); }, _ => Task.CompletedTask);
        router.SetActive(true); router.Submit("controlstudio://counter"); var operation = router.ProcessAsync();
        router.Dispose(); await operation;
        Assert.True(observed.IsCancellationRequested); Assert.False(router.HasPending);
        Assert.False(router.Submit("controlstudio://counter"));
    }
    [Fact]
    public async Task InvalidLinksCannotReplaceAnAlreadyQueuedTap()
    {
        var opened = new List<int>();
        using var router = new WidgetTapRouter(() => true, (link, _) => { opened.Add(link.Number); return Task.FromResult(true); }, _ => Task.CompletedTask);
        router.Submit("controlstudio://widget/large/20");
        Assert.False(router.Submit("controlstudio://widget/large/21")); Assert.False(router.Submit("https://example.test"));
        router.SetActive(true); await router.ProcessAsync(); Assert.Equal([20], opened);
    }
}
