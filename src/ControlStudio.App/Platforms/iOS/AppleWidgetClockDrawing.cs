using ControlStudio.Core.Widgets;
using CoreGraphics;
using Foundation;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal static class AppleWidgetClockDrawing
{
    public static void Draw(WidgetLayer layer, string text, CGRect frame, CGContext graphics)
    {
        var clock = layer.Clock!;
        var size = Math.Min(1024, frame.Height * clock.FontScale);
        using var candidate = Font(clock.Font, size);
        using var sample = new NSString(text);
        var initial = new UIStringAttributes { Font = candidate }; using var initialMap = initial.Dictionary;
        var measured = sample.GetSizeUsingAttributes(initial);
        var actualSize = measured.Width > frame.Width ? size * frame.Width / measured.Width : size;
        using var font = Font(clock.Font, Math.Max(.5, actualSize));
        using var colour = UIColor.FromRGB(Convert.ToInt32(clock.ColourHex.Substring(1, 2), 16), Convert.ToInt32(clock.ColourHex.Substring(3, 2), 16), Convert.ToInt32(clock.ColourHex.Substring(5, 2), 16));
        using var alpha = colour.ColorWithAlpha((nfloat)layer.Opacity);
        var attributes = new UIStringAttributes { Font = font, ForegroundColor = alpha }; using var attributesMap = attributes.Dictionary;
        var bounds = sample.GetSizeUsingAttributes(attributes);
        var x = clock.Alignment switch { WidgetClockAlignment.Left => frame.Left, WidgetClockAlignment.Right => frame.Right - bounds.Width, _ => frame.GetMidX() - bounds.Width / 2 };
        graphics.SaveState(); graphics.ClipToRect(frame);
        sample.DrawString(new CGPoint(x, frame.GetMidY() - bounds.Height / 2), attributes); graphics.RestoreState();
    }
    private static UIFont Font(WidgetClockFont font, double size) => (font switch
    {
        WidgetClockFont.Rounded => UIFont.FromName("ArialRoundedMTBold", (nfloat)size) ?? UIFont.MonospacedDigitSystemFontOfSize((nfloat)size, UIFontWeight.Semibold),
        WidgetClockFont.Monospaced => UIFont.FromName("Menlo-Bold", (nfloat)size) ?? UIFont.MonospacedDigitSystemFontOfSize((nfloat)size, UIFontWeight.Semibold),
        WidgetClockFont.Serif => UIFont.FromName("Georgia-Bold", (nfloat)size) ?? UIFont.MonospacedDigitSystemFontOfSize((nfloat)size, UIFontWeight.Semibold),
        _ => UIFont.MonospacedDigitSystemFontOfSize((nfloat)size, UIFontWeight.Semibold)
    }) ?? throw new IOException("The clock font is unavailable.");
}
