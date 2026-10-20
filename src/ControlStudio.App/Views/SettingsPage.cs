using System.ComponentModel;
using ControlStudio.App.Platforms.iOS;
using ControlStudio.Core;
using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

public sealed class SettingsPage : ContentPage
{
    private readonly AppearancePreferences preferences;
    private readonly AppleColorPicker picker;
    private readonly ProfilePhotoStore photos;
    private readonly List<ThemeChoice> themes = [];
    private readonly List<AccentChoice> accents = [];
    private readonly Label displayName = SettingsControls.Text("", 19, bold: true);
    private readonly Label initial = new() { FontSize = 27, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
    private readonly Image portrait = new() { Aspect = Aspect.AspectFill, HeightRequest = 60, WidthRequest = 60 };
    private readonly Switch custom = new() { HorizontalOptions = LayoutOptions.End };
    private readonly Slider hue = new() { Minimum = 0, Maximum = 360, MinimumTrackColor = Colors.Transparent, MaximumTrackColor = Colors.Transparent, ThumbColor = Colors.White, HeightRequest = 30 };
    private readonly Entry hex = new() { FontSize = 14, MaxLength = 7, HorizontalTextAlignment = TextAlignment.Center, ReturnType = ReturnType.Done, TextTransform = TextTransform.Uppercase, AutomationId = "CustomAccentHex" };
    private readonly Border colourWell;
    private readonly VerticalStackLayout customEditor = new() { Spacing = 8 };
    private readonly Label error = SettingsControls.Text("", 12, secondary: true);
    private readonly Label previewDetail = SettingsControls.Text("Accent colour in action", 10, secondary: true);
    private bool updating;
    private bool draggingHue;
    private bool picking;
    private bool subscribed;
    private string? displayedPhotoPath;

    public SettingsPage(AppearancePreferences preferences, AppleColorPicker picker, ProfilePhotoStore photos)
    {
        this.preferences = preferences; this.picker = picker; this.photos = photos;
        Title = "Settings"; AutomationId = "SettingsPage"; SafeAreaEdges = SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(this, false);
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F2F2F7"), Colors.Black);
        colourWell = SettingsControls.Rounded(new Grid(), 15);
        colourWell.WidthRequest = 56; colourWell.HeightRequest = 56;
        var body = new VerticalStackLayout { Padding = new Thickness(16, 8, 16, 24), Spacing = 12 };
        body.Children.Add(SettingsControls.Text("Settings", 34, bold: true));
        body.Children.Add(BuildProfile()); body.Children.Add(BuildThemes());
        body.Children.Add(BuildAccents()); body.Children.Add(BuildPreview());
        var about = new Button { Text = "Widget & Shortcuts setup  ›", BackgroundColor = Colors.Transparent, FontSize = 14, MinimumHeightRequest = 44 };
        about.SetDynamicResource(Button.TextColorProperty, "Accent");
        about.Clicked += async (_, _) => await Navigation.PushAsync(new AboutPage());
        body.Children.Add(about);
        Content = new ScrollView { Content = body };
        Refresh();
    }

    private GlassCard BuildProfile()
    {
        var avatar = new Grid { WidthRequest = 60, HeightRequest = 60, Children = { initial, portrait } };
        avatar.SetDynamicResource(BackgroundColorProperty, "Accent");
        var photo = new Border { Content = avatar, Stroke = Colors.White, StrokeThickness = 3, StrokeShape = new Ellipse() };
        var names = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center, Children = { displayName, SettingsControls.Text("Edit profile", 13, true) } };
        var chevron = new Button { Text = "›", FontSize = 26, CornerRadius = 22, WidthRequest = 44, HeightRequest = 44, Padding = 0, BackgroundColor = Colors.White.WithAlpha(.3f) };
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

    private GlassCard BuildAccents()
    {
        var palette = new Grid { ColumnSpacing = 2, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) } };
        var names = new[] { "Blue", "Purple", "Pink", "Green", "Orange" };
        for (var i = 0; i < AppearanceValues.Presets.Count; i++)
        {
            var value = AppearanceValues.Presets[i];
            var choice = new AccentChoice(names[i], SettingsControls.Swatch(Color.FromArgb(value)), "Accent" + names[i], () => preferences.SetAccent(value));
            accents.Add(choice); palette.Add(choice, i);
        }
        var spectrum = new LinearGradientBrush(new GradientStopCollection { new(Colors.Magenta, 0), new(Colors.Cyan, .33f), new(Colors.Lime, .5f), new(Colors.Yellow, .67f), new(Colors.Orange, .83f), new(Colors.Magenta, 1) }, new Point(0, 0), new Point(1, 1));
        var customChoice = new AccentChoice("Custom colour", spectrum, "AccentCustom", async () => await PickColourAsync(), true);
        accents.Add(customChoice); palette.Add(customChoice, 5);
        custom.SetDynamicResource(Switch.OnColorProperty, "Accent");
        SemanticProperties.SetDescription(custom, "Enable custom colour editing"); custom.AutomationId = "CustomColourEnabled";
        custom.Toggled += (_, args) => { if (!updating) preferences.SetCustomColourEnabled(args.Value); };
        var heading = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var title = SettingsControls.Text("Custom Colour", 13, bold: true); title.VerticalOptions = LayoutOptions.Center;
        heading.Add(title); heading.Add(custom, 1);
        var hueBrush = new LinearGradientBrush(new GradientStopCollection {
            new(Colors.Magenta, 0), new(Colors.Blue, 1f/6), new(Colors.Cyan, 2f/6), new(Colors.Lime, 3f/6), new(Colors.Yellow, 4f/6), new(Colors.Red, 5f/6), new(Colors.Magenta, 1)
        }, new Point(0, 0), new Point(1, 0));
        var bar = new Border { Background = hueBrush, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 8 }, HeightRequest = 15, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(5, 0) };
        hue.AutomationId = "AccentHue"; SemanticProperties.SetDescription(hue, "Accent hue");
        hue.DragStarted += (_, _) => draggingHue = true;
        hue.DragCompleted += (_, _) => { draggingHue = false; preferences.SaveAccent(); Refresh(); };
        // The spectrum runs backward from magenta (300°) through blue/cyan to red.
        hue.ValueChanged += (_, args) => { if (!updating) preferences.SetAccent(Color.FromHsv((float)((300 - args.NewValue + 360) % 360) / 360, 1, 1).ToArgbHex()[3..], persist: !draggingHue); };
        customEditor.Children.Add(new Grid { Children = { bar, hue } });
        hex.Completed += (_, _) => ApplyHex(); hex.Unfocused += (_, _) => ApplyHex();
        SemanticProperties.SetDescription(hex, "Accent colour in six-digit hexadecimal");
        hex.SetAppThemeColor(Entry.TextColorProperty, Colors.Black, Colors.White);
        var hexFrame = SettingsControls.Rounded(hex, 12, new Thickness(7, 0));
        var eyedropper = new ImageButton { Source = "color_picker.png", WidthRequest = 44, HeightRequest = 44, Padding = 11, CornerRadius = 12, BackgroundColor = Colors.White.WithAlpha(.35f), AutomationId = "OpenColourPicker" };
        eyedropper.SetAppTheme(ImageButton.SourceProperty, ImageSource.FromFile("color_picker.png"), ImageSource.FromFile("color_picker_dark.png"));
        SemanticProperties.SetDescription(eyedropper, "Open system colour picker"); eyedropper.Clicked += async (_, _) => await PickColourAsync();
        var colourTap = new TapGestureRecognizer(); colourTap.Tapped += async (_, _) => await PickColourAsync(); colourWell.GestureRecognizers.Add(colourTap);
        var controls = new Grid { ColumnSpacing = 10, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        controls.Add(colourWell); controls.Add(hexFrame, 1); controls.Add(eyedropper, 2);
        customEditor.Children.Add(controls); customEditor.Children.Add(error);
        var divider = new BoxView { HeightRequest = 1, Color = Colors.White.WithAlpha(.45f), Margin = new Thickness(0, 3) };
        return Section("Accent", new VerticalStackLayout { Spacing = 6, Children = { palette, divider, heading, customEditor } });
    }

    private GlassCard BuildPreview()
    {
        var primary = DashboardStyle.Button("Primary Button"); primary.FontSize = 12; primary.MinimumHeightRequest = 44; primary.Padding = new Thickness(10, 3);
        primary.Shadow = new Shadow { Opacity = .4f, Radius = 10, Offset = new Point(0, 2) }; primary.Shadow.SetDynamicResource(Shadow.BrushProperty, "AccentGlow");
        primary.Clicked += (_, _) => { previewDetail.Text = "Button tapped ✓"; SemanticScreenReader.Default.Announce("Live preview button tapped"); };
        var toggle = new Switch { IsToggled = true, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        toggle.SetDynamicResource(Switch.OnColorProperty, "Accent"); SemanticProperties.SetDescription(toggle, "Live preview switch");
        var star = new Label { Text = "☆", FontSize = 23, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        var starWell = SettingsControls.Rounded(star, 10); starWell.WidthRequest = 32; starWell.HeightRequest = 34; starWell.SetDynamicResource(BackgroundColorProperty, "AccentButton");
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
    private async Task OpenProfileAsync() { if (picking) return; picking = true; try { await Navigation.PushAsync(new ProfileEditorPage(preferences, photos)); } finally { picking = false; } }
    private async Task PickColourAsync()
    {
        if (picking) return;
        picking = true;
        try
        {
            hex.Unfocus(); error.Text = ""; error.IsVisible = false;
            preferences.SetCustomColourEnabled(true);
            var value = await picker.PickAsync(preferences.AccentHex, hexValue => preferences.SetAccent(hexValue, false));
            preferences.SetAccent(value);
        }
        catch { error.Text = "The colour picker is unavailable. Enter a hex colour instead."; error.IsVisible = true; }
        finally { preferences.SaveAccent(); picking = false; }
    }
    private void ApplyHex()
    {
        if (updating) return;
        if (!preferences.SetAccent(hex.Text)) { error.Text = "Use a six-digit colour, for example #007AFF."; error.IsVisible = true; }
        else { error.IsVisible = false; hex.Text = preferences.AccentHex; }
    }
    private void Changed(object? sender, PropertyChangedEventArgs args) => Refresh();
    private void Refresh()
    {
        updating = true;
        try
        {
            var accent = Color.FromArgb(preferences.AccentHex);
            for (var i = 0; i < themes.Count; i++) themes[i].Update(i == preferences.ThemeIndex, accent);
            for (var i = 0; i < accents.Count; i++) accents[i].Update(i < AppearanceValues.Presets.Count ? AppearanceValues.Presets[i] == preferences.AccentHex : !AppearanceValues.Presets.Contains(preferences.AccentHex), accent);
            custom.IsToggled = preferences.CustomColourEnabled; customEditor.IsEnabled = preferences.CustomColourEnabled; customEditor.Opacity = preferences.CustomColourEnabled ? 1 : .4;
            if (!hex.IsFocused) hex.Text = preferences.AccentHex;
            if (!draggingHue) hue.Value = (300 - Hue(accent) + 360) % 360;
            colourWell.Background = SettingsControls.Swatch(accent);
            colourWell.Shadow = new Shadow { Brush = new SolidColorBrush(accent), Opacity = .35f, Radius = 8, Offset = new Point(0, 2) };
            displayName.Text = preferences.DisplayName;
            initial.Text = System.Globalization.StringInfo.GetNextTextElement(preferences.DisplayName);
            var path = photos.GetPath(preferences.PhotoKey);
            if (path != displayedPhotoPath) { portrait.Source = path; displayedPhotoPath = path; }
            portrait.IsVisible = path is not null;
            if (string.IsNullOrEmpty(error.Text)) error.IsVisible = false;
        }
        finally { updating = false; }
    }
    private static double Hue(Color color)
    {
        var max = Math.Max(color.Red, Math.Max(color.Green, color.Blue)); var min = Math.Min(color.Red, Math.Min(color.Green, color.Blue)); var delta = max - min;
        if (delta < .00001) return 0;
        var hueValue = max == color.Red ? (color.Green - color.Blue) / delta : max == color.Green ? 2 + (color.Blue - color.Red) / delta : 4 + (color.Red - color.Green) / delta;
        return (hueValue * 60 + 360) % 360;
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
        preferences.SaveAccent(); base.OnDisappearing();
    }
}
