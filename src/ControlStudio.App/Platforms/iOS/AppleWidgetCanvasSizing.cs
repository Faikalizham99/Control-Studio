using System.Text.Json;
using ControlStudio.Core.Widgets;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal static class AppleWidgetCanvasSizing
{
    public static WidgetCanvasSizing Read(WidgetSize family, Func<string> sharedDirectory)
    {
        var scene = UIApplication.SharedApplication.ConnectedScenes.OfType<UIWindowScene>()
            .FirstOrDefault(x => x.ActivationState == UISceneActivationState.ForegroundActive);
        var info = DeviceDisplay.Current.MainDisplayInfo;
        var width = (double?)scene?.Screen.Bounds.Width ?? info.Width / info.Density;
        var height = (double?)scene?.Screen.Bounds.Height ?? info.Height / info.Density;
        var scale = (double?)scene?.Screen.Scale ?? info.Density;
        var estimate = WidgetCanvasSizes.Estimate(family, width, height);
        try
        {
            var key = family switch { WidgetSize.ExtraLargePortrait => "extraLargePortrait", _ => family.ToString().ToLowerInvariant() };
            var path = Path.Combine(sharedDirectory(), "dimensions-" + key + ".json");
            if (!File.Exists(path) || new FileInfo(path).Length > 2048) return estimate;
            var bytes = File.ReadAllBytes(path); if (bytes.Length > 2048) return estimate;
            var measurement = JsonSerializer.Deserialize(bytes, WidgetDesignRules.JsonContext.WidgetDisplayMeasurement);
            if (measurement is not null && WidgetCanvasSizes.Matches(measurement, family, width, height, scale, (int)Foundation.NSProcessInfo.ProcessInfo.OperatingSystemVersion.Major))
                return new(new(measurement.Width, measurement.Height), Measured: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return estimate;
    }
}
