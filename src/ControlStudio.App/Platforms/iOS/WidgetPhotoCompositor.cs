using ControlStudio.Core.Widgets;
using System.Collections.Concurrent;
using System.Text.Json;
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
    private const int MaximumCachedRasters = 4, MaximumCachedSources = 2, MaximumGraphLayers = 4;
    private readonly LinkedList<Guid> rasterOrder = new();
    private readonly LinkedList<string> sourceOrder = new();
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
        { RemoveRaster(id); }
        foreach (var key in sources.Keys.Where(key => !design.Layers.Any(x => x.Kind == WidgetLayerKind.Photo && x.AssetKey == key && x.Visible && x.Opacity > 0)).ToArray())
        { RemoveSource(key); }
        var result = Solid(bounds, design.TransparentBackground, design.BackgroundHex);
        try
        {
            var timestamp = time ?? DateTimeOffset.Now;
            var steps = WidgetScenes.Steps(design);
            var pendingText = 0;
            for (var index = 0; index < steps.Count; index++)
            {
                var step = steps[index];
                if (step.TextLayer is { } clock)
                {
                    var input = Rasterize(clock, size, timestamp);
                    using var blended = CIBlendKernel.SourceOver.Apply(input, result) ?? throw new IOException("This text layer could not be rendered.");
                    var cropped = blended.ImageByCroppingToRect(bounds); result.Dispose(); result = cropped;
                    if (++pendingText >= MaximumGraphLayers)
                    { var flat = Flatten(result, bounds); result.Dispose(); result = flat; pendingText = 0; }
                }
                else
                {
                    if (pendingText > 0)
                    { var flat = Flatten(result, bounds); result.Dispose(); result = flat; pendingText = 0; }
                    using var group = RenderGroup(step.Photos, size, timestamp, index != 0 || design.TransparentBackground, design.BackgroundHex);
                    // The first group already includes the background. Replacing
                    // it avoids applying a translucent background twice.
                    CIImage flattened;
                    if (index == 0) flattened = group.ImageByCroppingToRect(bounds);
                    else
                    {
                        using var blended = CIBlendKernel.SourceOver.Apply(group, result) ?? throw new IOException("This photo group could not be rendered.");
                        flattened = Flatten(blended, bounds);
                    }
                    result.Dispose(); result = flattened;
                }
            }
            using var bitmap = context.CreateCGImage(result, bounds, CIFormat.Rgba8, colorSpace, false)
                ?? throw new IOException("The widget preview could not be rendered.");
            return new UIImage(bitmap);
        }
        finally { result.Dispose(); }
    }
    private static CIImage Solid(CGRect bounds, bool transparent, string hex)
    {
        using var colour = transparent ? new CIColor(0, 0, 0, 0) : Background(hex);
        using var infinite = new CIImage(colour); return infinite.ImageByCroppingToRect(bounds);
    }
    private CIImage Flatten(CIImage input, CGRect bounds)
    {
        using var bitmap = context.CreateCGImage(input, bounds, CIFormat.Rgba8, colorSpace, false)
            ?? throw new IOException("The widget composition could not be prepared. Try hiding a few photo layers.");
        return CIImage.FromCGImage(bitmap);
    }
    private CIImage RenderGroup(IReadOnlyList<WidgetLayer> layers, CGSize size, DateTimeOffset time, bool transparent, string hex)
    {
        var bounds = new CGRect(CGPoint.Empty, size); var group = Solid(bounds, transparent, hex);
        try
        {
            for (var index = 0; index < layers.Count; index++)
            {
                using var pool = new NSAutoreleasePool(); var layer = layers[index];
                using var blended = Kernel(layer.BlendMode).Apply(Rasterize(layer, size, time), group)
                    ?? throw new IOException("This photo blend could not be rendered.");
                var cropped = blended.ImageByCroppingToRect(bounds); group.Dispose(); group = cropped;
                if ((index + 1) % MaximumGraphLayers == 0)
                { var flat = Flatten(group, bounds); group.Dispose(); group = flat; }
            }
            // The next group starts with a materialized image instead of
            // retaining every earlier source/raster in a lazy Core Image graph.
            return layers.Count % MaximumGraphLayers == 0 ? group.ImageByCroppingToRect(bounds) : Flatten(group, bounds);
        }
        finally { group.Dispose(); }
    }
    public WidgetSceneExport Scene(WidgetDesign design)
    {
        using var pool = new NSAutoreleasePool();
        var steps = WidgetScenes.Steps(design); var manifest = WidgetScenes.Manifest(design);
        if (JsonSerializer.SerializeToUtf8Bytes(manifest, WidgetDesignRules.JsonContext.WidgetScene).Length > WidgetScenes.MaximumBytes)
            throw new IOException("This widget's live settings are too large. Shorten layer names or use fewer Clock/Date layers.");
        var expectedGroups = steps.Count(step => step.TextLayer is null);
        for (var edge = WidgetDesignRules.MaximumImageEdge; edge >= 128; edge = (int)(edge * .8))
        {
            var images = new Dictionary<int, byte[]>(); long pixels = 0, bytes = 0;
            for (var index = 0; index < steps.Count; index++)
            {
                if (steps[index].TextLayer is not null) continue;
                using var groupPool = new NSAutoreleasePool();
                using var image = Render(design with { Layers = steps[index].Photos, TransparentBackground = index != 0 || design.TransparentBackground }, maximumEdge: edge);
                using var png = image.AsPNG() ?? throw new IOException("A photo group could not be prepared.");
                pixels += (long)image.CGImage!.Width * (long)image.CGImage.Height; bytes += (long)png.Length;
                if (pixels > WidgetScenes.MaximumPixels || bytes > WidgetScenes.MaximumImageBytes) break;
                images.Add(index, png.ToArray());
            }
            if (images.Count == expectedGroups && pixels <= WidgetScenes.MaximumPixels && bytes <= WidgetScenes.MaximumImageBytes) return new(manifest, images);
        }
        throw new IOException("This live widget contains too much photo detail. Try fewer photo groups.");
    }
    private static CIColor Background(string hex) => new(Convert.ToInt32(hex.Substring(1, 2), 16) / 255f,
        Convert.ToInt32(hex.Substring(3, 2), 16) / 255f, Convert.ToInt32(hex.Substring(5, 2), 16) / 255f, 1);
    private CIImage Rasterize(WidgetLayer layer, CGSize size, DateTimeOffset time)
    {
        var geometry = layer with { Name = "", BlendMode = WidgetBlendMode.Normal, Locked = false };
        var clockText = WidgetTextLayers.IsText(layer) ? WidgetTextLayers.Text(layer, time) : "";
        using var pool = new NSAutoreleasePool();
        if (rasters.TryGetValue(layer.Id, out var cached) && cached.Layer == geometry && cached.Size == size && cached.ClockText == clockText)
        { Touch(rasterOrder, layer.Id); return cached.Input; }
        // Evict before allocating a replacement; a gesture never accumulates old full-canvas buffers.
        if (cached is not null) RemoveRaster(layer.Id);
        while (rasters.Count >= MaximumCachedRasters) RemoveRaster(rasterOrder.First!.Value);
        if (WidgetTextLayers.IsText(layer))
        {
            using var clockFormat = UIGraphicsImageRendererFormat.DefaultFormat; clockFormat.Scale = 1; clockFormat.Opaque = false;
            using var clockRenderer = new UIGraphicsImageRenderer(size, clockFormat);
            var clockRaster = clockRenderer.CreateImage(context =>
            {
                context.CGContext.ClipToRect(new CGRect(CGPoint.Empty, size));
                var frame = new CGRect(layer.Frame.X * size.Width, layer.Frame.Y * size.Height, layer.Frame.Width * size.Width, layer.Frame.Height * size.Height);
                RotateFrame(context.CGContext, frame, layer.RotationDegrees);
                AppleWidgetClockDrawing.Draw(layer, clockText, frame, context.CGContext);
            });
            try { var input = CIImage.FromCGImage(clockRaster.CGImage!); rasters.Add(layer.Id, new(geometry, size, clockRaster, input, clockText)); Touch(rasterOrder, layer.Id); return input; }
            catch { clockRaster.Dispose(); throw; }
        }
        if (!sources.TryGetValue(layer.AssetKey, out var source))
        {
            while (sources.Count >= MaximumCachedSources) RemoveSource(sourceOrder.First!.Value);
            var sourcePath = path(layer.AssetKey) ?? throw new IOException("A photo is missing. Remove its layer or choose it again.");
            source = UIImage.FromFile(sourcePath) ?? throw new IOException("A widget photo could not be opened.");
            sources.Add(layer.AssetKey, source);
        }
        Touch(sourceOrder, layer.AssetKey);
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
            graphics.ClipToRect(new CGRect(CGPoint.Empty, size)); RotateFrame(graphics, frame, layer.RotationDegrees); graphics.ClipToRect(frame);
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
            rasters.Add(layer.Id, new(geometry, size, raster, input)); Touch(rasterOrder, layer.Id); return input;
        }
        catch { raster.Dispose(); throw; }
    }
    private static void Touch<T>(LinkedList<T> order, T key)
    { var node = order.Find(key); if (node is not null) order.Remove(node); order.AddLast(key); }
    private void RemoveRaster(Guid key)
    { rasters[key].Dispose(); rasters.Remove(key); rasterOrder.Remove(key); }
    private void RemoveSource(string key)
    { sources[key].Dispose(); sources.Remove(key); sourceOrder.Remove(key); }
    private static void RotateFrame(CGContext graphics, CGRect frame, double degrees)
    {
        graphics.TranslateCTM(frame.GetMidX(), frame.GetMidY()); graphics.RotateCTM((nfloat)(degrees * Math.PI / 180));
        graphics.TranslateCTM(-frame.GetMidX(), -frame.GetMidY());
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
        foreach (var raster in rasters.Values) raster.Dispose(); rasters.Clear(); rasterOrder.Clear();
        foreach (var source in sources.Values) source.Dispose(); sources.Clear(); sourceOrder.Clear(); context.Dispose(); colorSpace.Dispose();
    }
}
