using System.Runtime.InteropServices;
using ControlStudio.Infrastructure;

namespace ControlStudio.App.Platforms.iOS;

public sealed class AppleCounterBridge : INativeCounterBridge
{
    [DllImport("__Internal", EntryPoint = "cs_counter_read")]
    private static extern IntPtr ReadNative();
    [DllImport("__Internal", EntryPoint = "cs_counter_increment")]
    private static extern IntPtr IncrementNative();
    [DllImport("__Internal", EntryPoint = "cs_counter_free")]
    private static extern void FreeNative(IntPtr value);

    public string Read() => Invoke(ReadNative);
    public string Increment() => Invoke(IncrementNative);

    private static string Invoke(Func<IntPtr> operation)
    {
        var pointer = operation();
        if (pointer == IntPtr.Zero) throw new IOException("The native counter could not allocate a response.");
        try { return Marshal.PtrToStringUTF8(pointer) ?? throw new IOException("Empty native response."); }
        finally { FreeNative(pointer); }
    }
}
