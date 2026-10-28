using System.Reflection;
using System.Text.Json;
using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetEditorHistoryTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private static WidgetLayer Photo() => new(Guid.NewGuid(), "Photo 1", Guid.NewGuid().ToString("N"), 640, 480, new());
    private static WidgetLayer Layer(WidgetLayerKind kind) => kind switch
    { WidgetLayerKind.Clock => WidgetClocks.New(), WidgetLayerKind.Date => WidgetDates.New(), WidgetLayerKind.Text => WidgetTexts.New(), WidgetLayerKind.Battery => WidgetBatteries.New(), _ => Photo() };
    private void Open(Page page)
    {
        host.App.Windows[0].Page = page;
        if (!host.App.Windows[0].IsActivated) ((IWindow)host.App.Windows[0]).Activated();
        ((IPageController)page).SendAppearing();
    }
    private static IEnumerable<Element> All(Element root)
    {
        yield return root;
        if (root is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var value in All(child)) yield return value;
    }
    private static Button Button(Page page, string id) => All(page).OfType<Button>().Single(x => x.AutomationId == id);
    private static void Click(Page page, string id)
    {
        var button = All(page).OfType<Button>().SingleOrDefault(x => x.AutomationId == id)
            ?? All(All(page).OfType<ClipboardIconButton>().Single(x => x.AutomationId == id)).OfType<Button>().Single();
        ((IButtonController)button).SendClicked();
    }
    private static WidgetDesign Draft(Page page) => All(page).OfType<WidgetCompositeView>().Single().Design!;
    private static void EqualDesign(WidgetDesign expected, WidgetDesign actual) => Assert.Equal(
        JsonSerializer.Serialize(expected, WidgetDesignRules.JsonContext.WidgetDesign), JsonSerializer.Serialize(actual, WidgetDesignRules.JsonContext.WidgetDesign));
    private static void Lock(Page page, Guid id) => ((IButtonController)All(All(page).OfType<ClipboardIconButton>().Single(x => x.AutomationId == "WidgetLayerLock" + id.ToString("N"))).OfType<Button>().Single()).SendClicked();
    private static NavigationPage Navigation(Page root) => (NavigationPage)Activator.CreateInstance(typeof(NavigationPage), BindingFlags.Instance | BindingFlags.NonPublic, null, [false, root], null)!;
    private static WidgetEditorPage Editor(Store store, WidgetDesign original, Picker? picker = null, Func<string, string[], Task<string?>>? menu = null,
        Func<Task<string?>>? layerType = null, Func<WidgetLayer, Task<bool>>? confirm = null) => new(store, picker ?? new(), NullLogger.Instance, original,
            choosePhotoSource: () => Task.FromResult<string?>("Photo Library"), chooseLayerType: layerType, chooseLayerAction: menu, confirmLayerDeletion: confirm);

    [Theory]
    [InlineData(AppTheme.Light)] [InlineData(AppTheme.Dark)]
    public void HistorySitsAboveCanvasHasReadableDisabledIconsAndFollowsAccent(AppTheme theme)
    {
        host.App.UserAppTheme = theme; host.App.Resources["AccentText"] = Colors.Purple;
        var photo = Photo(); var page = Editor(new(), WidgetDesignRules.New() with { Layers = [photo] }); Open(page);
        var controls = All(page).OfType<WidgetHistoryControls>().Single();
        var heading = Assert.IsType<Grid>(controls.Parent); var body = Assert.IsType<Grid>(heading.Parent);
        Assert.Equal(0, Grid.GetRow(heading)); Assert.Equal(1, Grid.GetRow(All(body).OfType<WidgetCanvasViewport>().Single()));
        Assert.Contains(controls, Assert.IsType<WidgetEditorSurface>(page.Content).ProtectedAreas);
        Assert.False(Button(page, "WidgetUndo").IsEnabled); Assert.False(Button(page, "WidgetRedo").IsEnabled);
        var glyph = All(controls).OfType<StudioGlyph>().First(); Assert.Equal(1, glyph.Tint.Alpha);
        var fill = Assert.IsType<SolidColorBrush>(All(controls).OfType<Border>().First().Background).Color;
        Assert.True(AppearanceColors.Contrast(glyph.Tint, fill) >= 3);
        Lock(page, photo.Id); Assert.True(Button(page, "WidgetUndo").IsEnabled); Assert.Equal(Colors.Purple, glyph.Tint);
        host.App.Resources["AccentText"] = Colors.Teal; Assert.Equal(Colors.Teal, glyph.Tint);
        Assert.Equal("Undo widget edit", SemanticProperties.GetDescription(Button(page, "WidgetUndo")));
        Assert.Contains("1 step", SemanticProperties.GetHint(Button(page, "WidgetUndo")));
        Assert.All(controls.Content is HorizontalStackLayout row ? row.Children.OfType<View>() : [], view => { Assert.True(view.MinimumWidthRequest >= 44); Assert.True(view.MinimumHeightRequest >= 44); });
        Click(page, "WidgetUndo"); Assert.False(Draft(page).Layers[0].Locked); Assert.True(Button(page, "WidgetRedo").IsEnabled);
    }
    [Theory]
    [InlineData(WidgetLayerKind.Photo)] [InlineData(WidgetLayerKind.Clock)] [InlineData(WidgetLayerKind.Date)] [InlineData(WidgetLayerKind.Text)] [InlineData(WidgetLayerKind.Battery)]
    public void ManyPinchUpdatesUndoAndRedoAsOneStepWithoutMovingTheViewport(WidgetLayerKind kind)
    {
        var layer = Layer(kind); var page = Editor(new(), WidgetDesignRules.New() with { Layers = [layer] }); Open(page); var initial = Draft(page);
        var canvas = All(page).OfType<WidgetCanvasView>().Single(); ((IView)canvas.PhotoSurface).Arrange(new Rect(0, 0, 170, 170));
        var scroll = All(page).OfType<WidgetEditorScrollView>().Single(); ((IView)scroll).Arrange(new Rect(0, 0, 350, 600)); scroll.SetScrolledPosition(0, 700);
        Assert.True(canvas.BeginInteraction(85, 85, true));
        for (var i = 1; i <= 20; i++) canvas.Pinch(1 + i / 100d);
        Assert.False(Button(page, "WidgetUndo").IsEnabled); Click(page, "WidgetUndo"); var edited = Draft(page);
        canvas.EndInteraction(false); Assert.True(Button(page, "WidgetUndo").IsEnabled); Assert.Contains("1 step", SemanticProperties.GetHint(Button(page, "WidgetUndo")));
        Click(page, "WidgetUndo"); EqualDesign(initial, Draft(page)); Assert.Equal(700, scroll.ScrollY);
        Assert.False(Button(page, "WidgetUndo").IsEnabled); Click(page, "WidgetRedo"); EqualDesign(edited, Draft(page)); Assert.Equal(700, scroll.ScrollY);
    }
    [Fact]
    public void ABackgroundCanceledGesturePreservesRedoAndAChangedDateOrBatteryTickDoesNotCreateHistory()
    {
        var layer = WidgetClocks.New(); var page = Editor(new(), WidgetDesignRules.New() with { Layers = [layer, WidgetBatteries.New()] }); Open(page);
        Lock(page, layer.Id); Click(page, "WidgetUndo"); Assert.True(Button(page, "WidgetRedo").IsEnabled);
        var composition = All(page).OfType<WidgetCompositeView>().Single(); composition.TickClock(DateTimeOffset.Now.AddHours(1)); composition.TickBattery(new(21, true));
        Assert.False(Button(page, "WidgetUndo").IsEnabled);
        Click(page, "SelectWidgetLayer" + layer.Id.ToString("N"));
        var initial = Draft(page); var canvas = All(page).OfType<WidgetCanvasView>().Single(); ((IView)canvas.PhotoSurface).Arrange(new Rect(0, 0, 170, 170));
        Assert.True(canvas.BeginInteraction(85, 85, true)); canvas.Pinch(1.5);
        ((IWindow)host.App.Windows[0]).Deactivated(); Assert.Null(composition.Design); ((IWindow)host.App.Windows[0]).Activated();
        EqualDesign(initial, Draft(page)); Assert.False(Button(page, "WidgetUndo").IsEnabled); Assert.True(Button(page, "WidgetRedo").IsEnabled);
    }
    [Theory]
    [InlineData(WidgetLayerKind.Photo)] [InlineData(WidgetLayerKind.Clock)] [InlineData(WidgetLayerKind.Date)] [InlineData(WidgetLayerKind.Text)] [InlineData(WidgetLayerKind.Battery)]
    public void SliderDragIsOneStepAndASecondDragIsAnother(WidgetLayerKind kind)
    {
        var page = Editor(new(), WidgetDesignRules.New() with { Layers = [Layer(kind)] }); Open(page); var initial = Draft(page);
        if (kind == WidgetLayerKind.Photo) Click(page, "ClipboardAdjustphotoopacity");
        else Click(page, kind == WidgetLayerKind.Battery ? "BatteryToolOpacity" : "ClockToolSize");
        var slider = kind == WidgetLayerKind.Photo ? All(page).OfType<Slider>().Single(x => SemanticProperties.GetDescription(x) == "Selected photo opacity")
            : All(page).OfType<Slider>().Single(x => x.AutomationId == (kind == WidgetLayerKind.Battery ? "BatteryOpacity" : "ClockTextSize"));
        var controller = (ISliderController)slider; controller.SendDragStarted(); slider.Value = .4; slider.Value = .5; slider.Value = .6;
        Assert.False(Button(page, "WidgetUndo").IsEnabled); controller.SendDragCompleted(); var first = Draft(page);
        Assert.Contains("1 step", SemanticProperties.GetHint(Button(page, "WidgetUndo")));
        controller.SendDragStarted(); slider.Value = .7; slider.Value = .8; controller.SendDragCompleted();
        Click(page, "WidgetUndo"); EqualDesign(first, Draft(page)); Click(page, "WidgetUndo"); EqualDesign(initial, Draft(page));
    }
    [Fact]
    public void GroupedBackgroundHueAndRotationCanBeUndoneWhileAnInterruptedSliderRecovers()
    {
        var photo = Photo(); var page = Editor(new(), WidgetDesignRules.New() with { Layers = [photo], TransparentBackground = false }, menu: (_, _) => Task.FromResult<string?>("Size, position & crop")); Open(page);
        Click(page, "WidgetLayerOptions" + photo.Id.ToString("N"));
        var angle = All(page).OfType<Slider>().Single(x => x.AutomationId == "PrecisionRotationAngle");
        ((ISliderController)angle).SendDragStarted(); angle.Value = 15; angle.Value = 38;
        ((IWindow)host.App.Windows[0]).Deactivated(); ((IWindow)host.App.Windows[0]).Activated();
        Assert.True(Button(page, "WidgetUndo").IsEnabled); Click(page, "WidgetUndo"); Assert.Equal(0, Draft(page).Layers[0].RotationDegrees);
        Click(page, "WidgetRedo"); var beforeHue = Draft(page);
        var hue = All(page).OfType<Slider>().Single(x => x.AutomationId == "WidgetBackgroundHue");
        ((ISliderController)hue).SendDragStarted(); hue.Value = 1; hue.Value = 100; hue.Value = 270; ((ISliderController)hue).SendDragCompleted();
        Assert.NotEqual(beforeHue.BackgroundHex, Draft(page).BackgroundHex); Click(page, "WidgetUndo"); EqualDesign(beforeHue, Draft(page));
    }
    [Theory]
    [InlineData(WidgetSize.Small)] [InlineData(WidgetSize.Medium)] [InlineData(WidgetSize.Large)] [InlineData(WidgetSize.ExtraLargePortrait)]
    public void SizeBackgroundAndMeasuredDimensionsRestoreWithoutCreatingResumeSteps(WidgetSize size)
    {
        var store = new Store { Sizing = family => { var bounds = WidgetDesignRules.Canvas(family); return new(new(bounds.Width, bounds.Height), false); } };
        var page = Editor(store, WidgetDesignRules.New() with { Layers = [Photo()], TransparentBackground = true }); Open(page); var initial = Draft(page);
        Click(page, "EditorWidgetSize" + Array.IndexOf(WidgetDesignRules.SupportedSizes.ToArray(), size)); var resized = Draft(page);
        Click(page, "SolidWidgetBackground"); Assert.False(Draft(page).TransparentBackground);
        Click(page, "WidgetUndo"); EqualDesign(resized, Draft(page));
        if (size != WidgetSize.Small) { Click(page, "WidgetUndo"); EqualDesign(initial, Draft(page)); Click(page, "WidgetRedo"); EqualDesign(resized, Draft(page)); }
        var count = SemanticProperties.GetHint(Button(page, "WidgetUndo"));
        ((IWindow)host.App.Windows[0]).Deactivated(); ((IWindow)host.App.Windows[0]).Activated();
        Assert.Equal(count, SemanticProperties.GetHint(Button(page, "WidgetUndo"))); Assert.True(Button(page, "WidgetRedo").IsEnabled);
    }
    [Theory]
    [InlineData(WidgetLayerKind.Photo)] [InlineData(WidgetLayerKind.Clock)] [InlineData(WidgetLayerKind.Date)] [InlineData(WidgetLayerKind.Text)] [InlineData(WidgetLayerKind.Battery)]
    public void DeleteAndDuplicateRestoreAllSettingsAndSelection(WidgetLayerKind kind)
    {
        var layer = Layer(kind) with { RotationDegrees = 27, Locked = true, Opacity = .45 }; var action = "Duplicate layer";
        var page = Editor(new(), WidgetDesignRules.New() with { Layers = [layer] }, menu: (_, _) => Task.FromResult<string?>(action), confirm: _ => Task.FromResult(true)); Open(page);
        var original = Draft(page); Click(page, "WidgetLayerOptions" + layer.Id.ToString("N")); var duplicated = Draft(page); Assert.Equal(2, duplicated.Layers.Count);
        Click(page, "WidgetUndo"); EqualDesign(original, Draft(page)); Click(page, "WidgetRedo"); EqualDesign(duplicated, Draft(page));
        action = "Delete layer"; var copy = duplicated.Layers.Last(); Click(page, "WidgetLayerOptions" + copy.Id.ToString("N")); Assert.Single(Draft(page).Layers);
        Click(page, "WidgetUndo"); EqualDesign(duplicated, Draft(page));
        Assert.Contains(copy.Name, SemanticProperties.GetDescription(Button(page, "SelectWidgetLayer" + copy.Id.ToString("N"))));
        Assert.Single(All(page).OfType<WidgetLayerRow>(), x => x.LayerId == copy.Id);
    }
    [Fact]
    public void VisibilityLockAndReorderAreReversibleAndNewEditsClearRedo()
    {
        var bottom = Photo(); var top = WidgetDates.New(); var page = Editor(new(), WidgetDesignRules.New() with { Layers = [bottom, top] }); Open(page);
        var initial = Draft(page); var row = All(page).OfType<WidgetLayerRow>().Single(x => x.LayerId == top.Id); All(row).OfType<Switch>().Single().IsToggled = false;
        var hidden = Draft(page); Lock(page, top.Id); var locked = Draft(page);
        var list = All(page).OfType<WidgetLayerList>().Single(); var positions = list.Rows.Select((x, i) => new WidgetLayerPosition(x.LayerId, i * 72, 64)).ToArray();
        var drag = list.BeginReorder(positions, top.Id)!; drag.Move(300); list.CompleteReorder(drag); var reordered = Draft(page);
        Assert.Equal(top.Id, reordered.Layers[0].Id); Click(page, "WidgetUndo"); EqualDesign(locked, Draft(page));
        Click(page, "WidgetUndo"); EqualDesign(hidden, Draft(page)); Click(page, "WidgetUndo"); EqualDesign(initial, Draft(page));
        Click(page, "WidgetRedo"); EqualDesign(hidden, Draft(page)); Lock(page, bottom.Id); Assert.False(Button(page, "WidgetRedo").IsEnabled);
    }
    [Fact]
    public void TypingGroupsNameAndTextSeparatelyAndUndoDiscardsUnappliedColourInputs()
    {
        var layer = WidgetTexts.New(); var page = Editor(new(), WidgetDesignRules.New() with { Layers = [layer] }); Open(page); Click(page, "ClockToolTime");
        var content = All(page).OfType<Editor>().Single(x => x.AutomationId == "WidgetTextContent"); var original = content.Text;
        content.Text = "H"; content.Text = "He"; content.Text = "Hello";
        Assert.Contains("1 step", SemanticProperties.GetHint(Button(page, "WidgetUndo")));
        var name = All(page).OfType<ProfileNameEntry>().Single(); var originalName = name.Text; name.Text = "N"; name.Text = "New name";
        Assert.Contains("2 steps", SemanticProperties.GetHint(Button(page, "WidgetUndo")));
        Click(page, "WidgetUndo"); Assert.Equal(originalName, name.Text); Assert.Equal("Hello", content.Text);
        Click(page, "ClockToolColour"); var hex = All(page).OfType<HexColorEntry>().Single(x => x.AutomationId == "ClockTextHex"); hex.Text = "#BAD";
        Click(page, "WidgetUndo"); Assert.Equal(original, All(page).OfType<Editor>().Single().Text); Assert.NotEqual("#BAD", hex.Text);
        Click(page, "WidgetRedo"); Assert.Equal("Hello", All(page).OfType<Editor>().Single().Text); Assert.Equal(originalName, name.Text);
    }
    [Fact]
    public async Task NativeColourPreviewsAndBackgroundResumeAreOneStepAndUnchangedCancellationKeepsRedo()
    {
        var pending = new TaskCompletionSource<string>(); Action<string> preview = null!; var picker = new Picker { Pick = (_, show) => { preview = show; return pending.Task; } };
        var clock = WidgetClocks.New(); var page = Editor(new(), WidgetDesignRules.New() with { Layers = [clock] }, picker); Open(page); Click(page, "ClockToolColour");
        var colours = All(page).OfType<ColourPickerView>().Single(x => x.AutomationId == "ClockTextColourPicker"); var picking = colours.PickAsync();
        preview("#123456"); ((IWindow)host.App.Windows[0]).Deactivated(); preview("#654321"); pending.SetResult("#ABCDEF"); await picking;
        ((IWindow)host.App.Windows[0]).Activated(); Assert.Contains("1 step", SemanticProperties.GetHint(Button(page, "WidgetUndo")));
        Assert.Equal("#ABCDEF", Draft(page).Layers[0].Clock!.ColourHex); Click(page, "WidgetUndo"); Assert.Equal(clock.Clock!.ColourHex, Draft(page).Layers[0].Clock!.ColourHex);
        picker.Pick = (value, show) => { show("#FF0000"); return Task.FromResult(value); };
        await colours.PickAsync(); Assert.False(Button(page, "WidgetUndo").IsEnabled); Assert.True(Button(page, "WidgetRedo").IsEnabled);
    }
    [Fact]
    public async Task ImportedPhotosStayForUndoRedoThenAreCleanedWhenTheirBranchIsDiscarded()
    {
        var store = new Store(); var original = Photo(); var imported = Photo(); store.Pick = _ => Task.FromResult<WidgetLayer?>(imported);
        var page = Editor(store, WidgetDesignRules.New() with { Layers = [original] }); Open(page); await page.AddPhotoAsync(); Assert.Equal(2, Draft(page).Layers.Count);
        Click(page, "WidgetUndo"); Assert.Single(Draft(page).Layers); Assert.DoesNotContain(imported.AssetKey, store.Removed);
        Click(page, "WidgetRedo"); Assert.Equal(2, Draft(page).Layers.Count); Assert.NotNull(store.AssetPath(imported.AssetKey));
        Click(page, "WidgetUndo"); Lock(page, original.Id); Assert.Contains(imported.AssetKey, store.Removed); Assert.DoesNotContain(original.AssetKey, store.Removed);
        Assert.False(Button(page, "WidgetRedo").IsEnabled);
    }
    [Fact]
    public async Task DeletedImportedPhotoCanBeRestoredAndHistoryEvictionEventuallyReleasesIt()
    {
        var store = new Store(); var original = Photo(); var imported = Photo(); store.Pick = _ => Task.FromResult<WidgetLayer?>(imported);
        var page = Editor(store, WidgetDesignRules.New() with { Layers = [original] }, menu: (_, _) => Task.FromResult<string?>("Delete layer"), confirm: _ => Task.FromResult(true)); Open(page);
        await page.AddPhotoAsync(); Click(page, "WidgetLayerOptions" + imported.Id.ToString("N")); Assert.DoesNotContain(imported.AssetKey, store.Removed);
        Click(page, "WidgetUndo"); Assert.Equal(2, Draft(page).Layers.Count); Assert.NotNull(store.AssetPath(imported.AssetKey)); Click(page, "WidgetRedo");
        for (var i = 0; i < WidgetEditHistory.MaximumSteps; i++) Lock(page, original.Id);
        Assert.Contains(imported.AssetKey, store.Removed); Assert.DoesNotContain(original.AssetKey, store.Removed);
        Assert.Contains("100 steps", SemanticProperties.GetHint(Button(page, "WidgetUndo")));
    }
    [Fact]
    public async Task SaveFailureKeepsHistoryAndSavingAnUndoneStateCleansOnlyUnsavedPhotos()
    {
        var store = new Store { FailSave = true }; var original = Photo(); var imported = Photo(); store.Pick = _ => Task.FromResult<WidgetLayer?>(imported);
        var page = Editor(store, WidgetDesignRules.New() with { Layers = [original] }); Open(page); await page.AddPhotoAsync(); var added = Draft(page);
        Click(page, "SaveWidgetDesign"); Assert.True(Button(page, "WidgetUndo").IsEnabled); EqualDesign(added, Draft(page)); Assert.DoesNotContain(imported.AssetKey, store.Removed);
        Click(page, "WidgetUndo"); store.FailSave = false; Click(page, "SaveWidgetDesign"); Assert.Single(store.Saved!.Layers);
        Assert.Contains(imported.AssetKey, store.Removed); Assert.DoesNotContain(original.AssetKey, store.Removed);
        Assert.False(Button(page, "WidgetUndo").IsEnabled); Assert.False(Button(page, "WidgetRedo").IsEnabled);
        var reopened = Editor(new(), store.Saved); Open(reopened); Assert.False(Button(reopened, "WidgetUndo").IsEnabled);
    }
    [Fact]
    public async Task LeavingWithRedoPhotosReleasesSessionOwnershipAndPickerCancellationAddsNoStep()
    {
        var store = new Store(); var original = Photo(); var imported = Photo(); var page = Editor(store, WidgetDesignRules.New() with { Layers = [original] });
        var navigation = Navigation(new ContentPage()); Open(navigation); await navigation.PushAsync(page);
        await page.AddPhotoAsync(); Assert.False(Button(page, "WidgetUndo").IsEnabled); // Cancelled photo selection.
        store.Pick = _ => Task.FromResult<WidgetLayer?>(imported); await page.AddPhotoAsync(); Click(page, "WidgetUndo");
        Assert.DoesNotContain(imported.AssetKey, store.Removed); await navigation.PopAsync(); Assert.Contains(imported.AssetKey, store.Removed);
        Assert.DoesNotContain(original.AssetKey, store.Removed); Assert.False(Button(page, "WidgetUndo").IsEnabled); Assert.False(Button(page, "WidgetRedo").IsEnabled);
    }
    [Fact]
    public async Task BackgroundColourHistoryContinuesWithoutRestartingSuspendedComposition()
    {
        var pending = new TaskCompletionSource<string>(); Action<string> preview = null!;
        var picker = new Picker { Pick = (_, show) => { preview = show; return pending.Task; } };
        var page = Editor(new(), WidgetDesignRules.New() with { TransparentBackground = false, Layers = [Photo()] }, picker); Open(page); var original = Draft(page);
        var colours = All(page).OfType<ColourPickerView>().Single(x => x.AutomationId == "WidgetBackgroundColourPicker");
        var picking = colours.PickAsync(); preview("#123456"); ((IWindow)host.App.Windows[0]).Deactivated(); preview("#654321");
        var composition = All(page).OfType<WidgetCompositeView>().Single(); Assert.Null(composition.Design);
        pending.SetResult("#ABCDEF"); await picking; Assert.Null(composition.Design); ((IWindow)host.App.Windows[0]).Activated();
        Assert.Equal("#ABCDEF", Draft(page).BackgroundHex); Assert.Contains("1 step", SemanticProperties.GetHint(Button(page, "WidgetUndo")));
        Click(page, "WidgetUndo"); EqualDesign(original, Draft(page));
    }
    [Theory]
    [InlineData(WidgetLayerKind.Clock)] [InlineData(WidgetLayerKind.Date)] [InlineData(WidgetLayerKind.Text)] [InlineData(WidgetLayerKind.Battery)]
    public async Task AddingLiveOrTextLayersUndoToEmptyAndRedoPreservesTheSameIdentity(WidgetLayerKind kind)
    {
        var page = Editor(new(), WidgetDesignRules.New(), layerType: () => Task.FromResult<string?>(kind.ToString())); Open(page);
        await page.AddLayerAsync(); var added = Draft(page); Assert.Equal(kind, Assert.Single(added.Layers).Kind);
        Click(page, "WidgetUndo"); Assert.Empty(Draft(page).Layers); Assert.False(Button(page, "WidgetUndo").IsEnabled);
        Click(page, "WidgetRedo"); EqualDesign(added, Draft(page)); Assert.Contains("selected", SemanticProperties.GetDescription(Button(page, "SelectWidgetLayer" + added.Layers[0].Id.ToString("N"))));
    }
    [Fact]
    public void UndoBatteryEditsDiscardsInvalidThresholdsAndRehydratesItsStops()
    {
        var layer = WidgetBatteries.New(); var page = Editor(new(), WidgetDesignRules.New() with { Layers = [layer] }); Open(page);
        Click(page, "BatteryToolColour"); var original = Draft(page); Click(page, "AddBatteryStop"); Assert.Equal(5, Draft(page).Layers[0].Battery!.Stops.Count);
        var threshold = All(page).OfType<Entry>().Single(x => x.AutomationId == "BatteryThreshold"); threshold.Text = "999";
        Click(page, "WidgetUndo"); EqualDesign(original, Draft(page)); Assert.NotEqual("999", threshold.Text);
        Assert.DoesNotContain(All(page).OfType<Label>(), x => x.IsVisible && x.Text?.StartsWith("Enter a whole percentage") == true);
        Click(page, "WidgetRedo"); Assert.Equal(5, Draft(page).Layers[0].Battery!.Stops.Count);
    }
    [Fact]
    public void UndoingDeletionAtTheLayerLimitRestoresAvailabilityAndEveryLayer()
    {
        var layers = Enumerable.Range(1, WidgetDesignRules.MaximumLayers).Select(i => WidgetTexts.New("Text " + i)).ToArray();
        var page = Editor(new(), WidgetDesignRules.New() with { Layers = layers }, menu: (_, _) => Task.FromResult<string?>("Delete layer"), confirm: _ => Task.FromResult(true)); Open(page);
        var original = Draft(page); Assert.False(Button(page, "AddWidgetPhoto").IsEnabled); Click(page, "WidgetLayerOptions" + layers[^1].Id.ToString("N"));
        Assert.True(Button(page, "AddWidgetPhoto").IsEnabled); Click(page, "WidgetUndo"); EqualDesign(original, Draft(page));
        Assert.Equal(WidgetDesignRules.MaximumLayers, All(page).OfType<WidgetLayerRow>().Count()); Assert.False(Button(page, "AddWidgetPhoto").IsEnabled);
    }
    [Fact]
    public void InterruptedHueDragReleasesItsThumbStateAndDoesNotBlockUndo()
    {
        var page = Editor(new(), WidgetDesignRules.New() with { TransparentBackground = false }); Open(page);
        var hue = All(page).OfType<Slider>().Single(x => x.AutomationId == "WidgetBackgroundHue"); var original = hue.Value;
        ((ISliderController)hue).SendDragStarted(); hue.Value = 100;
        ((IWindow)host.App.Windows[0]).Deactivated(); ((IWindow)host.App.Windows[0]).Activated();
        Click(page, "WidgetUndo"); Assert.Equal(original, hue.Value, 3); Assert.True(Button(page, "WidgetRedo").IsEnabled);
    }
    [Fact]
    public async Task SavingAnImportedPhotoKeepsTheSavedSourceAndReleasesOnlyRetiredDrafts()
    {
        var imported = Photo(); var store = new Store { Pick = _ => Task.FromResult<WidgetLayer?>(imported) };
        var page = Editor(store, WidgetDesignRules.New()); Open(page); await page.AddPhotoAsync(); Click(page, "SaveWidgetDesign");
        Assert.Equal(imported.AssetKey, Assert.Single(store.Saved!.Layers).AssetKey); Assert.DoesNotContain(imported.AssetKey, store.Removed);
        Assert.NotNull(store.AssetPath(imported.AssetKey)); Assert.False(Button(page, "WidgetUndo").IsEnabled);
    }
    [Fact]
    public void InvalidWidgetNameSaveKeepsAValidPreviewAndCanUndoTheUncommittedName()
    {
        var page = Editor(new(), WidgetDesignRules.New() with { Layers = [Photo()] }); Open(page); var initial = Draft(page);
        var name = All(page).OfType<ProfileNameEntry>().Single(); var previous = name.Text; name.Text = "";
        Click(page, "SaveWidgetDesign"); EqualDesign(initial, Draft(page)); Assert.True(Button(page, "WidgetUndo").IsEnabled);
        Click(page, "WidgetUndo"); Assert.Equal(previous, name.Text); EqualDesign(initial, Draft(page));
    }
    [Fact]
    public async Task FailedPhotoPruningDoesNotBreakHistoryAndRetriesOnTheNextEdit()
    {
        var imported = Photo(); var original = Photo(); var store = new Store { Pick = _ => Task.FromResult<WidgetLayer?>(imported) };
        var page = Editor(store, WidgetDesignRules.New() with { Layers = [original] }); Open(page); await page.AddPhotoAsync(); Click(page, "WidgetUndo");
        store.CleanupFailures = 1; Lock(page, original.Id); Assert.DoesNotContain(imported.AssetKey, store.Removed); Assert.True(Button(page, "WidgetUndo").IsEnabled);
        Lock(page, original.Id); Assert.Contains(imported.AssetKey, store.Removed); Click(page, "WidgetUndo"); Assert.True(Draft(page).Layers[0].Locked);
    }
    [Fact]
    public void HidingTheActiveSliderBySelectingAnotherLayerFinishesItsHistoryStep()
    {
        var clock = WidgetClocks.New(); var photo = Photo(); var page = Editor(new(), WidgetDesignRules.New() with { Layers = [photo, clock] }); Open(page);
        Click(page, "ClockToolSize"); var slider = All(page).OfType<Slider>().Single(x => x.AutomationId == "ClockTextSize");
        ((ISliderController)slider).SendDragStarted(); slider.Value = .5;
        Click(page, "SelectWidgetLayer" + photo.Id.ToString("N"));
        Assert.True(Button(page, "WidgetUndo").IsEnabled); Assert.Contains("1 step", SemanticProperties.GetHint(Button(page, "WidgetUndo")));
        Click(page, "WidgetUndo"); Assert.Equal(clock.Clock!.FontScale, Draft(page).Layers[1].Clock!.FontScale);
        Click(page, "WidgetRedo"); Assert.Equal(.5, Draft(page).Layers[1].Clock!.FontScale);
    }
    [Fact]
    public void DisablingAnActiveSliderCannotLeaveTheEditorHistoryStuck()
    {
        var clock = WidgetClocks.New(); var store = new Store(); var page = Editor(store, WidgetDesignRules.New() with { Layers = [clock] }); Open(page);
        Click(page, "ClockToolSize"); var slider = All(page).OfType<Slider>().Single(x => x.AutomationId == "ClockTextSize");
        ((ISliderController)slider).SendDragStarted(); slider.Value = .5; slider.IsEnabled = false;
        Assert.True(Button(page, "WidgetUndo").IsEnabled); Click(page, "SaveWidgetDesign"); Assert.NotNull(store.Saved);
        Assert.Equal(.5, store.Saved!.Layers[0].Clock!.FontScale);
    }
    [Fact]
    public async Task TypingBackToTheOriginalNameAtTheLimitRetainsTheOldestRestorablePhoto()
    {
        var imported = Photo(); var original = Photo(); var store = new Store { Pick = _ => Task.FromResult<WidgetLayer?>(imported) };
        var page = Editor(store, WidgetDesignRules.New() with { Layers = [original] }, menu: (_, _) => Task.FromResult<string?>("Delete layer"), confirm: _ => Task.FromResult(true)); Open(page);
        await page.AddPhotoAsync(); Click(page, "WidgetLayerOptions" + imported.Id.ToString("N"));
        for (var i = 0; i < WidgetEditHistory.MaximumSteps - 1; i++) Lock(page, original.Id);
        Assert.Contains("100 steps", SemanticProperties.GetHint(Button(page, "WidgetUndo")));
        var name = All(page).OfType<ProfileNameEntry>().Single(); var previous = name.Text;
        name.Text = "Temporary"; Assert.DoesNotContain(imported.AssetKey, store.Removed);
        name.Text = previous; Assert.Contains("100 steps", SemanticProperties.GetHint(Button(page, "WidgetUndo")));
        for (var i = 0; i < WidgetEditHistory.MaximumSteps; i++) Click(page, "WidgetUndo");
        Assert.Contains(Draft(page).Layers, x => x.AssetKey == imported.AssetKey); Assert.DoesNotContain(imported.AssetKey, store.Removed);
    }
    private sealed class Picker : IColourPicker
    {
        public Func<string, Action<string>, Task<string>> Pick = (hex, _) => Task.FromResult(hex);
        public Task<string> PickAsync(string initialHex, Action<string> preview) => Pick(initialHex, preview);
    }
    private sealed class Store : IWidgetEditorStore
    {
        public Func<WidgetSize, WidgetCanvasSizing?> Sizing = _ => null;
        public WidgetCanvasSizing? CanvasSizing(WidgetSize size) => Sizing(size);
        public Func<bool, Task<WidgetLayer?>> Pick = _ => Task.FromResult<WidgetLayer?>(null);
        public WidgetDesign? Saved;
        public bool FailSave;
        public int CleanupFailures;
        public HashSet<string> Removed = [];
        public string? AssetPath(string key) => Removed.Contains(key) ? null : "/private/" + key + ".png";
        public Task<WidgetLayer?> PickPhotoAsync(bool library) => Pick(library);
        public Task<WidgetSaveResult> SaveAsync(WidgetDesign draft)
        {
            WidgetDesignRules.Validate(draft);
            if (FailSave) return Task.FromException<WidgetSaveResult>(new IOException("Storage unavailable"));
            Saved = draft; return Task.FromResult(new WidgetSaveResult(draft, true));
        }
        public Task RemoveDraftPhotosAsync(IEnumerable<string> keys)
        {
            if (CleanupFailures > 0) { CleanupFailures--; throw new IOException("Storage unavailable"); }
            var saved = Saved?.Layers.Select(x => x.AssetKey).ToHashSet() ?? [];
            foreach (var key in keys) if (!saved.Contains(key)) Removed.Add(key);
            return Task.CompletedTask;
        }
    }
    public void Dispose() => host.Dispose();
}
