using ControlStudio.App.Platforms.iOS;
using ControlStudio.App.Views;
using ControlStudio.Core;
using ControlStudio.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Logging.AddProvider(new DeviceLoggerProvider());
        builder.Services.AddSingleton<INativeCounterBridge, AppleCounterBridge>();
        builder.Services.AddSingleton<ICounterRepository, NativeCounterRepository>();
        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddSingleton<SettingsPage>();
        return builder.Build();
    }
}
