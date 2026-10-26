using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetRotationTests
{
    private static WidgetLayer Photo() => new(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 640, 480, new(.2, .3, .4, .2));
    [Theory]
    [InlineData(WidgetSize.Small)] [InlineData(WidgetSize.Medium)] [InlineData(WidgetSize.Large)] [InlineData(WidgetSize.ExtraLargePortrait)]
    public void AllKindsKeepTheirFrameAndOptionsAndRoundTripArbitraryAngles(WidgetSize size)
    {
        foreach (var source in new[] { Photo() with { QuarterTurns = 1, FlipHorizontal = true, FitPhoto = true }, WidgetClocks.New(), WidgetDates.New(), WidgetTexts.New() })
        {
            var rotated = WidgetLayerRotation.Set(source, 387.5); Assert.Equal(27.5, rotated.RotationDegrees); Assert.Equal(source with { RotationDegrees = 27.5 }, rotated);
            var design = WidgetDesignRules.New() with { Size = size, Layers = [rotated] }; WidgetDesignRules.Validate(design);
            var json = JsonSerializer.Serialize(design, WidgetDesignRules.JsonContext.WidgetDesign);
            Assert.Equal(rotated, JsonSerializer.Deserialize(json, WidgetDesignRules.JsonContext.WidgetDesign)!.Layers.Single());
            var copy = WidgetLayerEdits.Duplicate(design, rotated.Id).Copy; Assert.Equal(27.5, copy.RotationDegrees);
            Assert.Equal(0, WidgetLayerEdits.Reset(rotated, size).RotationDegrees);
            var locked = rotated with { Locked = true }; Assert.Same(locked, WidgetLayerRotation.Set(locked, 0));
            Assert.Same(rotated, WidgetLayerRotation.Set(rotated, double.NaN)); Assert.Same(rotated, WidgetLayerRotation.Set(rotated, double.PositiveInfinity));
        }
    }
    [Fact]
    public void ZeroAnglePreservesOldSerializationAndInvalidSavedAnglesAreRejected()
    {
        var source = Photo(); var json = JsonSerializer.Serialize(source, WidgetDesignRules.Json);
        Assert.DoesNotContain("rotationDegrees", json); Assert.Equal(0, JsonSerializer.Deserialize<WidgetLayer>(json, WidgetDesignRules.Json)!.RotationDegrees);
        foreach (var angle in new[] { -180.1, 180.1, double.NaN, double.PositiveInfinity })
            Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [source with { RotationDegrees = angle }] }));
        Assert.Equal(-179, WidgetLayerRotation.Normalize(181)); Assert.Equal(179, WidgetLayerRotation.Normalize(-181)); Assert.Equal(180, WidgetLayerRotation.Normalize(180));
        Assert.Equal(-180, WidgetLayerRotation.Normalize(-180)); Assert.Equal(0, WidgetLayerRotation.Normalize(720));
    }
    [Theory]
    [InlineData(170, 170)] [InlineData(338, 158)] [InlineData(338, 354)] [InlineData(338, 550)]
    public void HitTestingUsesTheRotatedFrameInPointSpaceAndIgnoresEmptyBoundingBoxCorners(double width, double height)
    {
        var layer = Photo() with { RotationDegrees = 45, Frame = new(.25, .3, .5, .15) };
        var cx = (layer.Frame.X + layer.Frame.Width / 2) * width; var cy = (layer.Frame.Y + layer.Frame.Height / 2) * height;
        var inside = WidgetLayerRotation.Point(cx + layer.Frame.Width * width * .45, cy, cx, cy, 45);
        Assert.True(WidgetLayerEdits.Contains(layer, inside.X / width, inside.Y / height, width, height));
        var outside = WidgetLayerRotation.Point(cx, cy + layer.Frame.Height * height, cx, cy, 45);
        Assert.False(WidgetLayerEdits.Contains(layer, outside.X / width, outside.Y / height, width, height));
        Assert.False(WidgetLayerEdits.Contains(layer with { Visible = false }, cx / width, cy / height, width, height));
        Assert.False(WidgetLayerEdits.Contains(layer with { Opacity = 0 }, cx / width, cy / height, width, height));
    }
    [Theory]
    [InlineData(170, 170, 35)] [InlineData(338, 158, -70)] [InlineData(338, 354, 110)] [InlineData(338, 550, -145)]
    public void RotatedCornerResizeAndFocalPinchKeepTheOppositeAnchorStill(double width, double height, double degrees)
    {
        var layer = Photo() with { RotationDegrees = degrees };
        (double X, double Y) Anchor(WidgetLayer value, double ax, double ay)
        {
            var f = value.Frame;
            return WidgetLayerRotation.Point((f.X + ax * f.Width) * width, (f.Y + ay * f.Height) * height,
                (f.X + f.Width / 2) * width, (f.Y + f.Height / 2) * height, value.RotationDegrees);
        }
        foreach (var corner in Enumerable.Range(0, 4))
        {
            var ax = corner % 2 == 0 ? 1 : 0; var ay = corner < 2 ? 1 : 0; var before = Anchor(layer, ax, ay);
            var delta = WidgetLayerRotation.Vector(corner % 2 == 0 ? -4 : 4, corner < 2 ? -4 : 4, degrees);
            var resized = WidgetLayerRotation.Resize(layer, corner, delta.X, delta.Y, width, height); var after = Anchor(resized, ax, ay);
            Assert.Equal(before.X, after.X, 8); Assert.Equal(before.Y, after.Y, 8); Assert.Equal(degrees, resized.RotationDegrees);
        }
        var focal = Anchor(layer, .2, .8); var pinched = WidgetLayerRotation.Scale(layer, 1.4, .2, .8, width, height); var retained = Anchor(pinched, .2, .8);
        Assert.Equal(focal.X, retained.X, 8); Assert.Equal(focal.Y, retained.Y, 8);
        var locked = layer with { Locked = true }; Assert.Same(locked, WidgetLayerRotation.Resize(locked, 0, 5, 5, width, height));
    }
    [Fact]
    public void RotatedClockAndDateScenesKeepTheirExistingSchemasAndAngles()
    {
        foreach (var layer in new[] { WidgetClocks.New(), WidgetDates.New() })
        {
            var rotated = layer with { RotationDegrees = -23.75 }; var design = WidgetDesignRules.New() with { Layers = [rotated] };
            var scene = WidgetScenes.Manifest(design); WidgetScenes.Validate(scene, design);
            Assert.Equal(layer.Kind == WidgetLayerKind.Clock ? 1 : 2, scene.SchemaVersion);
            var decoded = JsonSerializer.Deserialize(JsonSerializer.Serialize(scene, WidgetDesignRules.JsonContext.WidgetScene), WidgetDesignRules.JsonContext.WidgetScene)!;
            Assert.Equal(rotated, decoded.Parts[0].Clock ?? decoded.Parts[0].Date);
        }
    }
}
