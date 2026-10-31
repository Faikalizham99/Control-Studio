using ControlStudio.App.Views;
using ControlStudio.App.Platforms.iOS;
using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Dispatching;

namespace ControlStudio.App;

public sealed class App : Application
{
    private readonly HomePage home;
    private readonly SettingsPage settings;
    private readonly StudioPage studio;
    private readonly AppearancePreferences preferences;
    private readonly WidgetStudioService widgets;
    private readonly AppleColorPicker colourPicker;
    private readonly ILogger logger;
    private WidgetTapRouter? widgetLinks;
    private Window? widgetWindow;
    private IDispatcherTimer? widgetRetry;

    public App(HomePage home, StudioPage studio, SettingsPage settings, AppearancePreferences preferences,
        WidgetStudioService widgets, AppleColorPicker colourPicker, ILoggerFactory logs)
    {
        this.home = home;
        this.studio = studio;
        this.settings = settings;
        this.preferences = preferences;
        this.widgets = widgets; this.colourPicker = colourPicker; logger = logs.CreateLogger<App>();
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
        widgetLinks?.Dispose(); widgetRetry?.Stop(); widgetWindow = window;
        var slots = new AppleWidgetSlotStore(widgets);
        var navigation = new WidgetTapNavigation(tabs, homeNavigation, studioNavigation, slots,
            size => new WidgetsPage(widgets, colourPicker, logger, size),
            design => new WidgetEditorPage(widgets, colourPicker, logger, design),
            size => new WidgetSlotsPage(slots, logger, size),
            (title, message) => studioNavigation.CurrentPage.DisplayAlertAsync(title, message, "OK"),
            () => window.Handler?.PlatformView is UIKit.UIWindow native && native.Handle != IntPtr.Zero
                && native.RootViewController?.PresentedViewController is null);
        var router = new WidgetTapRouter(() => navigation.Ready, navigation.OpenAsync, async ex =>
        {
            logger.LogWarning("Widget tap navigation failed. ErrorType={ErrorType}", ex.GetType().Name);
            if (window.IsActivated && window.Handler?.PlatformView is UIKit.UIWindow native
                && native.Handle != IntPtr.Zero && native.RootViewController?.PresentedViewController is null)
                await tabs.CurrentPage.DisplayAlertAsync("Unable to open widget", "Open Studio → My Widgets to try again. Your saved designs have not been changed.", "OK");
        });
        widgetLinks = router;
        var retry = window.Dispatcher.CreateTimer(); retry.Interval = TimeSpan.FromMilliseconds(250); retry.IsRepeating = true;
        widgetRetry = retry;
        EventHandler retryTick = async (_, _) => await DrainWidgetLinksAsync(window, router, retry);
        retry.Tick += retryTick;
        window.Activated += async (_, _) =>
        {
            router.SetActive(true);
            await DrainWidgetLinksAsync(window, router, retry);
            preferences.SynchronizeCounterAppearance();
            await AppleRetiredWidgetReportsCleanup.RunAsync();
        };
        window.Resumed += async (_, _) => await home.RefreshAsync();
        window.Deactivated += (_, _) => { router.SetActive(false); retry.Stop(); };
        window.Destroying += (_, _) =>
        {
            router.Dispose(); retry.Stop(); retry.Tick -= retryTick;
            if (ReferenceEquals(widgetWindow, window)) { widgetWindow = null; widgetLinks = null; widgetRetry = null; }
        };
        return window;
    }

    internal bool OpenWidgetLink(Window window, string? url)
    {
        if (!ReferenceEquals(widgetWindow, window) || !WidgetTapLink.TryParse(url, out _)) return false;
        // UIKit can deliver a URL before the MAUI window has become active.
        return window.Dispatcher.Dispatch(async () =>
        {
            if (!ReferenceEquals(widgetWindow, window) || widgetLinks is not { } router || widgetRetry is not { } retry) return;
            router.Submit(url); router.SetActive(window.IsActivated);
            await DrainWidgetLinksAsync(window, router, retry);
        });
    }

    private async Task DrainWidgetLinksAsync(Window window, WidgetTapRouter router, IDispatcherTimer retry)
    {
        if (!ReferenceEquals(widgetWindow, window)) return;
        try { await router.ProcessAsync(); }
        catch (Exception ex) { logger.LogWarning("Widget tap dispatch failed. ErrorType={ErrorType}", ex.GetType().Name); }
        finally
        {
            if (router.HasPending && window.IsActivated) retry.Start(); else retry.Stop();
        }
    }
}
