namespace ControlStudio.App.Views;

internal sealed class PageLoadIndicator : ContentView
{
    private readonly ActivityIndicator spinner = new() { WidthRequest = 20, HeightRequest = 20, VerticalOptions = LayoutOptions.Center };
    public PageLoadIndicator(string caption, string id)
    {
        AutomationId = id; IsVisible = false; spinner.SetDynamicResource(ActivityIndicator.ColorProperty, "AccentText");
        var label = SettingsControls.Text(caption, 13, secondary: true); label.VerticalTextAlignment = TextAlignment.Center;
        Content = new HorizontalStackLayout { Spacing = 8, Children = { spinner, label } };
    }
    public void SetActive(bool active) { spinner.IsRunning = active; IsVisible = active; }
}
