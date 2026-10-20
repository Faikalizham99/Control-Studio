using ControlStudio.App.Views;

namespace ControlStudio.App;

public sealed class App : Application
{
    private readonly HomePage home;
    private readonly SettingsPage settings;
    private readonly ShortcutsPage shortcuts;

    public App(HomePage home, ShortcutsPage shortcuts, SettingsPage settings, AppearancePreferences preferences)
    {
        this.home = home;
        this.shortcuts = shortcuts;
        this.settings = settings;
        preferences.Attach(this);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var tabs = new TabbedPage();
        tabs.SetDynamicResource(TabbedPage.SelectedTabColorProperty, "Accent");
        tabs.Children.Add(new NavigationPage(home) { Title = "Home", IconImageSource = "tab_home.png" });
        tabs.Children.Add(new NavigationPage(shortcuts) { Title = "Shortcuts", IconImageSource = "tab_shortcuts.png" });
        tabs.Children.Add(new NavigationPage(settings) { Title = "Settings", IconImageSource = "tab_settings.png", AutomationId = "SettingsTab" });
        var window = new Window(tabs);
        window.Resumed += async (_, _) => await home.RefreshAsync();
        return window;
    }
}
