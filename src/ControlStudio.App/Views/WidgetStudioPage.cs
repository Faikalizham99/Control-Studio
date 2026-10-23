using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

/// <summary>Serial page actions with cancellation and stale-lifetime protection.</summary>
internal abstract class WidgetStudioPage : ContentPage
{
    protected readonly ILogger Logger;
    private CancellationTokenSource lifetime = new();
    private Window? observedWindow;
    private bool active, busy, reloadPending;
    private int epoch;
    protected bool IsCurrent(int version) => active && epoch == version && !lifetime.IsCancellationRequested;
    protected bool IsForeground(int version) => IsCurrent(version) && Window?.IsActivated == true;
    protected abstract Task LoadAsync(CancellationToken token, int version);
    protected abstract void SetActionsEnabled(bool enabled);
    protected abstract void ShowFailure(string message);
    protected WidgetStudioPage(ILogger logger) { Logger = logger; WidgetStudioStyle.Page(this); }
    protected Task ReloadAsync() => RunAsync(LoadAsync, reloadIfBusy: true);
    protected void Announce(string message)
    {
        try { Microsoft.Maui.Accessibility.SemanticScreenReader.Default.Announce(message); }
        catch (Exception ex) { Logger.LogWarning("Widget accessibility feedback unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
    }
    protected async Task RunAsync(Func<CancellationToken, int, Task> operation, bool reloadIfBusy = false)
    {
        if (!active) return;
        if (busy) { if (reloadIfBusy) reloadPending = true; return; }
        var version = epoch; var token = lifetime.Token;
        busy = true; SetActionsEnabled(false);
        try { await operation(token, version); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Logger.LogWarning("Widget page action failed. ErrorType={ErrorType}", ex.GetType().Name);
            if (IsCurrent(version)) ShowFailure(ex is IOException ? ex.Message : "This action could not finish. Try Refresh again.");
        }
        finally
        {
            busy = false; SetActionsEnabled(active);
            if (active && reloadPending) { reloadPending = false; await ReloadAsync(); }
        }
    }
    private async void WindowActivated(object? sender, EventArgs e) => await ReloadAsync();
    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true; epoch++;
        lifetime.Dispose(); lifetime = new();
        if (observedWindow is not null) observedWindow.Activated -= WindowActivated;
        observedWindow = Window; if (observedWindow is not null) observedWindow.Activated += WindowActivated;
        await ReloadAsync();
    }
    protected override void OnDisappearing()
    {
        active = false; epoch++; lifetime.Cancel();
        if (observedWindow is not null) { observedWindow.Activated -= WindowActivated; observedWindow = null; }
        SetActionsEnabled(false); base.OnDisappearing();
    }
    protected override bool OnBackButtonPressed() => busy || base.OnBackButtonPressed();
}
