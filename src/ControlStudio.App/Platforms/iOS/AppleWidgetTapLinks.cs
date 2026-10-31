using Foundation;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

/// <summary>Optional lifecycle observers. Managed exceptions never escape into UIKit/MAUI startup.</summary>
internal static class AppleWidgetTapLinks
{
    private static readonly WidgetTapLaunchInbox Launch = new();

    public static void Connecting(UIScene scene, UISceneSession session, UISceneConnectionOptions options)
    {
        try
        {
            var urls = options?.UrlContexts;
            if (urls is null || urls.Handle == IntPtr.Zero || urls.Count == 0) return;
            Launch.Capture(session.PersistentIdentifier, Addresses(urls));
        }
        catch (Exception ex) { Report(ex); }
    }
    public static void WindowCreated(Microsoft.Maui.Controls.Window window)
    {
        if (!Launch.HasPending) return;
        try
        {
            if (window.Handler?.PlatformView is UIWindow native) DeliverWindow(native);
        }
        catch (Exception ex) { Report(ex); }
    }
    public static void Activated(UIScene scene)
    {
        if (!Launch.HasPending) return;
        try
        {
            var window = WindowFor(scene);
            if (window is not null) DeliverWindow(window);
        }
        catch (Exception ex) { Report(ex); }
    }
    public static void Disconnected(UIScene scene)
    {
        if (!Launch.HasPending) return;
        try { Launch.Disconnect(scene.Session.PersistentIdentifier); }
        catch (Exception ex) { Report(ex); }
    }
    public static bool Open(UIScene scene, NSSet<UIOpenUrlContext>? contexts)
    {
        var handled = false;
        try
        {
            if (contexts is null || contexts.Handle == IntPtr.Zero || contexts.Count == 0) return false;
            var window = WindowFor(scene); if (window is null) return false;
            foreach (var url in Addresses(contexts)) handled = Forward(window, url) || handled;
        }
        catch (Exception ex) { Report(ex); }
        return handled;
    }
    public static bool OpenApplication(NSUrl? url)
    {
        try
        {
            if (url is null || url.Handle == IntPtr.Zero || Microsoft.Maui.Controls.Application.Current is not App app || app.Windows.Count != 1) return false;
            return app.OpenWidgetLink(app.Windows[0], url.AbsoluteString);
        }
        catch (Exception ex) { Report(ex); return false; }
    }
    private static IEnumerable<string?> Addresses(NSSet<UIOpenUrlContext> contexts)
    {
        foreach (var context in contexts)
            if (context is not null && context.Handle != IntPtr.Zero && context.Url is { } url && url.Handle != IntPtr.Zero)
                yield return url.AbsoluteString;
    }
    private static UIWindow? WindowFor(UIScene scene)
    {
        if (scene.Handle == IntPtr.Zero || Microsoft.Maui.Controls.Application.Current is not App app) return null;
        return app.Windows.Select(window => window.Handler?.PlatformView).OfType<UIWindow>()
            .FirstOrDefault(window => window.Handle != IntPtr.Zero && window.WindowScene?.Handle == scene.Handle);
    }
    private static void DeliverWindow(UIWindow window)
    {
        if (window.Handle == IntPtr.Zero || window.WindowScene is not { } scene) return;
        Launch.Deliver(scene.Session.PersistentIdentifier, url => Forward(window, url));
    }
    private static bool Forward(UIWindow native, string? url)
    {
        if (native.Handle == IntPtr.Zero || Microsoft.Maui.Controls.Application.Current is not App app) return false;
        var owner = app.Windows.FirstOrDefault(window => window.Handler?.PlatformView is UIWindow candidate
            && candidate.Handle != IntPtr.Zero && candidate.Handle == native.Handle);
        return owner is not null && app.OpenWidgetLink(owner, url);
    }
    internal static void Report(Exception error)
    {
        try
        {
            IPlatformApplication.Current?.Services.GetService<ILoggerFactory>()?.CreateLogger("WidgetTap")
                .LogWarning("Optional widget link handling failed. ErrorType={ErrorType}", error.GetType().Name);
        }
        catch { /* Logging is optional too, especially before a window exists. */ }
    }
}
