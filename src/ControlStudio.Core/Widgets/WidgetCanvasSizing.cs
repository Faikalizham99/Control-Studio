namespace ControlStudio.Core.Widgets;

public sealed record WidgetCanvasSize(double Width, double Height);
public sealed record WidgetCanvasSizing(WidgetCanvasSize Canvas, bool Measured = false);
public sealed record WidgetDisplayMeasurement(int SchemaVersion, WidgetSize Size, double ScreenWidth, double ScreenHeight,
    double ScreenScale, int OsMajor, double Width, double Height);

public static class WidgetCanvasSizes
{
    public static bool Valid(WidgetSize family, WidgetCanvasSize value)
    {
        if (!WidgetDesignRules.IsSupported(family) || !double.IsFinite(value.Width) || !double.IsFinite(value.Height)
            || value.Width is < 50 or > 600 || value.Height is < 50 or > 1100) return false;
        var ratio = value.Width / value.Height;
        return family switch
        {
            WidgetSize.Small => ratio is >= .98 and <= 1.02,
            WidgetSize.Medium => ratio is >= 1.5 and <= 2.8,
            WidgetSize.Large => ratio is >= .75 and <= 1.2,
            WidgetSize.ExtraLargePortrait => ratio is >= .35 and <= .8,
            _ => false
        };
    }
    public static bool Matches(WidgetDisplayMeasurement value, WidgetSize family, double width, double height, double scale, int osMajor)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || !double.IsFinite(scale) || scale <= 0) return false;
        return value.SchemaVersion == 1 && value.Size == family && value.OsMajor == osMajor
            && Math.Abs(value.ScreenWidth - Math.Min(width, height)) < .5 && Math.Abs(value.ScreenHeight - Math.Max(width, height)) < .5
            && Math.Abs(value.ScreenScale - scale) < .01 && Valid(family, new(value.Width, value.Height))
            && value.Width <= Math.Min(width, height) * (family == WidgetSize.Small ? .6 : 1.01)
            && (family == WidgetSize.Small || value.Width >= Math.Min(width, height) * .6);
    }
    // Apple's published iOS point-size examples. These are fallbacks: WidgetKit's
    // actual displaySize takes precedence, especially for newer phones and XL.
    public static WidgetCanvasSizing Estimate(WidgetSize family, double screenWidth, double screenHeight)
    {
        var key = double.IsFinite(screenWidth) && double.IsFinite(screenHeight)
            ? ((int)Math.Round(Math.Min(screenWidth, screenHeight)), (int)Math.Round(Math.Max(screenWidth, screenHeight))) : (0, 0);
        var values = key switch
        {
            (430, 932) or (428, 926) => (170d, 364d, 170d, 382d),
            (414, 896) => (169d, 360d, 169d, 379d),
            (414, 736) => (159d, 348d, 157d, 357d),
            (393, 852) or (390, 844) => (158d, 338d, 158d, 354d),
            (375, 812) or (360, 780) => (155d, 329d, 155d, 345d),
            (375, 667) => (148d, 321d, 148d, 324d),
            (320, 568) => (141d, 292d, 141d, 311d),
            _ => Unknown(screenWidth, screenHeight)
        };
        var (small, wide, mediumHeight, largeHeight) = values;
        return new(family switch
        {
            WidgetSize.Small => new(small, small), WidgetSize.Medium => new(wide, mediumHeight),
            WidgetSize.Large => new(wide, largeHeight),
            // Explicit estimate only; do not describe this inferred grid height as exact.
            WidgetSize.ExtraLargePortrait => new(wide, 2 * largeHeight - small),
            _ => throw new IOException("This widget size requires a newer app.")
        });
    }
    private static (double, double, double, double) Unknown(double width, double height)
    {
        var smaller = Math.Min(width, height);
        var scale = double.IsFinite(smaller) && smaller is >= 250 and <= 600 ? smaller / 430 : 1;
        return (170 * scale, 364 * scale, 170 * scale, 382 * scale);
    }
}
