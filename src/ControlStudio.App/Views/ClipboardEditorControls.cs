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
        var tile = Surface(icon, 14, 11); tile.WidthRequest = tile.HeightRequest = 46; tile.VerticalOptions = LayoutOptions.Center;
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
        back.VerticalOptions = trailing.VerticalOptions = LayoutOptions.Center;
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
    private readonly Border surface;
    public event EventHandler? Clicked;

    public ClipboardEditorActionButton(string text, View? icon, string automationId, bool primary = false, bool accentCaption = false, bool readableDisabled = false, bool compact = false)
    {
        var height = primary && !compact ? 52 : 44;
        MinimumHeightRequest = height;
        Label? label = null;
        if (text.Length > 0)
        {
            label = new Label { Text = text, FontSize = primary ? compact ? 14 : 19 : 12, FontAttributes = primary || accentCaption ? FontAttributes.Bold : FontAttributes.None };
            if (accentCaption && !primary) label.SetDynamicResource(Label.TextColorProperty, "AccentText");
            else if (!primary) label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#252A34"), Color.FromArgb("#F5F5F7"));
        }
        var caption = ActionCaption.Create(label, icon, 8);
        var padding = new Thickness(text.Length == 0 ? 10 : compact ? 8 : 12, 0);
        if (primary)
        {
            var accent = new AccentSurface(caption, compact ? 20 : 25, padding, captionSize: compact ? 14 : 16, readableDisabled: readableDisabled);
            if (label is not null) accent.BindForeground(label, Label.TextColorProperty);
            if (icon is ProfileGlyph profile) accent.BindForeground(profile, ProfileGlyph.TintProperty);
            if (icon is StudioGlyph studio) accent.BindForeground(studio, StudioGlyph.TintProperty);
            surface = accent;
        }
        else
        {
            var secondary = new SecondaryActionSurface(caption, 22, padding);
            if (label is not null) secondary.BindForeground(label, Label.TextColorProperty, accentCaption);
            if (icon is ProfileGlyph profile) secondary.BindForeground(profile, ProfileGlyph.TintProperty);
            if (icon is StudioGlyph studio) secondary.BindForeground(studio, StudioGlyph.TintProperty);
            surface = secondary;
        }
        surface.InputTransparent = true;
        if (primary)
        {
            surface.Shadow = new Shadow { Opacity = .45f, Radius = 14, Offset = new Point(0, 3) };
            surface.Shadow.SetDynamicResource(Shadow.BrushProperty, "AccentGlow");
        }
        tap = SettingsControls.TapTarget(text, automationId); tap.MinimumHeightRequest = height; tap.CornerRadius = primary ? 25 : 22;
        tap.Clicked += (_, args) => { if (IsEnabled) Clicked?.Invoke(this, args); };
        tap.Pressed += (_, _) => { if (IsEnabled) surface.Scale = .97; }; tap.Released += (_, _) => surface.Scale = 1;
        Content = new Grid { MinimumHeightRequest = height, Children = { tap, surface } };
    }

    public void Describe(string description) => SemanticProperties.SetDescription(tap, description);

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled)) { Opacity = 1; if (surface is not null) surface.Scale = 1; }
    }
}
