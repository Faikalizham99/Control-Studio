using ControlStudio.App.Platforms.iOS;
using ControlStudio.Core.Actions;

namespace ControlStudio.App.Views;

public sealed class ClipboardEditorPage : ContentPage
{
    private readonly IActionRepository repository;
    private readonly IActionValidator validator;
    private readonly ActionDefinition? original;
    private readonly ClipboardKind kind;
    private readonly AppearancePreferences preferences;
    private readonly ColourPickerView headerColours;
    private readonly Switch useAppAccent = SettingsControls.RowSwitch("ClipboardHeaderUseAccent");
    private readonly ClipboardHeaderView headerPreview;
    private readonly View headerColourCard;
    private string? headerColourHex;
    private bool subscribed;
    private readonly ProfileNameEntry name = new() { Placeholder = "E.g. Quick Note, Reminder, Quote", MaxLength = 60, FontSize = 14, HeightRequest = 44, BackgroundColor = Colors.Transparent, ReturnType = ReturnType.Done, AutomationId = "ClipboardName" };
    private readonly Editor text = CreateTextEditor("ClipboardText");
    private readonly ProfileNameEntry url = new() { Placeholder = "https://example.com", MaxLength = 2048, FontSize = 14, HeightRequest = 44, BackgroundColor = Colors.Transparent, Keyboard = Keyboard.Url, ReturnType = ReturnType.Done, IsTextPredictionEnabled = false, IsSpellCheckEnabled = false, AutomationId = "ClipboardLink" };
    private readonly Image photo = new() { Aspect = Aspect.AspectFit, HeightRequest = 220, AutomationId = "ClipboardPhotoDraft" };
    private readonly Label count = SettingsControls.Text("", 12, secondary: true);
    private readonly Label status = SettingsControls.Text("", 13, secondary: true);
    private readonly ClipboardEditorActionButton save = new("Save item", new ProfileGlyph(ProfileSymbol.Check, "AccentActionText"), "SaveClipboardItem", primary: true);
    private readonly ClipboardEditorActionButton back = new("", new ProfileGlyph(ProfileSymbol.Back, "AccentText"), "ClipboardEditorBack");
    private readonly ClipboardEditorActionButton cancel = new("Cancel", null, "ClipboardEditorCancel", accentCaption: true);
    private readonly VerticalStackLayout body = new() { Spacing = 12 };
    private readonly WidgetEditorScrollView scroll = new();
    private readonly View photoPlaceholder;
    private string? photoKey;
    private bool busy, departed, saved;

    public ClipboardEditorPage(IActionRepository repository, IActionValidator validator, AppearancePreferences preferences, AppleColorPicker colourPicker, ActionDefinition? original = null)
        : this(repository, validator, preferences, colourPicker, ClipboardKind.Text, original) { }

