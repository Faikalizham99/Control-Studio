using ControlStudio.Core.Widgets;
using CoreGraphics;
using Foundation;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal static class AppleWidgetClockDrawing
{
    public static void Draw(WidgetLayer layer, string text, CGRect frame, CGContext graphics)
    {
        var clock = WidgetTextLayers.Style(layer);
        var size = Math.Min(1024, frame.Height * clock.FontScale);
        using var candidate = AppleWidgetClockFonts.Create(clock, size);
        using var sample = new NSString(text);
        var initial = new UIStringAttributes { Font = candidate }; using var initialMap = initial.Dictionary;
        var measured = sample.GetSizeUsingAttributes(initial);
        // Display fonts may have taller line metrics than the built-in faces.
        // Fit both dimensions so selecting a font cannot crop the clock line.
        var actualSize = size * Math.Min(1, Math.Min(frame.Width / Math.Max(1, measured.Width), frame.Height / Math.Max(1, measured.Height)));
        using var font = AppleWidgetClockFonts.Create(clock, Math.Max(.5, actualSize));
        using var colour = UIColor.FromRGB(Convert.ToInt32(clock.ColourHex.Substring(1, 2), 16), Convert.ToInt32(clock.ColourHex.Substring(3, 2), 16), Convert.ToInt32(clock.ColourHex.Substring(5, 2), 16));
        using var alpha = colour.ColorWithAlpha((nfloat)layer.Opacity);
        var attributes = new UIStringAttributes { Font = font, ForegroundColor = alpha }; using var attributesMap = attributes.Dictionary;
        var bounds = sample.GetSizeUsingAttributes(attributes);
        var x = clock.Alignment switch { WidgetClockAlignment.Left => frame.Left, WidgetClockAlignment.Right => frame.Right - bounds.Width, _ => frame.GetMidX() - bounds.Width / 2 };
        graphics.SaveState(); graphics.ClipToRect(frame);
        sample.DrawString(new CGPoint(x, frame.GetMidY() - bounds.Height / 2), attributes); graphics.RestoreState();
    }
}
