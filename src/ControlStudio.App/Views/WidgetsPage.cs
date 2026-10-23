using ControlStudio.Core.Widgets;
using ControlStudio.App.Platforms.iOS;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

internal sealed class WidgetsPage : WidgetStudioPage
{
    private static readonly WidgetSize[] MainSizes = [WidgetSize.Small, WidgetSize.Medium, WidgetSize.Large, WidgetSize.ExtraLargePortrait];
    private readonly WidgetStudioService studio;
    private readonly AppleColorPicker colourPicker;
    private readonly VerticalStackLayout designs = new() { Spacing = 16 };
    private readonly WidgetAddButton add = new();
    private readonly PageActionButton back = new("", "BackFromWidgets", ProfileSymbol.Back);
    private readonly ClipboardEditorActionButton refresh = new("Refresh widgets", ClipboardEditorControls.Icon(StudioSymbol.Refresh), "RefreshWidgetDesigns");
    private readonly ClipboardEditorActionButton widgetSlots = new("Widget slots", ClipboardEditorControls.Icon(StudioSymbol.Widgets), "OpenWidgetSlots", primary: true);
    private readonly Label status = SettingsControls.Text("", 13, secondary: true);
    private readonly WidgetSegments sizes, xlLayouts;
    private readonly VerticalStackLayout xlOptions;
    private IReadOnlyList<WidgetDesign> saved = [];
    private WidgetSize SelectedSize => sizes.SelectedIndex == 3
        ? (xlLayouts.SelectedIndex == 0 ? WidgetSize.ExtraLargePortrait : WidgetSize.ExtraLarge)
        : MainSizes[sizes.SelectedIndex];

    public WidgetsPage(WidgetStudioService studio, AppleColorPicker colourPicker, ILogger logger) : base(logger)
    {
        this.studio = studio; this.colourPicker = colourPicker;
        Title = "My Widgets"; AutomationId = "WidgetsPage";
        sizes = new(["Small", "Medium", "Large", "XL"], "WidgetSizeFilter", _ => FilterChanged());
        xlLayouts = new(["Portrait", "Landscape"], "WidgetXLLayout", _ => ShowDesigns());
        xlOptions = new() { Spacing = 6, IsVisible = false, Children =
        {
            xlLayouts, SettingsControls.Text("Portrait: iOS/iPadOS 27+. Landscape: iPad.", 12, secondary: true)
        } };
        add.Clicked += async (_, _) => await RunAsync((_, _) => Navigation.PushAsync(new WidgetEditorPage(studio, colourPicker, Logger, initialSize: SelectedSize)));
        back.Clicked += async (_, _) => await RunAsync((_, _) => Navigation.PopAsync());
        refresh.Clicked += async (_, _) => await ReloadAsync();
        widgetSlots.Clicked += async (_, _) => await RunAsync((_, _) => Navigation.PushAsync(new WidgetSlotsPage(studio, Logger, SelectedSize)));
        var heading = SettingsControls.Text("Your widget designs", 22, bold: true);
        SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level2);
        var headingBlock = new VerticalStackLayout { Spacing = 10, Children =
        {
            heading, SettingsControls.Text("Create, edit and manage your custom widgets.", 14, secondary: true), sizes, xlOptions, widgetSlots
        } };
        status.IsVisible = false;
        var body = new VerticalStackLayout { Padding = new Thickness(20, 14, 28, 24), Spacing = 16, MaximumWidthRequest = 800,
            HorizontalOptions = LayoutOptions.Fill, Children = { headingBlock, designs, status, refresh } };
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        layout.Add(WidgetStudioStyle.Header(Title, back, add));
        layout.Add(new ScrollView { Content = body }, 0, 1); Content = layout;
    }
    private void FilterChanged() { xlOptions.IsVisible = sizes.SelectedIndex == 3; ShowDesigns(); }
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
        designs.Children.Clear(); var matching = saved.Where(x => x.Size == SelectedSize).ToArray();
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
            var stamp = studio.Repository.RenderUpdatedAt(design.RenderKey);
            if (stamp is { } updated) labels.Children.Add(SettingsControls.Text("Updated\n" + updated.ToLocalTime().ToString("dd MMM yyyy, h:mm tt"), 11, secondary: true));
            var open = SettingsControls.TapTarget("Open " + design.Name + " widget editor", "OpenWidgetDetails" + design.Id.ToString("N"));
            open.Clicked += async (_, _) => await Edit();
            var chevron = new StudioGlyph(StudioSymbol.Chevron, false) { WidthRequest = 15, HeightRequest = 15,
                HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
            chevron.SetAppThemeColor(StudioGlyph.TintProperty, Color.FromArgb("#53617A"), Color.FromArgb("#CAD4E8"));
            var next = new Grid { WidthRequest = 44, MinimumHeightRequest = 48, Children = { open, chevron } };
            var actions = new HorizontalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center, Children = { edit, delete, next } };
            var header = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
            header.Add(labels); header.Add(actions, 1);
            // Small composites preserve the complete canvas while bounding decoded memory for a full library.
            var path = studio.Repository.ThumbnailPath(design.RenderKey);
            View image = path is not null ? new Image { Source = path, Aspect = Aspect.AspectFit, InputTransparent = true }
                : new Label { Text = "Preview unavailable\nOpen Edit and save again.", TextColor = AppearanceColors.ButtonForeground(Color.FromArgb(design.BackgroundHex)),
                    FontSize = 13, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, Margin = 12, InputTransparent = true };
            var tap = SettingsControls.TapTarget("Open " + design.Name + " widget", "OpenWidget" + design.Id.ToString("N"));
            tap.Clicked += async (_, _) => await Edit();
            SemanticProperties.SetHint(tap, "Opens the widget editor.");
            AutomationProperties.SetIsInAccessibleTree(image, false);
            var preview = new WidgetPreviewFrame(design.Size, new Grid { Children = { tap, image } }, Color.FromArgb(design.BackgroundHex));
            SemanticProperties.SetDescription(tap, $"{design.Name}. {WidgetSizeLabel(design.Size)} widget preview. Open editor.");
            designs.Children.Add(WidgetStudioStyle.Card(new VerticalStackLayout { Spacing = 14, Children = { header, preview } }));
        }
    }
    private static string WidgetSizeLabel(WidgetSize size) => size switch
    {
        WidgetSize.ExtraLargePortrait => "XL Portrait", WidgetSize.ExtraLarge => "XL Landscape", _ => size.ToString()
    };
    protected override void SetActionsEnabled(bool enabled) => add.IsEnabled = back.IsEnabled = refresh.IsEnabled = widgetSlots.IsEnabled
        = sizes.IsEnabled = xlLayouts.IsEnabled = designs.IsEnabled = enabled;
    protected override void ShowFailure(string message) { status.Text = message; status.IsVisible = !string.IsNullOrEmpty(message); }
}
