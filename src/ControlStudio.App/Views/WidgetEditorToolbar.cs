namespace ControlStudio.App.Views;

/// <summary>One line of natural-size controls; scrolling never shrinks their hit targets.</summary>
internal sealed class WidgetHorizontalStrip : ContentView
{
    public WidgetHorizontalStrip(IEnumerable<View> items, string id, double spacing = 4)
    {
        AutomationId = id;
        var row = new HorizontalStackLayout { Spacing = spacing, Padding = 4 };
        foreach (var item in items) row.Children.Add(item);
        var scroll = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = row, IsClippedToBounds = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Default, VerticalScrollBarVisibility = ScrollBarVisibility.Never };
        Content = WidgetStudioStyle.Card(scroll, 0, 27);
        SemanticProperties.SetHint(this, "Swipe horizontally for more options.");
    }
}

internal sealed class WidgetToolButton : ContentView
{
    private readonly Button tap;
    private readonly AccentSurface selected;
    private readonly StudioGlyph idle;
    private readonly StudioGlyph active;
    private readonly bool destructive;
    private string description;
    public bool IsSelected { get => selected.IsVisible; set { selected.IsVisible = value; Scale = 1; UpdateDescription(); } }
    public event EventHandler? Clicked;
    public WidgetToolButton(StudioSymbol symbol, string title, bool destructive = false, string? automationId = null)
    {
        this.destructive = destructive; description = title;
        WidthRequest = HeightRequest = MinimumWidthRequest = MinimumHeightRequest = 48;
        idle = new StudioGlyph(symbol, false) { WidthRequest = 23, HeightRequest = 23, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        if (destructive) idle.SetAppThemeColor(StudioGlyph.TintProperty, Color.FromArgb("#B42336"), Color.FromArgb("#FF8798"));
        else idle.SetDynamicResource(StudioGlyph.TintProperty, "AccentText");
        active = new StudioGlyph(symbol, false) { WidthRequest = 23, HeightRequest = 23, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        selected = new AccentSurface(active, 19) { IsVisible = false, InputTransparent = true };
        selected.BindForeground(active, StudioGlyph.TintProperty);
        tap = SettingsControls.TapTarget(title, automationId ?? "Clipboard" + title.Replace(" ", "")); tap.CornerRadius = 19;
        tap.Clicked += (_, args) => { if (IsEnabled) Clicked?.Invoke(this, args); };
        tap.Pressed += (_, _) => { if (IsEnabled) Scale = .95; }; tap.Released += (_, _) => Scale = 1;
        Content = new Grid { Children = { tap, idle, selected } }; UpdateDescription();
    }
    public void Describe(string action, string detail) { description = action + " " + detail; UpdateDescription(); }
    public void SetSymbol(StudioSymbol symbol) { idle.Symbol = active.Symbol = symbol; }
    private void UpdateDescription()
    {
        if (tap is null) return;
        SemanticProperties.SetDescription(tap, description + (IsSelected ? ", expanded" : ""));
        SemanticProperties.SetHint(tap, destructive ? "Asks for confirmation before deleting." : IsSelected ? "Tap again to close these controls." : "");
    }
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled)) Scale = 1;
    }
}
