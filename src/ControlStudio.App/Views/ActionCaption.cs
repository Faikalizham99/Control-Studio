namespace ControlStudio.App.Views;

/// <summary>Shared alignment for button text and fixed-size vector icons.</summary>
internal static class ActionCaption
{
    public static HorizontalStackLayout Create(Label? label, View? icon, double spacing)
    {
        var row = new HorizontalStackLayout { Spacing = spacing, HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center, InputTransparent = true };
        if (icon is not null)
        {
            // Fill stretches the drawing surface when the adjacent label grows.
            icon.HorizontalOptions = icon.VerticalOptions = LayoutOptions.Center;
            icon.InputTransparent = true; row.Children.Add(icon);
        }
        if (label is not null)
        {
            label.VerticalOptions = LayoutOptions.Center;
            label.VerticalTextAlignment = TextAlignment.Center;
            label.InputTransparent = true;
            AutomationProperties.SetIsInAccessibleTree(label, false);
            row.Children.Add(label);
        }
        return row;
    }
}
