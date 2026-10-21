using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

internal static class ClipboardEditorControls
{
    public static Border Card(View content) => Surface(content, 20, 14);

    public static Border Surface(View content, double radius, Thickness padding)
    {
        var surface = new Border { Content = content, Padding = padding, StrokeShape = new RoundRectangle { CornerRadius = radius }, StrokeThickness = 1 };
        surface.SetAppTheme<Brush>(Border.BackgroundProperty,
            Gradient("#FFFFFF", "#EFF1F6"), Gradient("#24292C", "#111416"));
        surface.SetAppTheme<Brush>(Border.StrokeProperty,
            Gradient("#CBD2DE", "#E0E4EB"), Gradient("#596064", "#303739"));
        return surface;
    }

    public static Border Field(View content, Thickness padding)
    {
        var field = new Border { Content = content, Padding = padding, StrokeShape = new RoundRectangle { CornerRadius = 13 }, StrokeThickness = 1 };
        field.SetAppThemeColor(VisualElement.BackgroundColorProperty, Color.FromArgb("#F0F2F7"), Colors.White.WithAlpha(.055f));
        field.SetAppTheme<Brush>(Border.StrokeProperty,
            new SolidColorBrush(Color.FromArgb("#D2D8E2")), new SolidColorBrush(Colors.White.WithAlpha(.12f)));
        return field;
    }

    public static View Heading(string title, string subtitle, View icon)
    {
        var tile = Surface(icon, 14, 11); tile.WidthRequest = tile.HeightRequest = 46;
        var labels = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center, Children =
        {
            SettingsControls.Text(title, 15, bold: true), SettingsControls.Text(subtitle, 12, secondary: true)
        } };
        var row = new Grid { ColumnSpacing = 13, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        row.Add(tile); row.Add(labels, 1); return row;
    }

    public static StudioGlyph Icon(StudioSymbol symbol, string resource = "AccentText", double size = 20)
    {
        var icon = new StudioGlyph(symbol, useAccent: false) { WidthRequest = size, HeightRequest = size, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        icon.SetDynamicResource(StudioGlyph.TintProperty, resource); return icon;
    }

    public static View Header(string title, ClipboardEditorActionButton back, ClipboardEditorActionButton trailing)
    {
        var heading = SettingsControls.Text(title, 17, bold: true);
        heading.HorizontalTextAlignment = TextAlignment.Center; heading.VerticalOptions = LayoutOptions.Center;
        back.HorizontalOptions = LayoutOptions.Start; trailing.HorizontalOptions = LayoutOptions.End;
        var row = new Grid { Padding = new Thickness(16, 5), ColumnSpacing = 8,
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Star) } };
        row.Add(back); row.Add(heading, 1); row.Add(trailing, 2); return row;
    }

    private static LinearGradientBrush Gradient(string start, string end) => new(new GradientStopCollection
    {
        new(Color.FromArgb(start), 0), new(Color.FromArgb(end), 1)
    }, new Point(0, 0), new Point(1, 1));
}

internal sealed class ClipboardEditorActionButton : ContentView
{
    private readonly Button tap;
    public event EventHandler? Clicked;

    public ClipboardEditorActionButton(string text, View? icon, string automationId, bool primary = false, bool accentCaption = false)
    {
        var height = primary ? 52 : 44;
        MinimumHeightRequest = height;
        var caption = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, InputTransparent = true };
        if (icon is not null) { icon.InputTransparent = true; caption.Children.Add(icon); }
        if (text.Length > 0)
        {
            var label = new Label { Text = text, FontSize = primary ? 19 : 12, FontAttributes = primary || accentCaption ? FontAttributes.Bold : FontAttributes.None };
            if (primary || accentCaption) label.SetDynamicResource(Label.TextColorProperty, primary ? "AccentActionText" : "AccentText");
            else label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#252A34"), Color.FromArgb("#F5F5F7"));
            AutomationProperties.SetIsInAccessibleTree(label, false); caption.Children.Add(label);
        }
        var surface = ClipboardEditorControls.Surface(caption, primary ? 25 : 22, new Thickness(text.Length == 0 ? 10 : 12, 0));
        surface.InputTransparent = true;
        if (primary)
        {
            surface.RemoveBinding(Border.BackgroundProperty);
            surface.ClearValue(Border.BackgroundProperty);
            surface.SetDynamicResource(BackgroundColorProperty, "AccentButton");
            surface.RemoveBinding(Border.StrokeProperty);
            surface.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(Colors.White.WithAlpha(.8f)), new SolidColorBrush(Colors.White.WithAlpha(.55f)));
            surface.Shadow = new Shadow { Opacity = .45f, Radius = 14, Offset = new Point(0, 3) };
            surface.Shadow.SetDynamicResource(Shadow.BrushProperty, "AccentGlow");
        }
        tap = SettingsControls.TapTarget(text, automationId); tap.MinimumHeightRequest = height; tap.CornerRadius = primary ? 25 : 22;
        tap.Clicked += (_, args) => Clicked?.Invoke(this, args);
        tap.Pressed += (_, _) => surface.Scale = .97; tap.Released += (_, _) => surface.Scale = 1;
        Content = new Grid { MinimumHeightRequest = height, Children = { tap, surface } };
    }

    public void Describe(string description) => SemanticProperties.SetDescription(tap, description);

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled)) Opacity = IsEnabled ? 1 : .6;
    }
}
