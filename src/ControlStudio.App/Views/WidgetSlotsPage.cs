using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

internal sealed class WidgetSlotsPage : WidgetSlotPageBase
{
    private static readonly WidgetSize[] Sizes = WidgetDesignRules.SupportedSizes.ToArray();
    private readonly IWidgetSlotStore store;
    private readonly Func<string, Task<bool>> confirmClear;
    private readonly Func<WidgetSize, int, Task> openAssignment;
    private readonly PageActionButton back = new("", "BackFromWidgetSlots", ProfileSymbol.Back);
    private readonly PageActionButton refresh = new("Refresh", "RefreshWidgetSlots");
    private readonly WidgetSegments sizes;
    private readonly VerticalStackLayout slots = new() { Spacing = 12 };
    private readonly Label status = SettingsControls.Text("", 14, secondary: true);
    private readonly Label xlHint = SettingsControls.Text("Extra Large requires iOS 27 or later on iPhone.", 12, secondary: true);
    private readonly WidgetSlotPreviewWindow previews;
    private string? pendingStatus;
    private bool loaded;
    internal WidgetSize SelectedSize => Sizes[sizes.SelectedIndex];

    public WidgetSlotsPage(IWidgetSlotStore store, ILogger logger, WidgetSize initialSize = WidgetSize.Small,
        Func<string, Task<bool>>? confirmClear = null, Func<WidgetSize, int, Task>? openAssignment = null) : base(logger)
    {
        WidgetSlotRules.ValidateAddress(initialSize, 1); this.store = store;
        this.confirmClear = confirmClear ?? (title => DisplayAlertAsync($"Clear {title}?", "This empties the slot. Your saved design and original photos are kept.", "Clear", "Cancel"));
        this.openAssignment = openAssignment ?? ((family, number) => Navigation.PushAsync(new WidgetAssignDesignPage(store, Logger, family, number,
            published => pendingStatus = published ? "Design assigned. Home Screen refresh requested." : "Assignment saved on this iPhone. Tap Refresh to retry the Home Screen update.")));
        Title = "Widget slots"; AutomationId = "WidgetSlotsPage";
        sizes = new(["Small", "Medium", "Large", "XL"], "WidgetSlotsSize", async index => await SelectSizeAsync(Sizes[index]));
        sizes.Update(Array.IndexOf(Sizes, initialSize));
        back.Clicked += async (_, _) => await RunAsync((_, _) => Navigation.PopAsync());
        refresh.Clicked += async (_, _) => await RefreshSlotsAsync();
        status.IsVisible = false; xlHint.IsVisible = initialSize == WidgetSize.ExtraLargePortrait;
        var icon = ClipboardEditorControls.Icon(StudioSymbol.Layers, size: 36);
        var well = WidgetStudioStyle.Card(icon, 10, 17); well.WidthRequest = well.HeightRequest = 64; well.VerticalOptions = LayoutOptions.Center;
        well.SetDynamicResource(BackgroundProperty, "AccentSelectionBrush");
        var title = SettingsControls.Text("A place for every design", 19, bold: true);
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level2);
        var bannerRow = new Grid { ColumnSpacing = 12, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        bannerRow.Add(well); bannerRow.Add(new VerticalStackLayout { Spacing = 5, VerticalOptions = LayoutOptions.Center, Children = {
            title, SettingsControls.Text("Each size has 10 slots. Assign a saved design here, then add Studio 1–10 in the matching size on your Home Screen.", 13, secondary: true)
        } }, 1);
        var banner = WidgetStudioStyle.Card(bannerRow); banner.SetDynamicResource(Border.StrokeProperty, "AccentSelectionStroke");
        var body = new VerticalStackLayout { Padding = new Thickness(16, 8, 24, 24), Spacing = 12, MaximumWidthRequest = 800, Children = {
            banner, SettingsControls.Text("Widget size", 14, bold: true), sizes, xlHint, status, slots,
            SettingsControls.Text("Each size has independent assignments. Copies of the same slot and size show the same design.", 12, secondary: true)
        } };
        var scroll = new ScrollView { Content = body }; previews = new(scroll, body);
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        layout.Add(PageActionBar.Modal(Title, back, refresh)); layout.Add(scroll, 0, 1); Content = layout; SetActionsEnabled(false);
    }
    internal Task SelectSizeAsync(WidgetSize family) => RunAsync(async (token, version) =>
    {
        WidgetSlotRules.ValidateAddress(family, 1); sizes.Update(Array.IndexOf(Sizes, family));
        previews.Clear(); slots.Children.Clear(); ShowFailure("");
        xlHint.IsVisible = family == WidgetSize.ExtraLargePortrait; await LoadAsync(token, version);
    });
    protected override async Task LoadAsync(CancellationToken token, int version)
    {
        loaded = false;
        var library = await store.ReadAsync(token); if (!Current(version)) return;
        loaded = true;
        previews.Clear(); slots.Children.Clear(); var family = SelectedSize;
        for (var number = 1; number <= WidgetSlotRules.SlotsPerSize; number++)
        {
            var slot = number;
            var assignment = library.Slots.FirstOrDefault(x => x.Size == family && x.Number == slot);
            var design = library.Designs.FirstOrDefault(x => x.Size == family && x.Id == assignment?.DesignId);
            var path = design is null ? null : store.RenderPath(design.RenderKey);
            var frame = WidgetSlotCards.Preview(family, design, path, previews, 112);
            slots.Children.Add(new WidgetSlotCard(family, slot, design, frame, () => OpenAssignAsync(family, slot), () => ClearAsync(family, slot), path is not null));
        }
        previews.Activate(Foreground);
        ShowFailure(pendingStatus ?? ""); if (pendingStatus is not null) { Announce(pendingStatus); pendingStatus = null; }
    }
    internal Task OpenAssignAsync(WidgetSize family, int number) => RunAsync(async (_, _) =>
    {
        WidgetSlotRules.ValidateAddress(family, number); if (!loaded || family != SelectedSize) return;
        await openAssignment(family, number);
    });
    internal Task ClearAsync(WidgetSize family, int number) => RunAsync(async (token, version) =>
    {
        WidgetSlotRules.ValidateAddress(family, number); if (!loaded || family != SelectedSize) return;
        if (!await confirmClear(WidgetSlotRules.Title(family, number)) || !Current(version) || !Foreground) return;
        var published = await store.AssignAsync(family, number, null, token); if (!Current(version)) return;
        await LoadAsync(token, version); if (!Current(version)) return;
        var message = published ? "Slot cleared. Home Screen refresh requested." : "Slot cleared on this iPhone. Tap Refresh to retry the Home Screen update.";
        ShowFailure(message); Announce(message);
    });
    internal Task RefreshSlotsAsync() => RunAsync(async (token, version) =>
    {
        var published = await store.PublishAsync(); if (!Current(version)) return;
        await LoadAsync(token, version); if (Current(version)) ShowFailure(published ? "Widget refresh requested. iOS decides when it appears."
            : "Saved assignments are kept. Home Screen publication failed; check App Group access and try Refresh again.");
    });
    protected override void ReleasePreviews() { previews.Activate(false); previews.Clear(); }
    protected override void SetActionsEnabled(bool enabled)
    {
        back.IsEnabled = refresh.IsEnabled = sizes.IsEnabled = enabled; slots.IsEnabled = enabled && loaded;
    }
    protected override void ShowFailure(string message) { status.Text = message; status.IsVisible = message.Length > 0; }
}

