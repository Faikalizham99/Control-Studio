using ControlStudio.Core.Widgets;
using CoreGraphics;
using Foundation;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal static class AppleWidgetTextGradient
{
    private static UIColor Colour(string hex) => UIColor.FromRGB(Convert.ToInt32(hex.Substring(1, 2), 16),
        Convert.ToInt32(hex.Substring(3, 2), 16), Convert.ToInt32(hex.Substring(5, 2), 16));
    public static void Draw(CGContext graphics, CGRect bounds, WidgetGradientOptions gradient)
    {
        using var space = CGColorSpace.CreateSrgb();
        using var start = Colour(gradient.StartHex); using var middle = Colour(gradient.MiddleHex); using var end = Colour(gradient.EndHex);
        using var fill = new CGGradient(space, new[] { start.CGColor, middle.CGColor, end.CGColor });
        var points = WidgetGradients.Points(gradient.Direction);
        graphics.DrawLinearGradient(fill, new CGPoint(bounds.X + bounds.Width * points.Start.X, bounds.Y + bounds.Height * points.Start.Y),
            new CGPoint(bounds.X + bounds.Width * points.End.X, bounds.Y + bounds.Height * points.End.Y),
            CGGradientDrawingOptions.DrawsBeforeStartLocation | CGGradientDrawingOptions.DrawsAfterEndLocation);
    }
    public static void Paint(CGContext graphics, CGRect frame, CGRect bounds, WidgetGradientOptions gradient, double opacity, Action mask)
    {
        graphics.SaveState();
        try
        {
            graphics.ClipToRect(frame); graphics.BeginTransparencyLayer((NSDictionary?)null);
            try
            {
                mask(); graphics.SaveState();
                try { graphics.SetBlendMode(CGBlendMode.SourceIn); graphics.SetAlpha((nfloat)opacity); Draw(graphics, bounds, gradient); }
                finally { graphics.RestoreState(); }
            }
            finally { graphics.EndTransparencyLayer(); }
        }
        finally { graphics.RestoreState(); }
    }
}
