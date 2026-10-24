using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

internal static class SettingsControls
{
    /// <summary>Keep the native switch at its intrinsic size and centred beside wrapping text.</summary>
    public static Switch RowSwitch(string? automationId = null)
    {
        var toggle = new Switch { HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center };
        if (automationId is not null) toggle.AutomationId = automationId;
        toggle.SetDynamicResource(Switch.OnColorProperty, "Accent");
        return toggle;
    }

    public static Label Text(string text, double size = 13, bool secondary = false, bool bold = false)
    {
        var label = new Label { Text = text, FontSize = size, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None };
        label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb(secondary ? "#626570" : "#16171C"), Color.FromArgb(secondary ? "#AFB3BD" : "#F5F5F7"));
        return label;
    }
    public static Border Rounded(View content, double radius = 18, Thickness? padding = null)
    {
        var border = new Border { Content = content, StrokeShape = new RoundRectangle { CornerRadius = radius }, Padding = padding ?? new Thickness(0), StrokeThickness = 1 };
        SurfaceStroke(border);
        border.SetAppThemeColor(VisualElement.BackgroundColorProperty, Colors.White.WithAlpha(.65f), Colors.White.WithAlpha(.065f));
        return border;
    }
    public static void SurfaceStroke(Border border) => border.SetAppTheme<Brush>(Border.StrokeProperty,
        new SolidColorBrush(Color.FromArgb("#D2D7E1")), new SolidColorBrush(Colors.White.WithAlpha(.18f)));
    public static LinearGradientBrush Swatch(Color color) => new(new GradientStopCollection
    {
        new(new Color(Math.Min(1, color.Red + .18f), Math.Min(1, color.Green + .18f), Math.Min(1, color.Blue + .18f)), 0), new(color, .65f),
        new(new Color(color.Red * .85f, color.Green * .85f, color.Blue * .85f), 1)
    }, new Point(0, 0), new Point(1, 1));
    public static Button TapTarget(string description, string automationId) {
        var button = new Button { Text = "", Padding = 0, BackgroundColor = Colors.Transparent, BorderWidth = 0,
            MinimumWidthRequest = 44, MinimumHeightRequest = 44, AutomationId = automationId };
        SemanticProperties.SetDescription(button, description);
        return button;
    }
}

internal sealed class ThemeChoice : ContentView
{
    private readonly Border border;
    private readonly Border badge;
    private readonly Button tap;
    private readonly string title;
    private readonly Label check;
    public ThemeChoice(string title, string image, Action selected)
    {
        this.title = title;
        var label = SettingsControls.Text(title, 13); label.HorizontalTextAlignment = TextAlignment.Center;
        var icon = new Image { Source = image, WidthRequest = 48, HeightRequest = 48, HorizontalOptions = LayoutOptions.Center };
        border = SettingsControls.Rounded(new VerticalStackLayout { Spacing = 5, Padding = new Thickness(6, 11, 6, 9), Children = { icon, label } }, 18);
        check = new Label { Text = "✓", FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, FontAutoScalingEnabled = false };
        badge = new Border { Content = check, StrokeThickness = 0, StrokeShape = new Ellipse(), WidthRequest = 16, HeightRequest = 16, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, Margin = 5, InputTransparent = true };
        badge.SetDynamicResource(BackgroundColorProperty, "Accent");
        tap = SettingsControls.TapTarget(title + " theme", "Theme" + title); tap.Clicked += (_, _) => selected();
        Content = new Grid { Children = { border, badge, tap } };
        AutomationProperties.SetIsInAccessibleTree(icon, false); AutomationProperties.SetIsInAccessibleTree(label, false);
    }
    public void Update(bool selected, Color accent)
    {
        badge.IsVisible = selected;
        check.TextColor = AppearanceColors.Luminance(accent) > .18 ? Colors.Black : Colors.White;
        if (selected)
        {
            border.Stroke = accent; border.Background = new SolidColorBrush(accent.WithAlpha(.13f));
            border.Shadow = new Shadow { Brush = new SolidColorBrush(accent), Opacity = .23f, Radius = 10, Offset = new Point(0, 2) };
        }
        else
        {
            SettingsControls.SurfaceStroke(border);
            border.ClearValue(VisualElement.BackgroundProperty);
            border.SetAppThemeColor(BackgroundColorProperty, Colors.White.WithAlpha(.3f), Colors.White.WithAlpha(.065f));
            border.ClearValue(VisualElement.ShadowProperty);
        }
        SemanticProperties.SetDescription(tap, $"{title} theme{(selected ? ", selected" : "")}");
    }
}

internal sealed class ColourChoice : ContentView
{
    private readonly Border outline;
    private readonly Label check;
    private readonly Button tap;
    private readonly string title;
    public ColourChoice(string title, Brush fill, string automationId, Action selected, bool custom = false)
    {
        this.title = title;
        WidthRequest = MinimumWidthRequest = 44; VerticalOptions = LayoutOptions.Start;
        check = new Label { Text = "✓", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, FontAutoScalingEnabled = false, InputTransparent = true };
        var circle = new Border { Content = check, Background = fill, Stroke = Colors.White.WithAlpha(.5f), StrokeThickness = 1,
            StrokeShape = new Ellipse(), HeightRequest = 36, WidthRequest = 36, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        outline = new Border { Content = circle, StrokeThickness = 1.5, StrokeShape = new Ellipse(), Padding = 2, WidthRequest = 44, HeightRequest = 44, Stroke = Colors.Transparent };
        tap = SettingsControls.TapTarget(title + " colour", automationId); tap.Clicked += (_, _) => selected();
        var target = new Grid { WidthRequest = 44, HeightRequest = 44, HorizontalOptions = LayoutOptions.Center, Children = { outline, tap } };
        var column = new VerticalStackLayout { Spacing = 1, Children = { target } };
        if (custom) { var label = SettingsControls.Text("Custom Colour", 9, true); label.HorizontalTextAlignment = TextAlignment.Center; label.MaxLines = 2; column.Children.Add(label); }
        Content = column;
    }
    public void Update(bool selected, Color accent)
    {
        outline.Stroke = selected ? accent : Colors.Transparent;
        check.IsVisible = selected;
        check.TextColor = AppearanceColors.Luminance(accent) > .18 ? Colors.Black : Colors.White;
        SemanticProperties.SetDescription(tap, $"{title} colour{(selected ? ", selected" : "")}");
    }
}
