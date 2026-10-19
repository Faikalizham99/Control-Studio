using ControlStudio.Core.Actions;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

public sealed class ShortcutsPage : ContentPage
{
    private readonly IActionRepository repository;
    private readonly IActionValidator validator;
    private readonly IActionExecutor executor;
    private readonly ILogger<ShortcutsPage> logger;
    private readonly CollectionView tiles = new() { SelectionMode = SelectionMode.None, AutomationId = "ActionTiles" };
    private readonly SearchBar search = new() { Placeholder = "Find an action", AutomationId = "ActionSearch" };
    private readonly Picker category = new() { Title = "Category", ItemsSource = new[] { "All categories" }.Concat(Enum.GetNames<ActionCategory>()).ToArray(), SelectedIndex = 0 };
    private readonly Switch favorites = new();
    private readonly Label status = new() { FontSize = 14, AutomationId = "DashboardStatus" };
    private IReadOnlyList<ActionDefinition> actions = [];
    private bool busy;
    private CancellationTokenSource lifetime = new();

    public ShortcutsPage(IActionRepository repository, IActionValidator validator, IActionExecutor executor, ILogger<ShortcutsPage> logger)
    {
        this.repository = repository; this.validator = validator; this.executor = executor; this.logger = logger;
        Title = "Shortcuts"; AutomationId = "ShortcutsPage";
        DashboardStyle.Page(this);
        var add = new ToolbarItem { Text = "Add", AutomationId = "AddActionButton" };
        add.Clicked += async (_, _) => await GuardAsync(_ => Navigation.PushAsync(new ActionEditorPage(repository, validator)));
        var refresh = new ToolbarItem { Text = "Refresh" };
        refresh.Clicked += async (_, _) => await ReloadAsync();
        ToolbarItems.Add(add); ToolbarItems.Add(refresh);
        search.TextChanged += (_, _) => Filter(); category.SelectedIndexChanged += (_, _) => Filter();
        favorites.Toggled += (_, _) => Filter();
        SemanticProperties.SetDescription(favorites, "Show only favorites");
        tiles.ItemTemplate = new DataTemplate(() => new ActionTile(RunAsync, OptionsAsync));
        tiles.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 14 };
        tiles.Margin = new Thickness(24, 0, 24, 12);
        var header = new VerticalStackLayout { Padding = new Thickness(24, 16, 24, 14), Spacing = 10, Children = {
            new Label { Text = "Your everyday, a tap away.", FontSize = 28, FontAttributes = FontAttributes.Bold },
            new Label { Text = "Web links, ready-to-copy text and your shared counter.", FontSize = 16 }, search, category,
            new HorizontalStackLayout { Spacing = 12, Children = { new Label { Text = "Favorites only", VerticalOptions = LayoutOptions.Center }, favorites } }, status
        } };
        var layout = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        layout.Add(header); layout.Add(tiles, 0, 1); Content = layout;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing(); lifetime.Dispose(); lifetime = new(); await ReloadAsync();
    }
    protected override void OnDisappearing() { lifetime.Cancel(); base.OnDisappearing(); }

    private void Filter()
    {
        var query = search.Text?.Trim() ?? "";
        var visible = actions.Where(x => (!favorites.IsToggled || x.IsFavorite) &&
            (category.SelectedIndex <= 0 || (int)x.Category == category.SelectedIndex - 1) &&
            x.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        tiles.ItemsSource = visible;
        tiles.EmptyView = new VerticalStackLayout { Padding = new Thickness(0, 32), Spacing = 12, Children = {
            new Label { Text = actions.Count == 0 ? "Make room for your first action." : "No matching actions.", FontSize = 23, FontAttributes = FontAttributes.Bold },
            new Label { Text = actions.Count == 0 ? "Tap Add to save a link, copy text or update your counter. Your actions stay on this iPhone." : "Try a different search or category, or turn off Favorites only.", FontSize = 16 }
        } };
    }

    private async Task ReloadAsync()
    {
        await GuardAsync(async token => { actions = await repository.ReadAsync(token); Filter(); status.Text = $"{actions.Count} saved actions"; });
    }

    private Task RunAsync(ActionDefinition action) => GuardAsync(async token =>
    {
        var result = await executor.ExecuteAsync(action, token); status.Text = result.Message;
        SemanticScreenReader.Default.Announce(result.Message);
        logger.LogInformation("Dashboard action completed. Kind={Kind}", DashboardStyle.Kind(action.Parameters));
    });

    private async Task OptionsAsync(ActionDefinition action)
    {
        if (busy) return;
        // Editing and management are explicit controls, separate from Run.
        string? choice;
        busy = true; tiles.IsEnabled = false;
        try { choice = await DisplayActionSheetAsync(action.Name, "Cancel", "Delete", "Edit", action.IsFavorite ? "Remove favorite" : "Add favorite", "Move earlier", "Move later"); }
        finally { busy = false; tiles.IsEnabled = true; }
        if (choice == "Edit") { await Navigation.PushAsync(new ActionEditorPage(repository, validator, action)); return; }
        if (choice is null or "Cancel") return;
        if (choice == "Delete" && !await DisplayAlertAsync("Delete action?", $"Remove “{action.Name}” from this dashboard?", "Delete", "Cancel")) return;
        await GuardAsync(async token =>
        {
            switch (choice)
            {
                case "Delete": await repository.DeleteAsync(action.Id, token); break;
                case "Add favorite": case "Remove favorite": await repository.SaveAsync(action with { IsFavorite = !action.IsFavorite }, token); break;
                case "Move earlier": await repository.MoveAsync(action.Id, -1, token); break;
                case "Move later": await repository.MoveAsync(action.Id, 1, token); break;
                default: return;
            }
            actions = await repository.ReadAsync(token); Filter(); status.Text = "Dashboard saved.";
        });
    }

    private async Task GuardAsync(Func<CancellationToken, Task> operation)
    {
        if (busy) return;
        busy = true; tiles.IsEnabled = false;
        try { await operation(lifetime.Token); }
        catch (OperationCanceledException) { status.Text = "Ready."; }
        catch (Exception ex)
        {
            // Never log saved URLs, clipboard text, action names, or native exception messages.
            logger.LogWarning("Dashboard operation failed. ErrorType={ErrorType}", ex.GetType().Name);
            status.Text = ex is ActionValidationException or ActionExecutionException or IOException ? ex.Message : "The dashboard is unavailable. Try Refresh.";
            SemanticScreenReader.Default.Announce(status.Text);
        }
        finally { busy = false; tiles.IsEnabled = true; }
    }

    private sealed class ActionTile : ContentView
    {
        private readonly Label name = new() { FontSize = 22, FontAttributes = FontAttributes.Bold };
        private readonly Label detail = new() { FontSize = 14 };
        private readonly Label icon = new() { FontSize = 26, TextColor = Colors.White, VerticalTextAlignment = TextAlignment.Center, HorizontalTextAlignment = TextAlignment.Center, WidthRequest = 52, HeightRequest = 52 };
        private readonly Button run = DashboardStyle.Button("Run");
        private readonly Button options = new() { Text = "Manage", MinimumHeightRequest = 48, CornerRadius = 14 };
        private readonly Border card;
        public ActionTile(Func<ActionDefinition, Task> execute, Func<ActionDefinition, Task> manage)
        {
            var top = new Grid { ColumnSpacing = 14, ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) } };
            top.Add(icon); top.Add(new VerticalStackLayout { Spacing = 5, Children = { name, detail } }, 1);
            var buttons = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) } };
            buttons.Add(run); buttons.Add(options, 1);
            card = DashboardStyle.Card(new VerticalStackLayout { Spacing = 16, Children = { top, buttons } });
            Content = card;
            name.TextColor = Colors.White; detail.TextColor = Colors.White;
            run.BackgroundColor = Colors.White; run.TextColor = Colors.Black;
            options.BackgroundColor = Colors.Transparent; options.TextColor = Colors.White;
            options.BorderColor = Colors.White; options.BorderWidth = 1;
            run.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await execute(action); };
            options.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await manage(action); };
        }
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not ActionDefinition action) return;
            name.Text = action.Name; detail.Text = $"{DashboardStyle.Kind(action.Parameters)} · {action.Category}{(action.IsFavorite ? " · ★ Favorite" : "")}";
            icon.Text = DashboardStyle.Icon(action.Icon); icon.BackgroundColor = DashboardStyle.TileColor(action.Color);
            card.BackgroundColor = DashboardStyle.TileColor(action.Color);
            SemanticProperties.SetDescription(icon, action.Icon.ToString());
            SemanticProperties.SetDescription(run, $"Run {action.Name}");
            SemanticProperties.SetDescription(options, $"Manage {action.Name}");
        }
    }
}
