using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

internal sealed class ClipboardSegment : ContentView
{
    private readonly Border surface;
    private readonly StudioGlyph icon;
    private readonly Label label;
    private readonly Button tap;
    private readonly ClipboardKind kind;

    public ClipboardSegment(ClipboardKind kind, StudioSymbol symbol, Action select)
    {
        this.kind = kind;
        icon = new StudioGlyph(symbol, useAccent: false) { WidthRequest = 21, HeightRequest = 21, HorizontalOptions = LayoutOptions.Center };
        label = new Label { Text = kind.ToString(), FontSize = 11, HorizontalTextAlignment = TextAlignment.Center, InputTransparent = true };
        surface = new Border
        {
            Content = new VerticalStackLayout { Padding = new Thickness(2, 8), Spacing = 4, Children = { icon, label }, InputTransparent = true },
            StrokeShape = new RoundRectangle { CornerRadius = 19 }, StrokeThickness = 1, InputTransparent = true
        };
        tap = SettingsControls.TapTarget(kind.ToString(), "ClipboardTab" + kind); tap.Clicked += (_, _) => select();
        Content = new Grid { Children = { tap, surface } };
        AutomationProperties.SetIsInAccessibleTree(label, false);
    }

    public void Update(bool selected)
    {
        label.RemoveBinding(Label.TextColorProperty); icon.RemoveBinding(StudioGlyph.TintProperty);
        label.RemoveDynamicResource(Label.TextColorProperty); icon.RemoveDynamicResource(StudioGlyph.TintProperty);
        surface.RemoveDynamicResource(Border.BackgroundProperty); surface.RemoveDynamicResource(Border.StrokeProperty);
        label.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
        if (selected)
        {
            // Manual deselected values outrank dynamic resources until explicitly cleared.
            surface.ClearValue(Border.BackgroundProperty); surface.ClearValue(Border.StrokeProperty);
            label.ClearValue(Label.TextColorProperty); icon.ClearValue(StudioGlyph.TintProperty);
            surface.SetDynamicResource(Border.BackgroundProperty, "AccentSelectionBrush");
            surface.SetDynamicResource(Border.StrokeProperty, "AccentSelectionStroke");
            label.SetDynamicResource(Label.TextColorProperty, "AccentText"); icon.SetDynamicResource(StudioGlyph.TintProperty, "AccentText");
        }
        else
        {
            surface.Background = Brush.Transparent; surface.Stroke = Brush.Transparent;
            label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#626570"), Color.FromArgb("#AFB3BD"));
            icon.SetAppThemeColor(StudioGlyph.TintProperty, Color.FromArgb("#626570"), Color.FromArgb("#AFB3BD"));
        }
        SemanticProperties.SetDescription(tap, $"{kind}{(selected ? ", selected" : "")}");
    }
}
