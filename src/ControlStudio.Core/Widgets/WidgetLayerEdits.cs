using System.Globalization;

namespace ControlStudio.Core.Widgets;

/// <summary>Finite workspace transforms shared by gestures and accessible controls.</summary>
public static class WidgetLayerEdits
{
    public static (WidgetDesign Design, WidgetLayer Copy) Duplicate(WidgetDesign design, Guid source)
    {
        WidgetDesignRules.Validate(design);
        if (design.Layers.Count >= WidgetDesignRules.MaximumLayers)
            throw new IOException($"A widget can contain up to {WidgetDesignRules.MaximumLayers} layers. Remove a layer before duplicating.");
        var layers = design.Layers.ToList(); var index = layers.FindIndex(layer => layer.Id == source);
        if (index < 0) throw new IOException("This layer is no longer available.");
        var original = layers[index]; var names = layers.Select(layer => layer.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string name; var ordinal = 1;
        do
        {
            var suffix = ordinal == 1 ? " copy" : " copy " + ordinal.ToString(CultureInfo.InvariantCulture);
            var budget = 40 - suffix.Length;
            var length = original.Name.Length <= budget ? original.Name.Length
                : StringInfo.ParseCombiningCharacters(original.Name).LastOrDefault(start => start <= budget);
            var stem = original.Name[..length].TrimEnd();
            name = (stem.Length == 0 ? "Layer" : stem) + suffix; ordinal++;
        } while (names.Contains(name));
        var copy = original with { Id = Guid.NewGuid(), Name = name };
        // Layer order runs from back to front. A copy sits directly above its source.
        layers.Insert(index + 1, copy);
        var updated = design with { Layers = layers.ToArray() }; WidgetDesignRules.Validate(updated);
        return (updated, copy);
    }
    private static double Clamp(double value, double minimum, double maximum) => Math.Clamp(value, minimum, maximum);
    public static WidgetLayer Reset(WidgetLayer layer, WidgetSize size, WidgetCanvasSize? dimensions = null)
    {
        if (layer.Locked) return layer;
        if (WidgetTextLayers.IsText(layer)) return layer with { Frame = new(.05, .35, .9, .3), Opacity = 1 };
        var canvas = dimensions is null ? WidgetDesignRules.Canvas(size) : (Width: dimensions.Width, Height: dimensions.Height);
        var width = .8; var height = width * canvas.Width * layer.PixelHeight / layer.PixelWidth / canvas.Height;
        if (height > .8) { height = .8; width = height * canvas.Height * layer.PixelWidth / layer.PixelHeight / canvas.Width; }
        width = Clamp(width, .05, .8); height = Clamp(height, .05, .8);
        return layer with { Frame = new((1 - width) / 2, (1 - height) / 2, width, height), Zoom = 1, CropX = .5, CropY = .5,
            QuarterTurns = 0, FlipHorizontal = false, FlipVertical = false, Opacity = 1, FitPhoto = true, BlendMode = WidgetBlendMode.Normal };
    }
    public static WidgetLayer Move(WidgetLayer layer, double dx, double dy) => layer.Locked || !double.IsFinite(dx) || !double.IsFinite(dy) ? layer
        : layer with { Frame = layer.Frame with { X = Position(layer.Frame.X + dx), Y = Position(layer.Frame.Y + dy) } };
    private static double Position(double value) => Clamp(value, -WidgetDesignRules.MaximumLayerOffset, WidgetDesignRules.MaximumLayerOffset);
    public static WidgetLayer Center(WidgetLayer layer) => layer.Locked ? layer : layer with { Frame = layer.Frame with { X = (1 - layer.Frame.Width) / 2, Y = (1 - layer.Frame.Height) / 2 } };
    public static WidgetLayer CenterHorizontally(WidgetLayer layer) => layer.Locked ? layer
        : layer with { Frame = layer.Frame with { X = (1 - layer.Frame.Width) / 2 } };
    public static WidgetLayer ScaleCentered(WidgetLayer layer, double factor)
    {
        if (layer.Locked || !double.IsFinite(factor) || factor <= 0) return layer;
        var f = layer.Frame; var cx = f.X + f.Width / 2; var cy = f.Y + f.Height / 2;
        var offset = WidgetDesignRules.MaximumLayerOffset;
        // Bound the scale before moving the frame: clamping X/Y afterwards
        // would shift its centre near the outside workspace limits.
        var minimum = Math.Max(Math.Max(WidgetDesignRules.MinimumLayerSize / f.Width, WidgetDesignRules.MinimumLayerSize / f.Height),
            Math.Max(2 * (cx - offset) / f.Width, 2 * (cy - offset) / f.Height));
        var maximum = Math.Min(Math.Min(WidgetDesignRules.MaximumLayerSize / f.Width, WidgetDesignRules.MaximumLayerSize / f.Height),
            Math.Min(2 * (cx + offset) / f.Width, 2 * (cy + offset) / f.Height));
        // Identity is always feasible for a valid layer, including at an exact
        // limit where floating-point subtraction can straddle the factor 1.
        return Scale(layer, Clamp(factor, Math.Min(1, minimum), Math.Max(1, maximum)));
    }
    public static WidgetLayer Scale(WidgetLayer layer, double factor, double anchorX = .5, double anchorY = .5)
    {
        if (layer.Locked || !double.IsFinite(factor) || factor <= 0 || !double.IsFinite(anchorX) || !double.IsFinite(anchorY)) return layer;
        var f = layer.Frame;
        factor = Clamp(factor, Math.Max(WidgetDesignRules.MinimumLayerSize / f.Width, WidgetDesignRules.MinimumLayerSize / f.Height),
            Math.Min(WidgetDesignRules.MaximumLayerSize / f.Width, WidgetDesignRules.MaximumLayerSize / f.Height));
        var width = Clamp(f.Width * factor, WidgetDesignRules.MinimumLayerSize, WidgetDesignRules.MaximumLayerSize);
        var height = Clamp(f.Height * factor, WidgetDesignRules.MinimumLayerSize, WidgetDesignRules.MaximumLayerSize);
        return layer with { Frame = new(Position(f.X + (f.Width - width) * Clamp(anchorX, 0, 1)),
            Position(f.Y + (f.Height - height) * Clamp(anchorY, 0, 1)), width, height) };
    }
    public static WidgetLayer Resize(WidgetLayer layer, int corner, double dx, double dy)
    {
        if (layer.Locked || corner is < 0 or > 3 || !double.IsFinite(dx) || !double.IsFinite(dy)) return layer;
        var f = layer.Frame;
        var horizontal = (corner % 2 == 0 ? -dx : dx) / f.Width;
        var vertical = (corner < 2 ? -dy : dy) / f.Height;
        var factor = Math.Max(double.Epsilon, 1 + (Math.Abs(horizontal) >= Math.Abs(vertical) ? horizontal : vertical));
        return Scale(layer, factor, corner % 2 == 0 ? 1 : 0, corner < 2 ? 1 : 0);
    }
    public static WidgetLayer Rotate(WidgetLayer layer, int turns, WidgetSize? size = null, WidgetCanvasSize? dimensions = null)
    {
        if (layer.Locked || WidgetTextLayers.IsText(layer)) return layer;
        var rotated = layer with { QuarterTurns = ((layer.QuarterTurns + turns % 4) % 4 + 4) % 4 };
        if (!layer.FitPhoto || size is null || turns % 2 == 0) return rotated;
        var canvas = dimensions is null ? WidgetDesignRules.Canvas(size.Value) : (Width: dimensions.Width, Height: dimensions.Height); var f = layer.Frame;
        var width = Clamp(f.Height * canvas.Height / canvas.Width, WidgetDesignRules.MinimumLayerSize, WidgetDesignRules.MaximumLayerSize);
        var height = Clamp(f.Width * canvas.Width / canvas.Height, WidgetDesignRules.MinimumLayerSize, WidgetDesignRules.MaximumLayerSize);
        return rotated with { Frame = new(Position(f.X + (f.Width - width) / 2), Position(f.Y + (f.Height - height) / 2), width, height) };
    }
    public static IReadOnlyList<WidgetLayer> Reorder(IReadOnlyList<WidgetLayer> layers, Guid source, Guid target)
    {
        var result = layers.ToList(); var from = result.FindIndex(x => x.Id == source); var to = result.FindIndex(x => x.Id == target);
        if (from < 0 || to < 0 || from == to) return layers;
        var moved = result[from]; result.RemoveAt(from); result.Insert(to, moved); return result;
    }
    public static bool Contains(WidgetLayer layer, double x, double y) => layer.Visible && layer.Opacity > 0
        && x >= layer.Frame.X && y >= layer.Frame.Y && x <= layer.Frame.X + layer.Frame.Width && y <= layer.Frame.Y + layer.Frame.Height;
}
