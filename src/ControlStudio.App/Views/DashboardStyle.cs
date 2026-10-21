using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

internal static class DashboardStyle
{
    public static void Page(ContentPage page)
    {
        page.SetAppThemeColor(VisualElement.BackgroundColorProperty, Color.FromArgb("#F2F3F8"), Color.FromArgb("#101116"));
        NavigationAccent(page);
    }
    public static void NavigationAccent(ContentPage page) => page.SetDynamicResource(NavigationPage.IconColorProperty, "AccentText");
    public static Border Card(View content) {
        var card = new Border { Content = content, Padding = 20, StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 22 } };
        card.SetAppThemeColor(VisualElement.BackgroundColorProperty, Colors.White, Color.FromArgb("#1C1E27"));
        return card;
    }
    public static Button Button(string text)
    {
        var button = new Button { Text = text, CornerRadius = 14, MinimumHeightRequest = 48 };
        button.SetDynamicResource(VisualElement.BackgroundColorProperty, "AccentButton");
        button.SetDynamicResource(Microsoft.Maui.Controls.Button.TextColorProperty, "AccentButtonText");
        return button;
    }
}
