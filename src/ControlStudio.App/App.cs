using ControlStudio.App.Views;
using ControlStudio.App.Platforms.iOS;
using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App;

public sealed class App : Application
{
    private readonly HomePage home;
    private readonly SettingsPage settings;
    private readonly StudioPage studio;
    private readonly AppearancePreferences preferences;
    private WidgetTapSession? widgetLinks;
    private Window? widgetWindow;

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
        var homeNavigation = new NavigationPage(home) { Title = "Home", IconImageSource = "tab_home.png" };
        var studioNavigation = new NavigationPage(studio) { Title = "Studio", IconImageSource = "tab_studio.png" };
        tabs.Children.Add(homeNavigation);
        tabs.Children.Add(studioNavigation);
        tabs.Children.Add(new NavigationPage(settings) { Title = "Settings", IconImageSource = "tab_settings.png", AutomationId = "SettingsTab" });
        var window = new Window(tabs);
        widgetLinks?.Dispose(); widgetWindow = window;
        // Delegate capture only: no service resolution, native routing or timer on normal launch.
        var links = new WidgetTapSession(() => CreateWidgetRouter(window, tabs, homeNavigation, studioNavigation),
            () => window.Dispatcher.CreateTimer(), AppleWidgetTapLinks.Report);
        widgetLinks = links;
        window.Created += (_, _) => AppleWidgetTapLinks.WindowCreated(window);
        window.Activated += async (_, _) =>
        {
            links.SetActive(true); QueueWidgetLinks(window, links);
            preferences.SynchronizeCounterAppearance();
            await AppleRetiredWidgetReportsCleanup.RunAsync();
        };
        window.Resumed += async (_, _) => await home.RefreshAsync();
        window.Deactivated += (_, _) => links.SetActive(false);
        window.Destroying += (_, _) =>
        {
            links.Dispose();
            if (ReferenceEquals(widgetWindow, window)) { widgetWindow = null; widgetLinks = null; }
        };
        return window;
    }

    internal bool OpenWidgetLink(Window window, string? url)
    {
        try
        {
            if (!ReferenceEquals(widgetWindow, window) || widgetLinks is not { } links || !WidgetTapLink.TryParse(url, out _)) return false;
            if (!links.Submit(url)) return false;
            // Cold URLs are retained until activation; optional work is posted after the callback.
            links.SetActive(window.IsActivated);
            QueueWidgetLinks(window, links); return true;
        }
        catch (Exception ex) { AppleWidgetTapLinks.Report(ex); return false; }
    }

    private void QueueWidgetLinks(Window window, WidgetTapSession links)
    {
        if (!ReferenceEquals(widgetWindow, window) || !window.IsActivated || !links.HasPending) return;
        try
        {
            window.Dispatcher.Dispatch(async () =>
            {
                if (ReferenceEquals(widgetWindow, window)) await links.ProcessAsync();
            });
        }
        catch (Exception ex) { AppleWidgetTapLinks.Report(ex); }
    }

    private WidgetTapRouter CreateWidgetRouter(Window window, TabbedPage tabs, NavigationPage homeNavigation, NavigationPage studioNavigation)
    {
        var services = window.Handler?.MauiContext?.Services ?? throw new InvalidOperationException("Widget window is not ready.");
        var widgets = services.GetRequiredService<WidgetStudioService>();
        var colourPicker = services.GetRequiredService<AppleColorPicker>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<App>();
        var slots = new AppleWidgetSlotStore(widgets);
        var navigation = new WidgetTapNavigation(tabs, homeNavigation, studioNavigation, slots,
            size => new WidgetsPage(widgets, colourPicker, logger, size),
            design => new WidgetEditorPage(widgets, colourPicker, logger, design),
            size => new WidgetSlotsPage(slots, logger, size),
            (title, message) => studioNavigation.CurrentPage.DisplayAlertAsync(title, message, "OK"),
            () => window.Handler?.PlatformView is UIKit.UIWindow native && native.Handle != IntPtr.Zero
                && native.RootViewController?.PresentedViewController is null);
        return new(() => navigation.Ready, navigation.OpenAsync, async ex =>
        {
            AppleWidgetTapLinks.Report(ex);
            if (window.IsActivated && window.Handler?.PlatformView is UIKit.UIWindow native
                && native.Handle != IntPtr.Zero && native.RootViewController?.PresentedViewController is null)
                await tabs.CurrentPage.DisplayAlertAsync("Unable to open widget", "Open Studio → My Widgets to try again. Your saved designs have not been changed.", "OK");
        });
    }
}
