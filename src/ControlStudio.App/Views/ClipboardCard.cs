namespace ControlStudio.App.Views;

/// <summary>Shared frame for saved cards and their editor preview.</summary>
internal static class ClipboardCard
{
    public static GlassCard Create(ClipboardHeaderView header, View body)
    {
        var divider = new BoxView { HeightRequest = 1, HorizontalOptions = LayoutOptions.Fill, InputTransparent = true };
        divider.SetAppThemeColor(BoxView.ColorProperty, Color.FromArgb("#D2D7E1"), Colors.White.WithAlpha(.18f));
        AutomationProperties.SetIsInAccessibleTree(divider, false);
        return new GlassCard(new VerticalStackLayout { Spacing = 0, Children = { header, divider, body } }, 0);
    }
}
