using Microsoft.Maui.Graphics;

namespace ControlStudio.App;

internal static class PhotoCropGeometry
{
    public static Rect SourceSquare(double width, double height, double viewport, double zoom, double offsetX, double offsetY)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(viewport) || !double.IsFinite(zoom) ||
            width <= 0 || height <= 0 || viewport <= 0 || zoom <= 0 || !double.IsFinite(offsetX) || !double.IsFinite(offsetY))
            throw new ArgumentOutOfRangeException(nameof(width), "The photo and crop dimensions must be finite and positive.");
        var side = Math.Min(viewport / zoom, Math.Min(width, height));
        return new Rect(Math.Clamp(offsetX / zoom, 0, width - side), Math.Clamp(offsetY / zoom, 0, height - side), side, side);
    }
}
