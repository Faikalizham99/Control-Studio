using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetGradientTests
{
    [Fact]
    public void NativeGradientFixtureKeepsLiveClockAndDateTextAndTheirDirections()
    {
        var scene = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-gradient-v1.example.json")), WidgetDesignRules.JsonContext.WidgetScene)!;
        var design = WidgetDesignRules.New() with { Layers = [scene.Parts[0].Clock!, scene.Parts[1].Date!] }; WidgetScenes.Validate(scene, design);
        Assert.Equal(WidgetGradientDirection.Horizontal, scene.Parts[0].Clock!.Clock!.Gradient!.Direction);
        Assert.Equal(WidgetGradientDirection.Diagonal, scene.Parts[1].Date!.Date!.Gradient!.Direction);
        Assert.True(scene.Parts[0].Clock!.Clock!.ShowSeconds); Assert.True(scene.Parts[1].Date!.Date!.ShowWeekday);
    }
    [Theory]
    [InlineData(WidgetLayerKind.Clock)] [InlineData(WidgetLayerKind.Date)] [InlineData(WidgetLayerKind.Text)]
    public void AllTextKindsRetainThreeColoursAndDirectionThroughStyleEditsCopiesAndJson(WidgetLayerKind kind)
    {
        foreach (var direction in Enum.GetValues<WidgetGradientDirection>())
        foreach (var enabled in new[] { true, false })
        {
            var original = kind switch { WidgetLayerKind.Clock => WidgetClocks.New(), WidgetLayerKind.Date => WidgetDates.New(), _ => WidgetTexts.New() };
            var style = WidgetTextLayers.Style(original) with { ColourHex = "#123456", Gradient = new(enabled, "#FF0000", "#00FF00", "#0000FF", direction) };
            var layer = WidgetTextLayers.WithStyle(original, style); var design = WidgetDesignRules.New() with { Layers = [layer] }; WidgetDesignRules.Validate(design);
            var decoded = JsonSerializer.Deserialize(JsonSerializer.Serialize(design, WidgetDesignRules.JsonContext.WidgetDesign), WidgetDesignRules.JsonContext.WidgetDesign)!;
            Assert.Equal(layer, decoded.Layers.Single()); Assert.Equal(style, WidgetTextLayers.Style(layer));
            var edited = WidgetTextLayers.WithStyle(layer, style with { Font = WidgetClockFont.SfProRounded, Weight = WidgetClockWeight.Light, Alignment = WidgetClockAlignment.Right, FontScale = .5 });
            Assert.Equal(style.Gradient, WidgetTextLayers.Style(edited).Gradient); Assert.Equal(original.Clock?.TwentyFourHour, edited.Clock?.TwentyFourHour);
            Assert.Equal(original.Date?.Format, edited.Date?.Format); Assert.Equal(original.Text?.Content, edited.Text?.Content);
            var copy = WidgetLayerEdits.Duplicate(design, layer.Id).Copy;
            var changed = WidgetTextLayers.WithStyle(copy, style with { Gradient = WidgetGradients.WithColour(style.Gradient!, 1, "#ABCDEF") });
            Assert.Equal("#00FF00", WidgetTextLayers.Style(layer).Gradient!.MiddleHex); Assert.Equal("#ABCDEF", WidgetTextLayers.Style(changed).Gradient!.MiddleHex);
        }
    }
    [Fact]
    public void OlderSolidRecordsOmitGradientAndInvalidEvenDisabledChoicesAreRejected()
    {
        foreach (var layer in new[] { WidgetClocks.New(), WidgetDates.New(), WidgetTexts.New() })
        {
            var json = JsonSerializer.Serialize(layer, WidgetDesignRules.Json); Assert.DoesNotContain("gradient", json);
            Assert.Null(WidgetTextLayers.Style(JsonSerializer.Deserialize<WidgetLayer>(json, WidgetDesignRules.Json)!).Gradient);
            foreach (var bad in new[] { new WidgetGradientOptions(StartHex: "#bad"), new(EndHex: null!), new(MiddleHex: "#aabbcc"), new(Direction: (WidgetGradientDirection)99), new(false, "bad") })
                Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [WidgetTextLayers.WithStyle(layer, WidgetTextLayers.Style(layer) with { Gradient = bad })] }));
        }
        foreach (var value in new[] { "123", "\"unknown\"", "null" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WidgetGradientOptions>("{\"direction\":" + value + "}", WidgetDesignRules.Json));
    }
    [Theory]
    [InlineData(WidgetGradientDirection.Horizontal, 0, .5, 1, .5)] [InlineData(WidgetGradientDirection.Vertical, .5, 0, .5, 1)] [InlineData(WidgetGradientDirection.Diagonal, 0, 0, 1, 1)]
    public void DirectionUsesFixedWidgetCoordinates(WidgetGradientDirection direction, double x1, double y1, double x2, double y2)
    { var points = WidgetGradients.Points(direction); Assert.Equal((x1, y1), points.Start); Assert.Equal((x2, y2), points.End); }
    [Fact]
    public void ThirtyNormalLiveGradientsFitTheExistingSceneBudgetAndKeepNativeTextParts()
    {
        var layers = Enumerable.Range(0, 30).Select(index => WidgetClocks.New("Clock " + index) with { Clock = new(Gradient: new()) }).ToArray();
        var design = WidgetDesignRules.New() with { Layers = layers }; var scene = WidgetScenes.Manifest(design); WidgetScenes.Validate(scene, design);
        Assert.All(scene.Parts, part => Assert.NotNull(part.Clock!.Clock!.Gradient));
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(scene, WidgetDesignRules.JsonContext.WidgetScene).Length <= WidgetScenes.MaximumBytes);
    }
}
