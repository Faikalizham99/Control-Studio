using ControlStudio.App.Views;
using ControlStudio.App.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using UIKit;
using Foundation;
using CoreFoundation;
using CoreAnimation;
using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Platforms.iOS;

internal sealed class WidgetCompositeHandler : ViewHandler<WidgetCompositeView, UIImageView>
{
    // These fields belong exclusively to the serial rendering worker.
    private WidgetPhotoCompositor? compositor;
    private Func<string, string?>? workerAssetPath;
    private sealed record Request(WidgetDesign Design, WidgetRenderIdentity Identity, DateTimeOffset Time,
        WidgetBatteryReading Battery, Func<string, string?> AssetPath, bool ConserveMemory);
    private sealed class PreviewFailure(Exception cause, WidgetRenderStage stage) : Exception("Preview rendering failed.", cause)
    { public WidgetRenderStage Stage { get; } = stage; }
    private readonly LatestPreviewWorker<Request, UIImage> worker;
    private NSTimer? timer;
    private CADisplayLink? displayLink;
    private Action? nextFrame;
    private double timerInterval;
    private IDisposable? batteryObserver;
    private NSObject? memoryObserver;
    private bool unloaded, conserveMemory;
    private readonly WidgetRenderQueue renderQueue;
    private WidgetRenderIdentity? requested, painted;
    private WidgetBatteryReading? lastBattery;
    public WidgetCompositeHandler() : base(new PropertyMapper<WidgetCompositeView, WidgetCompositeHandler>(ViewMapper)
    { [nameof(WidgetCompositeView.Design)] = (handler, _) => handler.RequestRender() })
    {
        renderQueue = new(PostFrame, Render);
        worker = new(action => DispatchQueue.MainQueue.DispatchAsync(action), RenderImage, ReleaseRenderer, PresentImage, RenderFailed);
    }
    protected override UIImageView CreatePlatformView() => new() { ContentMode = UIViewContentMode.ScaleToFill, Opaque = false,
        UserInteractionEnabled = false, IsAccessibilityElement = false };
    public override Size GetDesiredSize(double widthConstraint, double heightConstraint) => Size.Zero;
    private void PostFrame(Action action)
    {
        nextFrame = action;
        if (displayLink is null)
        {
            displayLink = CADisplayLink.Create(() =>
            {
                displayLink!.Paused = true; var callback = nextFrame; nextFrame = null;
                try { callback?.Invoke(); } catch (Exception ex) { Clear(); ReportFailure(ex, WidgetRenderStage.LiveUpdate); }
            });
            displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
        }
        displayLink.Paused = false;
    }
    private void Clear()
    {
        renderQueue.Cancel(); nextFrame = null;
        displayLink?.Invalidate(); displayLink?.Dispose(); displayLink = null;
        requested = painted = null; lastBattery = null;
        StopTimer(); batteryObserver?.Dispose(); batteryObserver = null;
        memoryObserver?.Dispose(); memoryObserver = null;
        var previous = PlatformView.Image; PlatformView.Image = null; previous?.Dispose();
        worker.Clear(); // Retire native state only after in-flight work releases it.
    }
    private void ReleaseRenderer()
    {
        using var pool = new NSAutoreleasePool();
        var previous = compositor; compositor = null; workerAssetPath = null; previous?.Dispose();
    }
    private void RequestRender()
    {
        if (VirtualView.Design is null) Clear(); else if (!unloaded) renderQueue.Request();
    }
    private void Render()
    {
        if (unloaded) return;
        if (VirtualView.Design is not { } design) { Clear(); return; }
        try
        {
            memoryObserver ??= NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.DidReceiveMemoryWarningNotification, _ =>
                DispatchQueue.MainQueue.DispatchAsync(() =>
                {
                    if (unloaded || VirtualView.Design is null) return;
                    // Keep the displayed independent snapshot while retiring the
                    // large cache. Subsequent frames use the smaller budget.
                    conserveMemory = true; requested = null; worker.Clear(); RequestRender();
                }));
            var hasBattery = design.Layers.Any(x => x.Kind == WidgetLayerKind.Battery && x.Visible && x.Opacity > 0);
            if (hasBattery && PlatformView.Window is not null) batteryObserver ??= AppleWidgetBattery.Observe(_ => RequestRender());
            else { batteryObserver?.Dispose(); batteryObserver = null; }
            var battery = hasBattery ? AppleWidgetBattery.Read() : default;
            var time = DateTimeOffset.Now;
            var identity = WidgetRenderIdentity.Create(design, time, battery);
            UpdateTimer();
            if (identity.SamePixels(requested) && !worker.DeliveryFailed) return;
            // Snapshot the repository resolver itself, never a view lookup.
            // Its file checks run only on worker-side source-cache misses.
            var frozen = design with { Layers = Array.AsReadOnly(design.Layers.Select(layer => layer.Battery is { } options
                ? layer with { Battery = options with { Stops = Array.AsReadOnly(options.Stops.ToArray()) } } : layer).ToArray()) };
            requested = identity;
            worker.RequestRender(new(frozen, identity, time, battery, VirtualView.AssetPath, conserveMemory));
        }
        catch (Exception ex) { Clear(); ReportFailure(ex, WidgetRenderStage.Validation); }
    }
    private UIImage RenderImage(Request request, CancellationToken token)
    {
        using var pool = new NSAutoreleasePool();
        try
        {
            token.ThrowIfCancellationRequested(); workerAssetPath = request.AssetPath;
            compositor ??= new(key => workerAssetPath?.Invoke(key), editorPreview: true, conserveMemory: request.ConserveMemory);
            return compositor.Render(request.Design, time: request.Time, battery: request.Battery, token: token);
        }
        catch (Exception ex)
        {
            var stage = compositor?.Stage ?? WidgetRenderStage.Initialising;
            try { ReleaseRenderer(); } catch { /* Preserve the original failure. */ }
            throw new PreviewFailure(ex, stage);
        }
    }
    private void PresentImage(Request request, UIImage image)
    {
        if (unloaded || VirtualView.Design is null) { image.Dispose(); return; }
        try
        {
            var previous = PlatformView.Image; PlatformView.Image = image; previous?.Dispose();
            if (lastBattery != request.Battery) { VirtualView.TickBattery(request.Battery); lastBattery = request.Battery; }
            if (painted is null || !request.Identity.LiveText.SequenceEqual(painted.LiveText)) VirtualView.TickClock(request.Time);
            painted = request.Identity;
        }
        catch (Exception ex) { Clear(); ReportFailure(ex, WidgetRenderStage.LiveUpdate); }
    }
    private void RenderFailed(Request request, Exception error)
    {
        if (unloaded || VirtualView.Design is null) return;
        var failure = error as PreviewFailure;
        Clear(); ReportFailure(failure?.InnerException ?? error, failure?.Stage ?? WidgetRenderStage.Initialising);
    }
    private void ReportFailure(Exception error, WidgetRenderStage stage)
    {
        var nativeName = (error as ObjCRuntime.ObjCException)?.Name;
        MauiContext?.Services.GetService<ILogger<WidgetCompositeHandler>>()?.LogWarning(
            "Widget preview failed. Stage={Stage}; ErrorType={ErrorType}; NativeError={NativeError}", stage, error.GetType().Name, nativeName);
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
            try { if (PlatformView.Window is null || VirtualView.Design is null) { StopTimer(); return; } RequestRender(); }
            catch (Exception ex) { Clear(); ReportFailure(ex, WidgetRenderStage.LiveUpdate); }
        });
        timer.FireDate = NSDate.FromTimeIntervalSinceNow(interval - DateTimeOffset.Now.ToUnixTimeMilliseconds() % (long)(interval * 1000) / 1000d);
        NSRunLoop.Main.AddTimer(timer, NSRunLoopMode.Common);
    }
    private void Loaded(object? sender, EventArgs args) { unloaded = false; RequestRender(); }
    private void Unloaded(object? sender, EventArgs args) { unloaded = true; Clear(); }
    protected override void ConnectHandler(UIImageView platformView)
    { base.ConnectHandler(platformView); unloaded = false; VirtualView.Loaded += Loaded; VirtualView.Unloaded += Unloaded; }
    protected override void DisconnectHandler(UIImageView platformView)
    { VirtualView.Loaded -= Loaded; VirtualView.Unloaded -= Unloaded; unloaded = true; Clear(); base.DisconnectHandler(platformView); }
}
