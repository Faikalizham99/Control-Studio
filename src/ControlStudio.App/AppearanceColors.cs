using ControlStudio.Core;
using Microsoft.Maui.Graphics;

namespace ControlStudio.App;

internal static class AppearanceColors
{
    // ToArgbHex() omits alpha for opaque colors by default. Request alpha
    // explicitly before removing it so every caller receives six RGB digits.
    public static string ToRgbHex(Color color) => "#" + color.ToArgbHex(includeAlpha: true)[3..];

    public static double Luminance(Color color) => AppearanceValues.Luminance(ToRgbHex(color));

    public static Color ButtonBackground(Color accent)
    {
        var button = accent;
        while (1.05 / (Luminance(button) + .05) < 4.5)
            button = new Color(button.Red * .94f, button.Green * .94f, button.Blue * .94f);
        return button;
    }
}
