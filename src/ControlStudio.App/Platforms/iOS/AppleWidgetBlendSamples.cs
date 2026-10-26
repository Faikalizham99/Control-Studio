using System.Collections.Concurrent;
using ControlStudio.App.Rendering;
using ControlStudio.Core.Widgets;
using CoreGraphics;
using CoreImage;
using Foundation;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

/// <summary>Bounded illustrative samples, using the production blend kernels and no user assets.</summary>
internal static class AppleWidgetBlendSamples
{
    private static readonly ConcurrentDictionary<WidgetBlendMode, Lazy<Task<byte[]>>> cache = new();
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static readonly Lazy<CIContext> context = new(() =>
    {
        using var rgb = CGColorSpace.CreateSrgb() ?? throw new IOException("Sample colour rendering is unavailable.");
        return CIContext.FromOptions(new CIContextOptions { WorkingColorSpace = rgb, OutputColorSpace = rgb, CacheIntermediates = false });
    });
    public static ImageSource Source(WidgetBlendMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        // Source instances belong to individual views. The process cache holds only
        // at most 14 immutable PNG byte arrays/tasks and cannot retain an editor.
        return new StreamImageSource { Stream = token => StreamAsync(mode, token) };
    }
    private static async Task<Stream> StreamAsync(WidgetBlendMode mode, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var lazy = cache.GetOrAdd(mode, value => new(() => GenerateAsync(value)));
        try { return new MemoryStream(await lazy.Value.WaitAsync(token), writable: false); }
        catch { if (lazy.IsValueCreated && lazy.Value.IsFaulted) cache.TryRemove(mode, out _); throw; }
    }
    private static async Task<byte[]> GenerateAsync(WidgetBlendMode mode)
    {
        await gate.WaitAsync();
        try { return await Task.Run(() => Render(mode)); }
        finally { gate.Release(); }
    }
    private static byte[] Render(WidgetBlendMode mode)
    {
        using var pool = new NSAutoreleasePool();
        using var rgb = CGColorSpace.CreateSrgb() ?? throw new IOException("Sample colour rendering is unavailable.");
        using var background = Shape(false); using var foreground = Shape(true);
        using var images = new ImageRenderBatch<CIImage>();
        var backInput = images.Own(CIImage.FromCGImage(background.CGImage!)); var frontInput = images.Own(CIImage.FromCGImage(foreground.CGImage!));
        var kernel = WidgetPhotoCompositor.Kernel(mode);
        var blended = images.Own(kernel.Apply(frontInput, backInput) ?? throw new IOException("Blend sample unavailable."));
        var bounds = new CGRect(0, 0, 176, 128);
        using var color = new CIColor(238 / 255f, 241 / 255f, 246 / 255f, 1); var flat = images.Own(new CIImage(color));
        var paper = images.Own(flat.ImageByCroppingToRect(bounds)); var over = WidgetPhotoCompositor.Kernel(WidgetBlendMode.Normal);
        var composite = images.Own(over.Apply(blended, paper) ?? throw new IOException("Blend sample unavailable."));
        using var bitmap = context.Value.CreateCGImage(composite, bounds, CIFormat.Rgba8, rgb) ?? throw new IOException("Blend sample unavailable.");
        using var image = new UIImage(bitmap); using var png = image.AsPNG() ?? throw new IOException("Blend sample unavailable.");
        return png.ToArray();
    }
    private static UIImage Shape(bool front)
    {
        using var format = UIGraphicsImageRendererFormat.DefaultFormat;
        format.Scale = 2; format.Opaque = false; format.PreferredRange = UIGraphicsImageRendererFormatRange.Standard;
        using var renderer = new UIGraphicsImageRenderer(new CGSize(88, 64), format);
        return renderer.CreateImage(context =>
        {
            var graphics = context.CGContext;
            graphics.AddEllipseInRect(new CGRect(front ? 34 : 8, front ? 18 : 4, 46, 46)); graphics.Clip();
            // The identical cyan/blue and orange/pink bands expose both light and
            // dark overlap examples, including the whole-colour comparison modes.
            for (var band = 0; band < 8; band++)
            {
                var t = band / 7f;
                using var fill = front ? new CGColor(.98f - .13f * t, .68f - .43f * t, .15f + .45f * t, 1)
                    : new CGColor(.12f + .03f * t, .78f - .53f * t, .90f - .12f * t, 1);
                graphics.SetFillColor(fill); graphics.FillRect(new CGRect(0, band * 8, 88, 8));
            }
        });
    }
}
