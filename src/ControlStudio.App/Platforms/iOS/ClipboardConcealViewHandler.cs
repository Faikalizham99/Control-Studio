using ControlStudio.App.Views;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using System.Diagnostics;
using UIKit;
using NativeContentView = Microsoft.Maui.Platform.ContentView;

namespace ControlStudio.App.Platforms.iOS;

public sealed class ClipboardConcealViewHandler : ContentViewHandler
{
    private static readonly IPropertyMapper<IContentView, ClipboardConcealViewHandler> ConcealMapper =
        new PropertyMapper<IContentView, ClipboardConcealViewHandler>(ContentViewHandler.Mapper)
        {
            [nameof(ClipboardConcealView.IsConcealed)] = (handler, view) =>
            {
                var conceal = (ClipboardConcealView)view;
                ((ConcealPlatformView)handler.PlatformView).SetConcealed(conceal.IsConcealed, conceal.AnimateTransition);
            },
            [nameof(ClipboardConcealView.RenderVersion)] = (handler, _) => ((ConcealPlatformView)handler.PlatformView).RefreshContent()
        };

    public ClipboardConcealViewHandler() : base(ConcealMapper) { }
    protected override NativeContentView CreatePlatformView() => new ConcealPlatformView(
        MauiContext?.Services.GetService<ILogger<ClipboardConcealViewHandler>>()) { CrossPlatformLayout = VirtualView };
    protected override void DisconnectHandler(NativeContentView platformView)
    {
        ((ConcealPlatformView)platformView).ReleaseEffects(); base.DisconnectHandler(platformView);
    }

    private sealed class ConcealPlatformView(ILogger? logger) : NativeContentView
    {
        private readonly UIImageView blur = new()
        {
            Hidden = true, Opaque = false, BackgroundColor = UIColor.Clear, ContentMode = UIViewContentMode.ScaleToFill,
            IsAccessibilityElement = false, AccessibilityElementsHidden = true, UserInteractionEnabled = true
        };
        private UIImage? still;
        private ClipboardBlurFrames? frames;
        private CADisplayLink? displayLink;
        private readonly Stopwatch clock = new();
        private CancellationTokenSource? pending;
        private NSObject? motionObserver, typeObserver;
        private Application? application;
        private EventHandler<AppThemeChangedEventArgs>? themeChanged;
        private bool concealed, released, needsCapture, animateCapture;
        private long generation;
        private CGSize renderedSize;
        private double amount, fromAmount, toAmount;
        private int currentFrame;
        private UIView? Payload => (CrossPlatformLayout as IContentView)?.PresentedContent?.Handler?.PlatformView as UIView;

        public override void SubviewAdded(UIView view)
        {
            base.SubviewAdded(view);
            if (released || blur is null || ReferenceEquals(view, blur)) return;
            // A restored hidden item can acquire its native content after mapper initialization.
            // Suppress it immediately on insertion, before the first layout/draw opportunity.
            ApplyPayloadState(view);
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            if (released) return;
            EnsureBlur(); blur.Frame = Bounds;
            if (renderedSize != Bounds.Size && (concealed || blur.Image is not null)) RefreshContent();
            ApplyPayloadState();
            if (needsCapture && Window is not null && Payload is not null) BeginCapture();
        }

        private void EnsureBlur()
        {
            if (blur.Superview != this) AddSubview(blur);
            BringSubviewToFront(blur);
        }

        public void SetConcealed(bool value, bool animate)
        {
            if (released) return;
            CancelPending(); StopAnimation(preserveImage: animate);
            concealed = value;
            animateCapture = animate && !UIAccessibility.IsReduceMotionEnabled && Window is not null;
            if (!concealed && (!animateCapture || blur.Image is null || amount <= 0)) { FinishClear(); return; }
            if (!animate) ClearImages();
            needsCapture = true; renderedSize = Bounds.Size;
            if (!animateCapture) amount = concealed ? 1 : 0;
            ApplyPayloadState(); SetNeedsLayout();
        }

