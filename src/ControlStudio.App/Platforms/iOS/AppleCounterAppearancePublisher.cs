using System.Runtime.InteropServices;

namespace ControlStudio.App.Platforms.iOS;

public sealed class AppleCounterAppearancePublisher : ICounterAppearancePublisher
{
    [DllImport("__Internal", EntryPoint = "cs_counter_set_appearance")]
    private static extern int PublishNative(int themeIndex, [MarshalAs(UnmanagedType.LPUTF8Str)] string accentHex);

    public void Publish(int themeIndex, string accentHex)
    {
        if (PublishNative(themeIndex, accentHex) != 1) throw new IOException("Counter appearance could not be shared.");
    }
}
