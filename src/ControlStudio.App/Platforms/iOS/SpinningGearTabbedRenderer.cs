using System.Diagnostics;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

/// <summary>Animates the public tab item image without accessing UIKit's private view hierarchy.</summary>
public sealed class SpinningGearTabbedRenderer : TabbedRenderer
{
    private const int FrameCount = 24;
    private readonly List<NSObject> observers = [];
    private readonly List<UIImage> frames = [];
    private NSTimer? timer;
    private UITabBarItem? gearItem;
    private UIImage? originalImage;
    private UIImage? originalSelectedImage;
    private long startedAt;
    private int lastFrame = -1;
    private bool visible;
    private bool disposed;
    private bool updating;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Observe(UIApplication.DidBecomeActiveNotification);
        Observe(UIApplication.WillResignActiveNotification);
        Observe(UIApplication.DidEnterBackgroundNotification);
        Observe(UIApplication.ReduceMotionStatusDidChangeNotification);
    }

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        visible = true;
        UpdateAnimation();
    }

    public override void ViewDidDisappear(bool animated)
    {
        visible = false;
        StopAnimation();
        base.ViewDidDisappear(animated);
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        UpdateAnimation();
    }

    protected override void OnElementChanged(VisualElementChangedEventArgs e)
    {
        base.OnElementChanged(e);
        if (e.NewElement is null)
        {
            visible = false;
            StopAnimation();
        }
    }

    private void Observe(NSString notification)
    {
        var weakSelf = new WeakReference<SpinningGearTabbedRenderer>(this);
        observers.Add(NSNotificationCenter.DefaultCenter.AddObserver(notification, _ =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (weakSelf.TryGetTarget(out var renderer)) renderer.UpdateAnimation();
            });
        }));
    }

    private bool CanAnimate => !disposed && visible && !TabBar.Hidden && TabBar.Window is not null &&
        UIApplication.SharedApplication.ApplicationState == UIApplicationState.Active &&
        !UIAccessibility.IsReduceMotionEnabled;

    private void UpdateAnimation()
    {
        if (updating) return;
        updating = true;
        try { UpdateAnimationCore(); }
        finally { updating = false; }
    }

    private void UpdateAnimationCore()
    {
        if (!CanAnimate) { StopAnimation(); return; }
        var item = TabBar.Items?.FirstOrDefault(x => x.AccessibilityIdentifier == "SettingsTab");
        if (timer is not null && ReferenceEquals(item, gearItem)) return;
        StopAnimation();
        if (item?.Image is not { } source) return;

        gearItem = item;
        originalImage = source;
        originalSelectedImage = item.SelectedImage;
        using var format = UIGraphicsImageRendererFormat.DefaultFormat;
        format.Opaque = false;
        format.Scale = source.CurrentScale;
        using var imageRenderer = new UIGraphicsImageRenderer(source.Size, format);
        // The eight identical teeth repeat every 45 degrees. Cache one second's
        // frames and reuse them for a slow eight-second revolution.
        for (var index = 0; index < FrameCount; index++)
        {
            var angle = (nfloat)(index * (Math.PI / 4) / FrameCount);
            using var rendered = imageRenderer.CreateImage(context =>
            {
                context.CGContext.TranslateCTM(source.Size.Width / 2, source.Size.Height / 2);
                context.CGContext.RotateCTM(angle);
                source.Draw(new CGRect(-source.Size.Width / 2, -source.Size.Height / 2, source.Size.Width, source.Size.Height));
            });
            frames.Add(rendered.ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate));
        }
        startedAt = Stopwatch.GetTimestamp();
        var weakSelf = new WeakReference<SpinningGearTabbedRenderer>(this);
        timer = NSTimer.CreateRepeatingTimer(TimeSpan.FromSeconds(1d / FrameCount), currentTimer =>
        {
            if (weakSelf.TryGetTarget(out var renderer)) renderer.AdvanceFrame();
            else { currentTimer.Invalidate(); currentTimer.Dispose(); }
        });
        NSRunLoop.Main.AddTimer(timer, NSRunLoopMode.Common);
    }

    private void AdvanceFrame()
    {
        if (!CanAnimate) { StopAnimation(); return; }
        var index = (int)((Stopwatch.GetElapsedTime(startedAt).TotalSeconds % 1) * FrameCount);
        if (index == lastFrame || gearItem is null) return;
        lastFrame = index;
        UIView.PerformWithoutAnimation(() =>
        {
            gearItem.Image = frames[index];
            gearItem.SelectedImage = frames[index];
        });
    }

    private void StopAnimation()
    {
        timer?.Invalidate(); timer?.Dispose(); timer = null;
        if (gearItem is not null && gearItem.Handle != IntPtr.Zero)
        {
            gearItem.Image = originalImage;
            gearItem.SelectedImage = originalSelectedImage;
        }
        gearItem = null; originalImage = null; originalSelectedImage = null;
        foreach (var frame in frames) frame.Dispose();
        frames.Clear(); lastFrame = -1;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            visible = false;
            StopAnimation();
            foreach (var observer in observers)
            {
                NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
                observer.Dispose();
            }
            observers.Clear();
        }
        base.Dispose(disposing);
    }
}
