namespace ControlStudio.Core.Widgets;

// Persist names, not native API values. Normal must remain the missing-field default.
public enum WidgetBlendMode
{
    Normal, Darken, Multiply, ColorBurn, DarkerColor,
    Lighten, Screen, ColorDodge, LighterColor,
    Overlay, SoftLight, HardLight, Difference, Exclusion
}

public static class WidgetBlending
{
    public static IReadOnlyList<string> Groups { get; } = Array.AsReadOnly(new[] { "Normal", "Darken", "Lighten", "Contrast", "Effects" });
    public static IReadOnlyList<WidgetBlendMode> Modes { get; } = Array.AsReadOnly(Enum.GetValues<WidgetBlendMode>());
    public static string Name(WidgetBlendMode mode) => mode switch
    {
        WidgetBlendMode.ColorBurn => "Color Burn", WidgetBlendMode.DarkerColor => "Darker Color",
        WidgetBlendMode.ColorDodge => "Color Dodge", WidgetBlendMode.LighterColor => "Lighter Color",
        WidgetBlendMode.SoftLight => "Soft Light", WidgetBlendMode.HardLight => "Hard Light",
        _ when Enum.IsDefined(mode) => mode.ToString(), _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
    public static int Group(WidgetBlendMode mode) => mode switch
    {
        WidgetBlendMode.Normal => 0,
        WidgetBlendMode.Darken or WidgetBlendMode.Multiply or WidgetBlendMode.ColorBurn or WidgetBlendMode.DarkerColor => 1,
        WidgetBlendMode.Lighten or WidgetBlendMode.Screen or WidgetBlendMode.ColorDodge or WidgetBlendMode.LighterColor => 2,
        WidgetBlendMode.Overlay or WidgetBlendMode.SoftLight or WidgetBlendMode.HardLight => 3,
        WidgetBlendMode.Difference or WidgetBlendMode.Exclusion => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}
