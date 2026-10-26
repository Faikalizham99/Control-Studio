using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetBatteryTests
{
    [Fact]
    public void SharedNativeFixtureDecodesAsALiveBatteryScene()
    {
        var scene = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-scene-v3-battery.example.json")), WidgetDesignRules.JsonContext.WidgetScene)!;
        var design = WidgetDesignRules.New() with { Layers = [scene.Parts.Single().Battery!] }; WidgetScenes.Validate(scene, design);
        Assert.Equal("#808000", WidgetBatteries.Colour(design.Layers[0].Battery!, new(25)));
    }
    [Theory]
    [InlineData(0, "#FF453A")] [InlineData(20, "#FF453A")] [InlineData(21, "#FF9F0A")]
    [InlineData(50, "#FF9F0A")] [InlineData(51, "#0A84FF")] [InlineData(80, "#0A84FF")]
    [InlineData(81, "#30D158")] [InlineData(100, "#30D158")]
    public void RangeBoundariesSelectExactlyOneColour(int percent, string expected)
        => Assert.Equal(expected, WidgetBatteries.Colour(WidgetBatteries.Defaults(), new(percent)));
    [Fact]
    public void SmoothStopsInterpolateRgbAndClampPastTheFinalStop()
    {
        var options = WidgetBatteries.Defaults() with { ColourMode = WidgetBatteryColourMode.Smooth,
            Stops = [new(0, "#FF0000"), new(50, "#00FF00"), new(80, "#0000FF")] };
        Assert.Equal("#FF0000", WidgetBatteries.Colour(options, new(0)));
        Assert.Equal("#808000", WidgetBatteries.Colour(options, new(25)));
        Assert.Equal("#00FF00", WidgetBatteries.Colour(options, new(50)));
        Assert.Equal("#008080", WidgetBatteries.Colour(options, new(65)));
        Assert.Equal("#0000FF", WidgetBatteries.Colour(options, new(100)));
        foreach (var value in new int?[] { null, -1, 101 })
        { var reading = new WidgetBatteryReading(value, true, true); Assert.False(reading.Known); Assert.Equal("—", reading.Text); Assert.Equal("#8E8E93", WidgetBatteries.Colour(options, reading)); }
        Assert.Equal("0%", new WidgetBatteryReading(0).Text);
    }
    [Fact]
    public void SettingsRoundTripAsValuesAndCopiesKeepIndependentColourEdits()
    {
        foreach (var shape in Enum.GetValues<WidgetBatteryShape>())
        foreach (var mode in Enum.GetValues<WidgetBatteryColourMode>())
        {
            var layer = WidgetBatteries.New() with { Battery = WidgetBatteries.Defaults() with { Shape = shape, ColourMode = mode, ShowCharging = false, ShowPercentage = false }, RotationDegrees = 32, Opacity = .7 };
            var design = WidgetDesignRules.New() with { Layers = [layer] }; WidgetDesignRules.Validate(design);
            var json = JsonSerializer.Serialize(design, WidgetDesignRules.JsonContext.WidgetDesign);
            var restored = JsonSerializer.Deserialize(json, WidgetDesignRules.JsonContext.WidgetDesign)!;
            Assert.Equal(layer, restored.Layers.Single()); Assert.Equal(layer.Battery!.GetHashCode(), restored.Layers[0].Battery!.GetHashCode());
            Assert.DoesNotContain("charging\"", json); Assert.DoesNotContain("percent\"", json);
            var scene = JsonSerializer.Deserialize(JsonSerializer.Serialize(WidgetScenes.Manifest(design), WidgetDesignRules.JsonContext.WidgetScene), WidgetDesignRules.JsonContext.WidgetScene)!;
            WidgetScenes.Validate(scene, restored);
            var copy = WidgetLayerEdits.Duplicate(restored, layer.Id).Copy; var stops = copy.Battery!.Stops.ToArray(); stops[1] = new(25, "#123456");
            copy = copy with { Battery = copy.Battery with { Stops = stops } };
            Assert.NotEqual(copy.Id, layer.Id); Assert.Equal(21, layer.Battery.Stops[1].FromPercent); Assert.Equal("#FF9F0A", layer.Battery.Stops[1].ColourHex);
        }
    }
    [Fact]
    public void InvalidStopsEnumsAndMixedPayloadsNeverValidate()
    {
        var options = WidgetBatteries.Defaults();
        var invalid = new[] { options with { Shape = (WidgetBatteryShape)99 }, options with { ColourMode = (WidgetBatteryColourMode)99 },
            options with { Stops = null! }, options with { Stops = [] }, options with { Stops = [new(0, "#FFFFFF")] },
            options with { Stops = [new(1, "#FFFFFF"), new(50, "#000000")] }, options with { Stops = [new(0, "#FFFFFF"), new(0, "#000000")] },
            options with { Stops = [new(0, "#FFFFFF"), new(101, "#000000")] }, options with { Stops = [new(0, "#ffffff"), new(100, "#000000")] },
            options with { Stops = [new(0, "#FFFFFF"), null!] }, options with { Stops = Enumerable.Range(0, 9).Select(x => new WidgetBatteryColourStop(x, "#FFFFFF")).ToArray() } };
        foreach (var value in invalid) Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [WidgetBatteries.New() with { Battery = value }] }));
        foreach (var layer in new[] { WidgetClocks.New(), WidgetDates.New(), WidgetTexts.New(), new WidgetLayer(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 1, 1, new()) })
            Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [layer with { Battery = options }] }));
        Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [WidgetBatteries.New() with { Clock = new() }] }));
    }
    [Fact]
    public void BatterySeparatesStaticGroupsAndKeepsClockDateCompatibilityAndThirtyLayerBudget()
    {
        var text = WidgetTexts.New(); var battery = WidgetBatteries.New(); var clock = WidgetClocks.New(); var date = WidgetDates.New();
        var design = WidgetDesignRules.New() with { Layers = [text, battery, text with { Id = Guid.NewGuid() }, clock, date] };
        var scene = WidgetScenes.Manifest(design); Assert.Equal(3, scene.SchemaVersion); Assert.Equal(5, scene.Parts.Count);
        Assert.Equal(0, scene.Parts[0].ImageIndex); Assert.Equal(battery, scene.Parts[1].Battery); Assert.Equal(2, scene.Parts[2].ImageIndex);
        Assert.Equal(clock, scene.Parts[3].Clock); Assert.Equal(date, scene.Parts[4].Date);
        var hidden = WidgetDesignRules.New() with { Layers = [battery with { Visible = false }, date] }; Assert.Equal(2, WidgetScenes.Manifest(hidden).SchemaVersion);
        Assert.False(WidgetScenes.IsLive(hidden with { Layers = [battery with { Opacity = 0 }] }));
        var many = design with { Layers = Enumerable.Range(0, 30).Select(i => WidgetBatteries.New("Battery " + i)).ToArray() };
        WidgetDesignRules.Validate(many); WidgetScenes.Validate(WidgetScenes.Manifest(many), many);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(WidgetScenes.Manifest(many), WidgetDesignRules.JsonContext.WidgetScene).Length <= WidgetScenes.MaximumBytes);
    }
    [Fact]
    public void BatteryPinchResetAndLockUseVectorGeometryWithoutPhotoTransforms()
    {
        var layer = WidgetBatteries.New(); var frame = layer.Frame; var scaled = WidgetLayerEdits.ScaleCentered(layer, 2);
        Assert.Equal(frame.X + frame.Width / 2, scaled.Frame.X + scaled.Frame.Width / 2, 10);
        Assert.Equal(frame.Y + frame.Height / 2, scaled.Frame.Y + scaled.Frame.Height / 2, 10);
        Assert.Equal(layer, WidgetLayerEdits.Rotate(layer, 1));
        var locked = layer with { Locked = true }; Assert.Equal(locked, WidgetLayerEdits.ScaleCentered(locked, 2)); Assert.Equal(locked, WidgetLayerEdits.Reset(locked, WidgetSize.Small));
        Assert.Equal(layer, WidgetLayerEdits.Reset(scaled with { RotationDegrees = 30, Opacity = .1 }, WidgetSize.Small));
    }
}
