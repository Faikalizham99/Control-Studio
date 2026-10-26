using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetCanvasTests : IDisposable
{
    [Fact]
    public void MixedLiveCanvasSeparatesBatteryReadingsFromTextTicksAndDescribesUnknownAndCharging()
    {
        var battery = WidgetBatteries.New(); var clock = WidgetClocks.New(); var date = WidgetDates.New();
        var design = WidgetDesignRules.New() with { Layers = [clock, date, battery] };
        var view = new WidgetCanvasView(_ => throw new InvalidOperationException("Battery has no asset"), _ => { }); host.Attach(view);
        view.Update(design, battery.Id);
        var border = Assert.IsType<Border>(view.Workspace.Children[1]);
        var composite = Assert.IsType<WidgetCompositeView>(Assert.IsType<Grid>(border.Content).Children[2]);
        composite.TickClock(DateTimeOffset.Now); Assert.Contains("Battery unavailable", SemanticProperties.GetDescription(view));
        composite.TickBattery(new(45, true)); Assert.Contains("Battery 45%, charging", SemanticProperties.GetDescription(view));
        composite.TickClock(DateTimeOffset.Now.AddMinutes(1)); Assert.Contains("Battery 45%", SemanticProperties.GetDescription(view));
        composite.TickBattery(new(100, true, true)); Assert.Contains("fully charged", SemanticProperties.GetDescription(view));
        view.Update(design with { Layers = [battery with { Visible = false }] }, null); Assert.DoesNotContain("Battery 100%", SemanticProperties.GetDescription(view));
    }
    [Fact]
    public void OnlyTheOutsideGhostPaintsAGradientAndSuspensionReleasesIt()
    {
        var layers = Enumerable.Range(0, 30).Select(index => WidgetClocks.New("Clock " + index) with { Clock = new(Gradient: new()) }).ToArray();
        var design = WidgetDesignRules.New() with { Layers = layers }; var view = new WidgetCanvasView(_ => null, _ => { }); host.Attach(view);
        view.Update(design, layers[^1].Id); ((IView)view.PhotoSurface).Arrange(new Rect(0, 0, 170, 170));
        var labels = view.PhotoSurface.Children.OfType<AbsoluteLayout>().SelectMany(frame => frame.Children.OfType<WidgetClockFontLabel>()).ToArray();
        Assert.Equal(30, labels.Length); Assert.All(labels, label => Assert.False(label.PaintGradient));
        var ghost = Assert.IsType<AbsoluteLayout>(view.Workspace.Children[0]).Children.OfType<WidgetClockFontLabel>().Single(); Assert.True(ghost.PaintGradient);
        view.SuspendComposition(); Assert.False(ghost.PaintGradient); view.Update(design, layers[^1].Id); Assert.True(ghost.PaintGradient);
        view.Update(design, null); Assert.False(ghost.PaintGradient);
    }
    [Theory]
    [InlineData(WidgetLayerKind.Photo)] [InlineData(WidgetLayerKind.Clock)] [InlineData(WidgetLayerKind.Date)] [InlineData(WidgetLayerKind.Text)]
    public void HandleRotationKeepsTheLayerCentreAcrossAnglesFamiliesCancellationAndLocks(WidgetLayerKind kind)
    {
        foreach (var family in WidgetDesignRules.SupportedSizes)
        {
            var layer = (kind switch { WidgetLayerKind.Clock => WidgetClocks.New(), WidgetLayerKind.Date => WidgetDates.New(), WidgetLayerKind.Text => WidgetTexts.New(),
                _ => new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 640, 480, new()) }) with { Frame = new(.2, .3, .6, .4) };
            var design = WidgetDesignRules.New() with { Size = family, Layers = [layer] }; WidgetCanvasView? view = null;
            view = new(_ => null, _ => { }, value => { design = design with { Layers = [value] }; view!.Update(design, value.Id); });
            host.Attach(view); view.Update(design, layer.Id); var canvas = WidgetDesignRules.Canvas(family);
            var width = 260d; var height = width * canvas.Height / canvas.Width;
            ((IView)view.PhotoSurface).Arrange(new Rect(32, 32, width, height));
            var centre = new Point(32 + .5 * width, 32 + .5 * height); var handle = view.RotationHandleCenter!.Value;
            Assert.True(view.BeginInteraction(handle.X, handle.Y, false)); Assert.True(view.IsRotating);
            view.Pan(500, 500); view.Pinch(2); Assert.Equal(layer, design.Layers.Single());
            foreach (var degrees in new[] { 37d, 170, 179, 181, 270, 360, 397 })
            {
                var pointer = WidgetLayerRotation.Point(handle.X, handle.Y, centre.X, centre.Y, degrees);
                view.Drag(pointer.X, pointer.Y, 500, 500);
                Assert.Equal(WidgetLayerRotation.Normalize(degrees), design.Layers[0].RotationDegrees, 8);
                Assert.Equal(layer with { RotationDegrees = design.Layers[0].RotationDegrees }, design.Layers[0]);
            }
            view.Drag(double.NaN, 0, 0, 0); view.Drag(centre.X, centre.Y, 0, 0); Assert.Equal(37, design.Layers[0].RotationDegrees, 8);
            view.EndInteraction(false); Assert.False(view.IsInteracting); Assert.False(view.IsRotating);
            Assert.Equal(37, Assert.IsType<AbsoluteLayout>(view.PhotoSurface.Children[0]).Rotation, 8);
            var saved = design.Layers[0]; handle = view.RotationHandleCenter!.Value;
            Assert.True(view.BeginInteraction(handle.X, handle.Y, false)); var next = WidgetLayerRotation.Point(handle.X, handle.Y, centre.X, centre.Y, 30);
            view.Drag(next.X, next.Y, 0, 0); Assert.Equal(67, design.Layers[0].RotationDegrees, 8); view.EndInteraction(true); Assert.Equal(saved, design.Layers[0]);
            view.Update(design with { Layers = [saved with { Locked = true }] }, saved.Id); Assert.Null(view.RotationHandleCenter);
            Assert.False(view.BeginInteraction(handle.X, handle.Y, false));
        }
    }
    [Fact]
    public void RotationHandleDoesNotStealSmallLayerBodyDragsAndBoundsChangesCancelRotation()
    {
        var photo = new WidgetLayer(Guid.NewGuid(), "Small", Guid.NewGuid().ToString("N"), 640, 480, new(.45, 0, .05, .05));
        var design = WidgetDesignRules.New() with { Layers = [photo] }; WidgetCanvasView? view = null; var cleared = 0;
        view = new(_ => null, _ => { }, value => { design = design with { Layers = [value] }; view!.Update(design, value.Id); }, () => cleared++);
        host.Attach(view); view.Update(design, photo.Id); ((IView)view.PhotoSurface).Arrange(new Rect(32, 32, 170, 170));
        Assert.True(view.BeginInteraction(32 + .475 * 170, 32 + .025 * 170, false)); Assert.False(view.IsRotating); view.EndInteraction(false);
        var handle = view.RotationHandleCenter!.Value; view.SelectAt(handle.X, handle.Y); Assert.Equal(0, cleared);
        Assert.True(view.BeginInteraction(handle.X, handle.Y, false));
        view.Drag(handle.X + 20, handle.Y + 10, 0, 0); Assert.NotEqual(0, design.Layers[0].RotationDegrees);
        ((IView)view.PhotoSurface).Arrange(new Rect(32, 32, 160, 160)); Assert.False(view.IsInteracting); Assert.Equal(photo, design.Layers[0]);
        view.Update(design with { Layers = [photo with { Visible = false }] }, photo.Id); Assert.Null(view.RotationHandleCenter);
    }
    [Fact]
    public void CustomTextWrapsWithoutPhotoIOOrTimeTicksAndKeepsItsCentreWhileResizing()
    {
        var text = WidgetTexts.New() with { Text = new("Hello\n世界 🌸") }; var reads = 0;
        var draft = WidgetDesignRules.New() with { Layers = [text] }; WidgetCanvasView? view = null;
        view = new WidgetCanvasView(_ => { reads++; return null; }, _ => { }, value => { draft = draft with { Layers = [value] }; view!.Update(draft, value.Id); });
        host.Attach(view); view.Update(draft, text.Id); ((IView)view.PhotoSurface).Arrange(new Rect(0, 0, 170, 170));
        var frame = Assert.IsType<AbsoluteLayout>(view.PhotoSurface.Children.Single()); var label = frame.Children.OfType<WidgetClockFontLabel>().Single();
        Assert.Equal("Hello\n世界 🌸", label.Text); Assert.Equal(-1, label.MaxLines); Assert.Equal(LineBreakMode.WordWrap, label.LineBreakMode);
        var widget = Assert.IsType<Border>(view.Workspace.Children[1]); var composite = Assert.IsType<Grid>(widget.Content).Children.OfType<WidgetCompositeView>().Single();
        composite.TickClock(DateTimeOffset.MaxValue); Assert.Equal("Hello\n世界 🌸", label.Text); Assert.Equal(0, reads);
        Assert.True(view.BeginInteraction(117, 117, true)); view.Pinch(1.5, 100, -100); view.EndInteraction(false);
        var resized = draft.Layers.Single(); Assert.Equal(.5, resized.Frame.X + resized.Frame.Width / 2, 10); Assert.Equal(.5, resized.Frame.Y + resized.Frame.Height / 2, 10);
        Assert.Equal(text.Text, resized.Text); view.Update(draft with { Layers = [resized with { Locked = true }] }, resized.Id);
        Assert.False(view.BeginInteraction(117, 117, true)); Assert.Equal(0, reads);
    }
    [Fact]
    public void DateCanvasTicksWithoutPhotoAccessAndPinchesAroundItsCentre()
    {
        var date = WidgetDates.New() with { Date = new(WidgetDateFormat.Numeric), Frame = new(.1, .35, .8, .3) };
        var draft = WidgetDesignRules.New() with { Layers = [date] }; var photoReads = 0; WidgetCanvasView? view = null;
        view = new WidgetCanvasView(_ => { photoReads++; return null; }, _ => { }, layer => { draft = draft with { Layers = [layer] }; view!.Update(draft, layer.Id); });
        host.Attach(view); view.Update(draft, date.Id); ((IView)view.PhotoSurface).Arrange(new Rect(0, 0, 170, 170));
        var widget = Assert.IsType<Border>(view.Workspace.Children[1]);
        var composite = Assert.IsType<Grid>(widget.Content).Children.OfType<WidgetCompositeView>().Single();
        var frame = Assert.IsType<AbsoluteLayout>(view.PhotoSurface.Children.Single()); var label = frame.Children.OfType<WidgetClockFontLabel>().Single();
        var next = new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero); composite.TickClock(next);
        Assert.Equal(WidgetDates.Text(date.Date!, next), label.Text); Assert.Equal(0, photoReads);
        Assert.True(view.BeginInteraction(85, 85, true)); view.Pinch(1.5, 100, -100); view.EndInteraction(false);
        var changed = draft.Layers.Single(); Assert.Equal(.5, changed.Frame.X + changed.Frame.Width / 2, 8); Assert.Equal(.5, changed.Frame.Y + changed.Frame.Height / 2, 8);
        Assert.Equal(date.Date, changed.Date); Assert.Null(changed.Clock);
        view.Update(draft with { Layers = [changed with { Locked = true }] }, date.Id); Assert.False(view.BeginInteraction(85, 85, true));
        Assert.Equal(0, photoReads);
    }
    private readonly MauiTestContext host = new();
    [Fact]
    public void ClockPinchKeepsCentreDespiteOffCentreFingersAndStillAllowsSingleFingerMovement()
    {
        var clock = WidgetClocks.New() with { Frame = new(.2, .3, .6, .3) };
        var draft = WidgetDesignRules.New() with { Layers = [clock] }; WidgetCanvasView? canvas = null;
        canvas = new WidgetCanvasView(_ => null, _ => { }, changed => { draft = draft with { Layers = [changed] }; canvas!.Update(draft, changed.Id); });
        host.Attach(canvas); canvas.Update(draft, clock.Id); ((IView)canvas.PhotoSurface).Arrange(new Rect(32, 32, 170, 170));
        // Start near a corner, then change the pinch midpoint as well as scale.
        Assert.True(canvas.BeginInteraction(74.5, 91.5, true));
        foreach (var (factor, dx, dy) in new[] { (2d, 50d, -40d), (.5, -20d, 30d), (1.5, 0d, 70d) })
        {
            canvas.Pinch(factor, dx, dy); var frame = draft.Layers[0].Frame;
            Assert.Equal(.5, frame.X + frame.Width / 2, 8); Assert.Equal(.45, frame.Y + frame.Height / 2, 8);
            Assert.Equal(clock.Frame.Width * factor, frame.Width, 8);
        }
        canvas.EndInteraction(false); var committed = draft.Layers[0]; canvas.Pinch(5, 90, 90); Assert.Equal(committed, draft.Layers[0]);
        Assert.True(canvas.BeginInteraction(117, 108.5, true)); canvas.Pinch(.5); canvas.EndInteraction(true); Assert.Equal(committed, draft.Layers[0]);
        Assert.True(canvas.BeginInteraction(117, 108.5, false)); canvas.Pan(17, 17); canvas.EndInteraction(false);
        Assert.Equal(committed.Frame.X + .1, draft.Layers[0].Frame.X, 8); Assert.Equal(committed.Frame.Y + .1, draft.Layers[0].Frame.Y, 8);
    }
    [Theory]
    [InlineData(false, 1)] [InlineData(false, 3)] [InlineData(true, 1)] [InlineData(true, 3)]
    public void PinchReleaseNeverReadsChangedTouchLocationsAndKeepsLastValidPhotoOrClockFrame(bool isClock, int changedTouchCount)
    {
        var layer = isClock ? WidgetClocks.New() : new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 640, 480, new(.2, .2, .6, .6));
        var draft = WidgetDesignRules.New() with { Layers = [layer] }; WidgetCanvasView? canvas = null;
        canvas = new WidgetCanvasView(_ => null, _ => { }, changed => { draft = draft with { Layers = [changed] }; canvas!.Update(draft, changed.Id); });
        host.Attach(canvas); canvas.Update(draft, layer.Id); ((IView)canvas.PhotoSurface).Arrange(new Rect(0, 0, 170, 170));
        Assert.True(canvas.BeginInteraction(85, 85, true)); var locationReads = 0;
        WidgetPinchInput.Update(true, 2, () => { locationReads++; canvas.Pinch(1.4, 12, 15); }, () => canvas.EndInteraction(false));
        var resized = draft.Layers[0];
        foreach (var touches in new[] { 0, 1, 2 })
            WidgetPinchInput.Update(false, touches, () => { locationReads++; canvas.Pinch(2, 90, -90); }, () => canvas.EndInteraction(false));
        canvas.EndInteraction(false); Assert.Equal(1, locationReads); Assert.Equal(resized, draft.Layers[0]);
        Assert.True(canvas.BeginInteraction(85, 85, true));
        WidgetPinchInput.Update(true, 2, () => canvas.Pinch(.7, 10, 10), () => canvas.EndInteraction(false));
        var valid = draft.Layers[0];
        WidgetPinchInput.Update(true, changedTouchCount, () => throw new InvalidOperationException("A changed touch set must not read its new location"), () => canvas.EndInteraction(false));
        Assert.False(canvas.IsInteracting); Assert.Equal(valid, draft.Layers[0]);
        canvas.Pinch(2, 90, -90); Assert.Equal(valid, draft.Layers[0]);
        Assert.True(canvas.BeginInteraction(85, 85, true)); canvas.Pinch(.5); canvas.EndInteraction(true); Assert.Equal(valid, draft.Layers[0]);
    }
    [Fact]
    public void LiveEditsReuseImagesWhileOrderVisibilityAndRemovalUpdate()
    {
        var photo = new WidgetLayer(Guid.NewGuid(), "Photo 1", Guid.NewGuid().ToString("N"), 640, 480, new());
        var second = photo with { Id = Guid.NewGuid(), Name = "Photo 2" };
        var design = WidgetDesignRules.New() with { Layers = [photo, second] };
        var canvas = new WidgetCanvasView(_ => "/private/photo.png", _ => { }); host.Attach(canvas); canvas.Update(design, second.Id);
        Assert.True(Checker(canvas).IsVisible);
        var layout = canvas.PhotoSurface;
        var firstFrame = Assert.IsType<AbsoluteLayout>(layout.Children[0]);
        var firstImage = Assert.IsType<Image>(firstFrame.Children[0]);
        var secondFrame = Assert.IsType<AbsoluteLayout>(layout.Children[1]);
        var originalSource = firstImage.Source;
        canvas.Update(design with { Layers = [second, photo with { Zoom = 3, Frame = new(.1, .2, .5, .6), QuarterTurns = 1, FlipHorizontal = true, Opacity = .4 }] }, photo.Id);
        Assert.Same(secondFrame, layout.Children[0]); Assert.Same(firstFrame, layout.Children[1]);
        Assert.Same(originalSource, firstImage.Source);
        Assert.Equal(90, firstImage.Rotation); Assert.Equal(.4, firstImage.Opacity);
        Assert.Equal(1, firstImage.ScaleX); Assert.Equal(-1, firstImage.ScaleY);
        canvas.Update(design with { Layers = [photo with { Visible = false }, second] }, null);
        Assert.False(firstFrame.IsVisible); Assert.True(secondFrame.IsVisible);
        canvas.Update(design with { Layers = [second] }, second.Id);
        Assert.Same(secondFrame, Assert.Single(layout.Children)); Assert.Null(firstImage.Source);
        canvas.Update(design with { Layers = [] }, null); Assert.Empty(layout.Children);
    }
    public void Dispose() => host.Dispose();
    [Fact]
    public void LockedTopPhotoIsSelectableButDoesNotMoveResizeOrExposeTheLayerBehindIt()
    {
        var bottom = new WidgetLayer(Guid.NewGuid(), "Bottom", Guid.NewGuid().ToString("N"), 640, 480, new(.2, .2, .6, .6));
        var top = bottom with { Id = Guid.NewGuid(), Name = "Locked top", Locked = true }; Guid? selected = null; var writes = 0; var clears = 0;
        var canvas = new WidgetCanvasView(_ => null, id => selected = id, _ => writes++, () => clears++); host.Attach(canvas);
        var design = WidgetDesignRules.New() with { Layers = [bottom, top] }; canvas.Update(design, top.Id);
        ((IView)canvas.PhotoSurface).Arrange(new Rect(0, 0, 170, 170));
        canvas.SelectAt(85, 85); Assert.Equal(top.Id, selected);
        Assert.False(canvas.BeginInteraction(85, 85, false)); Assert.False(canvas.BeginInteraction(85, 85, true));
        Assert.False(canvas.BeginInteraction(34, 34, false)); canvas.Pan(50, 50); canvas.Pinch(2); Assert.Equal(0, writes);
        Assert.Contains("Locked. Unlock", SemanticProperties.GetDescription(canvas));
        canvas.SelectAt(5, 5); Assert.Equal(1, clears); Assert.True(canvas.PhotoSurface.Children.All(x => x is VisualElement { IsVisible: true }));
        canvas.Update(design with { Layers = [bottom, top with { Locked = false }] }, top.Id);
        Assert.True(canvas.BeginInteraction(85, 85, false)); canvas.Pan(17, 0); canvas.EndInteraction(false); Assert.Equal(1, writes);
    }
    [Fact]
    public void BlankTapsClearSelectionButNotDuringManipulationAndALockUpdateEndsTheGesture()
    {
        var photo = new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 640, 480, new(.2, .2, .6, .6));
        var design = WidgetDesignRules.New() with { Layers = [photo] }; var clears = 0; var writes = 0;
        var canvas = new WidgetCanvasView(_ => null, _ => { }, _ => writes++, () => clears++); host.Attach(canvas); canvas.Update(design, photo.Id);
        ((IView)canvas.PhotoSurface).Arrange(new Rect(32, 32, 170, 170));
        canvas.SelectAt(5, 5); Assert.Equal(1, clears); canvas.SelectAt(double.NaN, 5); Assert.Equal(1, clears);
        Assert.True(canvas.BeginInteraction(117, 117, false)); canvas.SelectAt(5, 5); Assert.Equal(1, clears);
        canvas.Update(design with { Layers = [photo with { Locked = true }] }, photo.Id); Assert.False(canvas.IsInteracting);
        canvas.Pan(40, 0); canvas.Pinch(2); Assert.Equal(0, writes);
        canvas.IsEnabled = false; canvas.SelectAt(5, 5); Assert.Equal(1, clears);
        canvas.IsEnabled = true; canvas.Update(design, null);
        Assert.False(Assert.IsType<AbsoluteLayout>(canvas.Workspace.Children[0]).IsVisible);
        Assert.Contains("No photo selected", SemanticProperties.GetDescription(canvas));
    }
    [Fact]
    public void CanvasGestureChangesAreDraftOnlyAndCancellationRestoresSnapshot()
    {
        var photo = new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 640, 480, new(.2, .2, .6, .6));
        var draft = WidgetDesignRules.New() with { Layers = [photo] }; WidgetCanvasView? canvas = null; var events = 0;
        canvas = new WidgetCanvasView(_ => "/private/photo.png", _ => { }, layer =>
        { draft = draft with { Layers = [layer] }; canvas!.Update(draft, layer.Id); });
        host.Attach(canvas); canvas.Update(draft, photo.Id); canvas.InteractionChanged += (_, _) => events++;
        var layout = canvas.PhotoSurface;
        ((IView)layout).Arrange(new Rect(0, 0, 170, 170));
        Assert.True(canvas.BeginInteraction(85, 85, false)); Assert.True(canvas.IsInteracting);
        canvas.Pan(17, -17); Assert.Equal(.3, draft.Layers[0].Frame.X, 8); Assert.Equal(.1, draft.Layers[0].Frame.Y, 8);
        canvas.EndInteraction(true); Assert.False(canvas.IsInteracting); Assert.Equal(photo, draft.Layers[0]); Assert.Equal(2, events);
        Assert.True(canvas.BeginInteraction(85, 85, true)); canvas.Pinch(.5); canvas.EndInteraction(false);
        Assert.Equal(.3, draft.Layers[0].Frame.Width, 8); Assert.Equal(.3, draft.Layers[0].Frame.Height, 8);
        var committed = draft.Layers[0]; canvas.Pan(100, 100); Assert.Equal(committed, draft.Layers[0]);
    }
    [Fact]
    public void TopmostVisiblePhotoWinsTapAndDisablingCanvasBlocksManipulation()
    {
        var first = new WidgetLayer(Guid.NewGuid(), "First", Guid.NewGuid().ToString("N"), 640, 480, new());
        var top = first with { Id = Guid.NewGuid(), Name = "Top" }; Guid? selected = null;
        var design = WidgetDesignRules.New() with { Layers = [first, top] };
        var canvas = new WidgetCanvasView(_ => null, id => selected = id, _ => { }); host.Attach(canvas); canvas.Update(design, null);
        ((IView)canvas.PhotoSurface).Arrange(new Rect(0, 0, 170, 170));
        canvas.SelectAt(85, 85); Assert.Equal(top.Id, selected);
        canvas.Update(design with { Layers = [first, top with { Opacity = 0 }] }, null);
        canvas.SelectAt(85, 85); Assert.Equal(first.Id, selected);
        canvas.IsEnabled = false; selected = null; canvas.SelectAt(85, 85); Assert.Null(selected); Assert.False(canvas.BeginInteraction(85, 85, true));
        canvas.Update(design with { TransparentBackground = false }, null); Assert.False(Checker(canvas).IsVisible);
    }
    private static WidgetCheckerboard Checker(WidgetCanvasView canvas) => Assert.IsType<WidgetCheckerboard>(
        Assert.IsType<Grid>(Assert.IsType<Border>(canvas.Workspace.Children[1]).Content).Children[0]);

    [Fact]
    public void OffCanvasGhostUsesTheSamePlacementAndSourceAndNeverCoversTheWidget()
    {
        var photo = new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 800, 400, new(-.2, .1, 1.2, .6), FitPhoto: true);
        var design = WidgetDesignRules.New() with { Layers = [photo] };
        var canvas = new WidgetCanvasView(_ => "/private/photo.png", _ => { }); host.Attach(canvas); canvas.Update(design, photo.Id);
        ((IView)canvas.Workspace).Arrange(new Rect(5, 7, 260, 260));
        ((IView)canvas.PhotoSurface).Arrange(new Rect(32, 32, 170, 170)); canvas.Update(design, photo.Id);
        var actual = Assert.IsType<AbsoluteLayout>(canvas.PhotoSurface.Children.Single());
        var ghost = Assert.IsType<AbsoluteLayout>(canvas.Workspace.Children[0]);
        Assert.True(canvas.Workspace.IsClippedToBounds); Assert.True(canvas.PhotoSurface.IsClippedToBounds);
        Assert.Equal(.22, ghost.Opacity); Assert.True(ghost.IsVisible);
        Assert.Same(Assert.IsType<Image>(actual.Children[0]).Source, Assert.IsType<Image>(ghost.Children[0]).Source);
        var actualBounds = AbsoluteLayout.GetLayoutBounds(actual); var ghostBounds = AbsoluteLayout.GetLayoutBounds(ghost);
        Assert.Equal(actualBounds.X + canvas.WidgetBounds.X - canvas.Workspace.X, ghostBounds.X, 8);
        Assert.Equal(actualBounds.Y + canvas.WidgetBounds.Y - canvas.Workspace.Y, ghostBounds.Y, 8);
        Assert.Equal(actualBounds.Width, ghostBounds.Width); Assert.Equal(actualBounds.Height, ghostBounds.Height);
        Assert.Equal(AbsoluteLayout.GetLayoutBounds(Assert.IsType<Image>(actual.Children[0])), AbsoluteLayout.GetLayoutBounds(Assert.IsType<Image>(ghost.Children[0])));
        Assert.IsType<Border>(canvas.Workspace.Children[1]); // Actual widget/checkerboard is above the ghost.
        canvas.Update(design with { Layers = [photo with { Visible = false }] }, photo.Id);
        Assert.False(ghost.IsVisible); Assert.Null(Assert.IsType<Image>(ghost.Children[0]).Source);
    }

    [Fact]
    public void WorkspaceOffsetAndTwoFingerTranslationKeepPinchingAndDraggingInWidgetCoordinates()
    {
        var photo = new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 800, 400, new(.2, .2, .4, .4), FitPhoto: true);
        var draft = WidgetDesignRules.New() with { Layers = [photo] }; WidgetCanvasView? canvas = null;
        canvas = new WidgetCanvasView(_ => null, _ => { }, changed => { draft = draft with { Layers = [changed] }; canvas!.Update(draft, changed.Id); });
        host.Attach(canvas); canvas.Update(draft, photo.Id); ((IView)canvas.PhotoSurface).Arrange(new Rect(32, 32, 170, 170));
        Assert.Equal(32, canvas.WidgetBounds.X); Assert.Equal(32, canvas.WidgetBounds.Y);
        Assert.True(canvas.BeginInteraction(100, 100, true)); canvas.Pinch(3, -17, 34); canvas.EndInteraction(false);
        var expected = WidgetLayerEdits.Move(WidgetLayerEdits.Scale(photo, 3, .5, .5), -.1, .2);
        Assert.Equal(expected.Frame.X, draft.Layers[0].Frame.X, 8); Assert.Equal(expected.Frame.Y, draft.Layers[0].Frame.Y, 8);
        Assert.True(draft.Layers[0].Frame.Width > 1); Assert.True(draft.Layers[0].Frame.X < 0);
        var snapshot = draft.Layers[0];
        Assert.True(canvas.BeginInteraction(100, 100, true)); canvas.Pinch(2, 17, 17); canvas.EndInteraction(true);
        Assert.Equal(snapshot, draft.Layers[0]);
    }

    [Fact]
    public void TinyLayerCenterDragsDoNotAccidentallyActivateOverlappingCornerHandles()
    {
        var photo = new WidgetLayer(Guid.NewGuid(), "Small photo", Guid.NewGuid().ToString("N"), 800, 400, new(.4, .4, .1, .1), FitPhoto: true);
        var draft = WidgetDesignRules.New() with { Layers = [photo] }; WidgetCanvasView? canvas = null;
        canvas = new WidgetCanvasView(_ => null, _ => { }, changed => { draft = draft with { Layers = [changed] }; canvas!.Update(draft, changed.Id); });
        host.Attach(canvas); canvas.Update(draft, photo.Id); ((IView)canvas.PhotoSurface).Arrange(new Rect(0, 0, 170, 170));
        Assert.True(canvas.BeginInteraction(76.5, 76.5, false)); canvas.Pan(17, 0); canvas.EndInteraction(false);
        Assert.Equal(.5, draft.Layers[0].Frame.X, 8); Assert.Equal(photo.Frame.Width, draft.Layers[0].Frame.Width);
        Assert.Equal(photo.Frame.Height, draft.Layers[0].Frame.Height);
    }

    [Fact]
    public void OutsidePortionRemainsSelectableAndReleaseDropsGhostAndOriginalSources()
    {
        var photo = new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 800, 400, new(-.1, .2, .4, .4), FitPhoto: true);
        Guid? selected = null;
        var canvas = new WidgetCanvasView(_ => "/private/photo.png", id => selected = id, _ => { }); host.Attach(canvas);
        canvas.Update(WidgetDesignRules.New() with { Layers = [photo] }, photo.Id);
        ((IView)canvas.PhotoSurface).Arrange(new Rect(32, 32, 170, 170));
        canvas.SelectAt(23.5, 100); Assert.Equal(photo.Id, selected);
        canvas.ReleaseSources();
        var ghost = Assert.IsType<AbsoluteLayout>(canvas.Workspace.Children[0]); Assert.False(ghost.IsVisible);
        Assert.Null(Assert.IsType<Image>(ghost.Children[0]).Source);
        Assert.Null(Assert.IsType<Image>(Assert.IsType<AbsoluteLayout>(canvas.PhotoSurface.Children[0]).Children[0]).Source);
    }

    [Fact]
    public void ResizingThePreviewDuringAGestureCancelsTheDraftAndAllowsANewGesture()
    {
        var photo = new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 800, 400, new(.2, .2, .6, .6), FitPhoto: true);
        var draft = WidgetDesignRules.New() with { Layers = [photo] }; WidgetCanvasView? canvas = null;
        canvas = new WidgetCanvasView(_ => null, _ => { }, changed => { draft = draft with { Layers = [changed] }; canvas!.Update(draft, changed.Id); });
        host.Attach(canvas); canvas.Update(draft, photo.Id); ((IView)canvas.PhotoSurface).Arrange(new Rect(0, 0, 170, 170));
        Assert.True(canvas.BeginInteraction(85, 85, true)); canvas.Pinch(2); Assert.NotEqual(photo, draft.Layers[0]);
        ((IView)canvas.PhotoSurface).Arrange(new Rect(0, 0, 150, 150));
        Assert.False(canvas.IsInteracting); Assert.Equal(photo, draft.Layers[0]);
        Assert.True(canvas.BeginInteraction(75, 75, true)); canvas.EndInteraction(false);
    }

    [Theory]
    [InlineData(WidgetSize.Small)] [InlineData(WidgetSize.Medium)] [InlineData(WidgetSize.Large)] [InlineData(WidgetSize.ExtraLargePortrait)]
    public void BorderInsetsDoNotChangeTheWidgetCanvasProportions(WidgetSize size)
    {
        var canvas = new WidgetCanvasView(_ => null, _ => { }); host.Attach(canvas);
        canvas.Update(WidgetDesignRules.New() with { Size = size }, null);
        var outer = Assert.IsType<Border>(canvas.Content);
        outer.CrossPlatformArrange(new Rect(0, 0, canvas.WidthRequest, canvas.HeightRequest));
        ((Microsoft.Maui.ILayout)canvas.Workspace).CrossPlatformArrange(new Rect(0, 0, canvas.Workspace.Width, canvas.Workspace.Height));
        var widget = Assert.IsType<Border>(canvas.Workspace.Children[1]);
        widget.CrossPlatformArrange(new Rect(0, 0, widget.Width, widget.Height));
        var grid = Assert.IsType<Grid>(widget.Content);
        ((Microsoft.Maui.ILayout)grid).CrossPlatformArrange(new Rect(0, 0, grid.Width, grid.Height));
        var expected = WidgetDesignRules.Canvas(size);
        Assert.True(canvas.PhotoSurface.Width > 0 && canvas.PhotoSurface.Height > 0);
        Assert.Equal(expected.Width / expected.Height, canvas.PhotoSurface.Width / canvas.PhotoSurface.Height, 8);
        Assert.Equal(34, canvas.WidgetBounds.X); Assert.Equal(34, canvas.WidgetBounds.Y);
        Assert.Equal(Math.Min(expected.Width, 296), canvas.PhotoSurface.Width, 8);
        Assert.Equal(canvas.PhotoSurface.Width + 68, canvas.WidthRequest, 8);
    }
    [Theory]
    [InlineData(WidgetSize.Small, false)] [InlineData(WidgetSize.Medium, false)] [InlineData(WidgetSize.Large, false)] [InlineData(WidgetSize.ExtraLargePortrait, false)]
    [InlineData(WidgetSize.Small, true)] [InlineData(WidgetSize.Medium, true)] [InlineData(WidgetSize.Large, true)] [InlineData(WidgetSize.ExtraLargePortrait, true)]
    public void EditorPreviewUsesWidthWithoutShrinkingForViewportHeight(WidgetSize family, bool measured)
    {
        var clock = WidgetClocks.New(); var design = WidgetDesignRules.New() with { Size = family, Layers = [clock] };
        if (measured)
        {
            var dimensions = WidgetDesignRules.Canvas(family);
            design = design with { CanvasDimensions = new(dimensions.Width * .9, dimensions.Height * .9) };
        }
        var canvas = new WidgetCanvasView(_ => null, _ => { });
        var viewport = new WidgetCanvasViewport(canvas, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance); host.Attach(viewport);
        canvas.Update(design, clock.Id);
        foreach (var height in new[] { 40d, 100d, 180d, 320d, 700d })
        {
            ((IView)viewport).Arrange(new Rect(0, 0, 300, height));
            var border = Assert.IsType<Border>(canvas.Workspace.Children[1]); var bounds = AbsoluteLayout.GetLayoutBounds(border);
            var expected = WidgetDesignRules.Canvas(design);
            Assert.Equal(expected.Width / expected.Height, (bounds.Width - 2) / (bounds.Height - 2), 8);
            var fit = WidgetPreviewFrame.Fit(design, 296);
            Assert.Equal(fit.Width, bounds.Width - 2, 8); Assert.Equal(fit.Height, bounds.Height - 2, 8);
            Assert.Equal(fit.Width + 68, canvas.WidthRequest, 8); Assert.Equal(fit.Height + 68, canvas.HeightRequest, 8);
            Assert.Equal(design, Assert.IsType<WidgetCompositeView>(Assert.IsType<Grid>(border.Content).Children[2]).Design);
        }
    }
    [Theory]
    [InlineData(WidgetSize.Small, 158, 158)] [InlineData(WidgetSize.Medium, 338, 158)]
    [InlineData(WidgetSize.Large, 338, 354)] [InlineData(WidgetSize.ExtraLargePortrait, 338, 550)]
    public void MeasuredCanvasFitsTheViewportBeforeAddingOutsideWorkspace(WidgetSize family, double width, double height)
    {
        var design = WidgetDesignRules.New() with { Size = family, CanvasDimensions = new(width, height) };
        var canvas = new WidgetCanvasView(_ => null, _ => { });
        var viewport = new WidgetCanvasViewport(canvas, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance); host.Attach(viewport);
        ((IView)viewport).Arrange(new Rect(0, 0, 300, 600)); canvas.Update(design, null);
        var expected = WidgetPreviewFrame.Fit(design, 296);
        Assert.Equal(expected.Width + 68, canvas.WidthRequest, 8); Assert.Equal(expected.Height + 68, canvas.HeightRequest, 8);
        Assert.Same(canvas, viewport.Content); Assert.Equal(ScrollOrientation.Horizontal, viewport.Orientation);
        Assert.Equal(Math.Max(0, (expected.Width + 68 - 300) / 2), WidgetCanvasViewport.CenterOffset(canvas.WidthRequest, 300), 8);
        // Normalized clipping edges still mean the complete measured widget, not the workspace.
        Assert.Equal(width / height, expected.Width / expected.Height, 8);
        var frame = new WidgetPreviewFrame(family, new Grid(), dimensions: design.CanvasDimensions, initialWidth: 200);
        var border = Assert.IsType<Border>(frame.Content);
        Assert.Equal(width / height, border.WidthRequest / border.HeightRequest, 8);
    }
}
