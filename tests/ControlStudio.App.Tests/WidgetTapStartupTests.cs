using Microsoft.Maui.Dispatching;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class WidgetTapStartupTests
{
    [Fact]
    public async Task OrdinaryIconLaunchAndResumeNeverInitializeWidgetRoutingOrTimer()
    {
        var errors = 0;
        using var session = new WidgetTapSession(() => throw new InvalidOperationException("router not available at startup"),
            () => throw new InvalidOperationException("native timer unavailable at startup"), _ => errors++);
        foreach (var active in new[] { false, true, false, true }) { session.SetActive(active); await session.ProcessAsync(); }
        foreach (var url in new[] { null, "", "https://example.test", "controlstudio://widget/small/21" }) Assert.False(session.Submit(url));
        await session.ProcessAsync(); Assert.False(session.HasPending); Assert.Equal(0, errors);
    }
    [Fact]
    public async Task ColdWidgetLinkInitializesOnlyAfterActivationAndPostedProcessing()
    {
        var builds = 0; var timers = 0; var opened = 0; var timer = new Timer();
        using var session = new WidgetTapSession(() => { builds++; return Router(() => true, () => { opened++; return Task.FromResult(true); }); },
            () => { timers++; return timer; }, _ => Assert.Fail("Unexpected failure"));
        Assert.True(session.Submit("controlstudio://widget/medium/20")); await session.ProcessAsync();
        Assert.Equal(0, builds); Assert.Equal(0, timers);
        session.SetActive(true); Assert.Equal(0, builds); await session.ProcessAsync();
        Assert.Equal(1, builds); Assert.Equal(1, timers); Assert.Equal(1, opened);
        Assert.False(session.HasPending); Assert.False(timer.IsRunning); Assert.Equal(0, timer.Starts);
        session.SetActive(false); session.SetActive(true); await session.ProcessAsync(); Assert.Equal(1, builds);
    }
    [Fact]
    public async Task NativeTimerIsUsedOnlyWhileActiveAndWorkIsDeferred()
    {
        var ready = false; var opened = 0; var timer = new Timer();
        using var session = new WidgetTapSession(() => Router(() => ready, () => { opened++; return Task.FromResult(true); }), () => timer, _ => Assert.Fail("Unexpected failure"));
        session.SetActive(true); session.Submit("controlstudio://counter"); await session.ProcessAsync();
        Assert.True(timer.IsRunning); Assert.Equal(TimeSpan.FromMilliseconds(250), timer.Interval); Assert.True(timer.IsRepeating);
        session.SetActive(false); Assert.False(timer.IsRunning); timer.Fire(); Assert.Equal(0, opened);
        ready = true; session.SetActive(true); await session.ProcessAsync();
        Assert.Equal(1, opened); Assert.False(timer.IsRunning); Assert.False(session.HasPending);
        session.Dispose(); Assert.Equal(0, timer.Handlers); timer.Fire(); Assert.Equal(1, opened);
    }
    [Theory]
    [InlineData("router")] [InlineData("timer")] [InlineData("configure")] [InlineData("subscribe")] [InlineData("start")] [InlineData("stop")]
    public async Task OptionalInitializationAndTimerFailuresCannotEscapeOrRetryWithoutANewTap(string stage)
    {
        var errors = 0; var broken = true; WidgetTapRouter? created = null;
        var timer = new Timer { Fail = stage };
        using var session = new WidgetTapSession(() =>
        {
            if (broken && stage == "router") throw new InvalidOperationException("router failure");
            return created = Router(() => !broken || stage == "stop", () => Task.FromResult(true));
        }, () =>
        {
            if (broken && stage == "timer") throw new InvalidOperationException("timer failure");
            return timer;
        }, _ => errors++);
        session.SetActive(true); session.Submit("controlstudio://counter"); await session.ProcessAsync();
        Assert.Equal(1, errors); Assert.False(session.HasPending); Assert.Equal(0, timer.Handlers);
        if (created is not null) Assert.False(created.Submit("controlstudio://counter"));
        await session.ProcessAsync(); session.SetActive(false); session.SetActive(true); await session.ProcessAsync(); Assert.Equal(1, errors);
        broken = false; timer.Fail = ""; session.Submit("controlstudio://counter"); await session.ProcessAsync();
        Assert.False(session.HasPending); Assert.Equal(1, errors);
    }
    [Fact]
    public async Task ReadinessFailureFromTimerTickIsContainedAndResourcesAreReleased()
    {
        var crash = false; var errors = 0; var timer = new Timer();
        using var session = new WidgetTapSession(() => Router(() => crash ? throw new InvalidOperationException() : false, () => Task.FromResult(true)), () => timer, _ => errors++);
        session.SetActive(true); session.Submit("controlstudio://counter"); await session.ProcessAsync(); Assert.True(timer.IsRunning);
        crash = true; timer.Fire(); Assert.Equal(1, errors); Assert.Equal(0, timer.Handlers); Assert.False(session.HasPending);
    }
    [Fact]
    public async Task BackgroundCancellationRetainsOneRequestAndDisposalPreventsLateRestart()
    {
        var timer = new Timer(); var calls = 0; var read = new TaskCompletionSource<bool>(); CancellationToken observed = default;
        using var session = new WidgetTapSession(() => new WidgetTapRouter(() => true, async (_, token) =>
        {
            calls++; observed = token;
            if (calls == 1) await read.Task.WaitAsync(token);
            return true;
        }, _ => Task.CompletedTask), () => timer, _ => Assert.Fail("Unexpected failure"));
        session.SetActive(true); session.Submit("controlstudio://counter"); var processing = session.ProcessAsync();
        session.SetActive(false); Assert.True(observed.IsCancellationRequested); await processing; Assert.True(session.HasPending);
        session.SetActive(true); await session.ProcessAsync(); Assert.Equal(2, calls); Assert.False(session.HasPending);
        session.Dispose(); Assert.Equal(0, timer.Handlers); Assert.False(timer.IsRunning); Assert.False(session.Submit("controlstudio://counter"));
    }
    [Fact]
    public async Task DestroyingWindowDuringReadCannotOpenAnythingOrRestartItsTimer()
    {
        var timer = new Timer(); var read = new TaskCompletionSource<bool>(); var errors = 0;
        using var session = new WidgetTapSession(() => new WidgetTapRouter(() => true, (_, token) => read.Task.WaitAsync(token), _ => Task.CompletedTask), () => timer, _ => errors++);
        session.SetActive(true); session.Submit("controlstudio://counter"); var processing = session.ProcessAsync();
        session.Dispose(); await processing;
        Assert.False(session.HasPending); Assert.Equal(0, timer.Handlers); Assert.False(timer.IsRunning); Assert.Equal(0, errors);
    }
    [Fact]
    public async Task CompletionFromFailedRuntimeCannotTouchItsReplacementTimer()
    {
        var first = new Timer(); var replacement = new Timer(); var read = new TaskCompletionSource<bool>();
        var builds = 0; var timerBuilds = 0; var errors = 0;
        using var session = new WidgetTapSession(() =>
        {
            builds++; return Router(() => true, () => builds == 1 ? read.Task : Task.FromResult(true));
        }, () => ++timerBuilds == 1 ? first : replacement, _ => errors++);
        session.SetActive(true); session.Submit("controlstudio://counter"); var old = session.ProcessAsync();
        first.Fail = "stop"; session.SetActive(false); Assert.Equal(1, errors);
        session.SetActive(true); session.Submit("controlstudio://widget/small/20"); await session.ProcessAsync();
        Assert.Equal(2, builds); var stops = replacement.Stops;
        read.SetResult(true); await old;
        Assert.Equal(stops, replacement.Stops); Assert.False(session.HasPending); Assert.Equal(1, errors);
    }
    [Fact]
    public async Task DeactivationAndCleanupFailuresAreContainedEvenIfLoggingAlsoFails()
    {
        var timer = new Timer();
        using var session = new WidgetTapSession(() => Router(() => false, () => Task.FromResult(true)), () => timer, _ => throw new IOException("logger unavailable"));
        session.SetActive(true); session.Submit("controlstudio://counter"); await session.ProcessAsync();
        timer.Fail = "stop"; session.SetActive(false); session.Dispose();
        Assert.False(session.HasPending); Assert.Equal(0, timer.Handlers);
    }
    [Fact]
    public void ColdLaunchPayloadMayBeNullEmptyOrUnrelatedWithoutDelivery()
    {
        var inbox = new WidgetTapLaunchInbox(); var sent = 0;
        Assert.False(inbox.Capture("scene", null)); Assert.False(inbox.Capture("scene", []));
        Assert.False(inbox.Capture("scene", [null, "", "https://example.test", "controlstudio://widget/extraLarge/1"]));
        Assert.False(inbox.Deliver("scene", _ => { sent++; return true; })); Assert.False(inbox.HasPending); Assert.Equal(0, sent);
    }
    [Fact]
    public void ColdLinkWaitsForItsOwnSceneAndIsDeliveredExactlyOnce()
    {
        var inbox = new WidgetTapLaunchInbox(); var sent = new List<string>();
        Assert.True(inbox.Capture("scene-a", ["controlstudio://widget/small/1", "ignored", "controlstudio://widget/medium/20"]));
        Assert.False(inbox.Deliver("scene-b", _ => throw new InvalidOperationException()));
        Assert.False(inbox.Deliver("scene-a", _ => false)); Assert.True(inbox.HasPending);
        Assert.True(inbox.Deliver("scene-a", url => { sent.Add(url); return true; })); Assert.Equal(["controlstudio://widget/medium/20"], sent);
        Assert.False(inbox.Deliver("scene-a", _ => throw new InvalidOperationException()));
    }
    [Fact]
    public void BrokenNativePayloadCannotOverwritePreviousLinkAndDisconnectReleasesIt()
    {
        var inbox = new WidgetTapLaunchInbox(); inbox.Capture("scene-a", ["controlstudio://counter"]);
        static IEnumerable<string?> Broken() { yield return "controlstudio://widget/small/20"; throw new InvalidOperationException(); }
        Assert.Throws<InvalidOperationException>(() => inbox.Capture("scene-a", Broken()));
        string? result = null; inbox.Deliver("scene-a", url => { result = url; return true; }); Assert.Equal("controlstudio://counter", result);
        inbox.Capture("scene-a", ["controlstudio://counter"]); inbox.Disconnect("scene-b"); Assert.True(inbox.HasPending);
        inbox.Disconnect("scene-a"); Assert.False(inbox.HasPending);
    }
    [Fact]
    public void FailedColdDeliveryRemainsRetryableAndNewerReentrantLinkIsNotCleared()
    {
        var inbox = new WidgetTapLaunchInbox(); inbox.Capture("scene", ["controlstudio://counter"]);
        Assert.Throws<InvalidOperationException>(() => inbox.Deliver("scene", _ => throw new InvalidOperationException())); Assert.True(inbox.HasPending);
        inbox.Deliver("scene", _ => { inbox.Capture("scene", ["controlstudio://widget/large/20"]); return true; });
        string? result = null; inbox.Deliver("scene", url => { result = url; return true; }); Assert.Equal("controlstudio://widget/large/20", result);
    }
    private static WidgetTapRouter Router(Func<bool> ready, Func<Task<bool>> open) => new(ready, (_, _) => open(), _ => Task.CompletedTask);
    private sealed class Timer : IDispatcherTimer
    {
        private TimeSpan interval;
        private EventHandler? tick;
        public string Fail = "";
        public int Starts, Stops;
        public int Handlers => tick?.GetInvocationList().Length ?? 0;
        public TimeSpan Interval { get => interval; set { if (Fail == "configure") throw new InvalidOperationException(); interval = value; } }
        public bool IsRepeating { get; set; }
        public bool IsRunning { get; private set; }
        public event EventHandler? Tick { add { if (Fail == "subscribe") throw new InvalidOperationException(); tick += value; } remove => tick -= value; }
        public void Start() { Starts++; IsRunning = true; if (Fail == "start") throw new InvalidOperationException(); }
        public void Stop() { Stops++; IsRunning = false; if (Fail == "stop") throw new InvalidOperationException(); }
        public void Fire() => tick?.Invoke(this, EventArgs.Empty);
    }
}
