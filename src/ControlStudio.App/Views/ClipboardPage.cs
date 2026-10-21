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
        items.ItemTemplate = new DataTemplate(() => new ClipboardTile(CopyAsync, SecondaryAsync, EditAsync, DeleteAsync));
        refresh.Content = items; refresh.SetDynamicResource(RefreshView.RefreshColorProperty, "AccentText");
        refresh.Refreshing += async (_, _) => await ReloadAsync();
        var switcher = new Grid { ColumnSpacing = 4, Padding = 4 };
        foreach (var kind in Enum.GetValues<ClipboardKind>())
        {
            switcher.ColumnDefinitions.Add(new(GridLength.Star));
            var segment = new Segment(kind, () => { if (!busy && selected != kind) { selected = kind; ShowStatus(""); Filter(); } });
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
    private Task EditAsync(ActionDefinition item) => GuardAsync(async token =>
    {
        validator.Validate(item);
        if (StudioContent.Kind(item.Parameters) is not { } kind) return;
        token.ThrowIfCancellationRequested();
        await Navigation.PushAsync(new ClipboardEditorPage(repository, validator, kind, item));
    });
    private Task DeleteAsync(ActionDefinition item) => GuardAsync(async token =>
    {
        if (!await DisplayAlertAsync("Delete clipboard item?", $"Remove “{item.Name}” from your saved clipboard?", "Delete", "Cancel")) return;
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
    private static StudioSymbol Symbol(ClipboardKind kind) => kind switch { ClipboardKind.Photo => StudioSymbol.Photo, ClipboardKind.Link => StudioSymbol.Link, _ => StudioSymbol.Text };

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
            label = new Label { Text = kind.ToString(), FontSize = 11, HorizontalTextAlignment = TextAlignment.Center };
            label.InputTransparent = true;
            surface = new Border
            {
                Content = new VerticalStackLayout { Padding = new Thickness(2, 8), Spacing = 4, Children = { icon, label }, InputTransparent = true },
                StrokeShape = new RoundRectangle { CornerRadius = 19 }, StrokeThickness = 1, InputTransparent = true,
                Background = Brush.Transparent, Stroke = Brush.Transparent
            };
            tap = SettingsControls.TapTarget(kind.ToString(), "ClipboardTab" + kind); tap.Clicked += (_, _) => select();
            Content = new Grid { Children = { tap, surface } };
            AutomationProperties.SetIsInAccessibleTree(label, false);
        }
        public void Update(bool selected)
        {
            // Theme bindings and dynamic resources must not compete when a tab changes state.
            label.RemoveBinding(Label.TextColorProperty); icon.RemoveBinding(StudioGlyph.TintProperty);
            label.RemoveDynamicResource(Label.TextColorProperty); icon.RemoveDynamicResource(StudioGlyph.TintProperty);
            surface.RemoveDynamicResource(Border.BackgroundProperty); surface.RemoveDynamicResource(Border.StrokeProperty);
            label.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
            if (selected)
            {
                surface.SetDynamicResource(Border.BackgroundProperty, "AccentSelectionBrush");
                surface.SetDynamicResource(Border.StrokeProperty, "AccentSelectionStroke");
                label.SetDynamicResource(Label.TextColorProperty, "AccentText"); icon.SetDynamicResource(StudioGlyph.TintProperty, "AccentText");
            }
            else
            {
                surface.Background = Brush.Transparent;
                surface.Stroke = Brush.Transparent;
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
        private readonly ClipboardIconButton copy = new(StudioSymbol.Clipboard, "Copy", primary: true);
        private readonly ClipboardIconButton open = new(StudioSymbol.Open, "Open link");
        private readonly ClipboardIconButton preview = new(StudioSymbol.Preview, "Preview photo");
        private readonly ClipboardIconButton edit = new(StudioSymbol.Edit, "Edit");
        private readonly ClipboardIconButton delete = new(StudioSymbol.Delete, "Delete", destructive: true);
        public ClipboardTile(Func<ActionDefinition, Task> copyAction, Func<ActionDefinition, Task> secondaryAction,
            Func<ActionDefinition, Task> editAction, Func<ActionDefinition, Task> deleteAction)
        {
            title.MaxLines = 2; text.MaxLines = 3; text.LineBreakMode = LineBreakMode.TailTruncation;
            var actions = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End, Children = { edit, delete, open, preview, copy } };
            Content = new GlassCard(new VerticalStackLayout { Spacing = 12, Children = { title, text, photo, actions } });
            copy.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await copyAction(action); };
            edit.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await editAction(action); };
            delete.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await deleteAction(action); };
            foreach (var button in new[] { open, preview })
                button.Clicked += async (_, _) => { if (BindingContext is ActionDefinition action) await secondaryAction(action); };
        }
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            photo.Source = null; photo.IsVisible = false;
            open.IsVisible = preview.IsVisible = false;
            if (BindingContext is not ActionDefinition action) { title.Text = text.Text = ""; return; }
            title.Text = action.Name;
            open.IsVisible = action.Parameters is OpenUrlParameters;
            preview.IsVisible = action.Parameters is PhotoParameters;
            text.Text = action.Parameters switch
            {
                CopyTextParameters value => value.Text, OpenUrlParameters value => value.Url,
                PhotoParameters value => ClipboardPhotoStore.GetPath(value.AssetKey) is null ? "Photo missing. Choose it again in Edit." : "Photo · saved on this iPhone", _ => ""
            };
            if (action.Parameters is PhotoParameters p && ClipboardPhotoStore.GetThumbnailPath(p.AssetKey) is { } path)
            { photo.Source = path; photo.IsVisible = true; SemanticProperties.SetDescription(photo, action.Name); }
            copy.Describe("Copy", action.Name); edit.Describe("Edit", action.Name); delete.Describe("Delete", action.Name);
            open.Describe("Open link", action.Name); preview.Describe("Preview photo", action.Name);
        }
    }
}
