using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

public sealed class ProfileEditorPage : ContentPage
{
    private readonly AppearancePreferences preferences;
    private readonly IProfilePhotoStore photos;
    private readonly Func<string[], Task<string?>> choosePhotoSource;
    private readonly ProfileNameEntry name = new()
    {
        MaxLength = 40, Placeholder = "Your name", FontSize = 15, BackgroundColor = Colors.Transparent,
        HeightRequest = 44, MinimumWidthRequest = 0, ReturnType = ReturnType.Done, AutomationId = "ProfileName"
    };
    private readonly Image portrait = new() { Aspect = Aspect.AspectFill, HeightRequest = 112, WidthRequest = 112 };
    private readonly Label initial = new() { FontSize = 44, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
    private readonly Label status = SettingsControls.Text("", 13, secondary: true);
    private readonly ProfileActionButton choose = new("Choose photo", ProfileSymbol.Photo, "ChooseProfilePhoto");
    private readonly ProfileActionButton remove = new("Remove photo", ProfileSymbol.Trash, "RemoveProfilePhoto", primary: false);
    private readonly ProfileActionButton save = new("Save profile", ProfileSymbol.Check, "SaveProfile", radius: 24);
    private readonly Button back = new() { Text = "", BackgroundColor = Colors.Transparent, WidthRequest = 44, HeightRequest = 44, Padding = 0, AutomationId = "ProfileBack" };
    private readonly Button cancel = new() { Text = "Cancel", BackgroundColor = Colors.Transparent, FontSize = 14, MinimumHeightRequest = 44, Padding = new Thickness(12, 0), AutomationId = "ProfileCancel" };
    private readonly Button clear = new() { Text = "", BackgroundColor = Colors.Transparent, WidthRequest = 44, HeightRequest = 44, Padding = 0, AutomationId = "ClearProfileName" };
    private readonly ActivityIndicator activity = new() { IsVisible = false, HeightRequest = 22 };
    private string photoKey;
    private bool busy;
    private bool saved;
    private bool departed;
    private bool closing;

    public ProfileEditorPage(AppearancePreferences preferences, IProfilePhotoStore photos)
        : this(preferences, photos, null) { }

    internal ProfileEditorPage(AppearancePreferences preferences, IProfilePhotoStore photos, Func<string[], Task<string?>>? choosePhotoSource)
    {
        this.preferences = preferences; this.photos = photos; photoKey = preferences.PhotoKey;
        this.choosePhotoSource = choosePhotoSource ?? (options => DisplayActionSheetAsync("Choose profile photo", "Cancel", null, options));
        Title = "Edit profile"; AutomationId = "ProfileEditorPage"; SafeAreaEdges = SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(this, false);
        DashboardStyle.NavigationAccent(this);
        initial.SetDynamicResource(Label.TextColorProperty, "AccentButtonText");
        activity.SetDynamicResource(ActivityIndicator.ColorProperty, "AccentText");
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F2F2F7"), Colors.Black);
        name.SetAppThemeColor(Entry.TextColorProperty, Color.FromArgb("#16171C"), Color.FromArgb("#F5F5F7"));
        name.Text = preferences.DisplayName;
        name.TextChanged += (_, _) => RefreshName();
        name.Completed += (_, _) => name.Unfocus();
        clear.Clicked += (_, _) => { name.Text = ""; name.Focus(); };
        SemanticProperties.SetDescription(clear, "Clear display name");
        choose.Clicked += async (_, _) => await ChoosePhotoAsync();
        remove.Clicked += (_, _) =>
        {
            if (busy) return;
            DiscardDraftPhoto(); photoKey = ""; RefreshPhoto(); ShowStatus("Photo removed. Save to keep this change.");
        };
        save.Clicked += async (_, _) => await SaveAsync();
        back.Clicked += async (_, _) => await CancelAsync();
        cancel.Clicked += async (_, _) => await CancelAsync();
        SemanticProperties.SetDescription(back, "Back to Settings without saving");
        cancel.SetDynamicResource(Button.TextColorProperty, "AccentText");

        var page = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, RowSpacing = 8 };
        var header = BuildHeader(); header.Margin = new Thickness(8, 4, 8, 0);
        page.Add(header);
        var body = new VerticalStackLayout { Padding = new Thickness(16, 4, 16, 20), Spacing = 12 };
        body.Children.Add(BuildPhotoCard());
        body.Children.Add(BuildNameCard());
        var saveFrame = SettingsControls.Rounded(save, 28, 3);
        saveFrame.Shadow = new Shadow { Opacity = .22f, Radius = 12, Offset = new Point(0, 3) };
        saveFrame.Shadow.SetDynamicResource(Shadow.BrushProperty, "AccentGlow");
        body.Children.Add(saveFrame);
        body.Children.Add(activity);
        page.Add(new ScrollView { Content = body }, 0, 1);
        Content = page;
        SemanticProperties.SetDescription(name, "Profile display name");
        RefreshPhoto(); ShowStatus("");
    }

