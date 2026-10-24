using System.ComponentModel;
using ControlStudio.App.Platforms.iOS;
using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

public sealed class SettingsPage : ContentPage
{
    private readonly AppearancePreferences preferences;
    private readonly ProfilePhotoStore photos;
    private readonly List<ThemeChoice> themes = [];
    private readonly ColourPickerView colours;
    private readonly Label displayName = SettingsControls.Text("", 19, bold: true);
    private readonly Label initial = new() { FontSize = 27, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
    private readonly Image portrait = new() { Aspect = Aspect.AspectFill, HeightRequest = 60, WidthRequest = 60 };
    private readonly Label previewDetail = SettingsControls.Text("Accent colour in action", 10, secondary: true);
    private bool picking;
    private bool subscribed;
    private string? displayedPhotoPath;

    public SettingsPage(AppearancePreferences preferences, AppleColorPicker picker, ProfilePhotoStore photos)
    {
        this.preferences = preferences; this.photos = photos;
        colours = new ColourPickerView(picker);
        colours.ColourChanged += (_, args) => preferences.SetAccent(args.Hex, args.Persist);
        colours.CustomEditingChanged += (_, args) => preferences.SetCustomColourEnabled(args.Value);
        Title = "Settings"; AutomationId = "SettingsPage"; SafeAreaEdges = SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(this, false);
        DashboardStyle.NavigationAccent(this);
        initial.SetDynamicResource(Label.TextColorProperty, "AccentButtonText");
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F2F2F7"), Colors.Black);
        var body = new VerticalStackLayout { Padding = new Thickness(16, 8, 16, 24), Spacing = 12 };
        body.Children.Add(SettingsControls.Text("Settings", 34, bold: true));
        body.Children.Add(BuildProfile()); body.Children.Add(BuildThemes());
        body.Children.Add(Section("Accent", colours)); body.Children.Add(BuildPreview());
        var about = new Button { Text = "Widget & Shortcuts setup  ›", BackgroundColor = Colors.Transparent, FontSize = 14, MinimumHeightRequest = 44 };
        about.SetDynamicResource(Button.TextColorProperty, "AccentText");
        about.Clicked += async (_, _) => await Navigation.PushAsync(new AboutPage());
        body.Children.Add(about);
        Content = new ScrollView { Content = body };
        Refresh();
    }

    private GlassCard BuildProfile()
    {
        var avatar = new Grid { WidthRequest = 60, HeightRequest = 60, Children = { initial, portrait } };
        avatar.SetDynamicResource(BackgroundColorProperty, "Accent");
        var photo = new Border { Content = avatar, Stroke = Colors.White, StrokeThickness = 3, StrokeShape = new Ellipse(), VerticalOptions = LayoutOptions.Center };
        var names = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center, Children = { displayName, SettingsControls.Text("Edit profile", 13, true) } };
        var chevron = new Button { Text = "›", FontSize = 26, CornerRadius = 22, WidthRequest = 44, HeightRequest = 44, Padding = 0, BackgroundColor = Colors.White.WithAlpha(.3f), VerticalOptions = LayoutOptions.Center };
        chevron.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#767A86"), Color.FromArgb("#C4C8D2"));
        SemanticProperties.SetDescription(chevron, "Edit profile"); chevron.Clicked += async (_, _) => await OpenProfileAsync();
        var row = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        row.Add(photo); row.Add(names, 1); row.Add(chevron, 2);
        var tap = new TapGestureRecognizer(); tap.Tapped += async (_, _) => await OpenProfileAsync(); row.GestureRecognizers.Add(tap);
        return Section("Profile", SettingsControls.Rounded(row, 20, 9));
    }

