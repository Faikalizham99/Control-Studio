using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetSlotsRedesignTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private static WidgetDesign Design(WidgetSize size, string name = "Saved design") => new(Guid.NewGuid(), name, size, "#007AFF", [], Guid.NewGuid().ToString("N"), 1, true);
    private void Open(ContentPage page)
    {
        host.App.Windows[0].Page = page;
        if (!host.App.Windows[0].IsActivated) ((IWindow)host.App.Windows[0]).Activated();
        ((IPageController)page).SendAppearing();
    }
    private static IEnumerable<Element> Descendants(Element element)
    {
        yield return element;
        if (element is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var value in Descendants(child)) yield return value;
    }
    private static Button Button(ContentPage page, string id) => Descendants(page).OfType<Button>().Single(x => x.AutomationId == id);
    private static WidgetAssignDesignPage Assignment(Store store, WidgetSize size, int number = 2, Action<bool>? saved = null, Func<Task>? close = null)
        => new(store, NullLogger.Instance, size, number, saved, close ?? (() => Task.CompletedTask));

    [Theory]
    [InlineData(WidgetSize.Small, 2)] [InlineData(WidgetSize.Medium, 2)] [InlineData(WidgetSize.Large, 1)] [InlineData(WidgetSize.ExtraLargePortrait, 1)]
    public async Task AssignmentStaysLockedToItsSizeAndSavesOnlyAfterConfirmation(WidgetSize size, int columns)
    {
        var chosen = Design(size, "Cancel"); var unrelated = Design(size == WidgetSize.Small ? WidgetSize.Large : WidgetSize.Small);
        var store = new Store([chosen, unrelated]); var page = Assignment(store, size); Open(page);
        Assert.Empty(Descendants(page).OfType<Picker>()); Assert.Empty(Descendants(page).OfType<WidgetSegments>());
        Assert.Single(Descendants(page).OfType<WidgetDesignChoiceCard>());
        Assert.Equal(columns, Descendants(page).OfType<Grid>().Single(x => x.AutomationId == "MatchingWidgetDesigns").ColumnDefinitions.Count);
        Assert.False(Button(page, "ConfirmWidgetAssignment").IsEnabled);
        page.Choose(unrelated.Id); Assert.Null(page.SelectedId);
        page.Choose(chosen.Id); Assert.Equal(chosen.Id, page.SelectedId); Assert.Empty(store.Writes);
        Assert.True(Button(page, "ConfirmWidgetAssignment").IsEnabled);
        await page.ConfirmAsync(); Assert.Equal((size, 2, (Guid?)chosen.Id), Assert.Single(store.Writes));
        Assert.False(Button(page, "ConfirmWidgetAssignment").IsEnabled);
        await page.ConfirmAsync(); Assert.Single(store.Writes);
    }
    [Theory]
    [InlineData(WidgetSize.Small)] [InlineData(WidgetSize.Medium)] [InlineData(WidgetSize.Large)] [InlineData(WidgetSize.ExtraLargePortrait)]
    public void SlotCardsKeepAllTenAddressesAndCorrectPreviewProportions(WidgetSize size)
    {
        var design = Design(size); var store = new Store([design], [new(size, 1, design.Id)]);
        var page = new WidgetSlotsPage(store, NullLogger.Instance, size); Open(page);
        Assert.Equal(10, Descendants(page).OfType<WidgetSlotCard>().Count());
        var frames = Descendants(page).OfType<WidgetPreviewFrame>().ToArray(); Assert.Equal(10, frames.Length);
        foreach (var frame in frames)
        {
            var border = Assert.IsType<Border>(frame.Content); var canvas = WidgetDesignRules.Canvas(size);
            Assert.Equal(112, border.WidthRequest); Assert.Equal(canvas.Width / canvas.Height, border.WidthRequest / border.HeightRequest, 10);
        }
        Assert.NotNull(Button(page, "Change" + size + "Slot1")); Assert.NotNull(Button(page, "Clear" + size + "Slot1"));
        Assert.NotNull(Button(page, "Assign" + size + "Slot2")); Assert.Empty(store.Writes);
    }
    [Fact]
    public async Task ChangingSizeKeepsAssignmentsSeparateAndOpensTheCorrectLockedAddress()
    {
        var small = Design(WidgetSize.Small); var large = Design(WidgetSize.Large);
        var store = new Store([small, large], [new(WidgetSize.Small, 1, small.Id), new(WidgetSize.Large, 1, large.Id)]);
        var opened = new List<(WidgetSize, int)>();
        var page = new WidgetSlotsPage(store, NullLogger.Instance, openAssignment: (size, number) => { opened.Add((size, number)); return Task.CompletedTask; }); Open(page);
        await page.SelectSizeAsync(WidgetSize.Large); Assert.Equal(WidgetSize.Large, page.SelectedSize);
        await page.OpenAssignAsync(WidgetSize.Small, 1); Assert.Empty(opened);
        await page.OpenAssignAsync(WidgetSize.Large, 3); Assert.Equal((WidgetSize.Large, 3), Assert.Single(opened));
        Assert.Equal(2, store.Library.Slots.Count); Assert.Empty(store.Writes);
    }
    [Fact]
    public async Task SearchIsSizeLimitedAndKeepsTheSelectionVisibleInTheFooter()
    {
        var first = Design(WidgetSize.Small, "First"); var second = Design(WidgetSize.Small, "Second"); var wrong = Design(WidgetSize.Medium, "Second medium");
        var store = new Store([first, second, wrong], [new(WidgetSize.Small, 2, first.Id)]);
        var page = Assignment(store, WidgetSize.Small); Open(page); Assert.Equal(first.Id, page.SelectedId);
        Descendants(page).OfType<SearchBar>().Single().Text = " SECOND ";
        Assert.Single(Descendants(page).OfType<WidgetDesignChoiceCard>());
        Assert.Contains(Descendants(page).OfType<Label>(), x => x.Text == "Selected: First");
        await page.ConfirmAsync(); Assert.Equal(first.Id, Assert.Single(store.Writes).Id);
    }
    [Fact]
    public async Task DeletedOrResizedSelectionCannotBeAssignedFromAStaleCard()
    {
        var design = Design(WidgetSize.Small); var store = new Store([design]); var page = Assignment(store, WidgetSize.Small); Open(page); page.Choose(design.Id);
        store.Library = store.Library with { Designs = [design with { Size = WidgetSize.Medium }] };
        await page.ConfirmAsync(); Assert.Empty(store.Writes); Assert.Null(page.SelectedId);
        Assert.False(Button(page, "ConfirmWidgetAssignment").IsEnabled);
        Assert.Contains(Descendants(page).OfType<Label>(), x => x.Text?.Contains("no longer available") == true);
    }
    [Fact]
    public async Task ClearCancelAndBackgroundedConfirmationLeaveTheSlotUntouched()
    {
        var design = Design(WidgetSize.Small); var store = new Store([design], [new(WidgetSize.Small, 1, design.Id)]);
        var answer = new TaskCompletionSource<bool>(); var page = new WidgetSlotsPage(store, NullLogger.Instance, confirmClear: _ => answer.Task); Open(page);
        var clearing = page.ClearAsync(WidgetSize.Small, 1); ((IWindow)host.App.Windows[0]).Deactivated();
        answer.SetResult(true); await clearing; Assert.Empty(store.Writes); Assert.Single(store.Library.Slots);
    }
    [Fact]
    public async Task ConfirmedClearChangesOnlyTheRequestedAddressAndKeepsTheSavedDesigns()
    {
        var design = Design(WidgetSize.Small); var store = new Store([design], [new(WidgetSize.Small, 1, design.Id), new(WidgetSize.Small, 2, design.Id)]);
        var page = new WidgetSlotsPage(store, NullLogger.Instance, confirmClear: _ => Task.FromResult(true)); Open(page);
        await page.ClearAsync(WidgetSize.Small, 1); Assert.Equal((WidgetSize.Small, 1, (Guid?)null), Assert.Single(store.Writes));
        Assert.Equal(2, Assert.Single(store.Library.Slots).Number); Assert.Single(store.Library.Designs);
    }
    [Fact]
    public async Task BackgroundingDuringFreshSelectionValidationCancelsTheWrite()
    {
        var design = Design(WidgetSize.Small); var store = new Store([design]); var page = Assignment(store, WidgetSize.Small); Open(page); page.Choose(design.Id);
        var reading = new TaskCompletionSource<WidgetLibraryCatalog>(); CancellationToken captured = default;
        store.Read = token => { captured = token; return reading.Task; };
        var assigning = page.ConfirmAsync(); ((IWindow)host.App.Windows[0]).Deactivated();
        Assert.True(captured.IsCancellationRequested); reading.SetResult(store.Library); await assigning;
        Assert.Empty(store.Writes); Assert.False(Button(page, "ConfirmWidgetAssignment").IsEnabled);
    }
    [Fact]
    public async Task DuplicateConfirmationsCannotWriteTwiceAndPublicationFailureStillReportsLocalSuccess()
    {
        var design = Design(WidgetSize.Small); var store = new Store([design]); var published = new TaskCompletionSource<bool>(); store.Publish = () => published.Task;
        var notifications = new List<bool>(); var page = Assignment(store, WidgetSize.Small, saved: notifications.Add); Open(page); page.Choose(design.Id);
        var first = page.ConfirmAsync(); await page.ConfirmAsync(); Assert.Single(store.Writes);
        published.SetResult(false); await first; Assert.False(Assert.Single(notifications)); Assert.Single(store.Library.Slots);
        await page.ConfirmAsync(); Assert.Single(store.Writes);
    }
    [Fact]
    public async Task FailedNavigationAfterACommittedAssignmentCannotRepeatTheWrite()
    {
        var design = Design(WidgetSize.Small); var store = new Store([design]);
        var page = Assignment(store, WidgetSize.Small, close: () => throw new IOException("navigation failed")); Open(page); page.Choose(design.Id);
        await page.ConfirmAsync(); await page.ConfirmAsync(); Assert.Single(store.Writes);
        Assert.Contains(Descendants(page).OfType<Label>(), x => x.Text?.Contains("Assignment saved. Use Back") == true);
    }
    [Fact]
    public void ReadFailureShowsRetryWithoutOfferingAStaleAssignment()
    {
        var store = new Store([]) { Read = _ => throw new IOException("Saved designs are unavailable.") };
        var page = Assignment(store, WidgetSize.Small); Open(page);
        Assert.False(Button(page, "ConfirmWidgetAssignment").IsEnabled);
        Assert.Contains(Descendants(page).OfType<Label>(), x => x.Text == "Saved designs are unavailable.");
    }
    [Fact]
    public async Task AFailedSizeSwitchCannotShowCardsFromThePreviousFamily()
    {
        var store = new Store([Design(WidgetSize.Small)]); var page = new WidgetSlotsPage(store, NullLogger.Instance); Open(page);
        Assert.Equal(10, Descendants(page).OfType<WidgetSlotCard>().Count());
        store.Read = _ => throw new IOException("Cannot read this library.");
        await page.SelectSizeAsync(WidgetSize.Large);
        Assert.Equal(WidgetSize.Large, page.SelectedSize); Assert.Empty(Descendants(page).OfType<WidgetSlotCard>());
        Assert.Contains(Descendants(page).OfType<Label>(), x => x.Text == "Cannot read this library.");
    }
    [Fact]
    public async Task AReadFromBeforePageDepartureCannotRepaintTheNextAppearance()
    {
        var old = Design(WidgetSize.Small, "Old"); var current = Design(WidgetSize.Small, "Current");
        var waiting = new TaskCompletionSource<WidgetLibraryCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new Store([old]) { Read = _ => waiting.Task }; var page = Assignment(store, WidgetSize.Small); Open(page);
        ((IPageController)page).SendDisappearing(); store.Read = null; store.Library = new(2, [current], []);
        ((IPageController)page).SendAppearing(); waiting.SetResult(new(2, [old], []));
        await WaitUntil(() => Descendants(page).OfType<Button>().Any(x => x.AutomationId == "ChooseWidgetDesign" + current.Id.ToString("N")));
        Assert.DoesNotContain(Descendants(page).OfType<Label>(), x => x.Text == "Old");
        page.Choose(current.Id); Assert.Equal(current.Id, page.SelectedId);
    }
    [Fact]
    public async Task MissingSavedRenderCannotCreateAnAssignmentWithAnUnavailablePreview()
    {
        var design = Design(WidgetSize.Medium); var store = new Store([design]) { MissingRender = true }; var page = Assignment(store, WidgetSize.Medium); Open(page);
        page.Choose(design.Id); Assert.False(Button(page, "ConfirmWidgetAssignment").IsEnabled);
        await page.ConfirmAsync(); Assert.Empty(store.Writes);
        Assert.Contains(Descendants(page).OfType<Label>(), x => x.Text?.Contains("Open Edit and save") == true);
    }
    [Fact]
    public void AssignmentFooterUsesItsOwnRowOutsideTheScrollArea()
    {
        var page = Assignment(new Store([Design(WidgetSize.Small)]), WidgetSize.Small); Open(page);
        var layout = Assert.IsType<Grid>(page.Content); var scroll = layout.Children.OfType<ScrollView>().Single();
        Assert.Equal(1, Grid.GetRow(scroll)); Assert.Equal(GridLength.Star, layout.RowDefinitions[1].Height);
        var footer = layout.Children.OfType<VerticalStackLayout>().Single(); Assert.Equal(2, Grid.GetRow(footer));
        Assert.Equal(GridLength.Auto, layout.RowDefinitions[2].Height);
        Assert.Contains(Descendants(footer).OfType<Button>(), x => x.AutomationId == "ConfirmWidgetAssignment");
    }
    [Fact]
    public void ReadableDisabledPrimaryActionRestoresTheLatestAccentAcrossThemes()
    {
        host.App.Resources["AccentButtonBrush"] = new SolidColorBrush(Colors.Blue);
        var action = new ClipboardEditorActionButton("Assign", null, "AssignTest", primary: true, readableDisabled: true); host.Attach(action);
        var surface = Descendants(action).OfType<AccentSurface>().Single();
        foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
        {
            host.App.UserAppTheme = theme; action.IsEnabled = false;
            var disabled = Assert.IsType<SolidColorBrush>(surface.Background).Color;
            Assert.True(AppearanceColors.Contrast(disabled, surface.Foreground) >= 4.5);
            host.App.Resources["AccentButtonBrush"] = new SolidColorBrush(Colors.Lime);
            Assert.Equal(disabled, Assert.IsType<SolidColorBrush>(surface.Background).Color);
            action.IsEnabled = true; Assert.Equal(Colors.Lime, Assert.IsType<SolidColorBrush>(surface.Background).Color);
            Assert.True(AppearanceColors.Contrast(Colors.Lime, surface.Foreground) >= 4.5);
        }
    }
    private static async Task WaitUntil(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(10);
        Assert.True(condition());
    }
    [Fact]
    public void FixedSizeSelectionRetainsValidDraftsAndRejectsRetiredFamilies()
    {
        var chosen = Design(WidgetSize.ExtraLargePortrait); var other = Design(WidgetSize.ExtraLarge, "Retired");
        var model = new WidgetSlotSelection(WidgetSize.ExtraLargePortrait, 10); model.Reload(new(2, [chosen, other], []));
        Assert.False(model.Choose(other.Id)); Assert.True(model.Choose(chosen.Id));
        model.Reload(new(2, [chosen with { Name = "Renamed" }], [])); Assert.Equal("Renamed", model.Selected?.Name);
        model.Reload(new(2, [], [])); Assert.Null(model.SelectedId);
        Assert.Throws<IOException>(() => new WidgetSlotSelection(WidgetSize.ExtraLarge, 1));
        Assert.Throws<IOException>(() => new WidgetSlotSelection(WidgetSize.Small, 11));
        Assert.Equal(1, WidgetSlotSelection.Columns(WidgetSize.Small, 200)); Assert.Equal(1, WidgetSlotSelection.Columns(WidgetSize.ExtraLargePortrait, 800));
    }
    private sealed class Store(IReadOnlyList<WidgetDesign> designs, IReadOnlyList<WidgetSlotAssignment>? slots = null) : IWidgetSlotStore
    {
        public WidgetLibraryCatalog Library = new(2, designs, slots ?? []);
        public Func<CancellationToken, Task<WidgetLibraryCatalog>>? Read;
        public Func<Task<bool>> Publish = () => Task.FromResult(true);
        public bool MissingRender;
        public readonly List<(WidgetSize Size, int Number, Guid? Id)> Writes = [];
        public Task<WidgetLibraryCatalog> ReadAsync(CancellationToken token) => Read?.Invoke(token) ?? Task.FromResult(Library);
        public string? RenderPath(string key) => MissingRender ? null : "/renders/" + key + ".png";
        public Task<bool> PublishAsync() => Publish();
        public async Task<bool> AssignAsync(WidgetSize size, int number, Guid? design, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); WidgetSlotRules.ValidateAddress(size, number);
            if (design is { } id && !Library.Designs.Any(x => x.Id == id && x.Size == size)) throw new IOException("Choose a matching saved design.");
            Writes.Add((size, number, design));
            var assignments = Library.Slots.Where(x => x.Size != size || x.Number != number).ToList();
            if (design is { } selected) assignments.Add(new(size, number, selected)); Library = Library with { Slots = assignments };
            return await Publish();
        }
    }
    public void Dispose() => host.Dispose();
}
