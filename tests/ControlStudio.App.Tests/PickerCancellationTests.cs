using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.ApplicationModel;
using System.Reflection;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class PickerCancellationTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private static WidgetLayer Photo() => new(Guid.NewGuid(), "Photo 1", Guid.NewGuid().ToString("N"), 640, 480, new());
    private void Open(Page page)
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
    private static Button Button(Page page, string id) => Descendants(page).OfType<Button>().Single(x => x.AutomationId == id);
    // MAUI 10.0.110 uses its legacy navigation implementation on iOS. Select that
    // same implementation so departure callbacks do not depend on a native test handler.
    private static NavigationPage Navigation(Page root) => (NavigationPage)Activator.CreateInstance(
        typeof(NavigationPage), BindingFlags.Instance | BindingFlags.NonPublic, null, [false, root], null)!;
    private static WidgetEditorPage Editor(Store store, string source = "Photo Library", ColourPicker? colours = null)
        => new(store, colours ?? new(), NullLogger.Instance, WidgetDesignRules.New() with { Layers = [Photo()] }, choosePhotoSource: () => Task.FromResult<string?>(source));

    [Theory]
    [InlineData("Photo Library")] [InlineData("Files")]
    public async Task CancelSecondPhotoRestoresTheEntireEditorAndKeepsItsExistingLayer(string source)
    {
        var pending = new TaskCompletionSource<WidgetLayer?>();
        var store = new Store { Pick = _ => pending.Task }; var page = Editor(store, source); Open(page);
        var originalRow = Descendants(page).OfType<WidgetLayerRow>().Single();
        var picking = page.AddPhotoAsync();
        Assert.False(Button(page, "AddWidgetPhoto").IsEnabled); Assert.False(Button(page, "SaveWidgetDesign").IsEnabled);
        // A native full-screen picker can cover the page without a MAUI navigation change.
        // No corresponding Appearing/Activated callback is required to unlock after cancellation.
        ((IPageController)page).SendDisappearing();
        await page.AddPhotoAsync(); Assert.Equal(1, store.Picks);
        pending.SetResult(null); await picking;
        Assert.True(Button(page, "AddWidgetPhoto").IsEnabled); Assert.True(Button(page, "SaveWidgetDesign").IsEnabled);
        Assert.True(Button(page, "BackFromWidgetEditor").IsEnabled); Assert.True(Button(page, "CancelWidgetDesign").IsEnabled);
        Assert.Same(originalRow, Descendants(page).OfType<WidgetLayerRow>().Single());
        Assert.Empty(store.Removed); Assert.Equal(0, store.Saves); Assert.Equal(source == "Photo Library", store.Library);
        store.Pick = _ => Task.FromResult<WidgetLayer?>(Photo());
        await page.AddPhotoAsync(); Assert.Equal(2, store.Picks); Assert.Equal(2, Descendants(page).OfType<WidgetLayerRow>().Count());
    }

    [Fact]
    public async Task CancelWhileInactiveDoesNotPersistAnInactiveSnapshotInTheButtons()
    {
        var pending = new TaskCompletionSource<WidgetLayer?>(); var store = new Store { Pick = _ => pending.Task };
        var page = Editor(store); Open(page); var picking = page.AddPhotoAsync();
        ((IWindow)host.App.Windows[0]).Deactivated(); pending.SetResult(null); await picking;
        Assert.True(Button(page, "AddWidgetPhoto").IsEnabled);
        await page.AddPhotoAsync(); Assert.Equal(1, store.Picks); // Execution still rejects a background page.
        ((IWindow)host.App.Windows[0]).Activated();
        await page.AddPhotoAsync(); Assert.Equal(2, store.Picks);
    }

    [Fact]
    public async Task PickerFailureRestoresActionsWithoutRemovingExistingPhotos()
    {
        var store = new Store { Pick = _ => throw new IOException("Photo unavailable.") }; var page = Editor(store); Open(page);
        await page.AddPhotoAsync();
        Assert.True(Button(page, "AddWidgetPhoto").IsEnabled); Assert.True(Button(page, "SaveWidgetDesign").IsEnabled);
        Assert.Single(Descendants(page).OfType<WidgetLayerRow>()); Assert.Empty(store.Removed);
        Assert.Contains(Descendants(page).OfType<Label>(), x => x.Text == "Photo unavailable.");
    }

    [Fact]
    public async Task SourceSheetCancellationNeverStartsAnImportAndAllowsRetry()
    {
        var store = new Store(); var page = Editor(store, "Cancel"); Open(page);
        await page.AddPhotoAsync(); await page.AddPhotoAsync();
        Assert.Equal(0, store.Picks); Assert.Single(Descendants(page).OfType<WidgetLayerRow>());
        Assert.True(Button(page, "AddWidgetPhoto").IsEnabled); Assert.Empty(store.Removed);
    }

    [Fact]
    public async Task APhotoReturningAfterNavigationAwayIsCleanedUpWithoutReenablingTheDepartedEditor()
    {
        var pending = new TaskCompletionSource<WidgetLayer?>(); var store = new Store { Pick = _ => pending.Task };
        var page = Editor(store); var navigation = Navigation(new ContentPage()); Open(navigation);
        await navigation.PushAsync(page); var picking = page.AddPhotoAsync(); Assert.Equal(1, store.Picks);
        await navigation.PopAsync(); var imported = Photo(); pending.SetResult(imported); await picking;
        Assert.Equal(imported.AssetKey, Assert.Single(store.Removed)); Assert.Equal(0, store.Saves);
        Assert.False(Button(page, "SaveWidgetDesign").IsEnabled); Assert.False(Button(page, "AddWidgetPhoto").IsEnabled);
        await page.AddPhotoAsync(); Assert.Equal(1, store.Picks);
    }

    [Fact]
    public async Task ChangingTabsWhilePickingCannotStartAnOperationOnTheHiddenEditor()
    {
        var pending = new TaskCompletionSource<WidgetLayer?>(); var store = new Store { Pick = _ => pending.Task };
        var page = Editor(store); var editorNavigation = Navigation(page); var other = Navigation(new ContentPage()); var tabs = new TabbedPage();
        tabs.Children.Add(editorNavigation); tabs.Children.Add(other); Open(tabs); ((IPageController)page).SendAppearing();
        var picking = page.AddPhotoAsync(); Assert.Equal(1, store.Picks);
        tabs.CurrentPage = other; pending.SetResult(null); await picking;
        await page.AddPhotoAsync(); Assert.Equal(1, store.Picks);
        tabs.CurrentPage = editorNavigation;
        await page.AddPhotoAsync(); Assert.Equal(2, store.Picks);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SharedColourPickerRestoresWidgetActionsAfterDismissalOrFailure(bool fail)
    {
        var pending = new TaskCompletionSource<string>(); var colours = new ColourPicker { Pick = (_, _) => pending.Task };
        var page = Editor(new Store(), colours: colours); Open(page);
        var picker = Descendants(page).OfType<ColourPickerView>().Single();
        var picking = picker.PickAsync(); Assert.True(picker.IsBusy); Assert.False(Button(page, "SaveWidgetDesign").IsEnabled);
        ((IPageController)page).SendDisappearing();
        if (fail) pending.SetException(new IOException("picker unavailable")); else pending.SetResult("#12ABEF");
        await picking;
        Assert.False(picker.IsBusy); Assert.True(Button(page, "SaveWidgetDesign").IsEnabled);
        Assert.True(Button(page, "AddWidgetPhoto").IsEnabled);
        if (!fail) Assert.Equal("#12ABEF", picker.SelectedHex);
    }

    [Fact]
    public async Task ColourPickerReleasesItsBusyStateEvenWhenPersistenceNotificationThrows()
    {
        var picker = new ColourPickerView(new ColourPicker()); host.Attach(picker);
        var states = new List<bool>(); picker.BusyChanged += (_, _) => states.Add(picker.IsBusy);
        picker.ColourChanged += (_, _) => throw new IOException("preference write failure");
        await picker.PickAsync();
        Assert.False(picker.IsBusy); Assert.Equal(new[] { true, false }, states);
        Assert.Contains(Descendants(picker).OfType<Label>(), x => x.IsVisible && x.Text == "The colour could not be saved. Try again.");
    }

    [Theory]
    [InlineData("Photo Library")] [InlineData("Files")] [InlineData("Adjust current photo")]
    public async Task ProfilePickerOrCropCancellationPreservesTheExistingPhotoAndAllowsRetry(string source)
    {
        var preferences = Preferences(); var photoKey = Guid.NewGuid().ToString("N"); Assert.True(preferences.SaveProfile("Faikal", photoKey));
        var pending = new TaskCompletionSource<string?>(); var photos = new ProfilePhotos { Pick = () => pending.Task };
        var page = new ProfileEditorPage(preferences, photos, _ => Task.FromResult<string?>(source)); Open(page);
        var original = Descendants(page).OfType<Image>().Single().Source;
        var picking = page.ChoosePhotoAsync(); Assert.False(Button(page, "SaveProfile").IsEnabled);
        ((IPageController)page).SendDisappearing(); pending.SetResult(null); await picking;
        Assert.True(Button(page, "ChooseProfilePhoto").IsEnabled); Assert.True(Button(page, "SaveProfile").IsEnabled);
        Assert.True(Button(page, "ProfileCancel").IsEnabled); Assert.True(Button(page, "RemoveProfilePhoto").IsEnabled);
        Assert.Same(original, Descendants(page).OfType<Image>().Single().Source);
        Assert.Equal(photoKey, preferences.PhotoKey); Assert.Empty(photos.Removed);
        await page.ChoosePhotoAsync(); Assert.Equal(2, photos.Picks);
    }

    [Fact]
    public async Task ProfileImportAfterDepartureDiscardsOnlyTheNewPhotoAndLeavesControlsDisabled()
    {
        var preferences = Preferences(); var previous = Guid.NewGuid().ToString("N"); preferences.SaveProfile("Faikal", previous);
        var pending = new TaskCompletionSource<string?>(); var photos = new ProfilePhotos { Pick = () => pending.Task };
        var page = new ProfileEditorPage(preferences, photos, _ => Task.FromResult<string?>("Photo Library"));
        var navigation = Navigation(new ContentPage()); Open(navigation); await navigation.PushAsync(page);
        var picking = page.ChoosePhotoAsync(); await navigation.PopAsync(); var imported = Guid.NewGuid().ToString("N");
        pending.SetResult(imported); await picking;
        Assert.Equal(imported, Assert.Single(photos.Removed)); Assert.Equal(previous, preferences.PhotoKey);
        Assert.False(Button(page, "ChooseProfilePhoto").IsEnabled); Assert.False(Button(page, "SaveProfile").IsEnabled);
        Assert.False(Button(page, "ClearProfileName").IsEnabled); Assert.False(Button(page, "ProfileCancel").IsEnabled);
    }

    private static AppearancePreferences Preferences() => new(new Publisher(), NullLogger<AppearancePreferences>.Instance, new TestPreferences());
    private sealed class Publisher : ICounterAppearancePublisher { public void Publish(int themeIndex, string accentHex) { } }
    private sealed class ProfilePhotos : IProfilePhotoStore
    {
        public Func<Task<string?>> Pick { get; init; } = () => Task.FromResult<string?>(null);
        public int Picks { get; private set; }
        public List<string> Removed { get; } = [];
        public string? GetPath(string key) => string.IsNullOrEmpty(key) ? null : "/private/" + key + ".jpg";
        public Task<string?> PickAsync(bool fromLibrary) { Picks++; return Pick(); }
        public Task<string?> AdjustAsync(string key) { Picks++; return Pick(); }
        public void Remove(string key) => Removed.Add(key);
    }

    private sealed class ColourPicker : IColourPicker
    {
        public Func<string, Action<string>, Task<string>> Pick { get; set; } = (value, _) => Task.FromResult(value);
        public Task<string> PickAsync(string initialHex, Action<string> preview) => Pick(initialHex, preview);
    }
    private sealed class Store : IWidgetEditorStore
    {
        public Func<bool, Task<WidgetLayer?>> Pick { get; set; } = _ => Task.FromResult<WidgetLayer?>(null);
        public int Picks { get; private set; }
        public int Saves { get; private set; }
        public bool Library { get; private set; }
        public List<string> Removed { get; } = [];
        public string? AssetPath(string key) => "/private/" + key + ".png";
        public Task<WidgetLayer?> PickPhotoAsync(bool library) { Picks++; Library = library; return Pick(library); }
        public Task<WidgetSaveResult> SaveAsync(WidgetDesign draft) { Saves++; return Task.FromResult(new WidgetSaveResult(draft, true)); }
        public Task RemoveDraftPhotosAsync(IEnumerable<string> keys) { Removed.AddRange(keys); return Task.CompletedTask; }
    }
    public void Dispose() => host.Dispose();
}
