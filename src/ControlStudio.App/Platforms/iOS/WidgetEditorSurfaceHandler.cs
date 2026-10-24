using ControlStudio.App.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using UIKit;
using NativeContentView = Microsoft.Maui.Platform.ContentView;

namespace ControlStudio.App.Platforms.iOS;

internal sealed class WidgetEditorSurfaceHandler : ContentViewHandler
{
    private UITapGestureRecognizer? tap;
    private WidgetEditorSurface Surface => (WidgetEditorSurface)VirtualView;
    protected override void ConnectHandler(NativeContentView platformView)
    {
        base.ConnectHandler(platformView);
        tap = new UITapGestureRecognizer(() =>
        {
            try { Surface.DeselectFromBlankSpace(); }
            catch (Exception ex) { Report(ex); }
        }) { CancelsTouchesInView = false };
        tap.ShouldReceiveTouch = (_, touch) =>
        {
            try
            {
                if (!Surface.CanDeselect) return false;
                // Buttons, switches, sliders, entry/editor fields keep their normal touch.
                for (var view = touch.View; view is not null && view != platformView; view = view.Superview)
                    if (view is UIControl or UITextView) return false;
                var point = touch.LocationInView(platformView);
                foreach (var area in Surface.ProtectedAreas)
                    if (area.IsVisible && area.Handler?.PlatformView is UIView native && !native.Hidden &&
                        native.ConvertRectToView(native.Bounds, platformView).Contains(point)) return false;
                return true;
            }
            catch (Exception ex) { Report(ex); return false; }
        };
        platformView.AddGestureRecognizer(tap);
    }
    private void Report(Exception ex) => MauiContext?.Services.GetService<ILogger<WidgetEditorSurfaceHandler>>()?
        .LogWarning("Widget blank-space selection failed. ErrorType={ErrorType}", ex.GetType().Name);
    protected override void DisconnectHandler(NativeContentView platformView)
    {
        if (tap is not null) { platformView.RemoveGestureRecognizer(tap); tap.ShouldReceiveTouch = null; tap.Dispose(); tap = null; }
        base.DisconnectHandler(platformView);
    }
}
