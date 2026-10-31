using Foundation;
using UIKit;

namespace ControlStudio.App;

[Register("AppDelegate")]
public sealed class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
    {
        var handled = base.OpenUrl(application, url, options);
        // Fallback for application-delegate delivery; scene delivery is handled by SceneDelegate.
        if (Microsoft.Maui.Controls.Application.Current is App app && app.Windows.Count == 1)
            handled = app.OpenWidgetLink(app.Windows[0], url.AbsoluteString) || handled;
        return handled;
    }
}
