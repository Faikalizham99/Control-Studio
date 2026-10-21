using ControlStudio.Core;
using Microsoft.Maui.Graphics;

namespace ControlStudio.App;

internal static class AppearanceColors
{
    // ToArgbHex() omits alpha for opaque colors by default. Request alpha
    // explicitly before removing it so every caller receives six RGB digits.
    public static string ToRgbHex(Color color) => "#" + color.ToArgbHex(includeAlpha: true)[3..];

    public static double Luminance(Color color) => AppearanceValues.Luminance(ToRgbHex(color));

    public static Color ButtonForeground(Color accent) => Luminance(accent) > .179 ? Colors.Black : Colors.White;

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
