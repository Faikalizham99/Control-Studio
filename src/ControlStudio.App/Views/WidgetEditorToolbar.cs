using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

/// <summary>One line of natural-size controls; scrolling never shrinks their hit targets.</summary>
internal sealed class WidgetHorizontalStrip : ContentView
{
    private readonly ScrollView scroll;
    private readonly ILogger? logger;
    private View? reveal;
    private bool pending, scrolling;
    public WidgetHorizontalStrip(IEnumerable<View> items, string id, ILogger? logger = null, double spacing = 4)
    {
        this.logger = logger; AutomationId = id;
        var row = new HorizontalStackLayout { Spacing = spacing, Padding = 4 };
        foreach (var item in items) row.Children.Add(item);
        scroll = new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = row, IsClippedToBounds = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Default, VerticalScrollBarVisibility = ScrollBarVisibility.Never };
        Content = WidgetStudioStyle.Card(scroll, 0, 27);
        row.SizeChanged += StripSized; scroll.SizeChanged += StripSized; Loaded += StripSized;
        SemanticProperties.SetHint(this, "Swipe horizontally for more options.");
    }
    public void Reveal(View item) { reveal = item; pending = true; TryReveal(); }
    private void StripSized(object? sender, EventArgs args) { if (reveal is not null) pending = true; TryReveal(); }
    private async void TryReveal()
    {
        if (!pending || scrolling || !IsLoaded || scroll.Handler is null || scroll.Width <= 0 || reveal?.Width is not > 0) return;
        scrolling = true;
        try
        {
            while (pending && IsLoaded && scroll.Handler is not null && reveal?.Width > 0)
            {
                pending = false;
                await scroll.ScrollToAsync(reveal, ScrollToPosition.MakeVisible, false);
            }
        }
        catch (Exception ex) { logger?.LogDebug("Widget options reveal unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
        finally { scrolling = false; }
    }
}

internal sealed class WidgetToolButton : ContentView
{
    private readonly Button tap;
    private readonly AccentSurface selected;
    private readonly StudioGlyph idle;
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
        var active = new StudioGlyph(symbol, false) { WidthRequest = 23, HeightRequest = 23, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        selected = new AccentSurface(active, 19) { IsVisible = false, InputTransparent = true };
        selected.BindForeground(active, StudioGlyph.TintProperty);
        tap = SettingsControls.TapTarget(title, automationId ?? "Clipboard" + title.Replace(" ", "")); tap.CornerRadius = 19;
        tap.Clicked += (_, args) => { if (IsEnabled) Clicked?.Invoke(this, args); };
        tap.Pressed += (_, _) => { if (IsEnabled) Scale = .95; }; tap.Released += (_, _) => Scale = 1;
        Content = new Grid { Children = { tap, idle, selected } }; UpdateDescription();
    }
    public void Describe(string action, string detail) { description = action + " " + detail; UpdateDescription(); }
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
