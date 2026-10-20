using Foundation;
using Microsoft.Maui.Handlers;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

public sealed class GlassCardHandler : ContentViewHandler
{
    protected override Microsoft.Maui.Platform.ContentView CreatePlatformView() => new GlassPlatformView { CrossPlatformLayout = VirtualView };

    protected override void DisconnectHandler(Microsoft.Maui.Platform.ContentView platformView)
    {
        if (platformView is GlassPlatformView glass) glass.ReleaseEffects();
        base.DisconnectHandler(platformView);
    }

    private sealed class GlassPlatformView : Microsoft.Maui.Platform.ContentView
    {
        private readonly UIVisualEffectView blur = new(UIBlurEffect.FromStyle(UIBlurEffectStyle.SystemUltraThinMaterial)) { UserInteractionEnabled = false };
        private NSObject? observer;
        private bool released;

        public GlassPlatformView()
        {
            ClipsToBounds = true; Layer.CornerRadius = 24;
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            if (released) return;
            // ContentViewHandler replaces its children when content changes.
            if (blur.Superview != this) InsertSubview(blur, 0);
            blur.Frame = Bounds;
            UpdateTransparency();
        }

        public override void MovedToWindow()
        {
            base.MovedToWindow();
            if (Window is not null && observer is null && !released)
            {
                var weakSelf = new WeakReference<GlassPlatformView>(this);
                observer = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.ReduceTransparencyStatusDidChangeNotification, _ =>
                    MainThread.BeginInvokeOnMainThread(() => { if (weakSelf.TryGetTarget(out var view) && !view.released) view.UpdateTransparency(); }));
            }
            else if (Window is null) RemoveObserver();
        }

        private void UpdateTransparency()
        {
            blur.Hidden = UIAccessibility.IsReduceTransparencyEnabled;
            BackgroundColor = blur.Hidden ? UIColor.SecondarySystemBackground : UIColor.Clear;
        }

        private void RemoveObserver()
        {
            if (observer is null) return;
            NSNotificationCenter.DefaultCenter.RemoveObserver(observer); observer.Dispose(); observer = null;
        }

        public void ReleaseEffects()
        {
            if (released) return;
            released = true; RemoveObserver(); blur.RemoveFromSuperview(); blur.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) ReleaseEffects();
            base.Dispose(disposing);
        }
    }
}
