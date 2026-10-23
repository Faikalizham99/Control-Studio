using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

/// <summary>Readable secondary actions: an explicit disabled palette, never an opacity fade.</summary>
internal sealed class SecondaryActionSurface : Border
{
    private static readonly BindableProperty ActiveFillProperty = BindableProperty.Create("ActiveFill", typeof(Color), typeof(SecondaryActionSurface), Color.FromArgb("#F2F2F7"));
    private static readonly BindableProperty InactiveFillProperty = BindableProperty.Create("InactiveFill", typeof(Color), typeof(SecondaryActionSurface), Color.FromArgb("#E8ECF2"));
    private static readonly BindableProperty ActiveStrokeProperty = BindableProperty.Create("ActiveStroke", typeof(Color), typeof(SecondaryActionSurface), Color.FromArgb("#B7BFCD"));
    private static readonly BindableProperty InactiveStrokeProperty = BindableProperty.Create("InactiveStroke", typeof(Color), typeof(SecondaryActionSurface), Color.FromArgb("#7A8496"));
    private static readonly BindableProperty ActiveCaptionProperty = BindableProperty.Create("ActiveCaption", typeof(Color), typeof(SecondaryActionSurface), Colors.Black);
    private static readonly BindableProperty InactiveCaptionProperty = BindableProperty.Create("InactiveCaption", typeof(Color), typeof(SecondaryActionSurface), Color.FromArgb("#515A6B"));
    private static readonly BindableProperty NeutralCaptionProperty = BindableProperty.Create("NeutralCaption", typeof(Color), typeof(SecondaryActionSurface), Color.FromArgb("#252A34"));
    private static readonly BindablePropertyKey ForegroundPropertyKey = BindableProperty.CreateReadOnly(nameof(Foreground), typeof(Color), typeof(SecondaryActionSurface), Colors.Black);
    public static readonly BindableProperty ForegroundProperty = ForegroundPropertyKey.BindableProperty;
    public Color Foreground => (Color)GetValue(ForegroundProperty);
    private static readonly BindablePropertyKey NeutralForegroundPropertyKey = BindableProperty.CreateReadOnly(nameof(NeutralForeground), typeof(Color), typeof(SecondaryActionSurface), Color.FromArgb("#252A34"));
    public static readonly BindableProperty NeutralForegroundProperty = NeutralForegroundPropertyKey.BindableProperty;
    public Color NeutralForeground => (Color)GetValue(NeutralForegroundProperty);

    public SecondaryActionSurface(View content, double radius = 22, Thickness? padding = null, bool destructive = false)
    {
        Content = content; StrokeShape = new RoundRectangle { CornerRadius = radius }; StrokeThickness = 1;
        Padding = padding ?? new Thickness(12, 0); InputTransparent = true;
        this.SetAppThemeColor(ActiveFillProperty, Color.FromArgb("#F2F2F7"), Color.FromArgb("#1C1E27"));
        this.SetAppThemeColor(InactiveFillProperty, Color.FromArgb("#E8ECF2"), Color.FromArgb("#292D36"));
        this.SetAppThemeColor(ActiveStrokeProperty, Color.FromArgb("#B7BFCD"), Color.FromArgb("#697486"));
        this.SetAppThemeColor(InactiveStrokeProperty, Color.FromArgb("#7A8496"), Color.FromArgb("#7F8999"));
        this.SetAppThemeColor(InactiveCaptionProperty, Color.FromArgb("#515A6B"), Color.FromArgb("#BCC4D2"));
        this.SetAppThemeColor(NeutralCaptionProperty, Color.FromArgb("#252A34"), Color.FromArgb("#F5F5F7"));
        if (destructive)
        {
            this.SetAppThemeColor(ActiveFillProperty, Color.FromArgb("#C22D42"), Color.FromArgb("#B6243B"));
            this.SetAppThemeColor(ActiveStrokeProperty, Color.FromArgb("#A51D31"), Color.FromArgb("#FF8597"));
            this.SetAppThemeColor(InactiveFillProperty, Color.FromArgb("#F2E2E6"), Color.FromArgb("#35282E"));
            this.SetAppThemeColor(InactiveStrokeProperty, Color.FromArgb("#A77581"), Color.FromArgb("#A87D89"));
            this.SetAppThemeColor(InactiveCaptionProperty, Color.FromArgb("#80515B"), Color.FromArgb("#CCA8B2"));
            SetValue(ActiveCaptionProperty, Colors.White); SetValue(NeutralCaptionProperty, Colors.White);
        }
        else SetDynamicResource(ActiveCaptionProperty, "AccentText");
        UpdatePalette();
    }

    public void BindForeground(Element target, BindableProperty property, bool accentWhenEnabled = true)
    {
        target.RemoveBinding(property); target.RemoveDynamicResource(property);
        target.SetBinding(property, new Binding(accentWhenEnabled ? nameof(Foreground) : nameof(NeutralForeground), source: this));
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsEnabled) || propertyName is "ActiveFill" or "InactiveFill" or "ActiveStroke" or "InactiveStroke" or "ActiveCaption" or "InactiveCaption" or "NeutralCaption") UpdatePalette();
    }

    private void UpdatePalette()
    {
        Background = new SolidColorBrush((Color)GetValue(IsEnabled ? ActiveFillProperty : InactiveFillProperty));
        Stroke = new SolidColorBrush((Color)GetValue(IsEnabled ? ActiveStrokeProperty : InactiveStrokeProperty));
        SetValue(ForegroundPropertyKey, (Color)GetValue(IsEnabled ? ActiveCaptionProperty : InactiveCaptionProperty));
        SetValue(NeutralForegroundPropertyKey, (Color)GetValue(IsEnabled ? NeutralCaptionProperty : InactiveCaptionProperty));
        Scale = 1;
    }
}
