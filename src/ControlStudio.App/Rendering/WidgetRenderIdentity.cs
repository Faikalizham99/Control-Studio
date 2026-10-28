using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Rendering;

/// <summary>Pixel inputs only. This retains no native objects or image bytes.</summary>
internal sealed record WidgetRenderIdentity(double Width, double Height, string Background, bool Transparent,
    WidgetLayer[] Layers, string[] LiveText, WidgetBatteryReading Battery)
{
    public static WidgetRenderIdentity Create(WidgetDesign design, DateTimeOffset time, WidgetBatteryReading battery = default)
    {
        // Validate even hidden layers and metadata before considering a no-op.
        WidgetDesignRules.Validate(design);
        var canvas = WidgetDesignRules.Canvas(design);
        var layers = design.Layers.Where(x => x.Visible && x.Opacity > 0)
            .Select(x => x with { Name = "", Locked = false }).ToArray();
        return new(canvas.Width, canvas.Height, design.BackgroundHex, design.TransparentBackground, layers,
            layers.Where(WidgetTextLayers.IsLive).Select(x => WidgetTextLayers.Text(x, time)).ToArray(),
            layers.Any(x => x.Kind == WidgetLayerKind.Battery) ? battery : default);
    }
    public bool SamePixels(WidgetRenderIdentity? other) => other is not null && Width == other.Width && Height == other.Height
        && Background == other.Background && Transparent == other.Transparent && Battery == other.Battery
        && Layers.SequenceEqual(other.Layers) && LiveText.SequenceEqual(other.LiveText);
}
