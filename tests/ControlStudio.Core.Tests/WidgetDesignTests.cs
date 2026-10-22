using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetDesignTests
{
    private static WidgetLayer Photo() => new(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 200, 100, new());
    [Fact]
    public void SharedContractRoundTripsAllLayerPropertiesAndFamilies()
    {
        var catalog = JsonSerializer.Deserialize<WidgetCatalog>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-designs-v1.example.json")), WidgetDesignRules.Json)!;
        WidgetDesignRules.Validate(catalog);
        var design = Assert.Single(catalog.Designs); var layer = Assert.Single(design.Layers);
        Assert.Equal(WidgetSize.ExtraLarge, design.Size); Assert.Equal(.7, layer.CropY); Assert.Equal(1.5, layer.Zoom);
        foreach (var size in Enum.GetValues<WidgetSize>())
        {
            var value = design with { Size = size }; WidgetDesignRules.Validate(value);
            var roundTrip = JsonSerializer.Deserialize<WidgetDesign>(JsonSerializer.Serialize(value, WidgetDesignRules.Json), WidgetDesignRules.Json)!;
            Assert.Equal(size, roundTrip.Size); Assert.Equal(layer, Assert.Single(roundTrip.Layers));
        }
    }
    [Fact]
    public void ExtraLargePortraitIsDistinctAndLandscapeContractsStayCompatible()
    {
        var landscape = WidgetDesignRules.New() with { Size = WidgetSize.ExtraLarge };
        var json = JsonSerializer.Serialize(landscape, WidgetDesignRules.Json);
        Assert.Contains("\"size\":\"extraLarge\"", json);
        var portrait = landscape with { Size = WidgetSize.ExtraLargePortrait };
        var portraitJson = JsonSerializer.Serialize(portrait, WidgetDesignRules.Json);
        Assert.Contains("\"size\":\"extraLargePortrait\"", portraitJson);
        Assert.Equal(WidgetSize.ExtraLargePortrait, JsonSerializer.Deserialize<WidgetDesign>(portraitJson, WidgetDesignRules.Json)!.Size);
        var wide = WidgetDesignRules.Canvas(landscape.Size); var tall = WidgetDesignRules.Canvas(portrait.Size);
        Assert.True(wide.Width > wide.Height); Assert.True(tall.Height > tall.Width);
        Assert.Equal(WidgetDesignRules.Canvas(WidgetSize.Large).Width, tall.Width);
        Assert.Contains("27+", WidgetDesignRules.SizeName(portrait.Size));
    }
    [Fact]
    public void PhotosAreOptionalAndBoundedAndInvalidLayerKindsAreNotSilentlyDiscarded()
    {
        var design = WidgetDesignRules.New(); WidgetDesignRules.Validate(design);
        WidgetDesignRules.Validate(design with { Layers = Enumerable.Range(0, 8).Select(_ => Photo()).ToArray() });
        Assert.Throws<IOException>(() => WidgetDesignRules.Validate(design with { Layers = Enumerable.Range(0, 9).Select(_ => Photo()).ToArray() }));
        var json = JsonSerializer.Serialize(design with { Layers = [Photo()] }, WidgetDesignRules.Json);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WidgetDesign>(json.Replace("\"photo\"", "\"clock\""), WidgetDesignRules.Json));
        Assert.Throws<IOException>(() => WidgetDesignRules.Validate(new WidgetCatalog(2, [design])));
    }
    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(-.1)] [InlineData(1.1)]
    public void InvalidGeometryIsRejectedBeforeSaving(double value)
    {
        var layer = Photo(); var design = WidgetDesignRules.New();
        Assert.Throws<IOException>(() => WidgetDesignRules.Validate(design with { Layers = [layer with { Frame = new(value, 0, .5, .5) }] }));
    }
    [Fact]
    public void BoundsZoomKeysAndUniqueIdsAreValidated()
    {
        var layer = Photo(); var design = WidgetDesignRules.New();
        foreach (var invalid in new[] { layer with { Frame = new(.5, 0, 1, 1) }, layer with { Zoom = 5 }, layer with { CropY = -1 }, layer with { AssetKey = "../private" }, layer with { PixelWidth = 4096 } })
            Assert.Throws<IOException>(() => WidgetDesignRules.Validate(design with { Layers = [invalid] }));
        Assert.Throws<IOException>(() => WidgetDesignRules.Validate(design with { Layers = [layer, layer] }));
        Assert.Throws<IOException>(() => WidgetDesignRules.Validate(new WidgetCatalog(1, [design, design])));
    }
    [Fact]
    public void PhotoFillAndCropStayConsistentAcrossAllCanvasSizes()
    {
        var photo = Photo();
        foreach (var size in Enum.GetValues<WidgetSize>())
        {
            var canvas = WidgetDesignRules.Canvas(size);
            foreach (var zoom in new[] { 1d, 2d, 4d })
                foreach (var crop in new[] { 0d, .5d, 1d })
                {
                    var rect = WidgetPhotoGeometry.Image(photo with { Zoom = zoom, CropX = crop, CropY = crop }, canvas.Width, canvas.Height);
                    Assert.True(rect.X <= 0 && rect.Y <= 0);
                    Assert.True(rect.X + rect.Width >= canvas.Width - .0001); Assert.True(rect.Y + rect.Height >= canvas.Height - .0001);
                    Assert.Equal(2, rect.Width / rect.Height, 8);
                }
        }
        Assert.Equal(new WidgetImageRect(-50, 0, 200, 100), WidgetPhotoGeometry.Image(photo, 100, 100));
        Assert.Equal(default, WidgetPhotoGeometry.Image(photo, 0, 100));
    }
}