    private GlassCard BuildHeader()
    {
        var backIcon = new ProfileGlyph(ProfileSymbol.Back) { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        backIcon.SetAppThemeColor(ProfileGlyph.TintProperty, Color.FromArgb("#252A34"), Colors.White);
        var backSurface = SettingsControls.Rounded(new Grid { Children = { backIcon, back } }, 22);
        backSurface.WidthRequest = backSurface.HeightRequest = 44; backSurface.HorizontalOptions = LayoutOptions.Start;
        var heading = SettingsControls.Text("Edit profile", 17, bold: true);
        heading.VerticalOptions = LayoutOptions.Center; heading.HorizontalTextAlignment = TextAlignment.Center;
        var cancelSurface = SettingsControls.Rounded(cancel, 22);
        cancelSurface.HorizontalOptions = LayoutOptions.End;
        var row = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Star) } };
        row.Add(backSurface); row.Add(heading, 1); row.Add(cancelSurface, 2);
        return new GlassCard(row, new Thickness(6, 4));
    }

    private GlassCard BuildPhotoCard()
    {
        var avatar = new Grid { WidthRequest = 112, HeightRequest = 112, Children = { initial, portrait } };
        avatar.SetDynamicResource(BackgroundColorProperty, "Accent");
        var frame = new Border { Content = avatar, Stroke = Colors.White, StrokeThickness = 2.5, StrokeShape = new Ellipse(), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        frame.Shadow = new Shadow { Opacity = .35f, Radius = 12, Offset = new Point(0, 2) };
        frame.Shadow.SetDynamicResource(Shadow.BrushProperty, "AccentGlow");
        var halo = new Border
        {
            Content = frame, WidthRequest = 136, HeightRequest = 136, StrokeShape = new Ellipse(),
            StrokeThickness = 1, HorizontalOptions = LayoutOptions.Center
        };
        halo.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(Colors.White.WithAlpha(.85f)), new SolidColorBrush(Colors.White.WithAlpha(.2f)));
        halo.SetAppThemeColor(BackgroundColorProperty, Colors.White.WithAlpha(.25f), Colors.White.WithAlpha(.05f));
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await ChoosePhotoAsync(adjustExisting: photos.GetPath(photoKey) is not null);
        halo.GestureRecognizers.Add(tap);
        SemanticProperties.SetDescription(halo, "Profile photo");
        SemanticProperties.SetHint(halo, "Tap to choose a photo or adjust its crop. You can also use Choose photo below.");
        return new GlassCard(new VerticalStackLayout
        {
            Padding = new Thickness(2, 0, 2, 0), Spacing = 10, Children = { halo, choose, remove }
        });
    }

    private GlassCard BuildNameCard()
    {
        var field = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var clearIcon = new ProfileGlyph(ProfileSymbol.Clear) { Tint = Color.FromArgb("#9EA5B3"), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        field.Add(name); field.Add(new Grid { Children = { clearIcon, clear } }, 1);
        var info = new ProfileGlyph(ProfileSymbol.Info) { Tint = Color.FromArgb("#7C879B"), WidthRequest = 20, HeightRequest = 20, VerticalOptions = LayoutOptions.Start };
        var note = SettingsControls.Text("Your profile stays on this iPhone. Choose a photo from Photo Library or Files, then crop and preview it before saving.", 12, secondary: true);
        var information = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        information.Add(info); information.Add(note, 1);
        return new GlassCard(new VerticalStackLayout
        {
            Spacing = 9, Children =
            {
                SettingsControls.Text("Display name", 13, bold: true),
                SettingsControls.Rounded(field, 14, new Thickness(10, 0, 0, 0)), information, status
            }
        }, 14);
    }

    private async Task SaveAsync()
    {
        if (busy || departed || saved || closing) return;
        name.Unfocus();
        var previousPhoto = preferences.PhotoKey;
        if (!preferences.SaveProfile(name.Text, photoKey)) { ShowStatus("Enter a name of 1–40 characters without control characters.", true); name.Focus(); return; }
        saved = true;
        RefreshActions();
        if (previousPhoto != photoKey) photos.Remove(previousPhoto);
        await Navigation.PopAsync();
    }

    private async Task CancelAsync()
    {
        if (busy || departed || saved || closing) return;
        closing = true; RefreshActions(); name.Unfocus();
        try { await Navigation.PopAsync(); }
        finally { closing = false; RefreshActions(); }
    }

    internal async Task ChoosePhotoAsync(bool adjustExisting = false)
    {
        if (busy || departed || saved || closing) return;
        name.Unfocus(); busy = true; RefreshActions();
        try
        {
            string? picked;
            if (adjustExisting) picked = await photos.AdjustAsync(photoKey);
            else
            {
                var options = photos.GetPath(photoKey) is null ? new[] { "Photo Library", "Files" } : new[] { "Photo Library", "Files", "Adjust current photo" };
                var source = await choosePhotoSource(options);
                if (departed || source is null or "Cancel") return;
                if (source == "Adjust current photo") picked = await photos.AdjustAsync(photoKey);
                else if (source is "Photo Library" or "Files") picked = await photos.PickAsync(fromLibrary: source == "Photo Library");
                else return;
            }
            if (picked is null) return;
            if (departed) { photos.Remove(picked); return; }
            DiscardDraftPhoto(); photoKey = picked; RefreshPhoto(); ShowStatus("Photo ready. Save to keep it.");
        }
        catch (Exception ex) { ShowStatus(ex is IOException ? ex.Message : "The photo could not be imported. Try another image.", true); }
        finally { busy = false; RefreshActions(); }
    }

    private void RefreshName()
    {
        var display = name.Text?.Trim();
        initial.Text = System.Globalization.StringInfo.GetNextTextElement(string.IsNullOrEmpty(display) ? preferences.DisplayName : display);
        clear.IsEnabled = !busy && !saved && !departed && !closing && !string.IsNullOrEmpty(name.Text);
    }

    private void RefreshPhoto()
    {
        var path = photos.GetPath(photoKey);
        portrait.Source = path; portrait.IsVisible = path is not null;
        RefreshName(); RefreshActions();
    }

    private void RefreshActions()
    {
        choose.IsEnabled = save.IsEnabled = name.IsEnabled = back.IsEnabled = cancel.IsEnabled = !busy && !saved && !departed && !closing;
        remove.IsEnabled = !busy && !saved && !departed && !closing && photos.GetPath(photoKey) is not null;
        activity.IsVisible = activity.IsRunning = busy;
        RefreshName();
    }

    private void ShowStatus(string message, bool error = false)
    {
        status.Text = message; status.IsVisible = !string.IsNullOrEmpty(message);
        if (error) status.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#B42336"), Color.FromArgb("#FF8798"));
        else status.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#626570"), Color.FromArgb("#AFB3BD"));
    }

    private void DiscardDraftPhoto() { if (photoKey != preferences.PhotoKey) photos.Remove(photoKey); }
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (!Navigation.NavigationStack.Contains(this))
        {
            departed = true;
            RefreshActions();
            if (!saved) DiscardDraftPhoto();
        }
    }
}