    internal ClipboardEditorPage(IActionRepository repository, IActionValidator validator, AppearancePreferences preferences, AppleColorPicker colourPicker, ClipboardKind kind, ActionDefinition? original = null)
    {
        if (original is not null && StudioContent.Kind(original.Parameters) != kind) throw new ArgumentException("Choose a matching clipboard item.", nameof(original));
        this.repository = repository; this.validator = validator; this.original = original; this.kind = kind;
        this.preferences = preferences; headerColourHex = original?.HeaderColorHex;
        headerPreview = new ClipboardHeaderView(new ClipboardIconButton(
            original?.IsFavorite == true ? StudioSymbol.PinFilled : StudioSymbol.Pin, "Pin", decorative: true));
        headerColours = new ColourPickerView(colourPicker, "ClipboardHeader", "Header");
        headerColourCard = BuildHeaderColourCard();
        Title = $"{(original is null ? "New" : "Edit")} {kind.ToString().ToLowerInvariant()}";
        AutomationId = "ClipboardEditorPage"; SafeAreaEdges = SafeAreaEdges.All;
        NavigationPage.SetHasNavigationBar(this, false); DashboardStyle.NavigationAccent(this);
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F2F2F7"), Colors.Black);
        StyleEntry(name); StyleEntry(url);
        name.Text = original?.Name;
        name.TextChanged += (_, _) => RefreshHeaderPreview(); RefreshHeaderPreview();
        name.Completed += (_, _) => name.Unfocus(); url.Completed += (_, _) => url.Unfocus();
        photoPlaceholder = new VerticalStackLayout { Spacing = 10, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Children =
        {
            ClipboardEditorControls.Icon(StudioSymbol.Photo, size: 44),
            SettingsControls.Text("Paste or choose a photo to preview it here", 13, secondary: true)
        } };
        switch (kind)
        {
            case ClipboardKind.Photo: BuildPhotoCard(); break;
            case ClipboardKind.Link: BuildLinkCard(); break;
            default: BuildTextCard(); break;
        }
        back.Describe("Back to Clipboard without saving");
        back.Clicked += async (_, _) => await CancelAsync(); cancel.Clicked += async (_, _) => await CancelAsync();
        save.Clicked += async (_, _) => await SaveAsync();
        status.IsVisible = false;
        var nameCard = ClipboardEditorControls.Card(new VerticalStackLayout { Spacing = 12, Children =
        {
            ClipboardEditorControls.Heading("Name", "Give this item a name", ClipboardEditorControls.Icon(StudioSymbol.Tag)),
            ClipboardEditorControls.Field(name, new Thickness(12, 0))
        } });
        var form = new VerticalStackLayout { Padding = new Thickness(16, 10, 16, 24), Spacing = 16, Children =
        {
            nameCard, ClipboardEditorControls.Card(body), headerColourCard, PrivacyNote(), status, save
        } };
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        layout.Add(ClipboardEditorControls.Header(Title, back, cancel));
        scroll.Content = form; scroll.AutomationId = "ClipboardEditorScroll";
        layout.Add(scroll, 0, 1); Content = layout;
        SemanticProperties.SetDescription(name, "Clipboard item name");
        SemanticProperties.SetDescription(text, "Text to save and copy");
        SemanticProperties.SetDescription(url, "Link to save and open");
        SemanticProperties.SetDescription(photo, "Chosen photo preview");
    }

    private View BuildHeaderColourCard()
    {
        useAppAccent.IsToggled = headerColourHex is null;
        SemanticProperties.SetDescription(useAppAccent, "Use app accent for this item's header");
        headerColours.SetSelection(headerColourHex ?? preferences.AccentHex);
        headerColours.IsVisible = !useAppAccent.IsToggled;
        useAppAccent.Toggled += (_, args) =>
        {
            scroll.PreserveViewport();
            headerColourHex = args.Value ? null : headerColours.SelectedHex;
            headerColours.IsVisible = !args.Value; RefreshHeaderPreview();
        };
        headerColours.ColourChanged += (_, args) =>
        {
            if (!useAppAccent.IsToggled && !departed) { headerColourHex = args.Hex; RefreshHeaderPreview(); }
        };
        headerColours.BusyChanged += (_, _) => RefreshActions();
        var follow = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var label = SettingsControls.Text("Use app accent", 14); label.VerticalOptions = LayoutOptions.Center;
        follow.Add(label); follow.Add(useAppAccent, 1);
        var previewBody = SettingsControls.Text("Your content appears below this header.", 13, secondary: true);
        previewBody.Margin = 12;
        var preview = ClipboardCard.Create(headerPreview, previewBody);
        preview.AutomationId = "ClipboardHeaderPreview";
        return ClipboardEditorControls.Card(new VerticalStackLayout { Spacing = 12, Children =
        {
            ClipboardEditorControls.Heading("Header colour", "Personalise this item's title area", ClipboardEditorControls.Icon(StudioSymbol.Tag)),
            follow, preview, headerColours
        } });
    }

    private void RefreshHeaderPreview() => headerPreview.SetItem(string.IsNullOrWhiteSpace(name.Text) ? "Item title" : name.Text.Trim(), headerColourHex);

