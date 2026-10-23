using ControlStudio.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Accessibility;

namespace ControlStudio.App.Views;

public sealed class HomePage : ContentPage
{
    private enum Operation { Read, Increment, Reset }
    private readonly ICounterRepository repository;
    private readonly ILogger<HomePage> logger;
    private readonly Func<Task<bool>> confirmReset;
    private readonly Label counter = new() { Text = "—", FontSize = 68, FontAttributes = FontAttributes.Bold };
    private readonly Label updated = new() { Text = "Loading your counter…", FontSize = 15 };
    private readonly Label status = new() { FontSize = 15 };
    private readonly Button increment = DashboardStyle.Button("Increment (+1)");
    private readonly PageActionButton reset = new("Reset counter", "ResetCounterButton", destructive: true) { MinimumHeightRequest = 54 };
    private readonly ToolbarItem refresh = new() { Text = "Refresh" };
    private CounterSnapshot? snapshot;
    private CancellationTokenSource lifetime = new();
    private Window? observedWindow;
    private bool busy, active, readPending;
    private int epoch;

    public HomePage(ICounterRepository repository, ILogger<HomePage> logger) : this(repository, logger, null) { }
    internal HomePage(ICounterRepository repository, ILogger<HomePage> logger, Func<Task<bool>>? confirmReset)
    {
        this.repository = repository; this.logger = logger;
        this.confirmReset = confirmReset ?? (() => DisplayAlertAsync("Reset counter to 0?", "This replaces the current counter value with zero.", "Reset", "Cancel"));
        Title = "Control Studio"; AutomationId = "HomePage"; DashboardStyle.NavigationAccent(this);
        increment.CornerRadius = 16; increment.MinimumHeightRequest = 54; increment.AutomationId = "UpdateWidgetButton";
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F2F3F8"), Color.FromArgb("#101116"));
        counter.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#141722"), Colors.White);
        foreach (var label in new[] { updated, status }) label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#535B6A"), Color.FromArgb("#BDC4D2"));
        SemanticProperties.SetDescription(counter, "Current counter value");
        SemanticProperties.SetHint(increment, "Increments the counter by one and requests a widget refresh");
        counter.AutomationId = "CounterValue";
        var card = new Border
        {
            StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 24 }, Padding = 24,
            Content = new VerticalStackLayout { Spacing = 12, Children =
            {
                new Label { Text = "YOUR COUNTER", FontSize = 13, CharacterSpacing = 1.5 }, counter, updated, increment, reset
            } }
        };
        card.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#1C1E27"));
        increment.Clicked += async (_, _) => await LoadAsync(Operation.Increment);
        reset.Clicked += async (_, _) => await ResetAsync();
        refresh.Clicked += async (_, _) => await LoadAsync(); ToolbarItems.Add(refresh);
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = new Thickness(24), Spacing = 24, Children =
            {
                new Label { Text = "A little action.\nA useful connection.", FontSize = 30, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Your counter, ready for Home Screen widgets and Shortcuts.", FontSize = 17 }, card, status,
                new Label { Text = "iOS schedules widget updates. A refresh request may take time to appear on your Home Screen.", FontSize = 14 }
            }
        } };
        UpdateEnabled();
    }

    private bool Current(int version) => active && version == epoch && !lifetime.IsCancellationRequested;
    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true; epoch++; lifetime.Dispose(); lifetime = new();
        if (observedWindow is not null) observedWindow.Activated -= WindowActivated;
        observedWindow = Window; if (observedWindow is not null) observedWindow.Activated += WindowActivated;
        await LoadAsync();
    }
    protected override void OnDisappearing()
    {
        active = false; epoch++; lifetime.Cancel();
        if (observedWindow is not null) { observedWindow.Activated -= WindowActivated; observedWindow = null; }
        UpdateEnabled(); base.OnDisappearing();
    }
    private async void WindowActivated(object? sender, EventArgs e) => await LoadAsync();
    public Task RefreshAsync() => LoadAsync();
    internal Task ResetAsync() => LoadAsync(Operation.Reset);

    private async Task LoadAsync(Operation operation = Operation.Read)
    {
        if (!active) return;
        if (busy) { if (operation == Operation.Read) readPending = true; return; }
        var version = epoch; var token = lifetime.Token;
        busy = true; UpdateEnabled();
        try
        {
            if (operation == Operation.Reset)
            {
                if (snapshot?.Value is not > 0 || !await confirmReset() || !Current(version) || Window?.IsActivated != true) return;
            }
            var result = operation switch
            {
                Operation.Increment => await repository.IncrementAsync(token),
                Operation.Reset => await repository.ResetAsync(token),
                _ => await repository.ReadAsync(token)
            };
            if (!Current(version)) return;
            snapshot = result;
            counter.Text = result.Value.ToString(System.Globalization.CultureInfo.CurrentCulture);
            SemanticProperties.SetDescription(counter, $"Counter: {counter.Text}");
            updated.Text = result.UpdatedAt is { } timestamp ? $"Last updated {timestamp.ToLocalTime():g}" : "Ready for your first update";
            var scope = result.IsShared
                ? (result.RefreshRequested ? "Saved. Widget refresh requested." : "Shared storage is connected.")
                : "Saved on this iPhone only. App Group access is unavailable; the widget and Shortcuts cannot share this counter. Check signing in Settings.";
            status.Text = (operation == Operation.Reset ? "Counter reset to 0. " : "") + scope;
            logger.LogInformation("Counter operation completed. Operation={Operation}, Storage={Storage}", operation, result.Storage);
            if (operation != Operation.Read) Announce($"Counter {result.Value}. {status.Text}");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Counter operation failed. Operation={Operation}", operation);
            if (Current(version))
            {
                snapshot = null; counter.Text = "—"; updated.Text = "Counter unavailable";
                SemanticProperties.SetDescription(counter, "Current counter value unavailable");
                status.Text = ex is IOException ? ex.Message : "The counter is unavailable. Try Refresh, then review signing in Settings.";
            }
        }
        finally
        {
            busy = false; UpdateEnabled();
            if (active && readPending) { readPending = false; await LoadAsync(); }
        }
    }
    private void UpdateEnabled()
    {
        increment.IsEnabled = refresh.IsEnabled = active && !busy;
        reset.DisabledReason = busy ? "Another counter action is in progress." : snapshot is null ? "Refresh the counter before resetting."
            : snapshot.Value == 0 ? "The counter is already zero." : "Open Home to reset the counter.";
        reset.IsEnabled = active && !busy && snapshot?.Value > 0;
    }
    private void Announce(string message)
    {
        try { SemanticScreenReader.Default.Announce(message); }
        catch (Exception ex) { logger.LogWarning("Counter accessibility feedback unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
    }
}