        public void RefreshContent()
        {
            if (released) return;
            CancelPending(); StopAnimation(preserveImage: false); ClearImages();
            animateCapture = false; needsCapture = concealed; amount = concealed ? 1 : 0; renderedSize = Bounds.Size;
            ApplyPayloadState(); if (needsCapture) SetNeedsLayout();
        }

        private void BeginCapture()
        {
            var payload = Payload;
            var plan = ClipboardBlurPlan.Create((double)Bounds.Width, (double)Bounds.Height);
            if (payload is null || plan is null) return;
            needsCapture = false;
            UIImage? source = null;
            var wasHidden = payload.Hidden;
            try
            {
                // Only the content subtree is captured. No material, parent card, header or actions.
                // Hidden is restored synchronously, before UIKit presents another frame.
                payload.Hidden = false;
                payload.LayoutIfNeeded();
                using var format = new UIGraphicsImageRendererFormat { Opaque = false, Scale = 1, PreferredRange = UIGraphicsImageRendererFormatRange.Standard };
                using var renderer = new UIGraphicsImageRenderer(new CGSize(plan.PixelWidth, plan.PixelHeight), format);
                source = renderer.CreateImage(context =>
                {
                    context.CGContext.ScaleCTM((nfloat)(plan.PixelWidth / (double)Bounds.Width), (nfloat)(plan.PixelHeight / (double)Bounds.Height));
                    context.CGContext.TranslateCTM(-payload.Bounds.X, -payload.Bounds.Y);
                    payload.Layer.RenderInContext(context.CGContext);
                });
                if (animateCapture && blur.Image is null && amount == 0 && source.CGImage is { } cgImage)
                    SetStill(UIImage.FromImage(cgImage));
            }
            catch (Exception ex) { LogFailure(ex); source?.Dispose(); source = null; }
            finally { payload.Hidden = wasHidden; ApplyPayloadState(); }
            if (source is null)
            {
                if (!concealed) FinishClear();
                return; // Stay concealed on capture failure; no white/tinted fallback.
            }
            var request = new CancellationTokenSource(); pending = request;
            RenderAsync(source, plan, animateCapture, generation, request.Token);
        }

