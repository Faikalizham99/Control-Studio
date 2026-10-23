using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

internal sealed class WidgetAssignDesignPage : WidgetSlotPageBase
{
    private readonly IWidgetSlotStore store;
    private readonly WidgetSlotSelection selection;
    private readonly Action<bool> assigned;
    private readonly Func<Task> close;
    private readonly PageActionButton back = new("", "BackFromAssignWidget", ProfileSymbol.Back);
    private readonly ClipboardEditorActionButton searchButton = new("", ClipboardEditorControls.Icon(StudioSymbol.Search), "SearchWidgetDesigns");
    private readonly SearchBar search = new() { AutomationId = "SearchMatchingWidgets", IsVisible = false, MaximumWidthRequest = 800 };
    private readonly Grid choices = new() { ColumnSpacing = 12, RowSpacing = 12, AutomationId = "MatchingWidgetDesigns" };
    private readonly Label status = SettingsControls.Text("", 14, secondary: true);
    private readonly Label summary = SettingsControls.Text("Choose a design to assign.", 13, secondary: true);
    private readonly PageActionButton retry = new("Try again", "RetryWidgetAssignments");
    private readonly ClipboardEditorActionButton confirm;
    private readonly WidgetSlotPreviewWindow previews;
    private readonly List<(Guid Id, WidgetDesignChoiceCard Card)> cards = [];
    private bool enabled, committed, loaded;
    private bool? publication;
    private int columns;
    internal WidgetSize Size => selection.Size;
    internal Guid? SelectedId => selection.SelectedId;

