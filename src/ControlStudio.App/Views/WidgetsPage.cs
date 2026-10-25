using ControlStudio.Core.Widgets;
using ControlStudio.App.Platforms.iOS;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

internal sealed class WidgetsPage : WidgetStudioPage
{
    private static readonly WidgetSize[] MainSizes = WidgetDesignRules.SupportedSizes.ToArray();
    private readonly WidgetStudioService studio;
    private readonly AppleColorPicker colourPicker;
    private readonly VerticalStackLayout designs = new() { Spacing = 16 };
    private readonly ScrollView scroll = new();
    private readonly VerticalStackLayout body;
    private readonly List<(WidgetPreviewFrame Frame, WidgetRenderPreview Image)> previews = [];
    private bool previewsActive, updatingPreviews;
    private readonly WidgetAddButton add = new();
    private readonly PageActionButton back = new("", "BackFromWidgets", ProfileSymbol.Back);
    private readonly WidgetRefreshButton refresh = new();
    private readonly ClipboardEditorActionButton widgetSlots = new("Widget slots", ClipboardEditorControls.Icon(StudioSymbol.Widgets), "OpenWidgetSlots", primary: true);
    private readonly Label status = SettingsControls.Text("", 13, secondary: true);
    private readonly WidgetSegments sizes;
    private readonly Label xlHint = SettingsControls.Text("Extra Large requires iOS 27 or later on iPhone.", 12, secondary: true);
    private IReadOnlyList<WidgetDesign> saved = [];
    private WidgetSize SelectedSize => MainSizes[sizes.SelectedIndex];

