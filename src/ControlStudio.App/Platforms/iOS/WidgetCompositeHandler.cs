using ControlStudio.App.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal sealed class WidgetCompositeHandler : ViewHandler<WidgetCompositeView, UIImageView>
{
    private WidgetPhotoCompositor? compositor;
    public WidgetCompositeHandler() : base(new PropertyMapper<WidgetCompositeView, WidgetCompositeHandler>(ViewMapper)
    { [nameof(WidgetCompositeView.Design)] = (handler, _) => handler.Render() }) { }
    protected override UIImageView CreatePlatformView() => new() { ContentMode = UIViewContentMode.ScaleToFill, Opaque = false,
        UserInteractionEnabled = false, IsAccessibilityElement = false };
    // The widget guide owns layout. A full-resolution bitmap must not contribute
    // UIImageView's intrinsic pixel dimensions to the editor's canvas measurement.
    public override Size GetDesiredSize(double widthConstraint, double heightConstraint) => Size.Zero;
    private void Clear()
    {
        var previous = PlatformView.Image; PlatformView.Image = null; previous?.Dispose();
        compositor?.Dispose(); compositor = null;
    }
    private void Render()
    {
        if (VirtualView.Design is not { } design) { Clear(); return; }
        try
        {
            compositor ??= new(VirtualView.AssetPath);
            var rendered = compositor.Render(design); var previous = PlatformView.Image;
            PlatformView.Image = rendered; previous?.Dispose();
        }
        catch (Exception ex)
        {
            // Never show stale/ordinary layers as though the chosen blend succeeded.
            Clear();
            MauiContext?.Services.GetService<ILogger<WidgetCompositeHandler>>()?.LogWarning("Widget composition preview failed. ErrorType={ErrorType}", ex.GetType().Name);
            VirtualView.Failed(ex is IOException ? ex.Message : "Preview unavailable. Try changing the layer or choosing the photo again.");
        }
    }
    protected override void DisconnectHandler(UIImageView platformView) { Clear(); base.DisconnectHandler(platformView); }
}