    private void AppearanceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(AppearancePreferences.AccentHex) && useAppAccent.IsToggled)
            headerColours.SetSelection(preferences.AccentHex);
    }

    private void BuildTextCard()
    {
        body.Children.Add(ClipboardEditorControls.Heading("Text to copy", "Enter the text that will be copied", ClipboardEditorControls.Icon(StudioSymbol.Text)));
        text.Text = (original?.Parameters as CopyTextParameters)?.Text;
        text.HeightRequest = 174;
        text.TextChanged += (_, _) => UpdateCount(); UpdateCount();
        var expand = new ClipboardEditorActionButton("", ClipboardEditorControls.Icon(StudioSymbol.Expand, size: 16), "ExpandClipboardText")
        {
            WidthRequest = 44, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End
        };
        expand.Describe("Expand text editor"); expand.Clicked += async (_, _) => await RunAsync(ExpandTextAsync);
        // Reserve a footer below the editable text so the expand target never covers a caret or selection.
        var field = new Grid { RowDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        field.Add(text); field.Add(expand, 0, 1);
        body.Children.Add(ClipboardEditorControls.Field(field, 4)); body.Children.Add(count);
        body.Children.Add(TextActions(isLink: false));
    }

    private void BuildLinkCard()
    {
        body.Children.Add(ClipboardEditorControls.Heading("Link to open", "Enter the address you want to save", ClipboardEditorControls.Icon(StudioSymbol.Link)));
        name.Placeholder = "E.g. Favourite website, Article";
        url.Text = (original?.Parameters as OpenUrlParameters)?.Url;
        body.Children.Add(ClipboardEditorControls.Field(url, new Thickness(12, 0)));
        body.Children.Add(SettingsControls.Text("Use a complete http:// or https:// address. Open launches it in your default browser.", 12, secondary: true));
        url.TextChanged += (_, _) => count.Text = $"{url.Text?.Length ?? 0:N0} / {url.MaxLength:N0}";
        count.Text = $"{url.Text?.Length ?? 0:N0} / {url.MaxLength:N0}";
        body.Children.Add(count); body.Children.Add(TextActions(isLink: true));
    }

    private void BuildPhotoCard()
    {
        name.Placeholder = "E.g. Receipt, Inspiration, Favourite photo";
        body.Children.Add(ClipboardEditorControls.Heading("Photo to copy", "Paste or choose a photo, then check the preview", ClipboardEditorControls.Icon(StudioSymbol.Photo)));
        photoKey = (original?.Parameters as PhotoParameters)?.AssetKey; RefreshPhoto();
        body.Children.Add(ClipboardEditorControls.Field(new Grid { HeightRequest = 220, Children = { photoPlaceholder, photo } }, 6));
        var paste = Action("Paste photo", StudioSymbol.Clipboard, "ClipboardPastePhoto", async () =>
        {
            name.Unfocus();
            var picked = await ClipboardPhotoStore.PasteAsync();
            AcceptDraftPhoto(picked);
        });
        var photos = Action("Photo Library", StudioSymbol.Photo, "ClipboardChooseLibraryPhoto", () => PickPhotoAsync(true));
        var files = Action("Files", StudioSymbol.Text, "ClipboardChooseFilePhoto", () => PickPhotoAsync(false));
        var remove = Action("Remove", StudioSymbol.Delete, "ClipboardRemoveDraftPhoto", () =>
        {
            DiscardDraftPhoto(); photoKey = null; RefreshPhoto(); ShowStatus(""); return Task.CompletedTask;
        });
        // Two columns keep the full labels and 44-point targets readable on smaller screens.
        body.Children.Add(ActionRow(paste, photos)); body.Children.Add(ActionRow(files, remove));
        body.Children.Add(SettingsControls.Text("A private copy is saved. Your original photo stays unchanged.", 12, secondary: true));
    }

    private View TextActions(bool isLink)
    {
        var paste = Action("Paste", StudioSymbol.Clipboard, "PasteClipboardContent", async () =>
        {
            var pasted = await Clipboard.Default.GetTextAsync();
            if (departed) return;
            if (string.IsNullOrEmpty(pasted)) { ShowStatus("The clipboard has no text to paste."); return; }
            var limit = isLink ? url.MaxLength : text.MaxLength;
            if (pasted.Length > limit) { ShowStatus($"This is longer than {limit:N0} characters. Shorten it before pasting."); return; }
            if (isLink) url.Text = pasted; else text.Text = pasted;
            ShowStatus("");
        });
        var clear = Action("Clear", StudioSymbol.Clear, "ClearClipboardContent", () =>
        {
            if (isLink) url.Text = ""; else text.Text = "";
            ShowStatus(""); return Task.CompletedTask;
        });
        var example = Action("Example", isLink ? StudioSymbol.Link : StudioSymbol.Text, "ExampleClipboardContent", async () =>
        {
            var current = isLink ? url.Text : text.Text;
            if (!string.IsNullOrEmpty(current) && !await DisplayAlertAsync("Replace draft?", "Replace the current content with an example?", "Replace", "Cancel")) return;
            if (departed) return;
            if (isLink) url.Text = "https://example.com"; else text.Text = "A little note, ready when you need it.";
            ShowStatus("");
        });
        return ActionRow(paste, clear, example);
    }

    private ClipboardEditorActionButton Action(string title, StudioSymbol icon, string id, Func<Task> operation)
    {
        var button = new ClipboardEditorActionButton(title, ClipboardEditorControls.Icon(icon, size: 17), id);
        button.Clicked += async (_, _) => await RunAsync(operation); return button;
    }

    private static View ActionRow(params View[] buttons)
    {
        var row = new Grid { ColumnSpacing = ActionSpacing.Gap };
        for (var i = 0; i < buttons.Length; i++) { row.ColumnDefinitions.Add(new(GridLength.Star)); row.Add(buttons[i], i); }
        return row;
    }

    private static View PrivacyNote()
    {
        var row = new Grid { Padding = new Thickness(13, 11), ColumnSpacing = 10, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        var info = new ProfileGlyph(ProfileSymbol.Info) { Tint = Color.FromArgb("#329DFF"), VerticalOptions = LayoutOptions.Center };
        row.Add(info); row.Add(SettingsControls.Text("Saved on this iPhone. Content is copied or shared only when you choose an action.", 12, secondary: true), 1);
        var note = ClipboardEditorControls.Field(row, 0);
        note.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#EAF3FE"), Color.FromArgb("#0B1929"));
        note.RemoveBinding(Border.StrokeProperty);
        note.Stroke = new SolidColorBrush(Color.FromArgb("#329DFF").WithAlpha(.35f)); return note;
    }

    private async Task ExpandTextAsync()
    {
        text.Unfocus(); name.Unfocus();
        var editor = CreateTextEditor("ExpandedClipboardText"); editor.Text = text.Text;
        var expandedCount = SettingsControls.Text(count.Text, 12, secondary: true);
        editor.TextChanged += (_, _) => { text.Text = editor.Text; expandedCount.Text = count.Text; };
        var page = new ContentPage { Title = "Text to copy", SafeAreaEdges = SafeAreaEdges.All, AutomationId = "ExpandedClipboardTextPage" };
        page.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F2F2F7"), Colors.Black);
        var close = new ClipboardEditorActionButton("", new ProfileGlyph(ProfileSymbol.Back, "AccentText"), "CloseExpandedClipboardText"); close.Describe("Return to item editor");
        var done = new ClipboardEditorActionButton("Done", null, "DoneExpandedClipboardText", accentCaption: true);
        var closing = false;
        async Task CloseAsync()
        {
            if (closing) return; closing = true;
            try { editor.Unfocus(); await Navigation.PopModalAsync(); }
            finally { closing = false; }
        }
        close.Clicked += async (_, _) => await CloseAsync(); done.Clicked += async (_, _) => await CloseAsync();
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }, RowSpacing = 12, Padding = new Thickness(0, 0, 0, 16) };
        layout.Add(ClipboardEditorControls.Header("Text to copy", close, done));
        var field = ClipboardEditorControls.Field(editor, 8); field.Margin = new Thickness(16, 0); layout.Add(field, 0, 1);
        expandedCount.Margin = new Thickness(24, 0); layout.Add(expandedCount, 0, 2); page.Content = layout;
        await Navigation.PushModalAsync(page);
    }

    private async Task PickPhotoAsync(bool fromLibrary)
    {
        name.Unfocus();
        var picked = await ClipboardPhotoStore.PickAsync(fromLibrary);
        if (picked is null) return;
        AcceptDraftPhoto(picked);
    }

    private void AcceptDraftPhoto(string picked)
    {
        if (departed) { ClipboardPhotoStore.Remove(picked); return; }
        DiscardDraftPhoto(); photoKey = picked; RefreshPhoto(); ShowStatus("");
    }

    private static Editor CreateTextEditor(string id)
    {
        var editor = new Editor { Placeholder = "Type your text here...", MaxLength = 10_000, FontSize = 14, AutoSize = EditorAutoSizeOption.Disabled,
            BackgroundColor = Colors.Transparent, IsTextPredictionEnabled = true, IsSpellCheckEnabled = true, AutomationId = id };
        editor.SetAppThemeColor(Editor.TextColorProperty, Color.FromArgb("#16171C"), Color.FromArgb("#F5F5F7"));
        editor.SetAppThemeColor(Editor.PlaceholderColorProperty, Color.FromArgb("#737B8B"), Color.FromArgb("#9198A7")); return editor;
    }

    private static void StyleEntry(Entry entry)
    {
        entry.SetAppThemeColor(Entry.TextColorProperty, Color.FromArgb("#16171C"), Color.FromArgb("#F5F5F7"));
        entry.SetAppThemeColor(Entry.PlaceholderColorProperty, Color.FromArgb("#737B8B"), Color.FromArgb("#9198A7"));
    }

    private void UpdateCount() => count.Text = $"{text.Text?.Length ?? 0:N0} / {text.MaxLength:N0}";
    private void RefreshPhoto()
    {
        photo.Source = ClipboardPhotoStore.GetPath(photoKey); photo.IsVisible = photo.Source is not null;
        photoPlaceholder.IsVisible = !photo.IsVisible;
    }
    private void ShowStatus(string message) { status.Text = message; status.IsVisible = !string.IsNullOrEmpty(message); if (status.IsVisible) SemanticScreenReader.Default.Announce(message); }
    private void RefreshActions() => save.IsEnabled = name.IsEnabled = body.IsEnabled = headerColourCard.IsEnabled = back.IsEnabled = cancel.IsEnabled = !busy && !headerColours.IsBusy && !saved && !departed;
    private void DiscardDraftPhoto()
    {
        if (photoKey != (original?.Parameters as PhotoParameters)?.AssetKey && !saved) ClipboardPhotoStore.Remove(photoKey);
    }
    private async Task CancelAsync()
    {
        if (busy || headerColours.IsBusy || departed || saved) return;
        busy = true; RefreshActions(); name.Unfocus(); text.Unfocus(); url.Unfocus();
        try { await Navigation.PopAsync(); }
        finally { busy = false; RefreshActions(); if (departed) DiscardDraftPhoto(); }
    }
    private async Task SaveAsync() => await RunAsync(async () =>
    {
        if (!useAppAccent.IsToggled && !headerColours.CommitPendingHex()) { ShowStatus("Enter a valid header colour before saving."); return; }
        name.Unfocus(); text.Unfocus(); url.Unfocus();
        var content = kind switch { ClipboardKind.Photo => photoKey, ClipboardKind.Link => url.Text, _ => text.Text };
        var item = StudioContent.Draft(original, kind, name.Text, content) with { HeaderColorHex = headerColourHex };
        validator.Validate(item);
        if (item.Parameters is PhotoParameters p) ClipboardPhotoStore.RequirePath(p.AssetKey);
        // Finish the database save before deleting a replaced photo or leaving this editor.
        await repository.SaveAsync(item); saved = true;
        if (original?.Parameters is PhotoParameters old && old.AssetKey != photoKey) ClipboardPhotoStore.Remove(old.AssetKey);
        if (!departed) await Navigation.PopAsync();
    });

    private async Task RunAsync(Func<Task> operation)
    {
        if (busy || headerColours.IsBusy || departed || saved) return;
        busy = true; RefreshActions();
        try { await operation(); }
        catch (Exception ex) { ShowStatus(ex is ActionValidationException or IOException ? ex.Message : "This action could not finish. Try again."); }
        finally { busy = false; RefreshActions(); if (departed && !saved) DiscardDraftPhoto(); }
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!subscribed) { preferences.PropertyChanged += AppearanceChanged; subscribed = true; }
        AppearanceChanged(preferences, new System.ComponentModel.PropertyChangedEventArgs(nameof(AppearancePreferences.AccentHex)));
    }
    protected override void OnDisappearing()
    {
        if (subscribed) { preferences.PropertyChanged -= AppearanceChanged; subscribed = false; }
        base.OnDisappearing();
    }
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (!Navigation.NavigationStack.Contains(this)) { departed = true; scroll.ReleaseViewport(); if (!busy) DiscardDraftPhoto(); }
    }
}
