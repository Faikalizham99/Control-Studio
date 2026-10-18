namespace ControlStudio.App.Views;

public sealed class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        Title = "Settings";
        var appearance = new Picker { Title = "Appearance", ItemsSource = new[] { "System", "Light", "Dark" } };
        appearance.SelectedIndex = Preferences.Default.Get("appearance", 0);
        if (appearance.SelectedIndex is < 0 or > 2) appearance.SelectedIndex = 0;
        ApplyAppearance(appearance.SelectedIndex);
        appearance.SelectedIndexChanged += (_, _) =>
        {
            Preferences.Default.Set("appearance", appearance.SelectedIndex);
            ApplyAppearance(appearance.SelectedIndex);
        };
        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Padding = 24, Spacing = 20, Children =
            {
                new Label { Text = "Make yourself at home", FontSize = 28, FontAttributes = FontAttributes.Bold }, appearance,
                new Label { Text = "Everything stays local", FontSize = 20, FontAttributes = FontAttributes.Bold },
                new Label { Text = "No account, backend or analytics. The counter is stored in SQLite. With valid App Group provisioning, the app, widget and Shortcuts use the same database." },
                new Label { Text = "Widget & Shortcuts setup", FontSize = 20, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Open the app once, then add Control Studio from the Home Screen widget picker. In Shortcuts, search for Increment Control Studio Counter. Both extensions must be preserved and signed with matching App Group permissions." },
                new Label { Text = "If shared storage is unavailable, the app uses a separate local counter. Local values are not automatically merged into the shared counter when signing changes." },
                new Label { Text = "Control Studio · Milestone 0 · 0.1.0", FontSize = 13 }
            }
        }};
    }

    private static void ApplyAppearance(int index)
    {
        if (Application.Current is { } app)
            app.UserAppTheme = index switch { 1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.Unspecified };
    }
}
