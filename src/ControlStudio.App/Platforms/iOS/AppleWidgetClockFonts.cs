using ControlStudio.Core.Widgets;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal static class AppleWidgetClockFonts
{
    public static UIFont Create(WidgetClockOptions clock, double size)
    {
        var weight = WidgetClockWeights.Resolve(clock);
        if (WidgetClockFonts.Family(clock.Font, weight) is { } family)
            return UIFont.FromName(family, (nfloat)size) ?? throw new IOException("The selected clock font is unavailable. Reinstall the latest app build.");
        var nativeWeight = weight switch
        {
            WidgetClockWeight.Light => UIFontWeight.Light, WidgetClockWeight.Regular => UIFontWeight.Regular,
            WidgetClockWeight.Medium => UIFontWeight.Medium, WidgetClockWeight.Semibold => UIFontWeight.Semibold,
            WidgetClockWeight.Bold => UIFontWeight.Bold, WidgetClockWeight.Heavy => UIFontWeight.Heavy,
            _ => throw new IOException("The clock font weight is invalid.")
        };
        var font = UIFont.MonospacedDigitSystemFontOfSize((nfloat)size, nativeWeight) ?? throw new IOException("The clock font is unavailable.");
        if (clock.Font != WidgetClockFont.SfProRounded) return font;
        using (font)
        using (var descriptor = font.FontDescriptor)
        using (var rounded = descriptor.CreateWithDesign(UIFontDescriptorSystemDesign.Rounded))
            return rounded is null ? throw new IOException("The rounded system font is unavailable.")
                : UIFont.FromDescriptor(rounded, (nfloat)size) ?? throw new IOException("The rounded system font is unavailable.");
    }
}
