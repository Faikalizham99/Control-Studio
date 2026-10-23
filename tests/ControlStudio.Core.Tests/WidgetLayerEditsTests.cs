using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetLayerEditsTests
{
    private static WidgetLayer Photo() => new(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 800, 400, new(.2, .3, .4, .5));
    [Fact]
    public void LegacyJsonKeepsSolidBackgroundAndUntransformedPhotosWhileNewDesignsAreTransparent()
    {
        var old = JsonSerializer.Deserialize<WidgetCatalog>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-designs-v1.example.json")), WidgetDesignRules.Json)!;
        var design = Assert.Single(old.Designs); var layer = Assert.Single(design.Layers);
        Assert.False(design.TransparentBackground); Assert.Equal(0, layer.QuarterTurns);
        Assert.False(layer.FlipHorizontal); Assert.False(layer.FlipVertical); Assert.Equal(1, layer.Opacity);
        Assert.True(WidgetDesignRules.New().TransparentBackground);
        var updated = design with { TransparentBackground = true, Layers = [layer with { QuarterTurns = 3, FlipHorizontal = true, FlipVertical = true, Opacity = .35 }] };
        var decoded = JsonSerializer.Deserialize(JsonSerializer.Serialize(updated, WidgetDesignRules.JsonContext.WidgetDesign), WidgetDesignRules.JsonContext.WidgetDesign)!;
        Assert.Equal(updated with { Layers = decoded.Layers }, decoded); Assert.Equal(updated.Layers, decoded.Layers);
    }
    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(-.1)] [InlineData(1.1)]
    public void InvalidOpacityCannotBeSaved(double opacity) => Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [Photo() with { Opacity = opacity }] }));
    [Theory]
    [InlineData(-1)] [InlineData(4)]
    public void InvalidRotationCannotBeSaved(int rotation) => Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [Photo() with { QuarterTurns = rotation }] }));
    [Fact]
    public void GesturesClampFramesAndKeepIdentityAndOppositeResizeCorner()
    {
        var photo = Photo(); var moved = WidgetLayerEdits.Move(photo, 9, -9);
        Assert.Equal(.6, moved.Frame.X); Assert.Equal(0, moved.Frame.Y); Assert.Equal(photo.Id, moved.Id);
        Assert.Equal(photo, WidgetLayerEdits.Move(photo, double.NaN, 0));
        Assert.Equal(photo, WidgetLayerEdits.Scale(photo, double.PositiveInfinity));
        Assert.Equal(photo, WidgetLayerEdits.Scale(photo, -1));
        var scaled = WidgetLayerEdits.Scale(photo, .5, 0, 0);
        Assert.Equal(new WidgetFrame(.2, .3, .2, .25), scaled.Frame);
        for (var corner = 0; corner < 4; corner++)
            foreach (var delta in new[] { -9d, -.1, .1, 9d })
            {
                var resized = WidgetLayerEdits.Resize(photo, corner, delta, delta);
                WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [resized] });
                if (corner % 2 == 0) Assert.Equal(photo.Frame.X + photo.Frame.Width, resized.Frame.X + resized.Frame.Width, 8);
                else Assert.Equal(photo.Frame.X, resized.Frame.X, 8);
                if (corner < 2) Assert.Equal(photo.Frame.Y + photo.Frame.Height, resized.Frame.Y + resized.Frame.Height, 8);
                else Assert.Equal(photo.Frame.Y, resized.Frame.Y, 8);
            }
        foreach (var factor in new[] { .0001, .5, 1, 2, 9999 }) WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [WidgetLayerEdits.Scale(photo, factor)] });
    }
    [Fact]
    public void ResetFitsAspectAndRotationWrapsWithoutLosingPhotoIdentity()
    {
        var photo = Photo() with { QuarterTurns = 3, FlipHorizontal = true, FlipVertical = true, Zoom = 4, CropX = 0, CropY = 1, Visible = false, Opacity = .2 };
        Assert.Equal(0, WidgetLayerEdits.Rotate(photo, 1).QuarterTurns); Assert.Equal(2, WidgetLayerEdits.Rotate(photo, -1).QuarterTurns);
        foreach (var size in WidgetDesignRules.SupportedSizes)
        {
            var reset = WidgetLayerEdits.Reset(photo, size); var canvas = WidgetDesignRules.Canvas(size);
            Assert.Equal(2, reset.Frame.Width * canvas.Width / (reset.Frame.Height * canvas.Height), 8);
            Assert.Equal(photo.Id, reset.Id); Assert.Equal(photo.AssetKey, reset.AssetKey); Assert.Equal(photo.Name, reset.Name); Assert.False(reset.Visible);
            Assert.Equal(0, reset.QuarterTurns); Assert.False(reset.FlipHorizontal); Assert.False(reset.FlipVertical); Assert.Equal(1, reset.Opacity);
            WidgetDesignRules.Validate(WidgetDesignRules.New() with { Size = size, Layers = [reset] });
        }
    }
    [Fact]
    public void RotatedCoverGeometryKeepsAspectAndCoversFrameAtEveryCropExtreme()
    {
        foreach (var size in WidgetDesignRules.SupportedSizes)
            for (var turn = 0; turn < 4; turn++)
                foreach (var crop in new[] { 0d, .5, 1d })
                {
                    var bounds = WidgetDesignRules.Canvas(size);
                    var layer = Photo() with { QuarterTurns = turn, Zoom = 2, CropX = crop, CropY = crop };
                    var image = WidgetPhotoGeometry.Image(layer, bounds.Width, bounds.Height);
                    Assert.Equal(turn % 2 == 0 ? 2 : .5, image.Width / image.Height, 8);
                    Assert.True(image.X <= 0 && image.Y <= 0);
                    Assert.True(image.X + image.Width >= bounds.Width - .000001); Assert.True(image.Y + image.Height >= bounds.Height - .000001);
                }
    }
    [Fact]
    public void ReorderingKeepsStableLayersAndInvisibleContentCannotInterceptTouches()
    {
        var first = Photo(); var second = Photo(); var third = Photo(); WidgetLayer[] layers = [first, second, third];
        Assert.Equal(new[] { second, third, first }, WidgetLayerEdits.Reorder(layers, first.Id, third.Id));
        Assert.Equal(new[] { third, first, second }, WidgetLayerEdits.Reorder(layers, third.Id, first.Id));
        Assert.Same(layers, WidgetLayerEdits.Reorder(layers, Guid.NewGuid(), first.Id));
        Assert.Same(layers, WidgetLayerEdits.Reorder(layers, first.Id, first.Id));
        Assert.True(WidgetLayerEdits.Contains(first, .3, .4)); Assert.False(WidgetLayerEdits.Contains(first, .1, .1));
        Assert.False(WidgetLayerEdits.Contains(first with { Visible = false }, .3, .4)); Assert.False(WidgetLayerEdits.Contains(first with { Opacity = 0 }, .3, .4));
    }
}
