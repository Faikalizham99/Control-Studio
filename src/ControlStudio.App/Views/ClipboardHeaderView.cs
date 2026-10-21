namespace ControlStudio.App.Views;

/// <summary>Shared card header and editor preview; null overrides follow the live app accent.</summary>
internal sealed class ClipboardHeaderView : ContentView
{
    private readonly Label title = new() { FontSize = 19, FontAttributes = FontAttributes.Bold, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation };

    public ClipboardHeaderView()
    {
        Padding = new Thickness(12, 14); Content = title; SetItem("Item title", null);
    }

    public void SetItem(string name, string? colourOverride)
    {
        title.Text = name;
        RemoveDynamicResource(BackgroundColorProperty); title.RemoveDynamicResource(Label.TextColorProperty);
        if (colourOverride is null)
        {
            SetDynamicResource(BackgroundColorProperty, "Accent");
            title.SetDynamicResource(Label.TextColorProperty, "AccentActionText");
        }
        else
        {
            var colour = Color.FromArgb(colourOverride);
            BackgroundColor = colour; title.TextColor = AppearanceColors.ButtonForeground(colour, 19, bold: true);
        }
    }
}