    private GlassCard BuildThemes()
    {
        var row = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) } };
        var titles = new[] { "System", "Light", "Dark" };
        for (var i = 0; i < titles.Length; i++)
        {
            var index = i;
            var choice = new ThemeChoice(titles[i], $"theme_{titles[i].ToLowerInvariant()}.png", () => preferences.SetTheme(index));
            themes.Add(choice); row.Add(choice, i);
        }
        return Section("Theme", row);
    }

    private GlassCard BuildPreview()
    {
        var primary = DashboardStyle.Button("Primary Button"); primary.FontSize = 12; primary.MinimumHeightRequest = 44; primary.Padding = new Thickness(10, 3);
        primary.Shadow = new Shadow { Opacity = .4f, Radius = 10, Offset = new Point(0, 2) }; primary.Shadow.SetDynamicResource(Shadow.BrushProperty, "AccentGlow");
        primary.Clicked += (_, _) => { previewDetail.Text = "Button tapped ✓"; SemanticScreenReader.Default.Announce("Live preview button tapped"); };
        var toggle = SettingsControls.RowSwitch(); toggle.IsToggled = true; toggle.HorizontalOptions = LayoutOptions.Center;
        SemanticProperties.SetDescription(toggle, "Live preview switch");
        var star = new Label { Text = "☆", FontSize = 23, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        var starWell = new AccentSurface(star, 10, captionSize: 23, captionBold: false) { WidthRequest = 32, HeightRequest = 34, VerticalOptions = LayoutOptions.Center };
        starWell.BindForeground(star, Label.TextColorProperty);
        var card = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        card.Add(starWell); card.Add(new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { SettingsControls.Text("Live Preview", 12), previewDetail } }, 1);
        var mini = SettingsControls.Rounded(card, 14, new Thickness(8));
        toggle.Toggled += (_, args) => { mini.Opacity = args.Value ? 1 : .45; previewDetail.Text = args.Value ? "Accent colour in action" : "Preview paused"; };
        var row = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(new GridLength(1.2, GridUnitType.Star)), new(GridLength.Auto), new(new GridLength(1.4, GridUnitType.Star)) } };
        row.Add(primary); row.Add(toggle, 1); row.Add(mini, 2);
        var slider = new Slider { Value = .55, Minimum = 0, Maximum = 1, MaximumTrackColor = Color.FromArgb("#B5B9C2"), ThumbColor = Colors.White, HeightRequest = 28 };
        slider.SetDynamicResource(Slider.MinimumTrackColorProperty, "Accent"); SemanticProperties.SetDescription(slider, "Live preview slider");
        return Section("Live Preview", new VerticalStackLayout { Spacing = 9, Children = { row, slider } });
    }

    private static GlassCard Section(string title, View content) => new(new VerticalStackLayout { Spacing = 8, Children = { SettingsControls.Text(title, 13, true, true), content } });
    private async Task OpenProfileAsync() { if (picking || colours.IsBusy) return; picking = true; try { await Navigation.PushAsync(new ProfileEditorPage(preferences, photos)); } finally { picking = false; } }
    private void Changed(object? sender, PropertyChangedEventArgs args) => Refresh();
    private void Refresh()
    {
        var accent = Color.FromArgb(preferences.AccentHex);
        for (var i = 0; i < themes.Count; i++) themes[i].Update(i == preferences.ThemeIndex, accent);
        colours.SetSelection(preferences.AccentHex, preferences.CustomColourEnabled);
        displayName.Text = preferences.DisplayName;
        initial.Text = System.Globalization.StringInfo.GetNextTextElement(preferences.DisplayName);
        var path = photos.GetPath(preferences.PhotoKey);
        if (path != displayedPhotoPath) { portrait.Source = path; displayedPhotoPath = path; }
        portrait.IsVisible = path is not null;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!subscribed) { preferences.PropertyChanged += Changed; subscribed = true; }
        Refresh();
    }
    protected override void OnDisappearing()
    {
        if (subscribed) { preferences.PropertyChanged -= Changed; subscribed = false; }
        colours.CommitPendingHex(); preferences.SaveAccent(); base.OnDisappearing();
    }
}
