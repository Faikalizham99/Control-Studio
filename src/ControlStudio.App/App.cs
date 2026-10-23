using ControlStudio.App.Views;
using ControlStudio.App.Platforms.iOS;

namespace ControlStudio.App;

public sealed class App : Application
{
    private readonly HomePage home;
    private readonly SettingsPage settings;
    private readonly StudioPage studio;
    private readonly AppearancePreferences preferences;

    public App(HomePage home, StudioPage studio, SettingsPage settings, AppearancePreferences preferences)
    {
        this.home = home;
        this.studio = studio;
        this.settings = settings;
        this.preferences = preferences;
        preferences.Attach(this);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var tabs = new TabbedPage();
        tabs.SetDynamicResource(TabbedPage.SelectedTabColorProperty, "AccentText");
        tabs.Children.Add(new NavigationPage(home) { Title = "Home", IconImageSource = "tab_home.png" });
        tabs.Children.Add(new NavigationPage(studio) { Title = "Studio", IconImageSource = "tab_studio.png" });
        tabs.Children.Add(new NavigationPage(settings) { Title = "Settings", IconImageSource = "tab_settings.png", AutomationId = "SettingsTab" });
        var window = new Window(tabs);
        window.Activated += async (_, _) =>
        {
            preferences.SynchronizeCounterAppearance();
            await AppleRetiredWidgetReportsCleanup.RunAsync();
        };
        window.Resumed += async (_, _) => await home.RefreshAsync();
        return window;
    }
}
