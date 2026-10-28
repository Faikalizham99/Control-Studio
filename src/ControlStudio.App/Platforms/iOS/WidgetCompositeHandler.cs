using ControlStudio.App.Views;
using ControlStudio.App.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using UIKit;
using Foundation;
using CoreFoundation;
using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Platforms.iOS;

internal sealed class WidgetCompositeHandler : ViewHandler<WidgetCompositeView, UIImageView>
{
    private WidgetPhotoCompositor? compositor;
    private NSTimer? timer;
    private double timerInterval;
    private IDisposable? batteryObserver;
    private bool rendering, unloaded;
    private readonly WidgetRenderQueue renderQueue;
    private WidgetRenderIdentity? painted;
    private WidgetBatteryReading? lastBattery;
    public WidgetCompositeHandler() : base(new PropertyMapper<WidgetCompositeView, WidgetCompositeHandler>(ViewMapper)
    { [nameof(WidgetCompositeView.Design)] = (handler, _) => handler.RequestRender() })
    { renderQueue = new(action => DispatchQueue.MainQueue.DispatchAsync(action), Render); }
    protected override UIImageView CreatePlatformView() => new() { ContentMode = UIViewContentMode.ScaleToFill, Opaque = false,
        UserInteractionEnabled = false, IsAccessibilityElement = false };
    // The widget guide owns layout. A full-resolution bitmap must not contribute
    // UIImageView's intrinsic pixel dimensions to the editor's canvas measurement.
    public override Size GetDesiredSize(double widthConstraint, double heightConstraint) => Size.Zero;
    private void Clear()
    {
        renderQueue.Cancel(); painted = null; lastBattery = null;
        StopTimer(); batteryObserver?.Dispose(); batteryObserver = null;
        var previous = PlatformView.Image; PlatformView.Image = null; previous?.Dispose();
        compositor?.Dispose(); compositor = null;
    }
    private void RequestRender()
    {
        // Null is a lifecycle boundary: release now and invalidate queued work.
        if (VirtualView.Design is null) Clear(); else if (!unloaded) renderQueue.Request();
    }
    private void Render()
    {
        if (rendering || unloaded) return;
        if (VirtualView.Design is not { } design) { Clear(); return; }
        rendering = true;
        var stage = WidgetRenderStage.Initialising; var composing = false;
        try
        {
            var hasBattery = design.Layers.Any(x => x.Kind == WidgetLayerKind.Battery && x.Visible && x.Opacity > 0);
            if (hasBattery && PlatformView.Window is not null) batteryObserver ??= AppleWidgetBattery.Observe(_ => RequestRender());
            else { batteryObserver?.Dispose(); batteryObserver = null; }
            var battery = hasBattery ? AppleWidgetBattery.Read() : default;
            var time = DateTimeOffset.Now;
            stage = WidgetRenderStage.Validation;
            var identity = WidgetRenderIdentity.Create(design, time, battery);
            var clockChanged = painted is null || !identity.LiveText.SequenceEqual(painted.LiveText);
            if (!identity.SamePixels(painted))
            {
                stage = WidgetRenderStage.Initialising;
                compositor ??= new(VirtualView.AssetPath);
                composing = true;
                var rendered = compositor.Render(design, time: time, battery: battery); var previous = PlatformView.Image;
                PlatformView.Image = rendered; previous?.Dispose();
                painted = identity; // Failed paints must remain retryable.
            }
            composing = false; stage = WidgetRenderStage.LiveUpdate;
            if (lastBattery != battery) { VirtualView.TickBattery(battery); lastBattery = battery; }
            if (clockChanged) VirtualView.TickClock(time);
            UpdateTimer();
        }
        catch (Exception ex)
        {
            // Never show stale/ordinary layers as though the chosen blend succeeded.
            if (composing) stage = compositor?.Stage ?? stage;
            Clear();
            ReportFailure(ex, stage);
        }
        finally { rendering = false; }
    }
    private void ReportFailure(Exception error, WidgetRenderStage stage)
    {
        var nativeName = (error as ObjCRuntime.ObjCException)?.Name;
        MauiContext?.Services.GetService<ILogger<WidgetCompositeHandler>>()?.LogWarning(
            "Widget preview failed. Stage={Stage}; ErrorType={ErrorType}; NativeError={NativeError}",
            stage, error.GetType().Name, nativeName);
        VirtualView.Failed(WidgetRenderFailure.Describe(error, stage, nativeName));
    }
    private void StopTimer() { timer?.Invalidate(); timer?.Dispose(); timer = null; timerInterval = 0; }
    private void UpdateTimer()
    {
        if (VirtualView.Design is not { } design || !design.Layers.Any(x => WidgetTextLayers.IsText(x) && WidgetTextLayers.IsLive(x) && x.Visible && x.Opacity > 0) || PlatformView.Window is null) { StopTimer(); return; }
        var interval = design.Layers.Any(x => x.Kind == WidgetLayerKind.Clock && x.Visible && x.Opacity > 0 && x.Clock?.ShowSeconds == true) ? 1d : 60;
        if (timer is not null && timerInterval == interval) return;
        StopTimer(); timerInterval = interval;
        timer = NSTimer.CreateRepeatingTimer(TimeSpan.FromSeconds(interval), _ =>
        {
            try
            {
                if (PlatformView.Window is null || VirtualView.Design is null) { StopTimer(); return; }
                RequestRender();
            }
            catch (Exception ex)
            {
                Clear(); ReportFailure(ex, WidgetRenderStage.LiveUpdate);
            }
        });
        timer.FireDate = NSDate.FromTimeIntervalSinceNow(interval - DateTimeOffset.Now.ToUnixTimeMilliseconds() % (long)(interval * 1000) / 1000d);
        NSRunLoop.Main.AddTimer(timer, NSRunLoopMode.Common);
    }
    private void Loaded(object? sender, EventArgs args) { unloaded = false; RequestRender(); }
    private void Unloaded(object? sender, EventArgs args) { unloaded = true; Clear(); }
    protected override void ConnectHandler(UIImageView platformView)
    {
        base.ConnectHandler(platformView); unloaded = false; VirtualView.Loaded += Loaded; VirtualView.Unloaded += Unloaded;
    }
    protected override void DisconnectHandler(UIImageView platformView)
    {
        VirtualView.Loaded -= Loaded; VirtualView.Unloaded -= Unloaded; unloaded = true; Clear(); base.DisconnectHandler(platformView);
    }
}
