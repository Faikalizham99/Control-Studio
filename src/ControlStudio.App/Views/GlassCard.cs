using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

public sealed class GlassCard : ContentView
{
    public GlassCard(View content, Thickness? padding = null)
    {
        var border = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 24 }, StrokeThickness = 1,
            Padding = padding ?? new Thickness(12), Content = content
        };
        border.SetAppTheme<Brush>(Border.StrokeProperty,
            new SolidColorBrush(Color.FromArgb("#C8CEDA")), new SolidColorBrush(Colors.White.WithAlpha(.22f)));
        border.SetAppThemeColor(BackgroundColorProperty, Colors.White.WithAlpha(.42f), Colors.White.WithAlpha(.035f));
        Content = border;
    }
}
