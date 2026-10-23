using ControlStudio.Core;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

internal sealed class CounterWidgetsPage : WidgetStudioPage
{
    private readonly ICounterRepository repository;
    private readonly CounterWidgetPreview small = new(false), medium = new(true) { IsVisible = false };
    private readonly PageActionButton back = new("", "BackFromCounterWidgets", ProfileSymbol.Back);
    private readonly ClipboardEditorActionButton refresh = new("Refresh preview", ClipboardEditorControls.Icon(StudioSymbol.Refresh), "RefreshCounterPreview", primary: true);
    private readonly WidgetSegments sizes;
    private readonly Label status = SettingsControls.Text("", 14, secondary: true);
    private readonly Border[] dots = [new(), new()];
    public CounterWidgetsPage(ICounterRepository repository, ILogger logger) : base(logger)
    {
        this.repository = repository; Title = "Counter Widgets"; AutomationId = "CounterWidgetsPage";
        sizes = new(["Small", "Medium"], "CounterSize", SelectSize);
        back.Clicked += async (_, _) => await RunAsync((_, _) => Navigation.PopAsync());
        refresh.Clicked += async (_, _) => await RunAsync(async (token, version) =>
        {
            await LoadAsync(token, version);
            if (IsCurrent(version) && !status.IsVisible) Announce("Counter preview updated.");
        });
        var indicators = new HorizontalStackLayout { Spacing = 10, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 0, 12) };
        foreach (var dot in dots)
        {
            dot.WidthRequest = dot.HeightRequest = 8; dot.StrokeThickness = 0;
            dot.StrokeShape = new Microsoft.Maui.Controls.Shapes.Ellipse(); dot.InputTransparent = true;
            AutomationProperties.SetIsInAccessibleTree(dot, false); indicators.Children.Add(dot);
        }
        status.IsVisible = false; SelectSize(0);
        var body = new VerticalStackLayout { Padding = new Thickness(20, 14, 28, 28), Spacing = 18, MaximumWidthRequest = 800, Children =
        {
            SettingsControls.Text("Preview your counter in the available sizes.", 15, secondary: true),
            SettingsControls.Text("Widget size", 15, bold: true), sizes,
            new Grid { Margin = new Thickness(0, 12, 0, 0), Children = { small, medium } }, indicators, refresh, status,
            SettingsControls.Text("Preview of your shared counter. iOS schedules Home Screen updates.", 13, secondary: true)
        } };
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        layout.Add(WidgetStudioStyle.Header(Title, back)); layout.Add(new ScrollView { Content = body }, 0, 1); Content = layout;
    }
    private void SelectSize(int index)
    {
        small.IsVisible = index == 0; medium.IsVisible = index == 1;
        for (var i = 0; i < dots.Length; i++)
        {
            dots[i].RemoveBinding(BackgroundColorProperty);
            dots[i].RemoveDynamicResource(BackgroundColorProperty);
            dots[i].ClearValue(BackgroundColorProperty);
            if (i == index) dots[i].SetDynamicResource(BackgroundColorProperty, "Accent");
            else dots[i].SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#A1A9B9"), Color.FromArgb("#3C4352"));
        }
    }
    protected override async Task LoadAsync(CancellationToken token, int version)
    {
        var snapshot = StudioContent.WidgetSnapshot(await repository.ReadAsync(token)); if (!IsCurrent(version)) return;
        small.Update(snapshot); medium.Update(snapshot);
        status.Text = snapshot is null ? "Shared counter unavailable. Check App Group signing in Settings." : "";
        status.IsVisible = snapshot is null;
    }
    protected override void ShowFailure(string message) { status.Text = message; status.IsVisible = true; }
    protected override void SetActionsEnabled(bool enabled) => back.IsEnabled = refresh.IsEnabled = sizes.IsEnabled = enabled;
}
