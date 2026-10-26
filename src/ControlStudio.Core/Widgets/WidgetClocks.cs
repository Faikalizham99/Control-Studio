using System.Globalization;
using System.Text.Json.Serialization;

namespace ControlStudio.Core.Widgets;

// Append new values so existing saved names and editor automation indices stay stable.
public enum WidgetClockFont { System, Rounded, Monospaced, Serif, RocknRollOne, DelaGothicOne, KaiseiDecol, SfProRounded }
public enum WidgetClockWeight { Light, Regular, Medium, Semibold, Bold, Heavy }
public enum WidgetClockAlignment { Left, Centre, Right }
public sealed record WidgetClockOptions(bool TwentyFourHour = true, bool ShowSeconds = true,
    WidgetClockFont Font = WidgetClockFont.Rounded, string ColourHex = "#FFFFFF", double FontScale = .72,
    WidgetClockAlignment Alignment = WidgetClockAlignment.Centre, bool ShowAmPm = true, WidgetClockWeight? Weight = null);

public static class WidgetClocks
{
    public static WidgetLayer New(string name = "Clock 1", string colourHex = "#FFFFFF") => new(Guid.NewGuid(), name, "", 1, 1,
        new(.05, .35, .9, .3), Kind: WidgetLayerKind.Clock, Clock: new(ColourHex: colourHex));
    public static void Validate(WidgetClockOptions clock)
    {
        if (!Enum.IsDefined(clock.Font) || !Enum.IsDefined(clock.Alignment) || !double.IsFinite(clock.FontScale)
            || clock.FontScale is < .2 or > 1 || !AppearanceValues.TryNormalizeHex(clock.ColourHex, out var colour) || colour != clock.ColourHex
            || clock.Weight is { } weight && !WidgetClockWeights.Available(clock.Font).Contains(weight))
            throw new IOException("Choose valid clock settings before saving.");
    }
    public static string Text(WidgetClockOptions clock, DateTimeOffset date, TimeZoneInfo? zone = null)
    {
        var local = TimeZoneInfo.ConvertTime(date, zone ?? TimeZoneInfo.Local);
        var format = clock.TwentyFourHour ? "HH:mm" : "h:mm";
        if (clock.ShowSeconds) format += ":ss";
        if (!clock.TwentyFourHour && clock.ShowAmPm) format += " tt";
        return local.ToString(format, CultureInfo.InvariantCulture);
    }
}

/// <summary>Photos and custom text are composited together; Clock/Date separate those static groups.</summary>
public sealed record WidgetSceneStep(IReadOnlyList<WidgetLayer> Photos, WidgetLayer? TextLayer);
public sealed record WidgetScenePart(int? ImageIndex = null, WidgetLayer? Clock = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WidgetLayer? Date = null);
public sealed record WidgetScene(int SchemaVersion, double Width, double Height, IReadOnlyList<WidgetScenePart> Parts);
public sealed record WidgetSceneExport(WidgetScene Scene, IReadOnlyDictionary<int, byte[]> Images);
public static class WidgetScenes
{
    public const int MaximumBytes = 32_768, MaximumPixels = 3_000_000, MaximumImageBytes = 4_000_000;
    public static bool IsLive(WidgetDesign design) => design.Layers.Any(x => WidgetTextLayers.IsLive(x) && x.Visible && x.Opacity > 0);
    public static IReadOnlyList<WidgetSceneStep> Steps(WidgetDesign design)
    {
        var result = new List<WidgetSceneStep>(); var photos = new List<WidgetLayer>();
        foreach (var layer in design.Layers)
        {
            if (!WidgetTextLayers.IsLive(layer)) { if (layer.Visible && layer.Opacity > 0) photos.Add(layer); }
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
        return new(design.Layers.Any(x => x.Kind == WidgetLayerKind.Date && x.Visible && x.Opacity > 0) ? 2 : 1,
            canvas.Width, canvas.Height, Steps(design).Select((step, index) => step.TextLayer is null
            ? new WidgetScenePart(index) : step.TextLayer.Kind == WidgetLayerKind.Date
                ? new WidgetScenePart(Date: step.TextLayer) : new WidgetScenePart(Clock: step.TextLayer)).ToArray());
    }
    public static void Validate(WidgetScene scene, WidgetDesign design)
    {
        var expected = Manifest(design);
        if (!IsLive(design) || scene.SchemaVersion != expected.SchemaVersion || scene.Width != expected.Width || scene.Height != expected.Height
            || scene.Parts is null || !scene.Parts.SequenceEqual(expected.Parts))
            throw new IOException("The live widget scene is invalid. Save the design again.");
    }
}
