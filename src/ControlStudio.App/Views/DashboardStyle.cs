using ControlStudio.Core.Actions;
using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

internal static class DashboardStyle
{
    public static void Page(ContentPage page) => page.SetAppThemeColor(VisualElement.BackgroundColorProperty,
        Color.FromArgb("#F2F3F8"), Color.FromArgb("#101116"));
    public static Border Card(View content) {
        var card = new Border { Content = content, Padding = 20, StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 22 } };
        card.SetAppThemeColor(VisualElement.BackgroundColorProperty, Colors.White, Color.FromArgb("#1C1E27"));
        return card;
    }
    public static Button Button(string text) => new() { Text = text, CornerRadius = 14,
        MinimumHeightRequest = 48, BackgroundColor = Color.FromArgb("#2458E6"), TextColor = Colors.White };
    public static string Icon(ActionIcon icon) => icon switch {
        ActionIcon.Link => "↗", ActionIcon.Copy => "▣", ActionIcon.Counter => "+1",
        ActionIcon.Star => "★", ActionIcon.Book => "▤", _ => "ϟ" };
    public static Color TileColor(ActionColor color) => Microsoft.Maui.Graphics.Color.FromArgb(color switch {
        ActionColor.Purple => "#7241BB", ActionColor.Green => "#147047", ActionColor.Orange => "#9A4A0B",
        ActionColor.Pink => "#B52F6A", ActionColor.Slate => "#475569", _ => "#2458E6" });
    public static string Kind(ActionParameters parameters) => parameters switch {
        OpenUrlParameters => "Open web link", CopyTextParameters => "Copy text", _ => "Increment counter" };
}
