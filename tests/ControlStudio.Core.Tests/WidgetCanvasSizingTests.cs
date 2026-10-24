using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetCanvasSizingTests
{
    [Theory]
    [InlineData(393, 852, 158, 338, 158, 354)]
    [InlineData(430, 932, 170, 364, 170, 382)]
    [InlineData(414, 736, 159, 348, 157, 357)]
    [InlineData(375, 667, 148, 321, 148, 324)]
    public void PublishedExamplesAreScreenSpecificAndOrientationIndependent(int width, int height, int small, int wide, int mediumHeight, int largeHeight)
    {
        Assert.Equal(new(small, small), WidgetCanvasSizes.Estimate(WidgetSize.Small, width, height).Canvas);
        Assert.Equal(new(wide, mediumHeight), WidgetCanvasSizes.Estimate(WidgetSize.Medium, width, height).Canvas);
        Assert.Equal(new(wide, largeHeight), WidgetCanvasSizes.Estimate(WidgetSize.Large, height, width).Canvas);
        Assert.False(WidgetCanvasSizes.Estimate(WidgetSize.ExtraLargePortrait, width, height).Measured);
    }
    [Fact]
    public void RuntimeContractMatchesOnlyTheSameFamilyScreenScaleAndOS()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "widget-display-measurement-v1.example.json"));
        var value = JsonSerializer.Deserialize(bytes, WidgetDesignRules.JsonContext.WidgetDisplayMeasurement)!;
        Assert.True(WidgetCanvasSizes.Matches(value, WidgetSize.Medium, 852, 393, 3, 27));
        foreach (var wrong in new[] { value with { SchemaVersion = 2 }, value with { Size = WidgetSize.Large }, value with { ScreenWidth = 375 },
            value with { ScreenHeight = 844 }, value with { ScreenScale = 2 }, value with { OsMajor = 28 }, value with { Width = double.NaN },
            value with { Height = double.PositiveInfinity }, value with { Height = 0 }, value with { Width = 700 }, value with { Width = 100 } })
            Assert.False(WidgetCanvasSizes.Matches(wrong, WidgetSize.Medium, 393, 852, 3, 27));
        var standby = value with { Size = WidgetSize.Small, Width = 350, Height = 350 };
        Assert.False(WidgetCanvasSizes.Matches(standby, WidgetSize.Small, 393, 852, 3, 27));
    }
    [Fact]
    public void OldDesignsRetainReferenceGeometryAndNewDimensionsSurviveStrictRoundTrip()
    {
        var design = WidgetDesignRules.New() with { Size = WidgetSize.Medium };
        Assert.Equal((364d, 170d), WidgetDesignRules.Canvas(design));
        var sized = design with { CanvasDimensions = new(338, 158) }; WidgetDesignRules.Validate(sized);
        var json = JsonSerializer.Serialize(sized, WidgetDesignRules.JsonContext.WidgetDesign);
        var restored = JsonSerializer.Deserialize(json, WidgetDesignRules.JsonContext.WidgetDesign)!;
        Assert.Equal(sized.CanvasDimensions, restored.CanvasDimensions); Assert.Equal(sized.Id, restored.Id); Assert.Equal(sized.Layers, restored.Layers);
        Assert.Equal((338d, 158d), WidgetDesignRules.Canvas(sized)); Assert.Null(design.CanvasDimensions);
        foreach (var bad in new[] { new WidgetCanvasSize(1, 1), new(338, 0), new(double.NaN, 158), new(338, double.PositiveInfinity), new(338, 1000) })
            Assert.Throws<IOException>(() => WidgetDesignRules.Validate(sized with { CanvasDimensions = bad }));
    }
    [Theory]
    [InlineData(WidgetSize.Small, 158, 158)] [InlineData(WidgetSize.Medium, 338, 158)]
    [InlineData(WidgetSize.Large, 338, 354)] [InlineData(WidgetSize.ExtraLargePortrait, 338, 550)]
    public void ResetAndQuarterTurnUseTheSavedCanvasRatherThanReferenceProportions(WidgetSize size, double width, double height)
    {
        var dimensions = new WidgetCanvasSize(width, height);
        var layer = new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 600, 400, new(), BlendMode: WidgetBlendMode.Multiply);
        var reset = WidgetLayerEdits.Reset(layer, size, dimensions);
        Assert.Equal(1.5, reset.Frame.Width * width / (reset.Frame.Height * height), 8);
        var turned = WidgetLayerEdits.Rotate(reset, 1, size, dimensions);
        Assert.Equal(reset.Frame.Width * width, turned.Frame.Height * height, 8);
        Assert.Equal(reset.Frame.Height * height, turned.Frame.Width * width, 8);
    }
    [Fact]
    public void UnknownAndInvalidScreensRemainBoundedExplicitEstimates()
    {
        foreach (var screen in new[] { (402d, 874d), (double.NaN, 0d), (0d, 0d), (double.PositiveInfinity, 852d) })
            foreach (var family in WidgetDesignRules.SupportedSizes)
            {
                var sizing = WidgetCanvasSizes.Estimate(family, screen.Item1, screen.Item2);
                Assert.False(sizing.Measured); Assert.True(WidgetCanvasSizes.Valid(family, sizing.Canvas));
            }
    }
}
