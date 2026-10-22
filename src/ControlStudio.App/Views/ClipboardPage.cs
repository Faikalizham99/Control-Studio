using ControlStudio.App.Platforms.iOS;
using ControlStudio.Core.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using System.Collections.ObjectModel;

namespace ControlStudio.App.Views;

public sealed class ClipboardPage : ContentPage
{
    private readonly IActionRepository repository;
    private readonly IActionValidator validator;
    private readonly IActionExecutor executor;
    private readonly ILogger<ClipboardPage> logger;
    private readonly AppearancePreferences preferences;
    private readonly AppleColorPicker colourPicker;
    // The list owns the full scroll viewport; each tile owns its horizontal inset.
    // This leaves a gutter for the native scroll indicator outside the cards.
    private readonly CollectionView items = new() { SelectionMode = SelectionMode.None, ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems, AutomationId = "ClipboardItems", Margin = new Thickness(0, 0, 0, 16) };
    private readonly Label status = SettingsControls.Text("", 13, secondary: true);
    private readonly Label description = SettingsControls.Text("", 15, secondary: true);
    private readonly RefreshView refresh = new();
    private readonly Dictionary<ClipboardKind, ClipboardSegment> segments = [];
    private readonly ClipboardVisibilityPreferences visibility = new(Preferences.Default);
    private readonly ClipboardCopyFeedback copyFeedback = new();
    private Window? observedWindow;
    private int copyEpoch;
    private bool foreground;
    private readonly PageActionButton arrange = new("Arrange", "ArrangeClipboardItems");
    private readonly PageActionButton add = new("Add", "AddClipboardItem");
    private readonly PageActionButton back = new("", "BackFromClipboard", ProfileSymbol.Back);
    private readonly Label arrangeHint = SettingsControls.Text("", 13, secondary: true);
    private ObservableCollection<ActionDefinition> visible = [];
    private ClipboardKind? displayedKind;
    private IReadOnlyList<ActionDefinition> saved = [];
    private ClipboardKind selected;
    private CancellationTokenSource lifetime = new();
    private bool busy, active, reloadPending, loaded;

