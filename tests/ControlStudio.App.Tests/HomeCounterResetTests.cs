using ControlStudio.App.Views;
using ControlStudio.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class HomeCounterResetTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private HomePage Open(Counter counter, Func<Task<bool>> confirm)
    {
        var page = new HomePage(counter, NullLogger<HomePage>.Instance, confirm);
        host.App.Windows[0].Page = page;
        ((IWindow)host.App.Windows[0]).Activated();
        ((IPageController)page).SendAppearing(); return page;
    }
    private static IEnumerable<Element> Descendants(Element element)
    {
        yield return element;
        if (element is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var descendant in Descendants(child)) yield return descendant;
    }
    private static PageActionButton Reset(HomePage page) => Descendants(page).OfType<PageActionButton>().Single();

    [Fact]
    public async Task ConfirmedResetSavesZeroUpdatesUiAndDisablesReset()
    {
        var counter = new Counter { Value = 7 }; var confirmations = 0;
        var page = Open(counter, () => { confirmations++; return Task.FromResult(true); });
        Assert.True(Reset(page).IsEnabled);
        await page.ResetAsync();
        Assert.Equal(1, confirmations); Assert.Equal(1, counter.Resets); Assert.Equal(0, counter.Value);
        Assert.False(Reset(page).IsEnabled); Assert.Contains("already zero", Reset(page).DisabledReason);
        Assert.Equal("0", Descendants(page).OfType<Label>().Single(x => x.AutomationId == "CounterValue").Text);
        Assert.Contains(Descendants(page).OfType<Label>(), x => x.Text?.Contains("Counter reset to 0.") == true);
        await page.ResetAsync(); Assert.Equal(1, counter.Resets); Assert.Equal(1, confirmations);
    }
    [Fact]
    public async Task CancelledConfirmationDoesNotMutateAndControlsRecover()
    {
        var counter = new Counter { Value = 4 };
        var page = Open(counter, () => Task.FromResult(false)); await page.ResetAsync();
        Assert.Equal(0, counter.Resets); Assert.Equal(4, counter.Value); Assert.True(Reset(page).IsEnabled);
    }
    [Fact]
    public async Task LeavingHomeDuringConfirmationCannotResetTheCounter()
    {
        var dialog = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var counter = new Counter { Value = 3 }; var page = Open(counter, () => dialog.Task);
        var action = page.ResetAsync(); Assert.False(Reset(page).IsEnabled);
        ((IPageController)page).SendDisappearing(); dialog.SetResult(true); await action;
        Assert.Equal(0, counter.Resets); Assert.Equal(3, counter.Value); Assert.False(Reset(page).IsEnabled);
    }
    [Fact]
    public async Task RepeatedTapsCannotOpenDuplicateDialogsOrResets()
    {
        var dialog = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var counter = new Counter { Value = 3 }; var calls = 0;
        var page = Open(counter, () => { calls++; return dialog.Task; });
        var first = page.ResetAsync(); await page.ResetAsync(); Assert.Equal(1, calls);
        dialog.SetResult(true); await first; Assert.Equal(1, counter.Resets);
    }
    [Fact]
    public async Task BackgroundedHomeCannotCommitAnApprovedReset()
    {
        var dialog = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var counter = new Counter { Value = 3 }; var page = Open(counter, () => dialog.Task);
        var action = page.ResetAsync(); ((IWindow)host.App.Windows[0]).Deactivated();
        dialog.SetResult(true); await action; Assert.Equal(0, counter.Resets); Assert.Equal(3, counter.Value);
    }
    [Fact]
    public async Task LocalFallbackResetDoesNotClaimSharedWidgetRefresh()
    {
        var counter = new Counter { Value = 3, Storage = "local" }; var page = Open(counter, () => Task.FromResult(true));
        await page.ResetAsync(); Assert.Equal(0, counter.Value);
        var text = Descendants(page).OfType<Label>().Select(x => x.Text ?? "").ToArray();
        Assert.Contains(text, x => x.Contains("Saved on this iPhone only"));
        Assert.DoesNotContain(text, x => x.Contains("Widget refresh requested"));
    }
    [Fact]
    public async Task FailedResetKeepsStoredValueAndRequiresRefreshBeforeRetry()
    {
        var counter = new Counter { Value = 8, FailReset = true }; var page = Open(counter, () => Task.FromResult(true));
        await page.ResetAsync(); Assert.Equal(8, counter.Value); Assert.False(Reset(page).IsEnabled);
        Assert.Contains("Refresh", Reset(page).DisabledReason);
        counter.FailReset = false; await page.RefreshAsync(); Assert.True(Reset(page).IsEnabled);
    }
    [Theory]
    [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void ResetStaysRedAndReadableDespiteAccentChangesAndWhenDisabled(AppTheme theme)
    {
        host.App.UserAppTheme = theme;
        var page = Open(new Counter { Value = 2 }, () => Task.FromResult(false));
        var button = Reset(page); var surface = Assert.Single(Assert.IsType<Grid>(button.Content).Children.OfType<SecondaryActionSurface>());
        var label = Assert.Single(Assert.IsType<HorizontalStackLayout>(surface.Content).Children.OfType<Label>());
        foreach (var enabled in new[] { true, false, true })
        {
            button.IsEnabled = enabled;
            foreach (var color in new[] { Colors.Lime, Colors.Blue, Colors.White })
            {
                host.App.Resources["AccentText"] = color;
                host.App.UserAppTheme = theme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
                host.App.UserAppTheme = theme;
                var fill = Assert.IsType<SolidPaint>(((IView)surface).Background).Color;
                Assert.True(AppearanceColors.Contrast(fill, label.TextColor) >= 4.5);
                if (enabled) Assert.True(fill.Red > fill.Green * 2 && fill.Red > fill.Blue * 2);
                else Assert.True(AppearanceColors.Contrast(Assert.IsType<SolidColorBrush>(surface.Stroke).Color, fill) >= 3);
                Assert.Equal(1, button.Opacity);
            }
        }
    }
    private sealed class Counter : ICounterRepository
    {
        public int Value, Resets; public bool FailReset;
        public string Storage = "shared";
        private CounterSnapshot Snapshot(bool changed = false) => new(1, Value, 1000, Storage, changed && Storage == "shared");
        public Task<CounterSnapshot> ReadAsync(CancellationToken token = default) => Task.FromResult(Snapshot());
        public Task<CounterSnapshot> IncrementAsync(CancellationToken token = default) { Value++; return Task.FromResult(Snapshot(true)); }
        public Task<CounterSnapshot> ResetAsync(CancellationToken token = default)
        {
            if (FailReset) throw new IOException("Storage temporarily unavailable.");
            Resets++; Value = 0; return Task.FromResult(Snapshot(true));
        }
    }
    public void Dispose() => host.Dispose();
}
