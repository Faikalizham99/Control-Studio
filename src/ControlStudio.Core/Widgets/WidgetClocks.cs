using System.Globalization;

namespace ControlStudio.Core.Widgets;

public enum WidgetClockFont { System, Rounded, Monospaced, Serif }
public enum WidgetClockAlignment { Left, Centre, Right }
public sealed record WidgetClockOptions(bool TwentyFourHour = true, bool ShowSeconds = true,
    WidgetClockFont Font = WidgetClockFont.Rounded, string ColourHex = "#FFFFFF", double FontScale = .72,
    WidgetClockAlignment Alignment = WidgetClockAlignment.Centre);

public static class WidgetClocks
{
    public static WidgetLayer New(string name = "Clock 1", string colourHex = "#FFFFFF") => new(Guid.NewGuid(), name, "", 1, 1,
        new(.05, .35, .9, .3), Kind: WidgetLayerKind.Clock, Clock: new(ColourHex: colourHex));
    public static void Validate(WidgetClockOptions clock)
    {
        if (!Enum.IsDefined(clock.Font) || !Enum.IsDefined(clock.Alignment) || !double.IsFinite(clock.FontScale)
            || clock.FontScale is < .2 or > 1 || !AppearanceValues.TryNormalizeHex(clock.ColourHex, out var colour) || colour != clock.ColourHex)
            throw new IOException("Choose valid clock settings before saving.");
    }
    public static string Text(WidgetClockOptions clock, DateTimeOffset date, TimeZoneInfo? zone = null)
    {
        var local = TimeZoneInfo.ConvertTime(date, zone ?? TimeZoneInfo.Local);
        var format = clock.TwentyFourHour ? clock.ShowSeconds ? "HH:mm:ss" : "HH:mm" : clock.ShowSeconds ? "h:mm:ss tt" : "h:mm tt";
        return local.ToString(format, CultureInfo.InvariantCulture);
    }
}

/// <summary>Consecutive photos are blended together; live clocks separate those static groups.</summary>
public sealed record WidgetSceneStep(IReadOnlyList<WidgetLayer> Photos, WidgetLayer? Clock);
public sealed record WidgetScenePart(int? ImageIndex = null, WidgetLayer? Clock = null);
public sealed record WidgetScene(int SchemaVersion, double Width, double Height, IReadOnlyList<WidgetScenePart> Parts);
public sealed record WidgetSceneExport(WidgetScene Scene, IReadOnlyDictionary<int, byte[]> Images);
public static class WidgetScenes
{
    public const int MaximumBytes = 32_768, MaximumPixels = 3_000_000, MaximumImageBytes = 4_000_000;
    public static bool IsLive(WidgetDesign design) => design.Layers.Any(x => x.Kind == WidgetLayerKind.Clock && x.Visible && x.Opacity > 0);
    public static IReadOnlyList<WidgetSceneStep> Steps(WidgetDesign design)
    {
        var result = new List<WidgetSceneStep>(); var photos = new List<WidgetLayer>();
        foreach (var layer in design.Layers)
        {
            if (layer.Kind == WidgetLayerKind.Photo) { if (layer.Visible && layer.Opacity > 0) photos.Add(layer); }
            else
            {
                if (photos.Count > 0) { result.Add(new(photos.ToArray(), null)); photos.Clear(); }
                if (layer.Visible && layer.Opacity > 0) result.Add(new([], layer));
            }
        }
        if (photos.Count > 0) result.Add(new(photos.ToArray(), null));
        return result;
    }
    public static WidgetScene Manifest(WidgetDesign design)
    {
        var canvas = WidgetDesignRules.Canvas(design);
        return new(1, canvas.Width, canvas.Height, Steps(design).Select((step, index) => step.Clock is null
            ? new WidgetScenePart(index) : new WidgetScenePart(Clock: step.Clock)).ToArray());
    }
    public static void Validate(WidgetScene scene, WidgetDesign design)
    {
        var expected = Manifest(design);
        if (!IsLive(design) || scene.SchemaVersion != 1 || scene.Width != expected.Width || scene.Height != expected.Height
            || scene.Parts is null || !scene.Parts.SequenceEqual(expected.Parts))
            throw new IOException("The live widget scene is invalid. Save the design again.");
    }
}
