using ControlStudio.App.Platforms.iOS;
using ControlStudio.Core.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;

namespace ControlStudio.App.Views;

public sealed class ClipboardPage : ContentPage
{
    private readonly IActionRepository repository;
    private readonly IActionValidator validator;
    private readonly IActionExecutor executor;
    private readonly ILogger<ClipboardPage> logger;
    private readonly AppearancePreferences preferences;
    private readonly AppleColorPicker colourPicker;
    private readonly CollectionView items = new() { SelectionMode = SelectionMode.None, ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems, AutomationId = "ClipboardItems", Margin = new Thickness(16, 0, 16, 16) };
    private readonly Label status = SettingsControls.Text("", 13, secondary: true);
    private readonly Label description = SettingsControls.Text("", 15, secondary: true);
    private readonly RefreshView refresh = new();
    private readonly Dictionary<ClipboardKind, ClipboardSegment> segments = [];
    private readonly HashSet<Guid> concealedItems = [];
    private IReadOnlyList<ActionDefinition> saved = [];
    private ClipboardKind selected;
    private CancellationTokenSource lifetime = new();
    private bool busy, active, reloadPending;

    public ClipboardPage(IActionRepository repository, IActionValidator validator, IActionExecutor executor, ILogger<ClipboardPage> logger, AppearancePreferences preferences, AppleColorPicker colourPicker)
    {
        this.repository = repository; this.validator = validator; this.executor = executor; this.logger = logger;
        this.preferences = preferences; this.colourPicker = colourPicker;
        Title = "Clipboard"; AutomationId = "ClipboardPage"; SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this);
        var add = new ToolbarItem { Text = "Add", AutomationId = "AddClipboardItem" };
        add.Clicked += async (_, _) => await GuardAsync(_ => Navigation.PushAsync(new ClipboardEditorPage(repository, validator, preferences, colourPicker, selected)));
        ToolbarItems.Add(add);
        items.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 12 };
        items.ItemTemplate = new DataTemplate(() => new ClipboardTile(CopyAsync, SecondaryAsync, EditAsync, DeleteAsync, ShareLinkAsync, concealedItems));
        refresh.Content = items; refresh.SetDynamicResource(RefreshView.RefreshColorProperty, "AccentText");
        refresh.Refreshing += async (_, _) => await ReloadAsync();
        var switcher = new Grid { ColumnSpacing = 4, Padding = 4 };
        foreach (var kind in Enum.GetValues<ClipboardKind>())
        {
            switcher.ColumnDefinitions.Add(new(GridLength.Star));
            var segment = new ClipboardSegment(kind, Symbol(kind), () => { if (!busy && selected != kind) { selected = kind; ShowStatus(""); Filter(); } });
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
            _ => ""
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
    private Task SecondaryAsync(ActionDefinition item) => item.Parameters switch
    {
        OpenUrlParameters => GuardAsync(async token => { var result = await executor.ExecuteAsync(item, token); ShowStatus(result.Message); }),
        _ => ViewAsync(item)
    };
    private Task CopyAsync(ActionDefinition item) => GuardAsync(async token =>
    {
        validator.Validate(item); token.ThrowIfCancellationRequested();
        switch (item.Parameters)
        {
            case PhotoParameters photo: await ClipboardPhotoStore.CopyAsync(photo.AssetKey); break;
            case CopyTextParameters text: await Clipboard.Default.SetTextAsync(text.Text); break;
            case OpenUrlParameters link: await Clipboard.Default.SetTextAsync(link.Url); break;
            default: return;
        }
        var message = item.Parameters is PhotoParameters ? "Photo copied. Paste it into an app that accepts images." : "Copied to the clipboard.";
        ShowStatus(message); SemanticScreenReader.Default.Announce(message);
    });
    private Task ViewAsync(ActionDefinition item) => GuardAsync(async _ =>
    {
        validator.Validate(item);
        if (item.Parameters is PhotoParameters photo) await Navigation.PushAsync(new ClipboardPhotoPage(item.Name, photo.AssetKey));
    });
    private Task ShareLinkAsync(ActionDefinition item) => GuardAsync(async token =>
    {
        validator.Validate(item); token.ThrowIfCancellationRequested();
        if (item.Parameters is OpenUrlParameters link) await Share.Default.RequestAsync(new ShareTextRequest { Title = item.Name, Uri = link.Url });
    });
    private Task EditAsync(ActionDefinition item) => GuardAsync(async token =>
    {
        validator.Validate(item);
        if (StudioContent.Kind(item.Parameters) is not { } kind) return;
        token.ThrowIfCancellationRequested();
        await Navigation.PushAsync(new ClipboardEditorPage(repository, validator, preferences, colourPicker, kind, item));
    });
    private Task DeleteAsync(ActionDefinition item) => GuardAsync(async token =>
    {
        if (!await DisplayAlertAsync("Delete clipboard item?", $"Remove “{item.Name}” from your saved clipboard?", "Delete", "Cancel")) return;
        token.ThrowIfCancellationRequested();
        // Finish the delete before removing its file; cancellation cannot orphan a committed item.
        await repository.DeleteAsync(item.Id);
        concealedItems.Remove(item.Id);
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
    private static StudioSymbol Symbol(ClipboardKind kind) => kind switch { ClipboardKind.Photo => StudioSymbol.Photo, ClipboardKind.Link => StudioSymbol.Link, _ => StudioSymbol.Text };

    private sealed class ClipboardTile : ContentView
    {
        private const int PreviewLines = 3;
        private readonly ClipboardHeaderView header = new();
        private readonly Label text = SettingsControls.Text("", 14, secondary: true);
        private readonly ClipboardLinkLabel link;
        private Label ActiveLabel => link.IsVisible ? link : text;
        private readonly Button expand = new()
        {
            Text = "\u2026 Show more", FontSize = 13, FontAttributes = FontAttributes.Bold, BackgroundColor = Colors.Transparent,
            Padding = new Thickness(0, 0, 12, 0), MinimumHeightRequest = 44, MinimumWidthRequest = 44,
            HorizontalOptions = LayoutOptions.Start, IsVisible = false, AutomationId = "ToggleClipboardPreview"
        };
        private bool expanded, overflowCheckQueued;
        private readonly Image photo = new() { Aspect = Aspect.AspectFit, HeightRequest = 130, HorizontalOptions = LayoutOptions.Start, WidthRequest = 160 };
        private readonly ClipboardIconButton copy = new(StudioSymbol.Clipboard, "Copy", primary: true);
        private readonly ClipboardIconButton open = new(StudioSymbol.Open, "Open link");
        private readonly ClipboardIconButton preview = new(StudioSymbol.Expand, "Preview photo");
        private readonly ClipboardIconButton edit = new(StudioSymbol.Edit, "Edit");
        private readonly ClipboardIconButton delete = new(StudioSymbol.Delete, "Delete", destructive: true);
        private readonly ClipboardIconButton visibility = new(StudioSymbol.Preview, "Hide content");
        private readonly ClipboardConcealView conceal;
        public ClipboardTile(Func<ActionDefinition, Task> copyAction, Func<ActionDefinition, Task> secondaryAction,
            Func<ActionDefinition, Task> editAction, Func<ActionDefinition, Task> deleteAction, Func<ActionDefinition, Task> shareAction, ISet<Guid> concealedItems)
        {
            link = new ClipboardLinkLabel(secondaryAction, copyAction, shareAction) { IsVisible = false };
            text.MaxLines = PreviewLines; text.LineBreakMode = LineBreakMode.TailTruncation;
            expand.SetDynamicResource(Button.TextColorProperty, "AccentText");
            expand.Clicked += (_, _) =>
            {
                expanded = !expanded;
                ActiveLabel.MaxLines = expanded ? -1 : PreviewLines;
                ActiveLabel.LineBreakMode = expanded ? LineBreakMode.WordWrap : LineBreakMode.TailTruncation;
                UpdateExpandCaption();
                InvalidateMeasure();
            };
            foreach (var label in new Label[] { text, link })
            {
                label.SizeChanged += (_, _) => QueueOverflowCheck();
                label.HandlerChanged += (_, _) => QueueOverflowCheck();
                label.Loaded += (_, _) => QueueOverflowCheck();
                label.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName is nameof(Label.FontSize) or nameof(Label.FontFamily) or nameof(Label.FontAttributes) or nameof(Label.FontAutoScalingEnabled)) QueueOverflowCheck();
                };
            }
            var actions = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End, Children = { visibility, edit, delete, open, preview, copy } };
            var content = new VerticalStackLayout { Spacing = 0, Children = { text, link, expand } };
            var divider = new BoxView { HeightRequest = 1, HorizontalOptions = LayoutOptions.Fill, InputTransparent = true };
            divider.SetAppThemeColor(BoxView.ColorProperty, Color.FromArgb("#D2D7E1"), Colors.White.WithAlpha(.18f));
            AutomationProperties.SetIsInAccessibleTree(divider, false);
            conceal = new ClipboardConcealView(new VerticalStackLayout { Spacing = 12, Children = { content, photo } }, concealedItems);
            var body = new VerticalStackLayout { Padding = 12, Spacing = 12, Children = { conceal, actions } };
            Content = new GlassCard(new VerticalStackLayout { Spacing = 0, Children = { header, divider, body } }, 0);
            visibility.Clicked += (_, _) => { conceal.Toggle(); UpdateVisibilityCaption(); };
            copy.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await copyAction(action); };
            edit.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await editAction(action); };
            delete.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await deleteAction(action); };
            foreach (var button in new[] { open, preview })
                button.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await secondaryAction(action); };
        }
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            // Apply concealment before installing a recycled tile's new text/image.
            conceal.Bind((BindingContext as ActionDefinition)?.Id); UpdateVisibilityCaption();
            // CollectionView reuses tiles; a different item always starts with its own collapsed preview.
            expanded = false; expand.IsVisible = false;
            text.MaxLines = PreviewLines; text.LineBreakMode = LineBreakMode.TailTruncation;
            link.Item = null; link.IsVisible = false; link.MaxLines = PreviewLines; link.LineBreakMode = LineBreakMode.TailTruncation; text.IsVisible = true;
            photo.Source = null; photo.IsVisible = false;
            open.IsVisible = preview.IsVisible = false;
            if (BindingContext is not ActionDefinition action) { header.SetItem("", null); text.Text = ""; return; }
            header.SetItem(action.Name, action.HeaderColorHex);
            open.IsVisible = action.Parameters is OpenUrlParameters;
            preview.IsVisible = action.Parameters is PhotoParameters;
            text.Text = action.Parameters switch
            {
                CopyTextParameters value => value.Text,
                PhotoParameters value => ClipboardPhotoStore.GetPath(value.AssetKey) is null ? "Photo missing. Choose it again in Edit." : "Photo · saved on this iPhone", _ => ""
            };
            if (action.Parameters is OpenUrlParameters) { link.Item = action; link.IsVisible = true; text.IsVisible = false; }
            if (action.Parameters is PhotoParameters p && ClipboardPhotoStore.GetThumbnailPath(p.AssetKey) is { } path)
            { photo.Source = path; photo.IsVisible = true; SemanticProperties.SetDescription(photo, action.Name); }
            copy.Describe("Copy", action.Name); edit.Describe("Edit", action.Name); delete.Describe("Delete", action.Name);
            open.Describe("Open link", action.Name); preview.Describe("Preview photo", action.Name);
            UpdateExpandCaption(); QueueOverflowCheck();
        }

        private void UpdateVisibilityCaption()
        {
            visibility.Symbol = conceal.IsConcealed ? StudioSymbol.EyeOff : StudioSymbol.Preview;
            visibility.Describe(conceal.IsConcealed ? "Show content for" : "Hide content for", (BindingContext as ActionDefinition)?.Name ?? "item");
        }

        private void UpdateExpandCaption()
        {
            expand.Text = expanded ? "Show less" : "\u2026 Show more";
            var name = (BindingContext as ActionDefinition)?.Name ?? "item";
            SemanticProperties.SetDescription(expand, $"{(expanded ? "Collapse" : "Expand")} content for {name}");
            SemanticProperties.SetHint(expand, expanded ? "Shows a three-line preview." : "Shows the full saved content.");
        }

        private void QueueOverflowCheck()
        {
            if (overflowCheckQueued) return;
            overflowCheckQueued = true;
            // Wait for the native label's text, scaled font and actual card width to be applied.
            Dispatcher.Dispatch(() =>
            {
                overflowCheckQueued = false;
                if (BindingContext is not ActionDefinition action || action.Parameters is not (CopyTextParameters or OpenUrlParameters))
                { expand.IsVisible = false; return; }
                if (ClipboardTextMeasurement.ExceedsLines(ActiveLabel, PreviewLines) is not { } overflows) return;
                expand.IsVisible = overflows;
                if (!overflows && expanded)
                {
                    expanded = false; ActiveLabel.MaxLines = PreviewLines; ActiveLabel.LineBreakMode = LineBreakMode.TailTruncation;
                    UpdateExpandCaption();
                }
            });
        }
    }
}
