using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetClockTests
{
    private static WidgetLayer Photo() => new(Guid.NewGuid(), "Photo", Guid.NewGuid().ToString("N"), 640, 480, new());
    [Fact]
    public void PhotoAndClockKindsAndAllClockOptionsRoundTripInEveryFamily()
    {
        var clock = WidgetClocks.New() with { Locked = true, Opacity = .6, Frame = new(-.2, .3, 1.4, .3) };
        foreach (var size in WidgetDesignRules.SupportedSizes)
        foreach (var font in Enum.GetValues<WidgetClockFont>())
        foreach (var alignment in Enum.GetValues<WidgetClockAlignment>())
        foreach (var showAmPm in new[] { true, false })
        {
            var layer = clock with { Clock = clock.Clock! with { TwentyFourHour = false, ShowSeconds = false, Font = font, Alignment = alignment, ColourHex = "#12ABEF", FontScale = .5, ShowAmPm = showAmPm } };
            var design = WidgetDesignRules.New() with { Size = size, Layers = [Photo(), layer] }; WidgetDesignRules.Validate(design);
            var json = JsonSerializer.Serialize(design, WidgetDesignRules.JsonContext.WidgetDesign);
            var restored = JsonSerializer.Deserialize(json, WidgetDesignRules.JsonContext.WidgetDesign)!;
            Assert.Equal(design.Layers, restored.Layers); Assert.Contains("\"kind\":\"clock\"", json);
            using var parsed = JsonDocument.Parse(json); Assert.Equal(JsonValueKind.String, parsed.RootElement.GetProperty("layers")[1].GetProperty("clock").GetProperty("font").ValueKind);
        }
    }
    [Theory]
    [InlineData(0, 0, 0, true, true, true, "00:00:00")] [InlineData(12, 0, 3, false, true, true, "12:00:03 PM")]
    [InlineData(0, 5, 9, false, true, true, "12:05:09 AM")] [InlineData(23, 59, 59, true, false, true, "23:59")]
    [InlineData(13, 8, 12, false, false, true, "1:08 PM")]
    [InlineData(0, 5, 9, false, true, false, "12:05:09")] [InlineData(12, 0, 3, false, true, false, "12:00:03")]
    [InlineData(13, 8, 12, false, false, false, "1:08")] [InlineData(0, 5, 9, false, false, false, "12:05")]
    [InlineData(23, 59, 59, true, false, false, "23:59")] [InlineData(0, 0, 0, true, true, false, "00:00:00")]
    public void ClockFormatsMidnightNoonSecondsAndHourCycles(int hour, int minute, int second, bool twentyFour, bool seconds, bool showAmPm, string expected)
        => Assert.Equal(expected, WidgetClocks.Text(new(TwentyFourHour: twentyFour, ShowSeconds: seconds, ShowAmPm: showAmPm), new(2026, 10, 24, hour, minute, second, TimeSpan.Zero), TimeZoneInfo.Utc));
    [Fact]
    public void ClockUsesTheChosenTimeZoneRatherThanAnElapsedTimer()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("ClockTest", TimeSpan.FromHours(8), "ClockTest", "ClockTest");
        Assert.Equal("00:00:01", WidgetClocks.Text(new(), new(2026, 10, 24, 16, 0, 1, TimeSpan.Zero), zone));
        Assert.Equal("08:00:01", WidgetClocks.Text(new(), new(2026, 10, 25, 0, 0, 1, TimeSpan.Zero), zone));
    }
    [Fact]
    public void InvalidClockFieldsAndPhotoClockConfusionAreRejected()
    {
        var clock = WidgetClocks.New(); var photo = Photo();
        foreach (var invalid in new[] { clock with { Clock = null }, clock with { AssetKey = photo.AssetKey }, clock with { PixelWidth = 0 },
            clock with { FitPhoto = true }, clock with { Zoom = 2 }, clock with { FlipHorizontal = true }, clock with { QuarterTurns = 1 },
            clock with { BlendMode = WidgetBlendMode.Multiply }, clock with { Clock = new(Font: (WidgetClockFont)99) },
            clock with { Clock = new(Alignment: (WidgetClockAlignment)99) }, clock with { Clock = new(ColourHex: "#bad") },
            clock with { Clock = new(FontScale: double.NaN) }, clock with { Clock = new(FontScale: 0) }, photo with { Clock = new() } })
            Assert.Throws<IOException>(() => WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [invalid] }));
        var json = JsonSerializer.Serialize(clock, WidgetDesignRules.Json);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WidgetLayer>(json.Replace("\"rounded\"", "42"), WidgetDesignRules.Json));
        foreach (var invalid in new[] { "null", "\"false\"" })
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WidgetLayer>(json.Replace("\"showAmPm\":true", "\"showAmPm\":" + invalid), WidgetDesignRules.Json));
    }
    [Fact]
    public void SceneGroupingKeepsStackOrderAndHidingClocksDoesNotChangePhotoGroups()
    {
        var bottom = Photo(); var top = Photo(); var clock = WidgetClocks.New(); var next = WidgetClocks.New("Clock 2");
        var design = WidgetDesignRules.New() with { Layers = [bottom, clock, top, next] };
        var steps = WidgetScenes.Steps(design); Assert.Equal(4, steps.Count); Assert.Equal(bottom, steps[0].Photos.Single());
        Assert.Equal(clock, steps[1].Clock); Assert.Equal(top, steps[2].Photos.Single()); Assert.Equal(next, steps[3].Clock);
        var scene = WidgetScenes.Manifest(design); WidgetScenes.Validate(scene, design);
        Assert.Equal(new int?[] { 0, null, 2, null }, scene.Parts.Select(x => x.ImageIndex));
        Assert.Throws<IOException>(() => WidgetScenes.Validate(scene with { Parts = scene.Parts.Reverse().ToArray() }, design));
        var hidden = design with { Layers = [bottom, clock with { Visible = false }, top, next with { Opacity = 0 }] };
        Assert.False(WidgetScenes.IsLive(hidden)); Assert.Equal(2, WidgetScenes.Steps(hidden).Count);
        Assert.Equal(bottom, WidgetScenes.Steps(hidden)[0].Photos.Single()); Assert.Equal(top, WidgetScenes.Steps(hidden)[1].Photos.Single());
    }
    [Fact]
    public void SharedSceneFixtureDecodesWithoutPhotoAssetsForTheClock()
    {
        var scene = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-scene-v1.example.json")), WidgetDesignRules.JsonContext.WidgetScene)!;
        var clock = scene.Parts[1].Clock!; WidgetDesignRules.Validate(WidgetDesignRules.New() with { Layers = [clock] });
        Assert.True(clock.Clock!.ShowAmPm); // This older scene fixture omits the setting.
        Assert.Equal("", clock.AssetKey); Assert.Equal(WidgetLayerKind.Clock, clock.Kind);
        WidgetScenes.Validate(scene, WidgetDesignRules.New() with { Layers = [Photo(), clock] });
        var locked = clock with { Locked = true }; Assert.Same(locked, WidgetLayerEdits.Move(locked, .5, .5));
        Assert.Same(clock, WidgetLayerEdits.Rotate(clock, 1, WidgetSize.Small));
        Assert.NotEqual(clock.Frame, WidgetLayerEdits.Scale(clock, 2).Frame);
    }
}
