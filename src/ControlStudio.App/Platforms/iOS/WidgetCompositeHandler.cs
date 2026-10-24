using ControlStudio.App.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using UIKit;
using Foundation;
using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Platforms.iOS;

internal sealed class WidgetCompositeHandler : ViewHandler<WidgetCompositeView, UIImageView>
{
    private WidgetPhotoCompositor? compositor;
    private NSTimer? timer;
    private double timerInterval;
    public WidgetCompositeHandler() : base(new PropertyMapper<WidgetCompositeView, WidgetCompositeHandler>(ViewMapper)
    { [nameof(WidgetCompositeView.Design)] = (handler, _) => handler.Render() }) { }
    protected override UIImageView CreatePlatformView() => new() { ContentMode = UIViewContentMode.ScaleToFill, Opaque = false,
        UserInteractionEnabled = false, IsAccessibilityElement = false };
    // The widget guide owns layout. A full-resolution bitmap must not contribute
    // UIImageView's intrinsic pixel dimensions to the editor's canvas measurement.
    public override Size GetDesiredSize(double widthConstraint, double heightConstraint) => Size.Zero;
    private void Clear()
    {
        StopTimer();
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
            UpdateTimer();
        }
        catch (Exception ex)
        {
            // Never show stale/ordinary layers as though the chosen blend succeeded.
            Clear();
            MauiContext?.Services.GetService<ILogger<WidgetCompositeHandler>>()?.LogWarning("Widget composition preview failed. ErrorType={ErrorType}", ex.GetType().Name);
            VirtualView.Failed(ex is IOException ? ex.Message : "Preview unavailable. Try changing the layer or choosing the photo again.");
        }
    }
    private void StopTimer() { timer?.Invalidate(); timer?.Dispose(); timer = null; timerInterval = 0; }
    private void UpdateTimer()
    {
        if (VirtualView.Design is not { } design || !WidgetScenes.IsLive(design) || PlatformView.Window is null) { StopTimer(); return; }
        var interval = design.Layers.Any(x => x.Kind == WidgetLayerKind.Clock && x.Visible && x.Opacity > 0 && x.Clock?.ShowSeconds == true) ? 1d : 60;
        if (timer is not null && timerInterval == interval) return;
        StopTimer(); timerInterval = interval;
        timer = NSTimer.CreateRepeatingTimer(TimeSpan.FromSeconds(interval), _ =>
        {
            try
            {
                if (PlatformView.Window is null || VirtualView.Design is null) { StopTimer(); return; }
                Render(); VirtualView.TickClock(DateTimeOffset.Now);
            }
            catch (Exception ex)
            {
                Clear(); MauiContext?.Services.GetService<ILogger<WidgetCompositeHandler>>()?.LogWarning("Clock preview update failed. ErrorType={ErrorType}", ex.GetType().Name);
                VirtualView.Failed("The clock preview could not update. Reopen this editor to retry.");
            }
        });
        timer.FireDate = NSDate.FromTimeIntervalSinceNow(interval - DateTimeOffset.Now.ToUnixTimeMilliseconds() % (long)(interval * 1000) / 1000d);
        NSRunLoop.Main.AddTimer(timer, NSRunLoopMode.Common);
    }
    private void Loaded(object? sender, EventArgs args) { Render(); }
    private void Unloaded(object? sender, EventArgs args) => Clear();
    protected override void ConnectHandler(UIImageView platformView)
    {
        base.ConnectHandler(platformView); VirtualView.Loaded += Loaded; VirtualView.Unloaded += Unloaded;
    }
    protected override void DisconnectHandler(UIImageView platformView)
    {
        VirtualView.Loaded -= Loaded; VirtualView.Unloaded -= Unloaded; Clear(); base.DisconnectHandler(platformView);
    }
}
