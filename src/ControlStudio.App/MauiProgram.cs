using ControlStudio.App.Platforms.iOS;
using ControlStudio.App.Views;
using ControlStudio.Core;
using ControlStudio.Core.Actions;
using ControlStudio.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler<TabbedPage, SpinningGearTabbedRenderer>();
            handlers.AddHandler<GlassCard, GlassCardHandler>();
            handlers.AddHandler<HexColorEntry, HexColorEntryHandler>();
            handlers.AddHandler<ProfileNameEntry, ProfileNameEntryHandler>();
            handlers.AddHandler<ClipboardLinkLabel, ClipboardLinkLabelHandler>();
            handlers.AddHandler<ClipboardConcealView, ClipboardConcealViewHandler>();
            handlers.AddHandler<ClipboardArrangeView, ClipboardArrangeViewHandler>();
            handlers.AddHandler<WidgetCanvasView, WidgetCanvasGestureHandler>();
            handlers.AddHandler<WidgetCompositeView, WidgetCompositeHandler>();
        });
        builder.Logging.AddProvider(new DeviceLoggerProvider());
        builder.Services.AddSingleton<AppearancePreferences>();
        builder.Services.AddSingleton<ICounterAppearancePublisher, AppleCounterAppearancePublisher>();
        builder.Services.AddSingleton<AppleColorPicker>();
        builder.Services.AddSingleton<WidgetStudioService>();
        builder.Services.AddSingleton<ProfilePhotoStore>();
        builder.Services.AddSingleton<INativeCounterBridge, AppleCounterBridge>();
        builder.Services.AddSingleton<ICounterRepository, NativeCounterRepository>();
        builder.Services.AddSingleton<IActionValidator, ActionValidator>();
        builder.Services.AddSingleton<IActionRepository>(services => new SqliteActionRepository(
            Path.Combine(FileSystem.Current.AppDataDirectory, "ControlStudio", "actions.sqlite3"),
            services.GetRequiredService<IActionValidator>()));
        builder.Services.AddSingleton<IActionPlatform, AppleActionPlatform>();
        builder.Services.AddSingleton<IActionExecutor, ActionExecutor>();
        builder.Services.AddSingleton<StudioPage>();
        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddSingleton<SettingsPage>();
        return builder.Build();
    }
}