    public WidgetAssignDesignPage(IWidgetSlotStore store, ILogger logger, WidgetSize size, int number,
        Action<bool>? assigned = null, Func<Task>? close = null) : base(logger)
    {
        this.store = store; selection = new(size, number); this.assigned = assigned ?? (_ => { }); this.close = close ?? (() => Navigation.PopAsync());
        var slotTitle = WidgetSlotRules.Title(size, number); var sizeTitle = WidgetDesignRules.SizeName(size);
        Title = "Assign design"; AutomationId = "WidgetAssignDesignPage";
        back.Clicked += async (_, _) => await RunAsync((_, _) => this.close());
        searchButton.Describe($"Search {sizeTitle} designs");
        searchButton.Clicked += (_, _) =>
        {
            if (!enabled) return; search.IsVisible = !search.IsVisible;
            if (search.IsVisible) search.Focus(); else { search.Unfocus(); search.Text = ""; }
        };
        search.Placeholder = $"Search {sizeTitle} designs"; search.MaxLength = 60;
        search.SetAppThemeColor(SearchBar.TextColorProperty, Color.FromArgb("#16171C"), Colors.White);
        search.SetAppThemeColor(SearchBar.PlaceholderColorProperty, Color.FromArgb("#626570"), Color.FromArgb("#AFB3BD"));
        search.TextChanged += (_, _) => ShowChoices();
        confirm = new("Assign to " + slotTitle, ClipboardEditorControls.Icon(StudioSymbol.Check), "ConfirmWidgetAssignment", primary: true, readableDisabled: true);
        confirm.Clicked += async (_, _) => await ConfirmAsync();
        var locked = SettingsControls.Text(sizeTitle + " designs", 15, bold: true); locked.SetDynamicResource(Label.TextColorProperty, "AccentText");
        locked.AutomationId = "LockedWidgetSize";
        var lockedSize = WidgetStudioStyle.Card(locked, new Thickness(14, 10), 20);
        lockedSize.SetDynamicResource(BackgroundProperty, "AccentSelectionBrush"); lockedSize.SetDynamicResource(Border.StrokeProperty, "AccentSelectionStroke");
        var body = new VerticalStackLayout { Padding = new Thickness(16, 8, 24, 20), Spacing = 12, MaximumWidthRequest = 800, Children = {
            lockedSize, SettingsControls.Text("Choose a saved design for " + slotTitle + ".", 14, secondary: true), search, status, retry, choices
        } };
        status.IsVisible = retry.IsVisible = false;
        retry.Clicked += async (_, _) => await ReloadAsync();
        var scroll = new ScrollView { Content = body }; previews = new(scroll, body);
        choices.SizeChanged += (_, _) => Reflow();
        var footer = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 12, 24, 14), Children = { summary, confirm } };
        footer.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F2F2F7"), Colors.Black);
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        layout.Add(WidgetStudioStyle.Header(Title, back, searchButton)); layout.Add(scroll, 0, 1); layout.Add(footer, 0, 2);
        Content = layout; SetActionsEnabled(false);
    }
    protected override async Task LoadAsync(CancellationToken token, int version)
    {
        if (committed)
        {
            ShowChoices(); ShowFailure(publication == true ? "Assignment saved. Home Screen refresh requested. Use Back to return to Widget slots."
                : "Assignment saved on this iPhone. Return to Widget slots and tap Refresh to retry the Home Screen update."); return;
        }
        loaded = false;
        var library = await store.ReadAsync(token); if (!Current(version)) return;
        selection.Reload(library); loaded = true; ShowChoices(); status.IsVisible = retry.IsVisible = false;
    }
    private void ShowChoices()
    {
        if (previews is null) return;
        previews.Clear(); choices.Children.Clear(); cards.Clear(); columns = 0;
        var matching = selection.Search(search.Text);
        foreach (var design in matching)
        {
            var path = store.RenderPath(design.RenderKey);
            var image = WidgetSlotCards.Preview(Size, design, path, previews);
            var card = new WidgetDesignChoiceCard(design, image, () => Choose(design.Id), path is not null);
            cards.Add((design.Id, card)); choices.Children.Add(card);
        }
        if (matching.Count == 0)
        {
            var message = string.IsNullOrWhiteSpace(search.Text) ? $"No {WidgetDesignRules.SizeName(Size)} designs yet. Create one in My Widgets, then return here."
                : "No matching designs. Try another name.";
            choices.Children.Add(SettingsControls.Text(message, 16, secondary: true));
        }
        Reflow(); UpdateSelection(); previews.Activate(Foreground);
    }
    private void Reflow()
    {
        var next = WidgetSlotSelection.Columns(Size, choices.Width > 0 ? choices.Width : 320);
        if (next == columns) return; columns = next;
        choices.ColumnDefinitions.Clear(); choices.RowDefinitions.Clear();
        for (var column = 0; column < columns; column++) choices.ColumnDefinitions.Add(new(GridLength.Star));
        if (cards.Count == 0) { choices.RowDefinitions.Add(new(GridLength.Auto)); if (choices.Children.FirstOrDefault() is View empty) Grid.SetColumnSpan(empty, columns); return; }
        for (var row = 0; row < (cards.Count + columns - 1) / columns; row++) choices.RowDefinitions.Add(new(GridLength.Auto));
        for (var index = 0; index < cards.Count; index++) { Grid.SetColumn(cards[index].Card, index % columns); Grid.SetRow(cards[index].Card, index / columns); }
        previews.Update();
    }
    internal void Choose(Guid id)
    {
        if (!enabled || !loaded || !Foreground || committed || !selection.Choose(id)) return;
        status.IsVisible = retry.IsVisible = false; UpdateSelection();
    }
    private void UpdateSelection()
    {
        foreach (var entry in cards) entry.Card.Update(entry.Id == selection.SelectedId);
        var missing = selection.Selected is { } selected && store.RenderPath(selected.RenderKey) is null;
        summary.Text = committed ? "Assignment saved. Return to Widget slots." : missing ? "Open Edit and save this design again before assigning."
            : selection.Selected is { } design ? "Selected: " + design.Name : "Choose a design to assign.";
        confirm.IsEnabled = enabled && loaded && !committed && selection.Selected is not null && !missing;
        confirm.Describe(committed ? "Assignment already saved" : selection.Selected is { } chosen
            ? $"Assign {chosen.Name} to {WidgetSlotRules.Title(Size, selection.Number)}" : "Choose a design before assigning");
    }
    internal Task ConfirmAsync() => RunAsync(async (token, version) =>
    {
        if (!loaded || committed || selection.SelectedId is not { } id) return;
        var fresh = await store.ReadAsync(token); if (!Current(version) || !Foreground) return;
        selection.Reload(fresh);
        if (selection.SelectedId != id || selection.Selected is null)
        { ShowChoices(); ShowFailure("This design is no longer available in this size. Choose another design."); return; }
        if (store.RenderPath(selection.Selected.RenderKey) is null)
        { ShowChoices(); ShowFailure("The saved preview is unavailable. Open Edit and save this design again before assigning."); return; }
        var published = await store.AssignAsync(Size, selection.Number, id, token);
        committed = true; publication = published; assigned(published);
        if (!Current(version) || !Foreground) return;
        UpdateSelection();
        try { await close(); }
        catch (Exception) { ShowFailure("Assignment saved. Use Back to return to Widget slots."); }
    });
    protected override void SetActionsEnabled(bool value)
    {
        enabled = value; back.IsEnabled = value; retry.IsEnabled = value && !committed;
        searchButton.IsEnabled = search.IsEnabled = choices.IsEnabled = value && loaded && !committed;
        UpdateSelection();
    }
    protected override void ReleasePreviews() { previews.Activate(false); previews.Clear(); }
    protected override void ShowFailure(string message) { status.Text = message; status.IsVisible = message.Length > 0; retry.IsVisible = !committed; }
}
