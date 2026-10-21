using ControlStudio.App.Platforms.iOS;
using ControlStudio.Core.Actions;

namespace ControlStudio.App.Views;

public sealed class ClipboardEditorPage : ContentPage
{
    private readonly IActionRepository repository;
    private readonly IActionValidator validator;
    private readonly ActionDefinition? original;
    private readonly ClipboardKind kind;
    private readonly ProfileNameEntry name = new() { Placeholder = "Give this item a name", MaxLength = 60, BackgroundColor = Colors.Transparent, AutomationId = "ClipboardName" };
    private readonly Editor text = new() { MaxLength = 10_000, MinimumHeightRequest = 240, AutoSize = EditorAutoSizeOption.Disabled, BackgroundColor = Colors.Transparent, AutomationId = "ClipboardText" };
    private readonly Entry url = new() { Placeholder = "https://example.com", MaxLength = 2048, Keyboard = Keyboard.Url, IsTextPredictionEnabled = false, IsSpellCheckEnabled = false, AutomationId = "ClipboardLink" };
    private readonly Image photo = new() { Aspect = Aspect.AspectFit, HeightRequest = 220 };
    private readonly Label count = SettingsControls.Text("", 12, secondary: true);
    private readonly Label status = SettingsControls.Text("", 13, secondary: true);
    private readonly Button save = DashboardStyle.Button("Save item");
    private readonly VerticalStackLayout body = new() { Spacing = 12 };
    private string? photoKey;
    private bool busy, departed, saved;

    public ClipboardEditorPage(IActionRepository repository, IActionValidator validator, ActionDefinition? original = null)
        : this(repository, validator, ClipboardKind.Text, original) { }

    internal ClipboardEditorPage(IActionRepository repository, IActionValidator validator, ClipboardKind kind, ActionDefinition? original = null)
    {
        if (original is not null && StudioContent.Kind(original.Parameters) != kind) throw new ArgumentException("Choose a matching clipboard item.", nameof(original));
        this.repository = repository; this.validator = validator; this.original = original; this.kind = kind;
        Title = $"{(original is null ? "New" : "Edit")} {kind.ToString().ToLowerInvariant()}";
        AutomationId = "ClipboardEditorPage"; SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this);
        name.SetAppThemeColor(Entry.TextColorProperty, Colors.Black, Colors.White);
        text.SetAppThemeColor(Editor.TextColorProperty, Colors.Black, Colors.White);
        url.SetAppThemeColor(Entry.TextColorProperty, Colors.Black, Colors.White);
        name.Text = original?.Name;
        if (kind == ClipboardKind.Photo)
        {
            photoKey = (original?.Parameters as PhotoParameters)?.AssetKey; RefreshPhoto();
            var choose = DashboardStyle.Button("Choose photo");
            choose.Clicked += async (_, _) => await RunAsync(async () =>
            {
                var source = await DisplayActionSheetAsync("Choose photo", "Cancel", null, "Photo Library", "Files");
                if (departed || source is not ("Photo Library" or "Files")) return;
                var picked = await ClipboardPhotoStore.PickAsync(source == "Photo Library");
                if (picked is null) return;
                if (departed) { ClipboardPhotoStore.Remove(picked); return; }
                DiscardDraftPhoto(); photoKey = picked; RefreshPhoto();
            });
            body.Children.Add(photo); body.Children.Add(choose);
            body.Children.Add(SettingsControls.Text("A private copy is saved, up to 4096 pixels on its longest side. The original photo stays unchanged.", 13, secondary: true));
        }
        else if (kind == ClipboardKind.Link)
        {
            url.Text = (original?.Parameters as OpenUrlParameters)?.Url;
            body.Children.Add(SettingsControls.Text("Link", 13, bold: true));
            body.Children.Add(SettingsControls.Rounded(url, 12, new Thickness(10, 0)));
            body.Children.Add(SettingsControls.Text("Enter a complete http:// or https:// address. Open launches it in your default browser.", 13, secondary: true));
        }
        else
        {
            text.Text = (original?.Parameters as CopyTextParameters)?.Text;
            text.Placeholder = "Text to copy";
            text.IsTextPredictionEnabled = true; text.IsSpellCheckEnabled = true;
            text.TextChanged += (_, _) => UpdateCount(); UpdateCount();
            body.Children.Add(SettingsControls.Rounded(text, 12, 8)); body.Children.Add(count);
        }
        var cancel = new ToolbarItem { Text = "Cancel" };
        cancel.Clicked += async (_, _) => { if (!busy && !departed) { busy = true; try { await Navigation.PopAsync(); } finally { busy = false; } } };
        ToolbarItems.Add(cancel);
        save.AutomationId = "SaveClipboardItem"; save.Clicked += async (_, _) => await SaveAsync();
        status.IsVisible = false;
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 16, Children =
        {
            new GlassCard(new VerticalStackLayout { Spacing = 8, Children = { SettingsControls.Text("Name", 13, bold: true), SettingsControls.Rounded(name, 12, new Thickness(10, 0)) } }),
            new GlassCard(body), SettingsControls.Text("Saved on this iPhone. Content is copied or shared only when you choose an action.", 13, secondary: true), status, save
        } } };
        SemanticProperties.SetDescription(name, "Clipboard item name");
        SemanticProperties.SetDescription(text, "Text to save and copy");
    }

    private void UpdateCount() => count.Text = $"{text.Text?.Length ?? 0:N0} / {text.MaxLength:N0}";
    private void RefreshPhoto() { photo.Source = ClipboardPhotoStore.GetPath(photoKey); photo.IsVisible = photo.Source is not null; }
    private void DiscardDraftPhoto()
    {
        if (photoKey != (original?.Parameters as PhotoParameters)?.AssetKey && !saved) ClipboardPhotoStore.Remove(photoKey);
    }
    private async Task SaveAsync() => await RunAsync(async () =>
    {
        var content = kind switch { ClipboardKind.Photo => photoKey, ClipboardKind.Link => url.Text, _ => text.Text };
        var item = StudioContent.Draft(original, kind, name.Text, content);
        validator.Validate(item);
        if (item.Parameters is PhotoParameters p) ClipboardPhotoStore.RequirePath(p.AssetKey);
        // Do not cancel a potentially committed save and then delete its asset.
        await repository.SaveAsync(item); saved = true;
        if (original?.Parameters is PhotoParameters old && old.AssetKey != photoKey) ClipboardPhotoStore.Remove(old.AssetKey);
        if (!departed) await Navigation.PopAsync();
    });

    private async Task RunAsync(Func<Task> operation)
    {
        if (busy || departed || saved) return;
        busy = true; save.IsEnabled = name.IsEnabled = body.IsEnabled = false;
        try { await operation(); }
        catch (Exception ex)
        {
            status.Text = ex is ActionValidationException or IOException ? ex.Message : "This item could not be updated. Try again.";
            status.IsVisible = true; SemanticScreenReader.Default.Announce(status.Text);
        }
        finally
        {
            busy = false; save.IsEnabled = name.IsEnabled = body.IsEnabled = !saved;
            if (departed && !saved) DiscardDraftPhoto();
        }
    }
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (!Navigation.NavigationStack.Contains(this)) { departed = true; if (!busy) DiscardDraftPhoto(); }
    }
}
