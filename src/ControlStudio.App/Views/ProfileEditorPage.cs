using ControlStudio.App.Platforms.iOS;
using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

public sealed class ProfileEditorPage : ContentPage
{
    private readonly AppearancePreferences preferences;
    private readonly ProfilePhotoStore photos;
    private readonly Entry name = new() { MaxLength = 40, Placeholder = "Your name", AutomationId = "ProfileName" };
    private readonly Image portrait = new() { Aspect = Aspect.AspectFill, HeightRequest = 112, WidthRequest = 112 };
    private readonly Label initial = new() { FontSize = 44, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, TextColor = Colors.White };
    private readonly Label status = new() { FontSize = 14 };
    private readonly Button choose = DashboardStyle.Button("Choose photo");
    private readonly Button adjust = new() { Text = "Adjust photo", BackgroundColor = Colors.Transparent };
    private readonly Button remove = new() { Text = "Remove photo", BackgroundColor = Colors.Transparent };
    private readonly Button save = DashboardStyle.Button("Save profile");
    private string photoKey;
    private bool busy;
    private bool saved;
    private bool departed;

    public ProfileEditorPage(AppearancePreferences preferences, ProfilePhotoStore photos)
    {
        this.preferences = preferences; this.photos = photos; photoKey = preferences.PhotoKey;
        Title = "Edit profile"; SafeAreaEdges = SafeAreaEdges.All;
        DashboardStyle.NavigationAccent(this);
        initial.SetDynamicResource(Label.TextColorProperty, "AccentButtonText");
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F2F2F7"), Colors.Black);
        name.Text = preferences.DisplayName;
        var avatar = new Grid { HeightRequest = 112, WidthRequest = 112, Children = { initial, portrait } };
        avatar.SetDynamicResource(BackgroundColorProperty, "Accent");
        var frame = new Border { Content = avatar, Stroke = Colors.White, StrokeThickness = 3, StrokeShape = new Ellipse(), HorizontalOptions = LayoutOptions.Center };
        choose.Clicked += async (_, _) => await ChoosePhotoAsync();
        adjust.SetDynamicResource(Button.TextColorProperty, "AccentText");
        adjust.Clicked += async (_, _) => await ChoosePhotoAsync(adjustExisting: true);
        remove.SetDynamicResource(Button.TextColorProperty, "AccentText");
        remove.Clicked += (_, _) => { if (busy) return; DiscardDraftPhoto(); photoKey = ""; RefreshPhoto(); };
        save.Clicked += async (_, _) =>
        {
            if (busy) return;
            var previousPhoto = preferences.PhotoKey;
            if (!preferences.SaveProfile(name.Text, photoKey)) { status.Text = "Enter a name of 1–40 characters without control characters."; return; }
            saved = true;
            if (previousPhoto != photoKey) photos.Remove(previousPhoto);
            await Navigation.PopAsync();
        };
        var cancel = new ToolbarItem { Text = "Cancel" };
        cancel.Clicked += async (_, _) => { if (!busy) await Navigation.PopAsync(); };
        ToolbarItems.Add(cancel);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 18, Children = {
            frame, choose, adjust, remove, new Label { Text = "Display name", FontAttributes = FontAttributes.Bold }, name,
            new Label { Text = "Choose from Photo Library or Files, then crop, zoom and rotate. Preview your photo here before saving. Your profile stays on this iPhone.", FontSize = 14 }, status, save
        } } };
        SemanticProperties.SetDescription(name, "Profile display name"); RefreshPhoto();
    }

    private async Task ChoosePhotoAsync(bool adjustExisting = false)
    {
        if (busy) return;
        busy = true;
        RefreshActions();
        try
        {
            string? picked;
            if (adjustExisting) picked = await photos.AdjustAsync(photoKey);
            else
            {
                var source = await DisplayActionSheetAsync("Choose profile photo", "Cancel", null, "Photo Library", "Files");
                if (departed || source is not ("Photo Library" or "Files")) return;
                picked = await photos.PickAsync(fromLibrary: source == "Photo Library");
            }
            if (picked is null) return;
            if (departed) { photos.Remove(picked); return; }
            DiscardDraftPhoto(); photoKey = picked; RefreshPhoto(); status.Text = "Photo ready. Save to keep it.";
        }
        catch (Exception ex) { status.Text = ex is IOException ? ex.Message : "The photo could not be imported. Try another image."; }
        finally { busy = false; RefreshActions(); }
    }
    private void RefreshPhoto()
    {
        var path = photos.GetPath(photoKey);
        portrait.Source = path; portrait.IsVisible = path is not null;
        initial.Text = System.Globalization.StringInfo.GetNextTextElement(preferences.DisplayName);
        RefreshActions();
    }
    private void RefreshActions()
    {
        choose.IsEnabled = save.IsEnabled = !busy;
        adjust.IsEnabled = remove.IsEnabled = !busy && photos.GetPath(photoKey) is not null;
    }
    private void DiscardDraftPhoto() { if (photoKey != preferences.PhotoKey) photos.Remove(photoKey); }
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (!Navigation.NavigationStack.Contains(this))
        {
            departed = true;
            if (!saved) DiscardDraftPhoto();
        }
    }
}
