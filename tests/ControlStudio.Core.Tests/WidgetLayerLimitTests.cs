using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetLayerLimitTests
{
    public static WidgetLayer Layer(int index) => ((index % 4) switch
    {
        0 => new WidgetLayer(Guid.NewGuid(), "Photo " + index, Guid.NewGuid().ToString("N"), 1, 1, new()),
        1 => WidgetClocks.New("Clock " + index), 2 => WidgetDates.New("Date " + index), _ => WidgetTexts.New("Text " + index)
    }) with { RotationDegrees = index - 15 };
    [Theory]
    [InlineData(WidgetSize.Small)] [InlineData(WidgetSize.Medium)] [InlineData(WidgetSize.Large)] [InlineData(WidgetSize.ExtraLargePortrait)]
    public void ThirtyMixedLayersKeepOrderAndOptionsWhileTheThirtyFirstIsRejected(WidgetSize family)
    {
        var layers = Enumerable.Range(0, 30).Select(Layer).ToArray(); var design = WidgetDesignRules.New() with { Size = family, Layers = layers };
        WidgetDesignRules.Validate(design); var scene = WidgetScenes.Manifest(design); WidgetScenes.Validate(scene, design);
        var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(design, WidgetDesignRules.JsonContext.WidgetDesign), WidgetDesignRules.JsonContext.WidgetDesign)!;
        Assert.Equal(layers, restored.Layers); Assert.Equal(30, WidgetScenes.Steps(design).Sum(step => step.Photos.Count + (step.TextLayer is null ? 0 : 1)));
        Assert.Throws<IOException>(() => WidgetDesignRules.Validate(design with { Layers = layers.Append(Layer(30)).ToArray() }));
        Assert.Throws<IOException>(() => WidgetLayerEdits.Duplicate(design, layers[0].Id));
    }
    [Fact]
    public void ThirtyLiveLayersFitTheExistingManifestBudgetWithoutLiftingImageLimits()
    {
        var layers = Enumerable.Range(0, 30).Select(index => (index % 2 == 0 ? WidgetClocks.New() : WidgetDates.New()) with
        {
            Name = new string('界', 37) + index.ToString("00"), RotationDegrees = -179.123456789,
            Frame = new(-.123456789012345, 1.123456789012345, .123456789012345, .987654321012345), Opacity = .87654321012345
        }).ToArray();
        var design = WidgetDesignRules.New() with { Layers = layers }; WidgetDesignRules.Validate(design);
        var scene = WidgetScenes.Manifest(design); Assert.Equal(30, scene.Parts.Count); WidgetScenes.Validate(scene, design);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(scene, WidgetDesignRules.JsonContext.WidgetScene).Length <= WidgetScenes.MaximumBytes);
        Assert.Equal(3_000_000, WidgetScenes.MaximumPixels); Assert.Equal(4_000_000, WidgetScenes.MaximumImageBytes);
    }
}
