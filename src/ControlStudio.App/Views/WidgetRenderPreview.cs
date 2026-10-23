namespace ControlStudio.App.Views;

/// <summary>The saved full composite, held only while its card is near the viewport.</summary>
internal sealed class WidgetRenderPreview : ContentView, IDisposable
{
    private readonly string? path;
    private readonly Image image = new() { Aspect = Aspect.AspectFit, InputTransparent = true };
    private bool visible, disposed;
    public WidgetRenderPreview(string? fullRenderPath, Color background)
    {
        path = fullRenderPath; InputTransparent = true;
        Content = path is not null ? image : new Label
        {
            Text = "Preview unavailable\nOpen Edit and save again.", TextColor = AppearanceColors.ButtonForeground(background),
            FontSize = 13, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
            Margin = 12, InputTransparent = true
        };
        AutomationProperties.SetIsInAccessibleTree(this, false);
        AutomationProperties.SetIsInAccessibleTree(Content, false);
    }
    public void SetVisible(bool value)
    {
        if (disposed || visible == value) return;
        visible = value;
        // Clearing Source also lets the MAUI handler cancel an outstanding load.
        image.Source = value && path is not null ? ImageSource.FromFile(path) : null;
    }
    public void Dispose()
    {
        disposed = true; visible = false; image.Source = null;
    }
}

internal static class WidgetPreviewWindow
{
    // Full renders are already bounded to 1024 pixels/edge. Limit both the
    // viewport buffer and live sources, including intermediate layout passes.
    public const int MaximumLoaded = 6;
    public static IReadOnlySet<int> Select(IReadOnlyList<Rect> cards, double scrollY, double height)
    {
        if (!double.IsFinite(scrollY) || !double.IsFinite(height) || height <= 0) return new HashSet<int>();
        var top = Math.Max(0, scrollY); var bottom = top + height;
        return cards.Select((bounds, index) => (bounds, index))
            .Where(x => double.IsFinite(x.bounds.Y) && double.IsFinite(x.bounds.Height) && double.IsFinite(x.bounds.Width)
                && x.bounds.Height > 0 && x.bounds.Width > 0 && x.bounds.Bottom > top - 64 && x.bounds.Top < bottom + 64)
            .OrderBy(x => x.bounds.Bottom > top && x.bounds.Top < bottom ? 0 : 1)
            .ThenBy(x => Math.Abs(x.bounds.Center.Y - (top + height / 2)))
            .Take(MaximumLoaded).Select(x => x.index).ToHashSet();
    }
}
