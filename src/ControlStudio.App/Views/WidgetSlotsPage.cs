using ControlStudio.App.Platforms.iOS;
using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

internal sealed class WidgetSlotsPage : ContentPage
{
    private static readonly WidgetSize[] Sizes = WidgetDesignRules.SupportedSizes.ToArray();
    private readonly WidgetStudioService studio;
    private readonly ILogger logger;
    private readonly PageActionButton back = new("", "BackFromWidgetSlots", ProfileSymbol.Back);
    private readonly PageActionButton refresh = new("Refresh", "RefreshWidgetSlots");
    private readonly Picker size = new() { Title = "Widget size", AutomationId = "WidgetSlotsSize" };
    private readonly VerticalStackLayout slots = new() { Spacing = 14 };
    private readonly List<SlotCard> cards = [];
    private WidgetSize? shownSize;
    private readonly Label status = SettingsControls.Text("", 14, secondary: true);
    private bool active, busy, reloadPending;
    private int lifetime;
    private Window? observedWindow;
    private WidgetSize SelectedSize => Sizes[Math.Clamp(size.SelectedIndex, 0, Sizes.Length - 1)];
    private bool Foreground => active && Window?.IsActivated == true;

    public WidgetSlotsPage(WidgetStudioService studio, ILogger logger, WidgetSize initialSize = WidgetSize.Small)
    {
        this.studio = studio; this.logger = logger;
        Title = "Widget slots"; AutomationId = "WidgetSlotsPage"; SafeAreaEdges = SafeAreaEdges.All;
        DashboardStyle.Page(this); NavigationPage.SetHasNavigationBar(this, false);
        size.ItemsSource = Sizes.Select(WidgetDesignRules.SizeName).ToArray(); size.SelectedIndex = Math.Max(0, Array.IndexOf(Sizes, initialSize));
        size.SetAppThemeColor(Picker.TextColorProperty, Color.FromArgb("#16171C"), Colors.White);
        SemanticProperties.SetDescription(size, "Size of the widget slots to assign");
        size.SelectedIndexChanged += async (_, _) => await RunAsync(LoadAsync);
        back.Clicked += async (_, _) => await RunAsync(() => Navigation.PopAsync());
        refresh.Clicked += async (_, _) => await RunAsync(async () =>
        {
            var epoch = lifetime;
            var published = await studio.PublishAsync(); await LoadAsync();
            if (active && epoch == lifetime) ShowStatus(published ? "Widget refresh requested. iOS decides when it appears." : "Saved assignments are kept. Home Screen publication failed; check App Group access and try Refresh again.");
        });
        var header = new VerticalStackLayout { Padding = new Thickness(20, 12, 20, 0), Spacing = 10, Children =
        {
            SettingsControls.Text("A place for every design", 25, bold: true),
            SettingsControls.Text("Each size has 10 slots. Assign a saved design here, then add Studio 1–10 in the matching size on your Home Screen.", 14, secondary: true),
            ClipboardEditorControls.Card(size)
        } };
        var content = new VerticalStackLayout { Padding = new Thickness(20, 12, 24, 24), Spacing = 14, Children =
        {
            status, slots,
            SettingsControls.Text("Studio 1 in Small uses Small 1; Studio 1 in Medium uses Medium 1. Copies of the same slot and size show the same design. Empty slots never substitute another photo. Extra Large requires iOS 27 or later on iPhone.", 13, secondary: true)
        } };
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) } };
        layout.Add(PageActionBar.Modal(Title, back, refresh)); layout.Add(header, 0, 1);
        layout.Add(new ScrollView { Content = content }, 0, 2); Content = layout; SetEnabled();
    }
    private async Task LoadAsync()
    {
        var epoch = lifetime;
        var library = await studio.Repository.ReadLibraryAsync();
        if (!active || epoch != lifetime) return;
        reloadPending = false;
        ShowSlots(library); ShowStatus(library.Designs.Any(x => x.Size == SelectedSize)
            ? "Choose Assign or Change to use a saved design."
            : "No saved designs in this size yet. Go back to My Widgets and tap + to create one.");
    }
    private void ShowSlots(WidgetLibraryCatalog library)
    {
        var family = SelectedSize;
        if (shownSize != family)
        {
            slots.Children.Clear(); cards.Clear(); shownSize = family;
            for (var number = 1; number <= WidgetSlotRules.SlotsPerSize; number++)
            {
                var slot = number;
                var card = new SlotCard(family, slot, () => RunAsync(() => AssignAsync(family, slot)), () => RunAsync(() => ClearAsync(family, slot)));
                cards.Add(card); slots.Children.Add(card);
            }
        }
        for (var index = 0; index < cards.Count; index++)
        {
            var assignment = library.Slots.FirstOrDefault(x => x.Size == family && x.Number == index + 1);
            var design = library.Designs.FirstOrDefault(x => x.Id == assignment?.DesignId && x.Size == family);
            cards[index].Update(design, design is null ? null : studio.Repository.ThumbnailPath(design.RenderKey));
        }
    }
    private async Task AssignAsync(WidgetSize family, int number)
    {
        var epoch = lifetime;
        var library = await studio.Repository.ReadLibraryAsync();
        if (!Foreground || epoch != lifetime) return;
        var choices = library.Designs.Where(x => x.Size == family).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        if (choices.Length == 0) { ShowStatus("Create a saved design in this size first. Go back to My Widgets and tap +."); return; }
        // Prefixes keep user names such as "Cancel" distinct from the native sheet's cancel action.
        var labels = choices.Select((design, index) => $"{index + 1}. {design.Name}").ToArray();
        var chosen = await DisplayActionSheetAsync($"Assign {WidgetSlotRules.Title(family, number)}", "Cancel", null, labels);
        if (!Foreground || epoch != lifetime) return;
        var index = Array.IndexOf(labels, chosen); if (index < 0) return;
        var published = await studio.AssignSlotAsync(family, number, choices[index].Id);
        await LoadAsync();
        if (active && epoch == lifetime) ShowStatus(published ? "Design assigned. Home Screen refresh requested." : "Assignment saved on this iPhone. Tap Refresh to retry the Home Screen update.", announce: true);
    }
    private async Task ClearAsync(WidgetSize family, int number)
    {
        var epoch = lifetime;
        if (!Foreground || !await DisplayAlertAsync($"Clear {WidgetSlotRules.Title(family, number)}?", "This empties the slot. Your saved design and original photos are kept.", "Clear", "Cancel")
            || !Foreground || epoch != lifetime) return;
        var published = await studio.AssignSlotAsync(family, number, null); await LoadAsync();
        if (active && epoch == lifetime) ShowStatus(published ? "Slot cleared. Home Screen refresh requested." : "Slot cleared on this iPhone. Tap Refresh to retry the Home Screen update.", announce: true);
    }
    private void ShowStatus(string message, bool announce = false)
    {
        status.Text = message; status.IsVisible = message.Length > 0;
        if (announce && Foreground)
            try { SemanticScreenReader.Default.Announce(message); }
            catch (Exception ex) { logger.LogDebug("Widget slot announcement unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
    }
    private void SetEnabled() => back.IsEnabled = refresh.IsEnabled = size.IsEnabled = slots.IsEnabled = active && !busy;
    private async Task RunAsync(Func<Task> operation)
    {
        if (!active) return;
        if (busy) { return; }
        var epoch = lifetime;
        busy = true; SetEnabled();
        try { await operation(); }
        catch (Exception ex) { logger.LogWarning("Widget slot action failed. ErrorType={ErrorType}", ex.GetType().Name); if (active && epoch == lifetime) ShowStatus(ex is IOException ? ex.Message : "The widget slots could not be updated. Try Refresh."); }
        finally { busy = false; SetEnabled(); if (active && reloadPending) { reloadPending = false; await RunAsync(LoadAsync); } }
    }
    private async void WindowActivated(object? sender, EventArgs args)
    {
        if (busy) { reloadPending = true; return; }
        await RunAsync(LoadAsync);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true; lifetime++;
        if (observedWindow is not null) observedWindow.Activated -= WindowActivated;
        observedWindow = Window; if (observedWindow is not null) observedWindow.Activated += WindowActivated;
        if (busy) { reloadPending = true; SetEnabled(); return; }
        await RunAsync(LoadAsync);
    }
    protected override void OnDisappearing()
    {
        active = false; lifetime++;
        if (observedWindow is not null) { observedWindow.Activated -= WindowActivated; observedWindow = null; }
        SetEnabled(); base.OnDisappearing();
    }
    protected override bool OnBackButtonPressed() => busy || base.OnBackButtonPressed();

    private sealed class SlotCard : ContentView
    {
        private readonly Label name = SettingsControls.Text("", 17, bold: true);
        private readonly Label empty = SettingsControls.Text("", 14, secondary: true);
        private readonly Image preview = new() { Aspect = Aspect.AspectFit, HeightRequest = 130, InputTransparent = true };
        private readonly Border emptyPreview;
        private readonly ClipboardEditorActionButton assign, change, clear;
        private readonly string title;
        private string? imagePath;
        public SlotCard(WidgetSize family, int number, Func<Task> assignAction, Func<Task> clearAction)
        {
            title = WidgetSlotRules.Title(family, number);
            name.MaxLines = 2; name.LineBreakMode = LineBreakMode.TailTruncation;
            emptyPreview = ClipboardEditorControls.Field(empty, 18); emptyPreview.HeightRequest = 130;
            var image = new Grid { Children = { emptyPreview, preview } };
            assign = new("Assign", null, $"Assign{family}Slot{number}", primary: true);
            change = new("Change", null, $"Change{family}Slot{number}", primary: true);
            clear = new("Clear", null, $"Clear{family}Slot{number}");
            assign.Describe($"Assign a design to {title}"); change.Describe($"Change the design in {title}");
            clear.Describe($"Clear {title}. The saved design is kept.");
            assign.Clicked += async (_, _) => await assignAction(); change.Clicked += async (_, _) => await assignAction();
            clear.Clicked += async (_, _) => await clearAction();
            var actions = new Grid { ColumnSpacing = 10, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
            actions.Add(new Grid { Children = { assign, change } }); actions.Add(clear, 1);
            Content = new GlassCard(new VerticalStackLayout { Spacing = 10, Children = { SettingsControls.Text(title, 19, bold: true), name, image, actions } });
        }
        public void Update(WidgetDesign? design, string? path)
        {
            name.Text = design?.Name ?? "Empty slot";
            empty.Text = design is null ? "Not assigned" : "Preview missing";
            if (imagePath != path) { imagePath = path; preview.Source = path; }
            preview.IsVisible = path is not null; emptyPreview.IsVisible = path is null;
            assign.IsVisible = design is null; change.IsVisible = clear.IsVisible = design is not null;
            SemanticProperties.SetDescription(preview, $"{title} preview: {design?.Name}");
            SemanticProperties.SetDescription(emptyPreview, design is null ? $"{title} is empty" : $"{title} preview unavailable. Open the design and save it again.");
        }
    }
}
