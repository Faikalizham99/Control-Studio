using ControlStudio.Core.Widgets;
using System.Collections.Concurrent;
using CoreGraphics;
using CoreImage;
using Foundation;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

/// <summary>Shared sRGB composition for live preview and export. Owned by one caller/thread.</summary>
internal sealed class WidgetPhotoCompositor : IDisposable
{
    // Built-in kernels are shared immutable native objects. Retain their wrappers
    // for the app lifetime; a background sample must not dispose a live editor's kernel.
    private static readonly ConcurrentDictionary<WidgetBlendMode, CIBlendKernel> kernels = new();
    private readonly Func<string, string?> path;
    private readonly CGColorSpace colorSpace = CGColorSpace.CreateSrgb() ?? throw new IOException("Widget colour rendering is unavailable.");
    private readonly CIContext context;
    private readonly Dictionary<Guid, Raster> rasters = [];
    private readonly Dictionary<string, UIImage> sources = [];
    private sealed record Raster(WidgetLayer Layer, CGSize Size, UIImage Image, CIImage Input, string ClockText = "") : IDisposable
    {
        public void Dispose() { Input.Dispose(); Image.Dispose(); }
    }
    public WidgetPhotoCompositor(Func<string, string?> path)
    {
        this.path = path;
        try { context = CIContext.FromOptions(new CIContextOptions { WorkingColorSpace = colorSpace, OutputColorSpace = colorSpace, CacheIntermediates = false }); }
        catch { colorSpace.Dispose(); throw; }
    }
    public UIImage Render(WidgetDesign design, DateTimeOffset? time = null, int maximumEdge = WidgetDesignRules.MaximumImageEdge)
    {
        WidgetDesignRules.Validate(design);
        using var pool = new NSAutoreleasePool();
        var canvas = WidgetDesignRules.Canvas(design);
        var scale = Math.Min(2, Math.Clamp(maximumEdge, 128, WidgetDesignRules.MaximumImageEdge) / Math.Max(canvas.Width, canvas.Height));
        var size = new CGSize(Math.Floor(canvas.Width * scale), Math.Floor(canvas.Height * scale));
        var bounds = new CGRect(CGPoint.Empty, size);
        // Keep only visible layers. Changing blend mode/order/background reuses rasterized transforms.
        foreach (var id in rasters.Keys.Where(id => !design.Layers.Any(x => x.Id == id && x.Visible && x.Opacity > 0)).ToArray())
        { rasters[id].Dispose(); rasters.Remove(id); }
        foreach (var key in sources.Keys.Where(key => !design.Layers.Any(x => x.Kind == WidgetLayerKind.Photo && x.AssetKey == key && x.Visible && x.Opacity > 0)).ToArray())
        { sources[key].Dispose(); sources.Remove(key); }
        var intermediates = new List<CIImage>();
        try
        {
            using var color = design.TransparentBackground ? new CIColor(0, 0, 0, 0) : Background(design.BackgroundHex);
            using var infinite = new CIImage(color);
            var result = infinite.ImageByCroppingToRect(bounds); intermediates.Add(result);
            var timestamp = time ?? DateTimeOffset.Now;
            var steps = WidgetScenes.Steps(design);
            for (var index = 0; index < steps.Count; index++)
            {
                var step = steps[index];
                if (step.Clock is { } clock)
                {
                    var input = Rasterize(clock, size, timestamp);
                    result = CIBlendKernel.SourceOver.Apply(input, result) ?? throw new IOException("This clock could not be rendered.");
                    intermediates.Add(result);
                }
                else
                {
                    CIImage group;
                    if (index == 0) group = result;
                    else
                    {
                        using var clear = new CIColor(0, 0, 0, 0); using var infiniteClear = new CIImage(clear);
                        group = infiniteClear.ImageByCroppingToRect(bounds); intermediates.Add(group);
                    }
                    foreach (var layer in step.Photos)
                    {
                        var blended = Kernel(layer.BlendMode).Apply(Rasterize(layer, size, timestamp), group) ?? throw new IOException("This photo blend could not be rendered.");
                        intermediates.Add(blended); group = blended.ImageByCroppingToRect(bounds); intermediates.Add(group);
                    }
                    result = index == 0 ? group : CIBlendKernel.SourceOver.Apply(group, result) ?? throw new IOException("This photo group could not be rendered.");
                    if (index != 0) intermediates.Add(result);
                }
            }
            using var bitmap = context.CreateCGImage(result, bounds, CIFormat.Rgba8, colorSpace)
                ?? throw new IOException("The widget preview could not be rendered.");
            return new UIImage(bitmap);
        }
        finally { foreach (var image in intermediates) image.Dispose(); }
    }
    public WidgetSceneExport Scene(WidgetDesign design)
    {
        var steps = WidgetScenes.Steps(design); var manifest = WidgetScenes.Manifest(design);
        for (var edge = WidgetDesignRules.MaximumImageEdge; edge >= 128; edge = (int)(edge * .8))
        {
            var images = new Dictionary<int, byte[]>(); long pixels = 0, bytes = 0;
            for (var index = 0; index < steps.Count; index++)
            {
                if (steps[index].Clock is not null) continue;
                using var image = Render(design with { Layers = steps[index].Photos, TransparentBackground = index != 0 || design.TransparentBackground }, maximumEdge: edge);
                using var png = image.AsPNG() ?? throw new IOException("A photo group could not be prepared.");
                pixels += (long)image.CGImage!.Width * (long)image.CGImage.Height; bytes += (long)png.Length;
                images.Add(index, png.ToArray());
            }
            if (pixels <= WidgetScenes.MaximumPixels && bytes <= WidgetScenes.MaximumImageBytes) return new(manifest, images);
        }
        throw new IOException("This live widget contains too much photo detail. Try fewer photo groups.");
    }
    private static CIColor Background(string hex) => new(Convert.ToInt32(hex.Substring(1, 2), 16) / 255f,
        Convert.ToInt32(hex.Substring(3, 2), 16) / 255f, Convert.ToInt32(hex.Substring(5, 2), 16) / 255f, 1);
    private CIImage Rasterize(WidgetLayer layer, CGSize size, DateTimeOffset time)
    {
        var geometry = layer with { Name = "", BlendMode = WidgetBlendMode.Normal, Locked = false };
        var clockText = layer.Clock is { } clock ? WidgetClocks.Text(clock, time) : "";
        if (rasters.TryGetValue(layer.Id, out var cached) && cached.Layer == geometry && cached.Size == size && cached.ClockText == clockText) return cached.Input;
        // Evict before allocating a replacement; a gesture never accumulates old full-canvas buffers.
        if (cached is not null) { cached.Dispose(); rasters.Remove(layer.Id); }
        if (layer.Kind == WidgetLayerKind.Clock)
        {
            using var clockFormat = UIGraphicsImageRendererFormat.DefaultFormat; clockFormat.Scale = 1; clockFormat.Opaque = false;
            using var clockRenderer = new UIGraphicsImageRenderer(size, clockFormat);
            var clockRaster = clockRenderer.CreateImage(context =>
            {
                context.CGContext.ClipToRect(new CGRect(CGPoint.Empty, size));
                var frame = new CGRect(layer.Frame.X * size.Width, layer.Frame.Y * size.Height, layer.Frame.Width * size.Width, layer.Frame.Height * size.Height);
                AppleWidgetClockDrawing.Draw(layer, clockText, frame, context.CGContext);
            });
            try { var input = CIImage.FromCGImage(clockRaster.CGImage!); rasters.Add(layer.Id, new(geometry, size, clockRaster, input, clockText)); return input; }
            catch { clockRaster.Dispose(); throw; }
        }
        if (!sources.TryGetValue(layer.AssetKey, out var source))
        {
            var sourcePath = path(layer.AssetKey) ?? throw new IOException("A photo is missing. Remove its layer or choose it again.");
            source = UIImage.FromFile(sourcePath) ?? throw new IOException("A widget photo could not be opened.");
            sources.Add(layer.AssetKey, source);
        }
        if (source.CGImage?.Width != layer.PixelWidth || source.CGImage?.Height != layer.PixelHeight)
            throw new IOException("A widget photo is invalid. Remove its layer and choose it again.");
        var frame = new CGRect(layer.Frame.X * size.Width, layer.Frame.Y * size.Height, layer.Frame.Width * size.Width, layer.Frame.Height * size.Height);
        var placement = WidgetPhotoGeometry.Image(layer, frame.Width, frame.Height);
        using var format = UIGraphicsImageRendererFormat.DefaultFormat;
        format.Scale = 1; format.Opaque = false; format.PreferredRange = UIGraphicsImageRendererFormatRange.Standard;
        using var renderer = new UIGraphicsImageRenderer(size, format);
        var raster = renderer.CreateImage(context =>
        {
            var graphics = context.CGContext;
            graphics.ClipToRect(new CGRect(CGPoint.Empty, size)); graphics.ClipToRect(frame);
            graphics.TranslateCTM((nfloat)(frame.X + placement.X + placement.Width / 2), (nfloat)(frame.Y + placement.Y + placement.Height / 2));
            graphics.RotateCTM((nfloat)(layer.QuarterTurns * Math.PI / 2));
            var odd = layer.QuarterTurns % 2 != 0;
            graphics.ScaleCTM((odd ? layer.FlipVertical : layer.FlipHorizontal) ? -1 : 1, (odd ? layer.FlipHorizontal : layer.FlipVertical) ? -1 : 1);
            var width = odd ? placement.Height : placement.Width; var height = odd ? placement.Width : placement.Height;
            source.Draw(new CGRect(-width / 2, -height / 2, width, height), CGBlendMode.Normal, (nfloat)layer.Opacity);
        });
        try
        {
            var input = CIImage.FromCGImage(raster.CGImage!);
            rasters.Add(layer.Id, new(geometry, size, raster, input)); return input;
        }
        catch { raster.Dispose(); throw; }
    }
    internal static CIBlendKernel Kernel(WidgetBlendMode mode) => kernels.GetOrAdd(mode, CreateKernel);
    private static CIBlendKernel CreateKernel(WidgetBlendMode mode) => mode switch
    {
        WidgetBlendMode.Normal => CIBlendKernel.SourceOver,
        WidgetBlendMode.Darken => CIBlendKernel.Darken, WidgetBlendMode.Multiply => CIBlendKernel.Multiply,
        WidgetBlendMode.ColorBurn => CIBlendKernel.ColorBurn, WidgetBlendMode.DarkerColor => CIBlendKernel.DarkerColor,
        WidgetBlendMode.Lighten => CIBlendKernel.Lighten, WidgetBlendMode.Screen => CIBlendKernel.Screen,
        WidgetBlendMode.ColorDodge => CIBlendKernel.ColorDodge, WidgetBlendMode.LighterColor => CIBlendKernel.LighterColor,
        WidgetBlendMode.Overlay => CIBlendKernel.Overlay, WidgetBlendMode.SoftLight => CIBlendKernel.SoftLight,
        WidgetBlendMode.HardLight => CIBlendKernel.HardLight, WidgetBlendMode.Difference => CIBlendKernel.Difference,
        WidgetBlendMode.Exclusion => CIBlendKernel.Exclusion,
        _ => throw new IOException("This photo blend requires a newer app.")
    };
    public void Dispose()
    {
        foreach (var raster in rasters.Values) raster.Dispose(); rasters.Clear();
        foreach (var source in sources.Values) source.Dispose(); sources.Clear(); context.Dispose(); colorSpace.Dispose();
    }
}
