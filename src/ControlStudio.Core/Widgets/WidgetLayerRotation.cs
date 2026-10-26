namespace ControlStudio.Core.Widgets;

/// <summary>Layer rotation in point space, so non-square widget families use the same geometry.</summary>
public static class WidgetLayerRotation
{
    public static double Normalize(double degrees)
    {
        if (!double.IsFinite(degrees)) return 0;
        var value = degrees % 360;
        return value > 180 ? value - 360 : value < -180 ? value + 360 : value;
    }
    public static WidgetLayer Set(WidgetLayer layer, double degrees) => layer.Locked || !double.IsFinite(degrees) ? layer
        : layer with { RotationDegrees = Normalize(degrees) };
    public static (double X, double Y) Vector(double x, double y, double degrees)
    {
        var radians = degrees * Math.PI / 180; var sin = Math.Sin(radians); var cos = Math.Cos(radians);
        return (x * cos - y * sin, x * sin + y * cos);
    }
    public static (double X, double Y) Point(double x, double y, double cx, double cy, double degrees)
    {
        var point = Vector(x - cx, y - cy, degrees); return (point.X + cx, point.Y + cy);
    }
    public static (double X, double Y) LocalPoint(WidgetLayer layer, double x, double y, double width, double height)
        => Point(x, y, (layer.Frame.X + layer.Frame.Width / 2) * width, (layer.Frame.Y + layer.Frame.Height / 2) * height, -layer.RotationDegrees);
    public static WidgetLayer Resize(WidgetLayer layer, int corner, double dx, double dy, double width, double height)
    {
        if (layer.Locked || !Valid(dx, dy, width, height)) return layer;
        var local = Vector(dx, dy, -layer.RotationDegrees);
        return Reorient(layer, WidgetLayerEdits.Resize(layer, corner, local.X / width, local.Y / height), width, height);
    }
    public static WidgetLayer Scale(WidgetLayer layer, double factor, double anchorX, double anchorY, double width, double height)
    {
        if (layer.Locked || !Valid(anchorX, anchorY, width, height)) return layer;
        return Reorient(layer, WidgetLayerEdits.Scale(layer, factor, anchorX, anchorY), width, height);
    }
    private static bool Valid(double x, double y, double width, double height) => double.IsFinite(x) && double.IsFinite(y)
        && double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > 0;
    private static WidgetLayer Reorient(WidgetLayer original, WidgetLayer changed, double width, double height)
    {
        if (original == changed || original.RotationDegrees == 0) return changed;
        var f = original.Frame; var next = changed.Frame;
        var localShift = ((next.X + next.Width / 2 - f.X - f.Width / 2) * width,
            (next.Y + next.Height / 2 - f.Y - f.Height / 2) * height);
        var shift = Vector(localShift.Item1, localShift.Item2, original.RotationDegrees);
        var centred = changed with { Frame = next with { X = f.X + (f.Width - next.Width) / 2, Y = f.Y + (f.Height - next.Height) / 2 } };
        return WidgetLayerEdits.Move(centred, shift.X / width, shift.Y / height);
    }
}
