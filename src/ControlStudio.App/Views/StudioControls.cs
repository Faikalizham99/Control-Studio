using Microsoft.Maui.Graphics;

namespace ControlStudio.App.Views;

internal enum StudioSymbol { Widgets, Clipboard, Counter, Bars, Calendar, Photo, Text, Link, Chevron, Edit, Delete, Open, Preview, Tag, Expand, Clear, EyeOff, Pin, PinFilled, Check, LayerUp, LayerDown, Plus, Hash, Refresh, RotateLeft, RotateRight, Flip, Opacity, Drag, More, Search, Layers, Blend, Lock, Unlock, Clock, CenterHorizontal }

internal sealed class StudioGlyph : GraphicsView, IDrawable
{
    public static readonly BindableProperty TintProperty = BindableProperty.Create(nameof(Tint), typeof(Color), typeof(StudioGlyph), Colors.Black,
        propertyChanged: (view, _, _) => ((StudioGlyph)view).Invalidate());
    private StudioSymbol symbol;
    public StudioSymbol Symbol { get => symbol; set { symbol = value; Invalidate(); } }
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
        else if (symbol is StudioSymbol.Preview or StudioSymbol.EyeOff)
        {
            var eye = new PathF(); eye.MoveTo(2, 12); eye.CurveTo(7, 3, 17, 3, 22, 12);
            eye.CurveTo(17, 21, 7, 21, 2, 12); eye.Close(); canvas.DrawPath(eye); canvas.DrawCircle(12, 12, 3);
            if (symbol == StudioSymbol.EyeOff) canvas.DrawLine(3, 3, 21, 21);
        }
        else if (symbol is StudioSymbol.Pin or StudioSymbol.PinFilled)
        {
            var pin = new PathF(); pin.MoveTo(8, 3); pin.LineTo(16, 3); pin.LineTo(15, 11);
            pin.LineTo(19, 15); pin.LineTo(5, 15); pin.LineTo(9, 11); pin.Close();
            if (symbol == StudioSymbol.PinFilled) { canvas.FillColor = Tint; canvas.FillPath(pin); }
            else canvas.DrawPath(pin);
            canvas.DrawLine(12, 15, 12, 22);
        }
        else if (symbol == StudioSymbol.Tag)
        {
            Lines(canvas, 3, 3, 12, 3, 22, 13, 13, 22, 3, 12, 3, 3); canvas.DrawCircle(8, 8, 1.5f);
        }
        else if (symbol == StudioSymbol.Expand)
        {
            Lines(canvas, 9, 3, 3, 3, 3, 9); canvas.DrawLine(3, 3, 9, 9);
            Lines(canvas, 15, 3, 21, 3, 21, 9); canvas.DrawLine(21, 3, 15, 9);
            Lines(canvas, 3, 15, 3, 21, 9, 21); canvas.DrawLine(3, 21, 9, 15);
            Lines(canvas, 15, 21, 21, 21, 21, 15); canvas.DrawLine(21, 21, 15, 15);
        }
        else if (symbol == StudioSymbol.Clear)
        {
            canvas.DrawCircle(12, 12, 9); canvas.DrawLine(8.5f, 8.5f, 15.5f, 15.5f); canvas.DrawLine(15.5f, 8.5f, 8.5f, 15.5f);
        }
        else if (symbol == StudioSymbol.LayerUp)
        {
            Lines(canvas, 6, 9, 12, 3, 18, 9); canvas.DrawLine(12, 3, 12, 19); canvas.DrawLine(5, 21, 19, 21);
        }
        else if (symbol == StudioSymbol.LayerDown)
        {
            Lines(canvas, 6, 15, 12, 21, 18, 15); canvas.DrawLine(12, 5, 12, 21); canvas.DrawLine(5, 3, 19, 3);
        }
        else if (symbol == StudioSymbol.Plus) { canvas.DrawLine(12, 4, 12, 20); canvas.DrawLine(4, 12, 20, 12); }
        else if (symbol == StudioSymbol.Hash)
        {
            canvas.DrawLine(9, 3, 6, 21); canvas.DrawLine(18, 3, 15, 21);
            canvas.DrawLine(4, 9, 21, 9); canvas.DrawLine(3, 16, 20, 16);
        }
        else if (symbol == StudioSymbol.Refresh)
        {
            var arc = new PathF(); arc.MoveTo(20, 9); arc.CurveTo(17, 1, 5, 1, 3, 11);
            arc.MoveTo(4, 15); arc.CurveTo(7, 23, 19, 23, 21, 13); canvas.DrawPath(arc);
            Lines(canvas, 20, 3, 20, 9, 14, 9); Lines(canvas, 4, 21, 4, 15, 10, 15);
        }
        else if (symbol is StudioSymbol.RotateLeft or StudioSymbol.RotateRight)
        {
            if (symbol == StudioSymbol.RotateRight) { canvas.Translate(24, 0); canvas.Scale(-1, 1); }
            var arc = new PathF(); arc.MoveTo(5, 8); arc.CurveTo(13, -2, 25, 10, 18, 18); arc.CurveTo(12, 25, 2, 19, 4, 13); canvas.DrawPath(arc);
            Lines(canvas, 5, 2, 5, 8, 11, 8);
        }
        else if (symbol == StudioSymbol.Flip)
        {
            Lines(canvas, 3, 9, 10, 3, 10, 9, 3, 9); Lines(canvas, 14, 15, 21, 15, 14, 21, 14, 15);
            canvas.DrawLine(3, 12, 21, 12);
        }
        else if (symbol == StudioSymbol.Blend)
        {
            canvas.DrawCircle(8.5f, 12, 6.5f); canvas.DrawCircle(15.5f, 12, 6.5f);
            canvas.DrawLine(12, 7, 12, 17);
        }
        else if (symbol == StudioSymbol.Opacity)
        {
            var drop = new PathF(); drop.MoveTo(12, 2); drop.CurveTo(9, 7, 4, 12, 4, 15); drop.CurveTo(4, 24, 20, 24, 20, 15); drop.CurveTo(20, 12, 15, 7, 12, 2); drop.Close(); canvas.DrawPath(drop);
        }
        else if (symbol is StudioSymbol.Drag or StudioSymbol.More)
        {
            canvas.FillColor = Tint;
            if (symbol == StudioSymbol.More) { canvas.FillCircle(5, 12, 1.5f); canvas.FillCircle(12, 12, 1.5f); canvas.FillCircle(19, 12, 1.5f); }
            else for (var y = 5; y <= 19; y += 7) { canvas.FillCircle(9, y, 1.2f); canvas.FillCircle(15, y, 1.2f); }
        }
        else if (symbol is StudioSymbol.Lock or StudioSymbol.Unlock)
        {
            canvas.DrawRoundedRectangle(5, 10, 14, 12, 2);
            var shackle = new PathF(); shackle.MoveTo(8, 10); shackle.LineTo(8, 7);
            if (symbol == StudioSymbol.Lock) { shackle.CurveTo(8, 1, 16, 1, 16, 7); shackle.LineTo(16, 10); }
            else shackle.CurveTo(8, 1, 16, 1, 17, 6);
            canvas.DrawPath(shackle); canvas.DrawCircle(12, 15, 1); canvas.DrawLine(12, 16, 12, 18);
        }
        else if (symbol == StudioSymbol.Clock) { canvas.DrawCircle(12, 12, 9); Lines(canvas, 12, 6, 12, 12, 16, 14); }
        else if (symbol == StudioSymbol.CenterHorizontal)
        {
            canvas.DrawLine(12, 2, 12, 22);
            canvas.DrawRoundedRectangle(4, 6, 16, 4, 1); canvas.DrawRoundedRectangle(8, 14, 8, 4, 1);
        }
        else if (symbol == StudioSymbol.Search) { canvas.DrawCircle(10, 10, 6); canvas.DrawLine(14.5f, 14.5f, 21, 21); }
        else if (symbol == StudioSymbol.Layers)
        {
            Lines(canvas, 2, 8, 12, 2, 22, 8, 12, 14, 2, 8);
            Lines(canvas, 2, 13, 12, 19, 22, 13); Lines(canvas, 2, 18, 12, 24, 22, 18);
        }
        else if (symbol == StudioSymbol.Check) Lines(canvas, 4, 12, 9, 17, 20, 6);
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
