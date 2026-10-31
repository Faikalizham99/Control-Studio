using Foundation;
using Microsoft.Maui;
using UIKit;

namespace ControlStudio.App;

[Register("SceneDelegate")]
public sealed class SceneDelegate : MauiUISceneDelegate
{
    public override void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        // MAUI must create and bind this scene's window before a cold-start URL is queued.
        base.WillConnect(scene, session, connectionOptions);
        foreach (var context in connectionOptions.UrlContexts) Forward(context.Url);
    }

    public override bool OpenUrl(UIScene scene, NSSet<UIOpenUrlContext> urlContexts)
    {
        var handled = base.OpenUrl(scene, urlContexts);
        foreach (var context in urlContexts) handled = Forward(context.Url) || handled;
        return handled;
    }

    private bool Forward(NSUrl url)
    {
        if (Microsoft.Maui.Controls.Application.Current is not App app || Window is not { } native || native.Handle == IntPtr.Zero) return false;
        var owner = app.Windows.FirstOrDefault(window => window.Handler?.PlatformView is UIWindow candidate
            && candidate.Handle != IntPtr.Zero && candidate.Handle == native.Handle);
        return owner is not null && app.OpenWidgetLink(owner, url.AbsoluteString);
    }
}
