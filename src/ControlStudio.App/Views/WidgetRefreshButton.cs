namespace ControlStudio.App.Views;

/// <summary>A fixed-size header action with readable accent and explicit refresh progress.</summary>
internal sealed class WidgetRefreshButton : ContentView
{
    private readonly StudioGlyph icon = ClipboardEditorControls.Icon(StudioSymbol.Refresh, size: 21);
    private readonly ActivityIndicator progress = new() { IsVisible = false, InputTransparent = true,
        WidthRequest = 22, HeightRequest = 22, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
    private readonly ClipboardEditorActionButton button;
    private bool refreshing;
    public event EventHandler? Clicked;
    public bool IsRefreshing
    {
        get => refreshing;
        set
        {
            refreshing = value; icon.IsVisible = !value;
            progress.IsVisible = progress.IsRunning = value;
            button.Describe(value ? "Refreshing widgets" : "Refresh widgets");
        }
    }
    public WidgetRefreshButton()
    {
        WidthRequest = HeightRequest = MinimumWidthRequest = MinimumHeightRequest = 48;
        button = new("", icon, "RefreshWidgetDesigns", accentCaption: true);
        button.Describe("Refresh widgets");
        button.Clicked += (_, args) => { if (IsEnabled && !IsRefreshing) Clicked?.Invoke(this, args); };
        progress.SetDynamicResource(ActivityIndicator.ColorProperty, "AccentText");
        AutomationProperties.SetIsInAccessibleTree(progress, false);
        Content = new Grid { Children = { button, progress } };
        Unloaded += (_, _) => progress.IsRunning = false;
        Loaded += (_, _) => progress.IsRunning = refreshing;
    }
}
