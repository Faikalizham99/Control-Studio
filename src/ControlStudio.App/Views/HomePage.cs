using ControlStudio.Core;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

public sealed class HomePage : ContentPage
{
    private readonly ICounterRepository repository;
    private readonly ILogger<HomePage> logger;
    private readonly Label counter = new() { Text = "—", FontSize = 68, FontAttributes = FontAttributes.Bold };
    private readonly Label updated = new() { Text = "Loading your counter…", FontSize = 15 };
    private readonly Label status = new() { FontSize = 15 };
    private readonly Button update = new() { Text = "Update Widget", CornerRadius = 16, HeightRequest = 54, BackgroundColor = Color.FromArgb("#2458E6"), TextColor = Colors.White };
    private bool busy;

    public HomePage(ICounterRepository repository, ILogger<HomePage> logger)
    {
        this.repository = repository;
        this.logger = logger;
        Title = "Control Studio";
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F2F3F8"), Color.FromArgb("#101116"));
        counter.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#141722"), Colors.White);
        SemanticProperties.SetDescription(counter, "Current counter value");
        SemanticProperties.SetHint(update, "Increments the counter and requests a widget refresh");
        AutomationId = "HomePage";
        update.AutomationId = "UpdateWidgetButton";
        counter.AutomationId = "CounterValue";
        var card = new Border
        {
            StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 24 },
            Padding = 24,
            Content = new VerticalStackLayout { Spacing = 12, Children =
            {
                new Label { Text = "YOUR COUNTER", FontSize = 13, CharacterSpacing = 1.5 }, counter, updated, update
            }}
        };
        card.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#1C1E27"));
        update.Clicked += async (_, _) => await LoadAsync(increment: true);
        var refresh = new ToolbarItem { Text = "Refresh" };
        refresh.Clicked += async (_, _) => await LoadAsync();
        ToolbarItems.Add(refresh);
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = new Thickness(24), Spacing = 24, Children =
            {
                new Label { Text = "A little action.\nA useful connection.", FontSize = 30, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Your app, Home Screen and Shortcuts, connected on this iPhone.", FontSize = 17 },
                card, status,
                new Label { Text = "iOS schedules widget updates. A refresh request may take time to appear on your Home Screen.", FontSize = 14 }
            }
        }};
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    public Task RefreshAsync() => LoadAsync();

    private async Task LoadAsync(bool increment = false)
    {
        if (busy) return;
        busy = true;
        update.IsEnabled = false;
        try
        {
            var snapshot = increment ? await repository.IncrementAsync() : await repository.ReadAsync();
            counter.Text = snapshot.Value.ToString(System.Globalization.CultureInfo.CurrentCulture);
            SemanticProperties.SetDescription(counter, $"Counter: {counter.Text}");
            updated.Text = snapshot.UpdatedAt is { } timestamp
                ? $"Last updated {timestamp.ToLocalTime():g}" : "Ready for your first update";
            status.Text = snapshot.IsShared
                ? (snapshot.RefreshRequested ? "Saved. Widget refresh requested." : "Shared storage is connected.")
                : "Saved on this iPhone only. App Group access is unavailable; the widget and Shortcuts cannot share this counter. Check signing in Settings.";
            logger.LogInformation("Counter operation completed. Operation={Operation}, Storage={Storage}", increment ? "increment" : "read", snapshot.Storage);
            if (increment) SemanticScreenReader.Default.Announce($"Counter {snapshot.Value}. {status.Text}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Counter operation failed. Operation={Operation}", increment ? "increment" : "read");
            status.Text = ex is IOException ? ex.Message : "The counter is unavailable. Try Refresh, then review signing in Settings.";
        }
        finally { busy = false; update.IsEnabled = true; }
    }
}
