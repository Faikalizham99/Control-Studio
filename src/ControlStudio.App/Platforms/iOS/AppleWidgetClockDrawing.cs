using ControlStudio.Core.Widgets;
using CoreGraphics;
using Foundation;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal static class AppleWidgetClockDrawing
{
    public static void Draw(WidgetLayer layer, string text, CGRect frame, CGContext graphics)
    {
        if (layer.Kind == WidgetLayerKind.Text) { DrawMultiline(layer, text, frame, graphics); return; }
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
        var attributes = new UIStringAttributes { Font = font, ForegroundColor = clock.Gradient is { Enabled: true } ? UIColor.White : alpha }; using var attributesMap = attributes.Dictionary;
        var bounds = sample.GetSizeUsingAttributes(attributes);
        var x = clock.Alignment switch { WidgetClockAlignment.Left => frame.Left, WidgetClockAlignment.Right => frame.Right - bounds.Width, _ => frame.GetMidX() - bounds.Width / 2 };
        var point = new CGPoint(x, frame.GetMidY() - bounds.Height / 2);
        if (clock.Gradient is { Enabled: true } gradient)
            AppleWidgetTextGradient.Paint(graphics, frame, new CGRect(point, bounds), gradient, layer.Opacity, () => sample.DrawString(point, attributes));
        else
        {
            graphics.SaveState(); graphics.ClipToRect(frame);
            sample.DrawString(point, attributes); graphics.RestoreState();
        }
    }
    private const NSStringDrawingOptions MultilineOptions = NSStringDrawingOptions.UsesLineFragmentOrigin | NSStringDrawingOptions.UsesFontLeading;
    private static NSMutableParagraphStyle Paragraph(WidgetClockOptions style) => new()
    {
        Alignment = style.Alignment switch { WidgetClockAlignment.Left => UITextAlignment.Left, WidgetClockAlignment.Right => UITextAlignment.Right, _ => UITextAlignment.Center },
        LineBreakMode = UILineBreakMode.WordWrap
    };
    private static CGSize Measure(string text, WidgetClockOptions style, double size, double width, NSMutableParagraphStyle paragraph)
    {
        using var font = AppleWidgetClockFonts.Create(style, size);
        var attributes = new UIStringAttributes { Font = font, ParagraphStyle = paragraph }; using var map = attributes.Dictionary;
        using var sample = new NSAttributedString(text, attributes);
        return sample.GetBoundingRect(new CGSize(width, 1_000_000), MultilineOptions, null).Size;
    }
    public static double MultilineSize(WidgetLayer layer, double width, double height)
    {
        var style = WidgetTextLayers.Style(layer); var text = layer.Text!.Content;
        var upper = Math.Clamp(height * style.FontScale, .5, 1024); var lower = .5;
        using var paragraph = Paragraph(style);
        bool Fits(double value)
        {
            var bounds = Measure(text, style, value, width, paragraph);
            return bounds.Width <= width + .01 && bounds.Height <= height + .01;
        }
        if (text.Length == 0 || Fits(upper)) return upper;
        // Re-measure wrapping at each font size: scaling a single-line width
        // cannot predict the number of lines in a paragraph.
        for (var iteration = 0; iteration < 16; iteration++)
        {
            var middle = (lower + upper) / 2;
            if (Fits(middle)) lower = middle; else upper = middle;
        }
        return lower;
    }
    private static void DrawMultiline(WidgetLayer layer, string text, CGRect frame, CGContext graphics)
    {
        if (text.Length == 0) return;
        var style = WidgetTextLayers.Style(layer);
        using var font = AppleWidgetClockFonts.Create(style, MultilineSize(layer, frame.Width, frame.Height));
        using var colour = UIColor.FromRGB(Convert.ToInt32(style.ColourHex.Substring(1, 2), 16), Convert.ToInt32(style.ColourHex.Substring(3, 2), 16), Convert.ToInt32(style.ColourHex.Substring(5, 2), 16));
        using var alpha = colour.ColorWithAlpha((nfloat)layer.Opacity);
        using var paragraph = Paragraph(style);
        var attributes = new UIStringAttributes { Font = font, ForegroundColor = style.Gradient is { Enabled: true } ? UIColor.White : alpha, ParagraphStyle = paragraph }; using var map = attributes.Dictionary;
        using var sample = new NSAttributedString(text, attributes);
        var bounds = sample.GetBoundingRect(new CGSize(frame.Width, 1_000_000), MultilineOptions, null);
        var height = Math.Min(frame.Height, Math.Ceiling(bounds.Height));
        var textFrame = new CGRect(frame.X, frame.GetMidY() - height / 2, frame.Width, height);
        if (style.Gradient is { Enabled: true } gradient)
        {
            var x = style.Alignment switch { WidgetClockAlignment.Left => frame.X, WidgetClockAlignment.Right => frame.Right - bounds.Width, _ => frame.GetMidX() - bounds.Width / 2 };
            AppleWidgetTextGradient.Paint(graphics, frame, new CGRect(x, textFrame.Y, Math.Max(1, bounds.Width), Math.Max(1, height)), gradient, layer.Opacity,
                () => sample.DrawString(textFrame, MultilineOptions, null));
            return;
        }
        graphics.SaveState();
        try
        {
            graphics.ClipToRect(frame);
            sample.DrawString(textFrame, MultilineOptions, null);
        }
        finally { graphics.RestoreState(); }
    }
}
