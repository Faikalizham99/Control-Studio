using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

/// <summary>Opaque primary-action fill. Never start with a themed card/field background.</summary>
internal sealed class AccentSurface : Border
{
    private static readonly BindablePropertyKey ForegroundPropertyKey = BindableProperty.CreateReadOnly(
        nameof(Foreground), typeof(Color), typeof(AccentSurface), Colors.White);
    public static readonly BindableProperty ForegroundProperty = ForegroundPropertyKey.BindableProperty;
    private readonly double captionSize;
    private readonly bool captionBold;
    private readonly bool readableDisabled;
    public Color Foreground => (Color)GetValue(ForegroundProperty);

    public AccentSurface(View content, double radius, Thickness? padding = null, double captionSize = 19, bool captionBold = true, bool readableDisabled = false)
    {
        this.captionSize = captionSize; this.captionBold = captionBold; this.readableDisabled = readableDisabled;
        Content = content; Padding = padding ?? new Thickness(0);
        StrokeShape = new RoundRectangle { CornerRadius = radius }; StrokeThickness = 1;
        SetDynamicResource(BackgroundProperty, "AccentButtonBrush");
        this.SetAppTheme<Brush>(StrokeProperty, new SolidColorBrush(Colors.White.WithAlpha(.8f)), new SolidColorBrush(Colors.White.WithAlpha(.35f)));
    }

    public void BindForeground(Element target, BindableProperty property)
    {
        target.RemoveBinding(property); target.RemoveDynamicResource(property);
        target.SetBinding(property, new Binding(nameof(Foreground), source: this));
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled) && readableDisabled)
        {
            RemoveDynamicResource(BackgroundProperty); ClearValue(BackgroundProperty);
            if (IsEnabled) SetDynamicResource(BackgroundProperty, "AccentButtonBrush");
            else this.SetAppTheme<Brush>(BackgroundProperty, new SolidColorBrush(Color.FromArgb("#E8ECF2")), new SolidColorBrush(Color.FromArgb("#292D36")));
        }
        if (propertyName == nameof(Background) && ((IView)this).Background is SolidPaint { Color.Alpha: >= .999f } paint)
            SetValue(ForegroundPropertyKey, AppearanceColors.ButtonForeground(paint.Color, captionSize, captionBold));
    }
}
