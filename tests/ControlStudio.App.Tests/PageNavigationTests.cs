using System.Reflection;
using ControlStudio.App.Views;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class PageNavigationTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private readonly ContentPage root = new();
    private NavigationPage Open()
    {
        var navigation = (NavigationPage)Activator.CreateInstance(typeof(NavigationPage), BindingFlags.Instance | BindingFlags.NonPublic, null, [false, root], null)!;
        host.App.Windows[0].Page = navigation; ((IWindow)host.App.Windows[0]).Activated(); return navigation;
    }
    private sealed class Target : ContentPage, IAsyncPageLoad;
    [Fact]
    public async Task DuplicateTapsCreateOnePageAndReadsWaitUntilPushCompletes()
    {
        var navigation = Open(); var log = new Recorder(); var target = new Target { Title = "private user title" };
        Task<bool>? gate = null; var factories = 0;
        navigation.Pushed += (_, _) => { gate = PageNavigation.WaitForTransitionAsync(target, default); Assert.False(gate.IsCompleted); };
        var first = PageNavigation.PushAsync(root, () => { factories++; return target; }, log);
        var second = PageNavigation.PushAsync(root, () => { factories++; return new Target(); }, log);
        await Task.WhenAll(first, second);
        Assert.Equal(1, factories); Assert.Equal(2, navigation.Navigation.NavigationStack.Count);
        Assert.True(await gate!); PageNavigation.DataReady(target); PageNavigation.DataReady(target);
        Assert.Single(log.Messages, x => x.Contains("data-ready"));
        Assert.DoesNotContain(log.Messages, x => x.Contains(target.Title));
        Assert.Contains(log.Messages, x => x.Contains("transition-complete"));
    }
    [Fact]
    public async Task FailedFactoryAndCancellationReleaseTheOriginForRetry()
    {
        var navigation = Open(); var log = new Recorder();
        await Assert.ThrowsAsync<InvalidOperationException>(() => PageNavigation.PushAsync(root, () => throw new InvalidOperationException(), log));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); var constructed = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PageNavigation.PushAsync(root, () => { constructed = true; return new Target(); }, log, cancelled.Token));
        Assert.False(constructed);
        log.Throw = true; var target = new Target(); await PageNavigation.PushAsync(root, () => target, log);
        Assert.Same(target, navigation.CurrentPage); Assert.True(await PageNavigation.WaitForTransitionAsync(target, default));
    }
    [Fact]
    public async Task BackgroundingBeforeConstructionPreventsOpeningAStaleDestination()
    {
        var navigation = Open(); var constructed = false;
        var opening = PageNavigation.PushAsync(root, () => { constructed = true; return new Target(); }, new Recorder());
        ((IWindow)host.App.Windows[0]).Deactivated(); await opening;
        Assert.False(constructed); Assert.Single(navigation.Navigation.NavigationStack);
    }
    [Fact]
    public async Task BackDuringPushWaitsForTransitionAndRepeatedBackPopsOnlyOnce()
    {
        var navigation = Open(); var target = new Target(); Task? first = null, second = null;
        navigation.Pushed += (_, _) =>
        {
            first = PageNavigation.PopAsync(target); second = PageNavigation.PopAsync(target);
            Assert.False(first.IsCompleted);
        };
        await PageNavigation.PushAsync(root, () => target, new Recorder());
        await Task.WhenAll(first!, second!); Assert.Same(root, navigation.CurrentPage);
        Assert.Single(navigation.Navigation.NavigationStack);
    }
    private sealed class Recorder : ILogger
    {
        public readonly List<string> Messages = [];
        public bool Throw;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (Throw) throw new InvalidOperationException(); Messages.Add(formatter(state, exception)); }
    }
    public void Dispose() => host.Dispose();
}
