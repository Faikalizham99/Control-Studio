using ControlStudio.Core.Actions;

namespace ControlStudio.App.Views;

public sealed class ActionEditorPage : ContentPage
{
    private readonly IActionRepository repository;
    private readonly IActionValidator validator;
    private readonly ActionDefinition? original;
    private readonly Entry name = new() { Placeholder = "e.g. Reading list", MaxLength = 60, AutomationId = "ActionName" };
    private readonly Picker type = new() { Title = "Action", ItemsSource = new[] { "Open web link", "Copy text", "Increment counter" }, SelectedIndex = 0 };
    private readonly Editor parameter = new() { AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 110, MaxLength = 10_000, AutomationId = "ActionParameter" };
    private readonly Label parameterTitle = new() { FontAttributes = FontAttributes.Bold };
    private readonly Label parameterHint = new() { FontSize = 14 };
    private readonly Picker category = new() { Title = "Category", ItemsSource = Enum.GetNames<ActionCategory>(), SelectedIndex = (int)ActionCategory.Personal };
    private readonly Picker icon = new() { Title = "Icon", ItemsSource = Enum.GetNames<ActionIcon>(), SelectedIndex = 0 };
    private readonly Picker color = new() { Title = "Color", ItemsSource = Enum.GetNames<ActionColor>(), SelectedIndex = 0 };
    private readonly Switch favorite = new();
    private readonly Label error = new() { FontSize = 15, AutomationId = "ActionEditorStatus" };
    private readonly Label preview = new() { FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };
    private readonly Border previewCard;
    private readonly Button save = DashboardStyle.Button("Save action");
    private bool busy;

    public ActionEditorPage(IActionRepository repository, IActionValidator validator, ActionDefinition? original = null)
    {
        this.repository = repository; this.validator = validator; this.original = original;
        Title = original is null ? "New action" : "Edit action";
        AutomationId = "ActionEditorPage";
        DashboardStyle.Page(this);
        favorite.SetDynamicResource(Switch.OnColorProperty, "Accent");
        previewCard = DashboardStyle.Card(preview);
        previewCard.MinimumHeightRequest = 84;
        if (original is not null)
        {
            name.Text = original.Name; category.SelectedIndex = (int)original.Category;
            icon.SelectedIndex = (int)original.Icon; color.SelectedIndex = (int)original.Color;
            favorite.IsToggled = original.IsFavorite;
            type.SelectedIndex = original.Parameters switch { OpenUrlParameters => 0, CopyTextParameters => 1, _ => 2 };
            parameter.Text = original.Parameters switch { OpenUrlParameters url => url.Url, CopyTextParameters copy => copy.Text, _ => "" };
        }
        name.TextChanged += (_, _) => UpdatePreview();
        icon.SelectedIndexChanged += (_, _) => UpdatePreview();
        color.SelectedIndexChanged += (_, _) => UpdatePreview();
        type.SelectedIndexChanged += (_, _) => UpdateParameter();
        UpdateParameter(); UpdatePreview();
        save.AutomationId = "SaveActionButton";
        save.Clicked += async (_, _) => await SaveAsync();
        var cancel = new ToolbarItem { Text = "Cancel" };
        cancel.Clicked += async (_, _) => { if (!busy) await Navigation.PopAsync(); };
        ToolbarItems.Add(cancel);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 16, Children = {
            previewCard,
            new Label { Text = "Make a little action your own.", FontSize = 18 },
            new Label { Text = "Name", FontAttributes = FontAttributes.Bold }, name,
            new Label { Text = "Action type", FontAttributes = FontAttributes.Bold }, type,
            parameterTitle, parameter, parameterHint,
            new Label { Text = "Category", FontAttributes = FontAttributes.Bold }, category,
            new Label { Text = "Icon", FontAttributes = FontAttributes.Bold }, icon,
            new Label { Text = "Tile color", FontAttributes = FontAttributes.Bold }, color,
            new HorizontalStackLayout { Spacing = 16, Children = { new Label { Text = "Favorite", VerticalOptions = LayoutOptions.Center }, favorite } },
            new Label { Text = "Saved privately on this iPhone. Clipboard text is stored as plain text; use a password manager for secrets.", FontSize = 14 },
            error, save
        } } };
        SemanticProperties.SetDescription(name, "Action name");
        SemanticProperties.SetDescription(parameter, "Web address or text to copy");
        SemanticProperties.SetDescription(favorite, "Favorite action");
    }

    private void UpdateParameter()
    {
        var isCounter = type.SelectedIndex == 2;
        parameter.IsVisible = !isCounter; parameterTitle.IsVisible = !isCounter;
        parameterTitle.Text = type.SelectedIndex == 0 ? "Web address" : "Text to copy";
        parameter.Placeholder = type.SelectedIndex == 0 ? "https://example.com" : "Text ready for your next paste";
        parameter.Keyboard = type.SelectedIndex == 0 ? Keyboard.Url : Keyboard.Default;
        parameterHint.Text = type.SelectedIndex switch {
            0 => "HTTP and HTTPS links open through iOS. Loading a website requires a connection.",
            1 => "Copies up to 10,000 characters when you tap Run.",
            _ => "Adds one to the existing counter and requests a widget refresh. iOS decides when the widget updates." };
    }

    private void UpdatePreview()
    {
        preview.Text = $"{DashboardStyle.Icon((ActionIcon)Math.Max(icon.SelectedIndex, 0))}  {(string.IsNullOrWhiteSpace(name.Text) ? "Your action" : name.Text)}";
        previewCard.BackgroundColor = DashboardStyle.TileColor((ActionColor)Math.Max(color.SelectedIndex, 0));
    }

    private async Task SaveAsync()
    {
        if (busy) return;
        busy = true; save.IsEnabled = false; error.Text = "Saving…";
        try
        {
            ActionParameters parameters = type.SelectedIndex switch {
                0 => new OpenUrlParameters((parameter.Text ?? "").Trim()),
                1 => new CopyTextParameters(parameter.Text ?? ""),
                _ => new IncrementCounterParameters() };
            var action = new ActionDefinition(original?.Id ?? Guid.NewGuid(), (name.Text ?? "").Trim(), parameters,
                (ActionCategory)category.SelectedIndex, (ActionIcon)icon.SelectedIndex, (ActionColor)color.SelectedIndex,
                favorite.IsToggled, original?.SortOrder ?? 0);
            validator.Validate(action);
            await repository.SaveAsync(action);
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            error.Text = ex is ActionValidationException or IOException ? ex.Message : "This action could not be saved. Try again.";
            SemanticScreenReader.Default.Announce(error.Text);
        }
        finally { busy = false; save.IsEnabled = true; }
    }
}
