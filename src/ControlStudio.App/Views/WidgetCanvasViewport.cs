using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

/// <summary>Outside space scrolls independently instead of reducing the widget canvas.</summary>
internal sealed class WidgetCanvasViewport : ScrollView
{
    private readonly WidgetCanvasView canvas;
    private readonly ILogger logger;
    private bool needsCenter = true, centering;
    public WidgetCanvasViewport(WidgetCanvasView canvas, ILogger logger)
    {
        this.canvas = canvas; this.logger = logger;
        Orientation = ScrollOrientation.Horizontal; HorizontalScrollBarVisibility = ScrollBarVisibility.Never;
        VerticalScrollBarVisibility = ScrollBarVisibility.Never; IsClippedToBounds = true; Content = canvas;
        AutomationId = "WidgetCanvasViewport";
        SizeChanged += Changed; canvas.SizeChanged += Changed; Loaded += Changed;
        canvas.InteractionChanged += (_, _) => { if (needsCenter && !canvas.IsInteracting) Center(); };
        SemanticProperties.SetHint(this, "Swipe sideways on empty workspace to see outside the widget. Drag a photo to move it; only content inside the widget boundary is saved.");
    }
    private void Changed(object? sender, EventArgs args) { needsCenter = true; Center(); }
    internal static double CenterOffset(double contentWidth, double viewportWidth) => double.IsFinite(contentWidth) && double.IsFinite(viewportWidth)
        && viewportWidth > 0 ? Math.Max(0, (contentWidth - viewportWidth) / 2) : 0;
    private async void Center()
    {
        if (centering || !IsLoaded || Handler is null || Width <= 0 || canvas.Width <= 0 || canvas.IsInteracting) return;
        centering = true;
        try
        {
            while (needsCenter && IsLoaded && Handler is not null && !canvas.IsInteracting)
            {
                needsCenter = false;
                await ScrollToAsync(CenterOffset(canvas.Width, Width), 0, false);
            }
        }
        catch (Exception ex) { logger.LogDebug("Widget workspace centering unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
        finally { centering = false; }
    }
}