    public WidgetsPage(WidgetStudioService studio, AppleColorPicker colourPicker, ILogger logger) : base(logger)
    {
        this.studio = studio; this.colourPicker = colourPicker;
        Title = "My Widgets"; AutomationId = "WidgetsPage";
        sizes = new(["Small", "Medium", "Large", "XL"], "WidgetSizeFilter", _ => FilterChanged());
        xlHint.IsVisible = false;
        add.Clicked += async (_, _) => await RunAsync((_, _) => Navigation.PushAsync(new WidgetEditorPage(studio, colourPicker, Logger, initialSize: SelectedSize)));
        back.Clicked += async (_, _) => await RunAsync((_, _) => Navigation.PopAsync());
        refresh.Clicked += async (_, _) => await RefreshWidgetsAsync();
        widgetSlots.Clicked += async (_, _) => await RunAsync((_, _) => Navigation.PushAsync(new WidgetSlotsPage(new AppleWidgetSlotStore(studio), Logger, SelectedSize)));
        var heading = SettingsControls.Text("Your widget designs", 22, bold: true);
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level2);
        var headingBlock = new VerticalStackLayout { Spacing = 10, Children =
        {
            heading, SettingsControls.Text("Create, edit and manage your custom widgets.", 14, secondary: true), sizes, xlHint, widgetSlots
        } };
        status.IsVisible = false;
        body = new VerticalStackLayout { Padding = new Thickness(20, 14, 28, 24), Spacing = 16, MaximumWidthRequest = 800,
            HorizontalOptions = LayoutOptions.Fill, Children = { headingBlock, designs, status } };
        scroll.Content = body;
        scroll.Scrolled += (_, _) => UpdateVisiblePreviews();
        scroll.SizeChanged += (_, _) => UpdateVisiblePreviews();
        body.SizeChanged += (_, _) => UpdateVisiblePreviews();
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        var headerActions = new HorizontalStackLayout { Spacing = ActionSpacing.Gap, WidthRequest = 104,
            MinimumWidthRequest = 104, Children = { refresh, add } };
        layout.Add(WidgetStudioStyle.Header(Title, back, headerActions, trailingWidth: 104));
        layout.Add(scroll, 0, 1); Content = layout; SetActionsEnabled(false);
    }
    private Task RefreshWidgetsAsync() => RunAsync(async (token, version) =>
    {
        refresh.IsRefreshing = true;
        try
        {
            await LoadAsync(token, version);
            if (IsForeground(version)) Announce(status.IsVisible ? status.Text : studio.Repository.HasSavedLibrary
                ? "Widget refresh requested. iOS decides when it appears." : "No saved widgets to refresh yet.");
        }
        catch (Exception ex)
        {
            if (IsForeground(version) && !token.IsCancellationRequested)
                Announce(ex is IOException ? ex.Message : "This action could not finish. Try Refresh again.");
            throw;
        }
        finally { refresh.IsRefreshing = false; }
    });
    private void FilterChanged() { xlHint.IsVisible = SelectedSize == WidgetSize.ExtraLargePortrait; ShowDesigns(); }
    protected override async Task LoadAsync(CancellationToken token, int version)
    {
        var loaded = await studio.Repository.ReadAsync(token); if (!IsCurrent(version)) return;
        saved = loaded; ShowDesigns();
        if (studio.Repository.HasSavedLibrary)
        {
            var published = await studio.PublishAsync(); if (!IsCurrent(version)) return;
            if (published) { status.Text = ""; status.IsVisible = false; }
            else ShowFailure("Saved on this device. Tap Refresh to retry the Home Screen update; check App Group access in Settings.");
        }
        else { status.Text = ""; status.IsVisible = false; }
    }
    private void ShowDesigns()
    {
        foreach (var preview in previews) preview.Image.Dispose();
        previews.Clear();
        designs.Children.Clear(); var matching = saved.Where(x => WidgetDesignRules.ForEditing(x).Size == SelectedSize).ToArray();
        if (matching.Length == 0)
        {
            designs.Children.Add(WidgetStudioStyle.Card(new VerticalStackLayout { Spacing = 8, Padding = 10, Children =
            {
                SettingsControls.Text("A space for your next idea", 19, bold: true),
                SettingsControls.Text($"No {WidgetSizeLabel(SelectedSize)} designs yet. Tap + to create one, then assign it in Widget slots.", 14, secondary: true)
            } }));
            return;
        }
        foreach (var design in matching)
        {
            var edit = new ClipboardIconButton(StudioSymbol.Edit, "Edit"); edit.Describe("Edit widget", design.Name);
            var delete = new ClipboardIconButton(StudioSymbol.Delete, "Delete", destructive: true); delete.Describe("Delete widget", design.Name);
            Task Edit() => RunAsync((_, _) => Navigation.PushAsync(new WidgetEditorPage(studio, colourPicker, Logger, design)));
            edit.Clicked += async (_, _) => await Edit();
            delete.Clicked += async (_, _) => await RunAsync(async (token, version) =>
            {
                if (!await DisplayAlertAsync("Delete widget design?", "Remove this saved design? Its assigned slots will become empty. Your original photos are kept.", "Delete", "Cancel") || !IsForeground(version)) return;
                var published = await studio.DeleteAsync(design.Id); if (!IsCurrent(version)) return;
                saved = await studio.Repository.ReadAsync(token); if (!IsCurrent(version)) return;
                ShowDesigns(); ShowFailure(published ? "Widget design deleted." : "Deleted on this device. Tap Refresh to update Home Screen widgets.");
                Announce("Widget design deleted.");
            });
            var title = SettingsControls.Text(design.Name, 18, bold: true); title.MaxLines = 2; title.LineBreakMode = LineBreakMode.TailTruncation;
            SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level3);
            var badgeLabel = SettingsControls.Text(WidgetSizeLabel(design.Size), 11);
            var badge = new AccentSurface(badgeLabel, 10, new Thickness(8, 3), captionSize: 11, captionBold: false) { HorizontalOptions = LayoutOptions.Start };
            badge.BindForeground(badgeLabel, Label.TextColorProperty);
            var labels = new VerticalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center, Children = { title, badge } };
            if (!WidgetDesignRules.IsSupported(design.Size)) labels.Children.Add(SettingsControls.Text("Open Edit to resize and save. Photos are kept.", 12, secondary: true));
            var stamp = studio.Repository.RenderUpdatedAt(design.RenderKey);
            if (stamp is { } updated) labels.Children.Add(SettingsControls.Text("Updated\n" + updated.ToLocalTime().ToString("dd MMM yyyy, h:mm tt"), 11, secondary: true));
            var open = SettingsControls.TapTarget("Open " + design.Name + " widget editor", "OpenWidgetDetails" + design.Id.ToString("N"));
            open.Clicked += async (_, _) => await Edit();
            var chevron = new StudioGlyph(StudioSymbol.Chevron, false) { WidthRequest = 15, HeightRequest = 15,
                HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            chevron.SetAppThemeColor(StudioGlyph.TintProperty, Color.FromArgb("#53617A"), Color.FromArgb("#CAD4E8"));
            var next = new Grid { WidthRequest = 44, MinimumHeightRequest = 48, Children = { open, chevron } };
            var actions = new ActionButtonRow(edit, delete, next);
            var header = new CardActionHeader(labels, actions);
            // Full-size cards need the full saved composite, not the 256px slot thumbnail.
            var image = new WidgetRenderPreview(studio.Repository.RenderPath(design.RenderKey), Color.FromArgb(design.BackgroundHex));
            var tap = SettingsControls.TapTarget("Open " + design.Name + " widget", "OpenWidget" + design.Id.ToString("N"));
            tap.Clicked += async (_, _) => await Edit();
            SemanticProperties.SetHint(tap, "Opens the widget editor.");
            AutomationProperties.SetIsInAccessibleTree(image, false);
            var preview = new WidgetPreviewFrame(WidgetDesignRules.ForEditing(design).Size,
                new Grid { Children = { tap, new WidgetCheckerboard { IsVisible = design.TransparentBackground }, image } },
                design.TransparentBackground ? Colors.Transparent : Color.FromArgb(design.BackgroundHex), dimensions: design.CanvasDimensions);
            previews.Add((preview, image));
            preview.SizeChanged += (_, _) => UpdateVisiblePreviews();
            SemanticProperties.SetDescription(tap, $"{design.Name}. {WidgetSizeLabel(design.Size)} widget preview. Open editor.");
            var cardBody = new VerticalStackLayout { Spacing = 14, Children = { header, preview } };
            if (WidgetScenes.IsLive(design)) cardBody.Children.Add(SettingsControls.Text("Saved preview. Clock and date layers update in the editor and on your Home Screen.", 12, secondary: true));
            designs.Children.Add(WidgetStudioStyle.Card(cardBody));
        }
        UpdateVisiblePreviews();
    }
    private Rect PreviewBounds(WidgetPreviewFrame frame)
    {
        double top = 0; VisualElement? current = frame;
        while (current is not null && current != body) { top += current.Y; current = current.Parent as VisualElement; }
        return current == body ? new Rect(0, top, frame.Width, frame.Height) : Rect.Zero;
    }
    private void UpdateVisiblePreviews()
    {
        if (updatingPreviews) return;
        updatingPreviews = true;
        try
        {
            var visible = previewsActive ? WidgetPreviewWindow.Select(previews.Select(x => PreviewBounds(x.Frame)).ToArray(), scroll.ScrollY, scroll.Height) : new HashSet<int>();
            // Release first so a jump or size switch never temporarily doubles live sources.
            for (var i = 0; i < previews.Count; i++) if (!visible.Contains(i)) previews[i].Image.SetVisible(false);
            foreach (var index in visible) previews[index].Image.SetVisible(true);
        }
        finally { updatingPreviews = false; }
    }
    protected override void OnAppearing() { previewsActive = true; base.OnAppearing(); UpdateVisiblePreviews(); }
    protected override void OnDisappearing()
    {
        refresh.IsRefreshing = false;
        previewsActive = false;
        // The next appearance reloads the library and builds fresh controls.
        // A render replaced/deleted in the editor must never be requested again.
        foreach (var preview in previews) preview.Image.Dispose();
        base.OnDisappearing();
    }
    private static string WidgetSizeLabel(WidgetSize size) => size switch
    {
        WidgetSize.ExtraLargePortrait => "Extra Large", WidgetSize.ExtraLarge => "Needs resize", _ => size.ToString()
    };
    protected override void SetActionsEnabled(bool enabled) => add.IsEnabled = back.IsEnabled = refresh.IsEnabled = widgetSlots.IsEnabled
        = sizes.IsEnabled = designs.IsEnabled = enabled;
    protected override void ShowFailure(string message) { status.Text = message; status.IsVisible = !string.IsNullOrEmpty(message); }
}
