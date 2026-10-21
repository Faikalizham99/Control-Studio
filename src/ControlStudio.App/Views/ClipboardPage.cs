using ControlStudio.App.Platforms.iOS;
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
    private readonly Label description = SettingsControls.Text("", 15, secondary: true);
    private readonly RefreshView refresh = new();
    private readonly Dictionary<ClipboardKind, Segment> segments = [];
    private IReadOnlyList<ActionDefinition> saved = [];
    private ClipboardKind selected;
    private CancellationTokenSource lifetime = new();
    private bool busy, active, reloadPending;

    public ClipboardPage(IActionRepository repository, IActionValidator validator, IActionExecutor executor, ILogger<ClipboardPage> logger)
    {
        this.repository = repository; this.validator = validator; this.executor = executor; this.logger = logger;
        Title = "Clipboard"; AutomationId = "ClipboardPage"; SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this);
        var add = new ToolbarItem { Text = "Add", AutomationId = "AddClipboardItem" };
        add.Clicked += async (_, _) => await GuardAsync(_ => Navigation.PushAsync(new ClipboardEditorPage(repository, validator, selected)));
        ToolbarItems.Add(add);
        items.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 12 };
        items.ItemTemplate = new DataTemplate(() => new ClipboardTile(PrimaryAsync, SecondaryAsync, ManageAsync));
        refresh.Content = items; refresh.SetDynamicResource(RefreshView.RefreshColorProperty, "AccentText");
        refresh.Refreshing += async (_, _) => await ReloadAsync();
        var switcher = new Grid { ColumnSpacing = 4, Padding = 4 };
        foreach (var kind in Enum.GetValues<ClipboardKind>())
        {
            switcher.ColumnDefinitions.Add(new(GridLength.Star));
            var segment = new Segment(kind, () => { if (!busy) { selected = kind; Filter(); } });
            segments.Add(kind, segment); switcher.Add(segment, (int)kind);
        }
        var heading = new VerticalStackLayout { Padding = new Thickness(16, 12, 16, 16), Spacing = 12, Children =
        {
            SettingsControls.Text("Keep it in Clipboard.", 25, bold: true),
            SettingsControls.Rounded(switcher, 24), description, status
        } };
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        layout.Add(heading); layout.Add(refresh, 0, 1); Content = layout; ShowStatus(""); Filter();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true; lifetime.Dispose(); lifetime = new(); await ReloadAsync();
    }
    protected override void OnDisappearing() { active = false; lifetime.Cancel(); base.OnDisappearing(); }

    private void Filter()
    {
        foreach (var (kind, segment) in segments) segment.Update(kind == selected);
        items.ItemsSource = StudioContent.ClipboardItems(saved, selected);
        description.Text = selected switch
        {
            ClipboardKind.Text => "Save text. Copy it in a tap.",
            ClipboardKind.Photo => "Keep photos ready to copy, preview or share.",
            ClipboardKind.Link => "Your links, ready to copy or open.",
            _ => "Keep code readable. Preview, beautify and copy."
        };
        items.EmptyView = new VerticalStackLayout { Padding = new Thickness(16, 36), Spacing = 10, Children =
        {
            new StudioGlyph(Symbol(selected)) { WidthRequest = 44, HeightRequest = 44, HorizontalOptions = LayoutOptions.Start },
            SettingsControls.Text($"No saved {selected.ToString().ToLowerInvariant()} items yet.", 22, bold: true),
            SettingsControls.Text($"Tap Add to save your first {selected.ToString().ToLowerInvariant()}.", 15, secondary: true)
        } };
    }
    private async Task ReloadAsync()
    {
        if (busy) { reloadPending = true; return; }
        await GuardAsync(async token =>
        {
            reloadPending = false; saved = await repository.ReadAsync(token); token.ThrowIfCancellationRequested(); Filter();
        });
    }
    private Task PrimaryAsync(ActionDefinition item) => item.Parameters is SnippetParameters ? ViewAsync(item) : CopyAsync(item);
    private Task SecondaryAsync(ActionDefinition item) => item.Parameters switch
    {
        OpenUrlParameters => GuardAsync(async token => { var result = await executor.ExecuteAsync(item, token); ShowStatus(result.Message); }),
        SnippetParameters => CopyAsync(item), _ => ViewAsync(item)
    };
    private Task CopyAsync(ActionDefinition item) => GuardAsync(async token =>
    {
        validator.Validate(item); token.ThrowIfCancellationRequested();
        switch (item.Parameters)
        {
            case PhotoParameters photo: await ClipboardPhotoStore.CopyAsync(photo.AssetKey); break;
            case CopyTextParameters text: await Clipboard.Default.SetTextAsync(text.Text); break;
            case OpenUrlParameters link: await Clipboard.Default.SetTextAsync(link.Url); break;
            case SnippetParameters snippet: await Clipboard.Default.SetTextAsync(snippet.Code); break;
            default: return;
        }
        var message = item.Parameters is PhotoParameters ? "Photo copied. Paste it into an app that accepts images." : "Copied to the clipboard.";
        ShowStatus(message); SemanticScreenReader.Default.Announce(message);
    });
    private Task ViewAsync(ActionDefinition item) => GuardAsync(async _ =>
    {
        validator.Validate(item);
        if (item.Parameters is PhotoParameters photo) await Navigation.PushAsync(new ClipboardPhotoPage(item.Name, photo.AssetKey));
        else if (item.Parameters is SnippetParameters snippet)
            await Navigation.PushAsync(new SnippetPage(item.Name, snippet, async code =>
            {
                var updated = item with { Parameters = snippet with { Code = code } }; validator.Validate(updated); await repository.SaveAsync(updated);
            }));
    });
    private Task ManageAsync(ActionDefinition item) => GuardAsync(async token =>
    {
        var choice = await DisplayActionSheetAsync(item.Name, "Cancel", "Delete", "Edit");
        token.ThrowIfCancellationRequested();
        if (choice == "Edit") { await Navigation.PushAsync(new ClipboardEditorPage(repository, validator, StudioContent.Kind(item.Parameters)!.Value, item)); return; }
        if (choice != "Delete" || !await DisplayAlertAsync("Delete clipboard item?", $"Remove “{item.Name}” from your saved clipboard?", "Delete", "Cancel")) return;
        token.ThrowIfCancellationRequested();
        // Finish the delete before removing its file; cancellation cannot orphan a committed item.
        await repository.DeleteAsync(item.Id);
        if (item.Parameters is PhotoParameters photo) ClipboardPhotoStore.Remove(photo.AssetKey);
        if (!active) return;
        saved = await repository.ReadAsync(); Filter(); ShowStatus("Clipboard item deleted.");
    });
    private async Task GuardAsync(Func<CancellationToken, Task> operation)
    {
        if (busy || !active) return;
        busy = true; items.IsEnabled = false;
        try { await operation(lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
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
    private static StudioSymbol Symbol(ClipboardKind kind) => kind switch { ClipboardKind.Photo => StudioSymbol.Photo, ClipboardKind.Link => StudioSymbol.Link, ClipboardKind.Snippet => StudioSymbol.Snippets, _ => StudioSymbol.Text };

    private sealed class Segment : ContentView
    {
        private readonly Border surface;
        private readonly StudioGlyph icon;
        private readonly Label label;
        private readonly Button tap;
        private readonly ClipboardKind kind;
        public Segment(ClipboardKind kind, Action select)
        {
            this.kind = kind;
            icon = new StudioGlyph(Symbol(kind), useAccent: false) { WidthRequest = 21, HeightRequest = 21, HorizontalOptions = LayoutOptions.Center };
            label = SettingsControls.Text(kind.ToString(), 11, bold: true); label.HorizontalTextAlignment = TextAlignment.Center;
            surface = SettingsControls.Rounded(new VerticalStackLayout { Padding = new Thickness(2, 8), Spacing = 4, Children = { icon, label } }, 19);
            tap = SettingsControls.TapTarget(kind.ToString(), "ClipboardTab" + kind); tap.Clicked += (_, _) => select();
            Content = new Grid { Children = { surface, tap } };
            AutomationProperties.SetIsInAccessibleTree(label, false);
        }
        public void Update(bool selected)
        {
            if (selected)
            {
                surface.SetDynamicResource(BackgroundColorProperty, "AccentButton"); surface.Stroke = Colors.Transparent;
                label.SetDynamicResource(Label.TextColorProperty, "AccentButtonText"); icon.SetDynamicResource(StudioGlyph.TintProperty, "AccentButtonText");
            }
            else
            {
                surface.BackgroundColor = Colors.Transparent; surface.Stroke = Colors.Transparent;
                label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#626570"), Color.FromArgb("#AFB3BD"));
                icon.SetAppThemeColor(StudioGlyph.TintProperty, Color.FromArgb("#626570"), Color.FromArgb("#AFB3BD"));
            }
            SemanticProperties.SetDescription(tap, $"{kind}{(selected ? ", selected" : "")}");
        }
    }
    private sealed class ClipboardTile : ContentView
    {
        private readonly Label title = SettingsControls.Text("", 19, bold: true);
        private readonly Label text = SettingsControls.Text("", 14, secondary: true);
        private readonly Image photo = new() { Aspect = Aspect.AspectFit, HeightRequest = 130, HorizontalOptions = LayoutOptions.Start, WidthRequest = 160 };
        private readonly Button primary = DashboardStyle.Button("Copy");
        private readonly Button secondary = DashboardStyle.Button("Open");
        private readonly Button more = new() { Text = "•••", FontSize = 20, BackgroundColor = Colors.Transparent, Padding = 0, WidthRequest = 44, HeightRequest = 44 };
        public ClipboardTile(Func<ActionDefinition, Task> primaryAction, Func<ActionDefinition, Task> secondaryAction, Func<ActionDefinition, Task> manage)
        {
            title.MaxLines = 2; text.MaxLines = 3; text.LineBreakMode = LineBreakMode.TailTruncation;
            more.SetDynamicResource(Button.TextColorProperty, "AccentText");
            var heading = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8 };
            heading.Add(title); heading.Add(more, 1);
            var actions = new HorizontalStackLayout { Spacing = 10, HorizontalOptions = LayoutOptions.End, Children = { secondary, primary } };
            primary.MinimumWidthRequest = secondary.MinimumWidthRequest = 88;
            primary.MinimumHeightRequest = secondary.MinimumHeightRequest = 44;
            Content = new GlassCard(new VerticalStackLayout { Spacing = 10, Children = { heading, text, photo, actions } });
            primary.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await primaryAction(action); };
            secondary.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await secondaryAction(action); };
            more.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await manage(action); };
        }
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            photo.Source = null; photo.IsVisible = false;
            if (BindingContext is not ActionDefinition action) return;
            title.Text = action.Name; primary.Text = action.Parameters is SnippetParameters ? "View code" : "Copy";
            secondary.IsVisible = action.Parameters is not CopyTextParameters;
            secondary.Text = action.Parameters switch { OpenUrlParameters => "Open", SnippetParameters => "Copy", _ => "Preview" };
            text.FontFamily = action.Parameters is SnippetParameters ? "Menlo" : null;
            text.Text = action.Parameters switch
            {
                CopyTextParameters value => value.Text, OpenUrlParameters value => value.Url,
                SnippetParameters value => StudioContent.LanguageName(value.Language) + "\n" + value.Code,
                PhotoParameters value => ClipboardPhotoStore.GetPath(value.AssetKey) is null ? "Photo missing. Choose it again in Edit." : "Photo · saved on this iPhone", _ => ""
            };
            if (action.Parameters is PhotoParameters p && ClipboardPhotoStore.GetThumbnailPath(p.AssetKey) is { } path)
            { photo.Source = path; photo.IsVisible = true; SemanticProperties.SetDescription(photo, action.Name); }
            SemanticProperties.SetDescription(primary, $"{primary.Text} {action.Name}");
            SemanticProperties.SetDescription(secondary, $"{secondary.Text} {action.Name}");
            SemanticProperties.SetDescription(more, $"Edit or delete {action.Name}");
        }
    }
}
