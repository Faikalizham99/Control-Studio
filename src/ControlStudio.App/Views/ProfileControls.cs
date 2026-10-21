using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace ControlStudio.App.Views;

internal enum ProfileSymbol { Back, Photo, Trash, Check, Info, Clear }

internal sealed class ProfileGlyph : GraphicsView, IDrawable
{
    public static readonly BindableProperty TintProperty = BindableProperty.Create(nameof(Tint), typeof(Color), typeof(ProfileGlyph), Colors.Black,
        propertyChanged: (view, _, _) => ((ProfileGlyph)view).Invalidate());
    private readonly ProfileSymbol symbol;
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }

    public ProfileGlyph(ProfileSymbol symbol, string? resource = null)
    {
        this.symbol = symbol; Drawable = this;
        WidthRequest = HeightRequest = 22; InputTransparent = true;
        if (resource is not null) SetDynamicResource(TintProperty, resource);
        AutomationProperties.SetIsInAccessibleTree(this, false);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState(); canvas.Scale((float)Width / 24, (float)Height / 24);
        canvas.StrokeColor = Tint; canvas.FillColor = Tint;
        canvas.StrokeSize = 1.8f; canvas.StrokeLineCap = LineCap.Round; canvas.StrokeLineJoin = LineJoin.Round;
        switch (symbol)
        {
            case ProfileSymbol.Back: Lines(canvas, 15, 4, 7, 12, 15, 20); break;
            case ProfileSymbol.Photo:
                canvas.DrawRoundedRectangle(3, 4, 18, 16, 2);
                canvas.FillCircle(8, 9, 1.7f); Lines(canvas, 4, 17, 10, 11, 13, 14, 16, 11, 21, 16); break;
            case ProfileSymbol.Trash:
                canvas.DrawLine(4, 6, 20, 6); Lines(canvas, 9, 6, 9, 3, 15, 3, 15, 6);
                Lines(canvas, 6, 6, 7, 21, 17, 21, 18, 6); canvas.DrawLine(10, 10, 10.5f, 17); canvas.DrawLine(14, 10, 13.5f, 17); break;
            case ProfileSymbol.Check:
                canvas.DrawCircle(12, 12, 9); Lines(canvas, 7.5f, 12, 10.5f, 15, 16.5f, 8.5f); break;
            case ProfileSymbol.Info:
                canvas.DrawCircle(12, 12, 9); canvas.FillCircle(12, 7, 1); Lines(canvas, 11, 11, 12, 11, 12, 17); canvas.DrawLine(10, 17, 14, 17); break;
            case ProfileSymbol.Clear:
                canvas.FillCircle(12, 12, 8.5f); canvas.StrokeColor = Colors.White; canvas.StrokeSize = 1.5f;
                canvas.DrawLine(9, 9, 15, 15); canvas.DrawLine(15, 9, 9, 15); break;
        }
        canvas.RestoreState();
    }

    private static void Lines(ICanvas canvas, params float[] points)
    {
        var path = new PathF(); path.MoveTo(points[0], points[1]);
        for (var i = 2; i < points.Length; i += 2) path.LineTo(points[i], points[i + 1]);
        canvas.DrawPath(path);
    }
}

internal sealed class ProfileActionButton : ContentView
{
    public event EventHandler? Clicked;

    public ProfileActionButton(string text, ProfileSymbol symbol, string automationId, bool primary = true, double radius = 18)
    {
        MinimumHeightRequest = 48;
        var glyph = new ProfileGlyph(symbol, primary ? "AccentButtonText" : "AccentText");
        var label = SettingsControls.Text(text, 14, bold: primary);
        label.SetDynamicResource(Label.TextColorProperty, primary ? "AccentButtonText" : "AccentText");
        AutomationProperties.SetIsInAccessibleTree(label, false);
        var caption = new HorizontalStackLayout { Spacing = 10, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, InputTransparent = true, Children = { glyph, label } };
        var content = new Grid();
        if (primary)
            content.Background = new LinearGradientBrush(new GradientStopCollection
            {
                new(Colors.White.WithAlpha(.25f), 0), new(Colors.Transparent, .4f), new(Colors.Black.WithAlpha(.16f), 1)
            }, new Point(0, 0), new Point(0, 1));
        content.Add(caption);
        var hit = new Button { Text = "", Padding = 0, CornerRadius = (int)radius, BackgroundColor = Colors.Transparent, BorderWidth = 0, MinimumHeightRequest = 48, AutomationId = automationId };
        SemanticProperties.SetDescription(hit, text);
        hit.Clicked += (_, args) => Clicked?.Invoke(this, args);
        content.Add(hit);
        var surface = SettingsControls.Rounded(content, radius);
        if (primary)
        {
            surface.SetDynamicResource(BackgroundColorProperty, "AccentButton");
            surface.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(Colors.White.WithAlpha(.8f)), new SolidColorBrush(Colors.White.WithAlpha(.35f)));
        }
        hit.Pressed += (_, _) => surface.Opacity = .8;
        hit.Released += (_, _) => surface.Opacity = 1;
        Content = surface;
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled)) Opacity = IsEnabled ? 1 : .45;
    }
}

public sealed class ProfileNameEntry : Entry;
