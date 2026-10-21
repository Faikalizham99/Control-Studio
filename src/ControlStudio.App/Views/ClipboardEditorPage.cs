using ControlStudio.Core.Actions;

namespace ControlStudio.App.Views;

public sealed class ClipboardEditorPage : ContentPage
{
    private readonly IActionRepository repository;
    private readonly IActionValidator validator;
    private readonly ActionDefinition? original;
    private readonly ProfileNameEntry name = new() { Placeholder = "e.g. Delivery address", MaxLength = 60, BackgroundColor = Colors.Transparent, AutomationId = "ClipboardName" };
    private readonly Editor text = new() { Placeholder = "Text to copy", MaxLength = 10_000, MinimumHeightRequest = 180, MaximumHeightRequest = 320, AutoSize = EditorAutoSizeOption.TextChanges, BackgroundColor = Colors.Transparent, AutomationId = "ClipboardText" };
    private readonly Label count = SettingsControls.Text("0 / 10,000", 12, secondary: true);
    private readonly Label status = SettingsControls.Text("", 13, secondary: true);
    private readonly Button save = DashboardStyle.Button("Save clipboard item");
    private bool busy;
    private bool departed;

    public ClipboardEditorPage(IActionRepository repository, IActionValidator validator, ActionDefinition? original = null)
    {
        if (original is not null && original.Parameters is not CopyTextParameters) throw new ArgumentException("Choose a clipboard item.", nameof(original));
        this.repository = repository; this.validator = validator; this.original = original;
        Title = original is null ? "New clipboard item" : "Edit clipboard item";
        AutomationId = "ClipboardEditorPage"; SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this);
        name.SetAppThemeColor(Entry.TextColorProperty, Colors.Black, Colors.White);
        text.SetAppThemeColor(Editor.TextColorProperty, Colors.Black, Colors.White);
        name.Text = original?.Name;
        text.Text = (original?.Parameters as CopyTextParameters)?.Text;
        text.TextChanged += (_, _) => count.Text = $"{text.Text?.Length ?? 0:N0} / 10,000";
        count.Text = $"{text.Text?.Length ?? 0:N0} / 10,000";
        var cancel = new ToolbarItem { Text = "Cancel" };
        cancel.Clicked += async (_, _) =>
        {
            if (busy || departed) return;
            busy = true;
            try { await Navigation.PopAsync(); }
            finally { busy = false; }
        };
        ToolbarItems.Add(cancel);
        save.AutomationId = "SaveClipboardItem";
        save.Clicked += async (_, _) => await SaveAsync();
        status.IsVisible = false;
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 16, Spacing = 16, Children =
        {
            new GlassCard(new VerticalStackLayout { Spacing = 8, Children =
            {
                SettingsControls.Text("Name", 13, bold: true), SettingsControls.Rounded(name, 12, new Thickness(10, 0))
            } }),
            new GlassCard(new VerticalStackLayout { Spacing = 8, Children =
            {
                SettingsControls.Text("Text", 13, bold: true), text, count
            } }),
            SettingsControls.Text("Saved on this iPhone. Text is copied only when you tap Copy. Use a password manager for secrets.", 13, secondary: true),
            status, save
        } } };
        SemanticProperties.SetDescription(name, "Clipboard item name");
        SemanticProperties.SetDescription(text, "Text to save and copy");
    }

    private async Task SaveAsync()
    {
        if (busy || departed) return;
        busy = true; save.IsEnabled = name.IsEnabled = text.IsEnabled = false;
        try
        {
            var item = StudioContent.ClipboardDraft(original, name.Text, text.Text);
            validator.Validate(item);
            await repository.SaveAsync(item);
            if (!departed) await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            status.Text = ex is ActionValidationException or IOException ? ex.Message : "This clipboard item could not be saved. Try again.";
            status.IsVisible = true; SemanticScreenReader.Default.Announce(status.Text);
        }
        finally { busy = false; save.IsEnabled = name.IsEnabled = text.IsEnabled = true; }
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (!Navigation.NavigationStack.Contains(this)) departed = true;
    }
}