        private async void RenderAsync(UIImage source, ClipboardBlurPlan plan, bool animated, long requestedGeneration, CancellationToken token)
        {
            ClipboardBlurFrames? result = null;
            try
            {
                result = await ClipboardBlurRenderer.RenderAsync(source, plan, animated, token).ConfigureAwait(false);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (released || requestedGeneration != generation || token.IsCancellationRequested || Window is null) return;
                    pending?.Dispose(); pending = null;
                    frames = result; result = null;
                    if (!animated || UIAccessibility.IsReduceMotionEnabled)
                    {
                        if (concealed) FinishBlur(); else FinishClear(); return;
                    }
                    fromAmount = amount; toAmount = concealed ? 1 : 0;
                    currentFrame = ClipboardBlurPlan.FrameIndex(amount);
                    blur.Image = frames![currentFrame]; blur.Hidden = false;
                    still?.Dispose(); still = null;
                    clock.Restart(); displayLink = CADisplayLink.Create(Tick);
                    displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
                    ApplyPayloadState();
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (released || requestedGeneration != generation) return;
                    LogFailure(ex); CancelPending(); StopAnimation(preserveImage: false); ClearImages(); ApplyPayloadState();
                });
            }
            finally { result?.Dispose(); source.Dispose(); }
        }

        private void Tick()
        {
            if (released || frames is null) return;
            amount = ClipboardBlurPlan.Amount(fromAmount, toAmount, clock.Elapsed.TotalSeconds);
            currentFrame = ClipboardBlurPlan.FrameIndex(amount); blur.Image = frames[currentFrame];
            if (clock.Elapsed.TotalSeconds < ClipboardBlurPlan.Duration * Math.Abs(toAmount - fromAmount)) return;
            if (concealed) FinishBlur(); else FinishClear();
        }

        private void FinishBlur()
        {
            StopDisplayLink();
            if (frames is not null) { SetStill(frames.Take(frames.Count - 1)); frames.Dispose(); frames = null; }
            amount = 1; blur.Hidden = blur.Image is null; ApplyPayloadState();
        }

        private void FinishClear()
        {
            StopDisplayLink(); ClearImages(); amount = 0; needsCapture = false; ApplyPayloadState();
        }

        private void ApplyPayloadState()
        {
            if (Payload is { } payload) ApplyPayloadState(payload);
        }

        private void ApplyPayloadState(UIView payload)
        {
            // Native hiding retains MAUI measurement; the visible image contains only blurred pixels.
            var suppressed = concealed || (CrossPlatformLayout as ClipboardConcealView)?.IsConcealed == true || blur.Image is not null || displayLink is not null;
            payload.Hidden = suppressed; payload.AccessibilityElementsHidden = suppressed; payload.UserInteractionEnabled = !suppressed;
        }

        private void CancelPending() { generation++; var previous = pending; pending = null; previous?.Cancel(); previous?.Dispose(); }
        private void StopDisplayLink() { var previous = displayLink; displayLink = null; previous?.Invalidate(); previous?.Dispose(); clock.Stop(); }
        private void StopAnimation(bool preserveImage)
        {
            StopDisplayLink();
            if (frames is null) return;
            if (preserveImage) SetStill(frames.Take(currentFrame));
            else blur.Image = null;
            frames.Dispose(); frames = null;
        }
        private void SetStill(UIImage image) { var previous = still; still = image; blur.Image = image; blur.Hidden = false; previous?.Dispose(); }
        private void ClearImages() { blur.Image = null; blur.Hidden = true; frames?.Dispose(); frames = null; still?.Dispose(); still = null; }
        private void LogFailure(Exception ex) => logger?.LogWarning("Clipboard blur failed. ErrorType={ErrorType}", ex.GetType().Name);

        public override void MovedToWindow()
        {
            base.MovedToWindow(); if (released) return;
            if (Window is null)
            {
                RemoveObservers(); CancelPending(); StopAnimation(preserveImage: false); ClearImages();
                needsCapture = concealed; animateCapture = false; amount = concealed ? 1 : 0; ApplyPayloadState(); return;
            }
            EnsureBlur(); ApplyPayloadState(); SetNeedsLayout();
            if (motionObserver is not null) return;
            var weakSelf = new WeakReference<ConcealPlatformView>(this);
            void Changed() => MainThread.BeginInvokeOnMainThread(() =>
            {
                if (weakSelf.TryGetTarget(out var view) && !view.released && view.Window is not null) view.RefreshContent();
            });
            motionObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.ReduceMotionStatusDidChangeNotification, _ => Changed());
            typeObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.ContentSizeCategoryChangedNotification, _ => Changed());
            application = Application.Current;
            if (application is not null) { themeChanged = (_, _) => Changed(); application.RequestedThemeChanged += themeChanged; }
        }

        private void RemoveObservers()
        {
            foreach (var observer in new[] { motionObserver, typeObserver })
                if (observer is not null) { NSNotificationCenter.DefaultCenter.RemoveObserver(observer); observer.Dispose(); }
            motionObserver = typeObserver = null;
            if (application is not null && themeChanged is not null) application.RequestedThemeChanged -= themeChanged;
            application = null; themeChanged = null;
        }

        public void ReleaseEffects()
        {
            if (released) return;
            CancelPending(); StopAnimation(preserveImage: false); ClearImages(); RemoveObservers(); released = true;
            if (Payload is { } payload) { payload.Hidden = false; payload.AccessibilityElementsHidden = false; payload.UserInteractionEnabled = true; }
            blur.RemoveFromSuperview(); blur.Dispose();
        }
        protected override void Dispose(bool disposing) { if (disposing) ReleaseEffects(); base.Dispose(disposing); }
    }
}
