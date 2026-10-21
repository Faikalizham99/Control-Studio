using Microsoft.Maui.Graphics;

namespace ControlStudio.App.Views;

internal enum StudioSymbol { Widgets, Clipboard, Counter, Bars, Calendar, Photo, Text, Link, Snippets, Chevron, Edit, Delete, Open, Preview }

internal sealed class StudioGlyph : GraphicsView, IDrawable
{
    public static readonly BindableProperty TintProperty = BindableProperty.Create(nameof(Tint), typeof(Color), typeof(StudioGlyph), Colors.Black,
        propertyChanged: (view, _, _) => ((StudioGlyph)view).Invalidate());
    private readonly StudioSymbol symbol;
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }

    public StudioGlyph(StudioSymbol symbol, bool useAccent = true)
    {
        this.symbol = symbol; Drawable = this; WidthRequest = HeightRequest = 28; InputTransparent = true;
        if (useAccent) SetDynamicResource(TintProperty, "AccentText");
        AutomationProperties.SetIsInAccessibleTree(this, false);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState(); canvas.Scale((float)Width / 24, (float)Height / 24);
        canvas.StrokeColor = Tint; canvas.StrokeSize = 1.7f; canvas.StrokeLineCap = LineCap.Round; canvas.StrokeLineJoin = LineJoin.Round;
        if (symbol == StudioSymbol.Counter)
        {
            canvas.FillColor = Tint;
            canvas.FillRoundedRectangle(2, 2, 9, 9, 2); canvas.FillRoundedRectangle(13, 2, 9, 9, 2);
            canvas.FillRoundedRectangle(2, 13, 9, 9, 2); canvas.FillRoundedRectangle(13, 13, 9, 9, 2);
        }
        else if (symbol == StudioSymbol.Widgets)
        {
            canvas.DrawRoundedRectangle(3, 3, 7, 7, 1.5f); canvas.DrawRoundedRectangle(14, 3, 7, 7, 1.5f);
            canvas.DrawRoundedRectangle(3, 14, 7, 7, 1.5f); canvas.DrawRoundedRectangle(14, 14, 7, 7, 1.5f);
        }
        else if (symbol == StudioSymbol.Clipboard)
        {
            canvas.DrawRoundedRectangle(5, 5, 14, 16, 2); canvas.DrawRoundedRectangle(8, 2, 8, 5, 1.5f);
            canvas.DrawLine(9, 12, 15, 12); canvas.DrawLine(9, 16, 15, 16);
        }
        else if (symbol == StudioSymbol.Bars)
        {
            canvas.DrawLine(3, 21, 21, 21);
            canvas.DrawRoundedRectangle(4, 11, 3, 10, .5f); canvas.DrawRoundedRectangle(10.5f, 3, 3, 18, .5f); canvas.DrawRoundedRectangle(17, 8, 3, 13, .5f);
        }
        else if (symbol == StudioSymbol.Calendar)
        {
            canvas.DrawRoundedRectangle(3, 5, 18, 16, 2); canvas.DrawLine(3, 10, 21, 10);
            canvas.DrawLine(7, 2, 7, 7); canvas.DrawLine(17, 2, 17, 7);
        }
        else if (symbol == StudioSymbol.Photo)
        {
            canvas.DrawRoundedRectangle(3, 4, 18, 16, 2); canvas.DrawCircle(8, 9, 1.5f);
            Lines(canvas, 4, 17, 10, 11, 14, 15, 17, 12, 21, 16);
        }
        else if (symbol == StudioSymbol.Text)
        {
            Lines(canvas, 14, 3, 6, 3, 6, 21, 19, 21, 19, 8, 14, 3, 14, 8, 19, 8);
            canvas.DrawLine(9, 12, 16, 12); canvas.DrawLine(9, 16, 16, 16);
        }
        else if (symbol == StudioSymbol.Link)
        {
            // Two open chain links, using only local vector geometry.
            var top = new PathF(); top.MoveTo(11, 8); top.LineTo(14, 5); top.CurveTo(19, 0, 25, 6, 20, 11); top.LineTo(17, 14); canvas.DrawPath(top);
            var bottom = new PathF(); bottom.MoveTo(13, 16); bottom.LineTo(10, 19); bottom.CurveTo(5, 24, -1, 18, 4, 13); bottom.LineTo(7, 10); canvas.DrawPath(bottom);
            canvas.DrawLine(8, 16, 16, 8);
        }
        else if (symbol == StudioSymbol.Snippets)
        {
            Lines(canvas, 7, 6, 2, 12, 7, 18); Lines(canvas, 17, 6, 22, 12, 17, 18);
            canvas.DrawLine(14, 3, 10, 21);
        }
        else if (symbol == StudioSymbol.Edit)
        {
            Lines(canvas, 4, 15, 15, 4, 20, 9, 9, 20, 3, 21, 4, 15);
            canvas.DrawLine(12, 7, 17, 12);
        }
        else if (symbol == StudioSymbol.Delete)
        {
            canvas.DrawLine(4, 6, 20, 6); Lines(canvas, 9, 6, 9, 3, 15, 3, 15, 6);
            Lines(canvas, 6, 6, 7, 21, 17, 21, 18, 6);
            canvas.DrawLine(10, 10, 10.5f, 17); canvas.DrawLine(14, 10, 13.5f, 17);
        }
        else if (symbol == StudioSymbol.Open)
        {
            Lines(canvas, 11, 4, 4, 4, 4, 20, 20, 20, 20, 13);
            Lines(canvas, 14, 3, 21, 3, 21, 10); canvas.DrawLine(11, 13, 21, 3);
        }
        else if (symbol == StudioSymbol.Preview)
        {
            var eye = new PathF(); eye.MoveTo(2, 12); eye.CurveTo(7, 3, 17, 3, 22, 12);
            eye.CurveTo(17, 21, 7, 21, 2, 12); eye.Close(); canvas.DrawPath(eye); canvas.DrawCircle(12, 12, 3);
        }
        else if (symbol == StudioSymbol.Chevron) Lines(canvas, 9, 5, 16, 12, 9, 19);
        canvas.RestoreState();
    }

    private static void Lines(ICanvas canvas, params float[] points)
    {
        var path = new PathF(); path.MoveTo(points[0], points[1]);
        for (var i = 2; i < points.Length; i += 2) path.LineTo(points[i], points[i + 1]);
        canvas.DrawPath(path);
    }
}
