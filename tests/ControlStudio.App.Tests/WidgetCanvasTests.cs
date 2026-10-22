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
        var layout = Assert.IsType<AbsoluteLayout>(Assert.IsType<Border>(canvas.Content).Content);
        var firstFrame = Assert.IsType<AbsoluteLayout>(layout.Children[0]);
        var firstImage = Assert.IsType<Image>(firstFrame.Children[0]);
        var secondFrame = Assert.IsType<AbsoluteLayout>(layout.Children[1]);
        var originalSource = firstImage.Source;
        canvas.Update(design with { Layers = [second, photo with { Zoom = 3, Frame = new(.1, .2, .5, .6) }] }, photo.Id);
        Assert.Same(secondFrame, layout.Children[0]); Assert.Same(firstFrame, layout.Children[1]);
        Assert.Same(originalSource, firstImage.Source);
        canvas.Update(design with { Layers = [photo with { Visible = false }, second] }, null);
        Assert.False(firstFrame.IsVisible); Assert.True(secondFrame.IsVisible);
        canvas.Update(design with { Layers = [second] }, second.Id);
        Assert.Same(secondFrame, Assert.Single(layout.Children)); Assert.Null(firstImage.Source);
        canvas.Update(design with { Layers = [] }, null); Assert.Empty(layout.Children);
    }
    public void Dispose() => host.Dispose();
}
