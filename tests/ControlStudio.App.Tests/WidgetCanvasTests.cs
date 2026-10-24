using ControlStudio.App.Views;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class WidgetCanvasTests : IDisposable
{
    private readonly MauiTestContext host = new();
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
        Assert.True(canvas.WidthRequest <= 300);
    }
}
