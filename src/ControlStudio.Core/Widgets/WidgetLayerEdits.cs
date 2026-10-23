namespace ControlStudio.Core.Widgets;

/// <summary>Bounded normalized transforms shared by gestures and accessible controls.</summary>
public static class WidgetLayerEdits
{
    private static double Clamp(double value, double minimum, double maximum) => Math.Clamp(value, minimum, maximum);
    public static WidgetLayer Reset(WidgetLayer layer, WidgetSize size)
    {
        var canvas = WidgetDesignRules.Canvas(size);
        var width = .8; var height = width * canvas.Width * layer.PixelHeight / layer.PixelWidth / canvas.Height;
        if (height > .8) { height = .8; width = height * canvas.Height * layer.PixelWidth / layer.PixelHeight / canvas.Width; }
        width = Clamp(width, .05, .8); height = Clamp(height, .05, .8);
        return layer with { Frame = new((1 - width) / 2, (1 - height) / 2, width, height), Zoom = 1, CropX = .5, CropY = .5,
            QuarterTurns = 0, FlipHorizontal = false, FlipVertical = false, Opacity = 1 };
    }
    public static WidgetLayer Move(WidgetLayer layer, double dx, double dy) => !double.IsFinite(dx) || !double.IsFinite(dy) ? layer
        : layer with { Frame = layer.Frame with { X = Clamp(layer.Frame.X + dx, 0, 1 - layer.Frame.Width), Y = Clamp(layer.Frame.Y + dy, 0, 1 - layer.Frame.Height) } };
    public static WidgetLayer Scale(WidgetLayer layer, double factor, double anchorX = .5, double anchorY = .5)
    {
        if (!double.IsFinite(factor) || factor <= 0 || !double.IsFinite(anchorX) || !double.IsFinite(anchorY)) return layer;
        var f = layer.Frame;
        factor = Clamp(factor, Math.Max(.05 / f.Width, .05 / f.Height), Math.Min(1 / f.Width, 1 / f.Height));
        var width = Clamp(f.Width * factor, .05, 1); var height = Clamp(f.Height * factor, .05, 1);
        return layer with { Frame = new(Clamp(f.X + (f.Width - width) * Clamp(anchorX, 0, 1), 0, 1 - width),
            Clamp(f.Y + (f.Height - height) * Clamp(anchorY, 0, 1), 0, 1 - height), width, height) };
    }
    public static WidgetLayer Resize(WidgetLayer layer, int corner, double dx, double dy)
    {
        if (corner is < 0 or > 3 || !double.IsFinite(dx) || !double.IsFinite(dy)) return layer;
        var f = layer.Frame; var left = f.X; var top = f.Y; var right = f.X + f.Width; var bottom = f.Y + f.Height;
        if (corner % 2 == 0) left = Clamp(left + dx, 0, right - .05); else right = Clamp(right + dx, left + .05, 1);
        if (corner < 2) top = Clamp(top + dy, 0, bottom - .05); else bottom = Clamp(bottom + dy, top + .05, 1);
        // Subtraction near the minimum can produce .04999999999999999.
        // Normalize before strict persistence validation, including opposite-edge bounds.
        var width = Clamp(right - left, .05, 1); var height = Clamp(bottom - top, .05, 1);
        return layer with { Frame = new(Clamp(left, 0, 1 - width), Clamp(top, 0, 1 - height), width, height) };
    }
    public static WidgetLayer Rotate(WidgetLayer layer, int turns) => layer with { QuarterTurns = ((layer.QuarterTurns + turns % 4) % 4 + 4) % 4 };
    public static IReadOnlyList<WidgetLayer> Reorder(IReadOnlyList<WidgetLayer> layers, Guid source, Guid target)
    {
        var result = layers.ToList(); var from = result.FindIndex(x => x.Id == source); var to = result.FindIndex(x => x.Id == target);
        if (from < 0 || to < 0 || from == to) return layers;
        var moved = result[from]; result.RemoveAt(from); result.Insert(to, moved); return result;
    }
    public static bool Contains(WidgetLayer layer, double x, double y) => layer.Visible && layer.Opacity > 0
        && x >= layer.Frame.X && y >= layer.Frame.Y && x <= layer.Frame.X + layer.Frame.Width && y <= layer.Frame.Y + layer.Frame.Height;
}
