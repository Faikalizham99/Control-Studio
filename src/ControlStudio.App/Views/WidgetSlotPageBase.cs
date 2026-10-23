using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

/// <summary>Slot pages cancel pending reads and queued writes when hidden or backgrounded.</summary>
internal abstract class WidgetSlotPageBase : ContentPage
{
    protected readonly ILogger Logger;
    private CancellationTokenSource lifetime = new();
    private Window? observedWindow;
    private bool active, busy, reloadPending;
    private int epoch;
    protected bool Current(int version) => active && version == epoch && !lifetime.IsCancellationRequested;
    protected bool Foreground => active && Window?.IsActivated == true && !lifetime.IsCancellationRequested;
    protected abstract Task LoadAsync(CancellationToken token, int version);
    protected abstract void SetActionsEnabled(bool enabled);
    protected abstract void ShowFailure(string message);
    protected virtual void ReleasePreviews() { }
    protected WidgetSlotPageBase(ILogger logger) { Logger = logger; WidgetStudioStyle.Page(this); }
    protected Task ReloadAsync() => RunAsync(LoadAsync, queueReload: true);
    protected async Task RunAsync(Func<CancellationToken, int, Task> action, bool queueReload = false)
    {
        if (!Foreground) return;
        if (busy) { if (queueReload) reloadPending = true; return; }
        busy = true; var version = epoch; var token = lifetime.Token; SetActionsEnabled(false);
        try { await action(token, version); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Logger.LogWarning("Widget slot page action failed. ErrorType={ErrorType}", ex.GetType().Name);
            if (Current(version)) ShowFailure(ex is IOException ? ex.Message : "This action could not finish. Please try again.");
        }
        finally
        {
            busy = false; SetActionsEnabled(Foreground);
            if (Foreground && reloadPending) { reloadPending = false; await ReloadAsync(); }
        }
    }
    protected void Announce(string message)
    {
        if (!Foreground) return;
        try { Microsoft.Maui.Accessibility.SemanticScreenReader.Default.Announce(message); }
        catch (Exception) { }
    }
    private async void Activated(object? sender, EventArgs args)
    {
        if (!active) return;
        if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); }
        await ReloadAsync();
    }
    private void Deactivated(object? sender, EventArgs args)
    {
        epoch++; lifetime.Cancel(); ReleasePreviews(); SetActionsEnabled(false);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true; epoch++; lifetime.Dispose(); lifetime = new();
        Unsubscribe(); observedWindow = Window;
        if (observedWindow is not null) { observedWindow.Activated += Activated; observedWindow.Deactivated += Deactivated; }
        await ReloadAsync();
    }
    private void Unsubscribe()
    {
        if (observedWindow is not null) { observedWindow.Activated -= Activated; observedWindow.Deactivated -= Deactivated; observedWindow = null; }
    }
    protected override void OnDisappearing()
    {
        active = false; epoch++; lifetime.Cancel(); reloadPending = false; Unsubscribe(); ReleasePreviews(); SetActionsEnabled(false); base.OnDisappearing();
    }
    protected override bool OnBackButtonPressed() => busy || base.OnBackButtonPressed();
}
