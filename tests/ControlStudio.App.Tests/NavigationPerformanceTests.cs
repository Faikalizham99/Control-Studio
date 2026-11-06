using System.Diagnostics;
using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.ApplicationModel;
using Xunit;
using Xunit.Abstractions;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class NavigationPerformanceTests(ITestOutputHelper output) : IDisposable
{
    private readonly MauiTestContext host = new();
    private static IEnumerable<Element> All(Element root)
    {
        yield return root;
        if (root is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var item in All(child)) yield return item;
    }
    private void Open(ContentPage page)
    {
        host.App.Windows[0].Page = page;
        if (!host.App.Windows[0].IsActivated) ((IWindow)host.App.Windows[0]).Activated();
        ((IPageController)page).SendAppearing();
    }
    private static Button Button(Element page, string id) => All(page).OfType<Button>().Single(x => x.AutomationId == id);
    private static void Return(ContentPage page)
    {
        ((IPageController)page).SendDisappearing(); ((IPageController)page).SendAppearing();
    }
    [Fact]
    public void GalleryReusesFreshEqualModelsButRebuildsChangedDataAndReleasesHiddenSources()
    {
        var store = new Store(2); var page = new WidgetsPage(store, new Picker(), NullLogger.Instance); Open(page);
        var initial = All(page).OfType<WidgetPreviewFrame>().ToArray();
        store.Designs = store.Designs.Select(x => x with { Layers = x.Layers.Select(layer => layer with { }).ToArray() }).ToArray();
        Return(page); Assert.Equal(initial, All(page).OfType<WidgetPreviewFrame>().ToArray());
        store.Designs = store.Designs.Select(x => x with { Name = x.Name + " changed" }).ToArray();
        Return(page); Assert.NotSame(initial[0], All(page).OfType<WidgetPreviewFrame>().First());
        var previous = All(page).OfType<WidgetPreviewFrame>().First(); store.PathSuffix = "-new";
        Return(page); Assert.NotSame(previous, All(page).OfType<WidgetPreviewFrame>().First());
        previous = All(page).OfType<WidgetPreviewFrame>().First(); store.Stamp = store.Stamp.AddSeconds(1);
        Return(page); Assert.NotSame(previous, All(page).OfType<WidgetPreviewFrame>().First());
        previous = All(page).OfType<WidgetPreviewFrame>().First();
        store.Designs = [store.Designs[0] with { Layers = [store.Designs[0].Layers[0] with { Opacity = .25 }] }];
        Return(page); Assert.Single(All(page).OfType<WidgetPreviewFrame>()); Assert.NotSame(previous, All(page).OfType<WidgetPreviewFrame>().First());
        foreach (var preview in All(page).OfType<WidgetRenderPreview>()) preview.SetVisible(true);
        Assert.All(All(page).OfType<Image>(), image => Assert.NotNull(image.Source));
        ((IPageController)page).SendDisappearing(); Assert.All(All(page).OfType<Image>(), image => Assert.Null(image.Source));
    }
    [Fact]
    public async Task PassivePublicationDoesNotBlockEditingAndItsLateResultCannotOverwriteForcedRefresh()
    {
        var passive = new TaskCompletionSource<bool>(); var forced = new TaskCompletionSource<bool>();
        var store = new Store { Publish = (force, _) => force ? forced.Task : passive.Task };
        var page = new WidgetsPage(store, new Picker(), NullLogger.Instance); Open(page);
        Assert.Equal(1, store.Publications); Assert.True(All(page).OfType<WidgetAddButton>().Single().IsEnabled);
        Assert.False(All(page).OfType<PageLoadIndicator>().Single().IsVisible);
        var refresh = All(page).OfType<WidgetRefreshButton>().Single(); ((IButtonController)All(refresh).OfType<Button>().Single()).SendClicked();
        Assert.Equal(2, store.Publications); Assert.False(All(page).OfType<WidgetAddButton>().Single().IsEnabled);
        forced.SetResult(true); await Task.Yield(); await Task.Yield();
        passive.SetResult(false); await Task.Yield(); await Task.Yield();
        Assert.True(All(page).OfType<WidgetAddButton>().Single().IsEnabled);
        Assert.DoesNotContain(All(page).OfType<Label>(), x => x.IsVisible && x.Text?.Contains("retry the Home Screen") == true);
        ((IPageController)page).SendDisappearing();
    }
    [Fact]
    public async Task CancelledLibraryReadDoesNotRestoreOldCardsAndReadOnlyBackRemainsAvailable()
    {
        var store = new Store(); var pending = new TaskCompletionSource<IReadOnlyList<WidgetDesign>>();
        store.Read = _ => pending.Task;
        var page = new WidgetsPage(store, new Picker(), NullLogger.Instance); Open(page);
        Assert.True(Button(page, "BackFromWidgets").IsEnabled);
        Assert.True(All(page).OfType<PageLoadIndicator>().Single().IsVisible);
        Assert.False(All(page).OfType<WidgetAddButton>().Single().IsEnabled);
        ((IPageController)page).SendDisappearing();
        Assert.False(All(page).OfType<PageLoadIndicator>().Single().IsVisible);
        pending.SetResult(store.Designs); await Task.Yield(); await Task.Yield();
        Assert.Empty(All(page).OfType<WidgetPreviewFrame>());
        store.Read = null; ((IPageController)page).SendAppearing();
        Assert.Single(All(page).OfType<WidgetPreviewFrame>());
        ((IPageController)page).SendDisappearing();
    }
    [Fact]
    public async Task GalleryComparesDecodedBatteryStopsByValueAndRevalidatesOnForegrounding()
    {
        var store = new Store(); store.Designs = [store.Designs[0] with { Layers = [WidgetBatteries.New()] }];
        var page = new WidgetsPage(store, new Picker(), NullLogger.Instance); Open(page);
        var card = All(page).OfType<WidgetPreviewFrame>().Single();
        var battery = store.Designs[0].Layers[0];
        store.Designs = [store.Designs[0] with { Layers = [battery with { Battery = battery.Battery! with { Stops = battery.Battery.Stops.ToArray() } }] }];
        Return(page); Assert.Same(card, All(page).OfType<WidgetPreviewFrame>().Single());
        store.Designs = [store.Designs[0] with { Layers = [battery with { Battery = battery.Battery! with { ShowPercentage = false } }] }];
        Return(page); Assert.NotSame(card, All(page).OfType<WidgetPreviewFrame>().Single());
        var pending = new TaskCompletionSource<IReadOnlyList<WidgetDesign>>(); store.Read = _ => pending.Task;
        ((IWindow)host.App.Windows[0]).Deactivated(); ((IWindow)host.App.Windows[0]).Activated();
        Assert.All(All(page).OfType<Image>(), x => Assert.Null(x.Source)); Assert.True(Button(page, "BackFromWidgets").IsEnabled);
        ((IPageController)page).SendDisappearing(); pending.SetResult([]); await Task.Yield(); await Task.Yield();
    }
    [Fact]
    public void FontAndBlendPanelsBuildOnlyWhenOpenedAndKeepSavedSelections()
    {
        var changes = 0;
        var fonts = new WidgetClockFontPicker(_ => changes++, _ => changes++, defer: true);
        fonts.Update(WidgetClockFont.SfProRounded, WidgetClockWeight.Light); fonts.SetSample("Rounded");
        Assert.Null(fonts.Content); fonts.EnsureLoaded(); var content = fonts.Content;
        Assert.Equal(WidgetClockFont.SfProRounded, fonts.SelectedFont);
        Assert.Equal(WidgetClockWeight.Light, All(fonts).OfType<WidgetClockWeightPicker>().Single().SelectedWeight);
        fonts.SetSample("Hello");
        Assert.Contains(All(fonts).OfType<Label>(), x => x.Text == WidgetClockFonts.Title(WidgetClockFont.SfProRounded));
        fonts.EnsureLoaded(); Assert.Same(content, fonts.Content); Assert.Equal(0, changes);
        var layer = new Store().Designs[0].Layers[0] with { BlendMode = WidgetBlendMode.Multiply };
        var blend = new WidgetBlendPanel(_ => changes++, defer: true); Assert.Null(blend.Content);
        blend.Update(layer); Assert.Null(blend.Content); blend.Show(layer); content = blend.Content;
        Assert.Contains("selected", SemanticProperties.GetDescription(Button(blend, "WidgetBlendModeMultiply")));
        blend.IsVisible = false; blend.Show(layer); Assert.Same(content, blend.Content); Assert.Equal(0, changes);
    }
    [Fact]
    public void Managed_construction_and_unchanged_gallery_probe()
    {
        // Headless control/data work only: these numbers do not measure UIKit
        // frames, native image decoding, AOT launch, or actual iPhone tap latency.
        var store = new Store(50); var picker = new Picker();
        var elapsed = new List<double>(); var allocations = new List<long>(); var nodes = new List<int>();
        for (var i = 0; i < 8; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
            var editor = new WidgetEditorPage(store, picker, NullLogger.Instance, store.Designs[0]);
            watch.Stop(); var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.NotNull(All(editor).OfType<WidgetCompositeView>().Single().Design);
            if (i == 0) output.WriteLine($"Editor cold: {watch.Elapsed.TotalMilliseconds:F3} ms; {bytes} B");
            else { elapsed.Add(watch.Elapsed.TotalMilliseconds); allocations.Add(bytes); nodes.Add(All(editor).Count()); }
        }
        output.WriteLine($"Editor warm median: {elapsed.Order().ElementAt(3):F3} ms; {allocations.Order().ElementAt(3)} B; {nodes.Order().ElementAt(3)} elements");
        var gallery = new WidgetsPage(store, picker, NullLogger.Instance); Open(gallery);
        Assert.Equal(50, All(gallery).OfType<WidgetPreviewFrame>().Count());
        elapsed.Clear(); allocations.Clear();
        for (var i = 0; i < 7; i++)
        {
            ((IPageController)gallery).SendDisappearing();
            var before = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
            ((IPageController)gallery).SendAppearing(); watch.Stop();
            allocations.Add(GC.GetAllocatedBytesForCurrentThread() - before); elapsed.Add(watch.Elapsed.TotalMilliseconds);
            Assert.Equal(50, All(gallery).OfType<WidgetPreviewFrame>().Count());
        }
        output.WriteLine($"Gallery unchanged return median: {elapsed.Order().ElementAt(3):F3} ms; {allocations.Order().ElementAt(3)} B");
        ((IPageController)gallery).SendDisappearing();
    }
    internal sealed class Picker : IColourPicker
    { public Task<string> PickAsync(string initialHex, Action<string> preview) => Task.FromResult(initialHex); }
    internal sealed class Store : IWidgetGalleryStore
    {
        public IReadOnlyList<WidgetDesign> Designs;
        public Func<CancellationToken, Task<IReadOnlyList<WidgetDesign>>>? Read;
        public Func<bool, CancellationToken, Task<bool>> Publish = (_, _) => Task.FromResult(true);
        public string PathSuffix = "";
        public DateTimeOffset Stamp = DateTimeOffset.UnixEpoch;
        public int Reads, Publications;
        public Store(int count = 1)
        {
            Designs = Enumerable.Range(0, count).Select(i => WidgetDesignRules.New() with
            {
                Name = "Design " + i, Layers = [new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 640, 480, new())]
            }).ToArray();
        }
        public bool HasSavedLibrary => Designs.Count > 0;
        public Task<IReadOnlyList<WidgetDesign>> ReadAsync(CancellationToken token) { Reads++; return Read?.Invoke(token) ?? Task.FromResult(Designs); }
        public string? RenderPath(string key) => "/private/" + key + PathSuffix + ".png";
        public DateTimeOffset? RenderUpdatedAt(string key) => Stamp;
        public Task<bool> PublishAsync(bool force, CancellationToken token) { Publications++; return Publish(force, token); }
        public Task<bool> DeleteAsync(Guid id) { Designs = Designs.Where(x => x.Id != id).ToArray(); return Task.FromResult(true); }
        public IWidgetSlotStore Slots => throw new NotSupportedException();
        public string? AssetPath(string key) => "/private/" + key + ".png";
        public Task<WidgetLayer?> PickPhotoAsync(bool library) => Task.FromResult<WidgetLayer?>(null);
        public Task<WidgetSaveResult> SaveAsync(WidgetDesign draft) => Task.FromResult(new WidgetSaveResult(draft, true));
        public Task RemoveDraftPhotosAsync(IEnumerable<string> keys) => Task.CompletedTask;
    }
    public void Dispose() => host.Dispose();
}
