using ControlStudio.Core;
using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

public sealed class WidgetsPage : ContentPage
{
    private readonly ICounterRepository repository;
    private readonly CounterWidgetPreview small = new(false);
    private readonly CounterWidgetPreview medium = new(true);
    private readonly Label status = SettingsControls.Text("", 13, secondary: true);
    private CancellationTokenSource lifetime = new();
    private bool busy;
    private bool active;
    private bool reloadPending;

    public WidgetsPage(ICounterRepository repository)
    {
        this.repository = repository;
        Title = "Widgets"; AutomationId = "WidgetsPage"; SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this);
        var refresh = new ToolbarItem { Text = "Refresh", AutomationId = "RefreshWidgetPreviews" };
        refresh.Clicked += async (_, _) => await ReloadAsync(); ToolbarItems.Add(refresh);
        var setup = DashboardStyle.Button("How to add a widget");
        setup.Clicked += async (_, _) => await Navigation.PushAsync(new AboutPage());
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 20, Spacing = 16, Children =
        {
            SettingsControls.Text("Counter widgets", 28, bold: true),
            SettingsControls.Text("Preview your counter in the two available sizes.", 15, secondary: true),
            SettingsControls.Text("Small", 14, bold: true), small,
            SettingsControls.Text("Medium", 14, bold: true), medium,
            status,
            SettingsControls.Text("These are in-app previews. iOS renders the Home Screen widget and schedules its updates.", 13, secondary: true), setup
        } } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true; lifetime.Dispose(); lifetime = new(); await ReloadAsync();
    }
    protected override void OnDisappearing() { active = false; lifetime.Cancel(); base.OnDisappearing(); }

    private async Task ReloadAsync()
    {
        if (!active) return;
        if (busy) { reloadPending = true; return; }
        busy = true;
        var token = lifetime.Token;
        try
        {
            reloadPending = false;
            var snapshot = StudioContent.WidgetSnapshot(await repository.ReadAsync(token));
            token.ThrowIfCancellationRequested(); small.Update(snapshot); medium.Update(snapshot);
            status.Text = snapshot is null ? "Shared counter unavailable. Review App Group signing in widget setup." : "";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            small.Update(null); medium.Update(null);
            status.Text = ex is IOException ? ex.Message : "The counter preview is unavailable. Try Refresh.";
        }
        finally
        {
            status.IsVisible = !string.IsNullOrEmpty(status.Text); busy = false;
            if (active && reloadPending) { reloadPending = false; await ReloadAsync(); }
        }
    }

    private sealed class CounterWidgetPreview : ContentView
    {
        private readonly Label value = new() { Text = "—", FontAttributes = FontAttributes.Bold, MaxLines = 1, FontAutoScalingEnabled = false };
        private readonly Label updated = new() { FontSize = 11, MaxLines = 1 };
        private readonly Label caption = new() { Text = "Last Updated", FontSize = 10 };
        private readonly bool medium;

        public CounterWidgetPreview(bool medium)
        {
            this.medium = medium;
            AutomationId = medium ? "MediumCounterPreview" : "SmallCounterPreview";
            HeightRequest = 164;
            if (!medium) { WidthRequest = 164; HorizontalOptions = LayoutOptions.Center; }
            var header = SettingsControls.Text("Control Studio", 11, secondary: true, bold: true); header.MaxLines = 1; header.FontAutoScalingEnabled = false;
            var headerMark = new StudioGlyph(StudioSymbol.Counter, useAccent: false) { WidthRequest = 12, HeightRequest = 12, VerticalOptions = LayoutOptions.Center };
            headerMark.SetAppThemeColor(StudioGlyph.TintProperty, Color.FromArgb("#6C6C70"), Color.FromArgb("#AEAEBA"));
            var brand = new Grid { ColumnSpacing = 4, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
            brand.Add(headerMark); brand.Add(header, 1);
            value.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#16171C"), Colors.White);
            updated.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6C6C70"), Color.FromArgb("#AEAEBA"));
            caption.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6C6C70"), Color.FromArgb("#AEAEBA"));
            var labels = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center, Children = { brand, value, caption, updated } };
            var content = new Grid { ColumnSpacing = 18, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
            content.Add(labels);
            if (medium)
            {
                var mark = new StudioGlyph(StudioSymbol.Counter, useAccent: false) { Tint = Color.FromArgb("#007AFF"), WidthRequest = 44, HeightRequest = 44, VerticalOptions = LayoutOptions.Center };
                // The existing WidgetKit medium design uses a fixed blue mark.
                content.Add(mark, 1);
            }
            var border = new Border { Content = content, Padding = 16, StrokeShape = new RoundRectangle { CornerRadius = 24 }, StrokeThickness = 1 };
            SettingsControls.SurfaceStroke(border); border.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#1C1C1E"));
            Content = border; Update(null, loading: true);
        }

        public void Update(CounterSnapshot? snapshot, bool loading = false)
        {
            value.Text = snapshot?.Value.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? (loading ? "—" : "Open Control Studio");
            value.MaxLines = snapshot is null && !loading ? 2 : 1;
            value.FontSize = snapshot is null ? (loading ? 46 : 17) : Math.Min(46, (medium ? 180 : 132) / (value.Text.Length * .62));
            caption.IsVisible = snapshot is not null;
            updated.Text = snapshot is not null ? snapshot.UpdatedAt?.ToLocalTime().ToString("t") ?? "Not yet" : loading ? "Loading counter…" : "Shared counter unavailable";
            SemanticProperties.SetDescription(this, snapshot is not null ? $"{(medium ? "Medium" : "Small")} widget preview. Counter {value.Text}. Last updated {updated.Text}." : updated.Text);
        }
    }
}
