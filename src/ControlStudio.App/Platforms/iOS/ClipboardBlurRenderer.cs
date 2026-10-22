using CoreImage;
using CoreGraphics;
using Foundation;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal sealed class ClipboardBlurFrames(UIImage?[] images) : IDisposable
{
    public int Count => images.Length;
    public UIImage this[int index] => images[index] ?? throw new ObjectDisposedException(nameof(ClipboardBlurFrames));
    public UIImage Take(int index)
    {
        var result = this[index]; images[index] = null; return result;
    }
    public void Dispose() { foreach (var image in images) image?.Dispose(); Array.Clear(images); }
}

internal static class ClipboardBlurRenderer
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Lazy<CIContext> Context = new(() => CIContext.FromOptions(new CIContextOptions { CacheIntermediates = false }));

    public static async Task<ClipboardBlurFrames> RenderAsync(UIImage source, ClipboardBlurPlan plan, bool animated, CancellationToken token)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try { return await Task.Run(() => Render(source, plan, animated, token), token).ConfigureAwait(false); }
        finally { Gate.Release(); }
    }

    private static ClipboardBlurFrames Render(UIImage source, ClipboardBlurPlan plan, bool animated, CancellationToken token)
    {
        using var pool = new NSAutoreleasePool();
        var images = new UIImage?[animated ? ClipboardBlurPlan.AnimationFrames : 1];
        try
        {
            var cgSource = source.CGImage ?? throw new InvalidOperationException("Blur source is unavailable.");
            using var input = CIImage.FromCGImage(cgSource);
            using var clamped = input.CreateByClampingToExtent();
            using var filter = new CIGaussianBlur { InputImage = clamped };
            using var colorSpace = CGColorSpace.CreateSrgb();
            for (var index = 0; index < images.Length; index++)
            {
                token.ThrowIfCancellationRequested();
                var amount = animated ? (double)index / (images.Length - 1) : 1;
                if (amount == 0) { images[index] = UIImage.FromImage(cgSource); continue; }
                filter.Radius = (float)(plan.Radius * amount);
                using var blurred = filter.OutputImage ?? throw new InvalidOperationException("Blur output is unavailable.");
                using var cropped = blurred.ImageByCroppingToRect(input.Extent);
                using var cgBlurred = Context.Value.CreateCGImage(cropped, input.Extent, CIFormat.Rgba8, colorSpace) ?? throw new InvalidOperationException("Blur could not be rendered.");
                images[index] = UIImage.FromImage(cgBlurred);
            }
            token.ThrowIfCancellationRequested();
            return new ClipboardBlurFrames(images);
        }
        catch { foreach (var image in images) image?.Dispose(); throw; }
    }
}
