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
        Assert.True(canvas.BeginInteraction(85, 85, false)); canvas.Pan(17, 0); canvas.EndInteraction(false);
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
    public void SingleRowToolsKeepAdjustmentPanelsOpenAndClearTheirHighlightOnDoneOrBackground()
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
        ((IWindow)host.App.Windows[0]).Deactivated(); Assert.False(opacity.IsSelected); Assert.Equal(1, opacity.Scale); ((IWindow)host.App.Windows[0]).Activated();
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
        ((IWindow)host.App.Windows[0]).Deactivated(); Assert.Null(composition.Design); Assert.False(panel.IsVisible);
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
