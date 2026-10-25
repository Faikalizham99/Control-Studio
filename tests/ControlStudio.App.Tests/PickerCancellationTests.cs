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
    [InlineData(false)] [InlineData(true)]
    public void LayerLockAndVisibilityKeepTheirControlsMountedAndTheCurrentPanelOpen(bool clockLayer)
    {
        var layer = clockLayer ? WidgetClocks.New() : Photo(); var other = Photo() with { Name = "Other photo" }; var store = new Store();
        var original = WidgetDesignRules.New() with { Layers = [other, layer] };
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original); Open(page);
        void Click(string id) => ((IButtonController)Button(page, id)).SendClicked();
        Click(clockLayer ? "ClockToolFont" : "ClipboardAdjustphotoopacity");
        var panel = clockLayer ? (View)Descendants(page).OfType<WidgetClockControls>().Single() : (View)Descendants(page).OfType<Slider>().Single(x => SemanticProperties.GetDescription(x) == "Selected photo opacity").Parent!;
        var rows = Descendants(page).OfType<WidgetLayerRow>().ToArray(); var row = rows.Single(x => x.LayerId == layer.Id);
        var toggle = Descendants(row).OfType<Switch>().Single(); var lockControl = Descendants(row).OfType<ClipboardIconButton>().Single(x => x.AutomationId == "WidgetLayerLock" + layer.Id.ToString("N"));
        var composition = Descendants(page).OfType<WidgetCompositeView>().Single();
        for (var i = 0; i < 2; i++)
        {
            ((IButtonController)Descendants(lockControl).OfType<Button>().Single()).SendClicked();
            Assert.Equal(i == 0, composition.Design!.Layers.Last().Locked); Assert.True(panel.IsVisible);
            toggle.IsToggled = !toggle.IsToggled; Assert.Equal(toggle.IsToggled, composition.Design.Layers.Last().Visible);
            Assert.True(panel.IsVisible); Assert.Equal(rows, Descendants(page).OfType<WidgetLayerRow>().ToArray());
            Assert.Same(toggle, Descendants(row).OfType<Switch>().Single());
            Assert.Contains("selected", SemanticProperties.GetDescription(Button(page, "SelectWidgetLayer" + layer.Id.ToString("N"))));
        }
        Assert.Equal(0, store.Saves); Assert.Equal(layer, original.Layers.Last());
    }
    [Theory]
    [InlineData("Cancel")] [InlineData(null)]
    public void BrowsingAnotherLayersOptionsDoesNotChangeSelectionOrCloseSettings(string? result)
    {
        var first = Photo(); var second = WidgetClocks.New(); var store = new Store(); var calls = 0;
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, WidgetDesignRules.New() with { Layers = [first, second] },
            chooseLayerAction: (_, _) => { calls++; return Task.FromResult(result); }); Open(page);
        ((IButtonController)Button(page, "ClockToolFont")).SendClicked();
        var controls = Descendants(page).OfType<WidgetClockControls>().Single();
        var otherRow = Descendants(page).OfType<WidgetLayerRow>().Single(x => x.LayerId == first.Id);
        var options = Descendants(otherRow).OfType<ClipboardIconButton>().Single(x => x.AutomationId == "WidgetLayerOptions" + first.Id.ToString("N"));
        ((IButtonController)Descendants(options).OfType<Button>().Single()).SendClicked();
        Assert.Equal(1, calls); Assert.True(controls.IsVisible); Assert.Equal(WidgetClockTool.Font, controls.ActiveTool);
        Assert.Contains("selected", SemanticProperties.GetDescription(Button(page, "SelectWidgetLayer" + second.Id.ToString("N"))));
        Assert.DoesNotContain("selected", SemanticProperties.GetDescription(Button(page, "SelectWidgetLayer" + first.Id.ToString("N"))));
        Assert.Equal(0, store.Saves); Assert.True(Button(page, "SaveWidgetDesign").IsEnabled);
    }
    [Fact]
    public void MenuLockKeepsPrecisionMountedAndDisabledUntilUnlocked()
    {
        var photo = Photo(); var action = "Size, position & crop"; var store = new Store();
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, WidgetDesignRules.New() with { Layers = [photo] }, chooseLayerAction: (_, _) => Task.FromResult<string?>(action)); Open(page);
        var row = Descendants(page).OfType<WidgetLayerRow>().Single();
        var options = Descendants(row).OfType<ClipboardIconButton>().Single(x => x.AutomationId == "WidgetLayerOptions" + photo.Id.ToString("N"));
        void Menu() => ((IButtonController)Descendants(options).OfType<Button>().Single()).SendClicked();
        Menu(); var precise = Descendants(page).OfType<VerticalStackLayout>().Single(x => x.Children.OfType<Label>().Any(l => l.Text == photo.Name + " · Size, position & crop"));
        Assert.True(precise.IsVisible); Assert.True(precise.IsEnabled);
        action = "Lock layer"; Menu(); Assert.True(precise.IsVisible); Assert.False(precise.IsEnabled);
        var frame = Descendants(page).OfType<WidgetCompositeView>().Single().Design!.Layers[0].Frame;
        action = "Unlock layer"; Menu(); Assert.True(precise.IsVisible); Assert.True(precise.IsEnabled);
        Assert.Equal(frame, Descendants(page).OfType<WidgetCompositeView>().Single().Design!.Layers[0].Frame); Assert.Equal(0, store.Saves);
    }
    [Fact]
    public void InactiveLayerActionsDoNotRefreshOrSelectOverTheCurrentClock()
    {
        var photo = Photo(); var clock = WidgetClocks.New(); var store = new Store();
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, WidgetDesignRules.New() with { Layers = [photo, clock] },
            chooseLayerAction: (_, _) => Task.FromResult<string?>("Lock layer")); Open(page);
        ((IButtonController)Button(page, "ClockToolFont")).SendClicked();
        var controls = Descendants(page).OfType<WidgetClockControls>().Single(); var refreshes = 0; controls.LayoutChanging += (_, _) => refreshes++;
        var row = Descendants(page).OfType<WidgetLayerRow>().Single(x => x.LayerId == photo.Id);
        var options = Descendants(row).OfType<ClipboardIconButton>().Single(x => x.AutomationId == "WidgetLayerOptions" + photo.Id.ToString("N"));
        ((IButtonController)Descendants(options).OfType<Button>().Single()).SendClicked();
        Descendants(row).OfType<Switch>().Single().IsToggled = false;
        Assert.Equal(0, refreshes); Assert.Equal(WidgetClockTool.Font, controls.ActiveTool); Assert.True(controls.IsVisible);
        var changed = Descendants(page).OfType<WidgetCompositeView>().Single().Design!;
        Assert.True(changed.Layers[0].Locked); Assert.False(changed.Layers[0].Visible); Assert.Equal(clock, changed.Layers[1]);
        Assert.Contains("selected", SemanticProperties.GetDescription(Button(page, "SelectWidgetLayer" + clock.Id.ToString("N")))); Assert.Equal(0, store.Saves);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancelledAddLayerKeepsExistingPhotoAndClockSettingsAcrossCoverage(bool clockLayer)
    {
        var layer = clockLayer ? WidgetClocks.New() : Photo(); WidgetEditorPage? page = null; var store = new Store();
        page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, WidgetDesignRules.New() with { Layers = [layer] },
            chooseLayerType: () => { ((IPageController)page!).SendDisappearing(); ((IWindow)host.App.Windows[0]).Deactivated(); return Task.FromResult<string?>("Cancel"); }); Open(page);
        ((IButtonController)Button(page, clockLayer ? "ClockToolFont" : "ClipboardBlendselectedphoto")).SendClicked();
        await page.AddLayerAsync(); ((IWindow)host.App.Windows[0]).Activated(); ((IPageController)page).SendAppearing();
        if (clockLayer) Assert.Equal(WidgetClockTool.Font, Descendants(page).OfType<WidgetClockControls>().Single().ActiveTool);
        else Assert.True(Descendants(page).OfType<WidgetBlendPanel>().Single().IsVisible);
        Assert.True(Button(page, "SaveWidgetDesign").IsEnabled); Assert.Equal(layer, Descendants(page).OfType<WidgetCompositeView>().Single().Design!.Layers[0]); Assert.Equal(0, store.Saves);
    }
    [Fact]
    public void PhotoAndClockSettingsKeepViewportAndManualStripPositionsWithoutScrollRequests()
    {
        var photo = Photo(); var clock = WidgetClocks.New(); var store = new Store();
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, WidgetDesignRules.New() with { Layers = [photo, clock] }); Open(page);
        var scroll = Descendants(page).OfType<WidgetEditorScrollView>().Single();
        ((IView)scroll).Arrange(new Rect(0, 0, 350, 600)); scroll.SetScrolledPosition(0, 900);
        var canvas = Descendants(page).OfType<WidgetCanvasView>().Single(); var parent = canvas.Parent; var bounds = new Size(canvas.WidthRequest, canvas.HeightRequest);
        var requests = 0; foreach (var viewport in Descendants(page).OfType<ScrollView>()) viewport.ScrollToRequested += (_, _) => requests++;
        void Click(string id) => ((IButtonController)Button(page, id)).SendClicked();
        void Stable(double offset)
        {
            Assert.Equal(offset, scroll.ScrollY); Assert.Equal(0, requests); Assert.Same(parent, canvas.Parent);
            Assert.Equal(bounds, new Size(canvas.WidthRequest, canvas.HeightRequest));
            Assert.True(scroll.Content.MinimumHeightRequest >= offset + scroll.Height);
        }
        Click("ClockToolFont"); Click("ClockFont7"); Click("ClockWeight7Light"); Stable(900);
        var strips = Descendants(page).OfType<WidgetHorizontalStrip>().SelectMany(x => Descendants(x).OfType<ScrollView>()).ToArray();
        foreach (var strip in strips) strip.SetScrolledPosition(110, 0);
        Click("ClockWeight7Heavy"); Click("ClockToolOpacity"); Stable(900);
        Assert.All(strips, strip => Assert.Equal(110, strip.ScrollX));
        scroll.SetScrolledPosition(0, 700); Click("ClockToolTime"); Stable(700);
        Click("SelectWidgetLayer" + photo.Id.ToString("N"));
        foreach (var tool in new[] { "ClipboardRotateselectedphoto", "ClipboardFlipselectedphoto", "ClipboardAdjustphotoopacity", "ClipboardBlendselectedphoto" })
        { Click(tool); Stable(700); }
        scroll.SetScrolledPosition(0, 1200); scroll.ReleaseViewport();
        Click("WidgetBlendGroup1"); Stable(1200); Click("CloseWidgetBlendPanel"); Stable(1200);
        Assert.Equal(0, store.Saves);
    }
    [Fact]
    public void SwitchingClockPanelsCapturesViewportAndShowsDestinationBeforeHidingSource()
    {
        var page = new WidgetEditorPage(new Store(), new ColourPicker(), NullLogger.Instance, WidgetDesignRules.New() with { Layers = [WidgetClocks.New()] }); Open(page);
        var scroll = Descendants(page).OfType<WidgetEditorScrollView>().Single(); ((IView)scroll).Arrange(new Rect(0, 0, 350, 600));
        ((IButtonController)Button(page, "ClockToolFont")).SendClicked(); scroll.SetScrolledPosition(0, 1100); scroll.ReleaseViewport();
        var source = Descendants(page).OfType<Border>().Single(x => x.AutomationId == "ClockPanelFont");
        var destination = Descendants(page).OfType<Border>().Single(x => x.AutomationId == "ClockPanelTime"); var hidden = false;
        source.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(source.IsVisible) || source.IsVisible) return;
            hidden = true; Assert.True(destination.IsVisible); Assert.True(scroll.Content.MinimumHeightRequest >= 1700);
        };
        ((IButtonController)Button(page, "ClockToolTime")).SendClicked(); Assert.True(hidden); Assert.Equal(1100, scroll.ScrollY);
    }
    [Fact]
    public void RoundedSystemWeightEditsRespectLocksActivityAndSupportedFontChanges()
    {
        var clock = WidgetClocks.New() with { Locked = true }; var original = WidgetDesignRules.New() with { Layers = [clock] }; var store = new Store();
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original); Open(page);
        var controls = Descendants(page).OfType<WidgetClockControls>().Single();
        var composition = Descendants(page).OfType<WidgetCompositeView>().Single();
        void Click(string id) => ((IButtonController)Button(page, id)).SendClicked();
        Click("ClockToolFont"); Click("ClockFont7"); Click("ClockWeight7Light");
        var edited = composition.Design!.Layers[0]; Assert.True(edited.Locked); Assert.Equal(clock.Frame, edited.Frame);
        Assert.Equal(WidgetClockFont.SfProRounded, edited.Clock!.Font); Assert.Equal(WidgetClockWeight.Light, edited.Clock.Weight);
        Assert.Equal(WidgetClockTool.Font, controls.ActiveTool); Assert.Equal(0, store.Saves); Assert.Null(clock.Clock!.Weight);
        Click("ClockToolPosition"); Click("ClockWeight7Bold"); Assert.Equal(edited, composition.Design.Layers[0]);
        Click("ClockToolFont"); Click("ClockFont2");
        Assert.Equal(WidgetClockWeight.Bold, WidgetClockWeights.Resolve(composition.Design.Layers[0].Clock!));
        Click("ClockWeight7Heavy"); Assert.Null(composition.Design.Layers[0].Clock!.Weight); // stale control for another font
        Click("ClockWeight2Regular"); Assert.Equal(WidgetClockWeight.Regular, composition.Design.Layers[0].Clock!.Weight);
        Click("ClockFont7"); Assert.Equal(WidgetClockWeight.Regular, composition.Design.Layers[0].Clock!.Weight);
        ((IWindow)host.App.Windows[0]).Deactivated(); Click("ClockWeight7Heavy");
        ((IWindow)host.App.Windows[0]).Activated(); Assert.Equal(WidgetClockWeight.Regular, composition.Design!.Layers[0].Clock!.Weight);
        Click("SaveWidgetDesign"); Assert.Equal(WidgetClockWeight.Regular, store.Saved!.Layers[0].Clock!.Weight);
        Assert.Equal(WidgetClockFont.SfProRounded, store.Saved.Layers[0].Clock!.Font);
    }
    [Theory]
    [InlineData(WidgetClockFont.RocknRollOne)] [InlineData(WidgetClockFont.DelaGothicOne)] [InlineData(WidgetClockFont.KaiseiDecol)]
    public void FontSpecimensApplyToTheLiveClockKeepTheirPanelOpenAndSaveOnlyOnRequest(WidgetClockFont font)
    {
        var clock = WidgetClocks.New(); var original = WidgetDesignRules.New() with { Layers = [clock] }; var store = new Store();
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original); Open(page);
        var controls = Descendants(page).OfType<WidgetClockControls>().Single();
        var picker = Descendants(page).OfType<WidgetClockFontPicker>().Single();
        var composition = Descendants(page).OfType<WidgetCompositeView>().Single();
        var button = Button(page, "ClockFont" + (int)font);
        // A stale/native callback while the Font panel is closed must not edit the draft.
        ((IButtonController)button).SendClicked(); Assert.Equal(clock, composition.Design!.Layers[0]);
        ((IButtonController)Button(page, "ClockToolFont")).SendClicked(); ((IButtonController)button).SendClicked();
        Assert.Equal(font, composition.Design.Layers[0].Clock!.Font); Assert.Equal(font, picker.SelectedFont);
        Assert.Equal(WidgetClockTool.Font, controls.ActiveTool); Assert.Contains("selected", SemanticProperties.GetDescription(button));
        Assert.Equal(clock, original.Layers[0]); Assert.Equal(0, store.Saves);
        ((IWindow)host.App.Windows[0]).Deactivated(); ((IButtonController)Button(page, "ClockFont0")).SendClicked();
        Assert.Null(composition.Design); Assert.Equal(font, picker.SelectedFont); Assert.Equal(0, store.Saves);
        ((IWindow)host.App.Windows[0]).Activated();
        Assert.Equal(font, composition.Design!.Layers[0].Clock!.Font);
        ((IButtonController)Button(page, "SaveWidgetDesign")).SendClicked(); Assert.Equal(font, store.Saved!.Layers[0].Clock!.Font);
    }
    [Fact]
    public async Task ClockToolbarHasSevenTargetsAndOnlyOnePanelWithAFullHeightScrollingPreview()
    {
        var photo = Photo(); var page = new WidgetEditorPage(new Store(), new ColourPicker(), NullLogger.Instance,
            WidgetDesignRules.New() with { Layers = [photo] }, chooseLayerType: () => Task.FromResult<string?>("Clock")); Open(page); await page.AddLayerAsync();
        var controls = Descendants(page).OfType<WidgetClockControls>().Single(); var canvas = Descendants(page).OfType<WidgetCanvasView>().Single();
        var originalParent = canvas.Parent; var preview = Descendants(page).OfType<Border>().Single(x => x.AutomationId == "WidgetEditorLivePreview");
        var settings = Descendants(page).OfType<ScrollView>().Single(x => x.AutomationId == "WidgetEditorSettingsScroll");
        var form = Assert.IsType<WidgetEditorForm>(settings.Content);
        var adjustments = Descendants(page).OfType<VerticalStackLayout>().Single(x => x.AutomationId == "WidgetAdjustments");
        Assert.Same(form, preview.Parent); Assert.Same(form, adjustments.Parent);
        Assert.Equal(form.Children.IndexOf(preview) + 1, form.Children.IndexOf(adjustments));
        Assert.Contains(canvas, Descendants(preview)); Assert.Contains(canvas, Descendants(settings));
        var body = Assert.IsType<Grid>(preview.Content);
        Assert.Equal(GridLength.Auto, body.RowDefinitions[1].Height); Assert.Equal(new GridLength(64), body.RowDefinitions[2].Height);
        var originalSize = new Size(canvas.WidthRequest, canvas.HeightRequest);
        var targets = Descendants(controls.Toolbar).OfType<Button>().ToArray(); Assert.Equal(7, targets.Length);
        Assert.All(Descendants(controls.Toolbar).OfType<WidgetToolButton>(), x => { Assert.Equal(48, x.WidthRequest); Assert.Equal(48, x.HeightRequest); });
        Assert.Null(controls.ActiveTool); Assert.False(controls.IsVisible);
        foreach (var tool in Enum.GetValues<WidgetClockTool>())
        {
            var button = Button(page, "ClockTool" + tool); ((IButtonController)button).SendClicked();
            Assert.Equal(tool, controls.ActiveTool); Assert.True(controls.IsVisible); Assert.Contains("expanded", SemanticProperties.GetDescription(button));
            Assert.Single(Descendants(controls).OfType<Border>(), x => x.AutomationId?.StartsWith("ClockPanel") == true && x.IsVisible);
            Assert.Same(originalParent, canvas.Parent); Assert.Single(Descendants(page).OfType<WidgetCompositeView>());
            Assert.Equal(originalSize, new Size(canvas.WidthRequest, canvas.HeightRequest));
        }
        ((IButtonController)Button(page, "ClockToolOpacity")).SendClicked(); Assert.Null(controls.ActiveTool); Assert.False(controls.IsVisible);
        ((IButtonController)Button(page, "ClockToolFont")).SendClicked();
        ((IButtonController)Button(page, "ClockFont2")).SendClicked(); Assert.Equal(WidgetClockTool.Font, controls.ActiveTool);
        var clock = Descendants(page).OfType<WidgetCompositeView>().Single().Design!.Layers.Last();
        ((IButtonController)Button(page, "SelectWidgetLayer" + photo.Id.ToString("N"))).SendClicked(); Assert.Null(controls.ActiveTool); Assert.False(controls.Toolbar.IsVisible);
        ((IButtonController)Button(page, "SelectWidgetLayer" + clock.Id.ToString("N"))).SendClicked(); Assert.Null(controls.ActiveTool); Assert.True(controls.Toolbar.IsVisible);
        Assert.Equal(WidgetClockFont.Monospaced, Descendants(page).OfType<WidgetCompositeView>().Single().Design!.Layers.Last().Clock!.Font);
    }
    [Fact]
    public async Task ClockPanelValidationBlocksClosingAndNativePickerDisablesTheCanvasUntilRecovery()
    {
        var pending = new TaskCompletionSource<string>(); var colours = new ColourPicker { Pick = (_, _) => pending.Task };
        var page = new WidgetEditorPage(new Store(), colours, NullLogger.Instance, chooseLayerType: () => Task.FromResult<string?>("Clock")); Open(page); await page.AddLayerAsync();
        var controls = Descendants(page).OfType<WidgetClockControls>().Single();
        var canvas = Descendants(page).OfType<WidgetCanvasView>().Single();
        ((IButtonController)Button(page, "ClockToolColour")).SendClicked();
        var hex = Descendants(controls).OfType<HexColorEntry>().Single(); hex.Text = "#bad";
        ((IButtonController)Button(page, "ClockToolColour")).SendClicked(); Assert.Equal(WidgetClockTool.Colour, controls.ActiveTool);
        ((IButtonController)Button(page, "ClockToolFont")).SendClicked(); Assert.Equal(WidgetClockTool.Colour, controls.ActiveTool);
        hex.Text = "#12ABEF"; ((IButtonController)Button(page, "ClockToolFont")).SendClicked(); Assert.Equal(WidgetClockTool.Font, controls.ActiveTool);
        ((IButtonController)Button(page, "ClockToolColour")).SendClicked();
        var picker = Descendants(controls).OfType<ColourPickerView>().Single(); var picking = picker.PickAsync();
        Assert.False(canvas.IsEnabled); Assert.False(controls.Toolbar.IsEnabled);
        ((IPageController)page).SendDisappearing(); Assert.Equal(WidgetClockTool.Colour, controls.ActiveTool);
        pending.SetException(new IOException("picker dismissed")); await picking;
        Assert.True(canvas.IsEnabled); Assert.True(controls.Toolbar.IsEnabled); Assert.Equal(WidgetClockTool.Colour, controls.ActiveTool);
        ((IWindow)host.App.Windows[0]).Deactivated(); Assert.Equal(WidgetClockTool.Colour, controls.ActiveTool);
        ((IWindow)host.App.Windows[0]).Activated();
        ((IButtonController)Button(page, "ClockToolTime")).SendClicked(); Assert.Equal(WidgetClockTool.Time, controls.ActiveTool);
    }
    [Fact]
    public async Task ClockPickerResultDuringDeactivationIsKeptWithoutRestartingThePreview()
    {
        var pending = new TaskCompletionSource<string>(); var colours = new ColourPicker { Pick = (_, _) => pending.Task };
        var page = new WidgetEditorPage(new Store(), colours, NullLogger.Instance, chooseLayerType: () => Task.FromResult<string?>("Clock")); Open(page); await page.AddLayerAsync();
        ((IButtonController)Button(page, "ClockToolColour")).SendClicked();
        var picker = Descendants(page).OfType<ColourPickerView>().Single(x => x.AutomationId == "ClockTextColourPicker");
        var composition = Descendants(page).OfType<WidgetCompositeView>().Single(); var picking = picker.PickAsync();
        ((IWindow)host.App.Windows[0]).Deactivated(); Assert.Null(composition.Design);
        pending.SetResult("#34ABEF"); await picking; Assert.Null(composition.Design);
        ((IWindow)host.App.Windows[0]).Activated(); Assert.Equal("#34ABEF", composition.Design!.Layers.Single().Clock!.ColourHex);
        Assert.Equal(WidgetClockTool.Colour, Descendants(page).OfType<WidgetClockControls>().Single().ActiveTool);
        Assert.True(Button(page, "SaveWidgetDesign").IsEnabled);
    }
    [Fact]
    public async Task PendingClockHexIsValidatedBeforeSavingOrChangingSelection()
    {
        var photo = Photo(); var store = new Store();
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, WidgetDesignRules.New() with { Layers = [photo] }, chooseLayerType: () => Task.FromResult<string?>("Clock")); Open(page); await page.AddLayerAsync();
        ((IButtonController)Button(page, "ClockToolColour")).SendClicked();
        var controls = Descendants(page).OfType<WidgetClockControls>().Single(); var hex = Descendants(controls).OfType<HexColorEntry>().Single();
        hex.Text = "#bad"; ((IButtonController)Button(page, "SaveWidgetDesign")).SendClicked(); Assert.Equal(0, store.Saves);
        Assert.True(Button(page, "SaveWidgetDesign").IsEnabled);
        ((IButtonController)Button(page, "SelectWidgetLayer" + photo.Id.ToString("N"))).SendClicked(); Assert.True(controls.IsVisible);
        hex.Text = "#12ABEF"; ((IButtonController)Button(page, "SaveWidgetDesign")).SendClicked();
        Assert.Equal("#12ABEF", store.Saved!.Layers.Last().Clock!.ColourHex); Assert.Null(store.Saved.Layers[0].Clock);
    }
    [Fact]
    public async Task PhotoChoiceCancellationRestoresTheEditorAndClockChoiceCanFollowIt()
    {
        var choice = "Photo"; var sources = 0; var store = new Store(); var photo = Photo(); WidgetEditorPage? page = null;
        page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, WidgetDesignRules.New() with { Layers = [photo] },
            chooseLayerType: () => Task.FromResult<string?>(choice), choosePhotoSource: () => { sources++; ((IPageController)page!).SendDisappearing(); return Task.FromResult<string?>("Cancel"); }); Open(page);
        await page.AddLayerAsync(); Assert.Equal(1, sources); Assert.Equal(0, store.Picks); Assert.True(Button(page, "SaveWidgetDesign").IsEnabled);
        choice = "Clock"; await page.AddLayerAsync(); Assert.Equal(1, sources);
        Assert.Equal(new[] { WidgetLayerKind.Photo, WidgetLayerKind.Clock }, Descendants(page).OfType<WidgetCompositeView>().Single().Design!.Layers.Select(x => x.Kind));
    }
    [Fact]
    public async Task SharedLayerLimitPreventsOpeningTheChooserAtEightLayers()
    {
        var choices = 0; var store = new Store(); var original = WidgetDesignRules.New() with { Layers = Enumerable.Range(0, 8).Select(_ => WidgetClocks.New()).ToArray() };
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original, chooseLayerType: () => { choices++; return Task.FromResult<string?>("Clock"); }); Open(page);
        Assert.False(Button(page, "AddWidgetPhoto").IsEnabled); await page.AddLayerAsync(); Assert.Equal(0, choices);
        Assert.Equal(8, Descendants(page).OfType<WidgetCompositeView>().Single().Design!.Layers.Count); Assert.Equal(0, store.Picks);
    }
    [Fact]
    public void ClockHorizontalCenterPreservesHeightOptionsAndDraftAndRespectsLockedAndInvalidColourStates()
    {
        var clock = WidgetClocks.New() with { Frame = new(1.2, .1, .7, .3), Clock = new(TwentyFourHour: false, ShowAmPm: false), Opacity = .6 };
        var photo = Photo(); var original = WidgetDesignRules.New() with { Layers = [photo, clock] }; var store = new Store();
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original); Open(page);
        var controls = Descendants(page).OfType<WidgetClockControls>().Single(); var composition = Descendants(page).OfType<WidgetCompositeView>().Single();
        var button = Button(page, "CentreClockHorizontally");
        Assert.Contains("keeping its vertical position and size", SemanticProperties.GetDescription(button));
        ((IButtonController)Button(page, "ClockToolColour")).SendClicked();
        var hex = Descendants(controls).OfType<HexColorEntry>().Single(); hex.Text = "#bad";
        ((IButtonController)Button(page, "ClockToolPosition")).SendClicked(); Assert.Equal(WidgetClockTool.Colour, controls.ActiveTool);
        Assert.Equal(clock, composition.Design!.Layers.Last()); Assert.Equal(0, store.Saves);
        hex.Text = "#12ABEF"; ((IButtonController)Button(page, "ClockToolPosition")).SendClicked(); ((IButtonController)button).SendClicked();
        var centered = composition.Design.Layers.Last(); Assert.Equal(.15, centered.Frame.X, 8);
        Assert.Equal(clock.Frame.Y, centered.Frame.Y); Assert.Equal(clock.Frame.Width, centered.Frame.Width); Assert.Equal(clock.Frame.Height, centered.Frame.Height);
        Assert.Equal(clock.Clock! with { ColourHex = "#12ABEF" }, centered.Clock); Assert.Equal(clock.Opacity, centered.Opacity);
        Assert.Equal(photo, composition.Design.Layers[0]); Assert.Equal(clock, original.Layers.Last()); Assert.Equal(0, store.Saves);
        var lockControl = Descendants(page).OfType<ClipboardIconButton>().Single(x => x.AutomationId == "WidgetLayerLock" + clock.Id.ToString("N"));
        var toggle = Descendants(lockControl).OfType<Button>().Single(); ((IButtonController)toggle).SendClicked(); Assert.False(button.IsEnabled);
        ((IButtonController)button).SendClicked(); Assert.Equal(centered.Frame, composition.Design.Layers.Last().Frame);
        ((IButtonController)toggle).SendClicked(); Assert.True(button.IsEnabled);
        var canvas = Descendants(page).OfType<WidgetCanvasView>().Single(); ((IView)canvas.PhotoSurface).Arrange(new Rect(0, 0, 170, 170));
        Assert.True(canvas.BeginInteraction(85, 42.5, true));
        for (Element? ancestor = canvas; ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor is VisualElement view) Assert.True(view.IsEnabled);
        Assert.False(Button(page, "SaveWidgetDesign").IsEnabled);
        canvas.Pinch(1.2, 30, 30); var pinching = composition.Design.Layers.Last();
        ((IButtonController)button).SendClicked(); Assert.Equal(pinching, composition.Design.Layers.Last()); canvas.EndInteraction(true);
        ((IButtonController)Button(page, "SaveWidgetDesign")).SendClicked(); Assert.Equal(centered, store.Saved!.Layers.Last());
    }
    [Fact]
    public async Task ClockAmPmSwitchOnlyAppearsForTwelveHourAndKeepsItsSavedChoice()
    {
        var store = new Store(); var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, chooseLayerType: () => Task.FromResult<string?>("Clock"));
        Open(page); await page.AddLayerAsync();
        var controls = Descendants(page).OfType<WidgetClockControls>().Single();
        var toggle = Descendants(controls).OfType<Switch>().Single(x => x.AutomationId == "ClockShowAmPm");
        var row = Assert.IsType<Grid>(toggle.Parent); var composition = Descendants(page).OfType<WidgetCompositeView>().Single();
        void Click(string id) => ((IButtonController)Button(page, id)).SendClicked();
        Click("ClockToolTime");
        Assert.False(row.IsVisible); Assert.True(toggle.IsToggled); Assert.Equal("Show AM/PM", SemanticProperties.GetDescription(toggle));
        Click("ClockFormat0"); Assert.True(row.IsVisible); Assert.True(toggle.IsToggled);
        toggle.IsToggled = false; Assert.False(composition.Design!.Layers.Single().Clock!.ShowAmPm);
        Click("ClockFormat1"); Assert.False(row.IsVisible); Assert.False(composition.Design.Layers.Single().Clock!.ShowAmPm);
        Click("ClockFormat0"); Assert.True(row.IsVisible); Assert.False(toggle.IsToggled);
        Click("SaveWidgetDesign"); Assert.False(store.Saved!.Layers.Single().Clock!.ShowAmPm); Assert.False(store.Saved.Layers.Single().Clock!.TwentyFourHour);
    }
    [Fact]
    public async Task ClockChooserAddsNoPhotoAndClockSettingsPersistAlongsideThePhoto()
    {
        var photo = Photo(); var store = new Store(); var original = WidgetDesignRules.New() with { Layers = [photo] };
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original, chooseLayerType: () => Task.FromResult<string?>("Clock")); Open(page);
        await page.AddLayerAsync(); Assert.Equal(0, store.Picks); Assert.Equal(0, store.Saves);
        var composition = Descendants(page).OfType<WidgetCompositeView>().Single(); var clock = composition.Design!.Layers.Last();
        Assert.Equal(WidgetLayerKind.Clock, clock.Kind); Assert.Equal("Clock 1", clock.Name);
        var controls = Descendants(page).OfType<WidgetClockControls>().Single(); Assert.True(controls.Toolbar.IsVisible); Assert.False(controls.IsVisible);
        void Click(string id) => ((IButtonController)Button(page, id)).SendClicked();
        Click("ClockToolTime"); Click("ClockFormat0");
        Descendants(controls).OfType<Switch>().Single(x => x.AutomationId == "ClockShowSeconds").IsToggled = false;
        Click("ClockToolFont"); Click("ClockFont2"); Click("ClockToolPosition"); Click("ClockAlignment2");
        Click("ClockToolSize");
        Descendants(controls).OfType<Slider>().Single(x => x.AutomationId == "ClockTextSize").Value = .5;
        Click("ClockToolOpacity");
        Descendants(controls).OfType<Slider>().Single(x => x.AutomationId == "ClockOpacity").Value = .7;
        Click("ClockToolColour"); Click("ClockTextPurple");
        clock = composition.Design.Layers.Last();
        Assert.False(clock.Clock!.TwentyFourHour); Assert.False(clock.Clock.ShowSeconds); Assert.Equal(WidgetClockFont.Monospaced, clock.Clock.Font);
        Assert.Equal(WidgetClockAlignment.Right, clock.Clock.Alignment); Assert.Equal(.5, clock.Clock.FontScale); Assert.Equal(.7, clock.Opacity);
        Assert.Equal(ControlStudio.Core.AppearanceValues.Presets[1], clock.Clock.ColourHex);
        Assert.False(Button(page, "ClipboardRotateselectedphoto").IsEnabled); Click("WidgetRotateRight"); Assert.Equal(0, composition.Design.Layers.Last().QuarterTurns);
        Click("SelectWidgetLayer" + photo.Id.ToString("N")); Assert.False(controls.IsVisible); Assert.True(Button(page, "ClipboardRotateselectedphoto").IsEnabled);
        Click("SelectWidgetLayer" + clock.Id.ToString("N")); Assert.True(controls.Toolbar.IsVisible); Assert.False(controls.IsVisible); Click("SaveWidgetDesign");
        Assert.Equal(clock, store.Saved!.Layers.Last()); Assert.Equal(photo, store.Saved.Layers[0]); Assert.Single(original.Layers);
    }
    [Theory]
    [InlineData("Cancel")] [InlineData(null)]
    public async Task CancellingLayerChooserDoesNotAlterPhotosOrDisableTheEditor(string? choice)
    {
        var store = new Store(); var photo = Photo(); var original = WidgetDesignRules.New() with { Layers = [photo] };
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original, chooseLayerType: () => Task.FromResult(choice)); Open(page);
        await page.AddLayerAsync(); Assert.Equal(0, store.Picks); Assert.Equal(0, store.Saves);
        Assert.Equal(original.Layers, Descendants(page).OfType<WidgetCompositeView>().Single().Design!.Layers);
        Assert.True(Button(page, "AddWidgetPhoto").IsEnabled); Assert.True(Button(page, "SaveWidgetDesign").IsEnabled);
    }
    [Fact]
    public async Task ClockCanMoveLockDeselectAndReorderWithPhotosWithoutLosingItsSettings()
    {
        var store = new Store(); var photo = Photo(); var original = WidgetDesignRules.New() with { Layers = [photo] };
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original, chooseLayerType: () => Task.FromResult<string?>("Clock")); Open(page); await page.AddLayerAsync();
        var canvas = Descendants(page).OfType<WidgetCanvasView>().Single(); var composition = Descendants(page).OfType<WidgetCompositeView>().Single();
        var clock = composition.Design!.Layers.Last(); ((IView)canvas.PhotoSurface).Arrange(new Rect(0, 0, 170, 170));
        Assert.True(canvas.BeginInteraction(85, 85, false)); canvas.Pan(17, -17); canvas.EndInteraction(false);
        var moved = composition.Design.Layers.Last(); Assert.Equal(clock.Frame.X + .1, moved.Frame.X, 8);
        var control = Descendants(page).OfType<ClipboardIconButton>().Single(x => x.AutomationId == "WidgetLayerLock" + clock.Id.ToString("N"));
        var toggle = Descendants(control).OfType<Button>().Single(); ((IButtonController)toggle).SendClicked();
        Assert.Contains("Unlock clock layer", SemanticProperties.GetDescription(toggle)); Assert.False(canvas.BeginInteraction(102, 68, false));
        var surface = Assert.IsType<WidgetEditorSurface>(page.Content); Assert.True(surface.DeselectFromBlankSpace());
        Assert.False(Descendants(page).OfType<WidgetClockControls>().Single().IsVisible);
        ((IButtonController)Button(page, "SelectWidgetLayer" + clock.Id.ToString("N"))).SendClicked();
        var list = Descendants(page).OfType<WidgetLayerList>().Single(); var positions = list.Rows.Select((row, index) => new WidgetLayerPosition(row.LayerId, index * 72, 64)).ToArray();
        var drag = list.BeginReorder(positions, clock.Id)!; drag.Move(500); list.CompleteReorder(drag);
        Assert.Equal(clock.Id, composition.Design.Layers[0].Id); Assert.True(composition.Design.Layers[0].Locked); Assert.Equal(clock.Clock, composition.Design.Layers[0].Clock);
        ((IButtonController)Button(page, "SaveWidgetDesign")).SendClicked(); Assert.Equal(clock.Id, store.Saved!.Layers[0].Id);
    }
    [Fact]
    public async Task ClockColourPickerCancellationAndFailureRestoreActionsAndKeepItsClock()
    {
        var pending = new TaskCompletionSource<string>(); var colours = new ColourPicker { Pick = (_, _) => pending.Task };
        var store = new Store(); var page = new WidgetEditorPage(store, colours, NullLogger.Instance, chooseLayerType: () => Task.FromResult<string?>("Clock")); Open(page); await page.AddLayerAsync();
        ((IButtonController)Button(page, "ClockToolColour")).SendClicked();
        var composition = Descendants(page).OfType<WidgetCompositeView>().Single(); var clock = composition.Design!.Layers.Single();
        var picker = Descendants(page).OfType<ColourPickerView>().Single(x => x.AutomationId == "ClockTextColourPicker");
        var picking = picker.PickAsync(); Assert.False(Button(page, "SaveWidgetDesign").IsEnabled); ((IPageController)page).SendDisappearing();
        pending.SetException(new IOException("picker dismissed")); await picking;
        Assert.True(Button(page, "SaveWidgetDesign").IsEnabled); Assert.True(Button(page, "AddWidgetPhoto").IsEnabled); Assert.Equal(clock, composition.Design!.Layers.Single());
    }
    [Fact]
    public void PhotoLockBlocksPlacementKeepsAppearanceEditableAndPersistsOnlyWhenSaved()
    {
        var photo = Photo(); var original = WidgetDesignRules.New() with { Layers = [photo] }; var store = new Store();
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original); Open(page);
        var canvas = Descendants(page).OfType<WidgetCanvasView>().Single(); var composition = Descendants(page).OfType<WidgetCompositeView>().Single();
        var row = Descendants(page).OfType<WidgetLayerRow>().Single();
        var control = Descendants(row).OfType<ClipboardIconButton>().Single(x => x.AutomationId == "WidgetLayerLock" + photo.Id.ToString("N"));
        var toggle = Descendants(control).OfType<Button>().Single();
        void Click(string id) => ((IButtonController)Button(page, id)).SendClicked();
        ((IButtonController)toggle).SendClicked(); Assert.True(composition.Design!.Layers[0].Locked); Assert.False(photo.Locked); Assert.Equal(0, store.Saves);
        Assert.Equal(StudioSymbol.Lock, control.Symbol); Assert.Contains("Unlock photo layer", SemanticProperties.GetDescription(toggle));
        Assert.False(Button(page, "ClipboardResetselectedphoto").IsEnabled); Assert.False(Button(page, "ClipboardRotateselectedphoto").IsEnabled);
        Assert.False(Button(page, "ClipboardFlipselectedphoto").IsEnabled); Assert.True(Button(page, "ClipboardBlendselectedphoto").IsEnabled);
        ((IView)canvas.PhotoSurface).Arrange(new Rect(0, 0, 170, 170)); Assert.False(canvas.BeginInteraction(85, 85, false));
        Click("WidgetRotateRight"); Assert.Equal(photo.Frame, composition.Design.Layers[0].Frame); Assert.Equal(0, composition.Design.Layers[0].QuarterTurns);
        Click("ClipboardAdjustphotoopacity"); Descendants(page).OfType<Slider>().Single(x => SemanticProperties.GetDescription(x) == "Selected photo opacity").Value = .35;
        Click("ClipboardBlendselectedphoto"); Click("WidgetBlendGroup1"); Click("WidgetBlendModeMultiply");
        Assert.True(composition.Design.Layers[0].Locked); Assert.Equal(.35, composition.Design.Layers[0].Opacity); Assert.Equal(WidgetBlendMode.Multiply, composition.Design.Layers[0].BlendMode);
        ((IButtonController)toggle).SendClicked(); Assert.False(composition.Design.Layers[0].Locked); Assert.Equal(StudioSymbol.Unlock, control.Symbol);
        Assert.True(Button(page, "ClipboardResetselectedphoto").IsEnabled); Assert.Same(row, Descendants(page).OfType<WidgetLayerRow>().Single());
        Assert.True(canvas.BeginInteraction(85, 85, false));
        for (Element? ancestor = canvas; ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor is VisualElement view) Assert.True(view.IsEnabled);
        Assert.False(Button(page, "SaveWidgetDesign").IsEnabled);
        canvas.Pan(17, 0); canvas.EndInteraction(false);
        ((IButtonController)toggle).SendClicked(); Click("SaveWidgetDesign");
        var savedPhoto = Assert.Single(store.Saved!.Layers); Assert.True(savedPhoto.Locked); Assert.Equal(.1, savedPhoto.Frame.X, 8);
        Assert.Equal(.35, savedPhoto.Opacity); Assert.Equal(WidgetBlendMode.Multiply, savedPhoto.BlendMode); Assert.False(original.Layers[0].Locked);
    }
    [Fact]
    public void BlankPageAndCanvasTapsDeselectWithoutSavingOrInterruptingActiveInteractions()
    {
        var first = Photo() with { Frame = new(.2, .2, .6, .6) }; var second = Photo() with { Name = "Second", Frame = new(.2, .2, .6, .6) };
        var original = WidgetDesignRules.New() with { Layers = [first, second] }; var store = new Store();
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original); Open(page);
        var surface = Assert.IsType<WidgetEditorSurface>(page.Content); var canvas = Descendants(page).OfType<WidgetCanvasView>().Single();
        var list = Descendants(page).OfType<WidgetLayerList>().Single(); var composition = Descendants(page).OfType<WidgetCompositeView>().Single();
        Assert.Contains(canvas, surface.ProtectedAreas); Assert.Contains(list, surface.ProtectedAreas);
        ((IButtonController)Button(page, "ClipboardRotateselectedphoto")).SendClicked();
        var tool = Descendants(page).OfType<WidgetToolButton>().Single(x => x.IsSelected); Assert.True(surface.DeselectFromBlankSpace());
        Assert.False(tool.IsSelected); Assert.False(Button(page, "ClipboardRotateselectedphoto").IsEnabled);
        Assert.Contains("No photo selected", SemanticProperties.GetDescription(canvas)); Assert.Equal(original.Layers, composition.Design!.Layers);
        Assert.False(surface.DeselectFromBlankSpace()); Assert.Equal(0, store.Saves);
        void Select() => ((IButtonController)Button(page, "SelectWidgetLayer" + second.Id.ToString("N"))).SendClicked();
        Select(); ((IView)canvas.PhotoSurface).Arrange(new Rect(32, 32, 170, 170));
        Assert.True(canvas.BeginInteraction(117, 117, false)); Assert.False(surface.DeselectFromBlankSpace()); canvas.SelectAt(5, 5);
        Assert.True(canvas.IsInteracting); canvas.EndInteraction(true); Assert.True(surface.CanDeselect);
        var positions = list.Rows.Select((row, index) => new WidgetLayerPosition(row.LayerId, index * 72, 64)).ToArray();
        Assert.NotNull(list.BeginReorder(positions, second.Id)); Assert.False(surface.DeselectFromBlankSpace()); list.CancelReorder();
        ((IWindow)host.App.Windows[0]).Deactivated(); Assert.False(surface.DeselectFromBlankSpace()); ((IWindow)host.App.Windows[0]).Activated();
        canvas.SelectAt(5, 5); Assert.Contains("No photo selected", SemanticProperties.GetDescription(canvas));
        Assert.False(Assert.IsType<AbsoluteLayout>(canvas.Workspace.Children[0]).IsVisible); Select(); Assert.True(surface.CanDeselect);
        Assert.Equal(original.Layers, composition.Design!.Layers); Assert.Equal(0, store.Saves);
    }
    [Fact]
    public void LayerDragCommitsOnlyOnDropKeepsStableRowsAndSavesTheVisualStackOrder()
    {
        var first = Photo(); var second = Photo() with { Name = "Second" }; var third = Photo() with { Name = "Third" };
        var original = WidgetDesignRules.New() with { Layers = [first, second, third] }; var store = new Store();
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original); Open(page);
        var list = Descendants(page).OfType<WidgetLayerList>().Single(); var rows = list.Rows.ToArray();
        var positions = rows.Select((row, index) => new WidgetLayerPosition(row.LayerId, index * 72, 64)).ToArray();
        var composition = Descendants(page).OfType<WidgetCompositeView>().Single();
        var drag = list.BeginReorder(positions, third.Id)!; Assert.NotNull(drag);
        Assert.True(list.IsEnabled); Assert.False(composition.IsEnabled);
        Assert.False(Button(page, "SaveWidgetDesign").IsEnabled); Assert.False(Button(page, "AddWidgetPhoto").IsEnabled); Assert.False(Button(page, "BackFromWidgetEditor").IsEnabled);
        var rowButtons = Descendants(list).OfType<Button>().ToArray(); Assert.All(rowButtons, button => Assert.False(button.IsEnabled));
        drag.Move(300); Assert.Equal(original.Layers, composition.Design!.Layers);
        ((IButtonController)Button(page, "SaveWidgetDesign")).SendClicked(); Assert.Equal(0, store.Saves);
        list.CompleteReorder(drag);
        Assert.Equal(new[] { third.Id, first.Id, second.Id }, composition.Design!.Layers.Select(x => x.Id));
        Assert.Equal(new[] { second.Id, first.Id, third.Id }, list.Rows.Select(x => x.LayerId));
        Assert.Equal(new[] { first, second, third }, original.Layers);
        Assert.Equal(rows.OrderBy(x => x.LayerId), list.Rows.OrderBy(x => x.LayerId));
        Assert.All(rowButtons, button => Assert.True(button.IsEnabled));
        Assert.True(Button(page, "SaveWidgetDesign").IsEnabled); Assert.True(composition.IsEnabled);
        ((IButtonController)Button(page, "SaveWidgetDesign")).SendClicked();
        Assert.Equal(new[] { third.Id, first.Id, second.Id }, store.Saved!.Layers.Select(x => x.Id));
    }
    [Fact]
    public void LayerDragCancelsOnBackgroundOrPageDisappearanceAndCannotCommitAfterwards()
    {
        var first = Photo(); var second = Photo() with { Name = "Second" }; var original = WidgetDesignRules.New() with { Layers = [first, second] };
        var page = new WidgetEditorPage(new Store(), new ColourPicker(), NullLogger.Instance, original); Open(page);
        var list = Descendants(page).OfType<WidgetLayerList>().Single();
        WidgetLayerDragPlan Begin()
        {
            var positions = list.Rows.Select((row, index) => new WidgetLayerPosition(row.LayerId, index * 72, 64)).ToArray();
            return list.BeginReorder(positions, second.Id)!;
        }
        var drag = Begin(); drag.Move(300); ((IWindow)host.App.Windows[0]).Deactivated();
        Assert.False(list.IsReordering); list.CompleteReorder(drag); ((IWindow)host.App.Windows[0]).Activated();
        Assert.Equal(original.Layers, Descendants(page).OfType<WidgetCompositeView>().Single().Design!.Layers);
        Assert.True(Button(page, "SaveWidgetDesign").IsEnabled);
        drag = Begin(); drag.Move(300); ((IPageController)page).SendDisappearing(); Assert.False(list.IsReordering);
        list.CompleteReorder(drag); ((IPageController)page).SendAppearing();
        Assert.True(Button(page, "SaveWidgetDesign").IsEnabled); Assert.Equal(new[] { second.Id, first.Id }, list.Rows.Select(x => x.LayerId));
    }
    [Fact]
    public void SingleRowToolsKeepPanelsAndHighlightAcrossBackgroundAndClearOnDone()
    {
        var page = Editor(new Store()); Open(page);
        WidgetToolButton Tool(string id) => Assert.IsType<WidgetToolButton>(Button(page, id).Parent!.Parent);
        void Click(string id) => ((IButtonController)Button(page, id)).SendClicked();
        var rotate = Tool("ClipboardRotateselectedphoto"); var flip = Tool("ClipboardFlipselectedphoto"); var blend = Tool("ClipboardBlendselectedphoto");
        var opacity = Tool("ClipboardAdjustphotoopacity");
        Click("ClipboardRotateselectedphoto"); Assert.True(rotate.IsSelected);
        Click("WidgetRotateRight"); Click("WidgetRotateRight"); Assert.True(rotate.IsSelected);
        var composition = Descendants(page).OfType<WidgetCompositeView>().Single(); Assert.Equal(2, composition.Design!.Layers[0].QuarterTurns);
        Click("ClipboardFlipselectedphoto"); Assert.False(rotate.IsSelected); Assert.True(flip.IsSelected);
        Click("WidgetFliphorizontally"); Assert.True(composition.Design.Layers[0].FlipHorizontal); Assert.True(flip.IsSelected);
        Click("ClipboardBlendselectedphoto"); Assert.False(flip.IsSelected); Assert.True(blend.IsSelected);
        Click("WidgetBlendGroup1"); Click("WidgetBlendModeMultiply"); Assert.True(blend.IsSelected);
        Click("CloseWidgetBlendPanel"); Assert.False(blend.IsSelected);
        Click("ClipboardAdjustphotoopacity"); Assert.True(opacity.IsSelected);
        var slider = Descendants(page).OfType<Slider>().Single(x => SemanticProperties.GetDescription(x) == "Selected photo opacity");
        slider.Value = .35; Assert.Equal(.35, composition.Design!.Layers[0].Opacity); Assert.True(opacity.IsSelected);
        Assert.Contains("35", SemanticProperties.GetDescription(Button(page, "ClipboardAdjustphotoopacity")));
        opacity.Scale = .95;
        ((IWindow)host.App.Windows[0]).Deactivated(); Assert.True(opacity.IsSelected); Assert.Equal(1, opacity.Scale); ((IWindow)host.App.Windows[0]).Activated();
        Click("ClipboardResetselectedphoto"); Assert.Equal(1, composition.Design!.Layers[0].Opacity); Assert.Equal(WidgetBlendMode.Normal, composition.Design.Layers[0].BlendMode);
        Assert.All(Descendants(page).OfType<WidgetToolButton>(), tool => Assert.False(tool.IsSelected));
    }

    [Theory]
    [InlineData(WidgetSize.Small, 158, 158)] [InlineData(WidgetSize.Medium, 338, 158)]
    [InlineData(WidgetSize.Large, 338, 354)] [InlineData(WidgetSize.ExtraLargePortrait, 338, 550)]
    public void EditingUsesMeasuredDimensionsWithoutChangingTheOriginalUntilSave(WidgetSize family, double width, double height)
    {
        var dimensions = new WidgetCanvasSize(width, height); var photo = Photo() with { FitPhoto = true, BlendMode = WidgetBlendMode.Screen };
        var original = WidgetDesignRules.New() with { Size = family, Layers = [photo] };
        var store = new Store { Sizing = _ => new(dimensions, Measured: true) };
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original); Open(page);
        var composition = Descendants(page).OfType<WidgetCompositeView>().Single();
        Assert.Equal(dimensions, composition.Design!.CanvasDimensions); Assert.Equal(photo, composition.Design.Layers[0]); Assert.Null(original.CanvasDimensions);
        Assert.Contains(Descendants(page).OfType<Label>(), x => x.Text?.Contains("Device widget size") == true);
        ((IButtonController)Button(page, "SaveWidgetDesign")).SendClicked();
        Assert.Equal(dimensions, store.Saved!.CanvasDimensions); Assert.Equal(photo, store.Saved.Layers[0]); Assert.Null(original.CanvasDimensions);
    }
    [Fact]
    public void SizeChangesAndForegroundCalibrationUpdateTheDraftWhileFallbackKeepsSavedDimensions()
    {
        var measured = false;
        var saved = new WidgetCanvasSize(338, 158);
        var store = new Store { Sizing = size => measured ? new(new(340, 160), true) : WidgetCanvasSizes.Estimate(size, 430, 932) };
        var original = WidgetDesignRules.New() with { Size = WidgetSize.Medium, CanvasDimensions = saved, Layers = [Photo()] };
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original); Open(page);
        var composition = Descendants(page).OfType<WidgetCompositeView>().Single(); Assert.Equal(saved, composition.Design!.CanvasDimensions);
        ((IWindow)host.App.Windows[0]).Deactivated(); measured = true; ((IWindow)host.App.Windows[0]).Activated();
        Assert.Equal(new WidgetCanvasSize(340, 160), composition.Design!.CanvasDimensions); Assert.Equal(saved, original.CanvasDimensions);
        measured = false; ((IButtonController)Button(page, "EditorWidgetSize2")).SendClicked();
        Assert.Equal(WidgetSize.Large, composition.Design.Size); Assert.Equal(new WidgetCanvasSize(364, 382), composition.Design.CanvasDimensions);
        ((IButtonController)Button(page, "EditorWidgetSize3")).SendClicked();
        Assert.Equal(WidgetSize.ExtraLargePortrait, composition.Design.Size); Assert.Equal(new WidgetCanvasSize(364, 594), composition.Design.CanvasDimensions);
        Assert.Contains(Descendants(page).OfType<Label>(), x => x.Text?.Contains("Estimated size") == true);
    }

    [Fact]
    public async Task BlendingStaysOpenUpdatesTheCorrectLayerAndSurvivesPickerCancellationAndSave()
    {
        var first = Photo() with { BlendMode = WidgetBlendMode.Multiply };
        var second = Photo() with { Name = "Second photo" };
        var original = WidgetDesignRules.New() with { Layers = [first, second] };
        var store = new Store(); var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original,
            choosePhotoSource: () => Task.FromResult<string?>("Photo Library")); Open(page);
        ((IButtonController)Button(page, "ClipboardBlendselectedphoto")).SendClicked();
        var panel = Descendants(page).OfType<WidgetBlendPanel>().Single(); Assert.True(panel.IsVisible);
        ((IButtonController)Button(page, "WidgetBlendGroup3")).SendClicked();
        ((IButtonController)Button(page, "WidgetBlendModeSoftLight")).SendClicked(); Assert.True(panel.IsVisible);
        Assert.Contains(Descendants(page).OfType<Label>(), x => x.Text == "Image · Soft Light");
        var composition = Descendants(page).OfType<WidgetCompositeView>().Single();
        Assert.Equal(WidgetBlendMode.Multiply, composition.Design!.Layers[0].BlendMode);
        Assert.Equal(WidgetBlendMode.SoftLight, composition.Design.Layers[1].BlendMode);
        ((IWindow)host.App.Windows[0]).Deactivated(); Assert.Null(composition.Design); Assert.True(panel.IsVisible);
        Assert.All(Descendants(panel).OfType<WidgetBlendThumbnail>(), picture => Assert.False(picture.IsPreviewActive));
        ((IWindow)host.App.Windows[0]).Activated(); Assert.Equal(WidgetBlendMode.SoftLight, composition.Design!.Layers[1].BlendMode);
        await page.AddPhotoAsync(); Assert.Equal(2, composition.Design.Layers.Count); Assert.True(Button(page, "SaveWidgetDesign").IsEnabled);
        ((IButtonController)Button(page, "SaveWidgetDesign")).SendClicked();
        Assert.Equal(WidgetBlendMode.Multiply, store.Saved!.Layers[0].BlendMode);
        Assert.Equal(WidgetBlendMode.SoftLight, store.Saved.Layers[1].BlendMode);
        Assert.Equal(WidgetBlendMode.Normal, second.BlendMode); // Draft editing did not mutate the original.
    }
    [Fact]
    public void ResetClearsBlendAndSwitchingLayersClosesTheOldPanel()
    {
        var first = Photo() with { BlendMode = WidgetBlendMode.DarkerColor }; var second = Photo() with { Name = "Second" };
        var page = new WidgetEditorPage(new Store(), new ColourPicker(), NullLogger.Instance, WidgetDesignRules.New() with { Layers = [first, second] }); Open(page);
        ((IButtonController)Button(page, "ClipboardBlendselectedphoto")).SendClicked();
        var panel = Descendants(page).OfType<WidgetBlendPanel>().Single(); Assert.True(panel.IsVisible);
        ((IButtonController)Button(page, "SelectWidgetLayer" + first.Id.ToString("N"))).SendClicked(); Assert.False(panel.IsVisible);
        ((IButtonController)Button(page, "ClipboardBlendselectedphoto")).SendClicked();
        Assert.Contains(Descendants(panel).OfType<Button>(), x => x.AutomationId == "WidgetBlendModeDarkerColor");
        ((IButtonController)Button(page, "ClipboardResetselectedphoto")).SendClicked(); Assert.False(panel.IsVisible);
        var composite = Descendants(page).OfType<WidgetCompositeView>().Single(); Assert.Equal(WidgetBlendMode.Normal, composite.Design!.Layers[0].BlendMode);
        Assert.DoesNotContain(Descendants(page).OfType<Label>(), x => x.Text == "Image · Darker Color");
    }
    [Fact]
    public void FailedSaveRestoresCompositionWithTheSelectedBlendAndCanBeRetried()
    {
        var photo = Photo() with { BlendMode = WidgetBlendMode.LighterColor };
        var store = new Store { FailSave = true };
        var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, WidgetDesignRules.New() with { Layers = [photo] }); Open(page);
        var composition = Descendants(page).OfType<WidgetCompositeView>().Single();
        ((IButtonController)Button(page, "SaveWidgetDesign")).SendClicked();
        Assert.True(Button(page, "SaveWidgetDesign").IsEnabled); Assert.Equal(photo, composition.Design!.Layers[0]);
        store.FailSave = false; ((IButtonController)Button(page, "SaveWidgetDesign")).SendClicked();
        Assert.Equal(2, store.Saves); Assert.Equal(photo, store.Saved!.Layers[0]);
    }

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
    public void EditorKeepsOffCanvasValuesAndAnExplicitFitChoiceThroughSave()
    {
        var photo = Photo() with { Frame = new(-.4, .8, 2.4, 1.6), FitPhoto = true };
        var original = WidgetDesignRules.New() with { Layers = [photo] };
        var store = new Store(); var page = new WidgetEditorPage(store, new ColourPicker(), NullLogger.Instance, original); Open(page);
        var toggle = Descendants(page).OfType<Switch>().Single(x => x.AutomationId == "WidgetFullPhoto");
        Assert.True(toggle.IsToggled); toggle.IsToggled = false; toggle.IsToggled = true;
        ((IButtonController)Button(page, "SaveWidgetDesign")).SendClicked();
        Assert.Equal(1, store.Saves); Assert.Equal(photo, Assert.Single(store.Saved!.Layers));
        Assert.True(store.Saved.PreserveCanvas);
        Assert.Equal(original.Layers, new[] { photo });
    }

    [Fact]
    public async Task NewPhotoImportsDefaultToFullPhotoAndLegacyLayersKeepTheirCropChoice()
    {
        var store = new Store { Pick = _ => Task.FromResult<WidgetLayer?>(Photo()) }; var page = Editor(store); Open(page);
        var toggle = Descendants(page).OfType<Switch>().Single(x => x.AutomationId == "WidgetFullPhoto");
        Assert.False(toggle.IsToggled);
        await page.AddPhotoAsync(); Assert.True(toggle.IsToggled);
        ((IButtonController)Button(page, "SaveWidgetDesign")).SendClicked();
        Assert.False(store.Saved!.Layers[0].FitPhoto); Assert.True(store.Saved.Layers[1].FitPhoto);
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
        var picker = Descendants(page).OfType<ColourPickerView>().Single(x => x.AutomationId == "WidgetBackgroundColourPicker");
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
        public Func<WidgetSize, WidgetCanvasSizing?> Sizing { get; set; } = _ => null;
        public WidgetCanvasSizing? CanvasSizing(WidgetSize size) => Sizing(size);
        public Func<bool, Task<WidgetLayer?>> Pick { get; set; } = _ => Task.FromResult<WidgetLayer?>(null);
        public int Picks { get; private set; }
        public int Saves { get; private set; }
        public WidgetDesign? Saved { get; private set; }
        public bool FailSave { get; set; }
        public bool Library { get; private set; }
        public List<string> Removed { get; } = [];
        public string? AssetPath(string key) => "/private/" + key + ".png";
        public Task<WidgetLayer?> PickPhotoAsync(bool library) { Picks++; Library = library; return Pick(library); }
        public Task<WidgetSaveResult> SaveAsync(WidgetDesign draft)
        {
            Saves++;
            if (FailSave) return Task.FromException<WidgetSaveResult>(new IOException("Save failed"));
            Saved = draft; return Task.FromResult(new WidgetSaveResult(draft, true));
        }
        public Task RemoveDraftPhotosAsync(IEnumerable<string> keys) { Removed.AddRange(keys); return Task.CompletedTask; }
    }
    public void Dispose() => host.Dispose();
}
