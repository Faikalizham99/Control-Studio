using ControlStudio.App.Views;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Accessibility;
using System.Diagnostics;
using UIKit;
using NativeContentView = Microsoft.Maui.Platform.ContentView;

namespace ControlStudio.App.Platforms.iOS;

/// <summary>One lightweight row snapshot; real controls and the draft remain in place until drop.</summary>
internal sealed class WidgetLayerListHandler : ContentViewHandler
{
    private sealed record Row(WidgetLayerRow View, UIView Native, CGRect Frame, nfloat Alpha, CGAffineTransform Transform);
    private readonly List<Row> rows = [];
    private readonly List<(UIScrollView View, bool Enabled)> scrolls = [];
    private UILongPressGestureRecognizer? press;
    private UIView? lifted;
    private WidgetLayerDragPlan? plan;
    private NSTimer? timer;
    private UIScrollView? viewport;
    private UISelectionFeedbackGenerator? feedback;
    private readonly Stopwatch clock = new();
    private double lastTick;
    private nfloat grabOffset;
    private bool settling, clipped, changedClipping;
    private int generation;
    private double layoutWidth, layoutHeight;
    private WidgetLayerList List => (WidgetLayerList)VirtualView;

    protected override void ConnectHandler(NativeContentView platformView)
    {
        base.ConnectHandler(platformView);
        press = new UILongPressGestureRecognizer(() => Guard(Recognized))
        { MinimumPressDuration = .2, AllowableMovement = 12, NumberOfTouchesRequired = 1, CancelsTouchesInView = true };
        press.ShouldReceiveTouch = (_, touch) =>
        {
            try { return !settling && !List.IsReordering && List.IsEnabled && List.Rows.Count() > 1 && HitHandle(touch.LocationInView(platformView)) is not null; }
            catch (Exception ex) { Report(ex); return false; }
        };
        platformView.AddGestureRecognizer(press);
        List.ReorderCancelled += Cancelled; List.SizeChanged += Resized;
    }
    private WidgetLayerRow? HitHandle(CGPoint point) => List.Rows.FirstOrDefault(row => row.IsEnabled && row.DragHandle.Handler?.PlatformView is UIView handle &&
        handle.ConvertRectToView(handle.Bounds, PlatformView).Contains(point));

