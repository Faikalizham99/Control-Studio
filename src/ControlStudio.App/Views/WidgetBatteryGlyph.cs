using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

// Canonical 160 x 100 artwork shared by editor guides and the native PNG compositor.
internal interface IWidgetBatteryCanvas
{
    void Rectangle(float x, float y, float width, float height, float radius, Color colour, bool fill, float stroke = 3);
    void Path(IReadOnlyList<PointF> points, Color colour, float stroke, bool fill = false);
    void Text(string text, float x, float y, float width, float height, float size, Color colour);
}
internal static class WidgetBatteryDrawing
{
    public static void Draw(IWidgetBatteryCanvas canvas, WidgetBatteryOptions options, WidgetBatteryReading reading)
    {
        var colour = Color.FromArgb(WidgetBatteries.Colour(options, reading));
        var track = Color.FromArgb("#8E8E93").WithAlpha(.28f);
        var fraction = reading.Known ? reading.Percent!.Value / 100f : 0;
        void Arc(float cx, float cy, float radius, double start, double sweep, Color tint, float stroke)
        {
            if (sweep <= 0) return;
            var points = Enumerable.Range(0, 65).Select(i =>
            {
                var angle = (start + sweep * i / 64) * Math.PI / 180;
                return new PointF(cx + radius * (float)Math.Cos(angle), cy + radius * (float)Math.Sin(angle));
            }).ToArray();
            canvas.Path(points, tint, stroke);
        }
        switch (options.Shape)
        {
            case WidgetBatteryShape.Battery:
                canvas.Rectangle(30, 12, 94, 48, 8, colour, false);
                canvas.Rectangle(127, 28, 5, 16, 2, colour, true);
                canvas.Rectangle(35, 17, 84, 38, 4, track, true);
                if (fraction > 0) canvas.Rectangle(35, 17, 84 * fraction, 38, Math.Min(4, 42 * fraction), colour, true);
                break;
            case WidgetBatteryShape.Bar:
                canvas.Rectangle(12, 24, 136, 18, 9, track, true);
                if (fraction > 0) canvas.Rectangle(12, 24, 136 * fraction, 18, Math.Min(9, 68 * fraction), colour, true);
                break;
            case WidgetBatteryShape.Ring:
                Arc(80, 34, 27, -90, 360, track, 7); Arc(80, 34, 27, -90, 360 * fraction, colour, 7); break;
            case WidgetBatteryShape.Arc:
                Arc(80, 49, 37, -180, 180, track, 7); Arc(80, 49, 37, -180, 180 * fraction, colour, 7); break;
            case WidgetBatteryShape.Percentage:
                canvas.Text(reading.Text, 4, 22, 152, 56, 44, colour); break;
        }
        if (options.Shape != WidgetBatteryShape.Percentage && (options.ShowPercentage || !reading.Known))
            canvas.Text(reading.Text, 20, 68, 120, 26, 21, colour);
        if (options.ShowCharging && reading.Known && (reading.Charging || reading.Full))
        {
            if (reading.Full) canvas.Path([new(136, 80), new(141, 85), new(150, 73)], colour, 3);
            else canvas.Path([new(144, 69), new(136, 81), new(142, 81), new(139, 91), new(150, 77), new(144, 77)], colour, 0, true);
        }
    }
}
internal sealed class WidgetBatteryGlyph : GraphicsView, IDrawable
{
    private WidgetBatteryOptions options = WidgetBatteries.Defaults();
    private WidgetBatteryReading reading;
    public WidgetBatteryGlyph() { Drawable = this; InputTransparent = true; IsVisible = false; }
    public void Update(WidgetBatteryOptions value, WidgetBatteryReading snapshot) { options = value; reading = snapshot; Invalidate(); }
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var scale = Math.Min(dirtyRect.Width / 160, dirtyRect.Height / 100);
        if (scale <= 0) return;
        canvas.SaveState();
        try
        {
            canvas.Translate((dirtyRect.Width - 160 * scale) / 2, (dirtyRect.Height - 100 * scale) / 2); canvas.Scale(scale, scale);
            WidgetBatteryDrawing.Draw(new Canvas(canvas), options, reading);
        }
        finally { canvas.RestoreState(); }
    }
    private sealed class Canvas(ICanvas canvas) : IWidgetBatteryCanvas
    {
        public void Rectangle(float x, float y, float width, float height, float radius, Color colour, bool fill, float stroke)
        {
            if (fill) { canvas.FillColor = colour; canvas.FillRoundedRectangle(x, y, width, height, radius); }
            else { canvas.StrokeColor = colour; canvas.StrokeSize = stroke; canvas.DrawRoundedRectangle(x, y, width, height, radius); }
        }
        public void Path(IReadOnlyList<PointF> points, Color colour, float stroke, bool fill)
        {
            var path = new PathF(); path.MoveTo(points[0]); foreach (var point in points.Skip(1)) path.LineTo(point);
            if (fill) { path.Close(); canvas.FillColor = colour; canvas.FillPath(path); }
            else { canvas.StrokeColor = colour; canvas.StrokeSize = stroke; canvas.StrokeLineCap = LineCap.Round; canvas.StrokeLineJoin = LineJoin.Round; canvas.DrawPath(path); }
        }
        public void Text(string text, float x, float y, float width, float height, float size, Color colour)
        {
            canvas.Font = new Microsoft.Maui.Graphics.Font("Arial", FontWeights.Bold); canvas.FontSize = size; canvas.FontColor = colour;
            canvas.DrawString(text, x, y, width, height, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
