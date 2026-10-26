using ControlStudio.Core.Widgets;
using Foundation;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

// All ownership changes and reads run on the main thread. Preserve another owner's monitoring.
internal static class AppleWidgetBattery
{
    private static int owners;
    private static bool previouslyEnabled;
    public static WidgetBatteryReading Read()
    {
        var device = UIDevice.CurrentDevice; var enabled = device.BatteryMonitoringEnabled;
        if (!enabled) device.BatteryMonitoringEnabled = true;
        try
        {
            var level = device.BatteryLevel; var state = device.BatteryState;
            if (!float.IsFinite(level) || level is < 0 or > 1) return default;
            return new(Math.Clamp((int)Math.Round(level * 100, MidpointRounding.AwayFromZero), 0, 100),
                state is UIDeviceBatteryState.Charging or UIDeviceBatteryState.Full, state == UIDeviceBatteryState.Full);
        }
        finally { if (!enabled) device.BatteryMonitoringEnabled = false; }
    }
    public static IDisposable Observe(Action<WidgetBatteryReading> changed)
    {
        if (owners == 0) { previouslyEnabled = UIDevice.CurrentDevice.BatteryMonitoringEnabled; UIDevice.CurrentDevice.BatteryMonitoringEnabled = true; }
        owners++;
        try { return new Observation(changed); }
        catch { Release(); throw; }
    }
    private static void Release() { if (--owners == 0 && !previouslyEnabled) UIDevice.CurrentDevice.BatteryMonitoringEnabled = false; }
    private sealed class Observation : IDisposable
    {
        private NSObject? level, state;
        private bool disposed;
        public Observation(Action<WidgetBatteryReading> changed)
        {
            void Update(NSNotification _) => MainThread.BeginInvokeOnMainThread(() => { if (!disposed) changed(Read()); });
            try
            {
                level = NSNotificationCenter.DefaultCenter.AddObserver(UIDevice.BatteryLevelDidChangeNotification, Update);
                state = NSNotificationCenter.DefaultCenter.AddObserver(UIDevice.BatteryStateDidChangeNotification, Update);
            }
            catch { disposed = true; level?.Dispose(); state?.Dispose(); throw; }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            level?.Dispose(); state?.Dispose(); level = state = null;
            Release();
        }
    }
}
