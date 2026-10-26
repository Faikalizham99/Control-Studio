using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using CoreGraphics;
using Foundation;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

internal static class AppleWidgetBatteryDrawing
{
    public static void Draw(WidgetLayer layer, WidgetBatteryReading reading, CGRect frame, CGContext graphics)
    {
        graphics.SaveState();
        try
        {
            graphics.ClipToRect(frame); graphics.SetAlpha((nfloat)layer.Opacity); graphics.BeginTransparencyLayer();
            try
            {
                var scale = Math.Min(frame.Width / 160, frame.Height / 100);
                graphics.TranslateCTM((nfloat)(frame.X + (frame.Width - 160 * scale) / 2), (nfloat)(frame.Y + (frame.Height - 100 * scale) / 2));
                graphics.ScaleCTM((nfloat)scale, (nfloat)scale);
                WidgetBatteryDrawing.Draw(new Canvas(), layer.Battery!, reading);
            }
            finally { graphics.EndTransparencyLayer(); }
        }
        finally { graphics.RestoreState(); }
    }
    private sealed class Canvas : IWidgetBatteryCanvas
    {
        private static UIColor Colour(Color colour) => UIColor.FromRGBA(colour.Red, colour.Green, colour.Blue, colour.Alpha);
        public void Rectangle(float x, float y, float width, float height, float radius, Color colour, bool fill, float stroke)
        {
            using var path = UIBezierPath.FromRoundedRect(new CGRect(x, y, width, height), radius);
            using var tint = Colour(colour); if (fill) { tint.SetFill(); path.Fill(); } else { tint.SetStroke(); path.LineWidth = stroke; path.Stroke(); }
        }
        public void Path(IReadOnlyList<PointF> points, Color colour, float stroke, bool fill)
        {
            using var path = new UIBezierPath(); path.MoveTo(new(points[0].X, points[0].Y));
            foreach (var point in points.Skip(1)) path.AddLineTo(new(point.X, point.Y));
            using var tint = Colour(colour);
            if (fill) { path.ClosePath(); tint.SetFill(); path.Fill(); }
            else { tint.SetStroke(); path.LineWidth = stroke; path.LineCapStyle = CGLineCap.Round; path.LineJoinStyle = CGLineJoin.Round; path.Stroke(); }
        }
        public void Text(string text, float x, float y, float width, float height, float size, Color colour, Color? outline)
        {
            using var font = UIFont.FromName("Arial-BoldMT", size) ?? UIFont.BoldSystemFontOfSize(size) ?? throw new IOException("Battery font is unavailable.");
            using var tint = Colour(colour); using var paragraph = new NSMutableParagraphStyle { Alignment = UITextAlignment.Center };
            using var value = new NSString(text);
            var attributes = new UIStringAttributes { Font = font, ForegroundColor = tint, ParagraphStyle = paragraph }; using var map = attributes.Dictionary;
            if (outline is not null)
            {
                using var edge = Colour(outline); attributes.ForegroundColor = edge;
                foreach (var dx in new[] { -.75f, 0, .75f })
                foreach (var dy in new[] { -.75f, 0, .75f })
                    if (dx != 0 || dy != 0) value.DrawString(new CGRect(x + dx, y + dy + (height - font.LineHeight) / 2, width, font.LineHeight), attributes);
                attributes.ForegroundColor = tint;
            }
            value.DrawString(new CGRect(x, y + (height - font.LineHeight) / 2, width, font.LineHeight), attributes);
        }
    }
}
