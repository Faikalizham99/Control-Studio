namespace ControlStudio.App.Views;

/// <summary>Keep text contrast tied to this button's actual fill and label size.</summary>
internal sealed class AccentButton : Button
{
    public AccentButton()
    {
        FontSize = 19;
        FontAttributes = FontAttributes.Bold;
        CornerRadius = 14; MinimumHeightRequest = 48;
        SetDynamicResource(BackgroundColorProperty, "AccentButton");
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName is nameof(BackgroundColor) or nameof(FontSize) or nameof(FontAttributes)) UpdateForeground();
    }

    private void UpdateForeground()
    {
        if (BackgroundColor is not { Alpha: >= .999f } fill) return;
        TextColor = AppearanceColors.ButtonForeground(fill, FontSize, FontAttributes.HasFlag(FontAttributes.Bold));
    }
}
