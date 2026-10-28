using System.Text.Json;
using ControlStudio.Core.Widgets;
using Xunit;

namespace ControlStudio.Core.Tests;

public sealed class WidgetSceneGroupingTests
{
    private static WidgetLayer Photo(string name, WidgetBlendMode mode = WidgetBlendMode.Normal) =>
        new(Guid.NewGuid(), name, Guid.NewGuid().ToString("N"), 1, 1, new(), BlendMode: mode);
    private static WidgetLayer Live(WidgetLayerKind kind) => kind switch
    {
        WidgetLayerKind.Date => WidgetDates.New(), WidgetLayerKind.Battery => WidgetBatteries.New(), _ => WidgetClocks.New()
    };

    [Theory]
    [InlineData(WidgetLayerKind.Clock, false)] [InlineData(WidgetLayerKind.Clock, true)]
    [InlineData(WidgetLayerKind.Date, false)] [InlineData(WidgetLayerKind.Date, true)]
    [InlineData(WidgetLayerKind.Battery, false)] [InlineData(WidgetLayerKind.Battery, true)]
    public void InvisibleLiveLayersCannotSeparateBlendingPhotos(WidgetLayerKind kind, bool zeroOpacity)
    {
        foreach (var size in WidgetDesignRules.SupportedSizes)
        foreach (var mode in WidgetBlending.Modes)
        {
            var bottom = Photo("Bottom"); var top = Photo("Top", mode); var text = WidgetTexts.New();
            var hidden = zeroOpacity ? Live(kind) with { Opacity = 0 } : Live(kind) with { Visible = false };
            var layers = new[] { bottom, hidden, text, top };
            var design = WidgetDesignRules.New() with { Size = size, Layers = layers };
            WidgetDesignRules.Validate(design);
            var group = Assert.Single(WidgetScenes.Steps(design));
            Assert.Null(group.TextLayer); Assert.Equal(new[] { bottom, text, top }, group.Photos);
            Assert.Equal(mode, group.Photos.Last().BlendMode); Assert.False(WidgetScenes.IsLive(design));
            Assert.Equal(layers, design.Layers); // Planning never removes or rewrites saved layers.
        }
    }

    [Theory]
    [InlineData(WidgetLayerKind.Clock)] [InlineData(WidgetLayerKind.Date)] [InlineData(WidgetLayerKind.Battery)]
    public void VisibleLiveLayersKeepTheirBoundariesAndNewManifestUsesTheSamePlan(WidgetLayerKind kind)
    {
        var bottom = Photo("Bottom"); var top = Photo("Top", WidgetBlendMode.Multiply);
        var visible = Live(kind); var hidden = Live(kind) with { Visible = false };
        var design = WidgetDesignRules.New() with { Layers = [bottom, hidden, top, visible, bottom with { Id = Guid.NewGuid() }] };
        var steps = WidgetScenes.Steps(design); Assert.Equal(3, steps.Count);
        Assert.Equal(new[] { bottom, top }, steps[0].Photos); Assert.Equal(visible, steps[1].TextLayer);
        Assert.Single(steps[2].Photos);
        var scene = WidgetScenes.Manifest(design); Assert.Equal(2, scene.GroupingVersion);
        Assert.Equal(new int?[] { 0, null, 2 }, scene.Parts.Select(part => part.ImageIndex));
        var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(scene, WidgetDesignRules.JsonContext.WidgetScene), WidgetDesignRules.JsonContext.WidgetScene)!;
        WidgetScenes.Validate(restored, design);
    }

    [Fact]
    public void SharedGroupingFixtureMergesPhotosAroundHiddenClocksWithoutLosingTheVisibleClock()
    {
        var scene = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-scene-grouping-v2.example.json")), WidgetDesignRules.JsonContext.WidgetScene)!;
        var hidden = WidgetClocks.New() with { Visible = false, Locked = true };
        var design = WidgetDesignRules.New() with { Layers = [Photo("Bottom"), hidden, Photo("Top", WidgetBlendMode.Multiply), scene.Parts[1].Clock!] };
        Assert.Equal(2, scene.GroupingVersion); WidgetScenes.Validate(scene, design);
        Assert.Equal(WidgetScenes.Manifest(design).Parts, scene.Parts);
        Assert.Equal(2, WidgetScenes.Steps(design)[0].Photos.Count);
    }

    [Theory]
    [InlineData(WidgetLayerKind.Clock, 1)] [InlineData(WidgetLayerKind.Date, 2)] [InlineData(WidgetLayerKind.Battery, 3)]
    public void MissingVersionReadsOldBoundariesButRelabelingOldOrNewGroupsIsRejected(WidgetLayerKind kind, int schema)
    {
        var bottom = Photo("Bottom"); var top = Photo("Top", WidgetBlendMode.Multiply);
        var hidden = Live(kind) with { Opacity = 0 }; var visible = Live(kind);
        var design = WidgetDesignRules.New() with { Layers = [bottom, hidden, top, visible] };
        var livePart = kind switch { WidgetLayerKind.Date => new WidgetScenePart(Date: visible), WidgetLayerKind.Battery => new WidgetScenePart(Battery: visible), _ => new WidgetScenePart(Clock: visible) };
        var legacy = new WidgetScene(schema, 170, 170, [new(0), new(1), livePart]);
        var node = JsonSerializer.SerializeToElement(legacy, WidgetDesignRules.JsonContext.WidgetScene);
        var fields = node.EnumerateObject().Where(property => property.Name != "groupingVersion").ToDictionary(property => property.Name, property => property.Value.Clone());
        var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(fields), WidgetDesignRules.JsonContext.WidgetScene)!;
        Assert.Equal(1, restored.GroupingVersion); WidgetScenes.Validate(restored, design);
        Assert.Throws<IOException>(() => WidgetScenes.Validate(restored with { GroupingVersion = 2 }, design));
        var current = WidgetScenes.Manifest(design); WidgetScenes.Validate(current, design);
        Assert.Throws<IOException>(() => WidgetScenes.Validate(current with { GroupingVersion = 1 }, design));
        foreach (var unknown in new[] { -1, 0, 3, int.MaxValue })
            Assert.Throws<IOException>(() => WidgetScenes.Validate(current with { GroupingVersion = unknown }, design));
        Assert.Equal(new[] { bottom, hidden, top, visible }, design.Layers);
    }

    [Theory]
    [InlineData("null")] [InlineData("\"2\"")] [InlineData("true")] [InlineData("2.5")]
    public void GroupingVersionHasTheSameStrictIntegerContractAsNative(string invalid)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "widget-scene-grouping-v2.example.json"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json.Replace("\"groupingVersion\": 2", "\"groupingVersion\": " + invalid), WidgetDesignRules.JsonContext.WidgetScene));
    }
}
