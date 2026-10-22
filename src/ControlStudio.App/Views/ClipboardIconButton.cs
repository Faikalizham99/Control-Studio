using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

/// <summary>A native button hit target with an opaque, independently drawn icon surface.</summary>
internal sealed class ClipboardIconButton : ContentView
{
    private readonly Button tap;
    public event EventHandler? Clicked;

    public ClipboardIconButton(StudioSymbol symbol, string action, bool primary = false, bool destructive = false)
    {
        WidthRequest = HeightRequest = 48; MinimumWidthRequest = MinimumHeightRequest = 48;
        var icon = new StudioGlyph(symbol, useAccent: false)
        {
            WidthRequest = HeightRequest = 23, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center
        };
        Border surface;
        if (primary)
        {
            var accent = new AccentSurface(icon, 15, captionSize: 14, captionBold: false);
            accent.Stroke = Colors.Transparent;
            accent.BindForeground(icon, StudioGlyph.TintProperty); surface = accent;
        }
        else
        {
            surface = new Border { Content = icon, StrokeShape = new RoundRectangle { CornerRadius = 15 }, StrokeThickness = 1 };
            SettingsControls.SurfaceStroke(surface);
            surface.SetAppTheme<Brush>(BackgroundProperty, new SolidColorBrush(Color.FromArgb("#F2F4F9")), new SolidColorBrush(Color.FromArgb("#33353C")));
            if (destructive) icon.SetAppThemeColor(StudioGlyph.TintProperty, Color.FromArgb("#B42336"), Color.FromArgb("#FF8798"));
            else icon.SetDynamicResource(StudioGlyph.TintProperty, "AccentText");
        }
        surface.InputTransparent = true;
        tap = SettingsControls.TapTarget(action, "Clipboard" + action.Replace(" ", ""));
        tap.CornerRadius = 15;
        tap.Clicked += (_, e) => Clicked?.Invoke(this, e);
        tap.Pressed += (_, _) => surface.Scale = .95;
        tap.Released += (_, _) => surface.Scale = 1;
        // Keep the native hit target beneath the visuals so its UIKit states cannot cover the fill/glyph.
        Content = new Grid { Children = { tap, surface } };
    }

    public void Describe(string action, string name)
    {
        SemanticProperties.SetDescription(tap, $"{action} {name}");
        SemanticProperties.SetHint(tap, action == "Delete" ? "Asks for confirmation before deleting." : "");
    }
}