    private void Recognized()
    {
        if (press is null) return;
        switch (press.State)
        {
            case UIGestureRecognizerState.Began: Begin(); break;
            case UIGestureRecognizerState.Changed: if (!settling) Move(); break;
            case UIGestureRecognizerState.Ended: Drop(); break;
            case UIGestureRecognizerState.Cancelled:
            case UIGestureRecognizerState.Failed: CancelAnimated(); break;
        }
    }
    private void Begin()
    {
        if (settling || plan is not null || press is null || PlatformView.Window is null) return;
        var point = press.LocationInView(PlatformView); var source = HitHandle(point); if (source is null) return;
        foreach (var row in List.Rows)
        {
            if (row.Handler?.PlatformView is not UIView native || native.Bounds.Width <= 0 || native.Bounds.Height <= 0) { Reset(); return; }
            rows.Add(new(row, native, native.ConvertRectToView(native.Bounds, PlatformView), native.Alpha, native.Transform));
        }
        var sourceIndex = rows.FindIndex(x => x.View == source);
        // Capture before hiding anything. An unavailable snapshot must never disable the editor.
        lifted = rows[sourceIndex].Native.SnapshotView(false);
        if (lifted is null) { Reset(); return; }
        plan = List.BeginReorder(rows.Select(x => new WidgetLayerPosition(x.View.LayerId, x.Frame.Y, x.Frame.Height)).ToArray(), source.LayerId);
        if (plan is null) { Reset(); return; }
        layoutWidth = List.Width; layoutHeight = List.Height;
        clipped = PlatformView.ClipsToBounds; changedClipping = true; PlatformView.ClipsToBounds = false;
        var frame = rows[sourceIndex].Frame; grabOffset = point.Y - frame.Y;
        lifted.Frame = frame; lifted.UserInteractionEnabled = false; lifted.IsAccessibilityElement = false;
        lifted.AccessibilityElementsHidden = true; lifted.Layer.CornerRadius = 16;
        PlatformView.AddSubview(lifted);
        rows[sourceIndex].Native.Alpha = 0;
        for (var parent = PlatformView.Superview; parent is not null; parent = parent.Superview)
            if (parent is UIScrollView scroll)
            {
                scrolls.Add((scroll, scroll.ScrollEnabled)); scroll.ScrollEnabled = false;
                if (viewport is null && scroll.ContentSize.Height > scroll.Bounds.Height) viewport = scroll;
            }
        if (!UIAccessibility.IsReduceMotionEnabled)
        {
            lifted.Layer.ShadowColor = UIColor.Black.CGColor; lifted.Layer.ShadowOffset = new CGSize(0, 5);
            lifted.Layer.ShadowRadius = 10; lifted.Layer.ShadowOpacity = .28f;
            var snapshot = lifted;
            Animate(.14, () => snapshot.Transform = CGAffineTransform.MakeScale(1.025f, 1.025f));
        }
        feedback = new UISelectionFeedbackGenerator(); feedback.Prepare(); feedback.SelectionChanged();
        clock.Restart(); lastTick = 0;
        timer = NSTimer.CreateRepeatingTimer(TimeSpan.FromSeconds(1d / 60), _ => Guard(ScrollTick));
        NSRunLoop.Main.AddTimer(timer, NSRunLoopMode.Common);
    }
    private void Move()
    {
        if (plan is null || lifted is null || press is null || !ReferenceEquals(plan, List.Drag)) return;
        var point = press.LocationInView(PlatformView);
        var changed = plan.Track(point.Y, grabOffset);
        lifted.Center = new CGPoint(rows[plan.Source].Frame.GetMidX(), plan.PreviewCentre);
        if (!changed) return;
        Animate(.18, SlideRows); feedback?.SelectionChanged(); feedback?.Prepare();
    }
    private void SlideRows()
    {
        if (plan is null) return;
        for (var index = 0; index < rows.Count; index++)
            if (index != plan.Source)
                rows[index].Native.Transform = CGAffineTransform.MakeTranslation(0, (nfloat)(plan.TopAt(index) - plan.Rows[index].Top));
    }
    private void ScrollTick()
    {
        var now = clock.Elapsed.TotalSeconds; var delta = Math.Clamp(now - lastTick, 0, .04); lastTick = now;
        if (plan is null || settling || viewport is null || press is null) return;
        var location = press.LocationInView(viewport); var bounds = viewport.Bounds;
        const double edge = 56;
        var direction = location.Y < bounds.Top + edge ? -Math.Clamp((bounds.Top + edge - location.Y) / edge, 0, 1)
            : location.Y > bounds.Bottom - edge ? Math.Clamp((location.Y - bounds.Bottom + edge) / edge, 0, 1) : 0;
        if (direction == 0) return;
        var inset = viewport.AdjustedContentInset;
        var next = Math.Clamp(viewport.ContentOffset.Y + direction * 420 * delta, -inset.Top,
            Math.Max(-inset.Top, viewport.ContentSize.Height - bounds.Height + inset.Bottom));
        viewport.SetContentOffset(new CGPoint(viewport.ContentOffset.X, next), false); Move();
    }
    private void Drop()
    {
        if (plan is null || settling || press is null) return;
        var point = press.LocationInView(PlatformView);
        var inside = point.X >= -24 && point.X <= PlatformView.Bounds.Width + 24 && point.Y >= -40 && point.Y <= PlatformView.Bounds.Height + 40;
        if (viewport is not null)
        {
            var visiblePoint = press.LocationInView(viewport);
            inside &= visiblePoint.Y >= viewport.Bounds.Top - 24 && visiblePoint.Y <= viewport.Bounds.Bottom + 24;
        }
        if (!inside) { CancelAnimated(); return; }
        Move(); Settle(commit: true);
    }
    private void CancelAnimated() { if (plan is not null && !settling) Settle(commit: false); }
    private void Settle(bool commit)
    {
        if (plan is not { } current || lifted is not { } snapshot) return;
        settling = true; StopTimer();
        var token = generation;
        var description = SemanticProperties.GetDescription(rows[current.Source].View.DragHandle);
        var destination = commit ? current.TopAt(current.Source) : current.Rows[current.Source].Top;
        void Finish()
        {
            if (token != generation || !ReferenceEquals(current, List.Drag)) return;
            Guard(() =>
            {
                // Keep the snapshot above the layout until the same rows have been rearranged.
                // This prevents a frame showing the original order between drop and re-layout.
                Reset(keepSnapshot: true);
                try
                {
                    if (commit) List.CompleteReorder(current); else List.CancelReorder();
                    if (List.Content?.Handler?.PlatformView is UIView content) { content.SetNeedsLayout(); content.LayoutIfNeeded(); }
                    PlatformView.SetNeedsLayout(); PlatformView.LayoutIfNeeded();
                    if (commit && current.Source != current.Target && List.Rows.ElementAtOrDefault(current.Target)?.LayerId == current.SourceId)
                    {
                        try { SemanticScreenReader.Default.Announce($"{description}. Position {current.Target + 1} of {current.Rows.Count}."); }
                        catch (Exception ex) { Report(ex); }
                    }
                }
                finally { snapshot.RemoveFromSuperview(); snapshot.Dispose(); }
            });
        }
        Animate(.18, () =>
        {
            snapshot.Transform = CGAffineTransform.MakeIdentity(); snapshot.Layer.ShadowOpacity = 0;
            snapshot.Center = new CGPoint(rows[current.Source].Frame.GetMidX(), destination + snapshot.Bounds.Height / 2);
            if (!commit) foreach (var row in rows) row.Native.Transform = row.Transform;
        }, Finish);
    }
    private static void Animate(double duration, Action change, Action? finished = null)
    {
        if (UIAccessibility.IsReduceMotionEnabled) { UIView.PerformWithoutAnimation(change); finished?.Invoke(); return; }
        UIView.Animate(duration, 0, UIViewAnimationOptions.BeginFromCurrentState | UIViewAnimationOptions.AllowUserInteraction | UIViewAnimationOptions.CurveEaseInOut,
            change, finished ?? (() => { }));
    }
    private void Cancelled(object? sender, EventArgs args) => Reset();
    private void Resized(object? sender, EventArgs args)
    {
        if (plan is not null && (Math.Abs(List.Width - layoutWidth) > 1 || Math.Abs(List.Height - layoutHeight) > 1)) List.CancelReorder();
    }
    private void StopTimer() { timer?.Invalidate(); timer?.Dispose(); timer = null; clock.Stop(); }
    private void Reset(bool keepSnapshot = false)
    {
        generation++; StopTimer();
        foreach (var row in rows) { row.Native.Layer.RemoveAllAnimations(); row.Native.Alpha = row.Alpha; row.Native.Transform = row.Transform; }
        rows.Clear();
        if (lifted is not null)
        {
            lifted.Layer.RemoveAllAnimations();
            if (!keepSnapshot) { lifted.RemoveFromSuperview(); lifted.Dispose(); }
        }
        lifted = null; plan = null; settling = false;
        foreach (var (scroll, enabled) in scrolls) scroll.ScrollEnabled = enabled;
        scrolls.Clear(); viewport = null; feedback?.Dispose(); feedback = null;
        if (changedClipping && PlatformView is not null) PlatformView.ClipsToBounds = clipped;
        changedClipping = false;
    }
    private void Guard(Action operation)
    {
        try { operation(); }
        catch (Exception ex) { Report(ex); Reset(); List.CancelReorder(); }
    }
    private void Report(Exception ex) => MauiContext?.Services.GetService<ILogger<WidgetLayerListHandler>>()?
        .LogWarning("Widget layer drag failed. ErrorType={ErrorType}", ex.GetType().Name);
    protected override void DisconnectHandler(NativeContentView platformView)
    {
        List.CancelReorder(); Reset(); List.ReorderCancelled -= Cancelled; List.SizeChanged -= Resized;
        if (press is not null) { platformView.RemoveGestureRecognizer(press); press.ShouldReceiveTouch = null; press.Dispose(); press = null; }
        base.DisconnectHandler(platformView);
    }
}
