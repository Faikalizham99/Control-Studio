using ControlStudio.Core;
using Foundation;

namespace ControlStudio.App.Platforms.iOS;

internal static class AppleWidgetBackgroundDiagnostics
{
    public static Task<WidgetBackgroundReport> ReadAsync()
    {
        var version = AppInfo.Current.VersionString; var build = AppInfo.Current.BuildString;
        var os = DeviceInfo.Current.VersionString;
        return Task.Run(() =>
        {
            using var pool = new NSAutoreleasePool();
            using var container = NSFileManager.DefaultManager.GetContainerUrl(AppGroupConfiguration.Identifier);
            var directory = container?.Path is { } path ? Path.Combine(path, WidgetBackgroundReportReader.RelativeDirectory) : null;
            return WidgetBackgroundReportReader.Read(directory, version, build, os);
        });
    }
}
