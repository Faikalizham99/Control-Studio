using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

/// <summary>Serial page actions with cancellation and stale-lifetime protection.</summary>
internal abstract class WidgetStudioPage : ContentPage, IAsyncPageLoad
{
    protected readonly ILogger Logger;
    private CancellationTokenSource lifetime = new();
    private Window? observedWindow;
    private bool active, busy, reloadPending, backgrounded, reading;
    private int epoch;
    protected bool IsCurrent(int version) => active && !backgrounded && epoch == version && !lifetime.IsCancellationRequested;
    protected bool IsForeground(int version) => IsCurrent(version) && Window?.IsActivated == true;
    protected abstract Task LoadAsync(CancellationToken token, int version);
    protected abstract void SetActionsEnabled(bool enabled);
    protected abstract void ShowFailure(string message);
    protected virtual void PreviewActivityChanged(bool visible) { }
    protected virtual void ReadingChanged(bool value) { }
    protected bool CanGoBack => active && !backgrounded && (!busy || reading);
    protected async Task NavigateBackAsync()
    {
        if (!CanGoBack) return;
        try { await PageNavigation.PopAsync(this); }
        catch (Exception ex) { Logger.LogWarning("Page Back failed. ErrorType={ErrorType}", ex.GetType().Name); if (active && !backgrounded) ShowFailure("Could not go back. Please try again."); }
    }
    protected WidgetStudioPage(ILogger logger) { Logger = logger; WidgetStudioStyle.Page(this); }
    protected Task ReloadAsync() => RunAsync(async (token, version) =>
    {
        if (!await PageNavigation.WaitForTransitionAsync(this, token) || !IsCurrent(version)) return;
        await LoadAsync(token, version);
        if (IsCurrent(version)) PageNavigation.DataReady(this);
    }, reloadIfBusy: true, readOnly: true);
    protected void Announce(string message)
    {
        try { Microsoft.Maui.Accessibility.SemanticScreenReader.Default.Announce(message); }
        catch (Exception ex) { Logger.LogWarning("Widget accessibility feedback unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
    }
    protected async Task RunAsync(Func<CancellationToken, int, Task> operation, bool reloadIfBusy = false, bool readOnly = false)
    {
        if (!active || backgrounded) return;
        if (busy) { if (reloadIfBusy) { reloadPending = true; ReadingChanged(reading); } return; }
        var version = epoch; var token = lifetime.Token;
        busy = true; reading = readOnly; SetActionsEnabled(false); ReadingChanged(reading);
        try { await operation(token, version); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Logger.LogWarning("Widget page action failed. ErrorType={ErrorType}", ex.GetType().Name);
            if (IsCurrent(version)) ShowFailure(ex is IOException ? ex.Message : "This action could not finish. Try Refresh again.");
        }
        finally
        {
            busy = reading = false; SetActionsEnabled(active && !backgrounded); ReadingChanged(false);
            if (active && !backgrounded && reloadPending) { reloadPending = false; await ReloadAsync(); }
        }
    }
    private async void WindowActivated(object? sender, EventArgs e)
    {
        if (!active) return;
        backgrounded = false;
        if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); }
        PreviewActivityChanged(true); await ReloadAsync();
    }
    private void WindowDeactivated(object? sender, EventArgs e)
    {
        backgrounded = true; epoch++; lifetime.Cancel(); reloadPending = false;
        PreviewActivityChanged(false); SetActionsEnabled(false); ReadingChanged(false);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true; epoch++;
        lifetime.Cancel(); lifetime.Dispose(); lifetime = new();
        Unsubscribe(); observedWindow = Window;
        if (observedWindow is not null) { observedWindow.Activated += WindowActivated; observedWindow.Deactivated += WindowDeactivated; }
        backgrounded = observedWindow is not null && !observedWindow.IsActivated;
        PreviewActivityChanged(!backgrounded);
        await ReloadAsync();
    }
    private void Unsubscribe()
    {
        if (observedWindow is null) return;
        observedWindow.Activated -= WindowActivated; observedWindow.Deactivated -= WindowDeactivated; observedWindow = null;
    }
    protected override void OnDisappearing()
    {
        active = false; epoch++; lifetime.Cancel(); reloadPending = false;
        Unsubscribe(); PreviewActivityChanged(false);
        SetActionsEnabled(false); ReadingChanged(false); base.OnDisappearing();
    }
    protected override bool OnBackButtonPressed() => (busy && !reading) || base.OnBackButtonPressed();
}
