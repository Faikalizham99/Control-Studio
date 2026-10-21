using Microsoft.Maui.Graphics;

namespace ControlStudio.App.Views;

internal enum StudioSymbol { Widgets, Clipboard, Counter }

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
            canvas.DrawRoundedRectangle(3, 3, 7, 7, 1.5f); canvas.DrawRoundedRectangle(14, 3, 7, 4, 1.5f);
            canvas.DrawRoundedRectangle(3, 14, 7, 7, 1.5f); canvas.DrawRoundedRectangle(14, 11, 7, 10, 1.5f);
        }
        else
        {
            canvas.DrawRoundedRectangle(5, 5, 14, 16, 2); canvas.DrawRoundedRectangle(8, 2, 8, 5, 1.5f);
            canvas.DrawLine(9, 12, 15, 12); canvas.DrawLine(9, 16, 15, 16);
        }
        canvas.RestoreState();
    }
}
