using System.Runtime.InteropServices;
using ControlStudio.Core.Widgets;
using CoreGraphics;
using CoreVideo;
using Foundation;
using UIKit;
using Vision;

namespace ControlStudio.App.Platforms.iOS;

internal sealed class PhotoCutoutImaging : IDisposable
{
    public UIImage Original { get; }
    public int Width { get; }
    public int Height { get; }
    private readonly byte[] rgba;
    public byte[] Pixels => rgba;
    public PhotoCutoutImaging(byte[] png)
    {
        using var data = NSData.FromArray(png);
        Original = UIImage.LoadFromData(data) ?? throw new IOException("This photo could not be opened.");
        try
        {
            var image = Original.CGImage ?? throw new IOException("This photo could not be opened.");
            Width = checked((int)image.Width); Height = checked((int)image.Height);
            if (Width is < 1 or > WidgetDesignRules.MaximumImageEdge || Height is < 1 or > WidgetDesignRules.MaximumImageEdge)
                throw new IOException("The photo is too large to edit.");
            using var colour = CGColorSpace.CreateSrgb();
            using var context = new CGBitmapContext(IntPtr.Zero, Width, Height, 8, Width * 4, colour,
                CGBitmapFlags.ByteOrder32Big | CGBitmapFlags.PremultipliedLast);
            context.DrawImage(new CGRect(0, 0, Width, Height), image);
            rgba = new byte[Width * Height * 4]; Marshal.Copy(context.Data, rgba, 0, rgba.Length);
        }
        catch { Original.Dispose(); throw; }
    }
    public UIImage Render(byte[] mask) => Render(rgba, mask);
    public UIImage Render(byte[] pixels, byte[] mask)
    {
        if (mask.Length != Width * Height) throw new IOException("The photo mask is invalid.");
        return Image(PhotoCutoutPixels.Apply(pixels, mask));
    }
    public UIImage Image(byte[] pixels)
    {
        if (pixels.Length != Width * Height * 4) throw new IOException("The photo pixels are invalid.");
        using var colour = CGColorSpace.CreateSrgb();
        using var context = new CGBitmapContext(IntPtr.Zero, Width, Height, 8, Width * 4, colour,
            CGBitmapFlags.ByteOrder32Big | CGBitmapFlags.PremultipliedLast);
        Marshal.Copy(pixels, 0, context.Data, pixels.Length);
        using var image = context.ToImage() ?? throw new IOException("The cut-out could not be rendered.");
        return new UIImage(image);
    }
    public byte[] AutomaticMask(byte[]? working = null)
    {
        if (!OperatingSystem.IsIOSVersionAtLeast(17)) throw new IOException("Automatic removal requires iOS 17 or later. You can still erase manually.");
        using var pool = new NSAutoreleasePool();
        using var repaired = working is null ? null : Render(working, Enumerable.Repeat((byte)255, Width * Height).ToArray());
        using var request = new VNGenerateForegroundInstanceMaskRequest(null);
        using var handler = new VNImageRequestHandler((repaired ?? Original).CGImage!, new VNImageOptions());
        var performed = handler.Perform([request], out var error);
        using (error)
            if (!performed) throw new IOException("Background removal is unavailable for this photo. Try manual Erase and Restore.");
        var results = request.GetResults<VNInstanceMaskObservation>();
        try
        {
            var observation = results?.FirstOrDefault() ?? throw new IOException("No subject was found. Try manual Erase and Restore.");
            using var instances = observation.AllInstances;
            if (instances.Count == 0) throw new IOException("No subject was found. Try manual Erase and Restore.");
            using var buffer = observation.GenerateScaledMask(instances, handler, out var maskError);
            using (maskError)
                if (buffer is null) throw new IOException("The background could not be removed. Try manual Erase and Restore.");
            if ((int)buffer.Width != Width || (int)buffer.Height != Height || buffer.PixelFormatType != CVPixelFormatType.OneComponent32Float)
                throw new IOException("The system returned an unsupported mask. Try manual Erase and Restore.");
            if (buffer.Lock(CVPixelBufferLock.ReadOnly) != CVReturn.Success) throw new IOException("The photo mask is unavailable. Try again.");
            try
            {
                var result = new byte[Width * Height]; var row = new float[Width];
                for (var y = 0; y < Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(buffer.BaseAddress, checked(y * (int)buffer.BytesPerRow)), row, 0, Width);
                    for (var x = 0; x < Width; x++) result[y * Width + x] = float.IsFinite(row[x]) ? (byte)Math.Round(Math.Clamp(row[x], 0, 1) * 255) : (byte)0;
                }
                return result;
            }
            finally { buffer.Unlock(CVPixelBufferLock.ReadOnly); }
        }
        finally { if (results is not null) foreach (var result in results) result.Dispose(); }
    }
    public void Dispose() => Original.Dispose();
}
