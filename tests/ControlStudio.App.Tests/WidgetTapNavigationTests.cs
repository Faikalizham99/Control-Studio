using System.Reflection;
using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetTapNavigationTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private static NavigationPage Navigation(Page page) => (NavigationPage)Activator.CreateInstance(typeof(NavigationPage), BindingFlags.Instance | BindingFlags.NonPublic, null, [false, page], null)!;
    private readonly Store store = new();
    private readonly List<(string Title, string Text)> messages = [];
    private TabbedPage tabs = null!;
    private NavigationPage home = null!, studio = null!;
    private bool nativeReady = true;
    private Action? creatingGallery;
    private WidgetTapNavigation Open()
    {
        home = Navigation(new ContentPage { AutomationId = "Home" });
        studio = Navigation(new ContentPage { AutomationId = "Studio" });
        tabs = new TabbedPage(); tabs.Children.Add(home); tabs.Children.Add(studio);
        host.App.Windows[0].Page = tabs; ((IWindow)host.App.Windows[0]).Activated();
        return new(tabs, home, studio, store,
            size => { creatingGallery?.Invoke(); return new ContentPage { AutomationId = "WidgetsPage", BindingContext = size }; },
            design => new WidgetEditorPage(store, new Picker(), NullLogger.Instance, design, choosePhotoSource: () => Task.FromResult<string?>("Photo Library")),
            size => new WidgetSlotsPage(store, NullLogger.Instance, size),
            (title, text) => { messages.Add((title, text)); return Task.CompletedTask; }, () => nativeReady);
    }
    [Theory]
    [InlineData(WidgetSize.Small)] [InlineData(WidgetSize.Medium)] [InlineData(WidgetSize.Large)] [InlineData(WidgetSize.ExtraLargePortrait)]
    public async Task AssignedSlotOpensNormalEditorWithBackRouteAndNoWrites(WidgetSize size)
    {
        var design = WidgetDesignRules.New() with { Size = size };
        store.Library = new(2, [design], [new(size, 20, design.Id)]);
        var navigation = Open();
        Assert.True(await navigation.OpenAsync(new(size, 20), default));
        Assert.Same(studio, tabs.CurrentPage);
        var editor = Assert.IsType<WidgetEditorPage>(studio.CurrentPage); Assert.Equal(design.Id, editor.DesignId);
        Assert.Equal(size, studio.Navigation.NavigationStack[1].BindingContext);
        Assert.Equal(3, studio.Navigation.NavigationStack.Count); Assert.Equal(0, store.Writes);
        await navigation.OpenAsync(new(size, 20), default);
        Assert.Same(editor, studio.CurrentPage); Assert.Equal(3, studio.Navigation.NavigationStack.Count); Assert.Empty(messages);
    }
    [Theory]
    [InlineData(WidgetSize.Small)] [InlineData(WidgetSize.Medium)] [InlineData(WidgetSize.Large)] [InlineData(WidgetSize.ExtraLargePortrait)]
    public async Task EmptySlotOpensMatchingSizeAndRepeatedTapReusesSlotsPage(WidgetSize size)
    {
        var navigation = Open();
        await navigation.OpenAsync(new(size, 20), default);
        var page = Assert.IsType<WidgetSlotsPage>(studio.CurrentPage); Assert.Equal(size, page.SelectedSize);
        await navigation.OpenAsync(new(size, 20), default);
        Assert.Same(page, studio.CurrentPage); Assert.Equal(3, studio.Navigation.NavigationStack.Count); Assert.Equal(0, store.Writes);
    }
    [Fact]
    public async Task DifferentWidgetTapKeepsDraftHistoryAndImportedPhotosIntact()
    {
        var first = WidgetDesignRules.New(); var other = WidgetDesignRules.New();
        store.Library = new(2, [first, other], [new(WidgetSize.Small, 1, first.Id), new(WidgetSize.Small, 20, other.Id)]);
        var navigation = Open(); await navigation.OpenAsync(new(WidgetSize.Small, 1), default);
        var original = studio.CurrentPage;
        var name = Descendants(original).OfType<ProfileNameEntry>().Single(); name.Text = "Unsaved name";
        await navigation.OpenAsync(new(WidgetSize.Small, 20), default);
        Assert.Same(original, studio.CurrentPage); Assert.Equal("Unsaved name", name.Text);
        Assert.Contains("edits are kept", Assert.Single(messages).Text);
        Assert.True(Descendants(original).OfType<Button>().Single(x => x.AutomationId == "WidgetUndo").IsEnabled);
        Assert.Equal(0, store.Writes); Assert.Equal(0, store.Removals);
    }
    [Fact]
    public async Task AssignmentChangedWhileOpeningGalleryUsesLatestSavedDesign()
    {
        var old = WidgetDesignRules.New(); var replacement = WidgetDesignRules.New();
        store.Library = new(2, [old, replacement], [new(WidgetSize.Small, 20, old.Id)]);
        var navigation = Open();
        creatingGallery = () => store.Library = store.Library with { Slots = [new(WidgetSize.Small, 20, replacement.Id)] };
        await navigation.OpenAsync(new(WidgetSize.Small, 20), default);
        Assert.Equal(replacement.Id, Assert.IsType<WidgetEditorPage>(studio.CurrentPage).DesignId);
    }
    [Fact]
    public async Task CounterOpensHomeAndKeepsExistingStudioEditor()
    {
        var design = WidgetDesignRules.New(); store.Library = new(2, [design], [new(WidgetSize.Small, 1, design.Id)]);
        var navigation = Open(); await navigation.OpenAsync(new(WidgetSize.Small, 1), default);
        var editor = studio.CurrentPage;
        await navigation.OpenAsync(new(), default);
        Assert.Same(home, tabs.CurrentPage); Assert.Same(editor, studio.CurrentPage); Assert.Equal(0, store.Removals);
    }
    [Fact]
    public async Task CancelledPhotoPickerDefersTapAndRecoversWithoutLosingTheEditor()
    {
        var design = WidgetDesignRules.New(); store.Library = new(2, [design], [new(WidgetSize.Small, 1, design.Id)]);
        var navigation = Open(); await navigation.OpenAsync(new(WidgetSize.Small, 1), default);
        var page = Assert.IsType<WidgetEditorPage>(studio.CurrentPage);
        var picking = new TaskCompletionSource<WidgetLayer?>(); store.Pick = () => picking.Task;
        var picker = page.AddPhotoAsync(); Assert.False(page.CanAcceptWidgetTap); Assert.False(navigation.Ready);
        using var router = new WidgetTapRouter(() => navigation.Ready, navigation.OpenAsync, _ => Task.CompletedTask);
        router.SetActive(true); router.Submit("controlstudio://widget/small/1");
        var reads = store.Reads; await router.ProcessAsync(); Assert.Equal(reads, store.Reads); Assert.True(router.HasPending);
        picking.SetResult(null); await picker; Assert.True(navigation.Ready);
        await router.ProcessAsync(); Assert.False(router.HasPending); Assert.Same(page, studio.CurrentPage);
        Assert.Empty(messages); Assert.Equal(0, store.Writes); Assert.Equal(0, store.Removals);
    }
    [Fact]
    public async Task BackgroundedOrCoveredWindowDefersWithoutReadingOrNavigating()
    {
        var navigation = Open(); nativeReady = false;
        Assert.False(navigation.Ready); Assert.False(await navigation.OpenAsync(new(WidgetSize.Small, 1), default)); Assert.Equal(0, store.Reads);
        nativeReady = true; ((IWindow)host.App.Windows[0]).Deactivated();
        Assert.False(navigation.Ready); Assert.False(await navigation.OpenAsync(new(WidgetSize.Small, 1), default)); Assert.Equal(0, store.Reads);
    }
    [Fact]
    public async Task CancellationOrModalDuringReadCannotOpenAStaleEditor()
    {
        var navigation = Open(); var reading = new TaskCompletionSource<WidgetLibraryCatalog>();
        store.Read = _ => reading.Task;
        using var cancellation = new CancellationTokenSource();
        var operation = navigation.OpenAsync(new(WidgetSize.Small, 1), cancellation.Token);
        cancellation.Cancel(); reading.SetResult(store.Library);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Single(studio.Navigation.NavigationStack);
        store.Read = _ => { nativeReady = false; return Task.FromResult(store.Library); };
        Assert.False(await navigation.OpenAsync(new(WidgetSize.Small, 1), default)); Assert.Single(studio.Navigation.NavigationStack);
    }
    private static IEnumerable<Element> Descendants(Element element)
    {
        yield return element;
        if (element is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var nested in Descendants(child)) yield return nested;
    }
    private sealed class Picker : IColourPicker
    { public Task<string> PickAsync(string initialHex, Action<string> preview) => Task.FromResult(initialHex); }
    private sealed class Store : IWidgetSlotStore, IWidgetEditorStore
    {
        public WidgetLibraryCatalog Library = new(2, [], []);
        public Func<CancellationToken, Task<WidgetLibraryCatalog>>? Read;
        public Func<Task<WidgetLayer?>> Pick = () => Task.FromResult<WidgetLayer?>(null);
        public int Reads, Writes, Removals;
        public Task<WidgetLibraryCatalog> ReadAsync(CancellationToken token) { Reads++; return Read?.Invoke(token) ?? Task.FromResult(Library); }
        public string? RenderPath(string key) => null;
        public Task<bool> AssignAsync(WidgetSize size, int number, Guid? design, CancellationToken token) { Writes++; return Task.FromResult(true); }
        public Task<bool> PublishAsync() => Task.FromResult(true);
        public string? AssetPath(string key) => null;
        public Task<WidgetLayer?> PickPhotoAsync(bool library) => Pick();
        public Task<WidgetSaveResult> SaveAsync(WidgetDesign draft) { Writes++; return Task.FromResult(new WidgetSaveResult(draft, true)); }
        public Task RemoveDraftPhotosAsync(IEnumerable<string> keys) { Removals++; return Task.CompletedTask; }
    }
    public void Dispose() => host.Dispose();
}
