namespace ControlStudio.App.Views;

internal static class DashboardStyle
{
    public static void Page(ContentPage page)
    {
        page.SetAppThemeColor(VisualElement.BackgroundColorProperty, Color.FromArgb("#F2F3F8"), Color.FromArgb("#101116"));
        NavigationAccent(page);
    }
    public static void NavigationAccent(ContentPage page) => page.SetDynamicResource(NavigationPage.IconColorProperty, "AccentText");
    public static Button Button(string text)
    {
        return new AccentButton { Text = text };
    }
}
