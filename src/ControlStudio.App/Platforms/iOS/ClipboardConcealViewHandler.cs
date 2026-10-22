using ControlStudio.App.Views;
using Foundation;
using Microsoft.Maui.Handlers;
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
            }
        };

    public ClipboardConcealViewHandler() : base(ConcealMapper) { }
    protected override NativeContentView CreatePlatformView() => new ConcealPlatformView { CrossPlatformLayout = VirtualView };
    protected override void DisconnectHandler(NativeContentView platformView)
    {
        ((ConcealPlatformView)platformView).ReleaseEffects();
        base.DisconnectHandler(platformView);
    }

    private sealed class ConcealPlatformView : NativeContentView
    {
        private readonly UIVisualEffectView cover = new((UIVisualEffect?)null) { Hidden = true };
        private readonly UIView tint = new() { BackgroundColor = UIColor.SecondarySystemBackground, Alpha = 0, UserInteractionEnabled = false };
        private readonly UILabel caption = new()
        {
            Text = "Content hidden", TextColor = UIColor.SecondaryLabel, TextAlignment = UITextAlignment.Center,
            Font = UIFont.PreferredCaption1!, Alpha = 0, UserInteractionEnabled = false, IsAccessibilityElement = false
        };
        private readonly UIBlurEffect effect = UIBlurEffect.FromStyle(UIBlurEffectStyle.SystemThickMaterial);
        private UIViewPropertyAnimator? animator;
        private NSObject? motionObserver, transparencyObserver;
        private bool concealed, released;
        private UIView? Payload => (CrossPlatformLayout as IContentView)?.PresentedContent?.Handler?.PlatformView as UIView;

        public ConcealPlatformView()
        {
            cover.ClipsToBounds = true; cover.Layer.CornerRadius = 14;
            cover.ContentView.AddSubview(tint); cover.ContentView.AddSubview(caption);
            cover.IsAccessibilityElement = false; cover.AccessibilityElementsHidden = true;
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            if (released) return;
            EnsureCover(); cover.Frame = Bounds; tint.Frame = cover.ContentView.Bounds; caption.Frame = cover.ContentView.Bounds;
            ApplyPayloadState();
        }

        private void EnsureCover()
        {
            // MAUI replaces native children when content changes; always restore the topmost cover.
            if (cover.Superview != this) AddSubview(cover);
            BringSubviewToFront(cover);
        }

        public void SetConcealed(bool value, bool animate)
        {
            if (released) return;
            StopAnimation(); concealed = value; EnsureCover();
            var shouldAnimate = animate && Window is not null && !UIAccessibility.IsReduceMotionEnabled && !UIAccessibility.IsReduceTransparencyEnabled;
            if (!shouldAnimate) { Finish(); return; }
            cover.Hidden = false;
            if (Payload is { } payload) payload.Hidden = false;
            var transition = new UIViewPropertyAnimator(.26, UIViewAnimationCurve.EaseInOut, () =>
            {
                // Keep effect-view alpha at 1; animate the effect and a separate opaque tint.
                cover.Effect = concealed ? effect : null;
                tint.Alpha = caption.Alpha = concealed ? 1 : 0;
            });
            animator = transition; ApplyPayloadState();
            transition.AddCompletion(_ =>
            {
                if (animator != transition) return;
                animator = null; Finish(); transition.Dispose();
            });
            transition.StartAnimation();
        }

        private void ApplyPayloadState()
        {
            if (Payload is not { } payload) return;
            // Native hiding preserves MAUI measurement while removing the actual pixels at rest.
            payload.Hidden = concealed && animator is null;
            payload.AccessibilityElementsHidden = concealed || animator is not null;
            payload.UserInteractionEnabled = !concealed && animator is null;
        }

        private void Finish()
        {
            cover.Effect = concealed && !UIAccessibility.IsReduceTransparencyEnabled ? effect : null;
            tint.Alpha = caption.Alpha = concealed ? 1 : 0;
            cover.Hidden = !concealed; ApplyPayloadState();
        }

        private void StopAnimation()
        {
            var previous = animator; animator = null;
            if (previous is null) return;
            previous.StopAnimation(true); previous.Dispose();
        }

        public override void MovedToWindow()
        {
            base.MovedToWindow();
            if (released) return;
            if (Window is null) { RemoveObservers(); SetConcealed(concealed, animate: false); return; }
            if (motionObserver is not null) return;
            var weakSelf = new WeakReference<ConcealPlatformView>(this);
            void Changed(NSNotification _) => MainThread.BeginInvokeOnMainThread(() =>
            {
                if (weakSelf.TryGetTarget(out var view) && !view.released) view.SetConcealed(view.concealed, animate: false);
            });
            motionObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.ReduceMotionStatusDidChangeNotification, Changed);
            transparencyObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.ReduceTransparencyStatusDidChangeNotification, Changed);
        }

        private void RemoveObservers()
        {
            foreach (var observer in new[] { motionObserver, transparencyObserver })
                if (observer is not null) { NSNotificationCenter.DefaultCenter.RemoveObserver(observer); observer.Dispose(); }
            motionObserver = transparencyObserver = null;
        }

        public void ReleaseEffects()
        {
            if (released) return;
            StopAnimation(); RemoveObservers();
            if (Payload is { } payload) { payload.Hidden = false; payload.AccessibilityElementsHidden = false; payload.UserInteractionEnabled = true; }
            released = true; cover.RemoveFromSuperview(); caption.Dispose(); tint.Dispose(); cover.Dispose(); effect.Dispose();
        }
        protected override void Dispose(bool disposing) { if (disposing) ReleaseEffects(); base.Dispose(disposing); }
    }
}
