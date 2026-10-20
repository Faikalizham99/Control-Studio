using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

public sealed class GlassCard : ContentView
{
    public GlassCard(View content)
    {
        Content = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 24 }, StrokeThickness = 1,
            Stroke = new LinearGradientBrush(new GradientStopCollection
            {
                new(Colors.White.WithAlpha(.8f), 0), new(Colors.White.WithAlpha(.15f), .6f), new(Colors.White.WithAlpha(.5f), 1)
            }, new Point(0, 0), new Point(1, 1)),
            Padding = 12, Content = content
        };
    }
}
