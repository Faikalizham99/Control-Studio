using ControlStudio.Core;
using Foundation;

namespace ControlStudio.App.Platforms.iOS;

internal static class AppleRetiredWidgetReportsCleanup
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static volatile bool completed;

    public static async Task RunAsync()
    {
        if (completed || !await gate.WaitAsync(0)) return;
        try
        {
            completed = await Task.Run(() =>
            {
                using var pool = new NSAutoreleasePool();
                using var container = NSFileManager.DefaultManager.GetContainerUrl(AppGroupConfiguration.Identifier);
                return RetiredWidgetReportsCleanup.TryRun(container?.Path, AppGroupConfiguration.Identifier, Preferences.Default);
            });
        }
        catch (Exception) { /* Shared storage may be unavailable until unlock or correct signing. */ }
        finally { gate.Release(); }
    }
}