internal sealed class WidgetSlotCard : ContentView
{
    public WidgetSlotCard(WidgetSize family, int number, WidgetDesign? design, View preview, Func<Task> assign, Func<Task> clear, bool hasPreview = true)
    {
        var title = WidgetSlotRules.Title(family, number);
        var heading = SettingsControls.Text(title, 16, bold: true); SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level3);
        var name = SettingsControls.Text(design?.Name ?? "Empty slot", 16, bold: design is not null);
        name.MaxLines = 3; name.LineBreakMode = LineBreakMode.TailTruncation;
        var change = new ClipboardEditorActionButton(design is null ? "Assign" : "Change", null, (design is null ? "Assign" : "Change") + family + "Slot" + number, primary: true, compact: true);
        change.Describe((design is null ? "Assign a design to " : "Change the design in ") + title);
        change.Clicked += async (_, _) => await assign();
        var actions = new Grid { ColumnSpacing = ActionSpacing.Gap, ColumnDefinitions = { new(GridLength.Star) } }; actions.Add(change);
        if (design is not null)
        {
            var remove = new PageActionButton("Clear", "Clear" + family + "Slot" + number);
            remove.Clicked += async (_, _) => await clear(); actions.ColumnDefinitions.Add(new(GridLength.Star)); actions.Add(remove, 1);
        }
        var details = new VerticalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center, Children = { name } };
        if (design is null) details.Children.Add(SettingsControls.Text("Not assigned", 12, secondary: true));
        else if (!hasPreview) details.Children.Add(SettingsControls.Text("Preview unavailable. Open Edit and save again.", 12, secondary: true));
        else if (WidgetScenes.IsLive(design)) details.Children.Add(SettingsControls.Text("Saved preview · live clock on Home Screen", 12, secondary: true));
        details.Children.Add(actions);
        var tap = SettingsControls.TapTarget((design is null ? "Assign " : "Change ") + title, "Open" + family + "Slot" + number);
        tap.Clicked += async (_, _) => await assign();
        var art = new Grid { WidthRequest = 112, Children = { tap, preview } };
        var row = new Grid { ColumnSpacing = 12, RowSpacing = 10, ColumnDefinitions = { new(112), new(GridLength.Star) } };
        row.Add(art); row.Add(details, 1);
        var stacked = false;
        row.SizeChanged += (_, _) =>
        {
            var next = row.Width is > 0 and < 280; if (next == stacked) return; stacked = next;
            row.ColumnDefinitions.Clear(); row.RowDefinitions.Clear();
            row.ColumnDefinitions.Add(new(stacked ? GridLength.Star : new GridLength(112)));
            if (stacked) { row.RowDefinitions.Add(new(GridLength.Auto)); row.RowDefinitions.Add(new(GridLength.Auto)); }
            else row.ColumnDefinitions.Add(new(GridLength.Star));
            Grid.SetColumn(details, stacked ? 0 : 1); Grid.SetRow(details, stacked ? 1 : 0);
        };
        Content = WidgetStudioStyle.Card(new VerticalStackLayout { Spacing = 8, Children = { heading, row } }, 12, 20);
    }
}
