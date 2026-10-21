using ControlStudio.Core.Actions;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

public sealed class ClipboardPage : ContentPage
{
    private readonly IActionRepository repository;
    private readonly IActionValidator validator;
    private readonly IActionExecutor executor;
    private readonly ILogger<ClipboardPage> logger;
    private readonly CollectionView items = new() { SelectionMode = SelectionMode.None, AutomationId = "ClipboardItems", Margin = new Thickness(16, 0, 16, 16) };
    private readonly Label status = SettingsControls.Text("", 13, secondary: true);
    private readonly RefreshView refresh = new();
    private CancellationTokenSource lifetime = new();
    private bool busy;
    private bool active;
    private bool reloadPending;

    public ClipboardPage(IActionRepository repository, IActionValidator validator, IActionExecutor executor, ILogger<ClipboardPage> logger)
    {
        this.repository = repository; this.validator = validator; this.executor = executor; this.logger = logger;
        Title = "Clipboard"; AutomationId = "ClipboardPage"; SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this);
        var add = new ToolbarItem { Text = "Add", AutomationId = "AddClipboardItem" };
        add.Clicked += async (_, _) => await GuardAsync(_ => Navigation.PushAsync(new ClipboardEditorPage(repository, validator)));
        ToolbarItems.Add(add);
        items.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 12 };
        items.ItemTemplate = new DataTemplate(() => new ClipboardTile(CopyAsync, ManageAsync));
        items.EmptyView = new VerticalStackLayout { Padding = new Thickness(16, 36), Spacing = 10, Children =
        {
            new StudioGlyph(StudioSymbol.Clipboard) { WidthRequest = 44, HeightRequest = 44, HorizontalOptions = LayoutOptions.Start },
            SettingsControls.Text("Your text, ready to copy.", 23, bold: true),
            SettingsControls.Text("Tap Add to save your first clipboard item.", 15, secondary: true)
        } };
        refresh.Content = items; refresh.SetDynamicResource(RefreshView.RefreshColorProperty, "AccentText");
        refresh.Refreshing += async (_, _) => await ReloadAsync();
        var heading = new VerticalStackLayout { Padding = new Thickness(20, 12, 20, 16), Spacing = 8, Children =
        {
            SettingsControls.Text("Save text. Copy in a tap.", 24, bold: true), status
        } };
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        layout.Add(heading); layout.Add(refresh, 0, 1); Content = layout; ShowStatus("");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true; lifetime.Dispose(); lifetime = new(); await ReloadAsync();
    }
    protected override void OnDisappearing() { active = false; lifetime.Cancel(); base.OnDisappearing(); }

    private async Task ReloadAsync()
    {
        if (busy) { reloadPending = true; return; }
        await GuardAsync(async token =>
        {
            reloadPending = false;
            var saved = await repository.ReadAsync(token); token.ThrowIfCancellationRequested();
            items.ItemsSource = StudioContent.ClipboardItems(saved);
        });
    }

    private Task CopyAsync(ActionDefinition item) => GuardAsync(async token =>
    {
        if (item.Parameters is not CopyTextParameters) return;
        var result = await executor.ExecuteAsync(item, token);
        ShowStatus(result.Message); SemanticScreenReader.Default.Announce(result.Message);
    });

    private Task ManageAsync(ActionDefinition item) => GuardAsync(async token =>
    {
        var choice = await DisplayActionSheetAsync(item.Name, "Cancel", "Delete", "Edit");
        token.ThrowIfCancellationRequested();
        if (choice == "Edit") { await Navigation.PushAsync(new ClipboardEditorPage(repository, validator, item)); return; }
        if (choice != "Delete" || !await DisplayAlertAsync("Delete clipboard item?", $"Remove “{item.Name}” from your saved clipboard?", "Delete", "Cancel")) return;
        await repository.DeleteAsync(item.Id, token);
        items.ItemsSource = StudioContent.ClipboardItems(await repository.ReadAsync(token));
        ShowStatus("Clipboard item deleted.");
    });

    private async Task GuardAsync(Func<CancellationToken, Task> operation)
    {
        if (busy || !active) return;
        busy = true; items.IsEnabled = false;
        try { await operation(lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // Saved text and names never enter logs.
            logger.LogWarning("Clipboard operation failed. ErrorType={ErrorType}", ex.GetType().Name);
            ShowStatus(ex is IOException or ActionValidationException or ActionExecutionException ? ex.Message : "Clipboard is unavailable. Pull down to try again.");
            SemanticScreenReader.Default.Announce(status.Text);
        }
        finally
        {
            busy = false; items.IsEnabled = true; refresh.IsRefreshing = false;
            if (active && reloadPending) { reloadPending = false; await ReloadAsync(); }
        }
    }

    private void ShowStatus(string message) { status.Text = message; status.IsVisible = !string.IsNullOrEmpty(message); }

    private sealed class ClipboardTile : ContentView
    {
        private readonly Label title = SettingsControls.Text("", 19, bold: true);
        private readonly Label text = SettingsControls.Text("", 14, secondary: true);
        private readonly Button copy = DashboardStyle.Button("Copy");
        private readonly Button more = new() { Text = "•••", FontSize = 20, BackgroundColor = Colors.Transparent, Padding = 0, WidthRequest = 44, HeightRequest = 44 };

        public ClipboardTile(Func<ActionDefinition, Task> copyItem, Func<ActionDefinition, Task> manage)
        {
            title.MaxLines = 2; text.MaxLines = 2; text.LineBreakMode = LineBreakMode.TailTruncation;
            more.SetDynamicResource(Button.TextColorProperty, "AccentText");
            var heading = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8 };
            heading.Add(title); heading.Add(more, 1);
            copy.HorizontalOptions = LayoutOptions.End; copy.MinimumWidthRequest = 100; copy.MinimumHeightRequest = 44;
            Content = new GlassCard(new VerticalStackLayout { Spacing = 8, Children = { heading, text, copy } });
            copy.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await copyItem(action); };
            more.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await manage(action); };
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is not ActionDefinition { Parameters: CopyTextParameters value } action) return;
            title.Text = action.Name; text.Text = value.Text;
            SemanticProperties.SetDescription(copy, $"Copy {action.Name}");
            SemanticProperties.SetDescription(more, $"Edit or delete {action.Name}");
        }
    }
}
