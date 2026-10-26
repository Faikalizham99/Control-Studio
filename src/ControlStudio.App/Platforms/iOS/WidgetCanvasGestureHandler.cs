using ControlStudio.App.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using UIKit;
using NativeContentView = Microsoft.Maui.Platform.ContentView;

namespace ControlStudio.App.Platforms.iOS;

/// <summary>Arbitrates canvas manipulation with the enclosing vertical scroll view.</summary>
internal sealed class WidgetCanvasGestureHandler : ContentViewHandler
{
    private UITapGestureRecognizer? tap;
    private UIPanGestureRecognizer? pan;
    private UIPinchGestureRecognizer? pinch;
    private UIGestureRecognizer? owner;
    private readonly List<(UIScrollView View, bool Enabled)> scrolls = [];
    private CoreGraphics.CGPoint pinchOrigin;
    private WidgetCanvasView Canvas => (WidgetCanvasView)VirtualView;
    protected override void ConnectHandler(NativeContentView platformView)
    {
        base.ConnectHandler(platformView);
        tap = new UITapGestureRecognizer(() => Guard(() =>
        {
            var point = tap!.LocationInView(platformView); Canvas.SelectAt(point.X, point.Y);
        }));
        pan = new UIPanGestureRecognizer(() => Guard(() =>
        {
            if (pan is not { } gesture) return;
            if (owner == gesture && gesture.State == UIGestureRecognizerState.Changed)
            {
                if (gesture.NumberOfTouches != 1) Canvas.EndInteraction(false);
                else { var delta = gesture.TranslationInView(platformView); var point = gesture.LocationInView(platformView); Canvas.Drag(point.X, point.Y, delta.X, delta.Y); }
            }
            else if (owner == gesture && gesture.State == UIGestureRecognizerState.Ended && !Canvas.IsRotating)
            { var delta = gesture.TranslationInView(platformView); Canvas.Pan(delta.X, delta.Y); }
            Finish(gesture);
        })) { MaximumNumberOfTouches = 1 };
        pinch = new UIPinchGestureRecognizer(() => Guard(() =>
        {
            if (pinch is not { } gesture) return;
            if (owner == gesture)
                WidgetPinchInput.Update(gesture.State == UIGestureRecognizerState.Changed, (int)gesture.NumberOfTouches,
                    () =>
                    {
                        var point = gesture.LocationInView(platformView);
                        Canvas.Pinch(gesture.Scale, point.X - pinchOrigin.X, point.Y - pinchOrigin.Y);
                    }, () => Canvas.EndInteraction(false));
            Finish(gesture);
        }));
        pan.ShouldBegin = _ => Begin(pan, false); pinch.ShouldBegin = _ => Begin(pinch, true);
        tap.RequireGestureRecognizerToFail(pan); tap.RequireGestureRecognizerToFail(pinch);
        platformView.AddGestureRecognizer(tap); platformView.AddGestureRecognizer(pan); platformView.AddGestureRecognizer(pinch);
        Canvas.InteractionChanged += InteractionChanged;
    }
    private bool Begin(UIGestureRecognizer recognizer, bool isPinch)
    {
        try
        {
            if (isPinch && recognizer.NumberOfTouches != 2) return false;
            var point = recognizer.LocationInView(PlatformView);
            if (recognizer is UIPanGestureRecognizer moving)
            { var translation = moving.TranslationInView(PlatformView); point.X -= translation.X; point.Y -= translation.Y; }
            if (!Canvas.BeginInteraction(point.X, point.Y, isPinch)) return false;
            owner = recognizer;
            if (recognizer is UIPinchGestureRecognizer resizing) { pinchOrigin = point; resizing.Scale = 1; }
            for (var parent = PlatformView.Superview; parent is not null; parent = parent.Superview)
                if (parent is UIScrollView found) { scrolls.Add((found, found.ScrollEnabled)); found.ScrollEnabled = false; }
            return true;
        }
        catch (Exception ex) { Report(ex); Canvas.EndInteraction(true); RestoreScroll(); return false; }
    }
    private void Finish(UIGestureRecognizer recognizer)
    {
        // A competing recognizer can fail while another gesture still owns the draft.
        if (owner != recognizer) return;
        if (recognizer.State is UIGestureRecognizerState.Ended or UIGestureRecognizerState.Cancelled or UIGestureRecognizerState.Failed)
        { Canvas.EndInteraction(recognizer.State != UIGestureRecognizerState.Ended); RestoreScroll(); }
    }
    private void Guard(Action operation)
    {
        try { operation(); }
        catch (Exception ex) { Report(ex); Canvas.EndInteraction(true); RestoreScroll(); }
    }
    private void Report(Exception ex) => MauiContext?.Services.GetService<ILogger<WidgetCanvasGestureHandler>>()?
        .LogWarning("Widget canvas gesture failed. ErrorType={ErrorType}", ex.GetType().Name);
    private void InteractionChanged(object? sender, EventArgs args) { if (!Canvas.IsInteracting) { owner = null; RestoreScroll(); } }
    private void RestoreScroll() { foreach (var (view, enabled) in scrolls) view.ScrollEnabled = enabled; scrolls.Clear(); }
    protected override void DisconnectHandler(NativeContentView platformView)
    {
        Canvas.EndInteraction(true); Canvas.InteractionChanged -= InteractionChanged; RestoreScroll();
        foreach (var recognizer in new UIGestureRecognizer?[] { tap, pan, pinch })
            if (recognizer is not null) { platformView.RemoveGestureRecognizer(recognizer); recognizer.Dispose(); }
        tap = null; pan = null; pinch = null; base.DisconnectHandler(platformView);
    }
}
