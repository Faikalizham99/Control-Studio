namespace ControlStudio.Core.Widgets;

public enum WidgetGradientDirection { Horizontal, Vertical, Diagonal }
public sealed record WidgetGradientOptions(bool Enabled = true, string StartHex = "#007AFF", string MiddleHex = "#AF52DE",
    string EndHex = "#FF2D92", WidgetGradientDirection Direction = WidgetGradientDirection.Horizontal);

public static class WidgetGradients
{
    public static void Validate(WidgetGradientOptions? gradient)
    {
        if (gradient is null) return;
        if (!Enum.IsDefined(gradient.Direction) || Colours(gradient).Any(hex =>
            !AppearanceValues.TryNormalizeHex(hex, out var value) || value != hex))
            throw new IOException("Choose three valid gradient colours and a direction.");
    }
    public static string[] Colours(WidgetGradientOptions gradient) => [gradient.StartHex, gradient.MiddleHex, gradient.EndHex];
    public static WidgetGradientOptions WithColour(WidgetGradientOptions gradient, int index, string hex) => index switch
    {
        0 => gradient with { StartHex = hex }, 1 => gradient with { MiddleHex = hex }, 2 => gradient with { EndHex = hex },
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
    public static ((double X, double Y) Start, (double X, double Y) End) Points(WidgetGradientDirection direction) => direction switch
    {
        WidgetGradientDirection.Horizontal => ((0, .5), (1, .5)), WidgetGradientDirection.Vertical => ((.5, 0), (.5, 1)),
        WidgetGradientDirection.Diagonal => ((0, 0), (1, 1)), _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };
}
