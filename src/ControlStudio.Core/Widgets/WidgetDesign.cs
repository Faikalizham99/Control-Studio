using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlStudio.Core.Widgets;

public enum WidgetSize { Small, Medium, Large, ExtraLarge, ExtraLargePortrait }
public enum WidgetLayerKind { Photo }
public sealed record WidgetFrame(double X = 0, double Y = 0, double Width = 1, double Height = 1);
public sealed record WidgetLayer(Guid Id, string Name, string AssetKey, int PixelWidth, int PixelHeight,
    WidgetFrame Frame, double Zoom = 1, double CropX = .5, double CropY = .5, bool Visible = true,
    WidgetLayerKind Kind = WidgetLayerKind.Photo);
public sealed record WidgetDesign(Guid Id, string Name, WidgetSize Size, string BackgroundHex,
    IReadOnlyList<WidgetLayer> Layers, string RenderKey, int Revision = 0);
public sealed record WidgetCatalog(int SchemaVersion, IReadOnlyList<WidgetDesign> Designs);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, UseStringEnumConverter = true, MaxDepth = 16)]
[JsonSerializable(typeof(WidgetCatalog))]
[JsonSerializable(typeof(WidgetDesign))]
[JsonSerializable(typeof(WidgetSlotCatalog))]
[JsonSerializable(typeof(WidgetLibraryCatalog))]
public partial class WidgetJsonContext : JsonSerializerContext;

public static class WidgetDesignRules
{
    public const int MaximumDesigns = 50, MaximumLayers = 8, MaximumImageEdge = 1024;
    public const int MaximumCatalogBytes = 1_000_000, MaximumImageBytes = 4_000_000;
    public static WidgetJsonContext JsonContext { get; } = new(CreateJson());
    public static JsonSerializerOptions Json => JsonContext.Options;
    private static JsonSerializerOptions CreateJson()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };
        json.Converters.Add(new JsonStringEnumConverter<WidgetSize>(JsonNamingPolicy.CamelCase, false));
        json.Converters.Add(new JsonStringEnumConverter<WidgetLayerKind>(JsonNamingPolicy.CamelCase, false));
        return json;
    }
    public static WidgetDesign New() => new(Guid.NewGuid(), "My widget", WidgetSize.Small, "#101116", [], Guid.NewGuid().ToString("N"));
    public static bool ValidKey(string? key) => key is { Length: 32 } && Guid.TryParseExact(key, "N", out _) && key == key.ToLowerInvariant();
    public static string SizeName(WidgetSize size) => size switch
    {
        WidgetSize.ExtraLarge => "Extra Large Landscape · iPad",
        WidgetSize.ExtraLargePortrait => "Extra Large Portrait · iOS/iPadOS 27+",
        _ => size.ToString()
    };
    // Representative canvas proportions. WidgetKit controls exact device dimensions.
    public static (double Width, double Height) Canvas(WidgetSize size) => size switch
    {
        WidgetSize.Small => (170, 170), WidgetSize.Medium => (364, 170),
        WidgetSize.Large => (364, 382), WidgetSize.ExtraLarge => (746, 382),
        WidgetSize.ExtraLargePortrait => (364, 594),
        _ => throw new IOException("This widget size requires a newer app.")
    };
    public static void Validate(WidgetDesign design)
    {
        if (design.Id == Guid.Empty || !Enum.IsDefined(design.Size) || design.Revision < 0 || !ValidKey(design.RenderKey))
            throw new IOException("This widget design is invalid. Its data was not changed.");
        if (string.IsNullOrWhiteSpace(design.Name) || design.Name.Length > 60 || design.Name.Any(char.IsControl))
            throw new IOException("Give your widget a name of up to 60 characters.");
        if (!AppearanceValues.TryNormalizeHex(design.BackgroundHex, out var hex) || hex != design.BackgroundHex)
            throw new IOException("Choose a valid background colour.");
        if (design.Layers is null || design.Layers.Count > MaximumLayers || design.Layers.Select(x => x?.Id).Distinct().Count() != design.Layers.Count)
            throw new IOException($"A widget can contain up to {MaximumLayers} photo layers.");
        foreach (var layer in design.Layers)
        {
            if (layer is null || layer.Id == Guid.Empty || layer.Kind != WidgetLayerKind.Photo || !ValidKey(layer.AssetKey)
                || string.IsNullOrWhiteSpace(layer.Name) || layer.Name.Length > 40 || layer.Name.Any(char.IsControl)
                || layer.PixelWidth is < 1 or > MaximumImageEdge || layer.PixelHeight is < 1 or > MaximumImageEdge
                || layer.Frame is not { } f || !Range(f.X, 0, 1) || !Range(f.Y, 0, 1)
                || !Range(f.Width, .05, 1) || !Range(f.Height, .05, 1)
                || f.X + f.Width > 1.000001 || f.Y + f.Height > 1.000001
                || !Range(layer.Zoom, 1, 4) || !Range(layer.CropX, 0, 1) || !Range(layer.CropY, 0, 1))
                throw new IOException("A photo layer is invalid. Its data was not changed.");
        }
    }
    public static void Validate(WidgetCatalog catalog)
    {
        if (catalog.SchemaVersion != 1) throw new IOException("These widget designs require a newer app. Data was not changed.");
        if (catalog.Designs is null || catalog.Designs.Count > MaximumDesigns || catalog.Designs.Select(x => x?.Id).Distinct().Count() != catalog.Designs.Count)
            throw new IOException("The saved widget list is invalid. Data was not changed.");
        foreach (var design in catalog.Designs) { if (design is null) throw new IOException("The widget list is invalid."); Validate(design); }
    }
    private static bool Range(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
}

public readonly record struct WidgetImageRect(double X, double Y, double Width, double Height);
public static class WidgetPhotoGeometry
{
    public static WidgetImageRect Image(WidgetLayer layer, double frameWidth, double frameHeight)
    {
        if (frameWidth <= 0 || frameHeight <= 0 || !double.IsFinite(frameWidth) || !double.IsFinite(frameHeight)) return default;
        var scale = Math.Max(frameWidth / layer.PixelWidth, frameHeight / layer.PixelHeight) * layer.Zoom;
        var width = layer.PixelWidth * scale; var height = layer.PixelHeight * scale;
        return new(-(width - frameWidth) * layer.CropX, -(height - frameHeight) * layer.CropY, width, height);
    }
}
