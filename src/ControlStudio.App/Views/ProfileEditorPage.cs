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
    private string photoKey;
    private bool busy;
    private bool saved;
    private bool departed;

    public ProfileEditorPage(AppearancePreferences preferences, ProfilePhotoStore photos)
    {
        this.preferences = preferences; this.photos = photos; photoKey = preferences.PhotoKey;
        Title = "Edit profile"; SafeAreaEdges = SafeAreaEdges.All;
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F2F2F7"), Colors.Black);
        name.Text = preferences.DisplayName;
        var avatar = new Grid { HeightRequest = 112, WidthRequest = 112, Children = { initial, portrait } };
        avatar.SetDynamicResource(BackgroundColorProperty, "Accent");
        var frame = new Border { Content = avatar, Stroke = Colors.White, StrokeThickness = 3, StrokeShape = new Ellipse(), HorizontalOptions = LayoutOptions.Center };
        var choose = DashboardStyle.Button("Choose photo"); choose.Clicked += async (_, _) => await ChoosePhotoAsync();
        var remove = new Button { Text = "Remove photo", BackgroundColor = Colors.Transparent };
        remove.SetDynamicResource(Button.TextColorProperty, "Accent");
        remove.Clicked += (_, _) => { if (busy) return; DiscardDraftPhoto(); photoKey = ""; RefreshPhoto(); };
        var save = DashboardStyle.Button("Save profile");
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
            frame, choose, remove, new Label { Text = "Display name", FontAttributes = FontAttributes.Bold }, name,
            new Label { Text = "Your profile stays on this iPhone. Choose an image from Files; no account or photo-library access is required.", FontSize = 14 }, status, save
        } } };
        SemanticProperties.SetDescription(name, "Profile display name"); RefreshPhoto();
    }

    private async Task ChoosePhotoAsync()
    {
        if (busy) return;
        busy = true;
        try
        {
            var picked = await photos.PickAsync();
            if (picked is null) return;
            if (departed) { photos.Remove(picked); return; }
            DiscardDraftPhoto(); photoKey = picked; RefreshPhoto(); status.Text = "Photo ready. Save to keep it.";
        }
        catch (Exception ex) { status.Text = ex is IOException ? ex.Message : "The photo could not be imported. Try another image."; }
        finally { busy = false; }
    }
    private void RefreshPhoto()
    {
        var path = photos.GetPath(photoKey);
        portrait.Source = path; portrait.IsVisible = path is not null;
        initial.Text = System.Globalization.StringInfo.GetNextTextElement(preferences.DisplayName);
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
