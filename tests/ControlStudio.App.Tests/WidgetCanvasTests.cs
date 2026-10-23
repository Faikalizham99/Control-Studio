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
        var root = Assert.IsType<Grid>(Assert.IsType<Border>(canvas.Content).Content);
        Assert.True(Assert.IsType<WidgetCheckerboard>(root.Children[0]).IsVisible);
        var layout = Assert.IsType<AbsoluteLayout>(root.Children[1]);
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
        var root = Assert.IsType<Grid>(Assert.IsType<Border>(canvas.Content).Content);
        var layout = Assert.IsType<AbsoluteLayout>(root.Children[1]);
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
        var root = Assert.IsType<Grid>(Assert.IsType<Border>(canvas.Content).Content);
        ((IView)Assert.IsType<AbsoluteLayout>(root.Children[1])).Arrange(new Rect(0, 0, 170, 170));
        canvas.SelectAt(85, 85); Assert.Equal(top.Id, selected);
        canvas.Update(design with { Layers = [first, top with { Opacity = 0 }] }, null);
        canvas.SelectAt(85, 85); Assert.Equal(first.Id, selected);
        canvas.IsEnabled = false; selected = null; canvas.SelectAt(85, 85); Assert.Null(selected); Assert.False(canvas.BeginInteraction(85, 85, true));
        canvas.Update(design with { TransparentBackground = false }, null); Assert.False(Assert.IsType<WidgetCheckerboard>(root.Children[0]).IsVisible);
    }
}
