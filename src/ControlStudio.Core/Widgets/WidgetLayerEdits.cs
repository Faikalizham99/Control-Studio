namespace ControlStudio.Core.Widgets;

/// <summary>Finite workspace transforms shared by gestures and accessible controls.</summary>
public static class WidgetLayerEdits
{
    private static double Clamp(double value, double minimum, double maximum) => Math.Clamp(value, minimum, maximum);
    public static WidgetLayer Reset(WidgetLayer layer, WidgetSize size, WidgetCanvasSize? dimensions = null)
    {
        if (layer.Locked) return layer;
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
        if (layer.Locked) return layer;
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