    public ClipboardPage(IActionRepository repository, IActionValidator validator, IActionExecutor executor, ILogger<ClipboardPage> logger, AppearancePreferences preferences, AppleColorPicker colourPicker)
    {
        this.repository = repository; this.validator = validator; this.executor = executor; this.logger = logger;
        this.preferences = preferences; this.colourPicker = colourPicker;
        Title = "Clipboard"; AutomationId = "ClipboardPage"; SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this);
        NavigationPage.SetHasNavigationBar(this, false);
        back.Clicked += async (_, _) => await GuardAsync(_ => Navigation.PopAsync());
        add.Clicked += async (_, _) => await GuardAsync(_ => Navigation.PushAsync(new ClipboardEditorPage(repository, validator, preferences, colourPicker, selected)));
        arrange.Clicked += async (_, _) => await GuardAsync(_ =>
        {
            var navigation = new NavigationPage(new ClipboardArrangePage(repository, logger, selected, visible.ToArray()));
            Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page.SetModalPresentationStyle(navigation.On<Microsoft.Maui.Controls.PlatformConfiguration.iOS>(),
                Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.UIModalPresentationStyle.FullScreen);
            return Navigation.PushModalAsync(navigation);
        });
        items.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 12 };
        items.ItemTemplate = new DataTemplate(() => new ClipboardTile(CopyAsync, SecondaryAsync, EditAsync, DeleteAsync, ShareLinkAsync, PinAsync, visibility, VisibilityFailed, copyFeedback));
        refresh.Content = items; refresh.SetDynamicResource(RefreshView.RefreshColorProperty, "AccentText");
        refresh.Refreshing += async (_, _) => await ReloadAsync();
        var switcher = new Grid { ColumnSpacing = 4, Padding = 4 };
        foreach (var kind in Enum.GetValues<ClipboardKind>())
        {
            switcher.ColumnDefinitions.Add(new(GridLength.Star));
            var segment = new ClipboardSegment(kind, Symbol(kind), () => { if (!busy && selected != kind) { selected = kind; ResetCopyFeedback(); ShowStatus(""); Filter(); } });
            segments.Add(kind, segment); switcher.Add(segment, (int)kind);
        }
        var heading = new VerticalStackLayout { Padding = new Thickness(16, 12, 16, 16), Spacing = 12, Children =
        {
            SettingsControls.Text("Keep it in Clipboard.", 25, bold: true),
            SettingsControls.Rounded(switcher, 24), description, arrangeHint, status
        } };
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) } };
        layout.Add(PageActionBar.Clipboard(back, arrange, add)); layout.Add(heading, 0, 1); layout.Add(refresh, 0, 2);
        var toast = new ClipboardToastView(copyFeedback); Grid.SetRowSpan(toast, 3); layout.Add(toast);
        Content = layout; ShowStatus(""); Filter();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = foreground = true; lifetime.Dispose(); lifetime = new();
        ObserveWindow(); await ReloadAsync();
    }
    protected override void OnDisappearing()
    {
        active = foreground = false; lifetime.Cancel(); ResetCopyFeedback(); StopObservingWindow(); base.OnDisappearing();
    }

    private void ObserveWindow()
    {
        StopObservingWindow(); observedWindow = Window;
        if (observedWindow is null) return;
        observedWindow.Stopped += WindowStopped; observedWindow.Resumed += WindowResumed;
    }
    private void StopObservingWindow()
    {
        if (observedWindow is null) return;
        observedWindow.Stopped -= WindowStopped; observedWindow.Resumed -= WindowResumed; observedWindow = null;
    }
    private void WindowStopped(object? sender, EventArgs args) { foreground = false; ResetCopyFeedback(); }
    private void WindowResumed(object? sender, EventArgs args) => foreground = active;
    private void ResetCopyFeedback() { copyEpoch++; copyFeedback.Clear(); }

    private void Filter()
    {
        foreach (var (kind, segment) in segments) segment.Update(kind == selected);
        var desired = StudioContent.ClipboardItems(saved, selected);
        if (displayedKind != selected)
        {
            visible = new ObservableCollection<ActionDefinition>(desired); items.ItemsSource = visible; displayedKind = selected;
        }
        else ClipboardCollection.Update(visible, desired);
        UpdateActions();
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
            reloadPending = false; saved = await repository.ReadAsync(token); token.ThrowIfCancellationRequested(); loaded = true; Filter();
        });
    }
    private Task SecondaryAsync(ActionDefinition item) => item.Parameters switch
    {
        OpenUrlParameters => GuardAsync(async token => { var result = await executor.ExecuteAsync(item, token); ShowStatus(result.Message); }),
        _ => ViewAsync(item)
    };
    private Task CopyAsync(ActionDefinition item) => GuardAsync(async token =>
    {
        var epoch = copyEpoch; copyFeedback.Clear(); ShowStatus("");
        try
        {
            validator.Validate(item); token.ThrowIfCancellationRequested();
            switch (item.Parameters)
            {
                case PhotoParameters photo: await ClipboardPhotoStore.CopyAsync(photo.AssetKey); break;
                case CopyTextParameters text: await Clipboard.Default.SetTextAsync(text.Text); break;
                case OpenUrlParameters link: await Clipboard.Default.SetTextAsync(link.Url); break;
                default: return;
            }
            token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning("Clipboard copy failed. ErrorType={ErrorType}", ex.GetType().Name);
            if (active && foreground && epoch == copyEpoch && !token.IsCancellationRequested)
            {
                var message = ex is IOException or ActionValidationException ? ex.Message : "Could not copy. Please try again.";
                ShowStatus(message);
                _ = copyFeedback.ErrorAsync(); AnnounceCopyFeedback(message);
            }
            return;
        }
        if (!active || !foreground || epoch != copyEpoch) return;
        _ = copyFeedback.SuccessAsync(item.Id, UIKit.UIAccessibility.IsVoiceOverRunning);
        AnnounceCopyFeedback(item.Parameters is PhotoParameters ? "Photo copied to clipboard. Paste it into an app that accepts images." : "Copied to clipboard.");
        // Optional feedback must never turn a successful clipboard write into a reported failure.
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); }
        catch (Exception ex) { logger.LogDebug("Copy haptic unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
    });
    private void AnnounceCopyFeedback(string message)
    {
        try { SemanticScreenReader.Default.Announce(message); }
        catch (Exception ex) { logger.LogDebug("Copy announcement unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
    }
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
        try { visibility.Remove(item.Id); }
        catch (Exception ex) { logger.LogWarning("Deleted clipboard item's visibility cleanup failed. ErrorType={ErrorType}", ex.GetType().Name); }
        if (item.Parameters is PhotoParameters photo) ClipboardPhotoStore.Remove(photo.AssetKey);
        if (!active) return;
        saved = await repository.ReadAsync(); Filter(); ShowStatus("Clipboard item deleted.");
    });
    private Task PinAsync(ActionDefinition item) => GuardAsync(async token =>
    {
        await repository.SetPinnedAsync(item.Id, !item.IsFavorite, token);
        saved = await repository.ReadAsync();
        if (!active) { reloadPending = true; return; }
        Filter(); ShowStatus(item.IsFavorite ? "Item unpinned." : "Item pinned to the top.");
        SemanticScreenReader.Default.Announce(status.Text);
    });
    private async Task GuardAsync(Func<CancellationToken, Task> operation)
    {
        if (busy || !active) return;
        busy = true; items.IsEnabled = false; UpdateActions();
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
            busy = false; items.IsEnabled = true; UpdateActions(); refresh.IsRefreshing = false;
            if (active && reloadPending) { reloadPending = false; await ReloadAsync(); }
        }
    }
    private void UpdateActions()
    {
        var availability = ClipboardArrangeAvailability.For(visible, selected);
        back.IsEnabled = add.IsEnabled = !busy;
        back.DisabledReason = add.DisabledReason = "Please wait for the current action to finish.";
        arrange.DisabledReason = busy ? "Please wait for the current action to finish." : !loaded ? "Load your items to arrange." : availability.Reason;
        arrange.IsEnabled = loaded && !busy && availability.CanArrange;
        arrangeHint.Text = availability.Reason; arrangeHint.IsVisible = loaded && !availability.CanArrange;
    }
    private void ShowStatus(string message) { status.Text = message; status.IsVisible = !string.IsNullOrEmpty(message); }
    private void VisibilityFailed(ClipboardVisibilityErrorEventArgs args)
    {
        logger.LogWarning("Clipboard visibility {Operation} failed. ErrorType={ErrorType}", args.Loading ? "load" : "save", args.Error.GetType().Name);
        ShowStatus(args.Loading ? "Could not load visibility. Content is kept hidden. Try reopening Clipboard." : "Could not save visibility. Try again.");
        if (active) SemanticScreenReader.Default.Announce(status.Text);
    }
    private static StudioSymbol Symbol(ClipboardKind kind) => kind switch { ClipboardKind.Photo => StudioSymbol.Photo, ClipboardKind.Link => StudioSymbol.Link, _ => StudioSymbol.Text };

    private sealed class ClipboardTile : ContentView
    {
        private const int PreviewLines = 3;
        private readonly ClipboardHeaderView header;
        private readonly ClipboardIconButton pin = new(StudioSymbol.Pin, "Pin");
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
        private Guid? boundId;
        private readonly Image photo = new() { Aspect = Aspect.AspectFit, HeightRequest = 130, HorizontalOptions = LayoutOptions.Start, WidthRequest = 160 };
        private readonly ClipboardCopyButton copy;
        private readonly ClipboardIconButton open = new(StudioSymbol.Open, "Open link");
        private readonly ClipboardIconButton preview = new(StudioSymbol.Expand, "Preview photo");
        private readonly ClipboardIconButton edit = new(StudioSymbol.Edit, "Edit");
        private readonly ClipboardIconButton delete = new(StudioSymbol.Delete, "Delete", destructive: true);
        private readonly ClipboardIconButton visibility = new(StudioSymbol.Preview, "Hide content");
        private readonly ClipboardConcealView conceal;
        public ClipboardTile(Func<ActionDefinition, Task> copyAction, Func<ActionDefinition, Task> secondaryAction,
            Func<ActionDefinition, Task> editAction, Func<ActionDefinition, Task> deleteAction, Func<ActionDefinition, Task> shareAction, Func<ActionDefinition, Task> pinAction, ClipboardVisibilityPreferences visibilityStore, Action<ClipboardVisibilityErrorEventArgs> visibilityFailed, ClipboardCopyFeedback copyFeedback)
        {
            Margin = new Thickness(16, 0);
            copy = new ClipboardCopyButton(copyFeedback);
            header = new ClipboardHeaderView(pin);
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
            conceal = new ClipboardConcealView(new VerticalStackLayout { Spacing = 12, Children = { content, photo } }, visibilityStore);
            conceal.VisibilityFailed += (_, args) => visibilityFailed(args);
            photo.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(Image.IsLoading) && !photo.IsLoading) conceal.RefreshContent();
            };
            var body = new VerticalStackLayout { Padding = 12, Spacing = 12, Children = { conceal, actions } };
            Content = ClipboardCard.Create(header, body);
            visibility.Clicked += (_, _) => { conceal.Toggle(); UpdateVisibilityCaption(); };
            pin.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await pinAction(action); };
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
            // A different item starts collapsed; metadata updates to the same item retain expansion.
            var nextId = (BindingContext as ActionDefinition)?.Id;
            copy.Bind(nextId, (BindingContext as ActionDefinition)?.Name ?? "item");
            if (nextId != boundId) expanded = false;
            boundId = nextId; expand.IsVisible = false;
            text.MaxLines = expanded ? -1 : PreviewLines; text.LineBreakMode = expanded ? LineBreakMode.WordWrap : LineBreakMode.TailTruncation;
            link.Item = null; link.IsVisible = false; link.MaxLines = text.MaxLines; link.LineBreakMode = text.LineBreakMode; text.IsVisible = true;
            photo.Source = null; photo.IsVisible = false;
            open.IsVisible = preview.IsVisible = false;
            if (BindingContext is not ActionDefinition action) { header.SetItem("", null); text.Text = ""; return; }
            header.SetItem(action.Name, action.HeaderColorHex);
            pin.Symbol = action.IsFavorite ? StudioSymbol.PinFilled : StudioSymbol.Pin;
            pin.Describe(action.IsFavorite ? "Unpin" : "Pin", action.Name);
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
            edit.Describe("Edit", action.Name); delete.Describe("Delete", action.Name);
            open.Describe("Open link", action.Name); preview.Describe("Preview photo", action.Name);
            conceal.RefreshContent(); UpdateExpandCaption(); QueueOverflowCheck();
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
