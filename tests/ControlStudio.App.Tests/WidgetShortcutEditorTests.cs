using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetShortcutEditorTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private static IEnumerable<Element> All(Element root)
    {
        yield return root;
        if (root is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var value in All(child)) yield return value;
    }
    private void Open(Page page)
    {
        host.App.Windows[0].Page = page;
        if (!host.App.Windows[0].IsActivated) ((IWindow)host.App.Windows[0]).Activated();
        ((IPageController)page).SendAppearing();
    }
    private static void Click(Page page, string id) => ((IButtonController)All(page).OfType<Button>().Single(x => x.AutomationId == id)).SendClicked();
    private static Entry Name(Page page) => All(page).OfType<Entry>().Single(x => x.AutomationId == "WidgetShortcutName");
    private static WidgetTapActionControls Settings(Page page) => All(page).OfType<WidgetTapActionControls>().Single();
    [Theory]
    [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void TapSettingsUndoRedoTestAndSaveWithoutInvalidatingThePreview(AppTheme theme)
    {
        host.App.UserAppTheme = theme; var store = new Store(); var original = WidgetDesignRules.New(); var requests = new List<Uri>();
        var page = new WidgetEditorPage(store, new Picker(), NullLogger.Instance, original,
            openShortcut: uri => { requests.Add(uri); return Task.FromResult(true); }); Open(page);
        Assert.Equal(WidgetTapAction.EditWidget, Settings(page).Action);
        Assert.False(Name(page).IsTextPredictionEnabled); Assert.False(Name(page).IsSpellCheckEnabled);
        Assert.Equal(ReturnType.Done, Name(page).ReturnType);
        Assert.False(All(page).OfType<Button>().Single(x => x.AutomationId == "WidgetUndo").IsEnabled);
        Click(page, "WidgetTapAction1"); Click(page, "SaveWidgetDesign"); Assert.Null(store.Saved);
        Assert.True(All(page).OfType<Label>().Single(x => x.AutomationId == "WidgetShortcutError").IsVisible);
        Name(page).Text = "  Pay & 日本語  ";
        ((IEntryController)Name(page)).SendCompleted(); Assert.Empty(requests); Assert.Null(store.Saved);
        Assert.Equal(WidgetTapAction.EditWidget, All(page).OfType<WidgetCompositeView>().Single().Design!.TapAction);
        Click(page, "WidgetUndo"); Assert.Equal("", Name(page).Text);
        Click(page, "WidgetUndo"); Assert.Equal(WidgetTapAction.EditWidget, Settings(page).Action);
        Click(page, "WidgetRedo"); Click(page, "WidgetRedo"); Assert.Equal("  Pay & 日本語  ", Name(page).Text);
        Click(page, "TestWidgetShortcut"); Assert.Equal(WidgetTapActions.ShortcutUri("Pay & 日本語"), Assert.Single(requests)); Assert.Null(store.Saved);
        Click(page, "SaveWidgetDesign"); Assert.NotNull(store.Saved); Assert.Equal(WidgetTapAction.RunShortcut, store.Saved.TapAction); Assert.Equal("Pay & 日本語", store.Saved.ShortcutName);
        Assert.Equal(WidgetTapAction.EditWidget, original.TapAction);
    }
    [Fact]
    public void InvalidUnicodeNameKeepsThePreviewAndDraftRecoverable()
    {
        var store = new Store(); var launches = 0;
        var page = new WidgetEditorPage(store, new Picker(), NullLogger.Instance,
            openShortcut: _ => { launches++; return Task.FromResult(true); }); Open(page);
        Click(page, "WidgetTapAction1"); Name(page).Text = new string('\uD800', 1);
        Assert.False(All(page).OfType<Button>().Single(x => x.AutomationId == "TestWidgetShortcut").IsEnabled);
        Click(page, "SaveWidgetDesign"); Assert.Null(store.Saved); Assert.Equal(0, launches);
        Assert.NotNull(All(page).OfType<WidgetCompositeView>().Single().Design);
        Assert.True(All(page).OfType<Label>().Single(x => x.AutomationId == "WidgetShortcutError").IsVisible);
        Click(page, "WidgetUndo"); Assert.Equal("", Name(page).Text);
    }
    [Fact]
    public async Task TestingDoesNotSaveAndRecoversFromFailedLaunchAndBackgroundResume()
    {
        var store = new Store(); var opening = new TaskCompletionSource<bool>(); var launches = 0;
        var page = new WidgetEditorPage(store, new Picker(), NullLogger.Instance,
            openShortcut: _ => { launches++; return opening.Task; }); Open(page);
        Click(page, "WidgetTapAction1"); Name(page).Text = "Evening";
        Click(page, "TestWidgetShortcut"); Click(page, "TestWidgetShortcut"); Assert.Equal(1, launches);
        Assert.False(page.CanAcceptWidgetTap); ((IWindow)host.App.Windows[0]).Deactivated();
        Name(page).Text = "Stale callback"; Assert.Equal("Evening", Name(page).Text);
        opening.SetResult(false); await Task.Yield(); await Task.Yield(); ((IWindow)host.App.Windows[0]).Activated();
        Assert.True(page.CanAcceptWidgetTap); Assert.Null(store.Saved); Assert.Equal("Evening", Name(page).Text);
        Assert.True(All(page).OfType<Button>().Single(x => x.AutomationId == "SaveWidgetDesign").IsEnabled);
        Assert.Contains(All(page).OfType<Label>(), x => x.Text?.Contains("Could not open Shortcuts") == true);
    }
    [Fact]
    public void ExistingShortcutDesignLoadsWithoutCreatingHistoryAndCanReturnToEdit()
    {
        var original = WidgetDesignRules.New() with { TapAction = WidgetTapAction.RunShortcut, ShortcutName = "Stored" }; var store = new Store();
        var page = new WidgetEditorPage(store, new Picker(), NullLogger.Instance, original); Open(page);
        Assert.Equal("Stored", Name(page).Text); Assert.Equal(WidgetTapAction.RunShortcut, Settings(page).Action);
        Assert.False(All(page).OfType<Button>().Single(x => x.AutomationId == "WidgetUndo").IsEnabled);
        Click(page, "WidgetTapAction0"); Click(page, "SaveWidgetDesign"); Assert.NotNull(store.Saved);
        Assert.Equal(WidgetTapAction.EditWidget, store.Saved.TapAction); Assert.Equal("", store.Saved.ShortcutName);
        Assert.Equal("Stored", original.ShortcutName);
    }
    private sealed class Picker : IColourPicker
    { public Task<string> PickAsync(string initialHex, Action<string> preview) => Task.FromResult(initialHex); }
    private sealed class Store : IWidgetEditorStore
    {
        public WidgetDesign? Saved;
        public string? AssetPath(string key) => null;
        public Task<WidgetLayer?> PickPhotoAsync(bool library) => Task.FromResult<WidgetLayer?>(null);
        public Task<WidgetSaveResult> SaveAsync(WidgetDesign design) { WidgetDesignRules.Validate(design); Saved = design; return Task.FromResult(new WidgetSaveResult(design, true)); }
        public Task RemoveDraftPhotosAsync(IEnumerable<string> keys) => Task.CompletedTask;
    }
    public void Dispose() => host.Dispose();
}
