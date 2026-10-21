using ControlStudio.Core;
using Microsoft.Maui.Graphics;

namespace ControlStudio.App;

internal static class AppearanceColors
{
    // ToArgbHex() omits alpha for opaque colors by default. Request alpha
    // explicitly before removing it so every caller receives six RGB digits.
    public static string ToRgbHex(Color color) => "#" + color.ToArgbHex(includeAlpha: true)[3..];

    public static double Luminance(Color color) => AppearanceValues.Luminance(ToRgbHex(color));

    // Prefer white when it passes the contrast requirement for the actual label.
    // 19 device-independent units in bold exceeds 14pt (18.67 CSS px).
    // Small text retains 4.5:1; large text requires 3:1. Never alter the accent.
    public static Color ButtonForeground(Color accent, double fontSize = 14, bool bold = false)
    {
        var minimum = fontSize >= 24 || (bold && fontSize >= 19) ? 3d : 4.5d;
        return Contrast(accent, Colors.White) >= minimum ? Colors.White : Colors.Black;
    }

    public static double Contrast(Color first, Color second)
    {
        var a = Luminance(first); var b = Luminance(second);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }

    public static Color TextAccent(Color accent, bool dark)
    {
        var background = Luminance(Color.FromArgb(dark ? "#1C1E27" : "#F2F2F7"));
        var color = accent;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var foreground = Luminance(color);
            if ((Math.Max(foreground, background) + .05) / (Math.Min(foreground, background) + .05) >= 4.5) return color;
            color = dark
                ? new Color(color.Red + (1 - color.Red) * .06f, color.Green + (1 - color.Green) * .06f, color.Blue + (1 - color.Blue) * .06f)
                : new Color(color.Red * .94f, color.Green * .94f, color.Blue * .94f);
        }
        return dark ? Colors.White : Colors.Black;
    }
}
