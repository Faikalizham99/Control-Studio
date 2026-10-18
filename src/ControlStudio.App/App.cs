using ControlStudio.App.Views;

namespace ControlStudio.App;

public sealed class App : Application
{
    private readonly HomePage home;
    private readonly SettingsPage settings;

    public App(HomePage home, SettingsPage settings)
    {
        this.home = home;
        this.settings = settings;
        Resources.Add("Accent", Color.FromArgb("#2458E6"));
        UserAppTheme = Preferences.Default.Get("appearance", 0) switch
        {
            1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.Unspecified
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var tabs = new TabbedPage();
        tabs.Children.Add(new NavigationPage(home) { Title = "Home" });
        tabs.Children.Add(new NavigationPage(settings) { Title = "Settings" });
        var window = new Window(tabs);
        window.Resumed += async (_, _) => await home.RefreshAsync();
        return window;
    }
}
