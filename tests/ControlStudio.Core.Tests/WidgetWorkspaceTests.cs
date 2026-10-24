using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetWorkspaceTests
{
    private static WidgetLayer Photo() => new(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 800, 400, new(.1, .2, .6, .4), FitPhoto: true);

    [Fact]
    public void MovingOutsideAnyEdgeIsReversibleWithoutChangingThePhotoOrCrop()
    {
        var photo = Photo() with { Zoom = 2, CropX = .3, CropY = .7, QuarterTurns = 1, FlipHorizontal = true };
        foreach (var (dx, dy) in new[] { (-1d, 0d), (1d, 0d), (0d, -1d), (0d, 1d) })
        {
            var moved = WidgetLayerEdits.Move(photo, dx, dy);
            WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [moved] });
            Assert.Equal(photo with { Frame = moved.Frame }, moved);
            var back = WidgetLayerEdits.Move(moved, -dx, -dy);
            Assert.Equal(photo.Frame.X, back.Frame.X, 10); Assert.Equal(photo.Frame.Y, back.Frame.Y, 10);
            Assert.Equal(photo.Frame.Width, back.Frame.Width); Assert.Equal(photo.Frame.Height, back.Frame.Height);
        }
    }

    [Fact]
    public void PinchingPastWidgetBoundsKeepsAspectAndTheGestureAnchor()
    {
        var photo = Photo(); var enlarged = WidgetLayerEdits.Scale(photo, 3, .7, .4);
        Assert.True(enlarged.Frame.Width > 1); Assert.True(enlarged.Frame.Height > 1); Assert.True(enlarged.Frame.X < 0);
        Assert.Equal(photo.Frame.Width / photo.Frame.Height, enlarged.Frame.Width / enlarged.Frame.Height, 10);
        Assert.Equal(photo.Frame.X + photo.Frame.Width * .7, enlarged.Frame.X + enlarged.Frame.Width * .7, 10);
        Assert.Equal(photo.Frame.Y + photo.Frame.Height * .4, enlarged.Frame.Y + enlarged.Frame.Height * .4, 10);
        var restored = WidgetLayerEdits.Scale(enlarged, 1d / 3, .7, .4);
        Assert.Equal(photo.Frame.X, restored.Frame.X, 10); Assert.Equal(photo.Frame.Y, restored.Frame.Y, 10);
        WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [enlarged] });
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void CornerResizingPreservesProportionsAndTheOppositeCorner(int corner)
    {
        var photo = Photo(); var resized = WidgetLayerEdits.Resize(photo, corner, corner % 2 == 0 ? -.7 : .7, corner < 2 ? -.2 : .2);
        Assert.Equal(photo.Frame.Width / photo.Frame.Height, resized.Frame.Width / resized.Frame.Height, 10);
        Assert.Equal(photo.Frame.X + (corner % 2 == 0 ? photo.Frame.Width : 0), resized.Frame.X + (corner % 2 == 0 ? resized.Frame.Width : 0), 10);
        Assert.Equal(photo.Frame.Y + (corner < 2 ? photo.Frame.Height : 0), resized.Frame.Y + (corner < 2 ? resized.Frame.Height : 0), 10);
    }

    [Theory]
    [InlineData(WidgetSize.Small)] [InlineData(WidgetSize.Medium)] [InlineData(WidgetSize.Large)] [InlineData(WidgetSize.ExtraLargePortrait)]
    public void FullPhotoIsNeverInternallyCroppedAcrossRotationSizesAndLegacyCropValues(WidgetSize size)
    {
        var bounds = WidgetDesignRules.Canvas(size);
        for (var turn = 0; turn < 4; turn++)
        {
            var layer = Photo() with { QuarterTurns = turn, Zoom = 4, CropX = 0, CropY = 1 };
            var rect = WidgetPhotoGeometry.Image(layer, bounds.Width, bounds.Height);
            Assert.True(rect.X >= 0 && rect.Y >= 0);
            Assert.True(rect.X + rect.Width <= bounds.Width + 1e-8); Assert.True(rect.Y + rect.Height <= bounds.Height + 1e-8);
            Assert.Equal(turn % 2 == 0 ? 2 : .5, rect.Width / rect.Height, 10);
            Assert.Equal((bounds.Width - rect.Width) / 2, rect.X, 10);
            Assert.Equal((bounds.Height - rect.Height) / 2, rect.Y, 10);
            Assert.Equal(rect, WidgetPhotoGeometry.Image(layer with { Zoom = 1, CropX = 1, CropY = 0 }, bounds.Width, bounds.Height));
        }
    }

    [Theory]
    [InlineData(WidgetSize.Small)] [InlineData(WidgetSize.Medium)] [InlineData(WidgetSize.Large)] [InlineData(WidgetSize.ExtraLargePortrait)]
    public void FullPhotoQuarterTurnsSwapPhysicalDimensionsAroundItsCenter(WidgetSize size)
    {
        var bounds = WidgetDesignRules.Canvas(size); var photo = WidgetLayerEdits.Reset(Photo(), size);
        var turned = WidgetLayerEdits.Rotate(photo, 1, size);
        Assert.Equal(photo.Frame.Width * bounds.Width, turned.Frame.Height * bounds.Height, 9);
        Assert.Equal(photo.Frame.Height * bounds.Height, turned.Frame.Width * bounds.Width, 9);
        Assert.Equal(photo.Frame.X + photo.Frame.Width / 2, turned.Frame.X + turned.Frame.Width / 2, 10);
        Assert.Equal(photo.Frame.Y + photo.Frame.Height / 2, turned.Frame.Y + turned.Frame.Height / 2, 10);
        Assert.True(turned.FitPhoto); WidgetDesignRules.Validate(WidgetDesignRules.New() with { Size = size, Layers = [turned] });
    }

    [Fact]
    public void CenterRecoversEvenCompletelyOffScreenPhotosWithoutResettingOtherEdits()
    {
        var photo = Photo() with { Frame = new(4, -4, 3, 2), QuarterTurns = 1, Zoom = 2, CropX = .3, Opacity = .4, FlipHorizontal = true };
        var centered = WidgetLayerEdits.Center(photo);
        Assert.Equal(new WidgetFrame(-1, -.5, 3, 2), centered.Frame); Assert.Equal(photo with { Frame = centered.Frame }, centered);
    }

    [Fact]
    public void WorkspaceBoundsStayFiniteAndRejectUnboundedCatalogValues()
    {
        var photo = Photo();
        foreach (var factor in new[] { .00001, 1, 3, 1e30 })
            foreach (var offset in new[] { -1e30, -1d, 0, 1, 1e30 })
            {
                var changed = WidgetLayerEdits.Move(WidgetLayerEdits.Scale(photo, factor), offset, -offset);
                WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [changed] });
                Assert.Equal(photo.Frame.Width / photo.Frame.Height, changed.Frame.Width / changed.Frame.Height, 9);
            }
        foreach (var frame in new[] { new WidgetFrame(double.NaN, 0, 1, 1), new(0, double.PositiveInfinity), new(4.001, 0), new(0, -4.001), new(0, 0, 4.001), new(0, 0, .049) })
            Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [photo with { Frame = frame }] }));
    }

    [Fact]
    public void LegacyCropIsPreservedAndNewOffCanvasFullPhotoRoundTrips()
    {
        var old = JsonSerializer.Deserialize<WidgetCatalog>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-designs-v1.example.json")), WidgetDesignRules.Json)!;
        Assert.False(Assert.Single(Assert.Single(old.Designs).Layers).FitPhoto);
        Assert.False(Assert.Single(old.Designs).PreserveCanvas);
        var layer = Photo() with { Frame = new(-.6, 1.2, 2.4, 1.6) };
        var design = WidgetDesignRules.New() with { Layers = [layer] };
        var roundTrip = JsonSerializer.Deserialize(JsonSerializer.Serialize(design, WidgetDesignRules.JsonContext.WidgetDesign), WidgetDesignRules.JsonContext.WidgetDesign)!;
        Assert.Equal(layer, Assert.Single(roundTrip.Layers)); WidgetDesignRules.Validate(roundTrip);
        Assert.True(roundTrip.PreserveCanvas);
        Assert.True(WidgetLayerEdits.Reset(layer with { FitPhoto = false }, WidgetSize.Small).FitPhoto);
    }
}
